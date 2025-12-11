using System;
using System.Threading.Tasks;
using UnityEngine;
using Whisper;
using Whisper.Utils; // adjust if your WhisperManager lives in a different namespace

public class WhisperRecorder : MonoBehaviour
{
    [Header("Whisper / Mic")]
    [SerializeField] private WhisperManager whisper;          // assign in Inspector
    [SerializeField] private MicrophoneRecord microphoneRecord; // assign in Inspector

    [Header("Settings (optional)")]
    [SerializeField] private bool translateToEnglish = false; // like sample's translateToggle
    [SerializeField] private string language = "auto";        // or "en", "vi", etc.

    private bool _isRecording;
    private TaskCompletionSource<string> _currentTcs;

    private void Awake()
    {
        if (whisper == null)
        {
            whisper = FindObjectOfType<WhisperManager>();
        }

        if (microphoneRecord == null)
        {
            microphoneRecord = FindObjectOfType<MicrophoneRecord>();
        }

        if (whisper == null || microphoneRecord == null)
        {
            Debug.LogError("WhisperRecorder: WhisperManager or MicrophoneRecord is not assigned.");
            return;
        }

        // Sync initial settings
        whisper.translateToEnglish = translateToEnglish;
        whisper.language = language;

        // Subscribe to mic stop event so we get the AudioChunk
        microphoneRecord.OnRecordStop += OnRecordStop;
    }

    private void OnDestroy()
    {
        if (microphoneRecord != null)
        {
            microphoneRecord.OnRecordStop -= OnRecordStop;
        }
    }

    /// <summary>
    /// Starts recording from MicrophoneRecord (no direct Unity Microphone calls here).
    /// </summary>
    public void BeginListening()
    {
        if (_isRecording)
            return;

        if (microphoneRecord == null)
        {
            Debug.LogError("WhisperRecorder: MicrophoneRecord is not assigned.");
            return;
        }

        microphoneRecord.StartRecord();
        _isRecording = true;
        Debug.Log("WhisperRecorder: BeginListening (MicrophoneRecord.StartRecord).");
    }

    /// <summary>
    /// Stops recording and returns a Task that completes once Whisper finishes transcribing.
    /// </summary>
    public Task<string> EndListeningAsync()
    {
        if (!_isRecording)
        {
            // nothing was recording; just return empty
            return Task.FromResult(string.Empty);
        }

        if (microphoneRecord == null)
        {
            Debug.LogError("WhisperRecorder: MicrophoneRecord is not assigned.");
            return Task.FromResult(string.Empty);
        }

        if (_currentTcs != null)
        {
            Debug.LogWarning("WhisperRecorder: EndListeningAsync called while a transcription is still in progress.");
            // You can decide whether to queue another or just return the existing one.
            return _currentTcs.Task;
        }

        _currentTcs = new TaskCompletionSource<string>();

        // Triggers MicrophoneRecord.OnRecordStop(AudioChunk) later
        microphoneRecord.StopRecord();
        _isRecording = false;

        return _currentTcs.Task;
    }

    /// <summary>
    /// Called by MicrophoneRecord when recording stops; runs Whisper locally and completes the TCS.
    /// </summary>
    private async void OnRecordStop(AudioChunk audio)
    {
        if (_currentTcs == null)
        {
            // Might have been stopped outside EndListeningAsync; just ignore or log.
            Debug.Log("WhisperRecorder: OnRecordStop received but no pending TaskCompletionSource.");
            return;
        }

        try
        {
            if (whisper == null)
            {
                Debug.LogError("WhisperRecorder: WhisperManager is not assigned.");
                _currentTcs.TrySetResult(string.Empty);
                return;
            }

            // Local inference – no HTTP, no WAV encoding.
            var res = await whisper.GetTextAsync(audio.Data, audio.Frequency, audio.Channels);

            if (res == null)
            {
                Debug.LogWarning("WhisperRecorder: Whisper returned null result.");
                _currentTcs.TrySetResult(string.Empty);
            }
            else
            {
                _currentTcs.TrySetResult(res.Result ?? string.Empty);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"WhisperRecorder: Error during transcription: {e}");
            _currentTcs.TrySetResult(string.Empty);
        }
        finally
        {
            _currentTcs = null;
        }
    }

    // Optional: expose these if you want to change them at runtime
    public void SetLanguage(string lang)
    {
        language = lang;
        if (whisper != null) whisper.language = lang;
    }

    public void SetTranslateToEnglish(bool translate)
    {
        translateToEnglish = translate;
        if (whisper != null) whisper.translateToEnglish = translate;
    }
}
