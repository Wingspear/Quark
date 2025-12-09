using System;
using System.IO;
using System.Threading.Tasks;
using Jusvibes.Core;
using Meta.XR;
using Sirenix.OdinInspector;
using UnityEngine;

public static class PassthroughCameraExtensions
{
    public static async Task<bool> SaveCurrentCameraImageAsync(this PassthroughCameraAccess camera, string filePath,
        bool jpg = false)
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

        // --- MAIN THREAD: Copy texture ---
        Texture2D copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
        copy.SetPixels32(tex.GetPixels32());
        copy.Apply();

        // --- MAIN THREAD: Encode ---
        byte[] bytes = jpg ? copy.EncodeToJPG(95) : copy.EncodeToPNG();

        // Cleanup texture copy (Unity object)
        UnityEngine.Object.Destroy(copy);

        if (bytes == null || bytes.Length == 0)
        {
            throw new CaptureException("Failed to encode texture to image bytes");
        }

        // Ensure directory exists
        string dir = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        // --- BACKGROUND THREAD: Write file ---
        try
        {
            await File.WriteAllBytesAsync(filePath, bytes);
        }
        catch (Exception ex)
        {
            throw new CaptureException($"Failed to write file to {filePath}", null, ex);
        }

        Debug.Log($"✅ Saved passthrough camera image → {filePath} ({bytes.Length} bytes)");
        return true;
    }
}

public class CaptureController : MonoBehaviour
{
    [SerializeField] private PassthroughCameraAccess camAccess;

    [Button(30)]
    public async void CapturePhotoTest()
    {
        try
        {
            await camAccess.SaveCurrentCameraImageAsync(Application.persistentDataPath + "/capture0.png");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Capture test failed: {ex.Message}");
        }
    }

    public async Task CapturePhoto()
    {
        if (camAccess == null)
        {
            throw new CaptureException("PassthroughCameraAccess is not assigned");
        }

        string filePath = Application.persistentDataPath + "/capture0.png";
        await camAccess.SaveCurrentCameraImageAsync(filePath);
    }
}
