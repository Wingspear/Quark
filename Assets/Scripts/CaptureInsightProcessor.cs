using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Jusvibes.Core;
using OpenAI;
using OpenAI.Files;
using OpenAI.Responses;
using Sirenix.OdinInspector;
using UnityEngine;

[Serializable]
public class VisualInsights
{
    public Color averageColor;
    public Color[] palette;      // k-means palette

    // For visual system
    public Color primaryColor;
    public Color secondaryColor;
}

public class CaptureInsightProcessor : MonoBehaviour
{
    public async Task<string> FetchCaptureMusicInsights(int numCaptures = 1)
    {
        var api = ApiConfigManager.Instance.CreateOpenAIClient();

        List<FileResponse> files = new();
        for (int i = 0; i < numCaptures; i++)
        {
            var file = await api.FilesEndpoint.UploadFileAsync(
                Application.persistentDataPath + "/capture" + i + ".png",
                FilePurpose.Vision
            );
            files.Add(file);
        }

        var contents = new List<IResponseContent>
        {
            new OpenAI.Responses.TextContent(
                "Analyze the space’s mood, lighting, textures, and season to guess what activity the user might be doing. " +
                "Use this to create an instrumental ambient music prompt. Describe the atmosphere vividly, then suggest a " +
                "flexible genre, instruments, and any subtle nature sounds or special effects. Keep the generated text " +
                "concise (under 499 characters) and ensure it’s dynamic, immersive, and fitting to the space’s vibe."
            )
        };

        foreach (var f in files)
        {
            contents.Add(new OpenAI.Responses.ImageContent(fileId: f.Id));
        }
        
        var input = new List<IResponseItem>
        {
            new Message(
                Role.User,
                contents.ToArray()
            )
        };

        var request = new CreateResponseRequest(
            input: input,
            model: "gpt-4.1-mini"
        );

        var response = await api.ResponsesEndpoint.CreateModelResponseAsync(request);
        var responseItem = response.Output.LastOrDefault();

        if (responseItem != null)
        {
            Debug.Log(responseItem.ToString());
            response.PrintUsage();
        }

        return response;
    }
    
