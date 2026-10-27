using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AntiFraudStorm.Minigame
{
    /// <summary>
    /// A placed anti-fraud tool. It automatically targets the nearest minion
    /// and fires the projectile that belongs to its tool type.
    /// </summary>
    public class FraudToolTurret : MonoBehaviour
    {
        AntiFraudDefenseGame _game;
        DefenseEnemy _target;
        RectTransform _labelCanvas;
        int _ammoIndex;
        float _fireInterval = 1.25f;
        float _nextFireTime;
        float _range = 30f;
        float _expireAt;
        bool _expiring;

        public void Initialize(
            AntiFraudDefenseGame game,
            int ammoIndex,
            Text label,
            float fireInterval,
            float lifetime)
        {
            _game = game;
            _ammoIndex = ammoIndex;
            _fireInterval = Mathf.Max(0.2f, fireInterval);
            _expireAt = Time.time + Mathf.Max(2f, lifetime);
            Canvas canvas = label != null ? label.GetComponentInParent<Canvas>() : null;
            _labelCanvas = canvas != null ? canvas.transform as RectTransform : null;
            if (_labelCanvas != null)
                _labelCanvas.gameObject.SetActive(true);
        }

        void Update()
        {
            if (_game == null)
                return;

            if (_expiring)
                return;

            if (Time.time >= _expireAt)
            {
                StartCoroutine(ExpireRoutine());
                return;
            }

            if (_target == null || _target.IsDead)
                _target = _game.FindNearestEnemy(transform.position, _range);

            if (_target == null)
                return;

            Vector3 toTarget = _target.AimPoint - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(toTarget.normalized, Vector3.up),
                    Time.deltaTime * 10f);
            }

            if (Time.time < _nextFireTime)
                return;

            _nextFireTime = Time.time + _fireInterval;
            _game.FireTurretProjectile(
                this,
                _ammoIndex,
                _target,
                transform.position + Vector3.up * 1.15f + transform.forward * 0.55f);
        }

        void LateUpdate()
        {
            if (_labelCanvas == null || _game == null || _game.ViewCamera == null)
                return;

            _labelCanvas.position = transform.position + Vector3.up * 3.15f;
            _labelCanvas.rotation = _game.ViewCamera.transform.rotation;
        }

        IEnumerator ExpireRoutine()
        {
            _expiring = true;
            _target = null;

            Vector3 initialScale = transform.localScale;
            Vector3 initialLabelScale = _labelCanvas != null
                ? _labelCanvas.localScale
                : Vector3.one;
            CanvasGroup labelGroup = _labelCanvas != null
                ? _labelCanvas.GetComponent<CanvasGroup>()
                : null;
            if (_labelCanvas != null && labelGroup == null)
                labelGroup = _labelCanvas.gameObject.AddComponent<CanvasGroup>();

            const float fadeDuration = 0.35f;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                float eased = t * t * (3f - 2f * t);
                transform.localScale = initialScale * Mathf.Lerp(1f, 0.08f, eased);
                if (_labelCanvas != null)
                {
                    _labelCanvas.localScale =
                        initialLabelScale * Mathf.Lerp(1f, 0.08f, eased);
                }
                if (labelGroup != null)
                    labelGroup.alpha = 1f - eased;
                yield return null;
            }

            RectTransform labelToDestroy = _labelCanvas;
            _labelCanvas = null;
            if (labelToDestroy != null)
                Destroy(labelToDestroy.gameObject);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (_labelCanvas != null)
                Destroy(_labelCanvas.gameObject);
        }
    }
}
