using UnityEngine;
using UnityEngine.Rendering;
using Unity.XR.XREAL;
using System;
using System.Runtime.InteropServices;

public class CameraOverride : MonoBehaviour
{
    private const string LIBRARY_NAME = "compositor";
    [DllImport(LIBRARY_NAME)]
    private static extern void yabe_compositor_draw(ref Matrix4x4 pv_mat);
    [DllImport(LIBRARY_NAME)]
    private static extern bool is_compositor_running();
    [DllImport(LIBRARY_NAME)]
    private static extern void terminate_compositor();

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

    private static readonly object vpMatrixLock = new object();
    private static readonly Matrix4x4[] cachedVpMatrices =
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

        UpdateCachedVpMatrices();
        Application.onBeforeRender += UpdateCachedVpMatrices;
    }
    void OnDisable()
    {
        Application.onBeforeRender -= UpdateCachedVpMatrices;
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
        terminate_compositor();
    }
    void OnDestroy()
    {
        Debug.Log("CameraOverride OnDestroy called");
        terminate_compositor();
    }
    private static CommandBuffer CreateCommandBuffer(string name, EyeEvent eyeEvent)
    {
        CommandBuffer buffer = new CommandBuffer();
        buffer.name = name;
        buffer.ClearRenderTarget(true, true, Color.red);
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

    private void UpdateCachedVpMatrices()
    {
        Matrix4x4 leftView = targetCamera.GetStereoViewMatrix(Camera.StereoscopicEye.Left);
        Matrix4x4 rightView = rightCamera.GetStereoViewMatrix(Camera.StereoscopicEye.Right);
        if (!hasTrackingOriginHeight)
        {
            trackingOriginHeight = leftView.inverse.GetColumn(3).y;
            hasTrackingOriginHeight = true;
        }

        Matrix4x4 leftVp = BuildVpMatrix(leftView, Camera.StereoscopicEye.Left, trackingOriginHeight);
        Matrix4x4 rightVp = BuildVpMatrix(rightView, Camera.StereoscopicEye.Right, trackingOriginHeight);
        lock (vpMatrixLock)
        {
            cachedVpMatrices[(int)EyeEvent.Left] = leftVp;
            cachedVpMatrices[(int)EyeEvent.Right] = rightVp;
        }
    }

    private static Matrix4x4 BuildVpMatrix(
        Matrix4x4 view,
        Camera.StereoscopicEye eye,
        float trackingOriginHeight)
    {
        XREALComponent component = eye == Camera.StereoscopicEye.Right
            ? XREALComponent.XREAL_COMPONENT_DISPLAY_RIGHT
            : XREALComponent.XREAL_COMPONENT_DISPLAY_LEFT;
        Matrix4x4 proj = Matrix4x4.identity;
        XREALPlugin.GetCameraProjectionMatrix(component, NearClip, FarClip, ref proj);

        view = BuildCompositorViewMatrix(view, trackingOriginHeight);
        return proj * view;
    }

    // Unity/XREAL uses +Z as forward and includes the tracked eye height, while the
    // compositor uses -Z as forward and treats the initial eye height as Y = 0.
    private static Matrix4x4 BuildCompositorViewMatrix(Matrix4x4 unityView, float trackingOriginHeight)
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
        if (!is_compositor_running() || (eventID != (int)EyeEvent.Left && eventID != (int)EyeEvent.Right))
        {
            return;
        }
        try {
            lock (vpMatrixLock)
            {
                yabe_compositor_draw(ref cachedVpMatrices[eventID]);
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
}
