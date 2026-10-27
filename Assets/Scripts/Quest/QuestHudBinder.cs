using AntiFraudStorm.UI;
using UnityEngine;

namespace AntiFraudStorm.Quest
{
    /// <summary>
    /// Pushes active quest title + live distance into GameplayHud.
    /// </summary>
    public class QuestHudBinder : MonoBehaviour
    {
        [SerializeField] GameplayHudController hud;
        [SerializeField] float updateInterval = 0.15f;

        float _timer;

        void Awake()
        {
            if (hud == null)
                hud = GameplayHudController.Instance;
        }

        void OnEnable()
        {
            if (QuestService.Instance != null)
                Bind(QuestService.Instance);
        }

        void Start()
        {
            if (hud == null)
                hud = GameplayHudController.Instance;

            var service = QuestService.Instance;
            if (service != null)
            {
                Bind(service);
                Refresh(service.ActiveQuest);
            }
        }

        void OnDisable()
        {
            var service = QuestService.Instance;
            if (service == null)
                return;
            service.QuestChanged -= OnQuestChanged;
            service.QuestCompleted -= OnQuestCompleted;
        }

        void Update()
        {
            var service = QuestService.Instance;
            if (service == null || hud == null || !service.HasActiveQuest)
                return;

            _timer += Time.deltaTime;
            if (_timer < updateInterval)
                return;
            _timer = 0f;

            float meters = service.GetDistanceToActiveMeters();
            hud.SetQuestDistance(QuestService.FormatDistance(meters));
        }

        void Bind(QuestService service)
        {
            service.QuestChanged -= OnQuestChanged;
            service.QuestCompleted -= OnQuestCompleted;
            service.QuestChanged += OnQuestChanged;
            service.QuestCompleted += OnQuestCompleted;
        }

        void OnQuestChanged(QuestDefinition def) => Refresh(def);

        void OnQuestCompleted(QuestDefinition _)
        {
            // Refresh happens via QuestChanged(null) from service.
        }

        void Refresh(QuestDefinition def)
        {
            if (hud == null)
                hud = GameplayHudController.Instance;
            if (hud == null)
                return;

            if (def == null)
            {
                hud.SetMainQuest(string.Empty);
                hud.SetQuestDistance(string.Empty);
                return;
            }

            hud.SetMainQuest(def.Title);
            var service = QuestService.Instance;
            float meters = service != null ? service.GetDistanceToActiveMeters() : -1f;
            hud.SetQuestDistance(QuestService.FormatDistance(meters));
        }
    }
}
