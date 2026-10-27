using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AntiFraudStorm.UI
{
    public class BeginningScreenController : MonoBehaviour
    {
        [SerializeField] Texture2D coverTexture;
        [SerializeField] Texture2D mapTexture;
        [SerializeField] Sprite mapLandmarkSprite;
        [SerializeField] Font labelFont;
        [SerializeField] string schoolSceneName = "School";
        [SerializeField, Min(0f)] float floatAmplitude = 10f;
        [SerializeField, Min(0f)] float floatSpeed = 1.6f;
        [SerializeField, Min(1f)] float mapScale = 1.35f;
        [SerializeField, Min(1f)] float edgePanSpeed = 820f;
        [SerializeField, Min(1f)] float edgePanThreshold = 240f;
        [SerializeField] Color homeColor = new Color(0.95f, 0.4f, 0.32f, 0.96f);
        [SerializeField] Color studyColor = new Color(0.18f, 0.62f, 0.92f, 0.96f);
        [SerializeField] Color bankColor = new Color(0.17f, 0.7f, 0.49f, 0.96f);

        RectTransform _startButtonRect;
        GameObject _coverObject;
        GameObject _startButtonObject;
        GameObject _mapRoot;
        Coroutine _startFloatRoutine;
        bool _mapOpen;
        bool _loadingSchool;
        Sprite _labelBackgroundSprite;
        Texture2D _labelBackgroundTexture;

        void Awake()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            EnsureEventSystem();
            BuildInterface();
        }

        void BuildInterface()
        {
            var canvasGo = new GameObject(
                "Beginning UI",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();

            var coverGo = new GameObject(
                "Cover",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage),
                typeof(AspectRatioFitter));
            RectTransform coverRect = coverGo.GetComponent<RectTransform>();
            coverRect.SetParent(canvasRect, false);
            coverRect.anchorMin = Vector2.zero;
            coverRect.anchorMax = Vector2.one;
            coverRect.offsetMin = Vector2.zero;
            coverRect.offsetMax = Vector2.zero;

            RawImage cover = coverGo.GetComponent<RawImage>();
            cover.texture = coverTexture;
            cover.color = Color.white;
            cover.raycastTarget = false;
            _coverObject = coverGo;

            AspectRatioFitter fitter =
                coverGo.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = coverTexture != null && coverTexture.height > 0
                ? (float)coverTexture.width / coverTexture.height
                : 16f / 9f;

            var buttonGo = new GameObject(
                "Start Button",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            _startButtonRect = buttonGo.GetComponent<RectTransform>();
            _startButtonRect.SetParent(canvasRect, false);
            _startButtonRect.anchorMin = new Vector2(0.5f, 0f);
            _startButtonRect.anchorMax = new Vector2(0.5f, 0f);
            _startButtonRect.pivot = new Vector2(0.5f, 0f);
            _startButtonRect.anchoredPosition = new Vector2(0f, 82f);
            _startButtonRect.sizeDelta = new Vector2(460f, 112f);

            Image hitArea = buttonGo.GetComponent<Image>();
            hitArea.color = new Color(1f, 1f, 1f, 0f);
            hitArea.raycastTarget = true;

            Button button = buttonGo.GetComponent<Button>();
            button.targetGraphic = hitArea;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(StartGame);
            _startButtonObject = buttonGo;

            var textGo = new GameObject(
                "Start Text",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.SetParent(_startButtonRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text label = textGo.GetComponent<Text>();
            label.font = ResolveFont();
            label.text = "点击开始";
            label.fontSize = 62;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;

            var outline = textGo.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(4f, -4f);

            var shadow = textGo.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
            shadow.effectDistance = new Vector2(2f, -2f);

            BuildMapScreen(canvasRect);
        }

        void Start()
        {
            if (_startButtonRect != null)
                _startFloatRoutine = StartCoroutine(FloatButtonRoutine());
        }

        IEnumerator FloatButtonRoutine()
        {
            Vector2 basePosition = _startButtonRect.anchoredPosition;
            float startedAt = Time.unscaledTime;
            while (true)
            {
                float wave = Mathf.Sin(
                    (Time.unscaledTime - startedAt) * floatSpeed);
                _startButtonRect.anchoredPosition =
                    basePosition + Vector2.up * (wave * floatAmplitude);
                _startButtonRect.localScale =
                    Vector3.one * (1f + Mathf.Abs(wave) * 0.025f);
                yield return null;
            }
        }

        void StartGame()
        {
            if (_mapOpen)
                return;
            _mapOpen = true;
            if (_startFloatRoutine != null)
            {
                StopCoroutine(_startFloatRoutine);
                _startFloatRoutine = null;
            }
            if (_coverObject != null)
                _coverObject.SetActive(false);
            if (_startButtonObject != null)
                _startButtonObject.SetActive(false);
            if (_mapRoot != null)
                _mapRoot.SetActive(true);
        }

        void LoadSchool()
        {
            if (_loadingSchool)
                return;
            _loadingSchool = true;
            SceneManager.LoadScene(schoolSceneName, LoadSceneMode.Single);
        }

        Sprite CreateLabelBackgroundSprite()
        {
            const int size = 64;
            const float radius = 30f;
            _labelBackgroundTexture =
                new Texture2D(size, size, TextureFormat.RGBA32, false);
            _labelBackgroundTexture.name = "Map Label Rounded Rectangle";
            _labelBackgroundTexture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nearestX = Mathf.Clamp(
                        x,
                        radius,
                        size - 1f - radius);
                    float nearestY = Mathf.Clamp(
                        y,
                        radius,
                        size - 1f - radius);
                    float dx = x - nearestX;
                    float dy = y - nearestY;
                    bool inside = dx * dx + dy * dy <= radius * radius;
                    pixels[y * size + x] = inside
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(255, 255, 255, 0);
                }
            }

            _labelBackgroundTexture.SetPixels32(pixels);
            _labelBackgroundTexture.Apply(false, true);
            return Sprite.Create(
                _labelBackgroundTexture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(30f, 30f, 30f, 30f));
        }

        void CreateDashedRoute(
            RectTransform parent,
            params Vector2[] waypoints)
        {
            if (waypoints == null || waypoints.Length < 2)
                return;

            const int samplesPerSpan = 28;
            var normalizedPoints = new List<Vector2>();
            for (int span = 0; span < waypoints.Length - 1; span++)
            {
                Vector2 p0 = waypoints[Mathf.Max(0, span - 1)];
                Vector2 p1 = waypoints[span];
                Vector2 p2 = waypoints[span + 1];
                Vector2 p3 = waypoints[Mathf.Min(
                    waypoints.Length - 1,
                    span + 2)];

                for (int step = 0; step < samplesPerSpan; step++)
                {
                    float t = step / (float)samplesPerSpan;
                    AddUnique(
                        normalizedPoints,
                        CatmullRom(p0, p1, p2, p3, t));
                }
            }
            AddUnique(
                normalizedPoints,
                waypoints[waypoints.Length - 1]);

            Vector2 size = parent.sizeDelta;
            var points = new List<Vector2>(normalizedPoints.Count);
            var distances = new List<float>(normalizedPoints.Count);
            float totalLength = 0f;
            for (int i = 0; i < normalizedPoints.Count; i++)
            {
                Vector2 normalized = normalizedPoints[i];
                Vector2 point = new Vector2(
                    (normalized.x - 0.5f) * size.x,
                    (normalized.y - 0.5f) * size.y);
                if (points.Count > 0)
                    totalLength += Vector2.Distance(points[points.Count - 1], point);
                points.Add(point);
                distances.Add(totalLength);
            }

            const float dashLength = 72f;
            const float gapLength = 42f;
            float cursor = 0f;
            int dashIndex = 1;
            while (cursor < totalLength)
            {
                float dashEnd = Mathf.Min(cursor + dashLength, totalLength);
                Vector2 start = PointAtDistance(
                    points,
                    distances,
                    cursor);
                Vector2 end = PointAtDistance(
                    points,
                    distances,
                    dashEnd);
                if ((end - start).sqrMagnitude > 0.5f)
                {
                    CreateRoadDash(
                        parent,
                        dashIndex++,
                        start,
                        end);
                }
                cursor += dashLength + gapLength;
            }
        }

        static void AddUnique(List<Vector2> points, Vector2 point)
        {
            if (points.Count == 0 ||
                (points[points.Count - 1] - point).sqrMagnitude > 0.000001f)
            {
                points.Add(point);
            }
        }

        static Vector2 CatmullRom(
            Vector2 p0,
            Vector2 p1,
            Vector2 p2,
            Vector2 p3,
            float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (
                2f * p1 +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        static Vector2 PointAtDistance(
            List<Vector2> points,
            List<float> distances,
            float distance)
        {
            if (distance <= 0f)
                return points[0];
            if (distance >= distances[distances.Count - 1])
                return points[points.Count - 1];

            int low = 0;
            int high = distances.Count - 1;
            while (low < high)
            {
                int middle = (low + high) / 2;
                if (distances[middle] < distance)
                    low = middle + 1;
                else
                    high = middle;
            }

            int index = Mathf.Max(1, low);
            float segmentStart = distances[index - 1];
            float segmentLength =
                Mathf.Max(0.0001f, distances[index] - segmentStart);
            float t = (distance - segmentStart) / segmentLength;
            return Vector2.Lerp(points[index - 1], points[index], t);
        }

        static void CreateRoadDash(
            RectTransform parent,
            int index,
            Vector2 start,
            Vector2 end)
        {
            Vector2 delta = end - start;
            var dashGo = new GameObject(
                $"Map Road Dash {index}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            RectTransform dashRect = dashGo.GetComponent<RectTransform>();
            dashRect.SetParent(parent, false);
            dashRect.anchorMin = new Vector2(0.5f, 0.5f);
            dashRect.anchorMax = new Vector2(0.5f, 0.5f);
            dashRect.pivot = new Vector2(0.5f, 0.5f);
            dashRect.anchoredPosition = (start + end) * 0.5f;
            dashRect.sizeDelta = new Vector2(delta.magnitude, 30f);
            dashRect.localRotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            Image dash = dashGo.GetComponent<Image>();
            dash.color = new Color(0.04f, 0.08f, 0.1f, 0.98f);
            dash.raycastTarget = false;

            var innerGo = new GameObject(
                "Dash Fill",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            RectTransform innerRect = innerGo.GetComponent<RectTransform>();
            innerRect.SetParent(dashRect, false);
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(0f, 7.5f);
            innerRect.offsetMax = new Vector2(0f, -7.5f);
            Image inner = innerGo.GetComponent<Image>();
            inner.color = new Color(1f, 1f, 1f, 0.94f);
            inner.raycastTarget = false;
        }

        void CreateMapLabel(
            RectTransform parent,
            string textValue,
            float x,
            float y,
            bool isSchool,
            float animationPhase,
            Color backgroundColor)
        {
            var buttonGo = new GameObject(
                isSchool ? "School Map Button" : $"{textValue} Map Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(MapLabelFloat));
            RectTransform rect = buttonGo.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(x, y);
            rect.anchorMax = new Vector2(x, y);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            Image hitArea = buttonGo.GetComponent<Image>();
            hitArea.color = Color.clear;
            hitArea.raycastTarget = true;

            const float iconSize = 84f;
            const float iconGap = 14f;
            const float innerHeight = 94f;
            const float borderWidth = 6f;
            float innerWidth = Mathf.Clamp(
                textValue.Length * 46f + 34f,
                98f,
                150f);
            float outerWidth = innerWidth + borderWidth * 2f;
            float outerHeight = innerHeight + borderWidth * 2f;
            float totalWidth = iconSize + iconGap + outerWidth;
            rect.sizeDelta = new Vector2(totalWidth, outerHeight + 14f);

            var backgroundGo = new GameObject(
                "Text Background Border",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            RectTransform backgroundRect =
                backgroundGo.GetComponent<RectTransform>();
            backgroundRect.SetParent(rect, false);
            backgroundRect.anchorMin = new Vector2(0.5f, 0.5f);
            backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
            backgroundRect.pivot = new Vector2(0.5f, 0.5f);
            backgroundRect.anchoredPosition = new Vector2(
                -totalWidth * 0.5f +
                iconSize +
                iconGap +
                outerWidth * 0.5f,
                0f);
            backgroundRect.sizeDelta = new Vector2(
                outerWidth,
                outerHeight);
            Image background = backgroundGo.GetComponent<Image>();
            background.sprite = _labelBackgroundSprite;
            background.type = Image.Type.Sliced;
            background.color = new Color(0.04f, 0.07f, 0.08f, 0.98f);
            background.raycastTarget = false;

            var fillGo = new GameObject(
                "Text Background",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            RectTransform fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.SetParent(backgroundRect, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(borderWidth, borderWidth);
            fillRect.offsetMax = new Vector2(-borderWidth, -borderWidth);
            Image fill = fillGo.GetComponent<Image>();
            fill.sprite = _labelBackgroundSprite;
            fill.type = Image.Type.Sliced;
            fill.color = backgroundColor;
            fill.raycastTarget = false;

            Button button = buttonGo.GetComponent<Button>();
            button.targetGraphic = hitArea;
            button.transition = Selectable.Transition.None;
            if (isSchool)
                button.onClick.AddListener(LoadSchool);

            Image icon = new GameObject(
                "Landmark Icon",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)).GetComponent<Image>();
            RectTransform iconRect = icon.rectTransform;
            iconRect.SetParent(rect, false);
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(
                -totalWidth * 0.5f + iconSize * 0.5f,
                0f);
            iconRect.sizeDelta = new Vector2(iconSize, iconSize);
            icon.sprite = mapLandmarkSprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var textGo = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.SetParent(fillRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(6f, 0f);
            textRect.offsetMax = new Vector2(-6f, 0f);

            Text label = textGo.GetComponent<Text>();
            label.font = ResolveFont();
            label.text = textValue;
            label.fontSize = 44;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;

            var outline = textGo.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(4f, -4f);

            var shadow = textGo.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
            shadow.effectDistance = new Vector2(2f, -2f);

            MapLabelFloat motion =
                buttonGo.GetComponent<MapLabelFloat>();
            motion.Configure(animationPhase, fill, backgroundColor);
        }

        void BuildMapScreen(RectTransform canvasRect)
        {
            _mapRoot = new GameObject(
                "Map Screen",
                typeof(RectTransform),
                typeof(Image),
                typeof(RectMask2D),
                typeof(MapDragPanController));
            RectTransform viewport =
                _mapRoot.GetComponent<RectTransform>();
            viewport.SetParent(canvasRect, false);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;

            Image dragSurface = _mapRoot.GetComponent<Image>();
            dragSurface.color = new Color(0.08f, 0.14f, 0.19f, 1f);
            dragSurface.raycastTarget = true;

            var contentGo = new GameObject(
                "Map Content",
                typeof(RectTransform),
                typeof(RawImage));
            RectTransform content =
                contentGo.GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0.5f, 0.5f);
            content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot = new Vector2(0.5f, 0.5f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(1920f, 1080f) * mapScale;

            RawImage map = contentGo.GetComponent<RawImage>();
            map.texture = mapTexture;
            map.color = Color.white;
            map.raycastTarget = false;

            MapDragPanController drag =
                _mapRoot.GetComponent<MapDragPanController>();
            drag.Configure(
                content,
                viewport,
                edgePanThreshold,
                edgePanSpeed);

            _labelBackgroundSprite = CreateLabelBackgroundSprite();

            CreateDashedRoute(
                content,
                new Vector2(0.17f, 0.22f),
                new Vector2(0.18f, 0.34f),
                new Vector2(0.29f, 0.48f),
                new Vector2(0.38f, 0.56f),
                new Vector2(0.45f, 0.57f),
                new Vector2(0.52f, 0.67f));
            CreateDashedRoute(
                content,
                new Vector2(0.52f, 0.67f),
                new Vector2(0.59f, 0.64f),
                new Vector2(0.65f, 0.58f),
                new Vector2(0.72f, 0.57f),
                new Vector2(0.78f, 0.50f),
                new Vector2(0.83f, 0.41f));

            CreateMapLabel(content, "家", 0.17f, 0.22f, false, 0f, homeColor);
            CreateMapLabel(content, "学校", 0.52f, 0.67f, true, 0.8f, studyColor);
            CreateMapLabel(content, "银行", 0.83f, 0.41f, false, 1.6f, bankColor);
            _mapRoot.SetActive(false);
        }

        Font ResolveFont()
        {
            if (labelFont != null)
                return labelFont;

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
                            62);
                    }
                }
            }

            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        static void EnsureEventSystem()
        {
            EventSystem[] systems = FindObjectsOfType<EventSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] != null && systems[i].isActiveAndEnabled)
                    return;
            }

            new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule));
        }

        void OnDestroy()
        {
            if (_labelBackgroundSprite != null)
                Destroy(_labelBackgroundSprite);
            if (_labelBackgroundTexture != null)
                Destroy(_labelBackgroundTexture);
        }
    }

    public class MapDragPanController : MonoBehaviour
    {
        RectTransform _map;
        RectTransform _viewport;
        float _edgeThreshold = 240f;
        float _panSpeed = 820f;
        Vector2 _velocity;

        public void Configure(
            RectTransform map,
            RectTransform viewport,
            float edgeThreshold,
            float panSpeed)
        {
            _map = map;
            _viewport = viewport;
            _edgeThreshold = Mathf.Max(1f, edgeThreshold);
            _panSpeed = Mathf.Max(1f, panSpeed);
        }

        void LateUpdate()
        {
            if (_map == null || _viewport == null)
                return;

            Vector3 mouse = Input.mousePosition;
            bool pointerInside =
                mouse.x >= 0f && mouse.x <= Screen.width &&
                mouse.y >= 0f && mouse.y <= Screen.height;
            float horizontal = pointerInside
                ? EdgeStrength(mouse.x, Screen.width)
                : 0f;
            float vertical = pointerInside
                ? EdgeStrength(mouse.y, Screen.height)
                : 0f;
            Vector2 direction = new Vector2(horizontal, vertical);
            if (direction.sqrMagnitude > 1f)
                direction.Normalize();

            float deltaTime = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            Vector2 targetVelocity = direction * _panSpeed;
            float changeRate = targetVelocity.sqrMagnitude > 0.001f
                ? 1200f
                : 720f;
            _velocity = Vector2.MoveTowards(
                _velocity,
                targetVelocity,
                changeRate * deltaTime);

            Vector2 position = _map.anchoredPosition +
                _velocity * deltaTime;
            Vector2 mapSize = _map.rect.size;
            Vector2 viewSize = _viewport.rect.size;
            float limitX = Mathf.Max(0f, (mapSize.x - viewSize.x) * 0.5f);
            float limitY = Mathf.Max(0f, (mapSize.y - viewSize.y) * 0.5f);
            float clampedX = Mathf.Clamp(position.x, -limitX, limitX);
            float clampedY = Mathf.Clamp(position.y, -limitY, limitY);
            if (!Mathf.Approximately(clampedX, position.x))
                _velocity.x = 0f;
            if (!Mathf.Approximately(clampedY, position.y))
                _velocity.y = 0f;
            position = new Vector2(clampedX, clampedY);
            _map.anchoredPosition = position;
        }

        float EdgeStrength(float position, float extent)
        {
            if (position <= _edgeThreshold)
            {
                return SmoothStepQuintic(
                    1f - Mathf.Clamp01(position / _edgeThreshold));
            }
            if (position >= extent - _edgeThreshold)
            {
                return -SmoothStepQuintic(
                    1f - Mathf.Clamp01(
                        (extent - position) / _edgeThreshold));
            }
            return 0f;
        }

        static float SmoothStepQuintic(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }
    }

    public class MapLabelFloat :
        MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        RectTransform _rect;
        Image _background;
        Vector2 _basePosition;
        Color _baseColor;
        float _phase;
        bool _hovered;
        float _hoverBlend;

        public void Configure(
            float phase,
            Image background,
            Color baseColor)
        {
            _rect = transform as RectTransform;
            _basePosition = _rect != null
                ? _rect.anchoredPosition
                : Vector2.zero;
            _phase = phase;
            _background = background;
            _baseColor = baseColor;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
        }

        void Update()
        {
            if (_rect == null)
                return;

            float time = Time.unscaledTime * 2.1f + _phase;
            float wave = Mathf.Sin(time);
            _hoverBlend = Mathf.MoveTowards(
                _hoverBlend,
                _hovered ? 1f : 0f,
                Time.unscaledDeltaTime * 6f);
            _rect.anchoredPosition =
                _basePosition + Vector2.up * (wave * 7f);
            _rect.localScale =
                Vector3.one *
                (1f + Mathf.Abs(wave) * 0.035f + _hoverBlend * 0.12f);

            if (_background != null)
            {
                Color hoverColor = Color.Lerp(
                    _baseColor,
                    Color.white,
                    0.28f);
                _background.color =
                    Color.Lerp(_baseColor, hoverColor, _hoverBlend);
            }
        }
    }
}
