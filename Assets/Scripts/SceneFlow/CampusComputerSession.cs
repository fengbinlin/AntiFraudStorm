using System.Collections;
using System.Collections.Generic;
using AntiFraudStorm.Navigation;
using AntiFraudStorm.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using Image = UnityEngine.UI.Image;

namespace AntiFraudStorm.SceneFlow
{
    /// <summary>
    /// Sleeps the campus scene, additively loads the computer (Win) scene, and restores campus on exit.
    /// Lives on QuestSystem so it is not disabled while campus roots sleep.
    /// </summary>
    public class CampusComputerSession : MonoBehaviour
    {
        public const string DefaultWinSceneName = "FakeWindowsDesktop";
        public const string DefaultDangerSceneName = "Anti-Scam Cortex";

        [SerializeField] string winSceneName = DefaultWinSceneName;
        [SerializeField] string dangerSceneName = DefaultDangerSceneName;
        [SerializeField] TransitionsPlus.TransitionAnimator dangerSceneTransition;
        [Header("Campus Entrance")]
        [SerializeField, Min(0f)] float campusEntryFadeDuration = 2f;
        [Header("Anti-Scam Cortex")]
        [SerializeField, Min(0f)] float dangerCompletionDelay = 20f;
        [Header("Scene Transition")]
        [SerializeField, Min(0.1f)] float cameraZoomDuration = 1.15f;
        [SerializeField, Min(0f)] float cameraPushDistance = 2.4f;
        [SerializeField, Range(15f, 80f)] float transitionFieldOfView = 34f;
        [SerializeField, Min(0f)] float fadeToBlackDuration = 0.45f;
        [SerializeField, Min(0f)] float fadeFromBlackDuration = 0.4f;
        [SerializeField, Min(0f)] float returnBlackHoldDuration = 0.15f;
        [SerializeField, Min(0f)] float schoolReturnBlackDuration = 0.3f;

        readonly List<GameObject> _sleptRoots = new List<GameObject>();
        Scene _campusScene;
        bool _busy;
        bool _inComputer;
        bool _inDanger;
        DigitalHumanWalker _rescuedStudent;
        Camera _transitionCamera;
        Vector3 _cameraStartPosition;
        Quaternion _cameraStartRotation;
        float _cameraStartFieldOfView;
        Canvas _fadeCanvas;
        CanvasGroup _fadeCanvasGroup;
        Sprite _fadeSprite;
        Image _fadeImage;
        Camera _displayHoldCamera;
        Coroutine _dangerCompletionRoutine;

        public static CampusComputerSession Instance { get; private set; }

        /// <summary>Player locomotion/look frozen while the computer scene is active.</summary>
        public static bool IsFrozen { get; private set; }

        public bool IsInComputer => _inComputer;
        public bool IsInDanger => _inDanger;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            if (campusEntryFadeDuration > 0f)
            {
                _busy = true;
                EnsureFadeOverlay();
                _fadeCanvas.enabled = true;
                SetFadeAlpha(1f);
            }
        }

        void Start()
        {
            if (campusEntryFadeDuration > 0f)
                StartCoroutine(CampusEntryFadeRoutine());
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                IsFrozen = false;
            }

