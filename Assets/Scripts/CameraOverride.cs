using UnityEngine;
using UnityEngine.Rendering;
using Unity.XR.XREAL;
using System;
using System.Runtime.InteropServices;

public class CameraOverride : MonoBehaviour
{
    [SerializeField] StreamServer streamServer;

    private const float NearClip = 0.002f;
    private const float FarClip = 100.0f;

    private CommandBuffer leftCommandBuffer;
    private CommandBuffer rightCommandBuffer;
    private Camera targetCamera;
    private Camera rightCamera;
    private StereoTargetEyeMask originalTargetEye;
    private float trackingOriginHeight;
    private bool hasTrackingOriginHeight;

    private enum EyeEvent
    {
        Left = 0,
        Right = 1,
    }

    private static readonly object matrixLock = new object();
    private static readonly Matrix4x4[] cachedProjectionMatrices =
    {
        Matrix4x4.identity,
        Matrix4x4.identity,
    };
    private static readonly Matrix4x4[] cachedViewMatrices =
    {
        Matrix4x4.identity,
        Matrix4x4.identity,
    };

    private delegate void RenderEventHandler(int eventID);
    private static RenderEventHandler handle = new RenderEventHandler(HandlePluginEvent);
    private static IntPtr handlePtr = Marshal.GetFunctionPointerForDelegate(handle);

    void OnEnable()
    {
        StartCompositorService();

        targetCamera = GetComponent<Camera>();
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null) return;

        originalTargetEye = targetCamera.stereoTargetEye;
        targetCamera.stereoTargetEye = StereoTargetEyeMask.Left;

        // A multipass camera replays the same command buffer for both eyes, so use a
        // dedicated camera and event ID for each eye instead of guessing the active eye.
        GameObject rightCameraObject = new GameObject("Yabe Right Eye Camera");
        rightCameraObject.transform.SetParent(targetCamera.transform, false);
        rightCamera = rightCameraObject.AddComponent<Camera>();
        rightCamera.CopyFrom(targetCamera);
        rightCamera.stereoTargetEye = StereoTargetEyeMask.Right;
        hasTrackingOriginHeight = false;

        leftCommandBuffer = CreateCommandBuffer("Yabe Left Eye", EyeEvent.Left);
        rightCommandBuffer = CreateCommandBuffer("Yabe Right Eye", EyeEvent.Right);
        targetCamera.AddCommandBuffer(CameraEvent.AfterForwardOpaque, leftCommandBuffer);
        rightCamera.AddCommandBuffer(CameraEvent.AfterForwardOpaque, rightCommandBuffer);

