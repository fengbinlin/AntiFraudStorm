using AntiFraudStorm.Evidence;
using AntiFraudStorm.Player;
using UnityEngine;
using UnityEngine.UIElements;

namespace AntiFraudStorm.UI
{
    /// <summary>
    /// Third-person HUD: coins, main-quest bar, top-right icon actions.
    /// Hidden while the camera is in first person.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GameplayHudController : MonoBehaviour
    {
        [SerializeField] UIDocument document;
        [SerializeField] ThirdPersonSpringArm springArm;
        [SerializeField] int startingCoins = 1280;
        [SerializeField] string startingQuestTitle = "办理入学注册";
        [SerializeField] string startingQuestDistance = "";

        [Header("Icons (Assets/ImportResource/ImageAsset)")]
        [SerializeField] Texture2D coinIcon;
        [SerializeField] Texture2D mapIcon;
        [SerializeField] Texture2D aiIcon;
        [SerializeField] Texture2D phoneIcon;
        [SerializeField] Texture2D logIcon;
        [SerializeField] Texture2D settingsIcon;
        [SerializeField] Texture2D questMarkerIcon;

        [Header("Typography")]
        [SerializeField] Font hudFont;

        VisualElement _root;
        VisualElement _questBar;
        VisualElement _journal;
        VisualElement _coinIcon;
        VisualElement _imgMap;
        VisualElement _imgAi;
        VisualElement _imgPhone;
        VisualElement _imgLog;
        VisualElement _imgSettings;
        VisualElement _imgQuestMarker;
        VisualElement _journalLeftEntry;
        VisualElement _journalRightEntry;
        VisualElement _journalLeftEmpty;
        VisualElement _journalRightEmpty;
        VisualElement _journalLeftPhoto;
        VisualElement _journalRightPhoto;
        Label _coinAmount;
        Label _questTitle;
        Label _questDistance;
        Label _journalLeftTitle;
        Label _journalRightTitle;
        Label _journalLeftSummary;
        Label _journalRightSummary;
        Label _journalPageIndicator;
        Button _btnMap;
        Button _btnAi;
        Button _btnPhone;
        Button _btnLog;
        Button _btnJournalClose;
        Button _btnJournalPrevious;
        Button _btnJournalNext;
        Button _btnSettings;

        Texture2D _cyanGradient;
        int _coins;
        string _questTitleText;
        string _questDistanceText;
        bool _bound;
        bool _questPulseScheduled;
        bool _journalOpen;
        IVisualElementScheduledItem _journalUnreadAnimation;
        int _journalPageIndex;

        public static GameplayHudController Instance { get; private set; }

        public int Coins => _coins;

        void Awake()
        {
            if (document == null)
                document = GetComponent<UIDocument>();

            Instance = this;
            _coins = Mathf.Max(0, startingCoins);
            _questTitleText = startingQuestTitle;
            _questDistanceText = startingQuestDistance;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            if (_cyanGradient != null)
                Destroy(_cyanGradient);
        }

        void OnEnable()
        {
            Bind();
            SetCoins(_coins);
            SetMainQuest(_questTitleText);
            SetQuestDistance(_questDistanceText);
            RefreshVisibility();
        }

        void OnDisable()
        {
            StopEvidenceIconBounce();
            UnbindButtons();
        }

        void LateUpdate()
        {
            if (springArm == null)
                springArm = FindObjectOfType<ThirdPersonSpringArm>();

            RefreshVisibility();
        }

        void Bind()
        {
            if (document == null)
                return;

            var rootVe = document.rootVisualElement;
            if (rootVe == null)
                return;

            _root = rootVe.Q<VisualElement>("root");
            _questBar = rootVe.Q<VisualElement>("quest-bar");
            _journal = rootVe.Q<VisualElement>("anti-fraud-journal");
            _coinIcon = rootVe.Q<VisualElement>("coin-icon");
            _coinAmount = rootVe.Q<Label>("coin-amount");
            _questTitle = rootVe.Q<Label>("quest-title");
            _questDistance = rootVe.Q<Label>("quest-distance");
            _imgMap = rootVe.Q<VisualElement>("img-map");
            _imgAi = rootVe.Q<VisualElement>("img-ai");
            _imgPhone = rootVe.Q<VisualElement>("img-phone");
            _imgLog = rootVe.Q<VisualElement>("img-log");
            _imgSettings = rootVe.Q<VisualElement>("img-settings");
            _imgQuestMarker = rootVe.Q<VisualElement>("img-quest-marker");
            _journalLeftEntry = rootVe.Q<VisualElement>("journal-left-entry");
            _journalRightEntry = rootVe.Q<VisualElement>("journal-right-entry");
            _journalLeftEmpty = rootVe.Q<VisualElement>("journal-left-empty");
            _journalRightEmpty = rootVe.Q<VisualElement>("journal-right-empty");
            _journalLeftTitle = rootVe.Q<Label>("journal-left-title");
            _journalRightTitle = rootVe.Q<Label>("journal-right-title");
            _journalLeftPhoto = rootVe.Q<VisualElement>("journal-left-photo");
            _journalRightPhoto = rootVe.Q<VisualElement>("journal-right-photo");
            _journalLeftSummary = rootVe.Q<Label>("journal-left-summary");
            _journalRightSummary = rootVe.Q<Label>("journal-right-summary");
            _journalPageIndicator = rootVe.Q<Label>("journal-page-indicator");
            _btnMap = rootVe.Q<Button>("btn-map");
            _btnAi = rootVe.Q<Button>("btn-ai");
            _btnPhone = rootVe.Q<Button>("btn-phone");
            _btnLog = rootVe.Q<Button>("btn-log");
            _btnJournalClose = rootVe.Q<Button>("btn-journal-close");
            _btnJournalPrevious = rootVe.Q<Button>("btn-journal-prev");
            _btnJournalNext = rootVe.Q<Button>("btn-journal-next");
            _btnSettings = rootVe.Q<Button>("btn-settings");

            EnsureGradients();
            ApplyGradientToClass(rootVe, "hud-icon-btn__disc", _cyanGradient);
            ApplyIcons();
            ApplyHudTypography(rootVe);
            ApplyHudSizes(rootVe);

            if (!_questPulseScheduled && _questBar != null)
            {
                _questBar.schedule.Execute(PulseQuestBar).Every(900);
                _questPulseScheduled = true;
            }

            if (!_bound)
            {
                _btnMap?.RegisterCallback<ClickEvent>(OnMapClicked);
                _btnAi?.RegisterCallback<ClickEvent>(OnAiClicked);
                _btnPhone?.RegisterCallback<ClickEvent>(OnPhoneClicked);
                _btnLog?.RegisterCallback<ClickEvent>(OnLogClicked);
                _btnJournalClose?.RegisterCallback<ClickEvent>(OnJournalCloseClicked);
                _btnJournalPrevious?.RegisterCallback<ClickEvent>(OnJournalPreviousClicked);
                _btnJournalNext?.RegisterCallback<ClickEvent>(OnJournalNextClicked);
                _btnSettings?.RegisterCallback<ClickEvent>(OnSettingsClicked);
                _bound = true;
            }
        }

        void ApplyIcons()
        {
            SetBackground(_coinIcon, coinIcon);
            SetBackground(_imgMap, mapIcon);
            SetBackground(_imgAi, aiIcon);
            SetBackground(_imgPhone, phoneIcon);
            SetBackground(_imgLog, logIcon);
            SetBackground(_imgSettings, settingsIcon);
            SetBackground(_imgQuestMarker, questMarkerIcon != null ? questMarkerIcon : mapIcon);
        }

        void ApplyHudTypography(VisualElement rootVe)
        {
            if (rootVe == null)
                return;

            rootVe.Query(className: "hud-text").ForEach(el =>
            {
                if (hudFont != null)
                    el.style.unityFontDefinition = new StyleFontDefinition(hudFont);

                el.style.color = Color.white;
                el.style.unityTextOutlineWidth = 0f;
            });
        }

        void ApplyHudSizes(VisualElement rootVe)
        {
            var questSpacer = rootVe.Q<VisualElement>("quest-spacer");
            if (questSpacer != null)
            {
                questSpacer.style.height = 96f;
                questSpacer.style.minHeight = 96f;
                questSpacer.style.flexShrink = 0;
            }

            if (_coinIcon != null)
            {
                _coinIcon.style.width = 108f;
                _coinIcon.style.height = 108f;
            }

            if (_coinAmount != null)
                _coinAmount.style.fontSize = 52f;

            if (_questTitle != null)
                _questTitle.style.fontSize = 36f;
            if (_questDistance != null)
                _questDistance.style.fontSize = 34f;

            var questTag = rootVe.Q<Label>("quest-tag");
            if (questTag != null)
                questTag.style.fontSize = 32f;

            if (_imgQuestMarker != null)
            {
                _imgQuestMarker.style.width = 72f;
                _imgQuestMarker.style.height = 72f;
            }

            var questMarker = rootVe.Q<VisualElement>("quest-marker");
            if (questMarker != null)
            {
                questMarker.style.width = 72f;
                questMarker.style.height = 72f;
            }

            rootVe.Query(className: "hud-icon-btn__disc").ForEach(el =>
            {
                el.style.width = 92f;
                el.style.height = 92f;
            });
            rootVe.Query(className: "hud-icon-btn__icon").ForEach(el =>
            {
                el.style.width = 84f;
                el.style.height = 84f;
            });
            rootVe.Query(className: "hud-icon-btn__caption").ForEach(el =>
            {
                el.style.fontSize = 26f;
            });
            rootVe.Query(className: "hud-icon-btn").ForEach(el =>
            {
                el.style.width = 120f;
                el.style.height = 148f;
            });
        }

        static void SetBackground(VisualElement element, Texture2D texture)
        {
            if (element == null || texture == null)
                return;
            element.style.backgroundImage = new StyleBackground(texture);
        }

        void UnbindButtons()
        {
            if (!_bound)
                return;

            _btnMap?.UnregisterCallback<ClickEvent>(OnMapClicked);
            _btnAi?.UnregisterCallback<ClickEvent>(OnAiClicked);
            _btnPhone?.UnregisterCallback<ClickEvent>(OnPhoneClicked);
            _btnLog?.UnregisterCallback<ClickEvent>(OnLogClicked);
            _btnJournalClose?.UnregisterCallback<ClickEvent>(OnJournalCloseClicked);
            _btnJournalPrevious?.UnregisterCallback<ClickEvent>(OnJournalPreviousClicked);
            _btnJournalNext?.UnregisterCallback<ClickEvent>(OnJournalNextClicked);
            _btnSettings?.UnregisterCallback<ClickEvent>(OnSettingsClicked);
            _bound = false;
        }

        void EnsureGradients()
        {
            if (_cyanGradient != null)
                return;

            _cyanGradient = CreateVerticalGradient(
                new Color(0.83f, 0.91f, 1f, 0.95f),
                new Color(0.24f, 0.56f, 0.85f, 0.92f));
        }

        static Texture2D CreateVerticalGradient(Color top, Color bottom, int height = 64)
        {
            var tex = new Texture2D(1, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "HudGradient"
            };

            for (int y = 0; y < height; y++)
            {
                float u = height <= 1 ? 0f : y / (height - 1f);
                tex.SetPixel(0, y, Color.Lerp(bottom, top, u));
            }

            tex.Apply(false, true);
            return tex;
        }

        static void ApplyGradientToClass(VisualElement root, string className, Texture2D gradient)
        {
            if (root == null || gradient == null)
                return;

            root.Query(className: className).ForEach(el =>
            {
                el.style.backgroundImage = new StyleBackground(gradient);
                el.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
            });
        }

        void RefreshVisibility()
        {
            if (_root == null)
                Bind();
            if (_root == null)
                return;

            bool firstPerson = springArm != null && springArm.Mode == CameraViewMode.FirstPerson;
            if (firstPerson)
                _root.AddToClassList("is-hidden");
            else
                _root.RemoveFromClassList("is-hidden");
        }

        public void SetCoins(int amount)
        {
            _coins = Mathf.Max(0, amount);
            if (_coinAmount == null)
                Bind();
            if (_coinAmount != null)
                _coinAmount.text = _coins.ToString("N0");
        }

        public void AddCoins(int delta) => SetCoins(_coins + delta);

        public void SetMainQuest(string title)
        {
            _questTitleText = title ?? string.Empty;
            if (_questTitle == null || _questBar == null)
                Bind();

            if (_questTitle != null)
                _questTitle.text = _questTitleText;

            if (_questBar == null)
                return;

            if (string.IsNullOrWhiteSpace(_questTitleText))
            {
                _questBar.AddToClassList("is-hidden");
                _questBar.RemoveFromClassList("is-urgent");
            }
            else
                _questBar.RemoveFromClassList("is-hidden");
        }

        void PulseQuestBar()
        {
            if (_questBar == null ||
                string.IsNullOrWhiteSpace(_questTitleText) ||
                _questBar.ClassListContains("is-hidden"))
                return;

            _questBar.AddToClassList("is-urgent");
            _questBar.schedule.Execute(() => _questBar?.RemoveFromClassList("is-urgent")).StartingIn(190);
        }

        public void SetQuestDistance(string distanceText)
        {
            _questDistanceText = string.IsNullOrWhiteSpace(distanceText) ? "500m" : distanceText;
            if (_questDistance == null)
                Bind();
            if (_questDistance != null)
                _questDistance.text = _questDistanceText;
        }

        void OnMapClicked(ClickEvent _) => Debug.Log("[HUD] 地图");
        void OnAiClicked(ClickEvent _) => Debug.Log("[HUD] AI");
        void OnPhoneClicked(ClickEvent _) => Debug.Log("[HUD] 手机");
        void OnLogClicked(ClickEvent _)
        {
            if (_journal == null)
                return;

            _journalOpen = !_journalOpen;
            if (_journalOpen)
            {
                StopEvidenceIconBounce();
                RefreshJournalEntries();
                _journal.RemoveFromClassList("is-hidden");
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
                PlayJournalEntryArrival();
            }
            else
            {
                CloseJournal();
            }
        }

        void OnJournalCloseClicked(ClickEvent _)
        {
            CloseJournal();
        }

        void OnJournalPreviousClicked(ClickEvent _)
        {
            _journalPageIndex = Mathf.Max(0, _journalPageIndex - 1);
            RefreshJournalEntries();
            PlayJournalEntryArrival();
        }

        void OnJournalNextClicked(ClickEvent _)
        {
            int evidenceCount = AntiFraudEvidenceSystem.Instance?.CapturedEntries.Count ?? 0;
            int pageCount = Mathf.Max(1, Mathf.CeilToInt(evidenceCount / 2f));
            _journalPageIndex = Mathf.Min(pageCount - 1, _journalPageIndex + 1);
            RefreshJournalEntries();
            PlayJournalEntryArrival();
        }

        void CloseJournal()
        {
            _journalOpen = false;
            _journal?.AddToClassList("is-hidden");
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
        }

        /// <summary>
        /// Called once after the viewfinder has captured a flyer. The journal is
        /// intentionally data-driven so no record is visible before investigation.
        /// </summary>
        public void ShowEvidenceCaptured(EvidenceFlyer flyer)
        {
            if (flyer == null)
                return;

            var entries = AntiFraudEvidenceSystem.Instance?.CapturedEntries;
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i] != flyer)
                        continue;

