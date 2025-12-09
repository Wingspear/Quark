using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction.Input;
using UnityEngine;

/// <summary>
/// Simplified Quark manager that works with the new QuarkEntity lifecycle system.
/// Handles spawning, palm detection, two-hand scaling, and coordinates with QuarkEntity.
/// </summary>
public class QuarkManagerSimplified : Singleton<QuarkManagerSimplified>
{
    [Header("Quark Spawning")]
    [SerializeField] private QuarkEntity quarkPrefab;
    [SerializeField] private Transform quarkSpawnParent; // Attach to b_l_wrist
    [SerializeField] private float respawnDelay = 3f;

    [Header("Palm Facing Camera")]
    [Tooltip("Dot threshold for palm facing camera: -1 = directly facing, 0 = sideways, 1 = facing away")]
    [Range(-1f, 1f)]
    [SerializeField] private float palmFacingCameraThreshold = -0.5f;
    [SerializeField] private Camera mainCamera;

    [Header("Just Vibes Audio")]
    [SerializeField] private AudioSource justVibesSource;
    [SerializeField] private List<AudioClip> justVibesClips;

    [Header("Two-Hand Scaling")]
    [SerializeField] private IHand leftHand;
    [SerializeField] private IHand rightHand;
    [SerializeField] private Transform scaleTarget;
    [Tooltip("Pinch strength above this counts as 'pinching'.")]
    [Range(0f, 1f)]
    [SerializeField] private float pinchStrengthThreshold = 0.7f;
    [Tooltip("Minimum uniform scale.")]
    [SerializeField] private float minScale = 0.3f;
    [Tooltip("Maximum uniform scale.")]
    [SerializeField] private float maxScale = 3f;

    // State
    private QuarkEntity _activeQuark;
    private List<QuarkEntity> _allQuarks = new();
    private bool _lastPalmFacingCamera = false;
    private bool _isTwoHandScaling = false;
    private float _initialHandsDistance = 0f;
    private Vector3 _initialObjectScale;

    protected override void Awake()
    {
        base.Awake();
        Debug.Log("[QuarkManagerSimplified] Initialized");
    }

    private void Start()
    {
        // Don't spawn immediately - wait for palm to face camera
        // Initial state check will happen in first Update()
    }

    private void Update()
    {
        // Spawn Quark if it doesn't exist and palm is facing camera
        if (_activeQuark == null)
        {
            if (IsPalmFacingCamera())
            {
                SpawnQuark();
                // Immediately summon since palm is already facing camera
                _activeQuark.Summon();
                _lastPalmFacingCamera = true;
            }
        }
        else
        {
            UpdatePalmFacingCamera();
        }
        UpdateTwoHandScaling();
    }

    /// <summary>
    /// Spawn a new Quark at the wrist position
    /// </summary>
    public void SpawnQuark()
    {
        if (_activeQuark != null)
        {
            Debug.LogWarning("[QuarkManagerSimplified] Active Quark already exists. Aborting spawn.");
            return;
        }

        _activeQuark = Instantiate(quarkPrefab, quarkSpawnParent);
        _activeQuark.transform.localPosition = Vector3.zero;
        _activeQuark.transform.localRotation = Quaternion.identity;
        _allQuarks.Add(_activeQuark);

        // Subscribe to events
        _activeQuark.OnGrabbed += HandleQuarkGrabbed;
        _activeQuark.OnReleased += HandleQuarkReleased;

        Debug.Log("[QuarkManagerSimplified] Spawned new Quark");
    }

    /// <summary>
    /// Check if palm is currently facing camera
    /// </summary>
    private bool IsPalmFacingCamera()
    {
        // Auto-find main camera if not assigned
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return false;
        }

        if (quarkSpawnParent == null) return false;

        // Get palm normal (world-space) - points outward from palm
        Vector3 palmNormal = -quarkSpawnParent.up;

        // Get direction from palm to camera
        Vector3 palmToCamera = (mainCamera.transform.position - quarkSpawnParent.position).normalized;

