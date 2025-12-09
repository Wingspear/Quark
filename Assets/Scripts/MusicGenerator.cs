using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jusvibes.Core;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Networking;

public class MusicGenerator : MonoBehaviour
{
    [Header("Suno API Settings")]
    [SerializeField] private string callBackUrl = "https://dummy-url.com/callback";
    [SerializeField] private string model = "V5";

    [Header("Polling Configuration")]
    [SerializeField] private int pollIntervalMs = 3000;
    [SerializeField] private int maxPollAttempts = 200; // 10 minutes at 3s intervals

    [TextArea]
    public string prompt = "";

    private const string GenerateUrl = "https://api.sunoapi.org/api/v1/generate";
    private const string RecordInfoUrl = "https://api.sunoapi.org/api/v1/generate/record-info";

    // ---------- Odin button entry (for inspector testing) ----------

    [Button(30)]
    public async void TestGenerate(AudioSource audioSource, string prompt)
    {
        await GenerateMusic(audioSource, prompt);
    }

    // ---------- Public async API ----------

    /// <summary>
    /// Full pipeline: load config → call Suno → poll → stream → play.
    /// This Task completes only when audio is playing (or if something fails).
    /// </summary>
    public async Task GenerateMusic(AudioSource audioSource, string userPrompt, PipelineLogger logger = null, CancellationToken cancellationToken = default)
    {
        logger?.Info("Retrieving Suno API configuration");
        await GenerateAndPlayAsync(audioSource, userPrompt, logger, cancellationToken);
    }

    // ---------- DTOs ----------

    [Serializable]
    private class GenerateRequestBody
    {
        public bool customMode;
        public bool instrumental;
        public string model;
        public string callBackUrl;
        public string prompt;

        public string style = null;
        public string title = null;
        public string personaId = null;
        public string negativeTags = null;
        public string vocalGender = null;
        public float styleWeight = 0f;
        public float weirdnessConstraint = 0f;
        public float audioWeight = 0f;
    }

    [Serializable]
    private class GenerateResponseData
    {
        public string taskId;
    }

    [Serializable]
    private class GenerateResponse
    {
        public int code;
        public string msg;
        public GenerateResponseData data;
    }

    [Serializable]
    private class SunoTrack
    {
        public string audioUrl;
        public string streamAudioUrl;
        public string title;
        public string id;
    }

    [Serializable]
    private class RecordInfoInner
    {
        public SunoTrack[] sunoData;
    }

    [Serializable]
    private class RecordInfoData
    {
        public string taskId;
        public string status;
        public RecordInfoInner response;
    }

    [Serializable]
    private class RecordInfoResponse
    {
        public int code;
        public string msg;
        public RecordInfoData data;
    }
    
    // ---------- Async pipeline ----------

    private async Task GenerateAndPlayAsync(AudioSource audioSource, string userPrompt, PipelineLogger logger, CancellationToken cancellationToken)
    {
        logger?.Info("Starting Suno music generation", new { prompt = userPrompt });

        // 1. POST /generate
        string taskId = await CallGenerateEndpointAsync(userPrompt, logger, cancellationToken);
        if (string.IsNullOrEmpty(taskId))
        {
            throw new SunoApiException("Failed to get taskId", correlationId: logger?.CorrelationId);
        }

        logger?.Info("Suno task created", new { taskId });

        // 2. Poll until FIRST_SUCCESS (audio ready)
        string streamUrl = await GetMusicUrlAsync(taskId, logger, cancellationToken);
        if (string.IsNullOrEmpty(streamUrl))
        {
            throw new SunoApiException("Failed to get stream URL", taskId: taskId, correlationId: logger?.CorrelationId);
        }

        logger?.Info("Stream URL received", new { streamUrl });

        // 3. Stream & play
        await StreamAndPlayAsync(audioSource, streamUrl, logger, cancellationToken);
    }

    private async Task<string> CallGenerateEndpointAsync(string userPrompt, PipelineLogger logger, CancellationToken cancellationToken)
    {
        string apiKey = ApiConfigManager.Instance.GetSunoApiKey();

        var body = new GenerateRequestBody
        {
            customMode = true,
            instrumental = true,
            model = model,
            callBackUrl = callBackUrl,
            prompt = userPrompt
        };

        string json = JsonUtility.ToJson(body);
        logger?.Info("Calling Suno generate endpoint", new { model, promptLength = userPrompt.Length });

        using (var request = new UnityWebRequest(GenerateUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);

            await AwaitRequest(request, cancellationToken);

            if (request.result != UnityWebRequest.Result.Success)
            {
                logger?.Error("Suno generate endpoint failed", null, new { error = request.error, response = request.downloadHandler.text });
                throw new SunoApiException($"Generate request failed: {request.error}", correlationId: logger?.CorrelationId);
            }

            var resp = JsonUtility.FromJson<GenerateResponse>(request.downloadHandler.text);
            if (resp == null || resp.code != 200 || resp.data == null)
            {
                var errorMsg = resp != null ? resp.msg : "null response";
                logger?.Error("Suno API error", null, new { code = resp?.code, message = errorMsg });
                throw new SunoApiException($"API returned error: {errorMsg}", resp?.code, correlationId: logger?.CorrelationId);
            }

            string taskId = resp.data.taskId;
            logger?.Info("Task ID received", new { taskId });
            return taskId;
        }
    }

