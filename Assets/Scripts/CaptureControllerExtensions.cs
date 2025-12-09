using System;
using System.Threading.Tasks;
using Jusvibes.Core;
using Meta.XR;
using UnityEngine;

/// <summary>
/// Extension methods for CaptureController to support base64 image encoding
/// (avoids file I/O overhead)
/// </summary>
public static class CaptureControllerExtensions
{
    /// <summary>
    /// Captures camera image and returns as PNG byte array (no file I/O)
    /// </summary>
    public static async Task<byte[]> CapturePhotoAsBytes(this CaptureController controller)
    {
        var camAccess = controller.GetComponent<PassthroughCameraAccess>();
        if (camAccess == null)
        {
            throw new CaptureException("PassthroughCameraAccess component not found");
        }

        return await camAccess.GetCurrentCameraImageBytesAsync();
    }

    /// <summary>
    /// Gets current camera frame as JPG byte array (optimized for performance)
    /// </summary>
    /// <param name="maxWidth">Maximum width for downscaling (default 1024, set to 0 to disable)</param>
    /// <param name="jpgQuality">JPG quality 1-100 (default 85, lower = faster but lower quality)</param>
    public static async Task<byte[]> GetCurrentCameraImageBytesAsync(
        this PassthroughCameraAccess camera, 
        int maxWidth = 1024, 
        int jpgQuality = 85)
    {
        if (!camera.IsPlaying)
        {
            throw new CaptureException("Camera is not playing - no frame available");
        }

        var tex = camera.GetTexture() as Texture2D;
        if (tex == null)
        {
            throw new CaptureException("Camera texture is null or not a Texture2D");
        }

        if (tex.width == 0 || tex.height == 0)
        {
            throw new CaptureException($"Invalid texture dimensions: {tex.width}x{tex.height}");
        }

        // Defer expensive operations to next frame to avoid frame drops
        await Task.Yield();

        // Calculate downscaled dimensions (maintain aspect ratio)
        int targetWidth = tex.width;
        int targetHeight = tex.height;
        
        if (maxWidth > 0 && tex.width > maxWidth)
        {
            float scale = (float)maxWidth / tex.width;
            targetWidth = maxWidth;
            targetHeight = Mathf.RoundToInt(tex.height * scale);
        }

        // Step 1: Copy original texture (expensive, but necessary)
        await Task.Yield(); // Spread work across frames
        Texture2D fullCopy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
        fullCopy.SetPixels32(tex.GetPixels32());
        fullCopy.Apply();

        // Step 2: Downscale if needed (much faster than encoding full resolution)
        Texture2D resized = fullCopy;
        if (targetWidth != tex.width || targetHeight != tex.height)
        {
            await Task.Yield(); // Another frame for downscaling
            resized = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
            // Use Graphics.Blit for faster downscaling (GPU-accelerated)
            RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight);
            Graphics.Blit(fullCopy, rt);
            RenderTexture.active = rt;
            resized.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            resized.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            UnityEngine.Object.Destroy(fullCopy);
        }

        // Step 3: Encode to JPG (faster than PNG, smaller file size)
        await Task.Yield(); // Another frame for encoding
        byte[] bytes = resized.EncodeToJPG(jpgQuality);
        UnityEngine.Object.Destroy(resized);

        if (bytes == null || bytes.Length == 0)
        {
            throw new CaptureException("Failed to encode texture to JPG bytes");
        }

        Debug.Log($"✅ Captured camera image: {bytes.Length} bytes ({bytes.Length / 1024}KB) from {tex.width}x{tex.height} → {targetWidth}x{targetHeight}");

        return bytes;
    }

    /// <summary>
    /// Converts byte array to base64 data URI for OpenAI Vision API
    /// </summary>
    public static string ToBase64DataUri(this byte[] imageBytes, string mimeType = "image/jpeg")
    {
        if (imageBytes == null || imageBytes.Length == 0)
        {
            throw new ArgumentException("Image bytes are null or empty");
        }

        string base64 = Convert.ToBase64String(imageBytes);
        return $"data:{mimeType};base64,{base64}";
    }
}
