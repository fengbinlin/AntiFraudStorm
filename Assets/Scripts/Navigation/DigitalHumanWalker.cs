using System.Collections;
using Animancer;
using Layer_lab._3D_Casual_Character;
using Pathfinding;
using UnityEngine;

namespace AntiFraudStorm.Navigation
{
    /// <summary>
    /// Switches a randomized digital human between walk and idle based on RichAI velocity.
    /// </summary>
    public class DigitalHumanWalker : MonoBehaviour
    {
        const float HeadParticleHeight = 1.90f;

        AnimancerComponent _animancer;
        AnimationClip _idleClip;
        AnimationClip _walkClip;
        AnimationClip _agonizeClip;
        AnimationClip _currentClip;
        CharacterBase _appearance;
        GameObject _headParticlePrefab;
        GameObject _headParticle;
        GameObject _cheerParticlePrefab;
        GameObject _cheerParticle;
        GameObject _cheerLabel;
        Font _cheerLabelFont;
        Coroutine _cheerLabelRoutine;
        float _headParticleScale = 0.1f;
        IAstarAI _ai;
        Coroutine _restoreRoutine;
        bool _initialized;
        bool _bewitched;
        bool _isWalking;
        bool _isRescuing;

        public bool IsBewitched => _bewitched;
        public bool IsRescuing => _isRescuing;

        public void Initialize(
            AnimancerComponent animancer,
            AnimationClip idleClip,
            AnimationClip walkClip,
            AnimationClip agonizeClip,
            CharacterBase appearance,
            GameObject headParticlePrefab,
            float headParticleScale,
            GameObject cheerParticlePrefab,
            Font cheerLabelFont)
        {
            _animancer = animancer;
            _idleClip = idleClip;
            _walkClip = walkClip;
            _agonizeClip = agonizeClip;
            _appearance = appearance;
            _headParticlePrefab = headParticlePrefab;
            _headParticleScale = Mathf.Max(0.01f, headParticleScale);
            _cheerParticlePrefab = cheerParticlePrefab;
            _cheerLabelFont = cheerLabelFont;
            _initialized = true;
            Play(idleClip, true);
        }

        void Awake()
        {
            _ai = GetComponent<IAstarAI>();
        }

        void OnEnable()
        {
            if (_initialized)
                RestoreAfterCampusActivation();
        }

        void OnDisable()
        {
            if (_restoreRoutine != null)
            {
                StopCoroutine(_restoreRoutine);
                _restoreRoutine = null;
            }
        }

        void Update()
        {
            if (_bewitched ||
                _ai == null ||
                _animancer == null ||
                _idleClip == null ||
                _walkClip == null)
                return;

            Vector3 planarVelocity = Vector3.ProjectOnPlane(_ai.velocity, Vector3.up);
            bool walking = planarVelocity.sqrMagnitude > 0.025f;
            if (walking == _isWalking)
                return;

            _isWalking = walking;
            Play(walking ? _walkClip : _idleClip, false);
        }

        public void EnterScamState(Transform flyer)
        {
            if (_bewitched)
                return;

            _bewitched = true;
            _isWalking = false;

            if (_ai != null)
            {
                _ai.isStopped = true;
                _ai.updateRotation = false;
            }

            if (flyer != null)
            {
                Vector3 direction = flyer.position - transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }

            // Face_A1..A5 occupy indexes 0..4, so A3 is index 2.
            _appearance?.SetItem(PartsType.Face, 2);
            Play(_agonizeClip, true);
            SpawnHeadParticle();
        }

        public IEnumerator PlayRescueCelebration(
            AnimationClip cheerClip,
            Transform player)
        {
            if (!_bewitched || _isRescuing)
                yield break;

            _isRescuing = true;
            yield return null;

            HideHeadParticle();
            SpawnCheerParticle();
            CreateCheerLabel();
            _appearance?.SetItem(PartsType.Face, 0);

            if (player != null)
            {
                Vector3 direction = player.position - transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.LookRotation(
                        direction.normalized,
                        Vector3.up);
                }
            }

            Play(cheerClip, true);
            if (cheerClip != null)
                yield return new WaitForSeconds(cheerClip.length);
            else
                yield return new WaitForSeconds(0.8f);

            ClearCheerEffects();
            _bewitched = false;
            _isRescuing = false;
            if (_ai != null)
            {
                _ai.isStopped = false;
                _ai.updateRotation = true;
            }

            Play(_idleClip, true);
        }

        /// <summary>
        /// The campus roots are disabled while the desktop scene is active. Rebuild
        /// the Animator and replay the current state after those roots wake up.
        /// </summary>
        public void RestoreAfterCampusActivation()
        {
            if (!_initialized || !isActiveAndEnabled)
                return;

            if (_restoreRoutine != null)
                StopCoroutine(_restoreRoutine);
            _restoreRoutine = StartCoroutine(RestoreAnimationNextFrame());
        }

        IEnumerator RestoreAnimationNextFrame()
        {
            // Wait until Animancer and the Animator on the child hierarchy are active.
            yield return null;
            _restoreRoutine = null;

            if (_animancer == null || _currentClip == null)
                yield break;

            Animator animator = _animancer.Animator;
            if (animator != null)
            {
                animator.enabled = true;
                animator.Rebind();
                animator.Update(0f);
            }

            AnimancerState state = _animancer.Play(
                _currentClip,
                0.08f,
                FadeMode.FromStart);
            state.Speed = 1f;

            if (_bewitched)
                PlayHeadParticle();
        }