            if (_fadeCanvas != null)
                Destroy(_fadeCanvas.gameObject);
            if (_displayHoldCamera != null)
                Destroy(_displayHoldCamera.gameObject);
            if (_fadeSprite != null)
                Destroy(_fadeSprite);
        }

        public void EnterComputer()
        {
            if (_busy || _inComputer || _inDanger)
                return;
            StartCoroutine(EnterAdditiveSceneRoutine(winSceneName, false));
        }

        public void EnterDangerScene(DigitalHumanWalker student = null)
        {
            if (_busy || _inComputer || _inDanger)
                return;
            _rescuedStudent = student;
            StartCoroutine(EnterAdditiveSceneRoutine(dangerSceneName, true));
        }

        public void ReturnToCampus()
        {
            LogReturnTrace(
                $"ReturnToCampus requested. busy={_busy}, " +
                $"inComputer={_inComputer}, inDanger={_inDanger}, " +
                $"activeScene={SceneManager.GetActiveScene().name}");
            if (_busy || (!_inComputer && !_inDanger))
            {
                LogReturnTrace("ReturnToCampus rejected.");
                return;
            }
            if (_dangerCompletionRoutine != null)
            {
                StopCoroutine(_dangerCompletionRoutine);
                _dangerCompletionRoutine = null;
            }
            StartCoroutine(ExitRoutine(_inDanger));
        }

        IEnumerator EnterAdditiveSceneRoutine(string sceneName, bool dangerScene)
        {
            _busy = true;
            IsFrozen = true;
            _campusScene = gameObject.scene;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (string.IsNullOrWhiteSpace(sceneName) ||
                !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError("[Campus] Target scene is not in Build Settings: " + sceneName);
                IsFrozen = false;
                _busy = false;
                yield break;
            }

            EnsureFadeOverlay();
            SetFadeAlpha(0f);
            CacheCameraState();
            if (dangerScene)
                PlayDangerSceneTransition();
            yield return ZoomCameraRoutine(cameraZoomDuration);
            if (dangerScene)
                yield return WaitForDangerSceneTransition();
            yield return FadeToAlphaRoutine(1f, fadeToBlackDuration);
            EnsureDisplayHoldCamera();
            _displayHoldCamera.enabled = true;
            SleepCampusRoots();
            DisableCampusCameras();

            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (load == null)
            {
                Debug.LogError("[Campus] Failed to load " + sceneName);
                WakeCampusRoots();
                RestoreCameraState();
                _displayHoldCamera.enabled = false;
                IsFrozen = false;
                _busy = false;
                yield return FadeToAlphaRoutine(0f, fadeFromBlackDuration);
                yield break;
            }

            load.allowSceneActivation = true;
            while (!load.isDone)
                yield return null;

            Scene targetScene = SceneManager.GetSceneByName(sceneName);
            if (targetScene.IsValid())
                SceneManager.SetActiveScene(targetScene);

            // Let the target scene's Start callbacks create gameplay cameras before
            // revealing the frame. Anti-Scam Cortex instantiates its 3C player at runtime.
            yield return null;
            if (dangerScene)
            {
                // Campus roots are asleep and the Anti-Scam Cortex controller must not inherit
                // the frozen state used while the old scene is being replaced.
                IsFrozen = false;
                EnsureActivePlayerInput();
            }
            yield return WaitForGameplayCamera(targetScene);

            if (dangerScene)
            {
                _inDanger = true;
            }
            else
            {
                _inComputer = true;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            yield return FadeToAlphaRoutine(0f, fadeFromBlackDuration);
            if (dangerScene)
            {
                IsFrozen = false;
                EnsureActivePlayerInput();
            }
            _displayHoldCamera.enabled = false;
            _busy = false;
            if (dangerScene)
            {
                if (_dangerCompletionRoutine != null)
                    StopCoroutine(_dangerCompletionRoutine);
                _dangerCompletionRoutine =
                    StartCoroutine(DangerCompletionRoutine());
            }
        }

        IEnumerator CampusEntryFadeRoutine()
        {
            EnsureFadeOverlay();
            SetFadeAlpha(1f);
            yield return null;
            yield return new WaitForSecondsRealtime(0.08f);
            yield return FadeToAlphaRoutine(0f, campusEntryFadeDuration);
            _busy = false;
        }

        void PlayDangerSceneTransition()
        {
            if (dangerSceneTransition == null)
                return;

            dangerSceneTransition.gameObject.SetActive(true);
            dangerSceneTransition.autoPlay = false;
            dangerSceneTransition.autoDestroy = false;
            dangerSceneTransition.loadSceneAtEnd = false;
            dangerSceneTransition.Play();
        }

        IEnumerator WaitForDangerSceneTransition()
        {
            if (dangerSceneTransition == null)
                yield break;

            while (dangerSceneTransition != null &&
                   dangerSceneTransition.isPlaying)
            {
                yield return null;
            }
        }

        void ResetDangerSceneTransition()
        {
            if (dangerSceneTransition == null)
                return;

            dangerSceneTransition.enabled = false;
            dangerSceneTransition.progress = 0f;
            dangerSceneTransition.SetProgress(0f);
            dangerSceneTransition.gameObject.SetActive(false);
        }

        IEnumerator DangerCompletionRoutine()
        {
            LogReturnTrace(
                $"Danger countdown started. delay={dangerCompletionDelay:0.00}s");
            yield return new WaitForSecondsRealtime(dangerCompletionDelay);
            LogReturnTrace(
                $"Danger countdown finished. inDanger={_inDanger}, " +
                $"busy={_busy}");
            _dangerCompletionRoutine = null;
            if (_inDanger && !_busy)
                ReturnToCampus();
            else
                LogReturnTrace("Danger countdown ignored because state changed.");
        }

        IEnumerator ExitRoutine(bool dangerScene)
        {
            LogReturnTrace(
                $"ExitRoutine started. danger={dangerScene}, " +
                $"activeScene={SceneManager.GetActiveScene().name}");
            _busy = true;
            IsFrozen = true;
            EnsureFadeOverlay();
            LogReturnTrace(
                $"Fade overlay before black: {DescribeFadeOverlay()}");
            yield return FadeToAlphaRoutine(1f, fadeToBlackDuration);
            LogReturnTrace(
                $"Fade to black complete. {DescribeFadeOverlay()}");
            yield return new WaitForSecondsRealtime(returnBlackHoldDuration);
            LogReturnTrace(
                $"Black hold complete. {DescribeFadeOverlay()}");

            EnsureDisplayHoldCamera();
            _displayHoldCamera.enabled = true;
            string sceneToUnload =
                dangerScene ? dangerSceneName : winSceneName;
            Scene loadedScene =
                SceneManager.GetSceneByName(sceneToUnload);

            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                LogReturnTrace($"Unloading additive scene '{sceneToUnload}'.");
                AsyncOperation unload =
                    SceneManager.UnloadSceneAsync(loadedScene);
                while (unload != null && !unload.isDone)
                    yield return null;
                LogReturnTrace($"Unload complete: '{sceneToUnload}'.");
            }
            else
            {
                LogReturnTrace(
                    $"Scene '{sceneToUnload}' was not loaded at exit.");
            }

            LogReturnTrace("Waking School roots.");
            WakeCampusRoots();
            RestoreCameraState();
            if (_campusScene.IsValid())
                SceneManager.SetActiveScene(_campusScene);
            LogReturnTrace(
                $"School active. {DescribeFadeOverlay()}");
            if (dangerScene)
                ResetDangerSceneTransition();
            if (dangerScene && _rescuedStudent != null)
            {
                SchoolDigitalHumanSpawner spawner =
                    SchoolDigitalHumanSpawner.Instance;
                if (spawner == null)
                    spawner = FindObjectOfType<SchoolDigitalHumanSpawner>(true);
                if (spawner != null)
                {
                    LogReturnTrace(
                        $"Starting rescue celebration for " +
                        $"'{_rescuedStudent.name}'.");
                    spawner.BeginRescueCelebration(_rescuedStudent);
                }
                else
                {
                    LogReturnTrace(
                        "Rescue celebration skipped: spawner not found.");
                }
                _rescuedStudent = null;
            }

            _inComputer = false;
            _inDanger = false;
            EnsureActivePlayerInput();
            yield return new WaitForSecondsRealtime(
                schoolReturnBlackDuration);
            LogReturnTrace(
                $"School black hold complete. {DescribeFadeOverlay()}");
            LogReturnTrace(
                $"Fade from black begin. {DescribeFadeOverlay()}");
            yield return FadeToAlphaRoutine(0f, fadeFromBlackDuration);
            LogReturnTrace(
                $"Fade from black complete. {DescribeFadeOverlay()}");
            _displayHoldCamera.enabled = false;
            IsFrozen = false;
            _busy = false;
            LogReturnTrace(
                $"ExitRoutine complete. inComputer={_inComputer}, " +
                $"inDanger={_inDanger}, activeScene=" +
                $"{SceneManager.GetActiveScene().name}");
        }

        void CacheCameraState()
        {
            _transitionCamera = Camera.main;
            if (_transitionCamera == null)
                return;

            _cameraStartPosition = _transitionCamera.transform.position;
            _cameraStartRotation = _transitionCamera.transform.rotation;
            _cameraStartFieldOfView = _transitionCamera.fieldOfView;
        }

        void RestoreCameraState()
        {
            if (_transitionCamera == null)
                return;

            _transitionCamera.transform.SetPositionAndRotation(_cameraStartPosition, _cameraStartRotation);
            _transitionCamera.fieldOfView = _cameraStartFieldOfView;
        }

        IEnumerator ZoomCameraRoutine(float duration)
        {
            if (_transitionCamera == null || duration <= 0f)
                yield break;

            Vector3 startPosition = _transitionCamera.transform.position;
            Vector3 targetPosition = startPosition + _transitionCamera.transform.forward * cameraPushDistance;
            float startFov = _transitionCamera.fieldOfView;
            float targetFov = Mathf.Min(startFov, transitionFieldOfView);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t);
                _transitionCamera.transform.position = Vector3.LerpUnclamped(startPosition, targetPosition, t);
                _transitionCamera.fieldOfView = Mathf.Lerp(startFov, targetFov, t);
                yield return null;
            }
        }

        IEnumerator FadeToAlphaRoutine(float targetAlpha, float duration)
        {
            EnsureFadeOverlay();
            if (_fadeImage == null)
                yield break;

            _fadeCanvas.enabled = true;
            float startAlpha = _fadeImage.color.a;
            if (duration <= 0f)
            {
                SetFadeAlpha(targetAlpha);
                _fadeCanvas.enabled = targetAlpha > 0.001f;
                if (targetAlpha <= 0.001f)
                    ReleaseFadeOverlay();
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                float t = Mathf.Clamp01(elapsed / duration);
                SetFadeAlpha(Mathf.Lerp(startAlpha, targetAlpha, t));
                yield return null;
            }

            SetFadeAlpha(targetAlpha);
            _fadeCanvas.enabled = targetAlpha > 0.001f;
            if (targetAlpha <= 0.001f)
                ReleaseFadeOverlay();
        }

        void EnsureFadeOverlay()
        {
            if (_fadeImage != null)
                return;

            var canvasGo = new GameObject(
                "SceneTransitionFadeCanvas",
                typeof(RectTransform),
                typeof(Canvas));
            canvasGo.transform.SetParent(null, false);
            DontDestroyOnLoad(canvasGo);
            _fadeCanvas = canvasGo.GetComponent<Canvas>();
            _fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _fadeCanvas.overrideSorting = true;
            _fadeCanvas.sortingOrder = 32767;
            _fadeCanvasGroup = canvasGo.AddComponent<CanvasGroup>();
            _fadeCanvasGroup.alpha = 1f;
            _fadeCanvasGroup.blocksRaycasts = false;
            _fadeCanvasGroup.interactable = false;

            var imageGo = new GameObject(
                "SceneTransitionBlack",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            var imageRect = imageGo.GetComponent<RectTransform>();
            imageRect.SetParent(canvasGo.transform, false);
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;

            _fadeImage = imageGo.GetComponent<Image>();
            _fadeSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            _fadeImage.sprite = _fadeSprite;
            _fadeImage.type = Image.Type.Simple;
            _fadeImage.raycastTarget = false;
            _fadeImage.color = new Color(0f, 0f, 0f, 0f);
            LogReturnTrace($"Fade overlay created. {DescribeFadeOverlay()}");
        }

        void EnsureDisplayHoldCamera()
        {
            if (_displayHoldCamera != null)
                return;

            var cameraGo = new GameObject("SceneTransitionDisplayHoldCamera");
            cameraGo.transform.SetParent(transform, false);
            _displayHoldCamera = cameraGo.AddComponent<Camera>();
            _displayHoldCamera.clearFlags = CameraClearFlags.SolidColor;
            _displayHoldCamera.backgroundColor = Color.black;
            _displayHoldCamera.cullingMask = 0;
            _displayHoldCamera.depth = -100f;
            _displayHoldCamera.allowHDR = false;
            _displayHoldCamera.allowMSAA = false;
            _displayHoldCamera.enabled = false;
        }

        void SetFadeAlpha(float alpha)
        {
            if (_fadeImage != null)
            {
                _fadeImage.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
            }
        }

        string DescribeFadeOverlay()
        {
            if (_fadeCanvas == null || _fadeImage == null)
                return "fade=none";

            return
                $"fadeActive={_fadeCanvas.gameObject.activeInHierarchy}, " +
                $"canvasEnabled={_fadeCanvas.enabled}, " +
                $"sortingOrder={_fadeCanvas.sortingOrder}, " +
                $"alpha={_fadeImage.color.a:0.000}";
        }

        void LogReturnTrace(string message)
        {
            Debug.Log(
                $"[AFS_RETURN_TRACE] frame={Time.frameCount} " +
                $"time={Time.unscaledTime:0.000} {message}",
                this);
        }

        void ReleaseFadeOverlay()
        {
            if (_fadeCanvas != null)
                Destroy(_fadeCanvas.gameObject);
            _fadeCanvas = null;
            _fadeCanvasGroup = null;
            _fadeImage = null;
        }

        void SleepCampusRoots()
        {
            _sleptRoots.Clear();
            if (!_campusScene.IsValid())
                return;

            var roots = _campusScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (root == null || !root.activeSelf)
                    continue;
                if (root.GetComponentInChildren<CampusComputerSession>(true) != null)
                    continue;

                _sleptRoots.Add(root);
                root.SetActive(false);
            }
        }

        void DisableCampusCameras()
        {
            if (!_campusScene.IsValid())
                return;

            var roots = _campusScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var cams = roots[i].GetComponentsInChildren<Camera>(true);
                for (int c = 0; c < cams.Length; c++)
                {
                    if (cams[c] != null)
                        cams[c].enabled = false;
                }
            }
        }

        static void EnsureSceneCameraEnabled(Scene scene)
        {
            if (!scene.IsValid())
                return;

            Camera fallback = null;
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var cameras = roots[i].GetComponentsInChildren<Camera>(true);
                for (int c = 0; c < cameras.Length; c++)
                {
                    Camera camera = cameras[c];
                    if (camera == null)
                        continue;

                    fallback ??= camera;
                    if (camera.gameObject.activeInHierarchy)
                    {
                        camera.enabled = true;
                        return;
                    }
                }
            }

            if (fallback != null)
            {
                fallback.gameObject.SetActive(true);
                fallback.enabled = true;
            }
        }

        IEnumerator WaitForGameplayCamera(Scene scene)
        {
            float deadline = Time.unscaledTime + 2f;
            while (!HasAnyGameplayCamera(scene) && Time.unscaledTime < deadline)
                yield return null;

            EnsureSceneCameraEnabled(scene);
            if (!HasAnyGameplayCamera(scene))
                EnsurePlayerSpringArmCameraEnabled();

            if (!HasAnyGameplayCamera(scene))
            {
                Debug.LogError(
                    "[Campus] No active gameplay camera was found in " +
                    scene.name + ".");
            }
        }

        bool HasAnyGameplayCamera(Scene scene)
        {
            if (HasActiveCamera(scene))
                return true;

            // New GameObjects created by additive-scene Start callbacks can be owned
            // by the previously active scene. Accept an enabled spring-arm camera
            // from any scene, but never count the temporary black hold camera.
            ThirdPersonSpringArm[] springArms =
                FindObjectsOfType<ThirdPersonSpringArm>(true);
            for (int i = 0; i < springArms.Length; i++)
            {
                ThirdPersonSpringArm arm = springArms[i];
                if (arm == null || !arm.gameObject.activeInHierarchy)
                    continue;

                Camera camera = arm.GetComponentInChildren<Camera>(true);
                if (camera != null &&
                    camera != _displayHoldCamera &&
                    camera.gameObject.activeInHierarchy &&
                    camera.enabled)
                {
                    return true;
                }
            }

            return false;
        }

        void EnsurePlayerSpringArmCameraEnabled()
        {
            ThirdPersonSpringArm[] springArms =
                FindObjectsOfType<ThirdPersonSpringArm>(true);
            for (int i = 0; i < springArms.Length; i++)
            {
                ThirdPersonSpringArm arm = springArms[i];
                if (arm == null || !arm.gameObject.activeInHierarchy)
                    continue;

                Camera camera = arm.GetComponentInChildren<Camera>(true);
                if (camera == null)
                    continue;

                camera.gameObject.SetActive(true);
                camera.enabled = true;
                camera.tag = "MainCamera";
                return;
            }
        }

        static void EnsureActivePlayerInput()
        {
            PlayerInputReader[] readers =
                FindObjectsOfType<PlayerInputReader>(true);
            for (int i = 0; i < readers.Length; i++)
            {
                PlayerInputReader reader = readers[i];
                if (reader == null || !reader.isActiveAndEnabled)
                    continue;

                reader.ClaimFocus();
                return;
            }
        }

        static bool HasActiveCamera(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return false;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Camera[] cameras = roots[i].GetComponentsInChildren<Camera>(true);
                for (int c = 0; c < cameras.Length; c++)
                {
                    Camera camera = cameras[c];
                    if (camera != null &&
                        camera.gameObject.activeInHierarchy &&
                        camera.enabled)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        void WakeCampusRoots()
        {
            for (int i = 0; i < _sleptRoots.Count; i++)
            {
                if (_sleptRoots[i] != null)
                    _sleptRoots[i].SetActive(true);
            }

            _sleptRoots.Clear();

            if (!_campusScene.IsValid())
                return;

            var roots = _campusScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var cams = roots[i].GetComponentsInChildren<Camera>(true);
                for (int c = 0; c < cams.Length; c++)
                {
                    if (cams[c] != null)
                        cams[c].enabled = true;
                }
            }

            // Animancer/Animator state is discarded while the campus roots are asleep.
            // Rebuild it only after every campus root is active again.
            DigitalHumanWalker[] walkers = FindObjectsOfType<DigitalHumanWalker>(true);
            for (int i = 0; i < walkers.Length; i++)
            {
                if (walkers[i] != null)
                    walkers[i].RestoreAfterCampusActivation();
            }
        }
    }
}
