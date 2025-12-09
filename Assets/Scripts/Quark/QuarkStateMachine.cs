using System;
using UnityEngine;

/// <summary>
/// Central state machine for Quark lifecycle.
/// Emits events for state transitions, allowing other components to react.
/// </summary>
public class QuarkStateMachine : MonoBehaviour
    {
        [SerializeField] private QuarkLifecycleState initialState = QuarkLifecycleState.Dormant;
        [SerializeField] private bool logStateChanges = true;

        private QuarkLifecycleState _currentState;
        private QuarkLifecycleState _previousState;

        /// <summary>
        /// Current lifecycle state
        /// </summary>
        public QuarkLifecycleState CurrentState => _currentState;

        /// <summary>
        /// Previous lifecycle state (useful for transitions)
        /// </summary>
        public QuarkLifecycleState PreviousState => _previousState;

        /// <summary>
        /// Fired when state changes - includes previous and new state
        /// </summary>
        public event Action<QuarkLifecycleState, QuarkLifecycleState> OnStateChanged;

        /// <summary>
        /// Fired when entering a specific state
        /// </summary>
        public event Action<QuarkLifecycleState> OnStateEntered;

        /// <summary>
        /// Fired when exiting a specific state
        /// </summary>
        public event Action<QuarkLifecycleState> OnStateExited;

        // Convenience events for common transitions
        public event Action OnBecameDormant;
        public event Action OnSummoned;
        public event Action OnGrabbed;
        public event Action OnGenerating;
        public event Action OnReady;
        public event Action OnPlaying;
        public event Action OnIdle;
        public event Action<string> OnError; // Includes error message

        private void Awake()
        {
            _currentState = initialState;
            _previousState = initialState;
        }

        /// <summary>
        /// Transition to a new state
        /// </summary>
        public bool SetState(QuarkLifecycleState newState, string errorMessage = null)
        {
            if (_currentState == newState)
            {
                return false; // No change
            }

            // Validate transition
            if (!IsValidTransition(_currentState, newState))
            {
                Debug.LogWarning($"[QuarkStateMachine] Invalid transition: {_currentState} → {newState}");
                return false;
            }

            _previousState = _currentState;
            _currentState = newState;

            if (logStateChanges)
            {
                string emoji = GetStateEmoji(newState);
                Debug.Log($"[QuarkStateMachine] {emoji} {_previousState} → {newState}");
            }

            // Fire general events
            OnStateExited?.Invoke(_previousState);
            OnStateEntered?.Invoke(_currentState);
            OnStateChanged?.Invoke(_previousState, _currentState);

            // Fire specific events
            switch (newState)
            {
                case QuarkLifecycleState.Dormant: OnBecameDormant?.Invoke(); break;
                case QuarkLifecycleState.Summoned: OnSummoned?.Invoke(); break;
                case QuarkLifecycleState.Grabbed: OnGrabbed?.Invoke(); break;
                case QuarkLifecycleState.Generating: OnGenerating?.Invoke(); break;
                case QuarkLifecycleState.Ready: OnReady?.Invoke(); break;
                case QuarkLifecycleState.Playing: OnPlaying?.Invoke(); break;
                case QuarkLifecycleState.Idle: OnIdle?.Invoke(); break;
                case QuarkLifecycleState.Error: OnError?.Invoke(errorMessage ?? "Unknown error"); break;
            }

            return true;
        }

        /// <summary>
        /// Check if current state matches
        /// </summary>
        public bool IsInState(QuarkLifecycleState state) => _currentState == state;

        /// <summary>
        /// Check if in generating state (processing/loading)
        /// </summary>
        public bool IsLoading => _currentState == QuarkLifecycleState.Generating;

        /// <summary>
        /// Check if Quark has been initialized with audio
        /// </summary>
        public bool HasAudio => _currentState == QuarkLifecycleState.Ready ||
                                _currentState == QuarkLifecycleState.Playing ||
                                _currentState == QuarkLifecycleState.Idle ||
                                _currentState == QuarkLifecycleState.Error;

        /// <summary>
        /// Reset to initial state
        /// </summary>
        public void Reset()
        {
            SetState(initialState);
        }

        private bool IsValidTransition(QuarkLifecycleState from, QuarkLifecycleState to)
        {
            // Error can be reached from any state
            if (to == QuarkLifecycleState.Error) return true;

            // Define valid transitions (simplified)
            return (from, to) switch
            {
                // Normal forward flow
                (QuarkLifecycleState.Dormant, QuarkLifecycleState.Summoned) => true,
                (QuarkLifecycleState.Summoned, QuarkLifecycleState.Dormant) => true,  // Palm moved away
                (QuarkLifecycleState.Playing, QuarkLifecycleState.Dormant) => true,    // Palm moved away during playback
                (QuarkLifecycleState.Idle, QuarkLifecycleState.Dormant) => true,       // Palm moved away during idle
                (QuarkLifecycleState.Ready, QuarkLifecycleState.Dormant) => true,      // Palm moved away before playing
                (QuarkLifecycleState.Summoned, QuarkLifecycleState.Grabbed) => true,
                (QuarkLifecycleState.Grabbed, QuarkLifecycleState.Generating) => true, // After drop
                (QuarkLifecycleState.Generating, QuarkLifecycleState.Ready) => true,
                (QuarkLifecycleState.Ready, QuarkLifecycleState.Playing) => true,
                (QuarkLifecycleState.Playing, QuarkLifecycleState.Idle) => true,
                (QuarkLifecycleState.Idle, QuarkLifecycleState.Playing) => true,      // Resume

                // Re-grab after initialization
                (QuarkLifecycleState.Playing, QuarkLifecycleState.Grabbed) => true,
                (QuarkLifecycleState.Idle, QuarkLifecycleState.Grabbed) => true,

                // Error recovery
                (QuarkLifecycleState.Error, QuarkLifecycleState.Ready) => true,       // After fallback

                _ => false
            };
        }

        private string GetStateEmoji(QuarkLifecycleState state)
        {
            return state switch
            {
                QuarkLifecycleState.Dormant => "⚪",
                QuarkLifecycleState.Summoned => "✨",
                QuarkLifecycleState.Grabbed => "✊",
                QuarkLifecycleState.Generating => "🎵",
                QuarkLifecycleState.Ready => "✅",
                QuarkLifecycleState.Playing => "▶️",
                QuarkLifecycleState.Idle => "⏸️",
                QuarkLifecycleState.Error => "❌",
                _ => "❓"
            };
        }
    }