        void Play(AnimationClip clip, bool fromStart)
        {
            if (_animancer == null || clip == null)
                return;

            _currentClip = clip;
            _isWalking = clip == _walkClip;
            AnimancerState state = fromStart
                ? _animancer.Play(clip, 0.15f, FadeMode.FromStart)
                : _animancer.Play(clip, 0.18f);
            state.Speed = 1f;
        }

        void SpawnHeadParticle()
        {
            if (_headParticle != null)
            {
                PlayHeadParticle();
                return;
            }

            if (_headParticlePrefab == null)
                return;

            // Keep this independent of the animated head bone. The agent root only
            // changes yaw, so the fixed height remains stable while the character bends.
            _headParticle = Instantiate(_headParticlePrefab, transform);
            _headParticle.name = "Bewitched_Sparkle";
            _headParticle.transform.localPosition = Vector3.up * HeadParticleHeight;
            _headParticle.transform.localRotation = Quaternion.identity;
            _headParticle.transform.localScale = Vector3.one * _headParticleScale;
            PlayHeadParticle();
        }

        void HideHeadParticle()
        {
            if (_headParticle == null)
                return;

            ParticleSystem[] systems =
                _headParticle.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] != null)
                {
                    systems[i].Stop(
                        true,
                        ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            Destroy(_headParticle);
            _headParticle = null;
        }

        void SpawnCheerParticle()
        {
            if (_cheerParticlePrefab == null)
                return;

            _cheerParticle = Instantiate(_cheerParticlePrefab, transform);
            _cheerParticle.name = "Rescue_ShinePink";
            _cheerParticle.transform.localPosition =
                Vector3.up * 1.75f;
            _cheerParticle.transform.localRotation = Quaternion.identity;
            _cheerParticle.transform.localScale = Vector3.one * 0.15f;

            ParticleSystem[] systems =
                _cheerParticle.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] != null)
                    systems[i].Play(true);
            }
        }

        void CreateCheerLabel()
        {
            if (_cheerLabel != null)
                return;

            _cheerLabel = new GameObject(
                "RescueThankYou3D",
                typeof(RectTransform),
                typeof(Canvas));
            RectTransform canvasRect =
                _cheerLabel.GetComponent<RectTransform>();
            canvasRect.SetParent(transform, false);
            canvasRect.localPosition = Vector3.up * 2.55f;
            canvasRect.localRotation = Quaternion.identity;
            canvasRect.localScale = Vector3.one * 0.0045f;
            canvasRect.sizeDelta = new Vector2(340f, 90f);

            Canvas canvas = _cheerLabel.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvas.sortingOrder = 100;

            var textObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(UnityEngine.UI.Text));
            RectTransform textRect =
                textObject.GetComponent<RectTransform>();
            textRect.SetParent(canvasRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var label = textObject.GetComponent<UnityEngine.UI.Text>();
            label.font = ResolveLabelFont(_cheerLabelFont);
            label.text = "谢谢你！";
            label.fontSize = 48;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.color = new Color(1f, 0.42f, 0.72f, 1f);
            label.raycastTarget = false;

            var outline =
                textObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(3f, -3f);
            var shadow =
                textObject.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = new Color(0.08f, 0.01f, 0.05f, 0.9f);
            shadow.effectDistance = new Vector2(3f, -3f);

            _cheerLabel.AddComponent<
                AntiFraudStorm.UI.WorldSpaceBillboard>();
            _cheerLabelRoutine =
                StartCoroutine(AnimateCheerLabelRoutine());
        }

        IEnumerator AnimateCheerLabelRoutine()
        {
            Transform labelTransform = _cheerLabel.transform;
            float startedAt = Time.unscaledTime;
            while (_cheerLabel != null)
            {
                float elapsed = Time.unscaledTime - startedAt;
                float pulse = 0.5f - 0.5f *
                    Mathf.Cos(elapsed * Mathf.PI * 4.4f);
                labelTransform.localScale =
                    Vector3.one * 0.0045f * (1f + pulse * 0.16f);
                yield return null;
            }
            _cheerLabelRoutine = null;
        }

        void ClearCheerEffects()
        {
            if (_cheerLabelRoutine != null)
            {
                StopCoroutine(_cheerLabelRoutine);
                _cheerLabelRoutine = null;
            }
            if (_cheerParticle != null)
            {
                Destroy(_cheerParticle);
                _cheerParticle = null;
            }
            if (_cheerLabel != null)
            {
                Destroy(_cheerLabel);
                _cheerLabel = null;
            }
        }

        static Font ResolveLabelFont(Font preferred)
        {
            if (preferred != null)
                return preferred;

            string[] installed = Font.GetOSInstalledFontNames();
            string[] names = { "Microsoft YaHei", "SimHei", "Arial Unicode MS" };
            for (int i = 0; i < names.Length; i++)
            {
                if (System.Array.IndexOf(installed, names[i]) >= 0)
                {
                    return Font.CreateDynamicFontFromOSFont(
                        names[i],
                        48);
                }
            }

            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        void PlayHeadParticle()
        {
            if (_headParticle == null)
                return;

            ParticleSystem[] systems = _headParticle.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem.MainModule main = systems[i].main;
                main.loop = true;
                systems[i].Play(true);
            }
        }
    }
}
