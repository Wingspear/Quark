using System.Collections;
using System.Collections.Generic;
using Jusvibes.Core;
using UnityEngine;

/// <summary>
/// Manages the Quark lifecycle: spawning, tracking, and responding to grab events.
/// Works with the new QuarkEntity system and coordinates with PalmDetector.
/// </summary>
public class QuarkManager : Singleton<QuarkManager>
{
    [Header("Quark Spawning")]
    [SerializeField] private QuarkEntity quarkPrefab;
    [SerializeField] private Transform quarkSpawnParent; // Attach to b_l_wrist

    [Header("Dependencies")]
    [SerializeField] private PalmDetector palmDetector;

    [Header("Settings")]
    [SerializeField] private float respawnDelay = 3f;

    // State
    private QuarkEntity _activeQuark;
    private List<QuarkEntity> _allQuarks = new List<QuarkEntity>();
    
    protected override void Awake()
    {
        base.Awake();
        Debug.Log("[QuarkManager] Initialized");
    }

    private void Start()
    {
        // Subscribe to palm detector events FIRST, before checking state
        if (palmDetector != null)
        {
            palmDetector.OnPalmFacingCamera += HandlePalmFacingCamera;
            palmDetector.OnPalmNotFacingCamera += HandlePalmNotFacingCamera;
            
            // Check initial state - only spawn if palm is already facing camera
            // Wait a couple frames to ensure PalmDetector has fully initialized
            StartCoroutine(CheckInitialPalmState());
        }
        else
        {
            Debug.LogWarning("[QuarkManager] PalmDetector not assigned!");
        }
    }
    
    /// <summary>
    /// Check initial palm state after PalmDetector has had a chance to initialize
    /// </summary>
    private System.Collections.IEnumerator CheckInitialPalmState()
    {
        // Wait a couple frames to ensure PalmDetector has initialized and updated
        yield return null;
        yield return null;
        
        if (palmDetector != null)
        {
            if (palmDetector.IsPalmFacingCamera)
            {
                Debug.Log("[QuarkManager] Initial palm state: facing camera - spawning Quark");
                HandlePalmFacingCamera();
            }
            else
            {
                Debug.Log($"[QuarkManager] Initial palm state: not facing camera (IsPalmFacingCamera={palmDetector.IsPalmFacingCamera}) - waiting for palm to face camera");
            }
        }
        else
        {
            Debug.LogWarning("[QuarkManager] PalmDetector became null during initialization check");
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from palm detector events
        if (palmDetector != null)
        {
            palmDetector.OnPalmFacingCamera -= HandlePalmFacingCamera;
            palmDetector.OnPalmNotFacingCamera -= HandlePalmNotFacingCamera;
        }
    }

    /// <summary>
    /// Spawn a new Quark at the wrist position
    /// </summary>
    public void SpawnQuark()
    {
        if (_activeQuark != null)
        {
            Debug.LogWarning("[QuarkManager] Active Quark already exists. Skipping spawn.");
            return;
        }

        _activeQuark = Instantiate(quarkPrefab, quarkSpawnParent);
        _activeQuark.transform.localPosition = Vector3.zero;
        _activeQuark.transform.localRotation = Quaternion.identity;
        _allQuarks.Add(_activeQuark);

        // Inject pipeline dependencies from scene
        InjectPipelineDependencies(_activeQuark);

        // Subscribe to Quark events
        _activeQuark.OnGrabbed += HandleQuarkGrabbed;
        _activeQuark.OnReleased += HandleQuarkReleased;

        Debug.Log("[QuarkManager] Spawned new Quark");
    }

    /// <summary>
    /// Inject pipeline dependencies into a spawned Quark
    /// </summary>
    private void InjectPipelineDependencies(QuarkEntity quark)
    {
        var orchestrator = quark.GetComponent<QuarkLifecycleOrchestrator>();
        if (orchestrator == null)
        {
            Debug.LogWarning("[QuarkManager] No QuarkLifecycleOrchestrator found on spawned Quark");
            return;
        }

        // Find scene singletons
        var captureController = FindObjectOfType<CaptureController>();
        var whisperRecorder = FindObjectOfType<WhisperRecorder>();
        var insightProcessor = FindObjectOfType<CaptureInsightProcessor>();
        var musicGenerator = FindObjectOfType<MusicGenerator>();

        // Inject dependencies
        orchestrator.InjectDependencies(
            captureController,
            whisperRecorder,
            insightProcessor,
            musicGenerator
        );

        // Log warnings if any dependencies are missing
        if (captureController == null)
            Debug.LogWarning("[QuarkManager] CaptureController not found in scene");
        if (whisperRecorder == null)
            Debug.LogWarning("[QuarkManager] WhisperRecorder not found in scene");
        if (insightProcessor == null)
            Debug.LogWarning("[QuarkManager] CaptureInsightProcessor not found in scene");
        if (musicGenerator == null)
            Debug.LogWarning("[QuarkManager] MusicGenerator not found in scene");
    }

    /// <summary>
    /// Handle palm facing camera - spawn and summon the Quark
    /// </summary>
    private void HandlePalmFacingCamera()
    {
        Debug.Log("[QuarkManager] HandlePalmFacingCamera called");
        
        // Spawn Quark if it doesn't exist
        if (_activeQuark == null)
        {
            Debug.Log("[QuarkManager] Palm facing camera - spawning Quark");
            SpawnQuark();
        }
        
        // Summon the Quark (transition from Dormant to Summoned)
        if (_activeQuark != null)
        {
            Debug.Log("[QuarkManager] Summoning Quark");
            _activeQuark.Summon();
        }
        else
        {
            Debug.LogWarning("[QuarkManager] Tried to summon but _activeQuark is null!");
        }
    }

    /// <summary>
    /// Handle palm not facing camera - dismiss the Quark
    /// </summary>
    private void HandlePalmNotFacingCamera()
    {
        if (_activeQuark != null)
        {
            Debug.Log("[QuarkManager] Palm not facing camera - dismissing Quark");
            _activeQuark.Dismiss();
        }
    }

    /// <summary>
    /// Handle Quark grabbed event
    /// </summary>
    private void HandleQuarkGrabbed(QuarkEntity quark, bool isFirstGrab)
    {
        Debug.Log($"[QuarkManager] Quark grabbed (first: {isFirstGrab})");

        if (isFirstGrab)
        {
            // Unsubscribe from this Quark's events
            quark.OnGrabbed -= HandleQuarkGrabbed;
            quark.OnReleased -= HandleQuarkReleased;

            // Clear active reference
            _activeQuark = null;

            // Spawn new Quark after delay
            StartCoroutine(SpawnNewQuarkAfterDelay(respawnDelay));
        }
    }

    /// <summary>
    /// Handle Quark released event
    /// </summary>
    private void HandleQuarkReleased(QuarkEntity quark)
    {
        Debug.Log("[QuarkManager] Quark released");
    }

    /// <summary>
    /// Spawn a new Quark after a delay
    /// </summary>
    private IEnumerator SpawnNewQuarkAfterDelay(float delay)
    {
        Debug.Log($"[QuarkManager] Waiting {delay}s before spawning new Quark...");
        yield return new WaitForSeconds(delay);
        SpawnQuark();
    }

    /// <summary>
    /// Get all Quarks ever spawned
    /// </summary>
    public List<QuarkEntity> GetAllQuarks() => _allQuarks;

    /// <summary>
    /// Get the currently active (ungrabbed) Quark
    /// </summary>
    public QuarkEntity GetActiveQuark() => _activeQuark;
}
