using System.Collections;
using System.Collections.Generic;
using Animancer;
using AntiFraudStorm.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace AntiFraudStorm.Minigame
{
    /// <summary>
    /// Three-lane anti-fraud defense minigame for Anti-Scam Cortex.
    /// </summary>
    public class AntiFraudDefenseGame : MonoBehaviour
    {
        const float ParticleEffectScale = 0.1f;

        static readonly string[] AmmoNames =
        {
            "一证通查",
            "一键查卡",
            "803智能体",
            "境外来电提醒",
            "反诈名片",
            "12381预警",
            "96110专线",
            "国家反诈中心",
            "跨境业务按需",
            "校园反诈中心",
        };

        static readonly int[] AmmoGroups = { 0, 0, 1, 2, 3, 2, 2, 3, 4, 1 };

        static readonly KeyCode[] NumberRowKeys =
        {
            KeyCode.Alpha1,
            KeyCode.Alpha2,
            KeyCode.Alpha3,
            KeyCode.Alpha4,
            KeyCode.Alpha5,
            KeyCode.Alpha6,
            KeyCode.Alpha7,
            KeyCode.Alpha8,
            KeyCode.Alpha9,
            KeyCode.Alpha0,
        };

        static readonly KeyCode[] KeypadKeys =
        {
            KeyCode.Keypad1,
            KeyCode.Keypad2,
            KeyCode.Keypad3,
            KeyCode.Keypad4,
            KeyCode.Keypad5,
            KeyCode.Keypad6,
            KeyCode.Keypad7,
            KeyCode.Keypad8,
            KeyCode.Keypad9,
            KeyCode.Keypad0,
        };

        static readonly Color HudPanelColor =
            new Color(0.94f, 0.975f, 1f, 0.52f);
        static readonly Color HudInkColor =
            new Color(0.055f, 0.18f, 0.3f, 1f);
        static readonly Color HudMutedColor =
            new Color(0.3f, 0.45f, 0.56f, 1f);
        static readonly Color HudAccentColor =
            new Color(0.08f, 0.43f, 0.62f, 1f);
        static readonly Color HudSkyBlueColor =
            new Color(0.12f, 0.66f, 0.92f, 1f);
        static readonly Color AmmoIdleColor =
            new Color(0.78f, 0.89f, 0.98f, 0.54f);
        static readonly Color AmmoSelectedColor =
            new Color(1f, 0.8f, 0.34f, 0.64f);
        static readonly Color AmmoCoolingColor =
            new Color(0.86f, 0.88f, 0.91f, 0.48f);

        static readonly string[] ScamNames =
        {
            "刷单返利",
            "虚假投资",
            "冒充公检法",
            "冒充客服退款",
            "网络贷款",
            "买卖两卡",
            "境外来电诈骗",
            "游戏交易诈骗",
            "跨境赌博",
            "校园贷/熟人诈骗",
        };

        static readonly int[] ScamMatchingAmmo = { 5, 2, 4, 6, 1, 0, 3, 7, 8, 9 };

        [Header("Scene References")]
        [SerializeField] GameObject playerPrefab;
        [SerializeField] GameObject fraudster;
        [SerializeField] GameObject[] enemyTemplates = new GameObject[3];
        [SerializeField] GameObject turretStatuePrefab;

        [Header("Animancer Clips")]
        [SerializeField] AnimationClip playerIdleClip;
        [SerializeField] AnimationClip playerAttackClip;
        [SerializeField] AnimationClip[] fraudsterDanceClips = new AnimationClip[5];
        [SerializeField] AnimationClip fraudsterSummonClip;

        [Header("Enemy Summon")]
        [SerializeField] GameObject enemySummonEffectPrefab;
        [SerializeField, Min(0.01f)] float enemySummonEffectScale = 1.5f;
        [SerializeField] string enemySpawnState = "Spawn";
        [SerializeField, Min(0.1f)] float enemySpawnFallbackDuration = 1.2f;
        [SerializeField, Min(0f)] float enemySpawnForwardOffset = 3.5f;

        [Header("Projectile and Hit FX")]
        [SerializeField] GameObject[] ammoProjectilePrefabs = new GameObject[10];
        [SerializeField] GameObject enemyHitEffectPrefab;
        [SerializeField] GameObject rangedEnemyProjectilePrefab;
        [SerializeField, Min(0.01f)] float turretProjectileEffectScale = 0.45f;

        [Header("Character FX")]
        [SerializeField] GameObject enemySpawnEffectPrefab;
        [SerializeField] GameObject turretLandingEffectPrefab;
        [SerializeField, Min(0.01f)] float turretLandingEffectScale = 4.5f;
        [SerializeField] GameObject playerHitEffectPrefab;

        [Header("UI")]
        [SerializeField] Font hudFont;
        [SerializeField] Texture2D coinIconTexture;
        [SerializeField] Texture2D[] ammoIconTextures = new Texture2D[10];

        [Header("Balance")]
        [SerializeField] int startingCoins = 1280;
        [SerializeField] int enemyHealth = 100;
        [SerializeField] int perfectDamage = 60;
        [SerializeField] int relatedDamage = 28;
        [SerializeField] int wrongDamage = 10;
        [SerializeField] int meleeCoinLoss = 85;
        [SerializeField] int rangedCoinLoss = 55;
        [SerializeField] float waveInterval = 6.5f;
        [SerializeField] float laneSpacing = 3.25f;
        [SerializeField] float enemyLabelHeight = 2.45f;
        [SerializeField] float turretPlacementCooldown = 4.5f;
        [SerializeField] float turretDropHeight = 7f;
        [SerializeField] float turretFireInterval = 1.2f;
        [SerializeField] float turretLifetime = 12f;

        readonly List<DefenseEnemy> _enemies = new List<DefenseEnemy>();
        readonly List<GameObject> _rangedTemplates = new List<GameObject>();
        readonly List<GameObject> _meleeTemplates = new List<GameObject>();
        readonly List<Button> _ammoButtons = new List<Button>();
        readonly List<Image> _ammoButtonImages = new List<Image>();
        readonly List<Image> _ammoButtonIcons = new List<Image>();
        readonly List<Image> _ammoButtonGlows = new List<Image>();
        readonly List<Outline> _ammoButtonOutlines = new List<Outline>();
        readonly List<Text> _ammoButtonLabels = new List<Text>();
        readonly List<Image> _ammoCooldownRings = new List<Image>();
        readonly List<Text> _ammoCooldownTexts = new List<Text>();
        readonly List<Image> _coinIcons = new List<Image>();
        readonly List<FraudToolTurret> _turrets = new List<FraudToolTurret>();
        readonly Sprite[] _ammoIconSprites = new Sprite[AmmoNames.Length];
        readonly float[] _toolReadyTimes = new float[10];
        readonly Coroutine[] _ammoPulseRoutines = new Coroutine[10];

        Transform _runtimeRoot;
        Transform _playerRoot;
        Transform _enemyRoot;
        Transform _worldLabelRoot;
        Animator _playerAnimator;
        AnimancerComponent _playerAnimancer;
        PlayerAnimancerLocomotion _playerLocomotion;
        Camera _viewCamera;
        ThirdPersonSpringArm _springArm;
        Animator _fraudsterAnimator;
        AnimancerComponent _fraudsterAnimancer;
        Coroutine _playerActionRoutine;
        Coroutine _coinRoutine;

        Canvas _canvas;
        RectTransform _coinFlightLayer;
        RectTransform _coinIconRect;
        Text _coinAmountText;
        RectTransform _notificationRoot;
        CanvasGroup _notificationGroup;
        Text _notificationText;
        RectTransform _skillPopupRoot;
        CanvasGroup _skillPopupGroup;
        Image _skillPopupGlow;
        Text _skillPopupText;
        RectTransform _damageBorderRoot;
        CanvasGroup _damageBorderGroup;
        Sprite _coinSprite;
        Sprite _panelSprite;
        Sprite _healthBarSprite;
        Sprite _cooldownRingSprite;
        Texture2D _panelTexture;
        Texture2D _healthBarTexture;
        Texture2D _cooldownRingTexture;
        int _coins;
        int _selectedAmmo;
        int _wave;
        int _placementIndex;
        float _groundY;
        float _centerX;
        bool _started;
        bool _fraudsterSummoning;
        Coroutine _notificationRoutine;
        Coroutine _skillPopupRoutine;
        Coroutine _damageBorderRoutine;

        public Camera ViewCamera => _viewCamera;
        public float EnemyLabelHeight => enemyLabelHeight;

        void Start()
        {
            if (playerPrefab == null)
            {
                Debug.LogError("[Defense] Player prefab is not assigned.", this);
                enabled = false;
                return;
            }
            if (fraudster == null)
                fraudster = GameObject.Find("thief");
            if (fraudster == null)
            {
                Debug.LogError("[Defense] Fraudster is not assigned.", this);
                enabled = false;
                return;
            }

            _coins = Mathf.Max(0, startingCoins);
            Input.imeCompositionMode = IMECompositionMode.Off;
            if (hudFont == null)
                hudFont = CreateChineseFont();
            _groundY = fraudster.transform.position.y;
            _centerX = fraudster.transform.position.x;
            fraudster.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            PrepareEnemyTemplates();
            CreatePlayer();
            CreateHud();
            SelectAmmo(0);

            _started = true;
            StartCoroutine(FraudsterTauntLoop());
            StartCoroutine(WaveLoop());
        }

        void Update()
        {
            if (!_started)
                return;

            HandleAmmoHotkeys();

            RefreshAmmoCooldowns();
            if (!Input.GetMouseButtonDown(0) ||
                EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            TryPlaceSelectedToolTurret();
        }

        void HandleAmmoHotkeys()
        {
            for (int i = 0; i < NumberRowKeys.Length; i++)
            {
                if (Input.GetKeyDown(NumberRowKeys[i]) ||
                    Input.GetKeyDown(KeypadKeys[i]))
                {
                    SelectAmmo(i);
                    return;
                }
            }

            string typed = Input.inputString;
            for (int i = 0; i < typed.Length; i++)
            {
                char key = typed[i];
                if (key >= '1' && key <= '9')
                {
                    SelectAmmo(key - '1');
                    return;
                }
                if (key == '0')
                {
                    SelectAmmo(9);
                    return;
                }
            }
        }

        void LateUpdate()
        {
            if (fraudster == null || _playerRoot == null)
                return;

            Vector3 direction = _playerRoot.position - fraudster.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.001f)
                return;

            fraudster.transform.rotation = Quaternion.Slerp(
                fraudster.transform.rotation,
                Quaternion.LookRotation(direction.normalized, Vector3.up),
                Time.deltaTime * 8f);
        }

        void PrepareEnemyTemplates()
        {
            _runtimeRoot = new GameObject("Anti-Fraud Defense Runtime").transform;
            MoveToOwningScene(_runtimeRoot.gameObject);
            _enemyRoot = new GameObject("Active Enemies").transform;
            _enemyRoot.SetParent(_runtimeRoot, false);
            _worldLabelRoot = new GameObject("Enemy Labels").transform;
            _worldLabelRoot.SetParent(_runtimeRoot, false);

            _rangedTemplates.Clear();
            _meleeTemplates.Clear();
            for (int i = 0; i < enemyTemplates.Length; i++)
            {
                GameObject template = enemyTemplates[i];
                if (template == null)
                    continue;

                if (template.scene.IsValid())
                    template.SetActive(false);
                if (template.name.IndexOf("Gloom", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    _rangedTemplates.Add(template);
                else
                    _meleeTemplates.Add(template);
            }

            HideLegacySceneMonsters();

            if (_rangedTemplates.Count == 0 && enemyTemplates.Length > 0)
                _rangedTemplates.Add(enemyTemplates[0]);
            if (_meleeTemplates.Count == 0)
            {
                for (int i = 0; i < enemyTemplates.Length; i++)
                {
                    if (enemyTemplates[i] != null &&
                        !_rangedTemplates.Contains(enemyTemplates[i]))
                        _meleeTemplates.Add(enemyTemplates[i]);
                }
            }
        }

        static void HideLegacySceneMonsters()
        {
            string[] names = { "Gloom", "Grim Reaper", "Wraith" };
            for (int i = 0; i < names.Length; i++)
            {
                GameObject legacy = GameObject.Find(names[i]);
                if (legacy != null)
                    legacy.SetActive(false);
            }
        }

        void CreatePlayer()
        {
            DisableStandaloneSceneCameras();
            _runtimeRoot.gameObject.SetActive(false);
            GameObject player = Instantiate(playerPrefab, _runtimeRoot);
            player.name = "Defense Player";
            player.transform.position = new Vector3(
                _centerX,
                _groundY,
                fraudster.transform.position.z - 5.7f);
            player.transform.rotation = Quaternion.identity;
            _playerRoot = player.transform;
            _runtimeRoot.gameObject.SetActive(true);

            _viewCamera = null;
            ThirdPersonPlayerController controller =
                player.GetComponent<ThirdPersonPlayerController>();
            if (controller != null)
                _viewCamera = controller.GameplayCamera;
            _springArm = null;

            if (_viewCamera != null)
            {
                _springArm =
                    _viewCamera.GetComponentInParent<ThirdPersonSpringArm>(true);
                _viewCamera.gameObject.SetActive(true);
                _viewCamera.enabled = true;
                _viewCamera.tag = "MainCamera";
                AudioListener listener = _viewCamera.GetComponent<AudioListener>();
                if (listener != null)
                    listener.enabled = true;
            }
            else
            {
                Debug.LogError(
                    "[Defense] The Player controller did not expose a spring-arm camera.",
                    this);
            }

            _playerAnimator = player.GetComponentInChildren<Animator>(true);
            if (_playerAnimator != null)
            {
                _playerAnimator.applyRootMotion = false;
                _playerAnimancer = _playerAnimator.GetComponent<AnimancerComponent>();
                _playerLocomotion = player.GetComponentInChildren<PlayerAnimancerLocomotion>(true);
                if (_playerAnimancer != null)
                {
                    _playerAnimancer.Animator = _playerAnimator;
                    _playerLocomotion?.Initialize(_playerAnimancer);
                }
            }

            _fraudsterAnimator = fraudster.GetComponentInChildren<Animator>(true);
            if (_fraudsterAnimator != null)
            {
                _fraudsterAnimator.applyRootMotion = false;
                _fraudsterAnimator.runtimeAnimatorController = null;
                _fraudsterAnimancer =
                    _fraudsterAnimator.GetComponent<AnimancerComponent>();
                if (_fraudsterAnimancer == null)
                    _fraudsterAnimancer =
                        _fraudsterAnimator.gameObject.AddComponent<AnimancerComponent>();
                _fraudsterAnimancer.Animator = _fraudsterAnimator;
            }
        }

        void MoveToOwningScene(GameObject root)
        {
            Scene owningScene = gameObject.scene;
            if (root == null ||
                !owningScene.IsValid() ||
                !owningScene.isLoaded ||
                root.scene == owningScene)
            {
                return;
            }

            SceneManager.MoveGameObjectToScene(root, owningScene);
        }

        static void DisableStandaloneSceneCameras()
        {
            Camera[] cameras = FindObjectsOfType<Camera>(true);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null)
                    cameras[i].enabled = false;
            }

            AudioListener[] listeners = FindObjectsOfType<AudioListener>(true);
            for (int i = 0; i < listeners.Length; i++)
            {
                if (listeners[i] != null)
                    listeners[i].enabled = false;
            }
        }

        IEnumerator WaveLoop()
        {
            yield return new WaitForSeconds(0.35f);
            while (true)
            {
                while (HasLivingEnemies())
                    yield return null;

                if (_wave > 0)
                    yield return new WaitForSeconds(waveInterval);
                _wave++;
                Coroutine summon = StartCoroutine(FraudsterSummonRoutine());
                yield return new WaitForSeconds(0.45f);

                for (int lane = 0; lane < 3; lane++)
                {
                    GameObject template = lane == 0
                        ? PickTemplate(_rangedTemplates)
                        : PickTemplate(_meleeTemplates);
                    SpawnEnemy(template, lane);
                    yield return new WaitForSeconds(0.16f);
                }

                yield return summon;
            }
        }

        bool HasLivingEnemies()
        {
            _enemies.RemoveAll(enemy => enemy == null || enemy.IsDead);
            return _enemies.Count > 0;
        }

        void SpawnEnemy(GameObject template, int lane)
        {
            if (template == null)
                return;

            Vector3 forward = fraudster.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 summonCenter =
                fraudster.transform.position + forward * enemySpawnForwardOffset;
            Vector3 spawnPosition =
                summonCenter + right * (lane - 1) * laneSpacing;
            spawnPosition.y = _groundY;

            GameObject enemy = Instantiate(template, _enemyRoot);
            enemy.name = $"{template.name}_{_wave}_{lane + 1}";
            enemy.transform.SetPositionAndRotation(
                spawnPosition,
                Quaternion.LookRotation(forward, Vector3.up));
            enemy.SetActive(true);

            var collider = enemy.GetComponent<BoxCollider>();
            if (collider == null)
                collider = enemy.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.center = Vector3.up * 1.0f;
            collider.size = new Vector3(1.35f, 2.1f, 1.35f);

            bool ranged = template.name.IndexOf(
                "Gloom",
                System.StringComparison.OrdinalIgnoreCase) >= 0;
            int scamIndex = Random.Range(0, ScamNames.Length);
            int matchingAmmo = ScamMatchingAmmo[scamIndex];
            string attackState = ranged
                ? "Projectile Attack"
                : template.name.IndexOf("Grim", System.StringComparison.OrdinalIgnoreCase) >= 0
                    ? "Slash Attack 01"
                    : "Wing Slash Attack";

            Text label = CreateWorldLabel(out Image healthFill);
            var defenseEnemy = enemy.AddComponent<DefenseEnemy>();
            defenseEnemy.Initialize(
                this,
                _playerRoot,
                label,
                healthFill,
                ScamNames[scamIndex],
                matchingAmmo,
                AmmoGroups[matchingAmmo],
                ranged,
                ranged ? 1.05f : Random.Range(1.15f, 1.45f),
                ranged ? 6.1f : 1.45f,
                ranged ? 3.15f : 2.35f,
                enemyHealth,
                attackState,
                enemySpawnState,
                enemySpawnFallbackDuration);

            _enemies.Add(defenseEnemy);
            SpawnFx(
                enemySummonEffectPrefab,
                enemy.transform.position + Vector3.up * 0.25f,
                maximumLifetime: 1.5f,
                scale: enemySummonEffectScale);
        }

        static GameObject PickTemplate(List<GameObject> templates)
        {
            if (templates == null || templates.Count == 0)
                return null;
            return templates[Random.Range(0, templates.Count)];
        }

        void TryPlaceSelectedToolTurret()
        {
            int ammoIndex = _selectedAmmo;
            if (Time.time < _toolReadyTimes[ammoIndex])
            {
                float remaining = _toolReadyTimes[ammoIndex] - Time.time;
                SetHint(
                    $"{AmmoNames[ammoIndex]} 放置冷却中 {remaining:0.0}s",
                    new Color(1f, 0.58f, 0.24f));
                return;
            }

            _toolReadyTimes[ammoIndex] = Time.time + turretPlacementCooldown;

            int slot = _placementIndex++;
            int ring = slot / 6;
            float angle = slot * 57f;
            float radius = 1.8f + ring * 0.85f;
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) *
                             (Vector3.forward * radius);
            Vector3 landing = new Vector3(
                _playerRoot.position.x + offset.x,
                _groundY,
                _playerRoot.position.z + offset.z);

            Color color = Color.Lerp(
                new Color(1f, 0.72f, 0.18f),
                new Color(0.22f, 0.7f, 1f),
                ammoIndex / 9f);
            var turretRoot = new GameObject($"Tool Turret {ammoIndex + 1}");
            turretRoot.transform.SetParent(_runtimeRoot, false);

            CreateTurretStatue(turretRoot.transform, color);

            Text label = CreateWorldText(
                "Tool Name",
                new Vector2(440f, 280f),
                0.0047f,
                54f,
                new Color(0.34f, 1f, 0.58f),
                false);
            SetWorldTextOutline(label, Color.white);
            label.text = AmmoNames[ammoIndex];
            label.alignment = TextAnchor.MiddleCenter;
            RectTransform labelTextRect = label.rectTransform;
            labelTextRect.anchorMin = new Vector2(0.04f, 0.02f);
            labelTextRect.anchorMax = new Vector2(0.96f, 0.31f);
            labelTextRect.offsetMin = Vector2.zero;
            labelTextRect.offsetMax = Vector2.zero;

            Canvas labelCanvas = label.GetComponentInParent<Canvas>();
            if (labelCanvas != null)
            {
                labelCanvas.sortingOrder = 70;
                labelCanvas.transform.position =
                    turretRoot.transform.position + Vector3.up * 3.15f;

                Image icon = CreateImage(
                    "Tool Icon",
                    labelCanvas.transform,
                    Vector2.zero,
                    new Vector2(180f, 180f),
                    Color.white);
                RectTransform iconRect = icon.rectTransform;
                iconRect.anchorMin = new Vector2(0.5f, 0.68f);
                iconRect.anchorMax = new Vector2(0.5f, 0.68f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = Vector2.zero;
                icon.sprite = GetAmmoIconSprite(ammoIndex);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
            }

            StartCoroutine(DropTurretRoutine(
                turretRoot,
                label,
                ammoIndex,
                color,
                landing));
            ShowSkillRelease(AmmoNames[ammoIndex]);
            SetHint(
                $"正在召唤：{AmmoNames[ammoIndex]}",
                new Color(0.75f, 0.9f, 1f));
        }

        void CreateTurretStatue(Transform parent, Color color)
        {
            if (turretStatuePrefab != null)
            {
                GameObject statue = Instantiate(
                    turretStatuePrefab,
                    parent,
                    false);
                statue.name = "Statue";
                statue.transform.localPosition = Vector3.zero;
                statue.transform.localRotation = Quaternion.identity;

                Collider[] colliders =
                    statue.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < colliders.Length; i++)
                    colliders[i].enabled = false;
                return;
            }

            GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallback.name = "Statue";
            fallback.transform.SetParent(parent, false);
            fallback.transform.localPosition = Vector3.up * 0.8f;
            fallback.transform.localScale =
                new Vector3(1.35f, 1.6f, 1.35f);
            Destroy(fallback.GetComponent<Collider>());
            Renderer fallbackRenderer = fallback.GetComponent<Renderer>();
            fallbackRenderer.material = CreateTurretMaterial(color);
        }

        IEnumerator DropTurretRoutine(
            GameObject turretRoot,
            Text label,
            int ammoIndex,
            Color color,
            Vector3 landing)
        {
            Vector3 start = landing + Vector3.up * turretDropHeight;
            turretRoot.transform.position = start;
            turretRoot.transform.rotation = Quaternion.Euler(
                0f,
                Random.Range(0f, 360f),
                0f);

            const float dropDuration = 0.62f;
            float elapsed = 0f;
            while (elapsed < dropDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dropDuration);
                float eased = t * t;
                turretRoot.transform.position = Vector3.LerpUnclamped(
                    start,
                    landing,
                    eased);
                if (label != null && _viewCamera != null)
                {
                    Canvas movingLabel = label.GetComponentInParent<Canvas>();
                    if (movingLabel != null)
                    {
                        movingLabel.transform.position =
                            turretRoot.transform.position + Vector3.up * 3.15f;
                        movingLabel.transform.rotation =
                            _viewCamera.transform.rotation;
                    }
                }
                yield return null;
            }

            turretRoot.transform.position = landing;
            SpawnFx(
                turretLandingEffectPrefab,
                landing + Vector3.up * 0.15f,
                scale: turretLandingEffectScale,
                preservePrefabRotation: true);
            if (_springArm == null)
                _springArm = FindObjectOfType<ThirdPersonSpringArm>(true);
            _springArm?.AddShake(0.14f, 0.28f);

            FraudToolTurret turret = turretRoot.AddComponent<FraudToolTurret>();
            turret.Initialize(
                this,
                ammoIndex,
                label,
                turretFireInterval,
                turretLifetime);
            _turrets.Add(turret);
        }

        static Material CreateTurretMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            var material = new Material(shader)
            {
                name = "Runtime Fraud Tool Statue"
            };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.45f);
            return material;
        }

        public DefenseEnemy FindNearestEnemy(Vector3 position, float range)
        {
            _enemies.RemoveAll(enemy => enemy == null || enemy.IsDead);
            DefenseEnemy nearest = null;
            float nearestDistance = range * range;
            for (int i = 0; i < _enemies.Count; i++)
            {
                if (_enemies[i] == null || _enemies[i].IsDead || _enemies[i].IsSpawning)
                    continue;

                Vector3 offset = _enemies[i].AimPoint - position;
                float distance = offset.sqrMagnitude;
                if (distance >= nearestDistance)
                    continue;
                nearestDistance = distance;
                nearest = _enemies[i];
            }

            return nearest;
        }

        public void FireTurretProjectile(
            FraudToolTurret turret,
            int ammoIndex,
            DefenseEnemy enemy,
            Vector3 origin)
        {
            if (turret == null || enemy == null || enemy.IsDead)
                return;

            Vector3 direction = (enemy.AimPoint - origin).normalized;
            Material trailMaterial;
            GameObject projectileRoot = CreateVisibleProjectile(
                $"Turret Projectile {ammoIndex + 1}",
                origin + direction * 0.25f,
                Quaternion.LookRotation(direction, Vector3.up),
                GetPrefab(ammoProjectilePrefabs, ammoIndex),
                Color.Lerp(
                    new Color(1f, 0.72f, 0.18f),
                    new Color(0.22f, 0.7f, 1f),
                    ammoIndex / 9f),
                turretProjectileEffectScale,
                out trailMaterial);
            DefenseProjectile projectile = projectileRoot.AddComponent<DefenseProjectile>();
            projectile.OwnMaterial(trailMaterial);
            projectile.InitializeEnemyTarget(
                enemy,
                20f,
                point => ResolveTurretHit(enemy, ammoIndex, point));
        }

        void ResolveTurretHit(DefenseEnemy enemy, int ammoIndex, Vector3 point)
        {
            if (enemy == null || enemy.IsDead)
                return;

            int damage = wrongDamage;
            string result = "不匹配";
            Color color = new Color(0.9f, 0.35f, 0.3f);
            if (ammoIndex == enemy.MatchingAmmoIndex)
            {
                damage = perfectDamage;
                result = "精准命中";
                color = new Color(1f, 0.78f, 0.24f);
            }
            else if (AmmoGroups[ammoIndex] == enemy.MatchingGroup)
            {
                damage = relatedDamage;
                result = "同类有效";
                color = new Color(0.3f, 0.78f, 1f);
            }

            enemy.TakeDamage(damage);
            SpawnFx(enemyHitEffectPrefab, point);
            SpawnDamagePopup(point, damage, result, color);
            SetHint($"{result} · {AmmoNames[ammoIndex]} -{damage} HP", color);
        }

        public void EnemyAttack(DefenseEnemy enemy, bool ranged)
        {
            if (enemy == null || enemy.IsDead)
                return;

            if (!ranged)
            {
                ApplyCoinLoss(enemy, meleeCoinLoss);
                return;
            }

            Vector3 origin = enemy.AimPoint;
            Vector3 target = _playerRoot.position + Vector3.up * 1.15f;
            Vector3 direction = (target - origin).normalized;
            Material trailMaterial;
            GameObject projectile = CreateVisibleProjectile(
                "Enemy Attack Projectile",
                origin,
                Quaternion.LookRotation(direction, Vector3.up),
                rangedEnemyProjectilePrefab,
                new Color(1f, 0.26f, 0.2f),
                ParticleEffectScale,
                out trailMaterial);
            DefenseProjectile homing = projectile.AddComponent<DefenseProjectile>();
            homing.OwnMaterial(trailMaterial);
            homing.InitializePlayerTarget(
                _playerRoot,
                12f,
                _ => ApplyCoinLoss(enemy, rangedCoinLoss));
        }

        GameObject CreateVisibleProjectile(
            string name,
            Vector3 position,
            Quaternion rotation,
            GameObject particlePrefab,
            Color color,
            float effectScale,
            out Material material)
        {
            var root = new GameObject(name);
            root.transform.SetPositionAndRotation(position, rotation);

            if (particlePrefab != null)
            {
                GameObject particle = Instantiate(particlePrefab, root.transform, false);
                particle.transform.localPosition = Vector3.zero;
                particle.transform.localRotation = Quaternion.identity;
                particle.transform.localScale = Vector3.one * effectScale;
            }

            material = CreateProjectileMaterial(color);
            GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Visible Projectile Core";
            core.transform.SetParent(root.transform, false);
            core.transform.localPosition = Vector3.zero;
            core.transform.localScale = Vector3.one *
                (0.09f + Mathf.Max(0f, effectScale) * 0.58f);
            Destroy(core.GetComponent<Collider>());
            Renderer coreRenderer = core.GetComponent<Renderer>();
            coreRenderer.material = material;

            TrailRenderer trail = root.AddComponent<TrailRenderer>();
            trail.time = 0.32f + Mathf.Max(0f, effectScale) * 0.4f;
            trail.minVertexDistance = 0.018f;
            trail.startWidth =
                0.09f + Mathf.Max(0f, effectScale) * 0.6f;
            trail.endWidth =
                0.012f + Mathf.Max(0f, effectScale) * 0.03f;
            trail.startColor = color;
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
            trail.material = material;
            trail.textureMode = LineTextureMode.Stretch;
            return root;
        }

        static Material CreateProjectileMaterial(Color color)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            var material = new Material(shader)
            {
                name = "Runtime Projectile Trail"
            };
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            return material;
        }

        void ApplyCoinLoss(DefenseEnemy enemy, int amount)
        {
            int previous = _coins;
            _coins = Mathf.Max(0, _coins - Mathf.Max(1, amount));
            int actualLoss = previous - _coins;
            if (actualLoss <= 0)
                return;

            _coinAmountText.text = _coins.ToString("N0");
            SetHint($"警惕！被骗走 {actualLoss} 金币", new Color(1f, 0.34f, 0.28f));
            SpawnFx(playerHitEffectPrefab, _playerRoot.position + Vector3.up * 1.1f);
            FlashDamageBorder();

            if (_coinRoutine != null)
                StopCoroutine(_coinRoutine);
            _coinRoutine = StartCoroutine(CoinLossRoutine(actualLoss));
        }

        public void EnemyHealthChanged(DefenseEnemy enemy)
        {
            _enemies.RemoveAll(item => item == null || item.IsDead);
        }

        public Vector3 GetEnemySeparation(DefenseEnemy seeker)
        {
            if (seeker == null)
                return Vector3.zero;

            const float requiredSpacing = 1.8f;
            Vector3 separation = Vector3.zero;
            for (int i = 0; i < _enemies.Count; i++)
            {
                DefenseEnemy other = _enemies[i];
                if (other == null || other == seeker || other.IsDead)
                    continue;

                Vector3 offset = seeker.transform.position - other.transform.position;
                offset.y = 0f;
                float distance = offset.magnitude;
                if (distance >= requiredSpacing)
                    continue;
                if (distance < 0.001f)
                {
                    offset = seeker.transform.right;
                    distance = 0.001f;
                }

                separation += offset.normalized * (1f - distance / requiredSpacing);
            }

            return separation;
        }

        public void OnEnemyDestroyed(DefenseEnemy enemy)
        {
            _enemies.Remove(enemy);
        }

        IEnumerator CoinLossRoutine(int amount)
        {
            RectTransform crate = _coinFlightLayer;
            Vector2 startScreen = RectTransformUtility.WorldToScreenPoint(
                null,
                _coinIconRect.position);
            Vector2 endScreen = new Vector2(
                Screen.width * 0.5f,
                Screen.height * 0.5f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                crate,
                startScreen,
                null,
                out Vector2 start);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                crate,
                endScreen,
                null,
                out Vector2 end);

            const float travel = 0.95f;
            const float stagger = 0.075f;
            float total = travel + (_coinIcons.Count - 1) * stagger;
            float started = Time.unscaledTime;
            Vector2 startVector = start - end;
            float startRadius = startVector.magnitude;
            float startAngle = Mathf.Atan2(startVector.y, startVector.x);

            Vector3 originalScale = _coinAmountText.rectTransform.localScale;
            while (Time.unscaledTime - started < total)
            {
                float elapsed = Time.unscaledTime - started;
                for (int i = 0; i < _coinIcons.Count; i++)
                {
                    Image icon = _coinIcons[i];
                    float local = (elapsed - i * stagger) / travel;
                    if (local < 0f || local >= 1f)
                    {
                        icon.gameObject.SetActive(false);
                        continue;
                    }

                    float t = local * local * (3f - 2f * local);
                    float angle =
                        startAngle + t * Mathf.PI * 1.45f;
                    float radius = startRadius *
                        Mathf.Pow(1f - t, 1.15f);
                    Vector2 position = end + new Vector2(
                        Mathf.Cos(angle),
                        Mathf.Sin(angle)) * radius;

                    icon.gameObject.SetActive(true);
                    icon.rectTransform.anchoredPosition = position;
                    icon.rectTransform.sizeDelta = Vector2.one *
                        Mathf.Lerp(72f, 34f, t);
                    icon.color = new Color(1f, 1f, 1f,
                        Mathf.Clamp01(local / 0.08f) *
                        (1f - Mathf.Clamp01((local - 0.78f) / 0.22f)));
                }

                float pulse = 1f + Mathf.Sin(
                    Mathf.Clamp01(elapsed / total) * Mathf.PI) * 0.22f;
                _coinAmountText.rectTransform.localScale = originalScale * pulse;
                yield return null;
            }

            _coinAmountText.rectTransform.localScale = originalScale;
            for (int i = 0; i < _coinIcons.Count; i++)
                _coinIcons[i].gameObject.SetActive(false);
            _coinRoutine = null;
        }

        IEnumerator FraudsterSummonRoutine()
        {
            _fraudsterSummoning = true;
            float duration = 0.75f;
            if (_fraudsterAnimancer != null && fraudsterSummonClip != null)
            {
                _fraudsterAnimancer.Play(
                    fraudsterSummonClip,
                    0.15f,
                    FadeMode.FromStart);
                duration = Mathf.Max(duration, fraudsterSummonClip.length);
            }

            SpawnFx(
                enemySpawnEffectPrefab,
                fraudster.transform.position + Vector3.up * 1.2f,
                0.65f);
            yield return new WaitForSeconds(duration);
            _fraudsterSummoning = false;
        }

        IEnumerator FraudsterTauntLoop()
        {
            yield return new WaitForSeconds(1.5f);
            while (true)
            {
                while (_fraudsterSummoning)
                    yield return null;

                AnimationClip clip = PickRandomFraudsterDance();
                if (clip == null)
                {
                    yield return new WaitForSeconds(1.4f);
                    continue;
                }

                _fraudsterAnimancer.Play(clip, 0.2f, FadeMode.FromStart);
                yield return new WaitForSeconds(Mathf.Max(0.1f, clip.length));
            }
        }

        AnimationClip PickRandomFraudsterDance()
        {
            if (_fraudsterAnimancer == null ||
                fraudsterDanceClips == null ||
                fraudsterDanceClips.Length == 0)
                return null;

            // Only Dance_1 through Dance_3 are idle candidates.
            int clipCount = Mathf.Min(3, fraudsterDanceClips.Length);
            int start = Random.Range(0, clipCount);
            for (int offset = 0; offset < clipCount; offset++)
            {
                AnimationClip clip =
                    fraudsterDanceClips[(start + offset) % clipCount];
                if (clip == null)
                    continue;

                return clip;
            }

            return null;
        }

        void PlayPlayerIdle()
        {
            if (_playerLocomotion != null)
                return;
            if (_playerAnimancer != null && playerIdleClip != null)
                _playerAnimancer.Play(playerIdleClip, 0.15f);
        }

        void PlayPlayerAction(AnimationClip clip)
        {
            if (_playerAnimancer == null || clip == null)
                return;

            _playerLocomotion?.NotifyActionStarted();
            if (_playerActionRoutine != null)
                StopCoroutine(_playerActionRoutine);
            _playerActionRoutine = StartCoroutine(
                PlayerActionRoutine(clip));
        }

        IEnumerator PlayerActionRoutine(AnimationClip clip)
        {
            _playerAnimancer.Play(clip, 0.06f, FadeMode.FromStart);
            yield return new WaitForSeconds(Mathf.Max(0.2f, clip.length));
            _playerActionRoutine = null;
            _playerLocomotion?.NotifyActionEnded();
            PlayPlayerIdle();
        }

        Text CreateWorldLabel(out Image healthFill)
        {
            Text label = CreateWorldText(
                "Scam Label",
                new Vector2(600f, 160f),
                0.0045f,
                44f,
                new Color(1f, 0.92f, 0.78f),
                false);
            SetWorldTextOutline(
                label,
                Color.white);
            label.alignment = TextAnchor.MiddleCenter;
            RectTransform nameRect = label.rectTransform;
            nameRect.anchorMin = new Vector2(0.06f, 0.38f);
            nameRect.anchorMax = new Vector2(0.94f, 1f);
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;

            Canvas labelCanvas = label.GetComponentInParent<Canvas>();
            RectTransform canvasRect = labelCanvas.transform as RectTransform;

            var trackGo = new GameObject(
                "Health Track",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            RectTransform trackRect = trackGo.GetComponent<RectTransform>();
            trackRect.SetParent(canvasRect, false);
            trackRect.anchorMin = new Vector2(0.22f, 0.1f);
            trackRect.anchorMax = new Vector2(0.78f, 0.27f);
            trackRect.offsetMin = Vector2.zero;
            trackRect.offsetMax = Vector2.zero;
            Image track = trackGo.GetComponent<Image>();
            track.sprite = _healthBarSprite;
            track.type = Image.Type.Simple;
            track.color = new Color(0.035f, 0.075f, 0.12f, 0.96f);
            track.raycastTarget = false;

            var backgroundGo = new GameObject(
                "Health Background",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            RectTransform backgroundRect =
                backgroundGo.GetComponent<RectTransform>();
            backgroundRect.SetParent(trackRect, false);
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = new Vector2(2f, 2f);
            backgroundRect.offsetMax = new Vector2(-2f, -2f);
            Image background = backgroundGo.GetComponent<Image>();
            background.sprite = _healthBarSprite;
            background.type = Image.Type.Simple;
            background.color = new Color(0.12f, 0.025f, 0.035f, 0.95f);
            background.raycastTarget = false;

            var fillGo = new GameObject(
                "Health Fill",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            RectTransform fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.SetParent(backgroundRect, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(1f, 1f);
            fillRect.offsetMax = new Vector2(-1f, -1f);
            healthFill = fillGo.GetComponent<Image>();
            healthFill.sprite = _healthBarSprite;
            healthFill.type = Image.Type.Filled;
            healthFill.fillMethod = Image.FillMethod.Horizontal;
            healthFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            healthFill.fillAmount = 1f;
            healthFill.color = new Color(1f, 0.2f, 0.16f, 1f);
            healthFill.raycastTarget = false;
            return label;
        }

        static void SetWorldTextOutline(Text text, Color color)
        {
            Outline outline = text != null ? text.GetComponent<Outline>() : null;
            if (outline == null)
                return;
            outline.effectColor = color;
            outline.effectDistance = new Vector2(2.8f, -2.8f);

            Shadow shadow = text.GetComponent<Shadow>();
            if (shadow == null)
                shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.015f, 0.04f, 0.08f, 0.88f);
            shadow.effectDistance = new Vector2(3.2f, -3.2f);
        }

        Text CreateWorldText(
            string name,
            Vector2 size,
            float worldScale,
            float fontSize,
            Color color,
            bool withBackground)
        {
            var canvasGo = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasGroup));
            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.SetParent(_worldLabelRoot, false);
            canvasRect.sizeDelta = size;
            canvasRect.localScale = Vector3.one * worldScale;

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = _viewCamera;
            canvas.sortingOrder = 60;

            CanvasGroup group = canvasGo.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            if (withBackground)
            {
                var backgroundGo = new GameObject(
                    "Background",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                RectTransform backgroundRect = backgroundGo.GetComponent<RectTransform>();
                backgroundRect.SetParent(canvasRect, false);
                backgroundRect.anchorMin = Vector2.zero;
                backgroundRect.anchorMax = Vector2.one;
                backgroundRect.offsetMin = Vector2.zero;
                backgroundRect.offsetMax = Vector2.zero;
                Image background = backgroundGo.GetComponent<Image>();
                background.color = new Color(0.025f, 0.07f, 0.12f, 0.82f);
                background.raycastTarget = false;
            }

            var textGo = new GameObject(
                "Text",
                typeof(RectTransform),
                typeof(CanvasRenderer));
            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.SetParent(canvasRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 6f);
            textRect.offsetMax = new Vector2(-10f, -6f);

            Text text = textGo.AddComponent<Text>();
            text.font = hudFont;
            text.fontSize = Mathf.RoundToInt(fontSize);
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;

            var outline = textGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.015f, 0.04f, 0.08f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        void SpawnDamagePopup(Vector3 position, int damage, string result, Color color)
        {
            Text popup = CreateWorldText(
                "Damage Popup",
                new Vector2(380f, 140f),
                0.0062f,
                46f,
                color,
                false);
            popup.text = $"-{damage}\n{result}";
            Canvas popupCanvas = popup.GetComponentInParent<Canvas>();
            RectTransform popupRect = popupCanvas.transform as RectTransform;
            popupRect.position = position + Vector3.up * 0.25f;
            popupRect.rotation = _viewCamera != null
                ? _viewCamera.transform.rotation
                : Quaternion.identity;
            StartCoroutine(DamagePopupRoutine(popup, popupRect));
        }

        IEnumerator DamagePopupRoutine(Text popup, RectTransform popupRect)
        {
            if (popup == null || popupRect == null)
                yield break;

            CanvasGroup group = popup.GetComponentInParent<CanvasGroup>();
            Vector3 start = popupRect.position;
            Vector3 baseScale = popupRect.localScale;
            const float duration = 0.9f;
            float elapsed = 0f;
            while (elapsed < duration && popup != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                popupRect.position = start + Vector3.up * (1.1f * t);
                if (_viewCamera != null)
                    popupRect.rotation = _viewCamera.transform.rotation;
                popupRect.localScale = baseScale * Mathf.Lerp(1.18f, 0.9f, t);
                if (group != null)
                    group.alpha = 1f - Mathf.Clamp01((t - 0.55f) / 0.45f);
                yield return null;
            }

            if (popup != null)
                Destroy(popupRect.gameObject);
        }

        void CreateHud()
        {
            EnsureEventSystem();
            _coinSprite = coinIconTexture != null
                ? CreateSpriteFromTexture(coinIconTexture, "School Coin Icon")
                : null;
            if (_coinSprite == null)
            {
                Debug.LogError(
                    "[Defense] School coin icon is not assigned. " +
                    "Assign Assets/ImportResource/ImageAsset/ICON.png.",
                    this);
            }
            _panelSprite = CreateRoundedSprite();
            _healthBarSprite = CreateHealthBarSprite();
            _cooldownRingSprite = CreateCooldownRingSprite();

            var canvasGo = new GameObject(
                "Defense HUD",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            MoveToOwningScene(canvasGo);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 200;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();

            RectTransform coinPanel = CreatePanel(
                "Coin Panel",
                canvasRect,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(26f, -22f),
                new Vector2(320f, 88f),
                HudPanelColor);
            Image coinIcon = CreateImage(
                "Coin Icon",
                coinPanel,
                new Vector2(10f, -4f),
                new Vector2(80f, 80f),
                Color.white);
            coinIcon.sprite = _coinSprite;
            coinIcon.preserveAspect = true;
            _coinIconRect = coinIcon.rectTransform;

            _coinAmountText = CreateText(
                "Coin Amount",
                coinPanel,
                "1,280",
                42f,
                TextAnchor.MiddleLeft);
            RectTransform coinTextRect = _coinAmountText.rectTransform;
            coinTextRect.anchorMin = new Vector2(0f, 1f);
            coinTextRect.anchorMax = new Vector2(0f, 1f);
            coinTextRect.pivot = new Vector2(0f, 1f);
            coinTextRect.anchoredPosition = new Vector2(98f, -18f);
            coinTextRect.sizeDelta = new Vector2(200f, 52f);
            _coinAmountText.text = _coins.ToString("N0");
            _coinAmountText.color = HudInkColor;

            Text title = CreateText(
                "Title",
                canvasRect,
                "反诈脑域",
                38f,
                TextAnchor.MiddleCenter);
            RectTransform titleTextRect = title.rectTransform;
            titleTextRect.anchorMin = new Vector2(0.5f, 1f);
            titleTextRect.anchorMax = new Vector2(0.5f, 1f);
            titleTextRect.pivot = new Vector2(0.5f, 1f);
            titleTextRect.anchoredPosition = new Vector2(0f, -18f);
            titleTextRect.sizeDelta = new Vector2(430f, 70f);
            title.color = HudSkyBlueColor;
            Outline titleOutline = title.gameObject.AddComponent<Outline>();
            titleOutline.effectColor = Color.white;
            titleOutline.effectDistance = new Vector2(2.5f, -2.5f);

            _notificationRoot = new GameObject(
                "Left Notification",
                typeof(RectTransform),
                typeof(CanvasGroup)).GetComponent<RectTransform>();
            _notificationRoot.SetParent(canvasRect, false);
            _notificationRoot.anchorMin = new Vector2(0f, 0.52f);
            _notificationRoot.anchorMax = new Vector2(0f, 0.52f);
            _notificationRoot.pivot = new Vector2(0f, 0.5f);
            _notificationRoot.anchoredPosition = new Vector2(36f, 0f);
            _notificationRoot.sizeDelta = new Vector2(680f, 58f);
            _notificationRoot.gameObject.SetActive(false);
            _notificationGroup =
                _notificationRoot.GetComponent<CanvasGroup>();
            _notificationGroup.alpha = 0f;
            _notificationGroup.blocksRaycasts = false;
            _notificationGroup.interactable = false;

            _notificationText = CreateText(
                "Notification",
                _notificationRoot,
                "",
                25f,
                TextAnchor.MiddleLeft);
            RectTransform notificationRect =
                _notificationText.rectTransform;
            notificationRect.anchorMin = Vector2.zero;
            notificationRect.anchorMax = Vector2.one;
            notificationRect.offsetMin = new Vector2(0f, 4f);
            notificationRect.offsetMax = new Vector2(0f, -4f);
            _notificationText.color = Color.black;
            _notificationText.horizontalOverflow =
                HorizontalWrapMode.Overflow;
            Outline notificationOutline =
                _notificationText.gameObject.AddComponent<Outline>();
            notificationOutline.effectColor = Color.white;
            notificationOutline.effectDistance =
                new Vector2(2.2f, -2.2f);

            RectTransform ammoBar = CreatePanel(
                "Ammo Bar",
                canvasRect,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 16f),
                new Vector2(-64f, 210f),
                HudPanelColor);

            Text ammoTitle = CreateText(
                "Ammo Title",
                ammoBar,
                "反诈全家桶",
                30f,
                TextAnchor.MiddleCenter);
            RectTransform ammoTitleRect = ammoTitle.rectTransform;
            ammoTitleRect.anchorMin = new Vector2(0f, 1f);
            ammoTitleRect.anchorMax = new Vector2(1f, 1f);
            ammoTitleRect.pivot = new Vector2(0.5f, 1f);
            ammoTitleRect.anchoredPosition = new Vector2(0f, -6f);
            ammoTitleRect.sizeDelta = new Vector2(-32f, 48f);
            ammoTitle.color = HudSkyBlueColor;
            Outline ammoTitleOutline =
                ammoTitle.gameObject.AddComponent<Outline>();
            ammoTitleOutline.effectColor = Color.white;
            ammoTitleOutline.effectDistance =
                new Vector2(2.2f, -2.2f);

            RectTransform buttonsRoot = new GameObject(
                "Ammo Buttons",
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            buttonsRoot.SetParent(ammoBar, false);
            buttonsRoot.anchorMin = new Vector2(0f, 0f);
            buttonsRoot.anchorMax = new Vector2(1f, 1f);
            buttonsRoot.offsetMin = new Vector2(18f, 10f);
            buttonsRoot.offsetMax = new Vector2(-18f, -58f);
            var layout = buttonsRoot.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            for (int i = 0; i < AmmoNames.Length; i++)
                CreateAmmoButton(buttonsRoot, i);

            CreateSkillReleasePopup(canvasRect);

            _coinFlightLayer = new GameObject(
                "Coin Flight Layer",
                typeof(RectTransform),
                typeof(CanvasGroup)).GetComponent<RectTransform>();
            _coinFlightLayer.SetParent(canvasRect, false);
            _coinFlightLayer.anchorMin = Vector2.zero;
            _coinFlightLayer.anchorMax = Vector2.one;
            _coinFlightLayer.offsetMin = Vector2.zero;
            _coinFlightLayer.offsetMax = Vector2.zero;
            CanvasGroup coinGroup = _coinFlightLayer.GetComponent<CanvasGroup>();
            coinGroup.blocksRaycasts = false;
            coinGroup.interactable = false;
            _coinFlightLayer.SetAsLastSibling();

            for (int i = 0; i < 14; i++)
            {
                Image coin = CreateImage(
                    $"Flying Coin {i}",
                    _coinFlightLayer,
                    Vector2.zero,
                    new Vector2(72f, 72f),
                    Color.white);
                coin.sprite = _coinSprite;
                coin.raycastTarget = false;
                coin.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                coin.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                coin.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                coin.gameObject.SetActive(false);
                _coinIcons.Add(coin);
            }

            CreateDamageBorder(canvasRect);
        }

        void CreateAmmoButton(RectTransform parent, int index)
        {
            var go = new GameObject(
                $"Ammo {index + 1}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            Image image = go.GetComponent<Image>();
            image.color = AmmoIdleColor;
            image.sprite = _panelSprite;
            image.type = Image.Type.Sliced;

            Image selectionGlow = new GameObject(
                "Selection Glow",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)).GetComponent<Image>();
            RectTransform glowRect = selectionGlow.rectTransform;
            glowRect.SetParent(rect, false);
            glowRect.SetAsFirstSibling();
            glowRect.anchorMin = Vector2.zero;
            glowRect.anchorMax = Vector2.one;
            glowRect.offsetMin = new Vector2(-7f, -7f);
            glowRect.offsetMax = new Vector2(7f, 7f);
            selectionGlow.sprite = _panelSprite;
            selectionGlow.type = Image.Type.Sliced;
            selectionGlow.color = new Color(1f, 0.88f, 0.34f, 0f);
            selectionGlow.raycastTarget = false;

            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => SelectAmmo(index));

            Image icon = new GameObject(
                "Icon",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)).GetComponent<Image>();
            RectTransform iconRect = icon.rectTransform;
            iconRect.SetParent(rect, false);
            iconRect.anchorMin = new Vector2(0.5f, 0.71f);
            iconRect.anchorMax = new Vector2(0.5f, 0.71f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(72f, 72f);
            icon.color = Color.white;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.sprite = GetAmmoIconSprite(index);

            Image cooldownTrack = CreateCooldownRingImage(
                "Cooldown Ring Track",
                rect,
                new Color(0.18f, 0.54f, 0.76f, 0.18f),
                new Vector2(88f, 88f));
            Image cooldownRing = CreateCooldownRingImage(
                "Cooldown Ring",
                cooldownTrack.rectTransform,
                HudSkyBlueColor,
                Vector2.zero);
            RectTransform cooldownRingRect = cooldownRing.rectTransform;
            cooldownRingRect.anchorMin = Vector2.zero;
            cooldownRingRect.anchorMax = Vector2.one;
            cooldownRingRect.offsetMin = Vector2.zero;
            cooldownRingRect.offsetMax = Vector2.zero;
            cooldownRing.type = Image.Type.Filled;
            cooldownRing.fillMethod = Image.FillMethod.Radial360;
            cooldownRing.fillOrigin = (int)Image.Origin360.Top;
            cooldownRing.fillClockwise = true;
            cooldownRing.fillAmount = 0f;
            cooldownTrack.gameObject.SetActive(false);

            Text label = CreateText(
                "Label",
                rect,
                AmmoNames[index],
                18f,
                TextAnchor.MiddleCenter);
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.color = HudInkColor;
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0.05f, 0.2f);
            labelRect.anchorMax = new Vector2(0.95f, 0.4f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            Text cooldownText = CreateText(
                "Cooldown Time",
                rect,
                "",
                15f,
                TextAnchor.MiddleCenter);
            cooldownText.color = HudMutedColor;
            RectTransform cooldownTextRect =
                cooldownText.rectTransform;
            cooldownTextRect.anchorMin = new Vector2(0.08f, 0.02f);
            cooldownTextRect.anchorMax = new Vector2(0.92f, 0.2f);
            cooldownTextRect.offsetMin = Vector2.zero;
            cooldownTextRect.offsetMax = Vector2.zero;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.8f, 0.2f, 0f);
            outline.effectDistance = new Vector2(2f, -2f);

            _ammoButtons.Add(button);
            _ammoButtonImages.Add(image);
            _ammoButtonIcons.Add(icon);
            _ammoButtonGlows.Add(selectionGlow);
            _ammoButtonOutlines.Add(outline);
            _ammoButtonLabels.Add(label);
            _ammoCooldownRings.Add(cooldownRing);
            _ammoCooldownTexts.Add(cooldownText);
        }

        void CreateSkillReleasePopup(RectTransform canvasRect)
        {
            _skillPopupRoot = new GameObject(
                "Skill Release Popup",
                typeof(RectTransform),
                typeof(CanvasGroup)).GetComponent<RectTransform>();
            _skillPopupRoot.SetParent(canvasRect, false);
            _skillPopupRoot.anchorMin = new Vector2(0.5f, 0f);
            _skillPopupRoot.anchorMax = new Vector2(0.5f, 0f);
            _skillPopupRoot.pivot = new Vector2(0.5f, 0f);
            _skillPopupRoot.anchoredPosition = new Vector2(0f, 238f);
            _skillPopupRoot.sizeDelta = new Vector2(760f, 100f);
            _skillPopupRoot.gameObject.SetActive(false);

            _skillPopupGroup =
                _skillPopupRoot.GetComponent<CanvasGroup>();
            _skillPopupGroup.alpha = 0f;
            _skillPopupGroup.blocksRaycasts = false;
            _skillPopupGroup.interactable = false;

            _skillPopupGlow = CreateImage(
                "Skill Glow",
                _skillPopupRoot,
                Vector2.zero,
                new Vector2(720f, 92f),
                new Color(0.3f, 0.82f, 1f, 0.2f));
            RectTransform glowRect = _skillPopupGlow.rectTransform;
            glowRect.anchorMin = new Vector2(0.5f, 0.5f);
            glowRect.anchorMax = new Vector2(0.5f, 0.5f);
            glowRect.pivot = new Vector2(0.5f, 0.5f);
            glowRect.anchoredPosition = Vector2.zero;
            _skillPopupGlow.sprite = _panelSprite;
            _skillPopupGlow.type = Image.Type.Sliced;
            _skillPopupGlow.raycastTarget = false;

            _skillPopupText = CreateText(
                "Skill Name",
                _skillPopupRoot,
                "",
                58f,
                TextAnchor.MiddleCenter);
            RectTransform textRect = _skillPopupText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            _skillPopupText.color = HudSkyBlueColor;
            Outline outline =
                _skillPopupText.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(4f, -4f);
            Shadow shadow =
                _skillPopupText.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.02f, 0.14f, 0.24f, 0.82f);
            shadow.effectDistance = new Vector2(5f, -5f);
        }

        void CreateDamageBorder(RectTransform canvasRect)
        {
            _damageBorderRoot = new GameObject(
                "Damage Border",
                typeof(RectTransform),
                typeof(CanvasGroup)).GetComponent<RectTransform>();
            _damageBorderRoot.SetParent(canvasRect, false);
            _damageBorderRoot.anchorMin = Vector2.zero;
            _damageBorderRoot.anchorMax = Vector2.one;
            _damageBorderRoot.offsetMin = Vector2.zero;
            _damageBorderRoot.offsetMax = Vector2.zero;
            _damageBorderRoot.SetAsLastSibling();
            _damageBorderGroup =
                _damageBorderRoot.GetComponent<CanvasGroup>();
            _damageBorderGroup.alpha = 0f;
            _damageBorderGroup.blocksRaycasts = false;
            _damageBorderGroup.interactable = false;

            Image tint = CreateImage(
                "Damage Tint",
                _damageBorderRoot,
                Vector2.zero,
                Vector2.zero,
                new Color(0.45f, 0.08f, 0.72f, 0.055f));
            RectTransform tintRect = tint.rectTransform;
            tintRect.anchorMin = Vector2.zero;
            tintRect.anchorMax = Vector2.one;
            tintRect.offsetMin = Vector2.zero;
            tintRect.offsetMax = Vector2.zero;
            tint.sprite = _healthBarSprite;
            tint.type = Image.Type.Simple;
            tint.raycastTarget = false;

            Color edgeColor = new Color(0.66f, 0.16f, 1f, 0.92f);
            CreateDamageBorderEdge(
                "Top",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, 26f),
                edgeColor);
            CreateDamageBorderEdge(
                "Bottom",
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 26f),
                edgeColor);
            CreateDamageBorderEdge(
                "Left",
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(0f, 0.5f),
                new Vector2(26f, 0f),
                edgeColor);
            CreateDamageBorderEdge(
                "Right",
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0.5f),
                new Vector2(26f, 0f),
                edgeColor);
        }

        void CreateDamageBorderEdge(
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 size,
            Color color)
        {
            Image edge = CreateImage(
                name,
                _damageBorderRoot,
                Vector2.zero,
                size,
                color);
            RectTransform rect = edge.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            edge.sprite = _healthBarSprite;
            edge.type = Image.Type.Simple;
            edge.raycastTarget = false;
        }

        Image CreateCooldownRingImage(
            string name,
            Transform parent,
            Color color,
            Vector2 size)
        {
            var go = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.71f);
            rect.anchorMax = new Vector2(0.5f, 0.71f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            Image image = go.GetComponent<Image>();
            image.sprite = _cooldownRingSprite;
            image.color = color;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        void SelectAmmo(int index)
        {
            if (index < 0 || index >= AmmoNames.Length)
                return;

            int previousIndex = _selectedAmmo;
            _selectedAmmo = index;
            for (int i = 0; i < _ammoButtonImages.Count; i++)
            {
                bool selected = i == index;
                _ammoButtonImages[i].color = selected
                    ? AmmoSelectedColor
                    : AmmoIdleColor;
                _ammoButtonOutlines[i].effectColor = selected
                    ? new Color(0.93f, 0.62f, 0.1f, 1f)
                    : new Color(0.26f, 0.62f, 0.86f, 0.35f);
                _ammoButtonLabels[i].color = HudInkColor;
            }

            SetHint(
                $"已选：{index + 1}. {AmmoNames[index]} · 点击场景召唤炮塔",
                HudInkColor);
            AnimateAmmoSelection(previousIndex, index);
        }

        void AnimateAmmoSelection(int previousIndex, int selectedIndex)
        {
            if (previousIndex != selectedIndex &&
                previousIndex >= 0 &&
                previousIndex < _ammoButtons.Count)
            {
                StartAmmoButtonRoutine(previousIndex, false);
            }

            StartAmmoButtonRoutine(selectedIndex, true);
        }

        void StartAmmoButtonRoutine(int index, bool selected)
        {
            if (index < 0 || index >= _ammoButtons.Count)
                return;
            if (_ammoPulseRoutines[index] != null)
                StopCoroutine(_ammoPulseRoutines[index]);

            RectTransform buttonRect = _ammoButtons[index].transform as RectTransform;
            RectTransform iconRect = _ammoButtonIcons[index].rectTransform;
            RectTransform glowRect = _ammoButtonGlows[index].rectTransform;
            if (buttonRect != null)
                buttonRect.localScale = Vector3.one;
            iconRect.localScale = Vector3.one;
            iconRect.localRotation = Quaternion.identity;
            glowRect.localScale = Vector3.one;

            _ammoPulseRoutines[index] = StartCoroutine(
                AmmoButtonSwitchRoutine(index, selected));
        }

        IEnumerator AmmoButtonSwitchRoutine(int index, bool selected)
        {
            RectTransform buttonRect = _ammoButtons[index].transform as RectTransform;
            RectTransform iconRect = _ammoButtonIcons[index].rectTransform;
            RectTransform glowRect = _ammoButtonGlows[index].rectTransform;
            Image glow = _ammoButtonGlows[index];
            float duration = selected ? 0.46f : 0.22f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float wave = Mathf.Sin(t * Mathf.PI);
                float settle = 1f - t;
                if (selected)
                {
                    buttonRect.localScale = Vector3.one *
                        (1f + wave * 0.14f + Mathf.Sin(t * Mathf.PI * 2f) * 0.025f);
                    iconRect.localScale = Vector3.one *
                        (1f + wave * 0.38f);
                    iconRect.localRotation = Quaternion.Euler(
                        0f,
                        0f,
                        Mathf.Sin(t * Mathf.PI * 2f) * 7f);
                    glowRect.localScale = Vector3.one * (1f + wave * 0.09f);
                    glow.color = new Color(
                        1f,
                        0.88f,
                        0.34f,
                        Mathf.Lerp(0.16f, 0.72f, wave));
                }
                else
                {
                    buttonRect.localScale = Vector3.one *
                        (1f - wave * 0.065f);
                    iconRect.localScale = Vector3.one *
                        (1f - wave * 0.1f);
                    glowRect.localScale = Vector3.one *
                        (1f - wave * 0.08f);
                    glow.color = new Color(
                        1f,
                        0.88f,
                        0.34f,
                        0.16f * settle * settle);
                }
                yield return null;
            }

            buttonRect.localScale = Vector3.one;
            iconRect.localScale = Vector3.one;
            iconRect.localRotation = Quaternion.identity;
            glowRect.localScale = Vector3.one;
            bool stillSelected = index == _selectedAmmo;
            glow.color = new Color(
                1f,
                0.88f,
                0.34f,
                stillSelected ? 0.16f : 0f);
            _ammoPulseRoutines[index] = null;
        }

        void RefreshAmmoCooldowns()
        {
            for (int i = 0; i < _ammoButtons.Count; i++)
            {
                float remaining = Mathf.Max(0f, _toolReadyTimes[i] - Time.time);
                bool cooling = remaining > 0.01f;
                _ammoButtons[i].interactable = !cooling;
                _ammoButtonLabels[i].text = AmmoNames[i];
                _ammoCooldownRings[i].transform.parent.gameObject
                    .SetActive(cooling);
                _ammoCooldownRings[i].fillAmount = cooling
                    ? Mathf.Clamp01(
                        remaining / Mathf.Max(0.01f, turretPlacementCooldown))
                    : 0f;
                _ammoCooldownTexts[i].text = cooling
                    ? $"冷却 {remaining:0.0}s"
                    : string.Empty;
                _ammoButtonIcons[i].color = cooling
                    ? new Color(0.42f, 0.5f, 0.56f, 0.55f)
                    : Color.white;
                if (_ammoPulseRoutines[i] == null)
                {
                    _ammoButtonGlows[i].color = i == _selectedAmmo && !cooling
                        ? new Color(1f, 0.88f, 0.34f, 0.16f)
                        : new Color(1f, 0.88f, 0.34f, 0f);
                }

                if (cooling)
                {
                    _ammoButtonImages[i].color =
                        AmmoCoolingColor;
                    _ammoButtonOutlines[i].effectColor =
                        new Color(0.45f, 0.55f, 0.66f, 0.28f);
                    _ammoButtonLabels[i].color =
                        new Color(0.43f, 0.5f, 0.57f);
                }
                else if (i == _selectedAmmo)
                {
                    _ammoButtonImages[i].color =
                        AmmoSelectedColor;
                    _ammoButtonOutlines[i].effectColor =
                        new Color(0.93f, 0.62f, 0.1f, 1f);
                    _ammoButtonLabels[i].color =
                        HudInkColor;
                }
                else
                {
                    _ammoButtonImages[i].color =
                        AmmoIdleColor;
                    _ammoButtonOutlines[i].effectColor =
                        new Color(0.26f, 0.62f, 0.86f, 0.35f);
                    _ammoButtonLabels[i].color = HudInkColor;
                }
            }
        }

        void ShowSkillRelease(string skillName)
        {
            if (_skillPopupText == null || _skillPopupRoot == null)
                return;

            if (_skillPopupRoutine != null)
                StopCoroutine(_skillPopupRoutine);
            _skillPopupText.text = skillName;
            _skillPopupRoot.gameObject.SetActive(true);
            _skillPopupRoot.anchoredPosition = new Vector2(0f, 224f);
            _skillPopupRoot.localScale = Vector3.one * 0.42f;
            _skillPopupGroup.alpha = 0f;
            _skillPopupGlow.color = new Color(0.3f, 0.82f, 1f, 0.04f);
            _skillPopupRoutine = StartCoroutine(
                SkillReleasePopupRoutine());
        }

        IEnumerator SkillReleasePopupRoutine()
        {
            const float enterDuration = 0.2f;
            float elapsed = 0f;
            while (elapsed < enterDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / enterDuration);
                float eased = t * t * (3f - 2f * t);
                _skillPopupRoot.anchoredPosition = new Vector2(
                    0f,
                    Mathf.LerpUnclamped(224f, 248f, eased));
                _skillPopupRoot.localScale =
                    Vector3.one * Mathf.LerpUnclamped(0.42f, 1.3f, eased);
                _skillPopupGlow.color = new Color(
                    0.3f,
                    0.82f,
                    1f,
                    Mathf.LerpUnclamped(0.04f, 0.3f, eased));
                _skillPopupGroup.alpha = eased;
                yield return null;
            }

            const float settleDuration = 0.1f;
            elapsed = 0f;
            while (elapsed < settleDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / settleDuration);
                t = t * t * (3f - 2f * t);
                _skillPopupRoot.localScale =
                    Vector3.one * Mathf.LerpUnclamped(1.3f, 0.94f, t);
                yield return null;
            }

            const float pulseDuration = 0.16f;
            elapsed = 0f;
            while (elapsed < pulseDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / pulseDuration);
                float pulse = Mathf.Sin(t * Mathf.PI);
                _skillPopupRoot.localScale =
                    Vector3.one * (0.94f + pulse * 0.16f);
                _skillPopupGlow.color = new Color(
                    0.3f,
                    0.82f,
                    1f,
                    Mathf.LerpUnclamped(0.3f, 0.16f, t));
                yield return null;
            }

            _skillPopupRoot.anchoredPosition = new Vector2(0f, 248f);
            _skillPopupRoot.localScale = Vector3.one;
            yield return new WaitForSecondsRealtime(0.72f);

            const float exitDuration = 0.4f;
            elapsed = 0f;
            while (elapsed < exitDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / exitDuration);
                _skillPopupRoot.anchoredPosition = new Vector2(
                    0f,
                    Mathf.LerpUnclamped(248f, 282f, t));
                _skillPopupRoot.localScale =
                    Vector3.one * Mathf.LerpUnclamped(1f, 1.24f, t);
                _skillPopupGlow.color = new Color(
                    0.3f,
                    0.82f,
                    1f,
                    Mathf.LerpUnclamped(0.16f, 0f, t));
                _skillPopupGroup.alpha = 1f - t;
                yield return null;
            }

            _skillPopupRoot.gameObject.SetActive(false);
            _skillPopupRoutine = null;
        }

        void FlashDamageBorder()
        {
            if (_damageBorderRoot == null || _damageBorderGroup == null)
                return;

            if (_damageBorderRoutine != null)
                StopCoroutine(_damageBorderRoutine);
            _damageBorderRoot.gameObject.SetActive(true);
            _damageBorderGroup.alpha = 0f;
            _damageBorderRoutine = StartCoroutine(
                DamageBorderRoutine());
        }

        IEnumerator DamageBorderRoutine()
        {
            const float enterDuration = 0.055f;
            float elapsed = 0f;
            while (elapsed < enterDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / enterDuration);
                _damageBorderGroup.alpha = Mathf.LerpUnclamped(0f, 0.96f, t);
                yield return null;
            }

            _damageBorderGroup.alpha = 0.96f;
            yield return new WaitForSecondsRealtime(0.07f);

            const float exitDuration = 0.34f;
            elapsed = 0f;
            while (elapsed < exitDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / exitDuration);
                _damageBorderGroup.alpha =
                    Mathf.LerpUnclamped(0.96f, 0f, t * t);
                yield return null;
            }

            _damageBorderGroup.alpha = 0f;
            _damageBorderRoutine = null;
        }

        void SetHint(string text, Color color)
        {
            if (_notificationText == null || _notificationRoot == null)
                return;

            if (_notificationRoutine != null)
                StopCoroutine(_notificationRoutine);
            _notificationText.text = text;
            _notificationText.color = Color.black;
            _notificationRoot.gameObject.SetActive(true);
            _notificationRoutine = StartCoroutine(
                ShowLeftNotificationRoutine(text));
        }

        IEnumerator ShowLeftNotificationRoutine(string text)
        {
            Vector2 target = new Vector2(36f, 0f);
            Vector2 hiddenLeft = target + new Vector2(-42f, 0f);
            _notificationRoot.anchoredPosition = hiddenLeft;
            _notificationGroup.alpha = 0f;

            const float enterDuration = 0.22f;
            float elapsed = 0f;
            while (elapsed < enterDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / enterDuration);
                t = t * t * (3f - 2f * t);
                _notificationRoot.anchoredPosition =
                    Vector2.Lerp(hiddenLeft, target, t);
                _notificationGroup.alpha = t;
                yield return null;
            }

            _notificationRoot.anchoredPosition = target;
            _notificationGroup.alpha = 1f;
            float hold = Mathf.Clamp(
                1.3f + (text != null ? text.Length : 0) * 0.025f,
                1.5f,
                3.4f);
            yield return new WaitForSecondsRealtime(hold);

            const float exitDuration = 0.28f;
            elapsed = 0f;
            Vector2 exit = target + new Vector2(-18f, 0f);
            while (elapsed < exitDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / exitDuration);
                _notificationRoot.anchoredPosition =
                    Vector2.Lerp(target, exit, t);
                _notificationGroup.alpha = 1f - t;
                yield return null;
            }

            _notificationRoot.gameObject.SetActive(false);
            _notificationRoutine = null;
        }

        void SpawnFx(
            GameObject prefab,
            Vector3 position,
            float maximumLifetime = -1f,
            float scale = -1f,
            bool preservePrefabRotation = false)
        {
            if (prefab == null)
                return;

            Quaternion rotation = preservePrefabRotation
                ? prefab.transform.rotation
                : Quaternion.identity;
            GameObject instance = Instantiate(prefab, position, rotation);
            instance.transform.localScale =
                Vector3.one * (scale > 0f ? scale : ParticleEffectScale);
            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            float lifetime = 1.5f;
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem.MainModule main = systems[i].main;
                float duration = main.duration + main.startLifetime.constantMax;
                if (duration > lifetime)
                    lifetime = duration;
                systems[i].Play(true);
            }
            if (maximumLifetime > 0f)
                lifetime = Mathf.Min(lifetime, maximumLifetime);
            Destroy(instance, lifetime + 0.25f);
        }

        static GameObject GetPrefab(GameObject[] prefabs, int index)
        {
            if (prefabs == null || index < 0 || index >= prefabs.Length)
                return null;
            return prefabs[index];
        }

        static Texture2D GetTexture(Texture2D[] textures, int index)
        {
            if (textures == null || index < 0 || index >= textures.Length)
                return null;
            return textures[index];
        }

        Sprite GetAmmoIconSprite(int index)
        {
            if (index < 0 || index >= _ammoIconSprites.Length)
                return null;
            if (_ammoIconSprites[index] != null)
                return _ammoIconSprites[index];

            Texture2D texture = GetTexture(ammoIconTextures, index);
            if (texture == null)
                return null;

            _ammoIconSprites[index] = CreateSpriteFromTexture(
                texture,
                $"Anti Fraud Tool Icon {index + 1}");
            return _ammoIconSprites[index];
        }

        static Sprite CreateSpriteFromTexture(Texture2D texture, string name)
        {
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            sprite.name = name;
            return sprite;
        }

        void EnsureEventSystem()
        {
            EventSystem current = EventSystem.current;
            if (current != null && current.isActiveAndEnabled)
                return;

            EventSystem[] systems = FindObjectsOfType<EventSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                EventSystem system = systems[i];
                if (system == null || !system.isActiveAndEnabled)
                    continue;
                return;
            }

            var eventSystemGo = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule));
            MoveToOwningScene(eventSystemGo);
        }

        RectTransform CreatePanel(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size,
            Color color)
        {
            var go = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.sprite = _panelSprite;
            image.type = Image.Type.Sliced;
            return rect;
        }

        static Image CreateImage(
            string name,
            Transform parent,
            Vector2 anchoredPosition,
            Vector2 size,
            Color color)
        {
            var go = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            Image image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        Text CreateText(
            string name,
            Transform parent,
            string text,
            float fontSize,
            TextAnchor alignment)
        {
            var go = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var label = go.AddComponent<Text>();
            label.font = hudFont;
            label.text = text;
            label.fontSize = Mathf.RoundToInt(fontSize);
            label.fontStyle = FontStyle.Bold;
            label.alignment = alignment;
            label.color = Color.white;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        static Font CreateChineseFont()
        {
            string[] preferred =
            {
                "Microsoft YaHei",
                "Microsoft YaHei UI",
                "SimHei",
                "Arial Unicode MS",
                "Arial",
            };
            string[] installed = Font.GetOSInstalledFontNames();
            for (int i = 0; i < preferred.Length; i++)
            {
                for (int j = 0; j < installed.Length; j++)
                {
                    if (installed[j] == preferred[i])
                        return Font.CreateDynamicFontFromOSFont(preferred[i], 32);
                }
            }

            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        Sprite CreateRoundedSprite()
        {
            const int size = 64;
            const float radius = 17f;
            _panelTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            _panelTexture.name = "Defense Rounded Panel";
            _panelTexture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nearestX = Mathf.Clamp(x, radius, size - radius);
                    float nearestY = Mathf.Clamp(y, radius, size - radius);
                    float dx = x - nearestX;
                    float dy = y - nearestY;
                    bool inside = dx * dx + dy * dy <= radius * radius;
                    pixels[y * size + x] = inside
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(255, 255, 255, 0);
                }
            }

            _panelTexture.SetPixels32(pixels);
            _panelTexture.Apply(false, true);
            return Sprite.Create(
                _panelTexture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(18f, 18f, 18f, 18f));
        }

        Sprite CreateHealthBarSprite()
        {
            const int size = 4;
            _healthBarTexture =
                new Texture2D(size, size, TextureFormat.RGBA32, false);
            _healthBarTexture.name = "Enemy Health Bar";
            _healthBarTexture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, 255);

            _healthBarTexture.SetPixels32(pixels);
            _healthBarTexture.Apply(false, true);
            return Sprite.Create(
                _healthBarTexture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                Vector4.zero);
        }

        Sprite CreateCooldownRingSprite()
        {
            const int size = 64;
            const float outerRadius = 29f;
            const float innerRadius = 22f;
            _cooldownRingTexture =
                new Texture2D(size, size, TextureFormat.RGBA32, false);
            _cooldownRingTexture.name = "Defense Cooldown Ring";
            _cooldownRingTexture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - (size - 1f) * 0.5f;
                    float dy = y - (size - 1f) * 0.5f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float outer = Mathf.Clamp01(
                        outerRadius + 0.75f - distance);
                    float inner = Mathf.Clamp01(
                        distance - innerRadius + 0.75f);
                    byte alpha = (byte)(Mathf.Min(outer, inner) * 255f);
                    pixels[y * size + x] =
                        new Color32(255, 255, 255, alpha);
                }
            }

            _cooldownRingTexture.SetPixels32(pixels);
            _cooldownRingTexture.Apply(false, true);
            return Sprite.Create(
                _cooldownRingTexture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                Vector4.zero);
        }

        void OnDestroy()
        {
            if (_coinSprite != null)
                Destroy(_coinSprite);
            if (_panelSprite != null)
                Destroy(_panelSprite);
            if (_panelTexture != null)
                Destroy(_panelTexture);
            if (_healthBarSprite != null)
                Destroy(_healthBarSprite);
            if (_healthBarTexture != null)
                Destroy(_healthBarTexture);
            if (_cooldownRingSprite != null)
                Destroy(_cooldownRingSprite);
            if (_cooldownRingTexture != null)
                Destroy(_cooldownRingTexture);
            for (int i = 0; i < _ammoIconSprites.Length; i++)
            {
                if (_ammoIconSprites[i] != null)
                    Destroy(_ammoIconSprites[i]);
            }
        }
    }
}
