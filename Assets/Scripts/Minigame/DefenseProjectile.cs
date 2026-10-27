using System;
using UnityEngine;

namespace AntiFraudStorm.Minigame
{
    /// <summary>
    /// Lightweight homing projectile used by both the player and ranged enemies.
    /// Visual prefabs from Hovl Studio provide the actual particle trail.
    /// </summary>
    public class DefenseProjectile : MonoBehaviour
    {
        Transform _playerTarget;
        DefenseEnemy _enemyTarget;
        Action<Vector3> _onHit;
        float _speed = 18f;
        float _hitRadius = 0.35f;
        float _remainingLife = 5f;
        Material _ownedMaterial;

        public void OwnMaterial(Material material)
        {
            _ownedMaterial = material;
        }

        public void InitializeEnemyTarget(
            DefenseEnemy target,
            float speed,
            Action<Vector3> onHit)
        {
            _enemyTarget = target;
            _speed = speed;
            _onHit = onHit;
            PreparePhysics();
        }

        public void InitializePlayerTarget(
            Transform target,
            float speed,
            Action<Vector3> onHit)
        {
            _playerTarget = target;
            _speed = speed;
            _onHit = onHit;
            PreparePhysics();
        }

        void PreparePhysics()
        {
            Rigidbody[] rigidbodies = GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                rigidbodies[i].isKinematic = true;
                rigidbodies[i].useGravity = false;
                rigidbodies[i].detectCollisions = false;
            }

            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;

            MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null &&
                    behaviours[i].GetType().Name == "ProjectileMover")
                    behaviours[i].enabled = false;
            }
        }

        void Update()
        {
            _remainingLife -= Time.deltaTime;
            if (_remainingLife <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 targetPosition;
            if (_enemyTarget != null && !_enemyTarget.IsDead)
            {
                targetPosition = _enemyTarget.AimPoint;
            }
            else if (_playerTarget != null)
            {
                targetPosition = _playerTarget.position + Vector3.up * 1.15f;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            Vector3 offset = targetPosition - transform.position;
            float distance = offset.magnitude;
            if (distance <= _hitRadius)
            {
                _onHit?.Invoke(targetPosition);
                Destroy(gameObject);
                return;
            }

            Vector3 direction = offset / Mathf.Max(distance, 0.0001f);
            transform.position += direction * Mathf.Min(_speed * Time.deltaTime, distance);
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        void OnDestroy()
        {
            if (_ownedMaterial != null)
                Destroy(_ownedMaterial);
        }
    }
}
