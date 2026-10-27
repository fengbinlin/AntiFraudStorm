using AntiFraudStorm.Player;
using AntiFraudStorm.SceneFlow;
using UnityEngine;
using UnityEngine.Events;

namespace AntiFraudStorm.Quest
{
    /// <summary>
    /// Scene quest point: keeps marker FX, reports distance target, fires enter event.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class QuestZoneTrigger : MonoBehaviour
    {
        [SerializeField] string questId = QuestIds.EnrollRegistration;
        [SerializeField] QuestDefinition questDefinition;
        [SerializeField] Transform markerRoot;
        [SerializeField] bool hideMarkerWhenInactive = true;
        [SerializeField] bool completeOnEnter;
        [SerializeField] bool interactToOpenComputer = true;
        [SerializeField] QuestInteractPrompt interactPrompt;
        [SerializeField] UnityEvent onPlayerEntered;

        SphereCollider _collider;
        bool _entered;
        bool _playerInside;

        public Transform MarkerRoot => markerRoot != null ? markerRoot : transform;

        void Reset()
        {
            EnsureCollider();
        }

        void Awake()
        {
            EnsureCollider();
            if (markerRoot == null)
                markerRoot = transform;
            if (interactPrompt == null)
                interactPrompt = GetComponent<QuestInteractPrompt>();
        }

        void OnEnable()
        {
            if (QuestService.Instance != null)
                BindService(QuestService.Instance);
        }

        void Start()
        {
            var service = QuestService.Instance;
            if (service == null)
                return;

            if (questDefinition != null)
            {
                service.RegisterDefinition(questDefinition);
                if (string.IsNullOrEmpty(questId))
                    questId = questDefinition.Id;
            }

            BindService(service);
            RefreshVisual(service);
        }

        void OnDisable()
        {
            var service = QuestService.Instance;
            if (service == null)
                return;
            service.QuestStarted -= OnQuestStarted;
            service.QuestChanged -= OnQuestChanged;
            service.QuestCompleted -= OnQuestCompleted;
        }

        void BindService(QuestService service)
        {
            service.QuestStarted -= OnQuestStarted;
            service.QuestChanged -= OnQuestChanged;
            service.QuestCompleted -= OnQuestCompleted;
            service.QuestStarted += OnQuestStarted;
            service.QuestChanged += OnQuestChanged;
            service.QuestCompleted += OnQuestCompleted;

            if (service.IsActive(questId))
                service.SetActiveTarget(MarkerRoot);
        }

        void EnsureCollider()
        {
            _collider = GetComponent<SphereCollider>();
            if (_collider == null)
                _collider = gameObject.AddComponent<SphereCollider>();

            _collider.isTrigger = true;
            float radius = questDefinition != null ? questDefinition.TriggerRadius : 2.5f;
            // Collider is on the FX root which may be scaled; keep world-ish radius.
            float scale = Mathf.Max(0.0001f, transform.lossyScale.x);
            _collider.radius = radius / scale;
            _collider.center = Vector3.zero;
        }

        void OnQuestStarted(QuestDefinition def)
        {
            if (def != null && def.Id == questId)
            {
                _entered = false;
                QuestService.Instance?.SetActiveTarget(MarkerRoot);
            }

            RefreshVisual(QuestService.Instance);
        }

        void OnQuestChanged(QuestDefinition _) => RefreshVisual(QuestService.Instance);

        void OnQuestCompleted(QuestDefinition def)
        {
            if (def != null && def.Id == questId)
                _entered = false;
            RefreshVisual(QuestService.Instance);
        }

        void RefreshVisual(QuestService service)
        {
            if (!hideMarkerWhenInactive || markerRoot == null)
                return;

            bool show = service != null && service.IsActive(questId);
            SetMarkerVisible(show);
        }

        void SetMarkerVisible(bool show)
        {
            // Never SetActive(false) on this component's GameObject (or an ancestor):
            // that disables QuestZoneTrigger, unsubscribes quest events, and the
            // marker stays off even after the quest becomes Active.
            if (markerRoot == transform || transform.IsChildOf(markerRoot))
            {
                var rootPs = markerRoot.GetComponent<ParticleSystem>();
                if (rootPs != null)
                {
                    if (show)
                        rootPs.Play(true);
                    else
                        rootPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }

                var rootRenderer = markerRoot.GetComponent<Renderer>();
                if (rootRenderer != null)
                    rootRenderer.enabled = show;

                for (int i = 0; i < markerRoot.childCount; i++)
                {
                    var child = markerRoot.GetChild(i).gameObject;
                    if (child.activeSelf != show)
                        child.SetActive(show);
                }

                return;
            }

            if (markerRoot.gameObject.activeSelf != show)
                markerRoot.gameObject.SetActive(show);
        }

        void Update()
        {
            if (!_playerInside || !interactToOpenComputer)
                return;
            if (CampusComputerSession.IsFrozen)
                return;

            var service = QuestService.Instance;
            if (service == null || !service.IsActive(questId))
                return;

            var input = PlayerInputReader.Active;
            if (input == null || !input.InteractPressed)
                return;

            interactPrompt?.SetVisible(false);
            if (CampusComputerSession.Instance != null)
                CampusComputerSession.Instance.EnterComputer();
        }

        void OnTriggerEnter(Collider other)
        {
            if (!IsPlayer(other))
                return;

            var service = QuestService.Instance;
            if (service == null || !service.IsActive(questId))
                return;

            _playerInside = true;
            interactPrompt?.SetVisible(true);

            if (!_entered)
            {
                _entered = true;
                service.NotifyQuestPointEntered(questId);
                onPlayerEntered?.Invoke();
            }

            if (completeOnEnter)
                service.CompleteActiveQuest();
        }

        void OnTriggerExit(Collider other)
        {
            if (!IsPlayer(other))
                return;

            _playerInside = false;
            interactPrompt?.SetVisible(false);
        }

        static bool IsPlayer(Collider other)
        {
            if (other == null)
                return false;
            if (other.CompareTag("Player"))
                return true;
            if (other.GetComponent<CharacterController>() != null && other.name.Contains("Player"))
                return true;
            return other.GetComponentInParent<AntiFraudStorm.Player.ThirdPersonPlayerController>() != null;
        }
    }
}
