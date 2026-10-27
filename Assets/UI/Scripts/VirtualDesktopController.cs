using System;
using System.Collections;
using System.Collections.Generic;
using AntiFraudStorm.Minigame;
using AntiFraudStorm.Portrait;
using AntiFraudStorm.Quest;
using AntiFraudStorm.SceneFlow;
using UnityEngine;
using UnityEngine.UIElements;

namespace AntiFraudStorm.UI
{
    /// <summary>
    /// Cute sky-blue virtual desktop + browser flow for 开学注册.
    /// Scene transitions are intentionally out of scope for now.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class VirtualDesktopController : MonoBehaviour
    {
        const float PhishingFlightDuration = 0.5f;
        static readonly float[] PhishingFlightDelays = { 0f, 0.26f, 0.52f, 0.78f };

        public const string OfficialUrl = "https://www.blueharbor.edu.cn";
        public const string ExpectedStudentId = "2026090101";
        public const string BrowserName = "反诈浏览器";

        static readonly string[] InitialUrlChoices =
        {
            "（请选择网址）",
            OfficialUrl,
        };

        static readonly string[] PhishingUrlChoices =
        {
            "https://www.blueharbor.edu.cn.login-secure.net",
            "https://www.b1ueharbor.edu.cn",
            "https://blueharbor-edu-cn.reg-help.com",
            "https://blueharbor-edu.cn.verify-login.top",
        };

        [SerializeField] UIDocument document;
        [SerializeField] bool completeQuestOnFinish = true;
        [SerializeField] float tuition = 6800f;
        [SerializeField] float cardBalance = 12800f;
        [SerializeField, Range(0f, 0.5f)] float wrongUrlCoinPenaltyRate = 0.15f;
        [SerializeField] GameObject enrollmentBackgroundPrefab;

        VisualElement _browser;
        VisualElement _pageBlank;
        VisualElement _pageSchool;
        VisualElement _pagePhish;
        VisualElement _pageEnrollIntro;
        VisualElement _pageEnroll;
        VisualElement _stepId;
        VisualElement _stepMail;
        VisualElement _stepPay;
        VisualElement _stepDone;
        Label _hint;
        Label _browserTitle;
        Label _phishMsg;
        Label _msgId;
        Label _msgMail;
        Label _msgPay;
        Label _mailAddress;
        Label _chipId;
        Label _chipMail;
        Label _chipPay;
        Label _chipDone;
        Label _clock;
        Label _walletAmount;
        Label _doneDetail;
        Label _cardBalanceLabel;
        Button _tabButton;
        Button _taskBrowser;
        Button _btnGo;
        Button _btnBack;
        Button _btnBrowserClose;
        Button _btnPhishBack;
        Button _btnIdNext;
        Button _btnSendCode;
        Button _btnMailBack;
        Button _btnMailNext;
        Button _btnPayBack;
        Button _btnPay;
        Button _btnFinish;
        Button _btnEnrollStart;
        Button _btnEnrollIntroBack;
        Button _appEnroll;
        Button _appCourse;
        Button _appLib;
        Button _iconBrowser;
        Button _iconFolder;
        Button _iconNotes;
        Button _iconMail;
        Button _btnStart;
        Button _btnExitDesk;
        Button _urlDropdownButton;
        Button _scholarshipNotification;
        Button _scholarshipClose;
        Button _scholarshipPhishingLink;
        VisualElement _urlDropdownPopup;
        VisualElement _scholarshipNotificationLayer;
        VisualElement _scholarshipMessageLayer;
        readonly Button[] _urlOptionButtons = new Button[6];
        readonly List<string> _urlChoices = new List<string>();
        readonly int[] _phishingInsertionOrder = { 0, 1, 2, 3 };
        TextField _fieldStudentId;
        TextField _fieldMailCode;
        VisualElement _desk;
        VisualElement _thiefPortrait;
        VisualElement _walletIcon;
        VisualElement _coinPenaltyLayer;
        readonly VisualElement[] _coinPenaltyIcons = new VisualElement[10];
        VisualElement _phishFlightLayer;
        readonly VisualElement[] _phishFlightGroups = new VisualElement[4];
        readonly bool[] _phishFlightArrived = new bool[4];

        string _studentId = "";
        string _expectedCode = "";
        bool _codeSent;
        bool _finished;
        bool _bound;
        int _wizardStep;
        float _balance;
        Vector2 _phishFlightStart;
        Vector2 _phishFlightEnd;
        float _phishFlightStartedAt;
        bool _phishFlightPlaying;
        bool _phishingSequenceStarted;
        bool _phishingInjected;
        bool _urlDropdownOpen;
        int _urlDropdownIndex;
        bool _screenShakeRunning;
        bool _enrollmentGameRunning;
        int _coins = 1280;
        Coroutine _coinPenaltyRoutine;
        Coroutine _scholarshipNotificationRoutine;

        void Awake()
        {
            if (document == null)
                document = GetComponent<UIDocument>();
            _balance = cardBalance;
        }

        void OnEnable()
        {
            if (document == null || document.rootVisualElement == null)
                return;

            Cache(document.rootVisualElement);
            Bind();
            ResetFlow();
            ShowHint("打开「反诈浏览器」，从下拉列表选择学校官网完成开学注册");
            RefreshClock();
        }

        void OnDisable()
        {
            StopAllCoroutines();
            _phishFlightPlaying = false;
            _screenShakeRunning = false;
            ResetScreenShake();
            HidePhishingFlight();
            HideCoinPenalty();
            _coinPenaltyRoutine = null;
            HideScholarshipNotification();
            HideScholarshipMessage();
            _scholarshipNotificationRoutine = null;
            Unbind();
        }

        int _lastClockMinute = -1;

        void Update()
        {
            UpdatePhishingFlight();

            if (_clock == null)
                return;
            int minute = DateTime.Now.Minute;
            if (minute == _lastClockMinute)
                return;
            _lastClockMinute = minute;
            RefreshClock();
        }

        void Cache(VisualElement root)
        {
            _desk = root.Q<VisualElement>("desk");
            _browser = root.Q("browser-window");
            _pageBlank = root.Q("page-blank");
            _pageSchool = root.Q("page-school");
            _pagePhish = root.Q("page-phish");
            _pageEnrollIntro = root.Q("page-enroll-intro");
            _pageEnroll = root.Q("page-enroll");
            _stepId = root.Q("step-id");
            _stepMail = root.Q("step-mail");
            _stepPay = root.Q("step-pay");
            _stepDone = root.Q("step-done");
            _hint = root.Q<Label>("desk-hint");
            _browserTitle = root.Q<Label>("browser-title");
            _phishMsg = root.Q<Label>("phish-msg");
            _msgId = root.Q<Label>("msg-id");
            _msgMail = root.Q<Label>("msg-mail");
            _msgPay = root.Q<Label>("msg-pay");
            _mailAddress = root.Q<Label>("mail-address");
            _chipId = root.Q<Label>("chip-id");
            _chipMail = root.Q<Label>("chip-mail");
            _chipPay = root.Q<Label>("chip-pay");
            _chipDone = root.Q<Label>("chip-done");
            _clock = root.Q<Label>("clock");
            _walletAmount = root.Q<Label>("desk-wallet-amount");
            _doneDetail = root.Q<Label>("done-detail");
            _cardBalanceLabel = root.Q<Label>("card-balance");
            _tabButton = root.Q<Button>("tab-main");
            _taskBrowser = root.Q<Button>("task-browser");
            _btnGo = root.Q<Button>("btn-go");
            _btnBack = root.Q<Button>("btn-back");
            _btnBrowserClose = root.Q<Button>("btn-browser-close");
            _btnPhishBack = root.Q<Button>("btn-phish-back");
            _btnIdNext = root.Q<Button>("btn-id-next");
            _btnSendCode = root.Q<Button>("btn-send-code");
            _btnMailBack = root.Q<Button>("btn-mail-back");
            _btnMailNext = root.Q<Button>("btn-mail-next");
            _btnPayBack = root.Q<Button>("btn-pay-back");
            _btnPay = root.Q<Button>("btn-pay");
            _btnFinish = root.Q<Button>("btn-finish");
            _btnEnrollStart = root.Q<Button>("btn-enroll-start");
            _btnEnrollIntroBack = root.Q<Button>("btn-enroll-intro-back");
            _appEnroll = root.Q<Button>("app-enroll");
            _appCourse = root.Q<Button>("app-course");
            _appLib = root.Q<Button>("app-lib");
            _iconBrowser = root.Q<Button>("icon-browser");
            _iconFolder = root.Q<Button>("icon-folder");
            _iconNotes = root.Q<Button>("icon-notes");
            _iconMail = root.Q<Button>("icon-mail");
            _btnStart = root.Q<Button>("btn-start");
            _btnExitDesk = root.Q<Button>("btn-exit-desk");
            _urlDropdownButton = root.Q<Button>("url-dropdown-button");
            _scholarshipNotification = root.Q<Button>("scholarship-notification");
            _scholarshipNotificationLayer = root.Q<VisualElement>("scholarship-notification-layer");
            _scholarshipMessageLayer = root.Q<VisualElement>("scholarship-message-layer");
            _scholarshipClose = root.Q<Button>("btn-scholarship-close");
            _scholarshipPhishingLink = root.Q<Button>("btn-scholarship-phishing-link");
            _urlDropdownPopup = root.Q<VisualElement>("url-dropdown-popup");
            for (int i = 0; i < _urlOptionButtons.Length; i++)
                _urlOptionButtons[i] = root.Q<Button>($"url-option-{i}");
            _fieldStudentId = root.Q<TextField>("field-student-id");
            _fieldMailCode = root.Q<TextField>("field-mail-code");
            _thiefPortrait = root.Q<VisualElement>("portrait-thief");
            _walletIcon = root.Q<VisualElement>("desk-wallet-icon");
            _coinPenaltyLayer = root.Q<VisualElement>("coin-penalty-layer");
            for (int i = 0; i < _coinPenaltyIcons.Length; i++)
                _coinPenaltyIcons[i] = root.Q<VisualElement>($"coin-flight-{i + 1}");
            _phishFlightLayer = root.Q<VisualElement>("phish-flight-layer");
            for (int i = 0; i < _phishFlightGroups.Length; i++)
                _phishFlightGroups[i] = root.Q<VisualElement>($"phish-flight-{i + 1}");
        }

        void Bind()
        {
            if (_bound)
                Unbind();

            _iconBrowser?.RegisterCallback<ClickEvent>(OnOpenBrowser);
            _taskBrowser?.RegisterCallback<ClickEvent>(OnOpenBrowser);
            _btnBrowserClose?.RegisterCallback<ClickEvent>(OnCloseBrowser);
            _btnGo?.RegisterCallback<ClickEvent>(OnGoClicked);
            _btnBack?.RegisterCallback<ClickEvent>(OnNavBack);
            _btnPhishBack?.RegisterCallback<ClickEvent>(OnNavBack);
            _appEnroll?.RegisterCallback<ClickEvent>(OnEnrollApp);
            _btnIdNext?.RegisterCallback<ClickEvent>(OnIdNext);
            _btnSendCode?.RegisterCallback<ClickEvent>(OnSendCode);
            _btnMailBack?.RegisterCallback<ClickEvent>(OnMailBack);
            _btnMailNext?.RegisterCallback<ClickEvent>(OnMailNext);
            _btnPayBack?.RegisterCallback<ClickEvent>(OnPayBack);
            _btnPay?.RegisterCallback<ClickEvent>(OnPay);
            _btnFinish?.RegisterCallback<ClickEvent>(OnFinish);
            _btnEnrollStart?.RegisterCallback<ClickEvent>(OnEnrollStart);
            _btnEnrollIntroBack?.RegisterCallback<ClickEvent>(OnEnrollIntroBack);
            _iconFolder?.RegisterCallback<ClickEvent>(OnFolder);
            _iconNotes?.RegisterCallback<ClickEvent>(OnNotes);
            _iconMail?.RegisterCallback<ClickEvent>(OnMailIcon);
            _appCourse?.RegisterCallback<ClickEvent>(OnCourse);
            _appLib?.RegisterCallback<ClickEvent>(OnLib);
            _btnStart?.RegisterCallback<ClickEvent>(OnStartMenu);
            _btnExitDesk?.RegisterCallback<ClickEvent>(OnExitDesk);
            _scholarshipNotification?.RegisterCallback<ClickEvent>(OnScholarshipNotificationClicked);
            _scholarshipClose?.RegisterCallback<ClickEvent>(OnScholarshipCloseClicked);
            _scholarshipPhishingLink?.RegisterCallback<ClickEvent>(OnScholarshipPhishingLinkClicked);

            _urlDropdownButton?.RegisterCallback<ClickEvent>(OnUrlFieldClicked);
            for (int i = 0; i < _urlOptionButtons.Length; i++)
            {
                _urlOptionButtons[i]?.RegisterCallback<ClickEvent>(OnUrlOptionClicked);
            }
            ResetUrlChoices();

            if (_walletAmount != null)
            {
                _coins = GameplayHudController.Instance != null
                    ? GameplayHudController.Instance.Coins
                    : 1280;
                _walletAmount.text = _coins.ToString("N0");
            }

            if (_cardBalanceLabel != null)
                _cardBalanceLabel.text = $"¥{_balance:0.00}";

            _bound = true;
        }

        void Unbind()
        {
            if (!_bound)
                return;

            _urlDropdownButton?.UnregisterCallback<ClickEvent>(OnUrlFieldClicked);
            for (int i = 0; i < _urlOptionButtons.Length; i++)
                _urlOptionButtons[i]?.UnregisterCallback<ClickEvent>(OnUrlOptionClicked);
            _iconBrowser?.UnregisterCallback<ClickEvent>(OnOpenBrowser);
            _taskBrowser?.UnregisterCallback<ClickEvent>(OnOpenBrowser);
            _btnBrowserClose?.UnregisterCallback<ClickEvent>(OnCloseBrowser);
            _btnGo?.UnregisterCallback<ClickEvent>(OnGoClicked);
            _btnBack?.UnregisterCallback<ClickEvent>(OnNavBack);
            _btnPhishBack?.UnregisterCallback<ClickEvent>(OnNavBack);
            _appEnroll?.UnregisterCallback<ClickEvent>(OnEnrollApp);
            _btnIdNext?.UnregisterCallback<ClickEvent>(OnIdNext);
            _btnSendCode?.UnregisterCallback<ClickEvent>(OnSendCode);
            _btnMailBack?.UnregisterCallback<ClickEvent>(OnMailBack);
            _btnMailNext?.UnregisterCallback<ClickEvent>(OnMailNext);
            _btnPayBack?.UnregisterCallback<ClickEvent>(OnPayBack);
            _btnPay?.UnregisterCallback<ClickEvent>(OnPay);
            _btnFinish?.UnregisterCallback<ClickEvent>(OnFinish);
            _btnEnrollStart?.UnregisterCallback<ClickEvent>(OnEnrollStart);
            _btnEnrollIntroBack?.UnregisterCallback<ClickEvent>(OnEnrollIntroBack);
            _iconFolder?.UnregisterCallback<ClickEvent>(OnFolder);
            _iconNotes?.UnregisterCallback<ClickEvent>(OnNotes);
            _iconMail?.UnregisterCallback<ClickEvent>(OnMailIcon);
            _appCourse?.UnregisterCallback<ClickEvent>(OnCourse);
            _appLib?.UnregisterCallback<ClickEvent>(OnLib);
            _btnStart?.UnregisterCallback<ClickEvent>(OnStartMenu);
            _btnExitDesk?.UnregisterCallback<ClickEvent>(OnExitDesk);
            _scholarshipNotification?.UnregisterCallback<ClickEvent>(OnScholarshipNotificationClicked);
            _scholarshipClose?.UnregisterCallback<ClickEvent>(OnScholarshipCloseClicked);
            _scholarshipPhishingLink?.UnregisterCallback<ClickEvent>(OnScholarshipPhishingLinkClicked);
            CloseUrlDropdown();
            _bound = false;
        }

        void OnOpenBrowser(ClickEvent _) { SetBrowserOpen(true); ShowHint("从地址栏下拉列表选择正确的学校官网，再点「前往」"); }
        void OnCloseBrowser(ClickEvent _) { SetBrowserOpen(false); ShowHint("浏览器已关闭。需要时再点桌面上的「反诈浏览器」"); }
        void OnGoClicked(ClickEvent _)
        {
            CloseUrlDropdown();
            NavigateFromDropdown();
        }
        void OnUrlFieldClicked(ClickEvent _)
        {
            if (!_phishingSequenceStarted)
            {
                _phishingSequenceStarted = true;
                DesktopCharacterPortraits.Instance?.RevealThief();
                StartCoroutine(PlayPhishingSequenceRoutine());
            }

            if (_urlDropdownOpen)
                CloseUrlDropdown();
            else
                OpenUrlDropdown();
        }

        void OnUrlOptionClicked(ClickEvent evt)
        {
            var button = evt.currentTarget as Button;
            int index = Array.IndexOf(_urlOptionButtons, button);
            if (index < 0 || index >= _urlChoices.Count)
                return;

            _urlDropdownIndex = index;
            RefreshUrlChoices();
            CloseUrlDropdown();
        }
        void OnNavBack(ClickEvent _)
        {
            CancelScholarshipNotification();
            ShowPage(_pageBlank, "新标签页");
        }
        void OnEnrollApp(ClickEvent _)
        {
            CancelScholarshipNotification();
            ShowPage(_pageEnrollIntro, "新生 3D 注册闯关");
            ShowHint("点击「开始」，完成 3D 闯关后继续注册");
        }
        void OnEnrollIntroBack(ClickEvent _)
        {
            ShowPage(_pageSchool, "蓝湾大学官网");
        }
        void OnEnrollStart(ClickEvent _)
        {
            if (_enrollmentGameRunning)
                return;
            StartCoroutine(StartEnrollmentGameRoutine());
        }
        void OnIdNext(ClickEvent _) => SubmitStudentId();
        void OnSendCode(ClickEvent _) => SendMailCode();
        void OnMailBack(ClickEvent _) => SetWizardStep(0);
        void OnMailNext(ClickEvent _) => SubmitMailCode();
        void OnPayBack(ClickEvent _) => SetWizardStep(1);
        void OnPay(ClickEvent _) => SubmitPayment();
        void OnFinish(ClickEvent _) => FinishToDesktop();
        void OnFolder(ClickEvent _) => ShowHint("这里只是装饰文件夹～注册请用浏览器打开官网");
        void OnNotes(ClickEvent _) => ShowHint("便签：学号写在录取通知书上，官网是 www.blueharbor.edu.cn");
        void OnMailIcon(ClickEvent _) => ShowHint("邮箱稍后再用；验证码会在注册流程里发送");
        void OnCourse(ClickEvent _) => ShowHint("选课将在注册完成后开放");
        void OnLib(ClickEvent _) => ShowHint("图书馆稍后开放");
        void OnStartMenu(ClickEvent _) => ShowHint("开始菜单还在装修中 ✦");
        void OnExitDesk(ClickEvent _)
        {
            if (CampusComputerSession.Instance != null)
                CampusComputerSession.Instance.ReturnToCampus();
            else
                ShowHint("当前是独立电脑场景，没有可返回的校园");
        }

        void ResetUrlChoices()
        {
            _urlChoices.Clear();
            _urlChoices.AddRange(InitialUrlChoices);
            _urlDropdownIndex = 0;
            CloseUrlDropdown();
            RefreshUrlChoices();
        }

        void RefreshUrlChoices()
        {
            int count = Mathf.Min(_urlChoices.Count, _urlOptionButtons.Length);
            for (int i = 0; i < _urlOptionButtons.Length; i++)
            {
                Button option = _urlOptionButtons[i];
                if (option == null)
                    continue;

                bool available = i < count;
                option.style.display = available ? DisplayStyle.Flex : DisplayStyle.None;
                if (!available)
                    continue;

                option.text = _urlChoices[i];
                option.EnableInClassList("is-selected", i == _urlDropdownIndex);
            }

            if (_urlDropdownButton != null && _urlChoices.Count > 0)
            {
                _urlDropdownIndex = Mathf.Clamp(_urlDropdownIndex, 0, _urlChoices.Count - 1);
                _urlDropdownButton.text = _urlChoices[_urlDropdownIndex];
            }
        }

        void OpenUrlDropdown()
        {
            if (_urlDropdownPopup == null || _urlDropdownButton == null)
                return;

            Rect fieldBounds = _urlDropdownButton.worldBound;
            Rect deskBounds = _desk.worldBound;
            _urlDropdownPopup.style.left = fieldBounds.x - deskBounds.x;
            _urlDropdownPopup.style.top = fieldBounds.yMax - deskBounds.y + 8f;
            _urlDropdownPopup.style.width = fieldBounds.width;
            _urlDropdownPopup.RemoveFromClassList("is-hidden");
            _urlDropdownOpen = true;
            RefreshUrlChoices();
        }

        void CloseUrlDropdown()
        {
            _urlDropdownPopup?.AddToClassList("is-hidden");
            _urlDropdownOpen = false;
        }

        IEnumerator PlayPhishingSequenceRoutine()
        {
            // Let the enemy portrait become visible and receive its first layout bounds.
            yield return null;
            yield return null;

            StartPhishingFlight();
        }

        void StartPhishingFlight()
        {
            if (_phishFlightLayer == null || _thiefPortrait == null || _urlDropdownButton == null)
                return;

            Rect layerBounds = _phishFlightLayer.worldBound;
            Vector2 enemyCenter = _thiefPortrait.worldBound.center;
            Vector2 addressCenter = _urlDropdownButton.worldBound.center;
            Vector2 enemyLocal = enemyCenter - layerBounds.position;
            Vector2 addressLocal = addressCenter - layerBounds.position;
            enemyLocal.x -= 110f;
            enemyLocal.y += 20f;

            _phishFlightStart = enemyLocal;
            _phishFlightEnd = addressLocal;
            _phishFlightStartedAt = Time.unscaledTime;
            _phishFlightPlaying = true;
            ShufflePhishingInsertionOrder();
            for (int i = 0; i < _phishFlightGroups.Length; i++)
            {
                _phishFlightArrived[i] = false;
                if (_phishFlightGroups[i] != null)
                {
                    _phishFlightGroups[i].style.display = DisplayStyle.None;
                    _phishFlightGroups[i].style.opacity = 0f;
                }
            }
        }

        void UpdatePhishingFlight()
        {
            if (!_phishFlightPlaying)
                return;

            float elapsed = Time.unscaledTime - _phishFlightStartedAt;
            float totalDuration = PhishingFlightDelays[PhishingFlightDelays.Length - 1] + PhishingFlightDuration;
            for (int i = 0; i < _phishFlightGroups.Length; i++)
            {
                VisualElement projectile = _phishFlightGroups[i];
                if (projectile == null)
                    continue;

                float local = (elapsed - PhishingFlightDelays[i]) / PhishingFlightDuration;
                if (local < 0f)
                {
                    projectile.style.display = DisplayStyle.None;
                    continue;
                }

                float eased = local * local * (3f - 2f * local);
                Vector2 position = Vector2.LerpUnclamped(_phishFlightStart, _phishFlightEnd, eased);
                position.y -= Mathf.Sin(Mathf.Clamp01(eased) * Mathf.PI) * 120f;

                float opacity = Mathf.Clamp01(local / 0.1f)
                    * (1f - Mathf.Clamp01((local - 0.76f) / 0.24f));
                projectile.style.display = DisplayStyle.Flex;
                projectile.style.left = position.x - 75f;
                projectile.style.top = position.y - 75f;
                projectile.style.opacity = opacity;

                if (local >= 1f && !_phishFlightArrived[i])
                {
                    _phishFlightArrived[i] = true;
                    projectile.style.display = DisplayStyle.None;
                    OnPhishingProjectileArrived(i);
                }
            }

            if (elapsed < totalDuration)
                return;

            _phishFlightPlaying = false;
            HidePhishingFlight();
            StartCoroutine(ShuffleInjectedUrlsRoutine());
        }

        void OnPhishingProjectileArrived(int index)
        {
            if (index < 0 || index >= _phishingInsertionOrder.Length)
                return;

            string url = PhishingUrlChoices[_phishingInsertionOrder[index]];
            if (!_urlChoices.Contains(url))
                _urlChoices.Add(url);

            _urlDropdownIndex = 0;
            RefreshUrlChoices();
            PulseUrlOption(_urlChoices.IndexOf(url));
            ShowHint($"可疑网址已注入：{url}");

            if (!_screenShakeRunning)
                StartCoroutine(ShakeScreenRoutine());
        }

        void ShufflePhishingInsertionOrder()
        {
            for (int i = _phishingInsertionOrder.Length - 1; i > 0; i--)
            {
                int swapIndex = UnityEngine.Random.Range(0, i + 1);
                int value = _phishingInsertionOrder[i];
                _phishingInsertionOrder[i] = _phishingInsertionOrder[swapIndex];
                _phishingInsertionOrder[swapIndex] = value;
            }
        }

        IEnumerator ShuffleInjectedUrlsRoutine()
        {
            const int choiceStartIndex = 1;
            yield return new WaitForSecondsRealtime(0.12f);

            for (int i = choiceStartIndex; i < _urlOptionButtons.Length; i++)
            {
                Button option = _urlOptionButtons[i];
                if (option == null || i >= _urlChoices.Count)
                    continue;
                option.AddToClassList(i % 2 == 0 ? "is-shuffling-left" : "is-shuffling-right");
            }

            yield return new WaitForSecondsRealtime(0.16f);
            for (int i = _urlChoices.Count - 1; i > choiceStartIndex; i--)
            {
                int swapIndex = UnityEngine.Random.Range(choiceStartIndex, i + 1);
                string value = _urlChoices[i];
                _urlChoices[i] = _urlChoices[swapIndex];
                _urlChoices[swapIndex] = value;
            }

            int officialIndex = _urlChoices.IndexOf(OfficialUrl);
            if (officialIndex == choiceStartIndex && _urlChoices.Count > choiceStartIndex + 1)
            {
                int swapIndex = UnityEngine.Random.Range(choiceStartIndex + 1, _urlChoices.Count);
                string value = _urlChoices[officialIndex];
                _urlChoices[officialIndex] = _urlChoices[swapIndex];
                _urlChoices[swapIndex] = value;
            }

            _urlDropdownIndex = 0;
            RefreshUrlChoices();

            for (int i = choiceStartIndex; i < _urlOptionButtons.Length; i++)
            {
                Button option = _urlOptionButtons[i];
                if (option == null || i >= _urlChoices.Count)
                    continue;
                option.RemoveFromClassList("is-shuffling-left");
                option.RemoveFromClassList("is-shuffling-right");
                int optionIndex = i;
                option.schedule.Execute(() => PulseUrlOption(optionIndex)).StartingIn(35L * (i - choiceStartIndex));
            }

            _phishingInjected = true;
            ShowHint("全部网址顺序已打乱，请仔细核对学校官方域名");
        }

        void PulseUrlOption(int index)
        {
            if (index < 0 || index >= _urlOptionButtons.Length)
                return;

            Button option = _urlOptionButtons[index];
            if (option == null)
                return;

            option.AddToClassList("is-pulsing");
            option.schedule.Execute(() => option.RemoveFromClassList("is-pulsing")).StartingIn(190);
        }

        IEnumerator ShakeScreenRoutine()
        {
            if (_desk == null)
                yield break;

            _screenShakeRunning = true;
            Vector2[] offsets =
            {
                new Vector2(-3f, 1f),
                new Vector2(3f, -1f),
                new Vector2(-2f, -1f),
                new Vector2(2f, 1f),
                Vector2.zero,
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                _desk.style.left = offsets[i].x;
                _desk.style.top = offsets[i].y;
                yield return new WaitForSecondsRealtime(0.035f);
            }

            ResetScreenShake();
            _screenShakeRunning = false;
        }

        void ResetScreenShake()
        {
            if (_desk == null)
                return;
            _desk.style.left = 0f;
            _desk.style.top = 0f;
        }

        void HidePhishingFlight()
        {
            for (int i = 0; i < _phishFlightGroups.Length; i++)
            {
                VisualElement projectile = _phishFlightGroups[i];
                if (projectile == null)
                    continue;
                projectile.style.display = DisplayStyle.None;
                projectile.style.opacity = 0f;
            }
        }

        void ResetFlow()
        {
            StopAllCoroutines();
            _studentId = "";
            _expectedCode = "";
            _codeSent = false;
            _finished = false;
            _wizardStep = 0;
            _balance = cardBalance;
            _phishingSequenceStarted = false;
            _phishingInjected = false;
            _phishFlightPlaying = false;
            _enrollmentGameRunning = false;
            _screenShakeRunning = false;
            _coinPenaltyRoutine = null;
            _scholarshipNotificationRoutine = null;
            for (int i = 0; i < _phishFlightArrived.Length; i++)
                _phishFlightArrived[i] = false;
            ResetScreenShake();
            HideCoinPenalty();
            HideScholarshipNotification();
            HideScholarshipMessage();
            DesktopCharacterPortraits.Instance?.ResetPlatformerWipe();
            if (_desk != null)
                _desk.style.display = DisplayStyle.Flex;
            SetBrowserOpen(false);
            ShowPage(_pageBlank, "新标签页");
            SetWizardStep(0);
            if (_fieldStudentId != null) _fieldStudentId.value = "";
            if (_fieldMailCode != null) _fieldMailCode.value = "";
            HidePhishingFlight();
            ResetUrlChoices();
            if (_cardBalanceLabel != null) _cardBalanceLabel.text = $"¥{_balance:0.00}";
        }

        void SetBrowserOpen(bool open)
        {
            SetHidden(_browser, !open);
            SetHidden(_taskBrowser, !open);
            if (!open)
            {
                CancelScholarshipNotification();
                CloseUrlDropdown();
            }
        }

        void NavigateFromDropdown()
        {
            if (_urlChoices.Count == 0)
                return;

            _urlDropdownIndex = Mathf.Clamp(_urlDropdownIndex, 0, _urlChoices.Count - 1);
            string url = _urlChoices[_urlDropdownIndex];
            if (string.IsNullOrEmpty(url) || url.StartsWith("（", StringComparison.Ordinal))
            {
                ShowHint("请先在下拉列表里选择一个网址");
                return;
            }

            if (string.Equals(url, OfficialUrl, StringComparison.OrdinalIgnoreCase))
            {
                DesktopCharacterPortraits.Instance?.PlayCorrectAnswerFeedback();
                ShowPage(_pageSchool, "蓝湾大学官网");
                ShowHint("已进入学校官网，点击「开学注册」应用继续");
                StartScholarshipNotificationCountdown();
                return;
            }

            string reason = url.Contains("login-secure")
                ? "域名后缀被拼接到第三方站点上，这是常见仿冒手法。"
                : url.Contains("b1ue")
                    ? "字母「l」被替换成数字「1」，属于视觉仿冒域名。"
                    : "看起来像帮助站点，但并不是学校官方域名。";

            ShowPhishingFailure(
                $"你打开了：{url}",
                reason,
                "这不是官网！请换回官方地址再试");
        }

        void ShowPhishingFailure(string source, string reason, string hint)
        {
            CancelScholarshipNotification();
            if (_phishMsg != null)
                _phishMsg.text = $"{source}\n{reason}\n请立即停止操作，并返回选择 {OfficialUrl}";

            DesktopCharacterPortraits.Instance?.PlayWrongAnswerFeedback();
            ApplyWrongUrlCoinPenalty();
            ShowPage(_pagePhish, "高危安全提示");
            ShowHint(hint);
        }

        void OnScholarshipNotificationClicked(ClickEvent _)
        {
            CancelScholarshipNotification();
            if (_scholarshipMessageLayer != null)
                _scholarshipMessageLayer.RemoveFromClassList("is-hidden");
        }

        void OnScholarshipCloseClicked(ClickEvent _)
        {
            HideScholarshipMessage();
        }

        void OnScholarshipPhishingLinkClicked(ClickEvent _)
        {
            ShowPhishingFailure(
                "你点击了伪造助学金邮件中的链接",
                "发件人伪装成学校资助中心，链接域名并不属于学校官方域名。",
                "这是助学金诈骗！请立即关闭页面并核对发件人域名");
        }

        void StartScholarshipNotificationCountdown()
        {
            CancelScholarshipNotification();
            _scholarshipNotificationRoutine = StartCoroutine(ScholarshipNotificationRoutine());
        }

        IEnumerator ScholarshipNotificationRoutine()
        {
            yield return new WaitForSecondsRealtime(2f);
            if (_scholarshipNotificationLayer == null ||
                _pageSchool == null || _pageSchool.ClassListContains("is-hidden"))
            {
                _scholarshipNotificationRoutine = null;
                yield break;
            }

            DesktopCharacterPortraits.Instance?.PlayThiefScholarshipTaunt();
            _scholarshipNotificationLayer.RemoveFromClassList("is-hidden");
            const float slideDuration = 0.34f;
            const float hiddenTop = -180f;
            const float visibleTop = 18f;
            float elapsed = 0f;
            while (elapsed < slideDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / slideDuration);
                t = t * t * (3f - 2f * t);
                _scholarshipNotificationLayer.style.top = Mathf.Lerp(hiddenTop, visibleTop, t);
                yield return null;
            }

            _scholarshipNotificationLayer.style.top = visibleTop;
            _scholarshipNotificationRoutine = null;
        }

