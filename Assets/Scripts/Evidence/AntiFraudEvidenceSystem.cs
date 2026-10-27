using System;
using System.Collections.Generic;
using AntiFraudStorm.Player;
using AntiFraudStorm.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AntiFraudStorm.Evidence
{
    public class AntiFraudEvidenceSystem : MonoBehaviour
    {
        const string FlyerPrefix = "scam_flyer_v2_";
        const float MaxCaptureDistance = 6f;

        public static AntiFraudEvidenceSystem Instance { get; private set; }
        public static event Action<EvidenceFlyer> EvidenceCaptured;

        readonly List<EvidenceFlyer> _captured = new List<EvidenceFlyer>();

        public IReadOnlyList<EvidenceFlyer> CapturedEntries => _captured;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureForScene(SceneManager.GetActiveScene());
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureForScene(scene);
        }

        static void EnsureForScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || scene.name != "School")
                return;
            if (FindObjectOfType<AntiFraudEvidenceSystem>() != null)
                return;

            var go = new GameObject("AntiFraudEvidenceSystem");
            go.AddComponent<AntiFraudEvidenceSystem>();
        }

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Start()
        {
            RegisterFlyers();
        }

        void Update()
        {
            var handheld = FindObjectOfType<PlayerHandheldCamera>();
            Camera camera = Camera.main;
            if (handheld == null || !handheld.IsHolding || handheld.IsBusy ||
                camera == null || Cursor.lockState != CursorLockMode.Locked ||
                !Input.GetMouseButtonDown(0))
                return;

            Ray ray = camera.ScreenPointToRay(
                new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
            if (!Physics.Raycast(ray, out RaycastHit hit, MaxCaptureDistance, ~0, QueryTriggerInteraction.Collide))
                return;

            EvidenceFlyer flyer = hit.collider.GetComponentInParent<EvidenceFlyer>();
            if (flyer != null && !flyer.Captured)
                Capture(flyer);
        }

        void RegisterFlyers()
        {
            var renderers = FindObjectsOfType<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer spriteRenderer = renderers[i];
                if (spriteRenderer == null ||
                    !spriteRenderer.gameObject.name.StartsWith(FlyerPrefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                GameObject flyerGo = spriteRenderer.gameObject;
                if (flyerGo.GetComponent<EvidenceFlyer>() != null)
                    continue;

                var collider = flyerGo.GetComponent<BoxCollider>();
                if (collider == null)
                    collider = flyerGo.AddComponent<BoxCollider>();

                Vector2 spriteSize = spriteRenderer.sprite != null
                    ? spriteRenderer.sprite.bounds.size
                    : new Vector2(1f, 1.4f);
                collider.size = new Vector3(spriteSize.x, spriteSize.y, 0.12f);
                collider.isTrigger = true;

                int order = ParseOrder(flyerGo.name);
                var evidence = flyerGo.AddComponent<EvidenceFlyer>();
                evidence.Initialize(order, spriteRenderer.sprite != null ? spriteRenderer.sprite.texture : null);
            }
        }

        void Capture(EvidenceFlyer flyer)
        {
            flyer.MarkCaptured();
            _captured.Add(flyer);
            _captured.Sort((a, b) => a.Order.CompareTo(b.Order));

            CameraViewfinderController.Instance?.PlayCaptureFlash();
            GameplayHudController.Instance?.ShowEvidenceCaptured(flyer);
            EvidenceCaptured?.Invoke(flyer);
        }

        static int ParseOrder(string objectName)
        {
            int marker = objectName.IndexOf("_v2_", StringComparison.OrdinalIgnoreCase);
            if (marker >= 0 && marker + 7 < objectName.Length)
            {
                if (int.TryParse(objectName.Substring(marker + 4, 2), out int order))
                    return order;
            }
            return 99;
        }
    }

    public class EvidenceFlyer : MonoBehaviour
    {
        public int Order { get; private set; }
        public Texture2D Photo { get; private set; }
        public string Title { get; private set; }
        public string Summary { get; private set; }
        public bool Captured { get; private set; }

        public void Initialize(int order, Texture2D photo)
        {
            Order = order;
            Photo = photo;

            switch (order)
            {
                case 1:
                    Title = "高收益兼职骗局";
                    Summary = "小额返利只是诱饵，后续垫付资金通常无法提现。";
                    break;
                case 2:
                    Title = "保本高息投资骗局";
                    Summary = "“稳赚不赔”违背投资常识，平台数据可被后台操控。";
                    break;
                case 3:
                    Title = "内部带单骗局";
                    Summary = "“内部带单”无法验证，所谓盈利只是诱骗充值的假象。";
                    break;
                case 4:
                    Title = "层层收费骗局";
                    Summary = "正规贷款不会在放款前收取押金、解冻费或保证金。";
                    break;
                case 5:
                    Title = "非法贷款骗局";
                    Summary = "无门槛贷款常借各种费用骗钱，并套取身份证和银行卡信息。";
                    break;
                default:
                    Title = "疑似诈骗传单";
                    Summary = "记录陌生传单中的可疑承诺，并继续核实来源。";
                    break;
            }
        }

        public void MarkCaptured()
        {
            Captured = true;
        }
    }
}