    public async Task<(VisualInsights insights, string musicPrompt)> FetchCaptureVisualInsights(int numCaptures = 1)
    {
        VisualInsights insights = new();
        string musicPrompt = null;

        try
        {
            // ---- 1. Load image bytes ----
            List<byte[]> allImageBytes = new();
            for (int i = 0; i < numCaptures; i++)
            {
                string path = Application.persistentDataPath + "/capture" + i + ".png";

                if (!File.Exists(path))
                {
                    Debug.LogWarning("Capture missing: " + path);
                    continue;
                }

                byte[] bytes = await File.ReadAllBytesAsync(path);
                allImageBytes.Add(bytes);
            }

            if (allImageBytes.Count == 0)
            {
                throw new Exception("No capture images found to process");
            }

            // ---- 2. Compute k-means palette ----
            Color[] palette = await GetKMeansPaletteAsync(
                allImageBytes,
                k: 5,
                sampleStep: 8,
                maxIterations: 10
            );

            insights.palette = palette;

            // ---- 3. Extract primary + secondary colors ----
            if (palette != null && palette.Length > 0)
            {
                // Dominant color = cluster 0
                Color dominant = palette[0];

                // Primary = boosted dominant
                insights.primaryColor = BoostColor(dominant);

                // Secondary = complementary accent
                insights.secondaryColor = BoostColor(Complement(insights.primaryColor), 0.2f, 0.1f);

                // For legacy compatibility, set averageColor = dominant
                insights.averageColor = dominant;
            }
            else
            {
                insights.primaryColor = Color.white;
                insights.secondaryColor = Color.gray;
                insights.averageColor = Color.black;
            }

            // ---- 4. OpenAI: Upload files + ask for prompt ----
            var api = ApiConfigManager.Instance.CreateOpenAIClient();

            List<FileResponse> files = new();
            for (int i = 0; i < numCaptures; i++)
            {
                string path = Application.persistentDataPath + "/capture" + i + ".png";
                if (!File.Exists(path)) continue;

                var file = await api.FilesEndpoint.UploadFileAsync(path, FilePurpose.Vision);
                files.Add(file);
            }

            if (files.Count == 0)
            {
                throw new Exception("Failed to upload any images to OpenAI");
            }

            var contents = new List<IResponseContent>
            {
                new OpenAI.Responses.TextContent(
                    "Analyze the space's mood, lighting, textures, and season to guess what activity the user might be doing. " +
                    "Use this to create an instrumental ambient music prompt. Describe the atmosphere vividly, then suggest a " +
                    "flexible genre, instruments, and subtle nature or special effects. Keep the generated text concise (<500 characters)."
                )
            };

            foreach (var f in files)
                contents.Add(new OpenAI.Responses.ImageContent(fileId: f.Id));

            var input = new List<IResponseItem>
            {
                new Message(Role.User, contents.ToArray())
            };

            var request = new CreateResponseRequest(input: input, model: "gpt-4.1-mini");

            var response = await api.ResponsesEndpoint.CreateModelResponseAsync(request);
            var responseItem = response.Output.LastOrDefault();

            if (responseItem != null)
            {
                musicPrompt = responseItem.ToString();
                Debug.Log("OpenAI Response: " + musicPrompt);
                response.PrintUsage();
            }
            else
            {
                throw new Exception("OpenAI returned no response");
            }

            return (insights, musicPrompt);
        }
        catch (Exception ex)
        {
            Debug.LogError("FetchCaptureVisualInsights failed: " + ex.Message);
            throw;
        }
    }

    
    private async Task<Color[]> GetKMeansPaletteAsync(
        List<byte[]> imagesBytes,
        int k = 5,
        int sampleStep = 8,
        int maxIterations = 10)
    {
        if (imagesBytes == null || imagesBytes.Count == 0)
            return Array.Empty<Color>();

        // --- MAIN THREAD: decode textures and sample pixels ---
        List<Vector3> samples = new List<Vector3>();

        foreach (var imageBytes in imagesBytes)
        {
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(imageBytes);               // must be on main thread

            Color32[] pixels = tex.GetPixels32();
            UnityEngine.Object.Destroy(tex);

            // Sample every Nth pixel to keep it fast
            for (int i = 0; i < pixels.Length; i += sampleStep)
            {
                Color32 c = pixels[i];
                // RGB in [0,1]
                samples.Add(new Vector3(c.r / 255f, c.g / 255f, c.b / 255f));
            }
        }

        if (samples.Count == 0)
            return Array.Empty<Color>();

        // --- BACKGROUND THREAD: run k-means ---
        return await Task.Run(() => RunKMeans(samples, k, maxIterations));
    }
    
    private Color[] RunKMeans(List<Vector3> samples, int k, int maxIterations)
    {
        if (samples == null || samples.Count == 0 || k <= 0)
            return Array.Empty<Color>();

        k = Mathf.Min(k, samples.Count);

        Vector3[] centroids = new Vector3[k];
        int[] assignments = new int[samples.Count];
        System.Random rng = new System.Random();

        // --- Initialize centroids with random sample points ---
        HashSet<int> used = new HashSet<int>();
        for (int i = 0; i < k; i++)
        {
            int idx;
            do
            {
                idx = rng.Next(samples.Count);
            }
            while (!used.Add(idx));

            centroids[i] = samples[idx];
        }

        // --- Iterate assign + update ---
        for (int iter = 0; iter < maxIterations; iter++)
        {
            bool changed = false;

            // Assign each sample to the closest centroid
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

            if (!changed && iter > 0)
            {
                // Converged
                break;
            }

            // Recompute centroids
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
                {
                    newCentroids[c] /= counts[c];
                }
                else
                {
                    // Reinitialize empty cluster to a random sample
                    newCentroids[c] = samples[rng.Next(samples.Count)];
                }
            }

            centroids = newCentroids;
        }

        // Convert centroids to Unity Colors
        Color[] palette = new Color[k];
        for (int i = 0; i < k; i++)
        {
            Vector3 v = centroids[i];
            palette[i] = new Color(v.x, v.y, v.z, 1f);
        }

        return palette;
    }
    
    private Color BoostColor(Color c, float satBoost = 0.15f, float valBoost = 0.15f)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        s = Mathf.Clamp01(s + satBoost);
        v = Mathf.Clamp01(v + valBoost);
        return Color.HSVToRGB(h, s, v);
    }

    private Color Complement(Color c)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        h = (h + 0.5f) % 1.0f; // rotate hue 180°
        return Color.HSVToRGB(h, s, v);
    }
}