using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenAI;
using OpenAI.Responses;
using UnityEngine;

namespace Jusvibes.Core
{
    /// <summary>
    /// Extended methods for CaptureInsightProcessor that support base64 images
    /// and voice transcription without file I/O
    /// </summary>
    public static class CaptureInsightProcessorExtended
    {
        /// <summary>
        /// Analyzes environment using base64-encoded images and optional voice transcription
        /// (No file I/O required - more efficient!)
        /// </summary>
        public static async Task<(VisualInsights insights, string musicPrompt)> AnalyzeEnvironmentWithContext(
            this CaptureInsightProcessor processor,
            byte[] imageBytes,
            string voiceTranscription = null,
            PipelineLogger logger = null)
        {
            if (imageBytes == null || imageBytes.Length == 0)
            {
                throw new OpenAIException("Image bytes are null or empty");
            }

            logger?.Info("🔍 Analyzing environment with context", new
            {
                imageSizeKB = imageBytes.Length / 1024,
                hasTranscription = !string.IsNullOrEmpty(voiceTranscription)
            });

            try
            {
                // 1. Extract color palette from image
                logger?.StartTimer("color_extraction");
                Color[] palette = await GetKMeansPaletteFromBytes(imageBytes, k: 5, sampleStep: 8, maxIterations: 10);
                logger?.StopTimer("color_extraction");

                var insights = new VisualInsights();
                insights.palette = palette;

                if (palette != null && palette.Length > 0)
                {
                    Color dominant = palette[0];
                    insights.primaryColor = BoostColor(dominant);
                    insights.secondaryColor = BoostColor(Complement(insights.primaryColor), 0.2f, 0.1f);
                    insights.averageColor = dominant;
                }
                else
                {
                    insights.primaryColor = Color.white;
                    insights.secondaryColor = Color.gray;
                    insights.averageColor = Color.black;
                }

                // 2. Get music prompt from OpenAI using base64 image
                logger?.StartTimer("openai_vision");
                string musicPrompt = await GetMusicPromptFromImageBase64(imageBytes, voiceTranscription, logger);
                logger?.StopTimer("openai_vision");

                logger?.Info("✅ Environment analysis complete", new { prompt = musicPrompt });

                return (insights, musicPrompt);
            }
            catch (Exception ex)
            {
                logger?.Error("Environment analysis failed", ex);
                throw;
            }
        }

        private static async Task<string> GetMusicPromptFromImageBase64(
            byte[] imageBytes,
            string voiceTranscription,
            PipelineLogger logger)
        {
            var api = ApiConfigManager.Instance.CreateOpenAIClient();

            // Convert to base64 data URI
            string base64Image = imageBytes.ToBase64DataUri("image/png");

            // Build prompt with optional transcription
            string systemPrompt = BuildSystemPrompt(voiceTranscription);

            logger?.Info("Sending vision request to OpenAI", new
            {
                imageSize = base64Image.Length,
                hasVoice = !string.IsNullOrEmpty(voiceTranscription)
            });

            var contents = new List<IResponseContent>
            {
                new OpenAI.Responses.TextContent(systemPrompt),
                new OpenAI.Responses.ImageContent(imageUrl: base64Image) // OpenAI accepts base64 data URIs
            };

            var input = new List<IResponseItem>
            {
                new Message(Role.User, contents.ToArray())
            };

            var request = new CreateResponseRequest(input: input, model: "gpt-4.1-mini");

            var response = await api.ResponsesEndpoint.CreateModelResponseAsync(request);
            var responseItem = response.Output.LastOrDefault();

            if (responseItem == null)
            {
                throw new OpenAIException("OpenAI returned no response");
            }

            string musicPrompt = responseItem.ToString();
            logger?.Info("OpenAI response received", new { promptLength = musicPrompt.Length });

            return musicPrompt;
        }

