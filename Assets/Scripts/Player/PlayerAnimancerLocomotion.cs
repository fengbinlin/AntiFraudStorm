using Animancer;
using UnityEngine;

namespace AntiFraudStorm.Player
{
    /// <summary>
    /// Discrete Idle / Walk / Run (no blend) + Jump via Animancer.
    /// Walk/Run play the original clips at full weight.
    /// </summary>
    [RequireComponent(typeof(AnimancerComponent))]
    public class PlayerAnimancerLocomotion : MonoBehaviour
    {
        enum LocoState
        {
            Idle,
            Walk,
            Run,
            Jump
        }

        [SerializeField] AnimancerComponent animancer;
        [SerializeField] AnimationClip idleClip;
        [SerializeField] AnimationClip walkClip;
        [SerializeField] AnimationClip runClip;
        [SerializeField] AnimationClip jumpClip;
        [SerializeField] float fadeDuration = 0.15f;

        [Header("Playback Pace")]
        [SerializeField] float walkAnimSpeed = 1.35f;
        [SerializeField] float runAnimSpeed = 1.2f;

        LocoState _state = LocoState.Idle;
        AnimancerState _current;
        AnimancerState _jumpState;
        bool _actionBusy;

        public bool IsJumping => _state == LocoState.Jump;
        public bool IsActionBusy => _actionBusy;

        public void NotifyActionBusy(bool busy)
        {
            _actionBusy = busy;
        }

        public void NotifyActionStarted()
        {
            NotifyActionBusy(true);
        }

        public void NotifyActionEnded()
        {
            NotifyActionBusy(false);
        }

        public void Initialize(AnimancerComponent animancerComponent)
        {
            animancer = animancerComponent;
            PlayState(LocoState.Idle, force: true);
        }

        public void SetClips(AnimationClip idle, AnimationClip walk, AnimationClip run, AnimationClip jump)
        {
            idleClip = idle;
            walkClip = walk;
            runClip = run;
            jumpClip = jump;
        }

        public void Tick(bool isMoving, bool isRunning, bool isGrounded, bool jumpPressed)
        {
            if (animancer == null)
                return;

            // Prop / interaction clips are driven externally.
            if (_actionBusy)
                return;

            if (_state != LocoState.Jump && jumpPressed && isGrounded && jumpClip != null)
            {
                PlayJump();
                return;
            }

            if (_state == LocoState.Jump)
            {
                if (isGrounded && _jumpState != null && _jumpState.NormalizedTime >= 0.9f)
                    PlayState(ResolveGroundState(isMoving, isRunning), force: true);
                return;
            }

            PlayState(ResolveGroundState(isMoving, isRunning));
        }

        static LocoState ResolveGroundState(bool isMoving, bool isRunning)
        {
            if (!isMoving)
                return LocoState.Idle;
            return isRunning ? LocoState.Run : LocoState.Walk;
        }

        void PlayState(LocoState next, bool force = false)
        {
            if (!force && _state == next && _current != null && _current.IsPlaying)
                return;

            AnimationClip clip = next switch
            {
                LocoState.Walk => walkClip,
                LocoState.Run => runClip,
                LocoState.Jump => jumpClip,
                _ => idleClip
            };

            if (clip == null)
                return;

            _state = next;
            _current = animancer.Play(clip, fadeDuration);
            _current.Speed = next switch
            {
                LocoState.Walk => walkAnimSpeed,
                LocoState.Run => runAnimSpeed,
                _ => 1f
            };
        }

        void PlayJump()
        {
            _state = LocoState.Jump;
            _jumpState = animancer.Play(jumpClip, fadeDuration, FadeMode.FromStart);
            _jumpState.Speed = 1f;
            _current = _jumpState;
            _jumpState.Events(this).OnEnd ??= () =>
            {
                if (_state == LocoState.Jump)
                    PlayState(LocoState.Idle, force: true);
            };
        }
    }
}