        void CancelScholarshipNotification()
        {
            if (_scholarshipNotificationRoutine != null)
            {
                StopCoroutine(_scholarshipNotificationRoutine);
                _scholarshipNotificationRoutine = null;
            }
            HideScholarshipNotification();
            HideScholarshipMessage();
        }

        void HideScholarshipNotification()
        {
            if (_scholarshipNotificationLayer == null)
                return;
            _scholarshipNotificationLayer.AddToClassList("is-hidden");
            _scholarshipNotificationLayer.style.top = -180f;
        }

        void HideScholarshipMessage()
        {
            _scholarshipMessageLayer?.AddToClassList("is-hidden");
        }

        void ApplyWrongUrlCoinPenalty()
        {
            if (_coins <= 0)
                return;

            int penalty = Mathf.Clamp(
                Mathf.RoundToInt(_coins * Mathf.Max(0f, wrongUrlCoinPenaltyRate)),
                1,
                _coins);
            _coins = Mathf.Max(0, _coins - penalty);

            GameplayHudController.Instance?.SetCoins(_coins);
            if (_walletAmount != null)
                _walletAmount.text = _coins.ToString("N0");

            if (_coinPenaltyRoutine != null)
                StopCoroutine(_coinPenaltyRoutine);
            _coinPenaltyRoutine = StartCoroutine(CoinPenaltyRoutine());
        }