                    _journalPageIndex = i / 2;
                    break;
                }
            }

            RefreshJournalEntries();
            StartEvidenceIconBounce();
        }

        void RefreshJournalEntries()
        {
            if (_journalLeftEntry == null)
                Bind();
            if (_journalLeftEntry == null)
                return;

            var entries = AntiFraudEvidenceSystem.Instance?.CapturedEntries;
            int evidenceCount = entries?.Count ?? 0;
            int pageCount = Mathf.Max(1, Mathf.CeilToInt(evidenceCount / 2f));
            _journalPageIndex = Mathf.Clamp(_journalPageIndex, 0, pageCount - 1);

            int firstEvidenceIndex = _journalPageIndex * 2;
            ApplyJournalEntry(
                _journalLeftEntry,
                _journalLeftEmpty,
                _journalLeftTitle,
                _journalLeftPhoto,
                _journalLeftSummary,
                entries,
                firstEvidenceIndex);
            ApplyJournalEntry(
                _journalRightEntry,
                _journalRightEmpty,
                _journalRightTitle,
                _journalRightPhoto,
                _journalRightSummary,
                entries,
                firstEvidenceIndex + 1);

            bool hasMultiplePages = pageCount > 1;
            if (_btnJournalPrevious != null)
            {
                _btnJournalPrevious.style.display = hasMultiplePages
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                _btnJournalPrevious.SetEnabled(_journalPageIndex > 0);
            }

            if (_btnJournalNext != null)
            {
                _btnJournalNext.style.display = hasMultiplePages
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                _btnJournalNext.SetEnabled(_journalPageIndex < pageCount - 1);
            }

            if (_journalPageIndicator != null)
            {
                _journalPageIndicator.style.display = hasMultiplePages
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                _journalPageIndicator.text = hasMultiplePages
                    ? $"{_journalPageIndex + 1} / {pageCount}"
                    : string.Empty;
            }
        }

        void ApplyJournalEntry(
            VisualElement entryElement,
            VisualElement emptyElement,
            Label title,
            VisualElement photo,
            Label summary,
            System.Collections.Generic.IReadOnlyList<EvidenceFlyer> entries,
            int evidenceIndex)
        {
            EvidenceFlyer entry = entries != null && evidenceIndex < entries.Count
                ? entries[evidenceIndex]
                : null;

            bool hasEntry = entry != null;
            entryElement?.EnableInClassList("is-hidden", !hasEntry);
            emptyElement?.EnableInClassList("is-hidden", hasEntry);

            if (!hasEntry)
                return;

            if (title != null)
                title.text = entry.Title;
            if (summary != null)
                summary.text = entry.Summary;
            if (photo == null)
                return;

            photo.style.backgroundImage = entry.Photo != null
                ? new StyleBackground(entry.Photo)
                : StyleKeyword.None;
        }

        void PlayJournalEntryArrival()
        {
            RevealJournalEntry(_journalLeftEntry, 0);
            RevealJournalEntry(_journalRightEntry, 410);
        }

        void RevealJournalEntry(VisualElement entry, int startDelay)
        {
            if (entry == null || entry.ClassListContains("is-hidden"))
                return;

            int revealIndex = 0;
            entry.Query<VisualElement>(className: "journal-reveal-item").ForEach(element =>
            {
                element.AddToClassList("is-reveal-waiting");

                int delay = startDelay + 60 + revealIndex * 120;
                revealIndex++;
                element.schedule
                    .Execute(() => element.RemoveFromClassList("is-reveal-waiting"))
                    .StartingIn(delay);
            });
        }

        void StartEvidenceIconBounce()
        {
            if (_btnLog == null)
                return;

            StopEvidenceIconBounce();
            _btnLog.AddToClassList("has-new-evidence");
            PlayEvidenceIconBounce();
            _journalUnreadAnimation = _btnLog.schedule
                .Execute(PlayEvidenceIconBounce)
                .Every(1000);
        }

        void PlayEvidenceIconBounce()
        {
            SetEvidenceIconBounce(true);
            _btnLog?.schedule.Execute(() => SetEvidenceIconBounce(false)).StartingIn(150);
            _btnLog?.schedule.Execute(() => SetEvidenceIconBounce(true)).StartingIn(260);
            _btnLog?.schedule.Execute(() => SetEvidenceIconBounce(false)).StartingIn(410);
        }

        void SetEvidenceIconBounce(bool active)
        {
            if (_btnLog == null ||
                !_btnLog.ClassListContains("has-new-evidence"))
                return;

            _btnLog.EnableInClassList("is-bouncing", active);
        }

        void StopEvidenceIconBounce()
        {
            _journalUnreadAnimation?.Pause();
            _journalUnreadAnimation = null;

            if (_btnLog == null)
                return;

            _btnLog.RemoveFromClassList("is-bouncing");
            _btnLog.RemoveFromClassList("has-new-evidence");
        }

        void OnSettingsClicked(ClickEvent _) => Debug.Log("[HUD] 设置");
    }
}
