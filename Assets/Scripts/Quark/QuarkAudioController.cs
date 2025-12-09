using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls Quark audio playback with lifecycle awareness.
/// Only plays audio when Ready state is reached.
/// Provides fallback to preset audio on error.
/// </summary>
public class QuarkAudioController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private QuarkStateMachine stateMachine;
        [SerializeField] private AudioSource mainAudioSource;
        [SerializeField] private AudioSource pickupSfx;
        [SerializeField] private AudioSource dropSfx;

        [Header("Preset Audio (Fallback)")]
        [SerializeField] private List<AudioClip> presetClips;
        [SerializeField] private bool shufflePresets = true;

        [Header("Settings")]
        [SerializeField] private float fadeInDuration = 0.5f;
        [SerializeField] private float fadeOutDuration = 1f;
        [SerializeField] private bool autoPlayOnReady = true;

        // Audio state
        private AudioClip _generatedClip;
        private bool _isUsingPreset = false;
        private float _fadeProgress = 0f;
        private float _targetVolume = 1f;
        private bool _isFading = false;

        /// <summary>
        /// Whether the audio is currently playing
        /// </summary>
        public bool IsPlaying => mainAudioSource != null && mainAudioSource.isPlaying;

        /// <summary>
        /// Whether we're using a preset (fallback) or generated audio
        /// </summary>
        public bool IsUsingPreset => _isUsingPreset;

        public AudioSource AudioSource => mainAudioSource;
        /// <summary>
        /// The current or pending audio clip
        /// </summary>
        public AudioClip CurrentClip => mainAudioSource?.clip;

        private void Awake()
        {
            if (stateMachine == null)
                stateMachine = GetComponent<QuarkStateMachine>();

            if (mainAudioSource == null)
                mainAudioSource = GetComponent<AudioSource>();

            if (stateMachine != null)
            {
                stateMachine.OnBecameDormant += OnBecameDormant;
                stateMachine.OnReady += OnReady;
                stateMachine.OnPlaying += OnPlaying;
                stateMachine.OnIdle += OnIdle;
                stateMachine.OnError += OnError;
                stateMachine.OnGrabbed += OnGrabbed;
                stateMachine.OnGenerating += OnGenerating;  // Changed from OnDropped
            }
        }

        private void OnDestroy()
        {
            if (stateMachine != null)
            {
                stateMachine.OnBecameDormant -= OnBecameDormant;
                stateMachine.OnReady -= OnReady;
                stateMachine.OnPlaying -= OnPlaying;
                stateMachine.OnIdle -= OnIdle;
                stateMachine.OnError -= OnError;
                stateMachine.OnGrabbed -= OnGrabbed;
                stateMachine.OnGenerating -= OnGenerating;  // Changed from OnDropped
            }
        }

        private void Update()
        {
            UpdateFade();
            MonitorAudioPlayback();
        }

        /// <summary>
        /// Monitor audio playback and auto-transition to Idle when audio finishes
        /// </summary>
        private void MonitorAudioPlayback()
        {
            // Only monitor when in Playing state
            if (stateMachine == null || !stateMachine.IsInState(QuarkLifecycleState.Playing))
                return;

            // Check if audio has stopped/finished
            if (mainAudioSource != null && !mainAudioSource.isPlaying)
            {
                // Audio finished - transition to Idle state
                Debug.Log("[QuarkAudioController] Audio finished - transitioning to Idle state");
                stateMachine.SetState(QuarkLifecycleState.Idle);
            }
        }

        /// <summary>
        /// Set the generated audio clip from the music pipeline
        /// </summary>
        public void SetGeneratedClip(AudioClip clip)
        {
            if (clip == null)
            {
                Debug.LogWarning("[QuarkAudioController] Received null clip, will use preset");
                UseFallbackPreset();
                return;
            }

            _generatedClip = clip;
            _isUsingPreset = false;

            Debug.Log($"[QuarkAudioController] Generated clip set: {clip.name} ({clip.length:F1}s)");

            // Don't assign to audio source yet - wait for Ready state
        }

        /// <summary>
        /// Use a fallback preset audio clip
        /// </summary>
        public void UseFallbackPreset()
        {
            if (presetClips == null || presetClips.Count == 0)
            {
                Debug.LogError("[QuarkAudioController] No preset clips available for fallback!");
                return;
            }

            int index = shufflePresets ? Random.Range(0, presetClips.Count) : 0;
            var clip = presetClips[index];

            if (mainAudioSource != null)
            {
                mainAudioSource.clip = clip;
            }

            _isUsingPreset = true;
            Debug.Log($"[QuarkAudioController] Using preset fallback: {clip.name}");
        }

        /// <summary>
        /// Start playing audio (call when Ready state)
        /// </summary>
        public void Play()
        {
            if (mainAudioSource == null) return;

            // Assign the appropriate clip
            if (!_isUsingPreset && _generatedClip != null)
            {
                mainAudioSource.clip = _generatedClip;
            }
            else if (mainAudioSource.clip == null)
            {
                UseFallbackPreset();
            }

            // Start playing with fade in
            mainAudioSource.volume = 0f;
            mainAudioSource.Play();
            StartFadeIn();

            Debug.Log($"[QuarkAudioController] ▶️ Playing: {mainAudioSource.clip?.name} (preset: {_isUsingPreset})");
        }

        /// <summary>
        /// Pause audio playback
        /// </summary>
        public void Pause()
        {
            if (mainAudioSource == null) return;

            StartFadeOut(() =>
            {
                mainAudioSource.Pause();
                Debug.Log("[QuarkAudioController] ⏸️ Paused");
            });
        }

        /// <summary>
        /// Resume audio playback
        /// </summary>
        public void Resume()
        {
            if (mainAudioSource == null) return;

            mainAudioSource.UnPause();
            StartFadeIn();
            Debug.Log("[QuarkAudioController] ▶️ Resumed");
        }

        /// <summary>
        /// Stop audio playback
        /// </summary>
        public void Stop()
        {
            if (mainAudioSource == null) return;

            StartFadeOut(() =>
            {
                mainAudioSource.Stop();
                Debug.Log("[QuarkAudioController] ⏹️ Stopped");
            });
        }

        /// <summary>
        /// Play pickup sound effect
        /// </summary>
        public void PlayPickupSfx()
        {
            if (pickupSfx != null)
            {
                pickupSfx.Play();
            }
        }

        /// <summary>
        /// Play drop sound effect
        /// </summary>
        public void PlayDropSfx()
        {
            if (dropSfx != null)
            {
                dropSfx.Play();
            }
        }

        // State handlers
        private void OnBecameDormant()
        {
            Debug.Log("[QuarkAudioController] Dormant state - audio will remain stopped");
            // No need to stop since PlayOnAwake is now 0
        }

        private void OnReady()
        {
            Debug.Log("[QuarkAudioController] Ready state - audio prepared");

            // Assign clip but don't play yet
            if (!_isUsingPreset && _generatedClip != null)
            {
                mainAudioSource.clip = _generatedClip;
            }

            // Only auto-play if we have generated audio (not on initial spawn)
            if (autoPlayOnReady && (_generatedClip != null || _isUsingPreset))
            {
                // Transition to Playing state
                stateMachine?.SetState(QuarkLifecycleState.Playing);
            }
        }

        private void OnPlaying()
        {
            Play();
        }

        private void OnIdle()
        {
            // Idle state is when audio has finished/stopped
            // QuarkVisualController's idleProfile defines the visual appearance
            Debug.Log("[QuarkAudioController] Entered Idle state - audio has stopped");
        }

        private void OnError(string errorMessage)
        {
            Debug.LogWarning($"[QuarkAudioController] Error occurred: {errorMessage} - using fallback");
            UseFallbackPreset();

            // Transition to Ready state with fallback audio
            stateMachine?.SetState(QuarkLifecycleState.Ready);
        }

        private void OnGrabbed()
        {
            PlayPickupSfx();

            // Pause music if playing
            if (IsPlaying)
            {
                Pause();
            }
        }

        private void OnGenerating()
        {
            PlayDropSfx();  // Play drop sound when entering Generating state (after release)
        }

        // Fade handling
        private System.Action _onFadeComplete;

        private void StartFadeIn()
        {
            _fadeProgress = 0f;
            _targetVolume = 1f;
            _isFading = true;
            _onFadeComplete = null;
        }

        private void StartFadeOut(System.Action onComplete = null)
        {
            _fadeProgress = 0f;
            _targetVolume = 0f;
            _isFading = true;
            _onFadeComplete = onComplete;
        }

        private void UpdateFade()
        {
            if (!_isFading || mainAudioSource == null) return;

            float duration = _targetVolume > 0.5f ? fadeInDuration : fadeOutDuration;
            _fadeProgress += Time.deltaTime / duration;

            if (_fadeProgress >= 1f)
            {
                _fadeProgress = 1f;
                _isFading = false;
                mainAudioSource.volume = _targetVolume;
                _onFadeComplete?.Invoke();
                _onFadeComplete = null;
            }
            else
            {
                float currentVolume = mainAudioSource.volume;
                mainAudioSource.volume = Mathf.Lerp(currentVolume, _targetVolume, _fadeProgress);
            }
        }

        /// <summary>
        /// Clear audio state (for reuse)
        /// </summary>
        public void Reset()
        {
            _generatedClip = null;
            _isUsingPreset = false;
            _isFading = false;

            if (mainAudioSource != null)
            {
                mainAudioSource.Stop();
                mainAudioSource.clip = null;
                mainAudioSource.volume = 1f;
            }
        }
    }
