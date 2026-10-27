using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace AntiFraudStorm.UI
{
    /// <summary>
    /// Fullscreen viewfinder overlay for handheld-camera first person.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class CameraViewfinderController : MonoBehaviour
    {
        [SerializeField] UIDocument document;
        [SerializeField] Sprite frameSprite;

        VisualElement _root;
        VisualElement _frame;
        VisualElement _captureFlash;
        Coroutine _flashRoutine;

        public static CameraViewfinderController Instance { get; private set; }

        void Awake()
        {
            if (document == null)
                document = GetComponent<UIDocument>();

            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void OnEnable()
        {
            Bind();
            Hide();
        }

        void Bind()
        {
            if (document == null)
                return;

            var rootVe = document.rootVisualElement;
            if (rootVe == null)
                return;

            _root = rootVe.Q<VisualElement>("root");
            _frame = rootVe.Q<VisualElement>("frame");
            _captureFlash = rootVe.Q<VisualElement>("capture-flash");

            if (_frame != null && frameSprite != null)
                _frame.style.backgroundImage = new StyleBackground(frameSprite);
        }

        public void Show()
        {
            if (_root == null)
                Bind();
            _root?.RemoveFromClassList("is-hidden");
        }

        public void Hide()
        {
            if (_root == null)
                Bind();
            _root?.AddToClassList("is-hidden");
        }

        public void PlayCaptureFlash()
        {
            if (_captureFlash == null)
                Bind();
            if (_captureFlash == null)
                return;

            if (_flashRoutine != null)
                StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(CaptureFlashRoutine());
        }

        IEnumerator CaptureFlashRoutine()
        {
            _captureFlash.RemoveFromClassList("is-hidden");
            _captureFlash.style.opacity = 0.92f;
            yield return new WaitForSecondsRealtime(0.055f);

            const float duration = 0.22f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _captureFlash.style.opacity = Mathf.Lerp(
                    0.92f,
                    0f,
                    Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            _captureFlash.style.opacity = 0f;
            _captureFlash.AddToClassList("is-hidden");
            _flashRoutine = null;
        }
    }
}
