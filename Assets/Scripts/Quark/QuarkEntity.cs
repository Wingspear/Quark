using System;
using Oculus.Interaction;
using UnityEngine;

/// <summary>
/// The core Quark entity - a clean, event-driven component.
/// Coordinates all Quark sub-controllers through the state machine.
/// </summary>
[RequireComponent(typeof(QuarkStateMachine))]
public class QuarkEntity : MonoBehaviour
    {
        [Header("Core Components")]
        [SerializeField] private QuarkStateMachine stateMachine;
        [SerializeField] private QuarkVisualController visualController;
        [SerializeField] private QuarkAudioController audioController;
        [SerializeField] private QuarkLifecycleOrchestrator orchestrator;

        [Header("Interaction")]
        [SerializeField] private Grabbable grabbable;
        
        [Header("Visual Control")]
        [SerializeField] private Renderer grabbableRenderer; // The base grabbable mesh renderer
        [SerializeField] private Transform grabbableTransform; // For scaling

        private bool _isGrabbed = false;
        private bool _hasBeenInitialized = false;
        private Vector3 _initialScale = Vector3.one; // Store the prefab's original scale

        /// <summary>
        /// Current lifecycle state
        /// </summary>
        public QuarkLifecycleState CurrentState => stateMachine?.CurrentState ?? QuarkLifecycleState.Dormant;

        /// <summary>
        /// Whether the Quark has been initialized with audio
        /// </summary>
        public bool HasAudio => stateMachine?.HasAudio ?? false;

        /// <summary>
        /// The main AudioSource for this Quark
        /// </summary>
        public AudioSource Audio => audioController?.GetComponent<AudioSource>();

        /// <summary>
        /// Event fired when Quark is grabbed
        /// </summary>
        public event Action<QuarkEntity, bool> OnGrabbed;

        /// <summary>
        /// Event fired when Quark is released
        /// </summary>
        public event Action<QuarkEntity> OnReleased;

        /// <summary>
        /// Event fired when state changes
        /// </summary>
        public event Action<QuarkLifecycleState> OnStateChanged;

        private void Awake()
        {
            // Auto-wire components
            if (stateMachine == null)
                stateMachine = GetComponent<QuarkStateMachine>();

            if (visualController == null)
                visualController = GetComponent<QuarkVisualController>();

            if (audioController == null)
                audioController = GetComponent<QuarkAudioController>();

            if (orchestrator == null)
                orchestrator = GetComponent<QuarkLifecycleOrchestrator>();

            if (grabbable == null)
                grabbable = GetComponent<Grabbable>();
            
            // Auto-find grabbable renderer and transform
            if (grabbableRenderer == null && grabbable != null)
            {
                grabbableRenderer = grabbable.GetComponent<Renderer>();
                if (grabbableRenderer == null)
                    grabbableRenderer = grabbable.GetComponentInChildren<Renderer>();
            }
            
            if (grabbableTransform == null && grabbable != null)
            {
                grabbableTransform = grabbable.transform;
            }
            
            // Store the initial scale from prefab (before any state changes)
            if (grabbableTransform != null)
            {
                _initialScale = grabbableTransform.localScale;
            }

            // Subscribe to state changes
            if (stateMachine != null)
            {
                stateMachine.OnStateChanged += HandleStateChanged;
                stateMachine.OnBecameDormant += OnBecameDormant;
                stateMachine.OnSummoned += OnSummoned;
            }
        }

        private void Start()
        {
            // Subscribe to grab events
            if (grabbable != null)
            {
                grabbable.WhenPointerEventRaised += HandlePointerEvent;
            }
        }

        private void OnDestroy()
        {
            if (grabbable != null)
            {
                grabbable.WhenPointerEventRaised -= HandlePointerEvent;
            }

            if (stateMachine != null)
            {
                stateMachine.OnStateChanged -= HandleStateChanged;
                stateMachine.OnBecameDormant -= OnBecameDormant;
                stateMachine.OnSummoned -= OnSummoned;
            }
        }

        /// <summary>
        /// Called when palm is facing camera - summon the Quark
        /// </summary>
        public void Summon()
        {
            if (CurrentState == QuarkLifecycleState.Dormant)
            {
                stateMachine?.SetState(QuarkLifecycleState.Summoned);
            }
        }

        /// <summary>
        /// Called when palm is not facing camera - dismiss the Quark back to Dormant
        /// </summary>
        public void Dismiss()
        {
            // Can dismiss from Summoned, Idle, or Ready states back to Dormant
            // Also allow from Playing state (stop audio and go dormant)
            if (CurrentState == QuarkLifecycleState.Summoned || 
                CurrentState == QuarkLifecycleState.Idle || 
                CurrentState == QuarkLifecycleState.Ready ||
                CurrentState == QuarkLifecycleState.Playing)
            {
                // Stop audio if playing
                if (CurrentState == QuarkLifecycleState.Playing)
                {
                    audioController?.Stop();
                }
                
                stateMachine?.SetState(QuarkLifecycleState.Dormant);
            }
            else if (CurrentState == QuarkLifecycleState.Dormant)
            {
                // Already dormant, nothing to do
            }
            else
            {
                Debug.Log($"[QuarkEntity] Cannot dismiss from {CurrentState} state - will dismiss when state allows");
            }
        }

        /// <summary>
        /// Inject colors from environment analysis
        /// </summary>
        public void InjectColors(Color primary, Color secondary)
        {
            visualController?.InjectColors(primary, secondary);
        }

        /// <summary>
        /// Reset to initial state
        /// </summary>
        public void ResetQuark()
        {
            stateMachine?.Reset();
            audioController?.Reset();
            orchestrator?.Reset();
            visualController?.ClearInjectedColors();
            _hasBeenInitialized = false;
        }

        private void HandlePointerEvent(PointerEvent evt)
        {
            switch (evt.Type)
            {
                case PointerEventType.Select:
                    if (!_isGrabbed)
                    {
                        _isGrabbed = true;
                        HandleGrab();
                    }
                    break;

                case PointerEventType.Unselect:
                case PointerEventType.Cancel:
                    if (_isGrabbed)
                    {
                        _isGrabbed = false;
                        HandleRelease();
                    }
                    break;
            }
        }

        private void HandleGrab()
        {
            bool isFirstGrab = !_hasBeenInitialized;

            Debug.Log($"[QuarkEntity] Grabbed (first: {isFirstGrab})");

            // Notify orchestrator
            orchestrator?.NotifyGrabbed(isFirstGrab);

            // Fire event for external listeners (e.g., QuarkManager)
            OnGrabbed?.Invoke(this, isFirstGrab);

            _hasBeenInitialized = true;
        }

        private void HandleRelease()
        {
            Debug.Log("[QuarkEntity] Released");

            // Detach from parent
            Vector3 pos = transform.position;
            Quaternion rot = transform.rotation;
            transform.SetParent(null);
            transform.position = pos;
            transform.rotation = rot;

            // Notify orchestrator
            orchestrator?.NotifyReleased();

            // Fire event for external listeners
            OnReleased?.Invoke(this);
        }

        private void HandleStateChanged(QuarkLifecycleState previous, QuarkLifecycleState current)
        {
            OnStateChanged?.Invoke(current);
            UpdateGrabbableVisuals(current);
        }
        
        private void OnBecameDormant()
        {
            // Hide the entire Quark GameObject when dormant
            gameObject.SetActive(false);
        }
        
        private void OnSummoned()
        {
            // Show the entire Quark GameObject when summoned
            gameObject.SetActive(true);
        }
        
        /// <summary>
        /// Update grabbable visuals (scale and color) based on state
        /// </summary>
        private void UpdateGrabbableVisuals(QuarkLifecycleState state)
        {
            if (grabbableTransform == null || grabbableRenderer == null) return;
            
            // Scale multiplier based on state (applied to prefab's original scale)
            float scaleMultiplier = state switch
            {
                QuarkLifecycleState.Dormant => 0f,  // Hidden
                QuarkLifecycleState.Summoned => 1f,
                QuarkLifecycleState.Grabbed => 1.1f,
                QuarkLifecycleState.Generating => 1.4f,
                QuarkLifecycleState.Ready => 1.5f,
                QuarkLifecycleState.Playing => 1.5f,
                QuarkLifecycleState.Idle => 1.4f,
                _ => 1f
            };

            // Apply scale multiplier to the prefab's original scale
            grabbableTransform.localScale = _initialScale * scaleMultiplier;

            // Color based on state (if material supports it)
            Color targetColor = state switch
            {
                QuarkLifecycleState.Dormant => Color.clear,
                QuarkLifecycleState.Summoned => new Color(1f, 1f, 1f, 0.8f),  // White, slightly transparent
                QuarkLifecycleState.Grabbed => Color.cyan,
                QuarkLifecycleState.Generating => new Color(1f, 0.5f, 0f),  // Orange
                QuarkLifecycleState.Ready => Color.magenta,
                QuarkLifecycleState.Playing => Color.magenta,
                QuarkLifecycleState.Idle => Color.blue,
                _ => Color.white
            };
            
            if (grabbableRenderer.material != null)
            {
                if (grabbableRenderer.material.HasProperty("_Color"))
                    grabbableRenderer.material.color = targetColor;
                else if (grabbableRenderer.material.HasProperty("_BaseColor"))
                    grabbableRenderer.material.SetColor("_BaseColor", targetColor);
            }
        }

        // For debug/testing
        [ContextMenu("Force State: Playing")]
        private void ForcePlayingState()
        {
            stateMachine?.SetState(QuarkLifecycleState.Playing);
        }

        [ContextMenu("Force State: Dormant")]
        private void ForceDormantState()
        {
            stateMachine?.SetState(QuarkLifecycleState.Dormant);
        }
    }
