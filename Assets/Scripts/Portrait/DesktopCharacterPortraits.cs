using Animancer;
using AntiFraudStorm.Player;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace AntiFraudStorm.Portrait
{
    /// <summary>
    /// Bottom-corner character busts on the virtual desktop. No locomotion; clips can be played later.
    /// </summary>
    public class DesktopCharacterPortraits : MonoBehaviour
    {
        public const string LayerName = "DesktopPortrait";

        [System.Serializable]
        public class PortraitLightSettings
        {
            [Range(0f, 4f)] public float keyIntensity = 1.4f;
            public Color keyColor = new Color(1f, 0.96f, 0.9f, 1f);
            [Range(-60f, 60f)] public float keyYaw = -22f;
            [Range(0f, 60f)] public float keyPitch = 18f;

            [Range(0f, 4f)] public float fillIntensity = 0.36f;
            public Color fillColor = new Color(0.72f, 0.84f, 1f, 1f);
            [Range(-60f, 60f)] public float fillYaw = 28f;
            [Range(0f, 60f)] public float fillPitch = 6f;
        }

        [System.Serializable]
        public class PortraitAnimationSettings
        {
            public AnimationClip entryClip;
            public AnimationClip idleClip;
            [Min(0f)] public float entryDelay;
            [Min(0f)] public float entryFade = 0.2f;
            [Min(0f)] public float idleFade = 0.3f;
        }

        [SerializeField] UIDocument document;
        [SerializeField] GameObject heroPrefab;
        [SerializeField] GameObject thiefPrefab;
        [SerializeField] int textureSize = 512;
        [SerializeField] Color heroBackground = new Color(0.95f, 0.78f, 0.18f, 1f);
        [SerializeField] Color thiefBackground = new Color(0.23f, 0.08f, 0.35f, 1f);
        [SerializeField] PortraitLightSettings portraitLighting = new PortraitLightSettings();
        [SerializeField] PortraitAnimationSettings heroAnimation = new PortraitAnimationSettings();
        [SerializeField] PortraitAnimationSettings thiefAnimation = new PortraitAnimationSettings();
        [SerializeField] Font portraitLabelFont;
        [Header("Answer Feedback")]
        [SerializeField] AnimationClip heroWrongClip;
        [SerializeField] AnimationClip heroCorrectClip;
        [SerializeField] AnimationClip thiefWrongClip;
        [SerializeField] AnimationClip thiefCorrectClip;
        [Header("Entry Presentation")]
        [SerializeField, Min(0f)] float heroCircleRevealDuration = 0.7f;
        [SerializeField, Min(0.1f)] float alertEffectDuration = 1.4f;

        class AlertMotion
        {
            public VisualElement Element;
            public Vector2 Start;
            public Vector2 End;
            public float Delay;
            public float Size;
        }

        static readonly Vector2[] AlertStarts =
        {
            new Vector2(185f, 210f),
            new Vector2(190f, 214f),
            new Vector2(180f, 205f),
            new Vector2(195f, 218f),
            new Vector2(178f, 212f),
            new Vector2(188f, 200f),
            new Vector2(192f, 220f),
        };

        static readonly Vector2[] AlertEnds =
        {
            new Vector2(118f, 62f),
            new Vector2(188f, 20f),
            new Vector2(292f, 70f),
            new Vector2(332f, 176f),
            new Vector2(286f, 292f),
            new Vector2(84f, 250f),
            new Vector2(44f, 138f),
        };

        static readonly float[] AlertDelays = { 0f, 0.04f, 0.08f, 0.12f, 0.17f, 0.22f, 0.27f };
        static readonly float[] AlertSizes = { 74f, 58f, 86f, 64f, 78f, 56f, 92f };

        [Header("圆圈（像素）")]
        [Tooltip("圆的直径")]
        [SerializeField] float circleSize = 591.4f;
        [Tooltip("负值把圆心推出左/右边缘外，越大（越接近 0）越往屏幕中间")]
        [SerializeField] float heroLeft = -200f;
        [SerializeField] float thiefRight = -200f;
        [Tooltip("负值把圆心推出底边外，越大（越接近 0）圆越往上")]
        [SerializeField] float circleBottom = -171.6f;

        [Header("人物贴图在圆内")]
        [Tooltip("人物画面宽度，占圆直径的百分比")]
        [Range(20f, 100f)]
        [SerializeField] float characterWidthPercent = 84.1f;
        [Tooltip("人物画面高度，占圆直径的百分比")]
        [Range(20f, 100f)]
        [SerializeField] float characterHeightPercent = 81.6f;
        [Tooltip("人物相对圆顶的偏移（百分比）")]
        [SerializeField] float characterTopPercent = 6.92f;
        [Tooltip("主角人物相对圆内侧（靠屏幕中）的像素偏移，正值更靠中")]
        [SerializeField] float heroCharacterInset = 0f;
        [Tooltip("小偷人物相对圆内侧（靠屏幕中）的像素偏移，正值更靠中")]
        [SerializeField] float thiefCharacterInset = 1.87f;

        [Header("镜头构图")]
        [Tooltip("越大人越小")]
        [SerializeField] float cameraDistance = 1.6f;
        [SerializeField] float cameraFov = 27.6f;
        [Tooltip("看向点高度，正值往头、负值往脚")]
        [SerializeField] float lookHeightBias = 0.12f;
        [Tooltip("主角在画面中偏移：X 右，Y 上")]
        [SerializeField] Vector2 heroFramingOffset;
        [Tooltip("小偷在画面中偏移：X 右，Y 上")]
        [SerializeField] Vector2 thiefFramingOffset;

        CharacterPortraitSlot _hero;
        CharacterPortraitSlot _thief;
        VisualElement _heroCircle;
        VisualElement _thiefCircle;
        VisualElement _heroView;
        VisualElement _thiefView;
        float _heroCircleRevealStartAt;
        float _thiefCircleRevealStartAt;
        float _platformerWipeStartedAt;
        float _platformerWipeDuration;
        bool _platformerWipeActive;
        int _portraitLayer;
        readonly AlertMotion[] _alertMotions = new AlertMotion[7];
        float _alertStartedAt;
        bool _alertPlaying;

        public static DesktopCharacterPortraits Instance { get; private set; }

        public CharacterPortraitSlot Hero => _hero;
        public CharacterPortraitSlot Thief => _thief;
        public bool IsThiefRevealed => _thief != null;
        public GameObject HeroPrefab => heroPrefab;
        public GameObject EnemyPrefab => thiefPrefab;

        public void BeginPlatformerWipe(float duration = 0.9f)
        {
            _platformerWipeStartedAt = Time.unscaledTime;
            _platformerWipeDuration = Mathf.Max(0.01f, duration);
            _platformerWipeActive = true;
        }

        public void ResetPlatformerWipe()
        {
            _platformerWipeActive = false;
            ApplyTuning();
        }

        public void RevealThief()
        {
            if (_thief != null)
                return;

            _thiefCircleRevealStartAt = Time.unscaledTime;
            _thief = CharacterPortraitSlot.Create(
                "ThiefPortraitStage",
                thiefPrefab,
                new Vector3(80f, 0f, 0f),
                transform,
                _portraitLayer,
                textureSize,
                thiefBackground,
                portraitLighting,
                thiefAnimation,
                createPortraitLights: false,
                worldLabelText: "电诈分子",
                worldLabelFont: portraitLabelFont);
            SetEnemyVisible(true);
            ApplyTextures();
            PlayAlertBurst();
        }

        public void PlayHero(AnimationClip clip, float fade = 0.2f) => _hero?.Play(clip, fade);
        public void PlayThief(AnimationClip clip, float fade = 0.2f) => _thief?.Play(clip, fade);
        public void PlayHeroIdle() => _hero?.PlayIdle();
        public void PlayThiefIdle() => _thief?.PlayIdle();

        public void PlayWrongAnswerFeedback()
        {
            _hero?.PlayFeedback(heroWrongClip, repeatCount: 2, useAgonizedFace: true);
            _thief?.PlayFeedback(thiefWrongClip);
        }

        public void PlayCorrectAnswerFeedback()
        {
            _hero?.PlayFeedback(heroCorrectClip);
            _thief?.PlayFeedback(thiefCorrectClip);
        }

        public void PlayThiefScholarshipTaunt()
        {
            _thief?.PlayFeedback(thiefWrongClip);
        }

        void Awake()
        {
            Instance = this;
            if (document == null)
                document = GetComponent<UIDocument>();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            _hero?.Dispose();
            _thief?.Dispose();
        }

        void OnEnable()
        {
            BindUi();
        }

        void Start()
        {
            _portraitLayer = LayerMask.NameToLayer(LayerName);
            if (_portraitLayer < 0)
                _portraitLayer = 0;

            _hero = CharacterPortraitSlot.Create(
                "HeroPortraitStage",
                heroPrefab,
                new Vector3(-80f, 0f, 0f),
                transform,
                _portraitLayer,
                textureSize,
                heroBackground,
                portraitLighting,
                heroAnimation);

            _heroCircleRevealStartAt = Time.unscaledTime;

            BindUi();
            ApplyTuning();
        }

        void LateUpdate()
        {
            _hero?.Tick();
            _thief?.Tick();
            UpdateAlertBurst();
            ApplyTuning();
        }

        void BindUi()
        {
            if (document == null || document.rootVisualElement == null)
                return;

            var root = document.rootVisualElement;
            _heroCircle = root.Q("portrait-hero");
            _thiefCircle = root.Q("portrait-thief");
            _heroView = root.Q("portrait-hero-view");
            _thiefView = root.Q("portrait-thief-view");
            SetEnemyVisible(_thief != null);
            BindAlertElements(root);
            ApplyTextures();
        }

        void BindAlertElements(VisualElement root)
        {
            if (_alertMotions[0] != null)
                return;

            for (int i = 0; i < _alertMotions.Length; i++)
            {
                var element = root.Q<VisualElement>($"thief-alert-{i + 1}");
                if (element == null)
                    continue;

                _alertMotions[i] = new AlertMotion
                {
                    Element = element,
                    Start = AlertStarts[i],
                    End = AlertEnds[i],
                    Delay = AlertDelays[i],
                    Size = AlertSizes[i],
                };
                element.style.display = DisplayStyle.None;
                element.style.opacity = 0f;
            }
        }

        void PlayAlertBurst()
        {
            _alertStartedAt = Time.unscaledTime;
            _alertPlaying = true;

            for (int i = 0; i < _alertMotions.Length; i++)
            {
                var motion = _alertMotions[i];
                if (motion == null)
                    continue;

                VisualElement element = motion.Element;
                element.style.display = DisplayStyle.Flex;
                element.style.left = motion.Start.x;
                element.style.top = motion.Start.y;
                element.style.width = 0f;
                element.style.height = 0f;
                element.style.opacity = 0f;
            }
        }

        void UpdateAlertBurst()
        {
            if (!_alertPlaying)
                return;

            float elapsed = Time.unscaledTime - _alertStartedAt;
            float totalDuration = Mathf.Max(0.1f, alertEffectDuration) + AlertDelays[AlertDelays.Length - 1];
            if (elapsed >= totalDuration)
            {
                _alertPlaying = false;
                for (int i = 0; i < _alertMotions.Length; i++)
                    SetDisplay(_alertMotions[i]?.Element, false);
                return;
            }

            for (int i = 0; i < _alertMotions.Length; i++)
            {
                var motion = _alertMotions[i];
                if (motion == null)
                    continue;

                float local = (elapsed - motion.Delay) / Mathf.Max(0.1f, alertEffectDuration);
                if (local < 0f)
                {
                    motion.Element.style.opacity = 0f;
                    continue;
                }

                if (local >= 1f)
                {
                    motion.Element.style.opacity = 0f;
                    motion.Element.style.display = DisplayStyle.None;
                    continue;
                }

                float move = EaseOutCubic(local);
                float pop = EaseOutBack(local);
                float fadeIn = Mathf.Clamp01(local / 0.12f);
                float fadeOut = 1f - Mathf.Clamp01((local - 0.62f) / 0.38f);
                float size = motion.Size * (0.35f + 0.75f * pop);
                Vector2 position = Vector2.LerpUnclamped(motion.Start, motion.End, move);

                motion.Element.style.left = position.x;
                motion.Element.style.top = position.y;
                motion.Element.style.width = size;
                motion.Element.style.height = size;
                motion.Element.style.opacity = fadeIn * fadeOut;
            }
        }

        static float EaseOutCubic(float value)
        {
            float inverse = 1f - Mathf.Clamp01(value);
            return 1f - inverse * inverse * inverse;
        }

        static float EaseOutBack(float value)
        {
            const float overshoot = 1.70158f;
            float shifted = Mathf.Clamp01(value) - 1f;
            return 1f + (overshoot + 1f) * shifted * shifted * shifted + overshoot * shifted * shifted;
        }

        void ApplyTuning()
        {
            ApplyCircle(_heroCircle, heroLeft, true);
            ApplyCircle(_thiefCircle, thiefRight, false);
            ApplyCharacterView(_heroView, true);
            ApplyCharacterView(_thiefView, false);
            _hero?.ApplyFraming(cameraDistance, cameraFov, lookHeightBias, heroFramingOffset);
            _thief?.ApplyFraming(cameraDistance, cameraFov, lookHeightBias, thiefFramingOffset);
            ApplyTextures();
        }

        void ApplyCircle(VisualElement circle, float side, bool hero)
        {
            if (circle == null)
                return;

            float size = GetCircleSize(hero) * GetPlatformerWipeScale();
            float centerInset = (circleSize - size) * 0.5f;
            circle.style.width = size;
            circle.style.height = size;
            circle.style.bottom = circleBottom + centerInset;
            if (hero)
            {
                circle.style.left = side + centerInset;
                circle.style.right = StyleKeyword.Auto;
                circle.style.backgroundColor = heroBackground;
            }
            else
            {
                circle.style.right = side + centerInset;
                circle.style.left = StyleKeyword.Auto;
                circle.style.backgroundColor = thiefBackground;
            }
        }

        float GetCircleSize(bool hero)
        {
            if (heroCircleRevealDuration <= 0f)
                return circleSize;

            float startedAt = hero ? _heroCircleRevealStartAt : _thiefCircleRevealStartAt;
            float progress = Mathf.Clamp01(
                (Time.unscaledTime - startedAt) / heroCircleRevealDuration);
            progress = progress * progress * (3f - 2f * progress);
            return circleSize * progress;
        }

        float GetPlatformerWipeScale()
        {
            if (!_platformerWipeActive)
                return 1f;

            float progress = Mathf.Clamp01(
                (Time.unscaledTime - _platformerWipeStartedAt) / _platformerWipeDuration);
            progress = progress * progress * (3f - 2f * progress);
            return Mathf.Lerp(1f, 5f, progress);
        }

        void ApplyCharacterView(VisualElement view, bool hero)
        {
            if (view == null)
                return;

            view.style.width = Length.Percent(characterWidthPercent);
            view.style.height = Length.Percent(characterHeightPercent);
            view.style.top = Length.Percent(characterTopPercent);
            view.style.bottom = StyleKeyword.Auto;
            if (hero)
            {
                view.style.right = heroCharacterInset;
                view.style.left = StyleKeyword.Auto;
            }
            else
            {
                view.style.left = thiefCharacterInset;
                view.style.right = StyleKeyword.Auto;
            }
        }

        void ApplyTextures()
        {
            if (_heroView != null && _hero != null)
                _heroView.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(_hero.Texture));
            if (_thiefView != null && _thief != null)
                _thiefView.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(_thief.Texture));
        }

        void SetEnemyVisible(bool visible)
        {
            SetDisplay(_thiefCircle, visible);
            SetDisplay(_thiefView, visible);
        }

        static void SetDisplay(VisualElement element, bool visible)
        {
            if (element != null)
                element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    public class CharacterPortraitSlot
    {
        readonly GameObject _root;
        readonly RenderTexture _rt;
        readonly AnimancerComponent _animancer;
        readonly PlayerAnimancerLocomotion _loco;
        readonly Animator _animator;
        readonly Camera _camera;
        readonly GameObject _faceA1;
        readonly GameObject _faceA3;
        readonly Light _keyLight;
        readonly Light _fillLight;
        readonly DesktopCharacterPortraits.PortraitLightSettings _lighting;
        readonly DesktopCharacterPortraits.PortraitAnimationSettings _animation;
        readonly Vector3 _lookCenter;
        readonly float _bodyHeight;

        AnimationClip _entryClip;
        bool _entryPending;
        bool _entryStarted;
        float _entryStartAt;
        float _entryEndAt;
        bool _feedbackPlaying;
        float _feedbackEndsAt;
        AnimationClip _feedbackClip;
        float _feedbackFade;
        int _feedbackRepeatsRemaining;

        public RenderTexture Texture => _rt;
        public Animator Animator => _animator;
        public AnimancerComponent Animancer => _animancer;

        CharacterPortraitSlot(
            GameObject root,
            RenderTexture rt,
            AnimancerComponent animancer,
            PlayerAnimancerLocomotion loco,
            Animator animator,
            Camera camera,
            GameObject faceA1,
            GameObject faceA3,
            Light keyLight,
            Light fillLight,
            DesktopCharacterPortraits.PortraitLightSettings lighting,
            DesktopCharacterPortraits.PortraitAnimationSettings animation,
            Vector3 lookCenter,
            float bodyHeight)
        {
            _root = root;
            _rt = rt;
            _animancer = animancer;
            _loco = loco;
            _animator = animator;
            _camera = camera;
            _faceA1 = faceA1;
            _faceA3 = faceA3;
            _keyLight = keyLight;
            _fillLight = fillLight;
            _lighting = lighting ?? new DesktopCharacterPortraits.PortraitLightSettings();
            _animation = animation ?? new DesktopCharacterPortraits.PortraitAnimationSettings();
            _lookCenter = lookCenter;
            _bodyHeight = bodyHeight;
        }

        public static CharacterPortraitSlot Create(
            string name,
            GameObject prefab,
            Vector3 worldPos,
            Transform stageParent,
            int layer,
            int textureSize,
            Color background,
            DesktopCharacterPortraits.PortraitLightSettings lighting,
            DesktopCharacterPortraits.PortraitAnimationSettings animation,
            bool createPortraitLights = true,
            string worldLabelText = null,
            Font worldLabelFont = null)
        {
            var root = new GameObject(name);
            root.transform.SetParent(stageParent, false);
            root.transform.position = worldPos;

            lighting ??= new DesktopCharacterPortraits.PortraitLightSettings();
            animation ??= new DesktopCharacterPortraits.PortraitAnimationSettings();

            GameObject actor = null;
            if (prefab != null)
                actor = SpawnStrippedActor(prefab, root.transform);

            if (actor != null)
                SetLayerRecursively(actor, layer);
            SetLayerRecursively(root, layer);
            FaceCameraAndFrame(root, actor, out Camera cam, out Vector3 lookCenter, out float bodyHeight);
            Light keyLight = null;
            Light fillLight = null;
            if (createPortraitLights)
            {
                keyLight = CreatePortraitLight(
                    "KeyLight",
                    root.transform,
                    cam.transform,
                    layer,
                    lighting.keyIntensity,
                    lighting.keyColor,
                    lighting.keyYaw,
                    lighting.keyPitch);
                fillLight = CreatePortraitLight(
                    "FillLight",
                    root.transform,
                    cam.transform,
                    layer,
                    lighting.fillIntensity,
                    lighting.fillColor,
                    lighting.fillYaw,
                    lighting.fillPitch);
            }

            var rt = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32)
            {
                name = name + "_RT",
                antiAliasing = 1,
                filterMode = FilterMode.Bilinear
            };
            rt.Create();

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.cullingMask = 1 << layer;
            cam.targetTexture = rt;
            cam.depth = -20;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 12f;
            cam.fieldOfView = 32f;
            cam.allowHDR = false;
            cam.allowMSAA = false;

            if (!string.IsNullOrWhiteSpace(worldLabelText))
            {
                CreateWorldLabel(
                    cam.transform,
                    layer,
                    worldLabelText,
                    worldLabelFont);
            }

            Animator animator = actor != null ? actor.GetComponentInChildren<Animator>(true) : null;
            GameObject faceA1 = actor != null ? FindChildObject(actor, "Face_A1") : null;
            GameObject faceA3 = actor != null ? FindChildObject(actor, "Face_A3") : null;
            var loco = actor != null ? actor.GetComponentInChildren<PlayerAnimancerLocomotion>(true) : null;
            var animancer = actor != null ? actor.GetComponentInChildren<AnimancerComponent>(true) : null;
            bool usePortraitClips = animation.entryClip != null || animation.idleClip != null;

            if (animancer == null && animator != null && usePortraitClips)
            {
                animancer = animator.gameObject.AddComponent<AnimancerComponent>();
                animancer.Animator = animator;
            }

            if (usePortraitClips && animator != null)
            {
                animator.runtimeAnimatorController = null;
                animator.applyRootMotion = false;
            }

            if (!usePortraitClips && loco != null && animancer != null)
                loco.Initialize(animancer);
            else if (!usePortraitClips && animancer != null)
                PlayFirstClip(animancer);
            else if (!usePortraitClips && animator != null)
                animator.Play(0, 0, 0f);

            var slot = new CharacterPortraitSlot(
                root,
                rt,
                animancer,
                loco,
                animator,
                cam,
                faceA1,
                faceA3,
                keyLight,
                fillLight,
                lighting,
                animation,
                lookCenter,
                bodyHeight);
            if (usePortraitClips)
                slot.PlayEntryThenIdle(animation);
            return slot;
        }

        public void Tick()
        {
            if (_feedbackPlaying)
            {
                if (Time.unscaledTime >= _feedbackEndsAt)
                {
                    _feedbackRepeatsRemaining--;
                    if (_feedbackRepeatsRemaining > 0)
                        BeginFeedbackPlayback();
                    else
                    {
                        _feedbackPlaying = false;
                        PlayIdle();
                    }
                }
                return;
            }

            if (!_entryPending)
                return;

            float now = Time.unscaledTime;
            if (!_entryStarted && now >= _entryStartAt)
            {
                _entryStarted = true;
                _animancer.Play(_entryClip, _animation.entryFade, FadeMode.FromStart);
                return;
            }

            if (_entryStarted && now >= _entryEndAt)
            {
                _entryPending = false;
                PlayIdle();
            }
        }

        public void PlayEntryThenIdle(DesktopCharacterPortraits.PortraitAnimationSettings animation)
        {
            SetFace(false);
            _entryPending = false;
            _entryStarted = false;
            _feedbackPlaying = false;
            _feedbackRepeatsRemaining = 0;
            _entryClip = animation != null ? animation.entryClip : null;

            if (_entryClip == null || _animancer == null)
            {
                PlayIdle();
                return;
            }

            float delay = Mathf.Max(0f, animation.entryDelay);
            _entryStartAt = Time.unscaledTime + delay;
            _entryEndAt = _entryStartAt + Mathf.Max(0.01f, _entryClip.length);
            _entryPending = true;
            PlayIdle();
        }

        public void ApplyFraming(float distanceMul, float fov, float lookHeightBias, Vector2 offset)
        {
            if (_camera == null)
                return;

            Vector3 center = _lookCenter + Vector3.up * lookHeightBias;
            float dist = Mathf.Max(0.4f, _bodyHeight * Mathf.Max(0.2f, distanceMul));
            Transform t = _camera.transform;
            t.position = center + Vector3.forward * dist;
            t.LookAt(center, Vector3.up);
            t.position += t.right * offset.x + t.up * offset.y;
            t.LookAt(center, Vector3.up);
            _camera.fieldOfView = Mathf.Clamp(fov, 10f, 80f);

            ApplyLightSettings();
        }

        void ApplyLightSettings()
        {
            if (_keyLight != null)
            {
                _keyLight.intensity = _lighting.keyIntensity;
                _keyLight.color = _lighting.keyColor;
                AimPortraitLight(_keyLight, _camera.transform, _lighting.keyYaw, _lighting.keyPitch);
            }

            if (_fillLight != null)
            {
                _fillLight.intensity = _lighting.fillIntensity;
                _fillLight.color = _lighting.fillColor;
                AimPortraitLight(_fillLight, _camera.transform, _lighting.fillYaw, _lighting.fillPitch);
            }
        }

        public void Play(AnimationClip clip, float fade = 0.2f)
        {
            if (clip == null)
                return;

            SetFace(false);
            _entryPending = false;
            _feedbackPlaying = false;
            _feedbackRepeatsRemaining = 0;
            if (_animancer != null)
            {
                _loco?.NotifyActionStarted();
                var state = _animancer.Play(clip, fade, FadeMode.FromStart);
                if (!clip.isLooping)
                {
                    var events = state.Events(this);
                    events.OnEnd = () =>
                    {
                        _loco?.NotifyActionEnded();
                        PlayIdle();
                    };
                }

                return;
            }

            if (_animator != null)
                _animator.CrossFadeInFixedTime(clip.name, fade);
        }

        public void PlayFeedback(
            AnimationClip clip,
            float fade = 0.18f,
            int repeatCount = 1,
            bool useAgonizedFace = false)
        {
            if (clip == null)
                return;

            SetFace(useAgonizedFace);
            _feedbackClip = clip;
            _feedbackFade = fade;
            _feedbackRepeatsRemaining = Mathf.Max(1, repeatCount);
            BeginFeedbackPlayback();
        }

        void BeginFeedbackPlayback()
        {
            if (_feedbackClip == null)
                return;

            _entryPending = false;
            _feedbackPlaying = true;
            _feedbackEndsAt = Time.unscaledTime + Mathf.Max(0.01f, _feedbackClip.length);

            if (_animancer != null)
            {
                _animancer.Play(_feedbackClip, _feedbackFade, FadeMode.FromStart);
                return;
            }

            if (_animator != null)
                _animator.CrossFadeInFixedTime(_feedbackClip.name, _feedbackFade);
        }

        public void PlayIdle()
        {
            _feedbackPlaying = false;
            _feedbackRepeatsRemaining = 0;
            SetFace(false);
            if (_animation.idleClip != null && _animancer != null)
            {
                _loco?.NotifyActionEnded();
                _animancer.Play(_animation.idleClip, _animation.idleFade, FadeMode.FromStart);
                return;
            }

            if (_loco != null && _animancer != null)
            {
                _loco.NotifyActionEnded();
                _loco.Initialize(_animancer);
                return;
            }

            if (_animator != null)
                _animator.Play(0, 0, 0f);
        }

        void SetFace(bool agonized)
        {
            if (_faceA1 != null)
                _faceA1.SetActive(!agonized);
            if (_faceA3 != null)
                _faceA3.SetActive(agonized);
        }

        public void Dispose()
        {
            if (_rt != null)
            {
                _rt.Release();
                Object.Destroy(_rt);
            }

            if (_root != null)
                Object.Destroy(_root);
        }

        static GameObject SpawnStrippedActor(GameObject prefab, Transform parent)
        {
            var holder = new GameObject("PortraitSpawnHolder");
            holder.SetActive(false);
            var actor = Object.Instantiate(prefab, holder.transform);
            actor.name = prefab.name;
            actor.transform.localPosition = Vector3.zero;
            actor.transform.localRotation = Quaternion.identity;
            StripGameplay(actor);
            actor.transform.SetParent(parent, false);
            Object.Destroy(holder);
            actor.SetActive(true);
            return actor;
        }

        static void StripGameplay(GameObject actor)
        {
            DestroyAll<PlayerInputReader>(actor);
            DestroyAll<ThirdPersonMotor>(actor);
            DestroyAll<ThirdPersonPlayerController>(actor);
            DestroyAll<PlayerHandheldCamera>(actor);
            DestroyAll<ThirdPersonSpringArm>(actor);
            DestroyAll<CharacterController>(actor);
            DestroyAll<AudioListener>(actor);
            StripCameras(actor);
            DestroyAll<Layer_lab._3D_Casual_Character.CharacterControl>(actor);
        }

        static void StripCameras(GameObject actor)
        {
            var cams = actor.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cams.Length; i++)
            {
                var cam = cams[i];
                if (cam == null)
                    continue;

                var extra = cam.GetComponent<UniversalAdditionalCameraData>();
                if (extra != null)
                    Object.DestroyImmediate(extra);

                var go = cam.gameObject;
                Object.DestroyImmediate(cam);
                if (go != null && go != actor)
                    Object.DestroyImmediate(go);
            }
        }

        static void DestroyAll<T>(GameObject actor) where T : Component
        {
            var comps = actor.GetComponentsInChildren<T>(true);
            for (int i = 0; i < comps.Length; i++)
            {
                if (comps[i] != null)
                    Object.DestroyImmediate(comps[i]);
            }
        }

        static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            var t = go.transform;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursively(t.GetChild(i).gameObject, layer);
        }

        static GameObject FindChildObject(GameObject root, string objectName)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == objectName)
                    return transforms[i].gameObject;
            }
            return null;
        }

        static void CreateWorldLabel(
            Transform cameraTransform,
            int layer,
            string textValue,
            Font fontAsset)
        {
            var canvasGo = new GameObject(
                "ThiefRoleLabel3D",
                typeof(RectTransform),
                typeof(Canvas));
            RectTransform canvasRect =
                canvasGo.GetComponent<RectTransform>();
            canvasRect.SetParent(cameraTransform, false);
            canvasRect.localPosition = new Vector3(-0.04f, -0.24f, 2.3f);
            canvasRect.localRotation = Quaternion.identity;
            canvasRect.localScale = Vector3.one * 0.0038f;
            canvasRect.sizeDelta = new Vector2(190f, 70f);

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cameraTransform.GetComponent<Camera>();
            canvas.sortingOrder = 90;

            var textGo = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(UnityEngine.UI.Text));
            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.SetParent(canvasRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var label = textGo.GetComponent<UnityEngine.UI.Text>();
            label.font = ResolvePortraitLabelFont(fontAsset);
            label.text = textValue;
            label.fontSize = 42;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.color = new Color(0.72f, 0.28f, 1f, 1f);
            label.raycastTarget = false;

            var outline =
                textGo.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(3f, -3f);
            var shadow =
                textGo.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = new Color(0.015f, 0.02f, 0.05f, 0.9f);
            shadow.effectDistance = new Vector2(3.5f, -3.5f);

            SetLayerRecursively(canvasGo, layer);
        }

        static Font ResolvePortraitLabelFont(Font fontAsset)
        {
            if (fontAsset != null)
                return fontAsset;

            string[] preferred =
            {
                "Microsoft YaHei",
                "Microsoft YaHei UI",
                "SimHei",
                "Arial Unicode MS",
            };
            string[] installed = Font.GetOSInstalledFontNames();
            for (int i = 0; i < preferred.Length; i++)
            {
                for (int j = 0; j < installed.Length; j++)
                {
                    if (installed[j] == preferred[i])
                    {
                        return Font.CreateDynamicFontFromOSFont(
                            preferred[i],
                            42);
                    }
                }
            }

            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        static Light CreatePortraitLight(
            string name,
            Transform parent,
            Transform cameraTransform,
            int layer,
            float intensity,
            Color color,
            float yaw,
            float pitch)
        {
            var lightGo = new GameObject(name);
            lightGo.transform.SetParent(parent, false);

            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.color = color;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
            light.cullingMask = 1 << layer;
            AimPortraitLight(light, cameraTransform, yaw, pitch);
            return light;
        }

        static void AimPortraitLight(Light light, Transform cameraTransform, float yaw, float pitch)
        {
            if (light == null || cameraTransform == null)
                return;

            Vector3 direction = Quaternion.AngleAxis(yaw, Vector3.up) * cameraTransform.forward;
            direction = (direction + Vector3.down * Mathf.Tan(pitch * Mathf.Deg2Rad)).normalized;
            light.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        static void FaceCameraAndFrame(
            GameObject root,
            GameObject actor,
            out Camera cam,
            out Vector3 lookCenter,
            out float bodyHeight)
        {
            var camGo = new GameObject("PortraitCamera");
            camGo.transform.SetParent(root.transform, false);
            cam = camGo.AddComponent<Camera>();

            Vector3 center = root.transform.position + Vector3.up * 0.95f;
            float height = 1.7f;
            if (actor != null)
            {
                var rends = actor.GetComponentsInChildren<Renderer>();
                if (rends != null && rends.Length > 0)
                {
                    var bounds = rends[0].bounds;
                    bool any = false;
                    for (int i = 0; i < rends.Length; i++)
                    {
                        if (!rends[i].enabled || !rends[i].gameObject.activeInHierarchy)
                            continue;
                        if (!any)
                        {
                            bounds = rends[i].bounds;
                            any = true;
                        }
                        else
                            bounds.Encapsulate(rends[i].bounds);
                    }

                    if (any)
                    {
                        center = bounds.center;
                        height = Mathf.Max(0.8f, bounds.size.y);
                    }
                }

            }

            float dist = height * 1.85f;
            camGo.transform.position = center + Vector3.forward * dist;
            camGo.transform.LookAt(center, Vector3.up);

            if (actor != null)
            {
                Transform face = actor.transform;
                var anim = actor.GetComponentInChildren<Animator>(true);
                if (anim != null)
                    face = anim.transform;

                Vector3 toCam = camGo.transform.position - face.position;
                toCam.y = 0f;
                Vector3 visualFwd = face.forward;
                visualFwd.y = 0f;
                if (toCam.sqrMagnitude > 0.001f && visualFwd.sqrMagnitude > 0.001f)
                {
                    Quaternion align = Quaternion.FromToRotation(visualFwd.normalized, toCam.normalized);
                    actor.transform.rotation = align * actor.transform.rotation;
                }
            }

            lookCenter = center;
            bodyHeight = height;
        }

        static void PlayFirstClip(AnimancerComponent animancer)
        {
            if (animancer.Animator == null)
                return;
            var clips = animancer.Animator.runtimeAnimatorController != null
                ? animancer.Animator.runtimeAnimatorController.animationClips
                : null;
            if (clips != null && clips.Length > 0)
                animancer.Play(clips[0]);
        }
    }
}
