using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Detects when the palm is facing the camera and fires events.
/// Also plays Just Vibes audio clips when palm faces camera.
/// </summary>
public class PalmDetector : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField] private Transform palmTransform; // Wrist transform (OpenXRLeftHand)
    [Tooltip("Dot threshold for palm facing camera: -1 = directly facing, 0 = sideways, 1 = facing away")]
    [Range(-1f, 1f)]
    [SerializeField] private float palmFacingThreshold = -0.5f;
    [SerializeField] private Camera mainCamera;

    [Header("Audio Feedback")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private List<AudioClip> justVibesClips;

    [Header("Debug Visualization")]
    [SerializeField] private bool showDebugRays = true;

    private bool _lastPalmFacingCamera = false;

    /// <summary>
    /// Event fired when palm starts facing the camera
    /// </summary>
    public event Action OnPalmFacingCamera;

    /// <summary>
    /// Event fired when palm stops facing the camera
    /// </summary>
    public event Action OnPalmNotFacingCamera;

    /// <summary>
    /// Current palm facing state
    /// </summary>
    public bool IsPalmFacingCamera { get; private set; }

    private void Start()
    {
        // Auto-find camera if not assigned
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (palmTransform == null)
        {
            Debug.LogError("[PalmDetector] Palm transform not assigned!");
            return;
        }
        
        // Initialize state immediately so other components can check it
        if (mainCamera != null)
        {
            DetectPalmDirection();
        }
    }

    private void Update()
    {
        if (mainCamera == null || palmTransform == null)
            return;

        DetectPalmDirection();
    }

    private void DetectPalmDirection()
    {
        // Get palm normal (world-space) - points outward from palm
        Vector3 palmNormal = -palmTransform.up;

        // Get direction from palm to camera
        Vector3 palmToCamera = (mainCamera.transform.position - palmTransform.position).normalized;

        // Debug visualization
        if (showDebugRays)
        {
            Debug.DrawLine(palmTransform.position,
                           palmTransform.position + palmNormal * 0.1f,
                           Color.blue); // Palm normal

            Debug.DrawLine(palmTransform.position,
                           palmTransform.position + palmToCamera * 0.1f,
                           Color.green); // Direction to camera
        }

        // Check if palm is facing camera
        float dot = Vector3.Dot(palmNormal.normalized, palmToCamera);
        bool palmFacingCamera = dot > -palmFacingThreshold;

        // Update state
        bool stateChanged = palmFacingCamera != _lastPalmFacingCamera;
        IsPalmFacingCamera = palmFacingCamera;

        // Fire events when state changes
        if (stateChanged)
        {
            if (palmFacingCamera)
            {
                HandlePalmFacingCamera(dot);
            }
            else
            {
                HandlePalmNotFacingCamera(dot);
            }

            _lastPalmFacingCamera = palmFacingCamera;
        }
    }

    private void HandlePalmFacingCamera(float dot)
    {
        Debug.Log($"[PalmDetector] Palm facing camera (dot: {dot:F3}, threshold: {-palmFacingThreshold:F3})");

        // Play Just Vibes audio
        if (audioSource != null && justVibesClips != null && justVibesClips.Count > 0)
        {
            audioSource.clip = justVibesClips[UnityEngine.Random.Range(0, justVibesClips.Count)];
            audioSource.Play();
        }

        Debug.Log($"[PalmDetector] Invoking OnPalmFacingCamera event (subscribers: {OnPalmFacingCamera?.GetInvocationList().Length ?? 0})");
        OnPalmFacingCamera?.Invoke();
    }

    private void HandlePalmNotFacingCamera(float dot)
    {
        Debug.Log($"[PalmDetector] Palm not facing camera (dot: {dot:F3})");
        OnPalmNotFacingCamera?.Invoke();
    }
}