        IEnumerator CoinPenaltyRoutine()
        {
            if (_coinPenaltyLayer == null || _walletAmount == null)
                yield break;

            Rect layerBounds = _coinPenaltyLayer.worldBound;
            Vector2 start = _walletIcon != null
                ? _walletIcon.worldBound.center - layerBounds.position
                : new Vector2(80f, layerBounds.height - 60f);
            Vector2 end = _thiefPortrait != null
                ? _thiefPortrait.worldBound.center - layerBounds.position - new Vector2(110f, -20f)
                : new Vector2(layerBounds.width - 220f, layerBounds.height * 0.58f);

            _walletAmount.AddToClassList("is-losing");
            for (int i = 0; i < _coinPenaltyIcons.Length; i++)
            {
                if (_coinPenaltyIcons[i] != null)
                    _coinPenaltyIcons[i].style.display = DisplayStyle.None;
            }

            const float coinTravelDuration = 1f;
            const float stagger = 0.09f;
            float totalDuration = (_coinPenaltyIcons.Length - 1) * stagger + coinTravelDuration;
            float startedAt = Time.unscaledTime;
            float elapsed = 0f;

            while (elapsed < totalDuration)
            {
                elapsed = Time.unscaledTime - startedAt;
                for (int i = 0; i < _coinPenaltyIcons.Length; i++)
                {
                    VisualElement coin = _coinPenaltyIcons[i];
                    if (coin == null)
                        continue;

                    float local = (elapsed - i * stagger) / coinTravelDuration;
                    if (local < 0f || local >= 1f)
                    {
                        coin.style.display = DisplayStyle.None;
                        continue;
                    }

                    float eased = local * local * (3f - 2f * local);
                    Vector2 control = new Vector2(
                        layerBounds.width * (0.48f + ((i % 3) - 1) * 0.055f),
                        layerBounds.height * (0.27f + (i % 3) * 0.035f));
                    float inverse = 1f - eased;
                    Vector2 position =
                        inverse * inverse * start +
                        2f * inverse * eased * control +
                        eased * eased * end;
                    float size = Mathf.Lerp(124f, 72f, eased);
                    float opacity = Mathf.Clamp01(local / 0.08f)
                        * (1f - Mathf.Clamp01((local - 0.78f) / 0.22f));

                    coin.style.display = DisplayStyle.Flex;
                    coin.style.left = position.x - size * 0.5f;
                    coin.style.top = position.y - size * 0.5f;
                    coin.style.width = size;
                    coin.style.height = size;
                    coin.style.opacity = opacity;
                }

                yield return null;
            }

            _walletAmount.RemoveFromClassList("is-losing");
            HideCoinPenalty();
            _coinPenaltyRoutine = null;
        }