    private async Task<string> GetMusicUrlAsync(string taskId, PipelineLogger logger, CancellationToken cancellationToken)
    {
        string apiKey = ApiConfigManager.Instance.GetSunoApiKey();
        string url = $"{RecordInfoUrl}?taskId={taskId}";
        int pollAttempt = 0;

        logger?.Info("Starting to poll for music generation status", new { taskId, maxAttempts = maxPollAttempts });

        while (pollAttempt < maxPollAttempts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            pollAttempt++;

            using (var req = UnityWebRequest.Get(url))
            {
                req.SetRequestHeader("Authorization", "Bearer " + apiKey);

                await AwaitRequest(req, cancellationToken);

                if (req.result != UnityWebRequest.Result.Success)
                {
                    logger?.Warning($"Poll attempt {pollAttempt} failed", new { error = req.error });

                    // Continue polling on transient errors
                    await Task.Delay(pollIntervalMs, cancellationToken);
                    continue;
                }

                var resp = JsonUtility.FromJson<RecordInfoResponse>(req.downloadHandler.text);

                if (resp == null || resp.data == null)
                {
                    logger?.Error("Invalid poll response", null, new { response = req.downloadHandler.text });
                    throw new SunoApiException("Invalid poll response format", taskId: taskId, correlationId: logger?.CorrelationId);
                }

                logger?.Info($"Poll attempt {pollAttempt}/{maxPollAttempts}", new { status = resp.data.status });

                // Check for failure
                if (resp.data.status == "FAILED")
                {
                    throw new SunoApiException("Music generation failed on Suno side", taskId: taskId, correlationId: logger?.CorrelationId);
                }

                // Check for success
                if (resp.data.status == "FIRST_SUCCESS" || resp.data.status == "TEXT_SUCCESS")
                {
                    if (resp.data.response?.sunoData != null && resp.data.response.sunoData.Length > 0)
                    {
                        string streamUrl = resp.data.response.sunoData[0].streamAudioUrl;
                        logger?.Info("🎵 Stream URL ready", new { streamUrl, pollAttempts = pollAttempt });
                        return streamUrl;
                    }
                    else
                    {
                        throw new SunoApiException("Success status but no audio data", taskId: taskId, correlationId: logger?.CorrelationId);
                    }
                }
            }

            // Wait between polls
            await Task.Delay(pollIntervalMs, cancellationToken);
        }

        throw new PipelineTimeoutException("Suno", maxPollAttempts * pollIntervalMs, logger?.CorrelationId);
    }

    private async Task StreamAndPlayAsync(AudioSource audioSource, string url, PipelineLogger logger, CancellationToken cancellationToken)
    {
        if (audioSource == null)
        {
            throw new AudioStreamException("AudioSource is null", url, logger?.CorrelationId);
        }

        logger?.Info("Starting audio stream", new { url });

        using (var req = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG))
        {
            ((DownloadHandlerAudioClip)req.downloadHandler).streamAudio = true;

            await AwaitRequest(req, cancellationToken);

            if (req.result != UnityWebRequest.Result.Success)
            {
                logger?.Error("Audio stream failed", null, new { error = req.error, url });
                throw new AudioStreamException($"Failed to stream audio: {req.error}", url, logger?.CorrelationId);
            }

            var clip = DownloadHandlerAudioClip.GetContent(req);

            if (clip == null)
            {
                logger?.Error("Failed to decode audio clip", null, new { url });
                throw new AudioStreamException("Failed to decode audio clip", url, logger?.CorrelationId);
            }

            audioSource.clip = clip;
            audioSource.Play();

            logger?.Info("▶️ Playing generated track", new { duration = clip.length, frequency = clip.frequency });
        }
    }

    // ---------- Helper: await UnityWebRequest ----------

    private static Task AwaitRequest(UnityWebRequest request, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<bool>();
        var op = request.SendWebRequest();

        cancellationToken.Register(() =>
        {
            request.Abort();
            tcs.TrySetCanceled();
        });

        op.completed += _ => tcs.TrySetResult(true);

        return tcs.Task;
    }
}