        private static string BuildSystemPrompt(string voiceTranscription)
        {
            string basePrompt = "Analyze the space's mood, lighting, textures, and season to guess what activity the user might be doing. " +
                               "Use this to create an instrumental ambient music prompt. Describe the atmosphere vividly, then suggest a " +
                               "flexible genre, instruments, and subtle nature or special effects. Keep the generated text concise (<500 characters).";

            if (!string.IsNullOrEmpty(voiceTranscription))
            {
                return $"{basePrompt}\n\nUser's voice description: \"{voiceTranscription}\"\n" +
                       "Incorporate their description into your music prompt.";
            }

            return basePrompt;
        }

        // K-means palette extraction from bytes
        private static async Task<Color[]> GetKMeansPaletteFromBytes(
            byte[] imageBytes,
            int k = 5,
            int sampleStep = 8,
            int maxIterations = 10)
        {
            if (imageBytes == null || imageBytes.Length == 0)
                return Array.Empty<Color>();

            // Decode texture on main thread
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(imageBytes);

            Color32[] pixels = tex.GetPixels32();
            UnityEngine.Object.Destroy(tex);

            // Sample pixels
            List<Vector3> samples = new List<Vector3>();
            for (int i = 0; i < pixels.Length; i += sampleStep)
            {
                Color32 c = pixels[i];
                samples.Add(new Vector3(c.r / 255f, c.g / 255f, c.b / 255f));
            }

            if (samples.Count == 0)
                return Array.Empty<Color>();

            // Run k-means on background thread
            return await Task.Run(() => RunKMeans(samples, k, maxIterations));
        }

        private static Color[] RunKMeans(List<Vector3> samples, int k, int maxIterations)
        {
            if (samples == null || samples.Count == 0 || k <= 0)
                return Array.Empty<Color>();

            k = Mathf.Min(k, samples.Count);

            Vector3[] centroids = new Vector3[k];
            int[] assignments = new int[samples.Count];
            System.Random rng = new System.Random();

            // Initialize centroids
            HashSet<int> used = new HashSet<int>();
            for (int i = 0; i < k; i++)
            {
                int idx;
                do { idx = rng.Next(samples.Count); }
                while (!used.Add(idx));
                centroids[i] = samples[idx];
            }

            // Iterate
            for (int iter = 0; iter < maxIterations; iter++)
            {
                bool changed = false;

                // Assign
                for (int i = 0; i < samples.Count; i++)
                {
                    Vector3 v = samples[i];
                    float bestDist = float.MaxValue;
                    int bestIndex = 0;

                    for (int c = 0; c < k; c++)
                    {
                        Vector3 diff = v - centroids[c];
                        float dist = diff.sqrMagnitude;
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            bestIndex = c;
                        }
                    }

                    if (assignments[i] != bestIndex)
                    {
                        assignments[i] = bestIndex;
                        changed = true;
                    }
                }

                if (!changed && iter > 0) break;

                // Update centroids
                Vector3[] newCentroids = new Vector3[k];
                int[] counts = new int[k];

                for (int i = 0; i < samples.Count; i++)
                {
                    int cluster = assignments[i];
                    newCentroids[cluster] += samples[i];
                    counts[cluster]++;
                }

                for (int c = 0; c < k; c++)
                {
                    if (counts[c] > 0)
                        newCentroids[c] /= counts[c];
                    else
                        newCentroids[c] = samples[rng.Next(samples.Count)];
                }

                centroids = newCentroids;
            }

            // Convert to colors
            Color[] palette = new Color[k];
            for (int i = 0; i < k; i++)
            {
                Vector3 v = centroids[i];
                palette[i] = new Color(v.x, v.y, v.z, 1f);
            }

            return palette;
        }

        private static Color BoostColor(Color c, float satBoost = 0.15f, float valBoost = 0.15f)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            s = Mathf.Clamp01(s + satBoost);
            v = Mathf.Clamp01(v + valBoost);
            return Color.HSVToRGB(h, s, v);
        }

        private static Color Complement(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            h = (h + 0.5f) % 1.0f;
            return Color.HSVToRGB(h, s, v);
        }
    }
}