        void HideCoinPenalty()
        {
            for (int i = 0; i < _coinPenaltyIcons.Length; i++)
            {
                VisualElement coin = _coinPenaltyIcons[i];
                if (coin == null)
                    continue;
                coin.style.display = DisplayStyle.None;
                coin.style.opacity = 0f;
            }
            _walletAmount?.RemoveFromClassList("is-losing");
        }

        void OpenEnrollWizard()
        {
            ShowPage(_pageEnroll, "开学注册");
            SetWizardStep(0);
            ShowHint("输入录取通知书上的 10 位学号（可用 2026090101）");
        }

        IEnumerator StartEnrollmentGameRoutine()
        {
            _enrollmentGameRunning = true;
            _btnEnrollStart?.SetEnabled(false);
            CancelScholarshipNotification();
            SetBrowserOpen(false);

            DesktopCharacterPortraits.Instance?.BeginPlatformerWipe(0.9f);
            yield return new WaitForSecondsRealtime(0.92f);
            _desk.style.display = DisplayStyle.None;

            var gameGo = new GameObject("Enrollment3DChallenge");
            gameGo.transform.SetParent(transform, false);
            var game = gameGo.AddComponent<Enrollment3DChallengeController>();
            DesktopCharacterPortraits portraits = DesktopCharacterPortraits.Instance;
            game.Initialize(
                portraits != null ? portraits.HeroPrefab : null,
                portraits != null ? portraits.EnemyPrefab : null,
                enrollmentBackgroundPrefab,
                OnEnrollmentGameFinished);
        }

