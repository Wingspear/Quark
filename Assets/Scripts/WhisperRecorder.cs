using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Jusvibes.Core;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

public class WhisperRecorder : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private int recordDurationSeconds = 30;
    [SerializeField] private int sampleRate = 44100;

    private bool isRecording = false;
    private const string WhisperUrl = "https://api.openai.com/v1/audio/transcriptions";

    // Start capturing mic audio
    public void BeginListening()
    {
        if (isRecording) return;

        if (audioSource == null)
        {
            Debug.LogError("WhisperRecorder: AudioSource is not assigned.");
            return;
        }

        audioSource.clip = Microphone.Start(
            deviceName: null,
            loop: false,
            lengthSec: recordDurationSeconds,
            frequency: sampleRate
        );

        isRecording = true;
        Debug.Log("BeginListening: started microphone.");
    }

    // Stop mic + return Task that completes when Whisper reply arrives
    public async Task<string> EndListeningAsync()
    {
        if (!isRecording)
            return string.Empty;

        Microphone.End(null);
        isRecording = false;

        if (audioSource.clip == null)
        {
            Debug.LogError("WhisperRecorder: No clip recorded.");
            return string.Empty;
        }

        // Encode audio to WAV incrementally across multiple frames to avoid frame drops
        byte[] wavBytes = await EncodeToWavAsync(audioSource.clip);
        
        // Send to Whisper API (async network call)
        string transcription = await SendToWhisperAsync(wavBytes);
        return transcription ?? string.Empty;
    }

    // Wrap coroutine in Task
    private Task<string> SendToWhisperAsync(byte[] wavBytes)
    {
        var tcs = new TaskCompletionSource<string>();
        StartCoroutine(SendToWhisperCoroutine(wavBytes, tcs));
        return tcs.Task;
    }

    private IEnumerator SendToWhisperCoroutine(byte[] wavBytes, TaskCompletionSource<string> tcs)
    {
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", wavBytes, "audio.wav", "audio/wav");
        form.AddField("model", "whisper-1");

        using (UnityWebRequest www = UnityWebRequest.Post(WhisperUrl, form))
        {
            www.SetRequestHeader("Authorization", "Bearer " + ApiConfigManager.Instance.GetOpenAIConfig().ApiKey);

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Whisper error: " + www.error);
                Debug.LogError("Response: " + www.downloadHandler.text);
                tcs.SetResult(string.Empty);
                yield break;
            }

            string json = www.downloadHandler.text;

            WhisperResponse data = null;
            try
            {
                data = JsonConvert.DeserializeObject<WhisperResponse>(json);
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to deserialize Whisper response: " + e);
                Debug.LogError("JSON: " + json);
            }

            tcs.SetResult(data?.text ?? string.Empty);
        }
    }

    // ------ WAV ENCODER (16-bit PCM) - Incremental reading across frames ------
    private async Task<byte[]> EncodeToWavAsync(AudioClip clip)
    {
        int channels = clip.channels;
        int sampleRate = clip.frequency;
        int totalSampleFrames = clip.samples; // Total frames (not interleaved samples)
        short bitsPerSample = 16;
        const int sampleFramesPerChunk = 22050; // Process ~0.5 seconds per frame (at 44.1kHz)

        // Defer to next frame to allow state transition to complete
        await Task.Yield();

        // Read and convert samples incrementally across multiple frames
        using (var mem = new MemoryStream())
        using (var writer = new BinaryWriter(mem, Encoding.ASCII))
        {
            // Write WAV header (we'll update chunk size later)
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            long chunkSizePos = mem.Position;
            writer.Write(0); // Placeholder for chunk size
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));

            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);               // Subchunk1Size
            writer.Write((short)1);         // AudioFormat (PCM)
            writer.Write((short)channels);  // NumChannels
            writer.Write(sampleRate);       // SampleRate
            int byteRate = sampleRate * channels * bitsPerSample / 8;
            writer.Write(byteRate);         // ByteRate
            short blockAlign = (short)(channels * bitsPerSample / 8);
            writer.Write(blockAlign);       // BlockAlign
            writer.Write(bitsPerSample);    // BitsPerSample

            writer.Write(Encoding.ASCII.GetBytes("data"));
            long dataSizePos = mem.Position;
            writer.Write(0); // Placeholder for data size

            // Process samples in chunks across multiple frames
            // GetData() reads interleaved samples: [L, R, L, R, ...] for stereo
            float[] chunkSamples = new float[sampleFramesPerChunk * channels];
            int sampleFramesProcessed = 0;

            const int rescale = 32767;

            while (sampleFramesProcessed < totalSampleFrames)
            {
                // Yield every chunk to spread work across frames
                await Task.Yield();

                int sampleFramesToRead = Mathf.Min(sampleFramesPerChunk, totalSampleFrames - sampleFramesProcessed);
                
                // Read chunk of samples starting from sampleFramesProcessed
                // GetData fills the entire array, so we need to resize if last chunk is smaller
                if (sampleFramesToRead < sampleFramesPerChunk)
                {
                    chunkSamples = new float[sampleFramesToRead * channels];
                }
                
                clip.GetData(chunkSamples, sampleFramesProcessed);

                // Convert chunk to 16-bit PCM and write directly (interleaved)
                for (int i = 0; i < chunkSamples.Length; i++)
                {
                    short v = (short)Mathf.Clamp(chunkSamples[i] * rescale, short.MinValue, short.MaxValue);
                    writer.Write(v);
                }

                sampleFramesProcessed += sampleFramesToRead;
            }

            // Update chunk sizes now that we know the actual data size
            long dataSize = mem.Position - dataSizePos - 4; // Subtract the 4 bytes for the size field itself
            int chunkSize = (int)(36 + dataSize);

            mem.Position = chunkSizePos;
            writer.Write(chunkSize);

            mem.Position = dataSizePos;
            writer.Write((int)dataSize);

            writer.Flush();
            return mem.ToArray();
        }
    }

    [Serializable]
    private class WhisperResponse
    {
        [JsonProperty("text")]
        public string text { get; set; }
    }
}
