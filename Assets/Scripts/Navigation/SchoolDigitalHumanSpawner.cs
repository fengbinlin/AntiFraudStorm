using System.Collections;
using System.Collections.Generic;
using Animancer;
using Layer_lab._3D_Casual_Character;
using Pathfinding;
using Pathfinding.RVO;
using UnityEngine;
using Random = UnityEngine.Random;

namespace AntiFraudStorm.Navigation
{
    /// <summary>
    /// Spawns randomized casual characters and turns them into RichAI patrol agents
    /// on the baked School Floor 1 recast graph.
    /// </summary>
    public class SchoolDigitalHumanSpawner : MonoBehaviour
    {
        const int InitialScamHumanCount = 3;

        [Header("Character Source")]
        [SerializeField] GameObject characterModelPrefab;
        [SerializeField] AnimationClip idleClip;
        [SerializeField] AnimationClip walkClip;
        [SerializeField] AnimationClip agonizeClip;
        [SerializeField] AnimationClip cheerClip;
        [SerializeField] GameObject headParticlePrefab;
        [SerializeField] GameObject cheerHeadParticlePrefab;
        [SerializeField] Font interactionPromptFont;

        [Header("Navigation")]
        [SerializeField] Transform waypointRoot;
        [SerializeField] Transform scamWaypointRoot;
        [SerializeField, Range(1, 20)] int npcCount = 10;
        [SerializeField] float minSpeed = 0.85f;
        [SerializeField] float maxSpeed = 1.35f;
        [SerializeField] float minWaypointWait = 0.35f;
        [SerializeField] float maxWaypointWait = 1.4f;
        [SerializeField, Min(0.01f)] float headParticleScale = 0.1f;

        [Header("Appearance")]
        [SerializeField] float spawnJitterRadius = 1.4f;
        [SerializeField] float minimumSpawnSpacing = 0.75f;

        readonly List<Vector3> _spawnPositions = new List<Vector3>();
        readonly List<Transform> _availableScamPoints = new List<Transform>();
        Transform[] _waypoints;
        int _scamAssignments;

        public static SchoolDigitalHumanSpawner Instance { get; private set; }

        void Awake()
        {
            Instance = this;
            EnsureLocalAvoidance();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        IEnumerator Start()
        {
            yield return WaitForNavigationReady();

            if (waypointRoot == null || waypointRoot.childCount == 0)
            {
                Debug.LogError("[DigitalHumans] Floor1 Navigation Points has no waypoints.", this);
                yield break;
            }

            if (characterModelPrefab == null)
            {
                Debug.LogError("[DigitalHumans] Character model prefab is not assigned.", this);
                yield break;
            }

            _waypoints = GetWaypoints();
            if (_waypoints.Length < 2)
            {
                Debug.LogError("[DigitalHumans] At least two waypoints are required.", this);
                yield break;
            }

            var runtimeRoot = new GameObject("Runtime Digital Humans");
            runtimeRoot.transform.SetParent(transform, false);

            PrepareScamAssignments();
            for (int i = 0; i < npcCount; i++)
                SpawnNpc(i, runtimeRoot.transform, TakeScamPointForNpc(i));
        }

        IEnumerator WaitForNavigationReady()
        {
            float deadline = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (AstarPath.active != null &&
                    AstarPath.active.data != null &&
                    AstarPath.active.data.recastGraph != null &&
                    AstarPath.active.data.recastGraph.isScanned)
                {
                    yield break;
                }

                yield return null;
            }

            Debug.LogError("[DigitalHumans] School Floor 1 navigation graph did not become ready.", this);
        }

        Transform[] GetWaypoints()
        {
            var result = new List<Transform>(waypointRoot.childCount);
            for (int i = 0; i < waypointRoot.childCount; i++)
            {
                Transform child = waypointRoot.GetChild(i);
                if (child != null)
                    result.Add(child);
            }

            return result.ToArray();
        }

        void SpawnNpc(int index, Transform runtimeRoot, Transform scamPoint)
        {
            bool startsBewitched = scamPoint != null;
            Vector3 spawnPosition = startsBewitched
                ? AstarPath.active.GetNearest(scamPoint.position).position
                : FindSpawnPosition(index);
            var root = new GameObject($"DigitalHuman_{index + 1:00}");
            root.layer = LayerMask.NameToLayer("PathfindingAgent");
            root.transform.SetParent(runtimeRoot, false);
            root.transform.position = spawnPosition;

            var visualHolder = new GameObject("Visual");
            visualHolder.transform.SetParent(root.transform, false);

            GameObject visual = Instantiate(characterModelPrefab, visualHolder.transform);
            visual.name = "RandomAppearance";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            CharacterBase appearance = visual.GetComponent<CharacterBase>();
            if (appearance == null)
                appearance = visual.AddComponent<CharacterBase>();
            RandomizeAppearance(appearance);

            Animator animator = visual.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.runtimeAnimatorController = null;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            }