        void OnEnrollmentGameFinished()
        {
            DesktopCharacterPortraits.Instance?.ResetPlatformerWipe();
            _desk.style.display = DisplayStyle.Flex;
            _btnEnrollStart?.SetEnabled(true);
            _enrollmentGameRunning = false;
            SetBrowserOpen(true);
            ShowRegistrationSuccess();
        }

        void ShowRegistrationSuccess()
        {
            ShowPage(_pageEnroll, "注册成功");
            SetWizardStep(3);
            if (_doneDetail != null)
                _doneDetail.text = "3D 注册闯关已完成。\n欢迎成为蓝湾大学的一员！";
            ShowHint("注册成功！可点击「返回校园」结束流程");
            _finished = true;
            if (completeQuestOnFinish)
                QuestService.Instance?.CompleteActiveQuest();
        }

        void SubmitStudentId()
        {
            string id = (_fieldStudentId != null ? _fieldStudentId.value : "").Trim();
            if (id.Length != 10 || !IsAllDigits(id))
            {
                SetMsg(_msgId, "学号应为 10 位数字", error: true);
                return;
            }

            SetMsg(_msgId, id == ExpectedStudentId ? "学号验证通过" : "学号格式正确，继续～", error: false);
            _studentId = id;
            if (_mailAddress != null)
                _mailAddress.text = $"邮箱：{id}@stu.blueharbor.edu.cn";
            SetWizardStep(1);
            ShowHint("点击发送验证码，再填写邮箱里的 4 位数字");
        }

