using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class ScreenshotHandler : MonoBehaviour
{
  [SerializeField] CameraOverride targetCamera;
  [SerializeField] InputActionReference buttonAction;

  private void OnEnable()
  {
    buttonAction.action.performed += RequestGetScreenshot;
    buttonAction.action.Enable();
  }
  private void OnDisable()
  {
    buttonAction.action.performed -= RequestGetScreenshot;
    buttonAction.action.Disable();
  }

  private void RequestGetScreenshot(InputAction.CallbackContext context)
  {
    Debug.Log("Requesting screenshot");
    this.targetCamera.RequestCapture();
  }

  public static void HandleSaveImage(AsyncGPUReadbackRequest req)
  {
    Debug.Log("HandleSaveImage called");
    if (req.hasError)
    {
      Android.ShowToast($"Failed to get image");
      return;
    }
    try
    {
      var tex = new Texture2D(req.width, req.height, TextureFormat.RGBA32, false);
      tex.LoadRawTextureData(req.GetData<Color32>());
      tex.Apply();

      var dst_dir = $"{Android.GetPublicDir(Android.DirectoryType.Pictures)}/yabe-screenshots";
      if (!Directory.Exists(dst_dir))
      {
        Directory.CreateDirectory(dst_dir);
      }
      var filename = $"yabe_{System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}.jpg";
      File.WriteAllBytes($"{dst_dir}/{filename}", tex.EncodeToJPG());
      Android.ShowToast($"'{filename}' is saved to {dst_dir}");
    }
    catch (Exception e)
    {
      Android.ShowToast($"{e.Message}");
      Debug.LogError($"Failed to save screenshot: {e.Message}");
    }
  }
}