        UpdateCachedMatrices();
        Application.onBeforeRender += UpdateCachedMatrices;
    }
    void OnDisable()
    {
        Application.onBeforeRender -= UpdateCachedMatrices;
        if (targetCamera != null)
        {
            targetCamera.stereoTargetEye = originalTargetEye;
            ReleaseCommandBuffer(targetCamera, ref leftCommandBuffer);
        }
        if (rightCamera != null)
        {
            ReleaseCommandBuffer(rightCamera, ref rightCommandBuffer);
            Destroy(rightCamera.gameObject);
            rightCamera = null;
        }
        NativePlugin.terminate_compositor();
    }
    void OnDestroy()
    {
        Debug.Log("CameraOverride OnDestroy called");
        NativePlugin.terminate_compositor();
    }
    private static CommandBuffer CreateCommandBuffer(string name, EyeEvent eyeEvent)
    {
        CommandBuffer buffer = new CommandBuffer();
        buffer.name = name;
        buffer.ClearRenderTarget(true, true, Color.clear);
        buffer.IssuePluginEvent(handlePtr, (int)eyeEvent);
        return buffer;
    }
    private static void ReleaseCommandBuffer(Camera camera, ref CommandBuffer buffer)
    {
        if (buffer == null) return;

        camera.RemoveCommandBuffer(CameraEvent.AfterForwardOpaque, buffer);
        buffer.Release();
        buffer = null;
    }

    private void UpdateCachedMatrices()
    {
        Matrix4x4 leftView = targetCamera.GetStereoViewMatrix(Camera.StereoscopicEye.Left);
        Matrix4x4 rightView = rightCamera.GetStereoViewMatrix(Camera.StereoscopicEye.Right);
        if (!hasTrackingOriginHeight)
        {
          // sometimes view position becomes 0, so set fallback value to 1.42f (in my environment, it tends to be this value)
            trackingOriginHeight = Math.Max(1.42f, leftView.inverse.GetColumn(3).y);
            hasTrackingOriginHeight = true;
        }

        Matrix4x4 leftProjection = Matrix4x4.identity;
        Matrix4x4 rightProjection = Matrix4x4.identity;
        XREALPlugin.GetCameraProjectionMatrix(
            XREALComponent.XREAL_COMPONENT_DISPLAY_LEFT,
            NearClip,
            FarClip,
            ref leftProjection);
        XREALPlugin.GetCameraProjectionMatrix(
            XREALComponent.XREAL_COMPONENT_DISPLAY_RIGHT,
            NearClip,
            FarClip,
            ref rightProjection);

        leftView = BuildCompositorViewMatrix(leftView);
        rightView = BuildCompositorViewMatrix(rightView);
        lock (matrixLock)
        {
            cachedProjectionMatrices[(int)EyeEvent.Left] = leftProjection;
            cachedProjectionMatrices[(int)EyeEvent.Right] = rightProjection;
            cachedViewMatrices[(int)EyeEvent.Left] = leftView;
            cachedViewMatrices[(int)EyeEvent.Right] = rightView;
        }
    }

    // Unity/XREAL uses +Z as forward and includes the tracked eye height, while the
    // compositor uses -Z as forward and treats the initial eye height as Y = 0.
    private Matrix4x4 BuildCompositorViewMatrix(Matrix4x4 unityView)
    {
        Matrix4x4 cameraToWorld = unityView.inverse;
        Vector4 cameraPosition = cameraToWorld.GetColumn(3);
        cameraPosition.y -= trackingOriginHeight;
        cameraToWorld.SetColumn(3, cameraPosition);

        Matrix4x4 compositorToUnity = Matrix4x4.Scale(new Vector3(1.0f, 1.0f, -1.0f));
        return cameraToWorld.inverse * compositorToUnity;
    }

    [AOT.MonoPInvokeCallback(typeof(RenderEventHandler))]
    static void HandlePluginEvent(int eventID)
    {
        if (!NativePlugin.is_compositor_running() || (eventID != (int)EyeEvent.Left && eventID != (int)EyeEvent.Right))
        {
            return;
        }
        try {
            lock (matrixLock)
            {
                NativePlugin.yabe_compositor_draw(
                    ref cachedProjectionMatrices[eventID],
                    ref cachedViewMatrices[eventID]);
            }
        } catch (Exception e) {
            Debug.LogError("Error calling yabe_compositor_draw: " + e.Message);
        }
    }

    private static void StartCompositorService()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent"))
            using (AndroidJavaObject componentName = new AndroidJavaObject(
                "android.content.ComponentName",
                activity.Call<string>("getPackageName"),
                "org.yabe.xreal.CompositorService"))
            {
                intent.Call<AndroidJavaObject>("setComponent", componentName);
                activity.Call<AndroidJavaObject>("startService", intent);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to start CompositorService: " + e.Message);
        }
#endif
    }

  enum Status
  {
    None, CaptureRequested, Streaming,
  }
  Status status;
  Status prevStatus;
  public void RequestCapture()
  {
    this.prevStatus = this.status;
    this.status = Status.CaptureRequested;
  }
  public void StartStreaming()
  {
    this.status = Status.Streaming;
  }
  public void StopStreaming()
  {
    this.status = Status.None;
  }
  void OnRenderImage(RenderTexture src, RenderTexture dst)
  {
    Graphics.Blit(src, dst);
    switch (this.status)
    {
      case Status.CaptureRequested:
        this.status = this.prevStatus;
        AsyncGPUReadback.Request(src, 0, ScreenshotHandler.HandleSaveImage);
        break;
      case Status.Streaming:
        AsyncGPUReadback.Request(src, 0, this.streamServer.HandleGetCurrentFrame);
        break;
    }
  }
}
