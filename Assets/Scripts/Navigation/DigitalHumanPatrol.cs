using Pathfinding;
using UnityEngine;

namespace AntiFraudStorm.Navigation
{
    /// <summary>
    /// Patrol controller with a dedicated scam waypoint. A digital human that
    /// reaches its assigned scam point stops there and enters the bewitched state.
    /// </summary>
    [RequireComponent(typeof(DigitalHumanWalker))]
    public class DigitalHumanPatrol : MonoBehaviour
    {
        const string FlyerPrefix = "scam_flyer_v2_";

        Transform[] _targets;
        float _delay;
        float _arrivalDistance = 0.9f;
        int _scamIndex = -1;
        int _index = -1;
        float _switchTime = float.NegativeInfinity;
        bool _initialized;
        bool _finishedScam;
        IAstarAI _agent;
        DigitalHumanWalker _walker;

        public void Initialize(Transform[] targets, int scamIndex, float delay)
        {
            _targets = targets;
            _scamIndex = scamIndex;
            _delay = Mathf.Max(0f, delay);
            _index = -1;
            _switchTime = float.NegativeInfinity;
            _finishedScam = false;
            _initialized = true;
        }

        void Awake()
        {
            _agent = GetComponent<IAstarAI>();
            _walker = GetComponent<DigitalHumanWalker>();
        }

        void OnEnable()
        {
            if (_initialized && !_finishedScam && _index >= 0 &&
                _targets != null && _index < _targets.Length)
            {
                _agent.destination = _targets[_index].position;
                _agent.SearchPath();
            }
        }

        void Update()
        {
            if (!_initialized || _finishedScam || _targets == null || _targets.Length == 0)
                return;

            if (_index >= 0 &&
                !_agent.pathPending &&
                (_agent.reachedEndOfPath || IsNearCurrentTarget()) &&
                float.IsPositiveInfinity(_switchTime))
            {
                _switchTime = Time.time + _delay;
            }

            if (Time.time < _switchTime)
                return;

            if (_index >= 0 && _index == _scamIndex)
            {
                _finishedScam = true;
                _agent.isStopped = true;
                _walker.EnterScamState(FindNearestFlyer());
                return;
            }

            _switchTime = float.PositiveInfinity;
            _index++;
            if (_index >= _targets.Length)
                _index = 0;

            _agent.destination = _targets[_index].position;
            _agent.SearchPath();
        }

        bool IsNearCurrentTarget()
        {
            if (_index < 0 || _index >= _targets.Length || _targets[_index] == null)
                return false;

            Vector3 delta = _targets[_index].position - transform.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= _arrivalDistance * _arrivalDistance;
        }

        Transform FindNearestFlyer()
        {
            SpriteRenderer[] flyers = FindObjectsOfType<SpriteRenderer>(true);
            Transform nearest = null;
            float nearestDistance = float.PositiveInfinity;

            for (int i = 0; i < flyers.Length; i++)
            {
                SpriteRenderer flyer = flyers[i];
                if (flyer == null ||
                    !flyer.gameObject.name.StartsWith(FlyerPrefix))
                    continue;

                float distance = (flyer.transform.position - transform.position).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = flyer.transform;
                }
            }

            return nearest;
        }
    }
}
