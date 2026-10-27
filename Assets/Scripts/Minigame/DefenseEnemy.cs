using UnityEngine;
using UnityEngine.UI;

namespace AntiFraudStorm.Minigame
{
    /// <summary>
    /// A scam minion that walks down one lane and attacks the player.
    /// </summary>
    public class DefenseEnemy : MonoBehaviour
    {
        public string ScamName { get; private set; }
        public int MatchingAmmoIndex { get; private set; }
        public int MatchingGroup { get; private set; }
        public bool IsDead { get; private set; }
        public bool IsSpawning { get; private set; }
        public Vector3 AimPoint => transform.position + Vector3.up * 1.15f;

        AntiFraudDefenseGame _game;
        Transform _player;
        RectTransform _labelCanvas;
        Text _labelText;
        Image _healthFill;
        Animator _animator;
        string _moveState = "Fly Forward In Place";
        string _attackState = "Projectile Attack";
        string _spawnState = "Spawn";
        string _hurtState = "Take Damage";
        string _dieState = "Die";
        bool _ranged;
        bool _attacking;
        float _moveSpeed = 1.2f;
        float _attackDistance = 1.4f;
        float _attackCooldown = 2.3f;
        float _nextAttackTime;
        int _health = 100;
        int _maxHealth = 100;
        float _spawnFallbackDuration = 1.2f;
        string _currentState;

        public void Initialize(
            AntiFraudDefenseGame game,
            Transform player,
            Text label,
            Image healthFill,
            string scamName,
            int matchingAmmoIndex,
            int matchingGroup,
            bool ranged,
            float moveSpeed,
            float attackDistance,
            float attackCooldown,
            int health,
            string attackState,
            string spawnState,
            float spawnFallbackDuration)
        {
            _game = game;
            _player = player;
            _labelText = label;
            _healthFill = healthFill;
            Canvas labelCanvas = label != null
                ? label.GetComponentInParent<Canvas>()
                : null;
            _labelCanvas = labelCanvas != null
                ? labelCanvas.transform as RectTransform
                : null;
            ScamName = scamName;
            MatchingAmmoIndex = matchingAmmoIndex;
            MatchingGroup = matchingGroup;
            _ranged = ranged;
            _moveSpeed = moveSpeed;
            _attackDistance = attackDistance;
            _attackCooldown = attackCooldown;
            _health = Mathf.Max(1, health);
            _maxHealth = _health;
            _attackState = string.IsNullOrWhiteSpace(attackState)
                ? "Projectile Attack"
                : attackState;
            _spawnState = string.IsNullOrWhiteSpace(spawnState)
                ? "Spawn"
                : spawnState;
            _spawnFallbackDuration = Mathf.Max(0.1f, spawnFallbackDuration);

            _animator = GetComponentInChildren<Animator>(true);
            if (_animator != null)
            {
                _animator.applyRootMotion = false;
                _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            UpdateLabel();
            StartCoroutine(SpawnIntroRoutine());
        }

        void Update()
        {
            if (IsDead || IsSpawning || _player == null || _game == null)
                return;

            Vector3 target = _player.position;
            target.y = transform.position.y;
            Vector3 direction = target - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion look = Quaternion.LookRotation(direction.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    look,
                    Time.deltaTime * 9f);
            }

            Vector3 separation = _game.GetEnemySeparation(this);
            if (separation.sqrMagnitude > 0.0001f)
                transform.position += separation * Time.deltaTime * 2.4f;

            float distance = direction.magnitude;
            if (distance > _attackDistance)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    target,
                    _moveSpeed * Time.deltaTime);
                if (!_attacking)
                    PlayState(_moveState);
                return;
            }

            if (!_attacking && Time.time >= _nextAttackTime)
                StartCoroutine(AttackRoutine());
        }

        System.Collections.IEnumerator SpawnIntroRoutine()
        {
            IsSpawning = true;
            float duration = _spawnFallbackDuration;

            if (_animator != null)
            {
                int stateHash = Animator.StringToHash(_spawnState);
                if (_animator.HasState(0, stateHash))
                {
                    _currentState = _spawnState;
                    _animator.Play(stateHash, 0, 0f);
                    _animator.Update(0f);

                    AnimatorClipInfo[] clipInfo =
                        _animator.GetCurrentAnimatorClipInfo(0);
                    if (clipInfo.Length > 0 && clipInfo[0].clip != null)
                    {
                        float speed = Mathf.Max(
                            0.01f,
                            Mathf.Abs(
                                _animator.GetCurrentAnimatorStateInfo(0)
                                    .speedMultiplier));
                        duration = clipInfo[0].clip.length / speed;
                    }
                }
            }

            yield return new WaitForSeconds(Mathf.Max(0.1f, duration));
            if (IsDead)
                yield break;

            IsSpawning = false;
            _currentState = null;
            PlayState(_moveState, true);
        }

        System.Collections.IEnumerator AttackRoutine()
        {
            _attacking = true;
            _nextAttackTime = Time.time + _attackCooldown;
            PlayState(_attackState, true);
            yield return new WaitForSeconds(0.32f);

            if (!IsDead && _game != null)
                _game.EnemyAttack(this, _ranged);

            yield return new WaitForSeconds(0.35f);
            _attacking = false;
            if (!IsDead)
                PlayState(_moveState);
        }

        public void TakeDamage(int damage)
        {
            if (IsDead || IsSpawning)
                return;

            _health = Mathf.Max(0, _health - Mathf.Max(0, damage));
            UpdateLabel();
            PlayState(_hurtState, true);

            if (_health <= 0)
                Die();
        }

        void Die()
        {
            if (IsDead)
                return;

            IsDead = true;
            _game?.OnEnemyDestroyed(this);
            StopAllCoroutines();
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;

            PlayState(_dieState, true);
            if (_labelCanvas != null)
                Destroy(_labelCanvas.gameObject);
            Destroy(gameObject, 1.4f);
        }

        void LateUpdate()
        {
            if (_labelCanvas == null || _game == null || _game.ViewCamera == null)
                return;

            _labelCanvas.position = transform.position + Vector3.up * _game.EnemyLabelHeight;
            _labelCanvas.rotation = _game.ViewCamera.transform.rotation;
        }

        void UpdateLabel()
        {
            if (_labelText != null)
            {
                _labelText.text = ScamName;
                _labelText.color = new Color(0.8f, 0.42f, 1f);
            }
            if (_healthFill != null)
            {
                float ratio = _maxHealth > 0
                    ? Mathf.Clamp01((float)_health / _maxHealth)
                    : 0f;
                _healthFill.fillAmount = ratio;
                _healthFill.color = Color.Lerp(
                    new Color(0.72f, 0.035f, 0.05f, 1f),
                    new Color(1f, 0.18f, 0.12f, 1f),
                    ratio);
            }

            if (_game != null)
                _game.EnemyHealthChanged(this);
        }

        void PlayState(string stateName, bool force = false)
        {
            if (_animator == null || string.IsNullOrWhiteSpace(stateName))
                return;
            if (!force && _currentState == stateName)
                return;

            _currentState = stateName;
            _animator.CrossFadeInFixedTime(stateName, 0.12f);
        }
    }
}