            AlignVisualToGround(visualHolder.transform, visual);

            AnimancerComponent animancer = null;
            if (animator != null)
            {
                animancer = animator.GetComponent<AnimancerComponent>();
                if (animancer == null)
                    animancer = animator.gameObject.AddComponent<AnimancerComponent>();
                animancer.Animator = animator;
            }

            root.AddComponent<Seeker>();
            RichAI richAI = root.AddComponent<RichAI>();
            richAI.radius = 0.30f;
            richAI.height = 1.8f;
            richAI.maxSpeed = Random.Range(minSpeed, maxSpeed);
            richAI.acceleration = Random.Range(6f, 9f);
            richAI.rotationSpeed = Random.Range(300f, 430f);
            richAI.slowdownTime = Random.Range(0.2f, 0.35f);
            richAI.wallDist = 0.35f;
            richAI.wallForce = 1.5f;
            richAI.funnelSimplification = true;
            richAI.slowWhenNotFacingTarget = true;
            richAI.endReachedDistance = 0.45f;

            RVOController rvo = root.AddComponent<RVOController>();
            rvo.agentTimeHorizon = 1.5f;
            rvo.obstacleTimeHorizon = 0.6f;
            rvo.maxNeighbours = 12;
            rvo.priority = 0.5f;
            rvo.lockWhenNotMoving = true;
            rvo.collidesWith = (RVOLayer)(-1);

            DigitalHumanWalker walker = root.AddComponent<DigitalHumanWalker>();
            walker.Initialize(
                animancer,
                idleClip,
                walkClip,
                agonizeClip,
                appearance,
                headParticlePrefab,
                headParticleScale,
                cheerHeadParticlePrefab,
                interactionPromptFont);

            BewitchedStudentPortal portal =
                root.AddComponent<BewitchedStudentPortal>();
            portal.Configure(walker, interactionPromptFont);

            if (startsBewitched)
            {
                walker.EnterScamState(FindNearestFlyer(spawnPosition));
                _spawnPositions.Add(spawnPosition);
                return;
            }

            Transform[] route = CreateShuffledRoute(scamPoint, out int scamRouteIndex);
            DigitalHumanPatrol patrol = root.AddComponent<DigitalHumanPatrol>();
            patrol.Initialize(
                route,
                scamRouteIndex,
                Random.Range(minWaypointWait, maxWaypointWait));

            root.transform.rotation = Quaternion.LookRotation(
                Flatten(route[0].position - spawnPosition),
                Vector3.up);

            _spawnPositions.Add(spawnPosition);
        }

        void EnsureLocalAvoidance()
        {
            RVOSimulator simulator = RVOSimulator.active;
            if (simulator == null)
                simulator = gameObject.AddComponent<RVOSimulator>();

            simulator.movementPlane = MovementPlane.XZ;
            simulator.hardCollisions = true;
            simulator.useNavmeshAsObstacle = true;
        }

        static void RandomizeAppearance(CharacterBase appearance)
        {
            appearance.SetItem(PartsType.Hair, RandomPartIndex(appearance.PartsHair, 6));
            appearance.SetItem(PartsType.Face, RandomPartIndex(appearance.PartsFace, 5));
            appearance.SetItem(PartsType.Headgear, RandomPartIndex(appearance.PartsHeadGear, 8));
            appearance.SetItem(PartsType.Top, RandomPartIndex(appearance.PartsTop, 46));
            appearance.SetItem(PartsType.Glove, 0);
            appearance.SetItem(PartsType.Bottom, RandomPartIndex(appearance.PartsBottom, 60));
            appearance.SetItem(PartsType.Shoes, RandomPartIndex(appearance.PartsShoes, 12));
            appearance.SetItem(PartsType.Bag, RandomPartIndex(appearance.PartsBag, 4));
            appearance.SetItem(PartsType.Eyewear, -1);
        }

        static int RandomPartIndex(IReadOnlyList<GameObject> parts, int maximumCount)
        {
            if (parts == null || parts.Count == 0 || maximumCount <= 0)
                return -1;

            return Random.Range(0, Mathf.Min(parts.Count, maximumCount));
        }

