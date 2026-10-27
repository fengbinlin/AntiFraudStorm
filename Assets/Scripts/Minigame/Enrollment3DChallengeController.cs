using System;
using System.Collections;
using System.Collections.Generic;
using Animancer;
using AntiFraudStorm.Player;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace AntiFraudStorm.Minigame
{
    public class Enrollment3DChallengeController : MonoBehaviour
    {
        const float MoveSpeed = 6.2f;
        const float JumpSpeed = 11.2f;
        const float Gravity = -25f;
        const float EnemyHitRadius = 0.85f;
        const float FinishX = 30f;

        Action _onFinished;
        GameObject _heroPrefab;
        GameObject _enemyPrefab;
        GameObject _backgroundPrefab;
        Camera _previousCamera;
        Camera _camera;
        CharacterController _playerController;
        AnimancerComponent _animancer;
        PlayerAnimancerLocomotion _locomotion;
        Transform _playerRoot;
        Transform _worldRoot;
        Vector3 _spawnPosition;
        Vector3 _cameraVelocity;
        float _cameraLookAhead;
        float _cameraFocusY = 1.6f;
        float _verticalVelocity;
        float _fadeAlpha;
        float _hitFlashAlpha;
        float _cameraShakeRemaining;
        float _dustTimer;
        bool _wasGrounded;
        bool _finished;
        ParticleSystem _runDust;
        Transform _finishFlag;
        readonly List<PatrolEnemy> _enemies = new List<PatrolEnemy>();

        public void Initialize(
            GameObject heroPrefab,
            GameObject enemyPrefab,
            GameObject backgroundPrefab,
            Action onFinished)
        {
            _heroPrefab = heroPrefab;
            _enemyPrefab = enemyPrefab;
            _backgroundPrefab = backgroundPrefab;
            _onFinished = onFinished;
            _previousCamera = Camera.main;

            BuildWorld();
        }

        void BuildWorld()
        {
            _worldRoot = new GameObject("Enrollment3DWorld").transform;
            if (_backgroundPrefab != null)
            {
                Material backgroundMaterial = CreateBackgroundMaterial();
                for (int i = 0; i < 3; i++)
                {
                    GameObject background = Instantiate(
                        _backgroundPrefab,
                        new Vector3(-48f + i * 48f, -1f, 48f),
                        Quaternion.identity,
                        _worldRoot);
                    background.name = $"ANIME_School_BackgroundCity_{i + 1}";
                    ApplyBackgroundMaterial(background, backgroundMaterial);
                }
            }

            var cameraGo = new GameObject("Enrollment3DCamera");
            _camera = cameraGo.AddComponent<Camera>();
            var cameraData = cameraGo.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null)
                cameraData = cameraGo.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderType = CameraRenderType.Base;
            cameraData.renderPostProcessing = false;
            _camera.enabled = true;
            _camera.depth = 10f;
            _camera.cullingMask = ~0;
            _camera.fieldOfView = 42f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.68f, 0.84f, 0.97f);
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 120f;
            _camera.transform.position = new Vector3(-10f, 2.5f, -11f);
            _camera.transform.LookAt(new Vector3(-10f, 2.5f, 0f));

            var lightGo = new GameObject("Enrollment3DKeyLight");
            lightGo.transform.SetParent(_worldRoot, false);
            lightGo.transform.rotation = Quaternion.Euler(42f, -32f, 0f);
            var keyLight = lightGo.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.intensity = 1.15f;
            keyLight.color = new Color(1f, 0.96f, 0.88f);
            keyLight.shadows = LightShadows.None;

            CreatePlatform("Start", new Vector3(-8f, 0f, 0f), new Vector3(8f, 1f, 6f), new Color(0.3f, 0.68f, 0.92f));
            CreatePlatform("Step1", new Vector3(-1.2f, 0.8f, 0f), new Vector3(4f, 1f, 5f), new Color(0.86f, 0.91f, 0.98f));
            CreatePlatform("Step2", new Vector3(4.8f, 1.8f, 1.5f), new Vector3(4f, 1f, 5f), new Color(0.35f, 0.72f, 0.55f));
            CreatePlatform("Step3", new Vector3(11.4f, 0.8f, -1.5f), new Vector3(5f, 1f, 5f), new Color(0.94f, 0.78f, 0.32f));
            CreatePlatform("Step4", new Vector3(18.5f, 1.8f, 0f), new Vector3(4.2f, 1f, 5f), new Color(0.35f, 0.72f, 0.55f));
            CreatePlatform("Finish", new Vector3(26.2f, 2.8f, 0f), new Vector3(8f, 1f, 6f), new Color(0.24f, 0.78f, 0.44f));

            CreatePlayer();
            CreateEnemy(new Vector3(11.4f, 1.3f, 0f), Vector3.right, 3f, 1.7f);
            CreateEnemy(new Vector3(18.5f, 2.3f, 0f), Vector3.right, 2.8f, 1.9f);
            CreateFinishGate();
            CreateAtmosphere();
        }

        void CreatePlayer()
        {
            _spawnPosition = new Vector3(-10f, 1.5f, 0f);
            var root = new GameObject("CurrentHero");
            root.transform.SetParent(_worldRoot, false);
            root.transform.position = _spawnPosition;
            _playerRoot = root.transform;

            _playerController = root.AddComponent<CharacterController>();
            _playerController.height = 1.75f;
            _playerController.radius = 0.34f;
            _playerController.center = new Vector3(0f, 0.88f, 0f);
            _playerController.minMoveDistance = 0f;

            if (_heroPrefab == null)
                return;

            GameObject visual = Instantiate(_heroPrefab, root.transform);
            visual.name = "CurrentHeroVisual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            StripPlayerGameplay(visual);
            AlignVisualFeet(visual, root.transform.position.y);

            _animancer = visual.GetComponentInChildren<AnimancerComponent>(true);
            _locomotion = visual.GetComponentInChildren<PlayerAnimancerLocomotion>(true);
            if (_locomotion != null && _animancer != null)
                _locomotion.Initialize(_animancer);

            _runDust = CreateRunDust(root.transform);
            _wasGrounded = false;
        }

        void CreateEnemy(Vector3 position, Vector3 patrolAxis, float travelDistance, float speed)
        {
            var root = new GameObject("CurrentEnemy");
            root.transform.SetParent(_worldRoot, false);
            root.transform.position = position;

            if (_enemyPrefab != null)
            {
                GameObject visual = Instantiate(_enemyPrefab, root.transform);
                visual.name = "CurrentEnemyVisual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                StripEnemyGameplay(visual);
                AlignVisualFeet(visual, position.y);
            }
            else
            {
                var body = CreatePrimitive(
                    PrimitiveType.Capsule,
                    Vector3.zero,
                    new Vector3(0.8f, 0.9f, 0.8f),
                    new Color(0.48f, 0.16f, 0.68f),
                    root.transform);
                body.GetComponent<Collider>().enabled = false;
            }

            Animator animator = root.GetComponentInChildren<Animator>(true);
            AnimancerComponent animancer = null;
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.speed = 1f;
                animator.SetFloat("Forward", 1f);
                animator.SetFloat("Turn", 0f);
                animator.SetBool("OnGround", true);

                AnimationClip walkClip = FindWalkClip(animator.runtimeAnimatorController);
                if (walkClip != null)
                {
                    animancer = animator.GetComponent<AnimancerComponent>();
                    if (animancer == null)
                        animancer = animator.gameObject.AddComponent<AnimancerComponent>();
                    animancer.Animator = animator;
                    animator.runtimeAnimatorController = null;
                    animancer.Play(walkClip, 0.15f);
                }
                else
                {
                    animator.Play("Walk", 0, 0f);
                }
            }

            _enemies.Add(new PatrolEnemy
            {
                Transform = root.transform,
                Animator = animator,
                Animancer = animancer,
                Center = position,
                Axis = patrolAxis.normalized,
                Distance = travelDistance,
                Speed = speed,
                Direction = 1f,
            });
        }

        void CreateFinishGate()
        {
            var pole = CreatePrimitive(
                PrimitiveType.Cube,
                new Vector3(FinishX, 5.2f, 0f),
                new Vector3(0.18f, 5f, 0.18f),
                new Color(0.1f, 0.42f, 0.26f),
                _worldRoot);
            pole.GetComponent<Collider>().enabled = false;

            var flag = CreatePrimitive(
                PrimitiveType.Cube,
                new Vector3(FinishX + 0.9f, 6.9f, 0f),
                new Vector3(1.8f, 1f, 0.12f),
                new Color(0.2f, 0.84f, 0.42f),
                _worldRoot);
            flag.GetComponent<Collider>().enabled = false;
            _finishFlag = flag.transform;
        }

        void CreateAtmosphere()
        {
            for (int i = 0; i < 6; i++)
            {
                float x = -34f + i * 14f;
                float y = 9f + (i % 3) * 0.8f;
                for (int c = 0; c < 3; c++)
                {
                    var cloud = CreatePrimitive(
                        PrimitiveType.Sphere,
                        new Vector3(x + c * 2.2f, y + (c % 2) * 0.35f, 24f),
                        new Vector3(5.2f, 1.8f, 1.8f),
                        new Color(0.86f, 0.93f, 0.98f),
                        _worldRoot);
                    cloud.GetComponent<Collider>().enabled = false;
                }
            }

            for (int i = 0; i < 7; i++)
            {
                var guide = CreatePrimitive(
                    PrimitiveType.Cube,
                    new Vector3(-7f + i * 6f, -3.8f, 2f),
                    new Vector3(4.5f, 0.14f, 0.12f),
                    new Color(0.65f, 0.82f, 0.95f),
                    _worldRoot);
                guide.GetComponent<Collider>().enabled = false;
            }
        }

        ParticleSystem CreateRunDust(Transform parent)
        {
            var dustGo = new GameObject("RunDust");
            dustGo.transform.SetParent(parent, false);
            dustGo.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            var particles = dustGo.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.startLifetime = 0.38f;
            main.startSpeed = 1.2f;
            main.startSize = 0.18f;
            main.startColor = new Color(0.75f, 0.85f, 0.95f, 0.75f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;
            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.22f;
            var renderer = dustGo.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = CreateParticleMaterial(new Color(0.75f, 0.85f, 0.95f));
            return particles;
        }

        static Material CreateParticleMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                return null;
            var material = new Material(shader);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            return material;
        }

        void EmitMovementDust()
        {
            _runDust?.Emit(2);
        }

        void TriggerImpactFeedback(Vector3 position)
        {
            _hitFlashAlpha = 0.3f;
            _cameraShakeRemaining = 0.26f;
            SpawnBurst(
                position + Vector3.up * 0.8f,
                new Color(0.9f, 0.32f, 0.42f),
                24,
                4.5f);
        }

        void SpawnBurst(Vector3 position, Color color, int count, float speed)
        {
            var go = new GameObject("ImpactBurst");
            go.transform.position = position;
            var particles = go.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = false;
            main.startLifetime = 0.75f;
            main.startSpeed = speed;
            main.startSize = 0.22f;
            main.startColor = color;
            main.gravityModifier = 0.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = CreateParticleMaterial(color);
            particles.Emit(count);
            Destroy(go, 2f);
        }

        void SpawnFinishCelebration()
        {
            var go = new GameObject("FinishCelebration");
            go.transform.position = _playerRoot.position + new Vector3(0f, 1.4f, 0f);
            var particles = go.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = false;
            main.startLifetime = 1.8f;
            main.startSpeed = 6.5f;
            main.startSize = 0.28f;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.2f, 0.9f, 0.55f),
                new Color(1f, 0.82f, 0.25f));
            main.gravityModifier = 0.8f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(3f, 1f, 2f);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = CreateParticleMaterial(Color.white);
            particles.Emit(90);
            Destroy(go, 3f);
        }

        static Material CreateBackgroundMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                return null;

            var material = new Material(shader)
            {
                name = "MutedBackgroundBuildings",
            };
            var color = new Color(0.24f, 0.29f, 0.35f, 1f);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            return material;
        }

        static void ApplyBackgroundMaterial(GameObject background, Material material)
        {
            if (material == null)
                return;
            var renderers = background.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].sharedMaterial = material;
        }

        GameObject CreatePlatform(string name, Vector3 position, Vector3 scale, Color color)
        {
            var platform = CreatePrimitive(
                PrimitiveType.Cube,
                position,
                scale,
                color,
                _worldRoot);
            platform.name = "Platform_" + name;

            var surface = CreatePrimitive(
                PrimitiveType.Cube,
                position + new Vector3(0f, scale.y * 0.52f, 0f),
                new Vector3(scale.x * 0.98f, 0.1f, scale.z * 0.96f),
                Color.Lerp(color, Color.white, 0.4f),
                _worldRoot);
            surface.GetComponent<Collider>().enabled = false;

            var trim = CreatePrimitive(
                PrimitiveType.Cube,
                position + new Vector3(0f, -scale.y * 0.15f, -scale.z * 0.51f),
                new Vector3(scale.x * 0.92f, 0.08f, 0.08f),
                Color.Lerp(color, Color.black, 0.25f),
                _worldRoot);
            trim.GetComponent<Collider>().enabled = false;

            return platform;
        }

        GameObject CreatePrimitive(
            PrimitiveType type,
            Vector3 position,
            Vector3 scale,
            Color color,
            Transform parent)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = scale;

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                    shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    shader = Shader.Find("Standard");
                if (shader != null)
                {
                    var material = new Material(shader);
                    if (material.HasProperty("_BaseColor"))
                        material.SetColor("_BaseColor", color);
                    if (material.HasProperty("_Color"))
                        material.SetColor("_Color", color);
                    renderer.material = material;
                }
            }
            return go;
        }

        void Update()
        {
            if (_finished || _playerController == null)
                return;

            float horizontal = 0f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) horizontal += 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) horizontal -= 1f;
            Vector3 move = new Vector3(horizontal, 0f, 0f);

            bool jumpPressed = Input.GetKeyDown(KeyCode.Space);
            if (_playerController.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;
            if (_playerController.isGrounded && jumpPressed)
                _verticalVelocity = JumpSpeed;

            _verticalVelocity += Gravity * Time.deltaTime;
            Vector3 motion = move * MoveSpeed + Vector3.up * _verticalVelocity;
            _playerController.Move(motion * Time.deltaTime);

            bool grounded = _playerController.isGrounded;
            if (grounded && !_wasGrounded)
                _runDust?.Emit(12);
            if (!grounded && _wasGrounded)
                _runDust?.Emit(7);
            _wasGrounded = grounded;

            if (grounded && move.sqrMagnitude > 0.01f)
            {
                _dustTimer -= Time.deltaTime;
                if (_dustTimer <= 0f)
                {
                    EmitMovementDust();
                    _dustTimer = 0.09f;
                }
            }
            else
            {
                _dustTimer = 0f;
            }

            if (move.sqrMagnitude > 0.01f)
            {
                _playerRoot.rotation = Quaternion.LookRotation(move, Vector3.up);
            }

            _locomotion?.Tick(move.sqrMagnitude > 0.01f, false, _playerController.isGrounded, jumpPressed);
            UpdateEnemies();

            if (_playerRoot.position.y < -8f)
                ResetPlayer();

            if (_playerRoot.position.x >= FinishX)
                CompleteChallenge();

            if (_hitFlashAlpha > 0f)
                _hitFlashAlpha = Mathf.Max(0f, _hitFlashAlpha - Time.deltaTime * 1.8f);
            if (_finishFlag != null)
                _finishFlag.Rotate(0f, 75f * Time.deltaTime, 0f, Space.World);

            float desiredLookAhead = horizontal * 2.2f;
            _cameraLookAhead = Mathf.Lerp(
                _cameraLookAhead,
                desiredLookAhead,
                1f - Mathf.Exp(-4f * Time.deltaTime));

            float desiredFocusY = Mathf.Clamp(_playerRoot.position.y + 1.15f, 1.8f, 5.2f);
            _cameraFocusY = Mathf.Lerp(
                _cameraFocusY,
                desiredFocusY,
                1f - Mathf.Exp(-3.5f * Time.deltaTime));

            float focusX = Mathf.Clamp(_playerRoot.position.x + _cameraLookAhead, -10f, 28f);
            Vector3 focus = new Vector3(focusX, _cameraFocusY, 0f);
            Vector3 targetCamera = focus + new Vector3(0f, 0.9f, -11f);
            _camera.transform.position = Vector3.SmoothDamp(
                _camera.transform.position,
                targetCamera,
                ref _cameraVelocity,
                0.22f,
                Mathf.Infinity,
                Time.deltaTime);

            Vector3 lookTarget = focus + new Vector3(0f, 0.9f, 0f);
            Quaternion cameraTargetRotation = Quaternion.LookRotation(
                lookTarget - _camera.transform.position,
                Vector3.up);
            _camera.transform.rotation = Quaternion.Slerp(
                _camera.transform.rotation,
                cameraTargetRotation,
                1f - Mathf.Exp(-9f * Time.deltaTime));

            if (_cameraShakeRemaining > 0f)
            {
                _cameraShakeRemaining = Mathf.Max(0f, _cameraShakeRemaining - Time.deltaTime);
                float strength = _cameraShakeRemaining / 0.26f * 0.16f;
                _camera.transform.position += UnityEngine.Random.insideUnitSphere * strength;
            }
        }

        void UpdateEnemies()
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                PatrolEnemy enemy = _enemies[i];
                float phase = Time.time * enemy.Speed + i * 1.7f;
                float offset = Mathf.PingPong(phase, enemy.Distance) - enemy.Distance * 0.5f;
                Vector3 next = enemy.Center + enemy.Axis * offset;
                Vector3 direction = next - enemy.Transform.position;
                enemy.Transform.position = next;
                if (enemy.Animator != null && enemy.Animancer == null)
                {
                    enemy.Animator.SetFloat("Forward", 1f, 0.08f, Time.deltaTime);
                    enemy.Animator.SetFloat("Turn", 0f);
                    enemy.Animator.SetBool("OnGround", true);
                    AnimatorStateInfo state = enemy.Animator.GetCurrentAnimatorStateInfo(0);
                    if (!enemy.Animator.IsInTransition(0) &&
                        !state.loop &&
                        state.normalizedTime >= 1f)
                    {
                        enemy.Animator.Play("Walk", 0, 0f);
                    }
                }
                if (direction.sqrMagnitude > 0.0001f)
                {
                    enemy.Transform.rotation = Quaternion.LookRotation(
                        new Vector3(direction.x, 0f, direction.z),
                        Vector3.up);
                }

                Vector3 delta = _playerRoot.position - enemy.Transform.position;
                if (Mathf.Abs(delta.x) < EnemyHitRadius &&
                    Mathf.Abs(delta.z) < EnemyHitRadius &&
                    Mathf.Abs(delta.y) < 1.2f)
                {
                    TriggerImpactFeedback(enemy.Transform.position);
                    ResetPlayer();
                    return;
                }
            }
        }

        void ResetPlayer()
        {
            _playerController.enabled = false;
            _playerRoot.position = _spawnPosition;
            _playerController.enabled = true;
            _verticalVelocity = 0f;
            _cameraLookAhead = 0f;
            _cameraVelocity = Vector3.zero;
            _cameraFocusY = _spawnPosition.y + 1.15f;
            _camera.transform.position = new Vector3(
                _spawnPosition.x,
                _cameraFocusY + 0.9f,
                -11f);
            _camera.transform.rotation = Quaternion.LookRotation(
                new Vector3(_spawnPosition.x, _cameraFocusY + 0.9f, 0f) -
                _camera.transform.position,
                Vector3.up);
        }

        void CompleteChallenge()
        {
            if (_finished)
                return;
            _finished = true;
            SpawnFinishCelebration();
            StartCoroutine(FinishRoutine());
        }

        IEnumerator FinishRoutine()
        {
            yield return new WaitForSecondsRealtime(0.5f);
            yield return FocusOnPlayerRoutine(0.52f);
            yield return FadeScreenRoutine(1f, 0.32f);

            if (_camera != null)
                Destroy(_camera.gameObject);
            if (_worldRoot != null)
                Destroy(_worldRoot.gameObject);
            _onFinished?.Invoke();
            yield return null;
            yield return FadeScreenRoutine(0f, 0.35f);
            Destroy(gameObject);
        }

        IEnumerator FocusOnPlayerRoutine(float duration)
        {
            if (_camera == null || _playerRoot == null)
                yield break;

            Vector3 startPosition = _camera.transform.position;
            Quaternion startRotation = _camera.transform.rotation;
            float startFov = _camera.fieldOfView;
            Vector3 focus = _playerRoot.position + new Vector3(0f, 1.15f, 0f);
            Vector3 targetPosition = focus + new Vector3(0f, 0.25f, -5.8f);
            Quaternion targetRotation = Quaternion.LookRotation(
                focus - targetPosition,
                Vector3.up);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t);
                _camera.transform.position = Vector3.LerpUnclamped(
                    startPosition,
                    targetPosition,
                    t);
                _camera.transform.rotation = Quaternion.Slerp(
                    startRotation,
                    targetRotation,
                    t);
                _camera.fieldOfView = Mathf.Lerp(startFov, 30f, t);
                yield return null;
            }
        }

        IEnumerator FadeScreenRoutine(float targetAlpha, float duration)
        {
            float startAlpha = _fadeAlpha;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _fadeAlpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }
            _fadeAlpha = targetAlpha;
        }

        void OnGUI()
        {
            if (_hitFlashAlpha > 0f)
            {
                Color previousFlashColor = GUI.color;
                GUI.color = new Color(1f, 0.08f, 0.12f, _hitFlashAlpha);
                GUI.DrawTexture(
                    new Rect(0f, 0f, Screen.width, Screen.height),
                    Texture2D.whiteTexture);
                GUI.color = previousFlashColor;
            }

            if (_fadeAlpha <= 0f)
                return;

            GUI.depth = -1000;
            Color previousFadeColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, _fadeAlpha);
            GUI.DrawTexture(
                new Rect(0f, 0f, Screen.width, Screen.height),
                Texture2D.whiteTexture);
            GUI.color = previousFadeColor;
        }

        static void StripPlayerGameplay(GameObject actor)
        {
            DestroyAll<PlayerInputReader>(actor);
            DestroyAll<ThirdPersonMotor>(actor);
            DestroyAll<ThirdPersonPlayerController>(actor);
            DestroyAll<PlayerHandheldCamera>(actor);
            DestroyAll<ThirdPersonSpringArm>(actor);
            DestroyAll<CharacterController>(actor);
            DestroyAll<AudioListener>(actor);
            var cameras = actor.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null)
                    cameras[i].enabled = false;
            }
        }

        static void StripEnemyGameplay(GameObject actor)
        {
            var behaviours = actor.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null &&
                    behaviours[i].GetType().Name == "CharacterControl")
                    behaviours[i].enabled = false;
            }
        }

        static AnimationClip FindWalkClip(RuntimeAnimatorController controller)
        {
            if (controller == null)
                return null;

            AnimationClip[] clips = controller.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null && clips[i].name == "Action_Walk")
                    return clips[i];
            }
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null &&
                    clips[i].name.IndexOf("Walk", StringComparison.OrdinalIgnoreCase) >= 0)
                    return clips[i];
            }
            return null;
        }

        static void AlignVisualFeet(GameObject visual, float floorY)
        {
            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            var bounds = new Bounds();
            bool found = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (found)
                visual.transform.position += Vector3.up * (floorY - bounds.min.y);
        }

        static void DestroyAll<T>(GameObject actor) where T : Component
        {
            var components = actor.GetComponentsInChildren<T>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                    Destroy(components[i]);
            }
        }

        class PatrolEnemy
        {
            public Transform Transform;
            public Animator Animator;
            public AnimancerComponent Animancer;
            public Vector3 Center;
            public Vector3 Axis;
            public float Distance;
            public float Speed;
            public float Direction;
        }
    }
}
