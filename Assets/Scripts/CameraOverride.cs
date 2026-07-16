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

    private CommandBuffer commandBuffer;
    private Camera targetCamera;

    private static Matrix4x4 cachedVpMatrix = Matrix4x4.identity;

    private delegate void RenderEventHandler(int eventID);
    private static RenderEventHandler handle = new RenderEventHandler(HandlePluginEvent);
    private static IntPtr handlePtr = Marshal.GetFunctionPointerForDelegate(handle);

    void OnEnable()
    {
        StartCompositorService();

        targetCamera = GetComponent<Camera>();
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null) return;

        commandBuffer = new CommandBuffer();
        commandBuffer.name = "XREAL_NativeRenderingPlugin";
        commandBuffer.ClearRenderTarget(true, true, Color.red);
        commandBuffer.IssuePluginEvent(handlePtr, 0);

        targetCamera.AddCommandBuffer(CameraEvent.AfterForwardOpaque, commandBuffer);
    }
    void OnDisable()
    {
        if (targetCamera != null && commandBuffer != null)
        {
            targetCamera.RemoveCommandBuffer(CameraEvent.AfterForwardOpaque, commandBuffer);
            commandBuffer.Release();
            commandBuffer = null;
        }
        terminate_compositor();
    }
    void OnDestroy()
    {
        Debug.Log("CameraOverride OnDestroy called");
        terminate_compositor();
    }


    void Update()
    {
        if (targetCamera == null) return;
        UpdateCachedVpMatrix(Camera.MonoOrStereoscopicEye.Mono);
    }

    void OnPreRender()
    {
        if (targetCamera == null) return;
        UpdateCachedVpMatrix(targetCamera.stereoActiveEye);
    }

    void UpdateCachedVpMatrix(Camera.MonoOrStereoscopicEye activeEye)
    {
        XREALComponent component = activeEye == Camera.MonoOrStereoscopicEye.Right
            ? XREALComponent.XREAL_COMPONENT_DISPLAY_RIGHT
            : XREALComponent.XREAL_COMPONENT_DISPLAY_LEFT;
        Matrix4x4 proj = Matrix4x4.identity;
        XREALPlugin.GetCameraProjectionMatrix(component, NearClip, FarClip, ref proj);

        Matrix4x4 view = targetCamera.worldToCameraMatrix;
        if (activeEye == Camera.MonoOrStereoscopicEye.Left)
        {
            view = targetCamera.GetStereoViewMatrix(Camera.StereoscopicEye.Left);
        }
        else if (activeEye == Camera.MonoOrStereoscopicEye.Right)
        {
            view = targetCamera.GetStereoViewMatrix(Camera.StereoscopicEye.Right);
        }
        cachedVpMatrix = proj * view;
        Debug.Log($"Camera pos: {targetCamera.transform.position}");
    }

    [AOT.MonoPInvokeCallback(typeof(RenderEventHandler))]
    static void HandlePluginEvent(int eventID)
    {
        if (!is_compositor_running())
        {
            return;
        }
        Matrix4x4 vp_mat = cachedVpMatrix;
        try {
            yabe_compositor_draw(ref vp_mat);
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