        Vector3 FindSpawnPosition(int index)
        {
            Transform preferredPoint = _waypoints[index % _waypoints.Length];
            Vector3 best = AstarPath.active.GetNearest(preferredPoint.position).position;
            float bestSpacing = -1f;

            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector2 circle = Random.insideUnitCircle * spawnJitterRadius;
                Vector3 candidate = preferredPoint.position + new Vector3(circle.x, 0f, circle.y);
                NNInfo nearest = AstarPath.active.GetNearest(candidate);
                if (nearest.node == null || !nearest.node.Walkable)
                    continue;

                float spacing = float.PositiveInfinity;
                for (int i = 0; i < _spawnPositions.Count; i++)
                {
                    float distance = Vector3.Distance(nearest.position, _spawnPositions[i]);
                    if (distance < spacing)
                        spacing = distance;
                }

                if (spacing > bestSpacing)
                {
                    best = nearest.position;
                    bestSpacing = spacing;
                }

                if (spacing >= minimumSpawnSpacing)
                    return nearest.position;
            }

            return best;
        }

        void PrepareScamAssignments()
        {
            _availableScamPoints.Clear();
            _scamAssignments = 0;
            if (scamWaypointRoot == null)
                return;

            for (int i = 0; i < scamWaypointRoot.childCount; i++)
            {
                Transform point = scamWaypointRoot.GetChild(i);
                if (point != null)
                    _availableScamPoints.Add(point);
            }

            if (_availableScamPoints.Count == 0)
                return;

            Shuffle(_availableScamPoints);
            _scamAssignments = Mathf.Min(
                InitialScamHumanCount,
                _availableScamPoints.Count);
        }

        Transform TakeScamPointForNpc(int index)
        {
            if (index >= _scamAssignments || _availableScamPoints.Count == 0)
                return null;

            Transform point = _availableScamPoints[0];
            _availableScamPoints.RemoveAt(0);
            return point;
        }

        Transform[] CreateShuffledRoute(Transform scamPoint, out int scamRouteIndex)
        {
            var route = new List<Transform>(_waypoints);
            Shuffle(route);

            scamRouteIndex = -1;
            if (scamPoint != null)
            {
                // Keep the demonstration responsive: complete one ordinary patrol
                // leg, then head to the assigned scam point.
                scamRouteIndex = Mathf.Min(1, route.Count);
                route.Insert(scamRouteIndex, scamPoint);
            }

            return route.ToArray();
        }

        public void BeginRescueCelebration(DigitalHumanWalker walker)
        {
            if (walker == null || !walker.IsBewitched)
                return;
            StartCoroutine(RescueCelebrationRoutine(walker));
        }

        IEnumerator RescueCelebrationRoutine(DigitalHumanWalker walker)
        {
            Transform player = null;
            GameObject playerObject = GameObject.Find("Player");
            if (playerObject != null)
                player = playerObject.transform;

            yield return walker.PlayRescueCelebration(cheerClip, player);
            if (walker == null || !walker.isActiveAndEnabled)
                yield break;

            Transform[] route = CreateShuffledRoute(null, out _);
            DigitalHumanPatrol patrol =
                walker.GetComponent<DigitalHumanPatrol>();
            if (patrol == null)
                patrol = walker.gameObject.AddComponent<DigitalHumanPatrol>();
            patrol.Initialize(
                route,
                -1,
                Random.Range(minWaypointWait, maxWaypointWait));
        }

        static Transform FindNearestFlyer(Vector3 position)
        {
            SpriteRenderer[] flyers = FindObjectsOfType<SpriteRenderer>(true);
            Transform nearest = null;
            float nearestDistance = float.PositiveInfinity;

            for (int i = 0; i < flyers.Length; i++)
            {
                SpriteRenderer flyer = flyers[i];
                if (flyer == null ||
                    !flyer.gameObject.name.StartsWith("scam_flyer_v2_"))
                    continue;

                float distance = (flyer.transform.position - position).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = flyer.transform;
                }
            }

            return nearest;
        }

        static void Shuffle<T>(IList<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int swapIndex = Random.Range(0, i + 1);
                T temp = items[i];
                items[i] = items[swapIndex];
                items[swapIndex] = temp;
            }
        }

        static void AlignVisualToGround(Transform holder, GameObject visual)
        {
            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds bounds = default;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds || bounds.size.y <= 0.01f)
                return;

            holder.localScale = Vector3.one;
            holder.localPosition = new Vector3(0f, -bounds.min.y, 0f);
        }

        static Vector3 Flatten(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
        }
    }
}
