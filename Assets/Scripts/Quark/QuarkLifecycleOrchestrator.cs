using System;
using System.Threading;
using System.Threading.Tasks;
using Jusvibes.Core;
using UnityEngine;

/// <summary>
/// Orchestrates the complete Quark lifecycle from grab to audio playback.
/// Coordinates between capture, voice recording, OpenAI, Suno, and audio.
/// </summary>
public class QuarkLifecycleOrchestrator : MonoBehaviour
    {
        [Header("State Machine")]
        [SerializeField] private QuarkStateMachine stateMachine;

        [Header("Controllers")]
        [SerializeField] private QuarkVisualController visualController;
        [SerializeField] private QuarkAudioController audioController;

        [Header("Pipeline Dependencies")]
        [SerializeField] private CaptureController captureController;
        [SerializeField] private WhisperRecorder whisperRecorder;
        [SerializeField] private CaptureInsightProcessor insightProcessor;
        [SerializeField] private MusicGenerator musicGenerator;

        [Header("Settings")]
        [SerializeField] private bool captureOnGrab = true;
        [SerializeField] private bool recordVoiceOnGrab = true;
        [SerializeField] private int maxRetries = 3;
        [SerializeField] private float timeoutSeconds = 300f;

        private PipelineLogger _logger;
        private CancellationTokenSource _cts;
        private byte[] _capturedImageBytes;
        private string _voiceTranscription;

        /// <summary>
        /// Event fired when pipeline completes (success or fallback)
        /// </summary>
        public event Action<bool> OnPipelineComplete; // true = generated, false = preset

        private void Awake()
        {
            if (stateMachine == null)
                stateMachine = GetComponent<QuarkStateMachine>();

            if (visualController == null)
                visualController = GetComponent<QuarkVisualController>();

            if (audioController == null)
                audioController = GetComponent<QuarkAudioController>();

            // Subscribe to state machine events
            if (stateMachine != null)
            {
                stateMachine.OnGrabbed += HandleGrabbed;
                stateMachine.OnGenerating += HandleGenerating;
            }
        }

        /// <summary>
        /// Inject pipeline dependencies from scene (called by QuarkManager after spawning)
        /// </summary>
        public void InjectDependencies(
            CaptureController capture,
            WhisperRecorder whisper,
            CaptureInsightProcessor insight,
            MusicGenerator music)
        {
            captureController = capture;
            whisperRecorder = whisper;
            insightProcessor = insight;
            musicGenerator = music;

            Debug.Log("[QuarkLifecycleOrchestrator] Dependencies injected");
        }

        private void OnDestroy()
        {
            if (stateMachine != null)
            {
                stateMachine.OnGrabbed -= HandleGrabbed;
                stateMachine.OnGenerating -= HandleGenerating;
            }

            CancelPipeline();
        }

        /// <summary>
        /// Notify orchestrator that user grabbed the Quark
        /// </summary>
        public void NotifyGrabbed(bool isFirstGrab)
        {
            if (isFirstGrab)
            {
                stateMachine?.SetState(QuarkLifecycleState.Grabbed);
            }
            else
            {
                // Already initialized - just transition state
                stateMachine?.SetState(QuarkLifecycleState.Grabbed);
            }
        }

        /// <summary>
        /// Notify orchestrator that user released the Quark
        /// </summary>
        public void NotifyReleased()
        {
            stateMachine?.SetState(QuarkLifecycleState.Generating);
        }

        /// <summary>
        /// Cancel current pipeline operation
        /// </summary>
        public void CancelPipeline()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private void HandleGrabbed()
        {
            // Only start capture on first grab
            if (stateMachine.HasAudio) return;

            _logger = new PipelineLogger("QuarkLifecycle");
            _cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

            _logger.Info("✊ Quark grabbed - starting capture");

            // Start voice recording (non-blocking)
            if (recordVoiceOnGrab && whisperRecorder != null)
            {
                _logger.Info("🎤 Starting voice recording");
                whisperRecorder.BeginListening();
            }
        }

        private async void HandleGenerating()
        {
            // Only process on first generation
            if (stateMachine.HasAudio) return;

            _logger?.Info("🎵 Generating music - capturing, analyzing, and generating");

            try
            {
                await CaptureAndGenerate();
            }
            catch (Exception ex)
            {
                _logger?.Error("Generation phase failed", ex);
                HandlePipelineError(ex.Message);
            }
        }

        private async Task CaptureAndGenerate()
        {
            // Capture image (if not already captured)
            if (captureOnGrab && captureController != null)
            {
                _logger.StartTimer("capture");
                try
                {
                    _capturedImageBytes = await captureController.CapturePhotoAsBytes();
                    _logger.StopTimer("capture");
                    _logger.Info("📸 Image captured", new { sizeKB = _capturedImageBytes.Length / 1024 });
                }
                catch (Exception ex)
                {
                    _logger.Warning("Image capture failed, continuing without image", new { error = ex.Message });
                    _capturedImageBytes = null;
                }
            }

            // Stop voice recording and get transcription
            if (recordVoiceOnGrab && whisperRecorder != null)
            {
                _logger.StartTimer("transcription");
                _voiceTranscription = await whisperRecorder.EndListeningAsync();
                _logger.StopTimer("transcription");
                _logger.Info("🎤 Transcription complete", new { text = _voiceTranscription });
            }

            // Analyze with OpenAI
            _logger.StartTimer("openai");
            var (insights, musicPrompt) = await AnalyzeEnvironment();
            _logger.StopTimer("openai");

            if (string.IsNullOrEmpty(musicPrompt))
            {
                throw new OpenAIException("Failed to get music prompt from OpenAI");
            }

            _logger.Info("🔍 Analysis complete", new { prompt = musicPrompt });

            // Inject colors into visual controller
            if (visualController != null && insights != null)
            {
                visualController.InjectColors(insights.primaryColor, insights.secondaryColor);
            }

            // Generate music with Suno
            _logger.StartTimer("suno");
            var clip = await GenerateMusic(musicPrompt);
            _logger.StopTimer("suno");

            if (clip == null)
            {
                throw new SunoApiException("Failed to generate audio from Suno");
            }

            _logger.Info("🎵 Music generated", new { duration = clip.length });

            // Set audio and transition to Ready
            if (audioController != null)
            {
                audioController.SetGeneratedClip(clip);
            }

            stateMachine.SetState(QuarkLifecycleState.Ready);
            OnPipelineComplete?.Invoke(true);

            _logger.Info("✅ Pipeline complete - Quark ready to play");
        }

        private async Task<(VisualInsights, string)> AnalyzeEnvironment()
        {
            if (insightProcessor == null)
            {
                throw new Exception("CaptureInsightProcessor not assigned");
            }

            int attempts = 0;
            Exception lastException = null;

            while (attempts < maxRetries)
            {
                attempts++;
                _cts.Token.ThrowIfCancellationRequested();

                try
                {
                    if (_capturedImageBytes != null && _capturedImageBytes.Length > 0)
                    {
                        // Use base64 path (faster)
                        return await insightProcessor.AnalyzeEnvironmentWithContext(
                            _capturedImageBytes,
                            _voiceTranscription,
                            _logger
                        );
                    }
                    else
                    {
                        // Fallback to file-based path
                        return await insightProcessor.FetchCaptureVisualInsights(1);
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.Warning($"OpenAI attempt {attempts}/{maxRetries} failed", new { error = ex.Message });

                    if (attempts < maxRetries)
                    {
                        await Task.Delay(1000 * attempts, _cts.Token); // Exponential backoff
                    }
                }
            }

            throw lastException ?? new Exception("Failed to analyze environment");
        }

        private async Task<AudioClip> GenerateMusic(string prompt)
        {
            if (musicGenerator == null)
            {
                throw new Exception("MusicGenerator not assigned");
            }

            int attempts = 0;
            Exception lastException = null;

            while (attempts < maxRetries)
            {
                attempts++;
                _cts.Token.ThrowIfCancellationRequested();

                try
                {
                    // Create a temporary AudioSource to receive the clip
                    var tempAudio = audioController?.AudioSource;
                    if (tempAudio == null)
                    {
                        throw new Exception("No AudioSource available for music generation");
                    }

                    await musicGenerator.GenerateMusic(tempAudio, prompt, _logger, _cts.Token);

                    return tempAudio.clip;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.Warning($"Suno attempt {attempts}/{maxRetries} failed", new { error = ex.Message });

                    if (attempts < maxRetries)
                    {
                        await Task.Delay(2000 * attempts, _cts.Token); // Exponential backoff
                    }
                }
            }

            throw lastException ?? new Exception("Failed to generate music");
        }

        private void HandlePipelineError(string errorMessage)
        {
            _logger?.Error($"Pipeline error: {errorMessage}");

            // Transition to Error state - AudioController will use fallback
            stateMachine?.SetState(QuarkLifecycleState.Error, errorMessage);
            OnPipelineComplete?.Invoke(false);
        }

        /// <summary>
        /// Reset orchestrator state (for reuse)
        /// </summary>
        public void Reset()
        {
            CancelPipeline();
            _capturedImageBytes = null;
            _voiceTranscription = null;
            _logger = null;
        }
    }
