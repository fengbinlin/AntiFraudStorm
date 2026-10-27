using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AntiFraudStorm.Quest
{
    /// <summary>
    /// Central quest runtime. Triggers and HUD subscribe here.
    /// </summary>
    public class QuestService : MonoBehaviour
    {
        [SerializeField] QuestDefinition startingQuest;
        [SerializeField] Transform player;
        [SerializeField] bool autoStartOnAwake = true;

        readonly Dictionary<string, QuestState> _states = new Dictionary<string, QuestState>();
        readonly Dictionary<string, QuestDefinition> _defs = new Dictionary<string, QuestDefinition>();

        QuestDefinition _active;
        Transform _activeTarget;

        public static QuestService Instance { get; private set; }

        public QuestDefinition ActiveQuest => _active;
        public Transform ActiveTarget => _activeTarget;
        public bool HasActiveQuest => _active != null;

        public event Action<QuestDefinition> QuestStarted;
        public event Action<QuestDefinition> QuestChanged;
        public event Action<QuestDefinition> QuestCompleted;
        public event Action<QuestDefinition> QuestPointEntered;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (player == null)
            {
                var p = GameObject.Find("Player");
                if (p != null)
                    player = p.transform;
            }

            // Start in Awake so quest is Active before other components' Start()
            // (e.g. QuestZoneTrigger.RefreshVisual). Otherwise a race can hide the
            // marker GameObject and disable the trigger forever.
            if (autoStartOnAwake && startingQuest != null)
                StartQuest(startingQuest);
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (player == null)
            {
                var p = GameObject.Find("Player");
                if (p != null)
                    player = p.transform;
            }
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void RegisterDefinition(QuestDefinition def)
        {
            if (def == null || string.IsNullOrEmpty(def.Id))
                return;
            _defs[def.Id] = def;
            if (!_states.ContainsKey(def.Id))
                _states[def.Id] = QuestState.Inactive;
        }

        public void StartQuest(QuestDefinition def)
        {
            if (def == null)
                return;

            RegisterDefinition(def);
            _active = def;
            _states[def.Id] = QuestState.Active;
            QuestStarted?.Invoke(def);
            QuestChanged?.Invoke(def);
        }

        public void StartQuest(string questId)
        {
            if (_defs.TryGetValue(questId, out var def))
                StartQuest(def);
            else
                Debug.LogWarning($"[Quest] Unknown quest id: {questId}");
        }

        public void SetActiveTarget(Transform target)
        {
            _activeTarget = target;
            QuestChanged?.Invoke(_active);
        }

        public QuestState GetState(string questId)
        {
            return _states.TryGetValue(questId, out var s) ? s : QuestState.Inactive;
        }

        public bool IsActive(string questId)
        {
            return _active != null && _active.Id == questId && GetState(questId) == QuestState.Active;
        }

        /// <summary>
        /// Called when the player enters the quest point trigger.
        /// Fires <see cref="QuestPointEntered"/>; content can subscribe later.
        /// </summary>
        public void NotifyQuestPointEntered(string questId)
        {
            if (!IsActive(questId))
                return;

            QuestPointEntered?.Invoke(_active);
            Debug.Log($"[Quest] Point entered: {questId} ({_active?.Title})");
        }

        public void CompleteActiveQuest()
        {
            if (_active == null)
                return;

            var done = _active;
            _states[done.Id] = QuestState.Completed;
            _active = null;
            _activeTarget = null;
            QuestCompleted?.Invoke(done);
            QuestChanged?.Invoke(null);

            if (!string.IsNullOrEmpty(done.NextQuestId) &&
                _defs.TryGetValue(done.NextQuestId, out var next))
            {
                StartQuest(next);
            }
        }

        public float GetDistanceToActiveMeters()
        {
            if (player == null || _activeTarget == null)
                return -1f;

            Vector3 a = player.position;
            Vector3 b = _activeTarget.position;
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        public static string FormatDistance(float meters)
        {
            if (meters < 0f)
                return string.Empty;
            if (meters < 1000f)
                return $"{Mathf.RoundToInt(meters)}m";
            return $"{meters / 1000f:0.0}km";
        }
    }
}
