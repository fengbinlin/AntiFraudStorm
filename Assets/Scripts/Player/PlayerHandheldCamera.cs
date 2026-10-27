using System.Collections;
using Animancer;
using AntiFraudStorm.UI;
using UnityEngine;

namespace AntiFraudStorm.Player
{
    /// <summary>
    /// C-key handheld prop camera:
    /// rest under Hips → on C: pulse + FX + world lerp to hand pose → parent RightHand → FP →
    /// put away anim finishes then restore to Hips.
    /// </summary>
    public class PlayerHandheldCamera : MonoBehaviour
    {
        const string RightHandBoneName = "QuickRigCharacter2_RightHand";
        const string RestBoneName = "QuickRigCharacter2_Hips";

        [Header("References")]
        [SerializeField] Transform propCamera;
        [SerializeField] Transform handBone;
        [SerializeField] Transform restParent;
        [SerializeField] AnimancerComponent animancer;
        [SerializeField] ThirdPersonSpringArm springArm;
        [SerializeField] PlayerAnimancerLocomotion locomotion;
        [SerializeField] GameObject attachFlashPrefab;
        [SerializeField] CameraViewfinderController viewfinderUi;

        [Header("Clips")]
        [SerializeField] AnimationClip pickupClip;
        [SerializeField] AnimationClip putAwayClip;

        [Header("Hand Pose (local to RightHand)")]
        [SerializeField] Vector3 handLocalPosition = new Vector3(0.232f, 0.017f, -0.141f);
        [SerializeField] Vector3 handLocalEuler = new Vector3(325.926331f, 37.4378052f, 154.730164f);
        [Tooltip("World-space blend into hand pose, then change parent.")]
        [SerializeField] float attachTimeSeconds = 0.2f;
        [SerializeField] float actionFade = 0.12f;

        [Header("Prop Scale Feedback")]
        [SerializeField] float scalePulseDuration = 0.2f;
        [SerializeField] float scalePulsePeak = 1.65f;
        [SerializeField] int scalePulseBounces = 1;

        Vector3 _restLocalPos;
        Quaternion _restLocalRot;
        Vector3 _restLocalScale;
        bool _holding;
        bool _busy;
        Coroutine _scalePulseRoutine;
        Coroutine _handBlendRoutine;

        public bool IsHolding => _holding;
        public bool IsBusy => _busy;

        public void Initialize(
            AnimancerComponent animancerComponent,
            ThirdPersonSpringArm arm,
            PlayerAnimancerLocomotion loco)
        {
            animancer = animancerComponent;
            springArm = arm;
            locomotion = loco;
            AutoBindIfNeeded();
            CaptureRestPose();
        }

        public void SetClips(AnimationClip pickup, AnimationClip putAway)
        {
            pickupClip = pickup;
            putAwayClip = putAway;
        }

        public bool TryToggle()
        {
            AutoBindIfNeeded();
            if (_busy || propCamera == null || handBone == null || animancer == null)
                return false;

            if (_holding)
                BeginPutAway();
            else
                BeginPickup();
            return true;
        }

        void AutoBindIfNeeded()
        {
            restParent = FindNamedTransform(RestBoneName);
            handBone = FindNamedTransform(RightHandBoneName);
            propCamera = FindPropCameraUnderHips() ?? propCamera ?? FindAnyPropCamera();
        }