        // Check if palm is facing camera
        float dot = Vector3.Dot(palmNormal.normalized, palmToCamera);
        return dot > -palmFacingCameraThreshold;
    }

    /// <summary>
    /// Update palm facing camera detection and summon/dismiss Quark accordingly
    /// </summary>
    private void UpdatePalmFacingCamera()
    {
        // Auto-find main camera if not assigned
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        if (quarkSpawnParent == null || _activeQuark == null) return;

        // Get palm normal (world-space) - points outward from palm
        Vector3 palmNormal = -quarkSpawnParent.up;

        // Get direction from palm to camera
        Vector3 palmToCamera = (mainCamera.transform.position - quarkSpawnParent.position).normalized;

        // Debug visualization
        Debug.DrawLine(quarkSpawnParent.position,
                       quarkSpawnParent.position + palmNormal * 0.1f,
                       Color.blue); // Palm normal

        Debug.DrawLine(quarkSpawnParent.position,
                       quarkSpawnParent.position + palmToCamera * 0.1f,
                       Color.green); // Direction to camera

        // Check if palm is facing camera
        float dot = Vector3.Dot(palmNormal.normalized, palmToCamera);
        bool palmFacingCamera = dot > -palmFacingCameraThreshold;

        // Only act when state changes
        if (palmFacingCamera != _lastPalmFacingCamera)
        {
            if (palmFacingCamera)
            {
                // Play Just Vibes audio
                if (justVibesSource != null && justVibesClips != null && justVibesClips.Count > 0)
                {
                    justVibesSource.clip = justVibesClips[Random.Range(0, justVibesClips.Count)];
                    justVibesSource.Play();
                }

                // Summon the Quark
                _activeQuark.Summon();
                Debug.Log($"[QuarkManagerSimplified] Palm facing camera - Summoned (dot: {dot:F3})");
            }
            else
            {
                // Dismiss the Quark
                _activeQuark.Dismiss();
                Debug.Log($"[QuarkManagerSimplified] Palm not facing camera - Dismissed (dot: {dot:F3})");
            }

            _lastPalmFacingCamera = palmFacingCamera;
        }
    }

    /// <summary>
    /// Update two-hand pinch scaling
    /// </summary>
    private void UpdateTwoHandScaling()
    {
        if (leftHand == null || rightHand == null || scaleTarget == null)
            return;

        // Check pinch state on both hands (index finger)
        bool leftPinching =
            leftHand.GetFingerIsPinching(HandFinger.Index) &&
            leftHand.GetFingerPinchStrength(HandFinger.Index) >= pinchStrengthThreshold;

        bool rightPinching =
            rightHand.GetFingerIsPinching(HandFinger.Index) &&
            rightHand.GetFingerPinchStrength(HandFinger.Index) >= pinchStrengthThreshold;

        bool bothPinching = leftPinching && rightPinching;

        // Get hand positions
        Pose leftPose, rightPose;
        if (!leftHand.GetRootPose(out leftPose) || !rightHand.GetRootPose(out rightPose))
            return;

        Vector3 leftPos = leftPose.position;
        Vector3 rightPos = rightPose.position;

        float currentDistance = Vector3.Distance(leftPos, rightPos);

        // State transitions
        if (bothPinching && !_isTwoHandScaling)
        {
            // Start two-hand scaling
            _isTwoHandScaling = true;
            _initialHandsDistance = Mathf.Max(currentDistance, 0.001f); // Avoid division by zero
            _initialObjectScale = scaleTarget.localScale;
            Debug.Log("[QuarkManagerSimplified] Two-hand pinch scaling started");
        }
        else if (bothPinching && _isTwoHandScaling)
        {
            // Actively scaling
            float scaleFactor = currentDistance / _initialHandsDistance;
            float uniform = Mathf.Clamp(scaleFactor, minScale, maxScale);
            scaleTarget.localScale = _initialObjectScale * uniform;
        }
        else if (!bothPinching && _isTwoHandScaling)
        {
            // Stop scaling
            _isTwoHandScaling = false;
            Debug.Log("[QuarkManagerSimplified] Two-hand pinch scaling ended");
        }
    }

    /// <summary>
    /// Handle Quark grabbed event
    /// </summary>
    private void HandleQuarkGrabbed(QuarkEntity quark, bool isFirstGrab)
    {
        Debug.Log($"[QuarkManagerSimplified] Quark grabbed (first: {isFirstGrab})");

        if (isFirstGrab)
        {
            // Unsubscribe from events
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
        Debug.Log("[QuarkManagerSimplified] Quark released");
    }

    /// <summary>
    /// Spawn a new Quark after a delay
    /// </summary>
    private IEnumerator SpawnNewQuarkAfterDelay(float delay)
    {
        Debug.Log($"[QuarkManagerSimplified] Waiting {delay}s before spawning new Quark...");
        yield return new WaitForSeconds(delay);
        SpawnQuark();
    }

    /// <summary>
    /// Get all Quarks ever spawned
    /// </summary>
    public List<QuarkEntity> GetAllQuarks() => _allQuarks;

    /// <summary>
    /// Get the currently active (ungrabbbed) Quark
    /// </summary>
    public QuarkEntity GetActiveQuark() => _activeQuark;
}