        void SendMailCode()
        {
            if (string.IsNullOrEmpty(_studentId))
            {
                SetMsg(_msgMail, "请先完成学号填写", error: true);
                return;
            }

            _expectedCode = UnityEngine.Random.Range(1000, 9999).ToString();
            _codeSent = true;
            SetMsg(_msgMail, $"验证码已发送（模拟收件：{_expectedCode}）", error: false);
            ShowHint($"邮箱验证码（演示）：{_expectedCode}");
        }

        void SubmitMailCode()
        {
            if (!_codeSent)
            {
                SetMsg(_msgMail, "请先点击「发送验证码」", error: true);
                return;
            }

            string code = (_fieldMailCode != null ? _fieldMailCode.value : "").Trim();
            if (code != _expectedCode)
            {
                SetMsg(_msgMail, "验证码不正确，请核对后重试", error: true);
                return;
            }

            SetMsg(_msgMail, "邮箱验证成功", error: false);
            SetWizardStep(2);
            ShowHint("确认学费金额后，使用校园卡余额缴费");
        }

        void SubmitPayment()
        {
            if (_balance < tuition)
            {
                SetMsg(_msgPay, "校园卡余额不足", error: true);
                return;
            }

            _balance -= tuition;
            if (_cardBalanceLabel != null)
                _cardBalanceLabel.text = $"¥{_balance:0.00}";

            if (_doneDetail != null)
                _doneDetail.text = $"学号 {_studentId} 已完成缴费与注册。\n欢迎成为蓝湾大学的一员！";

            SetWizardStep(3);
            ShowHint("注册完成！可点「返回校园」关闭窗口");
            _finished = true;

            if (completeQuestOnFinish)
                QuestService.Instance?.CompleteActiveQuest();
        }

