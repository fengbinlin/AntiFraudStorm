using UnityEngine;

namespace AntiFraudStorm.Quest
{
    [CreateAssetMenu(menuName = "AntiFraudStorm/Quest Definition", fileName = "Quest_")]
    public class QuestDefinition : ScriptableObject
    {
        [SerializeField] string id = QuestIds.EnrollRegistration;
        [SerializeField] string tagLabel = "主线";
        [SerializeField] string title = "办理入学注册";
        [SerializeField] string nextQuestId;
        [SerializeField] GameObject markerPrefab;
        [SerializeField] Vector3 markerLocalScale = new Vector3(0.1502f, 0.1502f, 0.1502f);
        [SerializeField] float triggerRadius = 2.5f;

        public string Id => id;
        public string TagLabel => tagLabel;
        public string Title => title;
        public string NextQuestId => nextQuestId;
        public GameObject MarkerPrefab => markerPrefab;
        public Vector3 MarkerLocalScale => markerLocalScale;
        public float TriggerRadius => triggerRadius;
    }
}
