using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Oculus.Interaction.Input;
using UnityEngine;

public class QuarkManager : Singleton<QuarkManager>
{
    [Header("Quark + Room")]
    [SerializeField] private RoomScanner roomScanner;
    [SerializeField] private Quark quarkPrefab;
    [SerializeField] private Transform quarkSpawnParent; // attach b_l_wrist here
    [SerializeField] private AudioSource justVibesSource;
    [SerializeField] private IHand _leftHand;        // assign in Inspector
    [SerializeField] private IHand _rightHand;       // assign in Inspector
    [SerializeField] private Transform _scaleTarget; // object to scale

    [SerializeField] private List<AudioClip> presetClips;
    
    [SerializeField] private List<AudioClip> justVibesClips;

    [Tooltip("Pinch strength above this counts as 'pinching'.")]
    [Range(0f, 1f)]
    [SerializeField] private float pinchStrengthThreshold = 0.7f;

    [Tooltip("Minimum uniform scale.")]
    [SerializeField] private float minScale = 0.3f;

    [Tooltip("Maximum uniform scale.")]
    [SerializeField] private float maxScale = 3f;

    private bool _isTwoHandScaling = false;
    private float _initialHandsDistance = 0f;
    private Vector3 _initialObjectScale;
    
    
    [Header("Palm Up Detection")]
    [Tooltip("Dot threshold: 1 = exactly up, 0 = sideways, -1 = down.")]
    [Range(-1f, 1f)]
    [SerializeField] private float palmUpThreshold = 0.75f;

    private Quark spawnedQuark = null;
    private bool lastPalmUp = false; // for change-detect
    private List<Quark> allQuarks = new();
    
    protected override void Awake()
    {
        base.Awake();
        Debug.Log("[QuarkManager] Awake");
    }

    private void Start()
    {
        SpawnQuark(quarkSpawnParent);
    }

    public void SpawnQuark(Transform parent)
    {
        spawnedQuark = Instantiate(quarkPrefab, parent);
        spawnedQuark.transform.localPosition = Vector3.zero;
        spawnedQuark.transform.localRotation = Quaternion.identity;
        spawnedQuark.gameObject.SetActive(false);
        spawnedQuark.Audio.clip = presetClips[UnityEngine.Random.Range(0, presetClips.Count)];
        allQuarks.Add(spawnedQuark);
        
        Debug.Log("[QuarkManager] Spawned new Quark.");
    }

    public async Task GenerateMusicForQuark(Quark quark)
    {
        Debug.Log("[QuarkManager] Generating music for Quark...");
        await roomScanner.ScanAndPlayMusic(quark);
    }

    public void OnQuarkGrabbed(Quark quark, bool isFirstGrab)
    {
        Debug.Log($"[QuarkManager] Quark grabbed. First grab: {isFirstGrab}");

        if (isFirstGrab)
        {
            spawnedQuark = null;
            StartCoroutine(SpawnNewQuark());
        }
    }

    private IEnumerator SpawnNewQuark(float delay = 3f)
    {
        Debug.Log($"[QuarkManager] Waiting {delay}s before spawning a new Quark...");
        yield return new WaitForSeconds(delay);

        SpawnQuark(quarkSpawnParent);
    }

    private void Update()
    {
        // === PALM-UP LOGIC (your existing code) ===

        // World-space up direction of the wrist
        Vector3 palmNormal = -quarkSpawnParent.up; // already world-space

        // Debug line in Scene view
        Debug.DrawLine(quarkSpawnParent.position,
                       quarkSpawnParent.position + palmNormal * 0.1f,
                       Color.blue);

        // Compare wrist up with global up
        float dot = Vector3.Dot(palmNormal.normalized, Vector3.up);
        bool palmUp = dot > palmUpThreshold;

        // Only log when state changes
        if (palmUp != lastPalmUp)
        {
            if (palmUp)
            {
                justVibesSource.clip = justVibesClips[UnityEngine.Random.Range(0, justVibesClips.Count)];
                justVibesSource.Play();
            }
            Debug.Log($"[QuarkManager] PalmUp = {palmUp} (dot: {dot:F3})");
            lastPalmUp = palmUp;
        }

        if (spawnedQuark != null)
        {
            spawnedQuark.gameObject.SetActive(palmUp);
        }

        // === TWO-HAND PINCH SCALING ===

        if (_leftHand == null || _rightHand == null || _scaleTarget == null)
            return;

        // 1) Check pinch state on both hands (index finger)
        bool leftPinching =
            _leftHand.GetFingerIsPinching(HandFinger.Index) &&
            _leftHand.GetFingerPinchStrength(HandFinger.Index) >= pinchStrengthThreshold;

        bool rightPinching =
            _rightHand.GetFingerIsPinching(HandFinger.Index) &&
            _rightHand.GetFingerPinchStrength(HandFinger.Index) >= pinchStrengthThreshold;

        bool bothPinching = leftPinching && rightPinching;

        // 2) Get a representative position for each hand
        //    (you can use wrist, index tip, or a custom anchor)
        Pose leftPose, rightPose;
        if (!_leftHand.GetRootPose(out leftPose) || !_rightHand.GetRootPose(out rightPose))
            return;

        Vector3 leftPos  = leftPose.position;
        Vector3 rightPos = rightPose.position;

        float currentDistance = Vector3.Distance(leftPos, rightPos);

        // 3) State transitions
        if (bothPinching && !_isTwoHandScaling)
        {
            // just started two-hand pinch → capture baseline
            _isTwoHandScaling = true;
            _initialHandsDistance = Mathf.Max(currentDistance, 0.001f); // avoid div by zero
            _initialObjectScale = _scaleTarget.localScale;
            // Debug.Log("[QuarkManager] Two-hand pinch scaling started.");
        }
        else if (bothPinching && _isTwoHandScaling)
        {
            // actively scaling
            float scaleFactor = currentDistance / _initialHandsDistance;

            // Optionally enforce uniform scaling based on x
            float uniform = Mathf.Clamp(scaleFactor, minScale, maxScale);
            _scaleTarget.localScale = _initialObjectScale * uniform;
        }
        else if (!bothPinching && _isTwoHandScaling)
        {
            // pinch released on at least one hand → stop scaling
            _isTwoHandScaling = false;
            // Debug.Log("[QuarkManager] Two-hand pinch scaling ended.");
        }
    }

}