        void FinishToDesktop()
        {
            if (!_finished && completeQuestOnFinish)
                QuestService.Instance?.CompleteActiveQuest();

            if (CampusComputerSession.Instance != null)
            {
                CampusComputerSession.Instance.ReturnToCampus();
                return;
            }

            SetBrowserOpen(false);
            ShowHint("开学注册已完成 ✦ 当前为独立桌面场景");
        }

        void SetWizardStep(int step)
        {
            _wizardStep = Mathf.Clamp(step, 0, 3);
            SetHidden(_stepId, _wizardStep != 0);
            SetHidden(_stepMail, _wizardStep != 1);
            SetHidden(_stepPay, _wizardStep != 2);
            SetHidden(_stepDone, _wizardStep != 3);
            SetChip(_chipId, 0);
            SetChip(_chipMail, 1);
            SetChip(_chipPay, 2);
            SetChip(_chipDone, 3);
        }

        void SetChip(Label chip, int index)
        {
            if (chip == null) return;
            chip.RemoveFromClassList("is-active");
            chip.RemoveFromClassList("is-done");
            if (index < _wizardStep) chip.AddToClassList("is-done");
            else if (index == _wizardStep) chip.AddToClassList("is-active");
        }

        void ShowPage(VisualElement page, string tabTitle)
        {
            SetHidden(_pageBlank, page != _pageBlank);
            SetHidden(_pageSchool, page != _pageSchool);
            SetHidden(_pagePhish, page != _pagePhish);
            SetHidden(_pageEnrollIntro, page != _pageEnrollIntro);
            SetHidden(_pageEnroll, page != _pageEnroll);
            if (_tabButton != null)
                _tabButton.text = tabTitle;
            if (_browserTitle != null)
                _browserTitle.text = BrowserName + " — " + tabTitle;
        }

        void ShowHint(string text)
        {
            if (_hint == null) return;
            _hint.text = text;
            _hint.RemoveFromClassList("is-hidden");
        }

        void SetMsg(Label label, string text, bool error)
        {
            if (label == null) return;
            label.text = text;
            label.EnableInClassList("is-error", error);
            label.EnableInClassList("is-ok", !error && !string.IsNullOrEmpty(text));
        }

        static void SetHidden(VisualElement el, bool hidden)
        {
            if (el == null) return;
            el.EnableInClassList("is-hidden", hidden);
        }

        void RefreshClock()
        {
            if (_clock != null)
                _clock.text = DateTime.Now.ToString("HH:mm");
        }

        static bool IsAllDigits(string s)
        {
            for (int i = 0; i < s.Length; i++)
                if (s[i] < '0' || s[i] > '9')
                    return false;
            return true;
        }
    }
}
