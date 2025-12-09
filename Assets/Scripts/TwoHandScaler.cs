using System;
using Oculus.Interaction.Input;
using UnityEngine;

/// <summary>
/// Handles two-hand pinch scaling for world objects.
/// Completely independent of Quark system - generic utility.
/// </summary>
public class TwoHandScaler : MonoBehaviour
{
    [Header("Hand References")]
    [SerializeField] private IHand leftHand;
    [SerializeField] private IHand rightHand;

    [Header("Scale Target")]
    [SerializeField] private Transform scaleTarget;

    [Header("Settings")]
    [Tooltip("Pinch strength above this counts as 'pinching'.")]
    [Range(0f, 1f)]
    [SerializeField] private float pinchStrengthThreshold = 0.7f;

    [Tooltip("Minimum uniform scale.")]
    [SerializeField] private float minScale = 0.3f;

    [Tooltip("Maximum uniform scale.")]
    [SerializeField] private float maxScale = 3f;

    [Header("Debug")]
    [SerializeField] private bool logScaling = false;

    // State
    private bool _isScaling = false;
    private float _initialHandsDistance = 0f;
    private Vector3 _initialObjectScale;

    /// <summary>
    /// Event fired when scaling starts
    /// </summary>
    public event Action OnScalingStarted;

    /// <summary>
    /// Event fired when scaling ends
    /// </summary>
    public event Action OnScalingEnded;

    /// <summary>
    /// Whether two-hand scaling is currently active
    /// </summary>
    public bool IsScaling => _isScaling;

    private void Update()
    {
        if (leftHand == null || rightHand == null || scaleTarget == null)
            return;

        ProcessTwoHandScaling();
    }

    private void ProcessTwoHandScaling()
    {
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
        if (bothPinching && !_isScaling)
        {
            StartScaling(currentDistance);
        }
        else if (bothPinching && _isScaling)
        {
            UpdateScaling(currentDistance);
        }
        else if (!bothPinching && _isScaling)
        {
            EndScaling();
        }
    }

    private void StartScaling(float currentDistance)
    {
        _isScaling = true;
        _initialHandsDistance = Mathf.Max(currentDistance, 0.001f); // Avoid division by zero
        _initialObjectScale = scaleTarget.localScale;

        if (logScaling)
        {
            Debug.Log($"[TwoHandScaler] Scaling started (initial distance: {_initialHandsDistance:F3}m)");
        }

        OnScalingStarted?.Invoke();
    }

    private void UpdateScaling(float currentDistance)
    {
        float scaleFactor = currentDistance / _initialHandsDistance;
        float clampedScale = Mathf.Clamp(scaleFactor, minScale, maxScale);
        scaleTarget.localScale = _initialObjectScale * clampedScale;
    }

    private void EndScaling()
    {
        _isScaling = false;

        if (logScaling)
        {
            Debug.Log($"[TwoHandScaler] Scaling ended (final scale: {scaleTarget.localScale.x:F2})");
        }

        OnScalingEnded?.Invoke();
    }

    /// <summary>
    /// Set the target object to scale at runtime
    /// </summary>
    public void SetScaleTarget(Transform target)
    {
        scaleTarget = target;
    }

    /// <summary>
    /// Get current scale factor relative to initial scale
    /// </summary>
    public float GetCurrentScaleFactor()
    {
        if (_initialObjectScale == Vector3.zero) return 1f;
        return scaleTarget.localScale.x / _initialObjectScale.x;
    }
}