        Transform FindNamedTransform(string exactName)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == exactName)
                    return t;
            }
            return null;
        }

        Transform FindPropCameraUnderHips()
        {
            if (restParent == null)
                restParent = FindNamedTransform(RestBoneName);
            if (restParent == null)
                return null;

            foreach (Transform child in restParent)
            {
                if (child.name == "Camera" && child.GetComponent<Camera>() == null)
                    return child;
            }

            // Nested under hips.
            foreach (var t in restParent.GetComponentsInChildren<Transform>(true))
            {
                if (t == restParent)
                    continue;
                if (t.name == "Camera" && t.GetComponent<Camera>() == null)
                    return t;
            }

            return null;
        }

        Transform FindAnyPropCamera()
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "Camera")
                    continue;
                if (t.GetComponent<Camera>() != null)
                    continue;
                return t;
            }
            return null;
        }

        void CaptureRestPose()
        {
            if (propCamera == null)
                return;

            if (restParent == null)
                restParent = FindNamedTransform(RestBoneName) ?? propCamera.parent;

            if (restParent != null && propCamera.parent != restParent)
                propCamera.SetParent(restParent, true);

            _restLocalPos = propCamera.localPosition;
            _restLocalRot = propCamera.localRotation;
            _restLocalScale = propCamera.localScale.sqrMagnitude > 0.0001f
                ? propCamera.localScale
                : Vector3.one;
        }

        void BeginPickup()
        {
            _busy = true;
            locomotion?.NotifyActionStarted();

            if (!_holding && propCamera != null && restParent != null && propCamera.parent == restParent)
                CaptureRestPose();

            // Feedback + blend start the instant C is pressed; parent swaps only after arrive.
            StartScalePulse();
            SpawnAttachFlashUnderProp();
            StartBlendToHandThenAttach();

            var clip = pickupClip;
            if (clip == null)
            {
                StartCoroutine(FinishPickupAfterBlendRoutine());
                return;
            }

            var state = animancer.Play(clip, actionFade, FadeMode.FromStart);
            var events = state.Events(this);
            events.Clear();
            events.OnEnd = () =>
            {
                EnsureAttachedToHand();
                EnterFirstPerson();
                _holding = true;
                _busy = false;
                locomotion?.NotifyActionEnded();
            };
        }

        IEnumerator FinishPickupAfterBlendRoutine()
        {
            while (_handBlendRoutine != null)
                yield return null;

            EnsureAttachedToHand();
            EnterFirstPerson();
            _holding = true;
            _busy = false;
            locomotion?.NotifyActionEnded();
        }

        void SpawnAttachFlashUnderProp()
        {
            if (attachFlashPrefab == null || propCamera == null)
                return;

            var fx = Instantiate(attachFlashPrefab, propCamera);
            fx.transform.localPosition = Vector3.zero;
            fx.transform.localRotation = Quaternion.identity;
            fx.transform.localScale = Vector3.one * 0.003f;

            float life = 2f;
            var systems = fx.GetComponentsInChildren<ParticleSystem>(true);
            if (systems != null && systems.Length > 0)
            {
                life = 0.5f;
                foreach (var ps in systems)
                {
                    var main = ps.main;
                    float d = main.duration + main.startLifetime.constantMax;
                    if (d > life)
                        life = d;
                }
            }
            Destroy(fx, life + 0.25f);
        }

        void BeginPutAway()
        {
            _busy = true;
            locomotion?.NotifyActionStarted();
            StopHandBlend();
            StopScalePulse();
            SetViewfinderVisible(false);

            springArm?.SetMode(CameraViewMode.ThirdPerson, instant: true, keepVisibleRoot: null);
            SetPropVisible(true);

            if (propCamera != null && handBone != null && propCamera.parent == handBone)
                ApplyHandBasePose();

            var clip = putAwayClip;
            if (clip == null)
            {
                RestoreToRest();
                PlayPutAwayScalePulse();
                _holding = false;
                _busy = false;
                locomotion?.NotifyActionEnded();
                return;
            }

            var state = animancer.Play(clip, actionFade, FadeMode.FromStart);
            var events = state.Events(this);
            events.Clear();
            // Wait until put-away animation finishes, then restore to Hips.
            events.OnEnd = () =>
            {
                RestoreToRest();
                PlayPutAwayScalePulse();
                _holding = false;
                _busy = false;
                locomotion?.NotifyActionEnded();
            };
        }

        void StartBlendToHandThenAttach()
        {
            StopHandBlend();
            if (propCamera == null || handBone == null)
                return;
            _handBlendRoutine = StartCoroutine(BlendToHandThenAttachRoutine());
        }

        void StopHandBlend()
        {
            if (_handBlendRoutine == null)
                return;
            StopCoroutine(_handBlendRoutine);
            _handBlendRoutine = null;
        }

        IEnumerator BlendToHandThenAttachRoutine()
        {
            if (propCamera == null || handBone == null)
            {
                _handBlendRoutine = null;
                yield break;
            }

            SetPropVisible(true);
            // Free from rest bone so hips motion does not drag the blend.
            propCamera.SetParent(null, true);

            Vector3 startPos = propCamera.position;
            Quaternion startRot = propCamera.rotation;
            Quaternion handLocalRot = Quaternion.Euler(handLocalEuler);
            float duration = Mathf.Max(0.01f, attachTimeSeconds);
            float t = 0f;

            while (t < duration)
            {
                if (propCamera == null || handBone == null)
                {
                    _handBlendRoutine = null;
                    yield break;
                }

                t += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                Vector3 targetPos = handBone.TransformPoint(handLocalPosition);
                Quaternion targetRot = handBone.rotation * handLocalRot;
                propCamera.position = Vector3.Lerp(startPos, targetPos, u);
                propCamera.rotation = Quaternion.Slerp(startRot, targetRot, u);
                yield return null;
            }

            _handBlendRoutine = null;
            ParentToHandPreservePulse();
        }

        void EnsureAttachedToHand()
        {
            StopHandBlend();
            if (propCamera == null || handBone == null)
                return;
            if (propCamera.parent == handBone)
                return;
            ParentToHandPreservePulse();
        }

        void ParentToHandPreservePulse()
        {
            if (propCamera == null || handBone == null)
                return;

            bool keepPulse = _scalePulseRoutine != null;
            propCamera.SetParent(handBone, false);
            ApplyHandBasePose(preserveScale: keepPulse);
            SetPropVisible(true);
        }

        void ApplyHandBasePose(bool preserveScale = false)
        {
            propCamera.localPosition = handLocalPosition;
            propCamera.localRotation = Quaternion.Euler(handLocalEuler);
            if (preserveScale)
                return;

            propCamera.localScale = _restLocalScale.sqrMagnitude > 0.0001f
                ? _restLocalScale
                : Vector3.one;
        }

        void PlayPutAwayScalePulse()
        {
            if (propCamera == null || !propCamera.gameObject.activeInHierarchy)
                return;
            StartScalePulse();
        }

        void StartScalePulse()
        {
            StopScalePulse();
            _scalePulseRoutine = StartCoroutine(ScalePulseRoutine());
        }

        void StopScalePulse()
        {
            if (_scalePulseRoutine != null)
            {
                StopCoroutine(_scalePulseRoutine);
                _scalePulseRoutine = null;
            }

            if (propCamera == null)
                return;

            Vector3 baseScale = _restLocalScale.sqrMagnitude > 0.0001f ? _restLocalScale : Vector3.one;
            if (propCamera.parent == handBone || propCamera.parent == restParent)
                propCamera.localScale = baseScale;
        }

        IEnumerator ScalePulseRoutine()
        {
            Vector3 baseScale = _restLocalScale.sqrMagnitude > 0.0001f ? _restLocalScale : Vector3.one;
            float duration = Mathf.Max(0.05f, scalePulseDuration);
            float t = 0f;
            int bounces = Mathf.Max(1, scalePulseBounces);

            while (t < duration)
            {
                if (propCamera == null || !propCamera.gameObject.activeInHierarchy)
                    yield break;

                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / duration);
                // Stronger interactive pulse: big overshoot then settle.
                float wave = Mathf.Abs(Mathf.Sin(u * Mathf.PI * bounces));
                float envelope = Mathf.Pow(1f - u, 0.65f);
                float mul = Mathf.Lerp(1f, scalePulsePeak, wave * envelope);
                propCamera.localScale = baseScale * mul;
                yield return null;
            }

            if (propCamera != null)
                propCamera.localScale = baseScale;
            _scalePulseRoutine = null;
        }

        void RestoreToRest()
        {
            if (propCamera == null)
                return;

            StopScalePulse();

            if (restParent == null)
                restParent = FindNamedTransform(RestBoneName);

            if (restParent != null)
                propCamera.SetParent(restParent, false);

            propCamera.localPosition = _restLocalPos;
            propCamera.localRotation = _restLocalRot;
            propCamera.localScale = _restLocalScale.sqrMagnitude > 0.0001f ? _restLocalScale : Vector3.one;
            SetPropVisible(true);
        }

        void EnterFirstPerson()
        {
            StopScalePulse();
            springArm?.SetMode(
                CameraViewMode.FirstPerson,
                instant: true,
                hideRenderersOverride: true,
                keepVisibleRoot: null);
            SetPropVisible(false);
            SetViewfinderVisible(true);
        }

        void SetViewfinderVisible(bool visible)
        {
            if (viewfinderUi == null)
                viewfinderUi = CameraViewfinderController.Instance;
            if (viewfinderUi == null)
                return;

            if (visible)
                viewfinderUi.Show();
            else
                viewfinderUi.Hide();
        }

        void SetPropVisible(bool visible)
        {
            if (propCamera == null)
                return;
            propCamera.gameObject.SetActive(visible);
        }
    }
}
