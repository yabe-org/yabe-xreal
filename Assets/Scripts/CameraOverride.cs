using UnityEngine;
using UnityEngine.Rendering;
using Unity.XR.XREAL;
using System;
using System.Runtime.InteropServices;

public class CameraOverride : MonoBehaviour
{
    private const string LIBRARY_NAME = "compositor";
    [DllImport(LIBRARY_NAME, EntryPoint = "yabe_compositor_draw")]
    private static extern void yabe_compositor_draw(ref Matrix4x4 pv_mat);

    private CommandBuffer commandBuffer;
    private Camera targetCamera;

    private static Matrix4x4 cachedVpMatrix = Matrix4x4.identity;

    private delegate void RenderEventHandler(int eventID);
    private static RenderEventHandler handle = new RenderEventHandler(HandlePluginEvent);
    private static IntPtr handlePtr = Marshal.GetFunctionPointerForDelegate(handle);

    void OnEnable()
    {
        targetCamera = GetComponent<Camera>();
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null) return;

        commandBuffer = new CommandBuffer();
        commandBuffer.name = "XREAL_NativeRenderingPlugin";
        commandBuffer.ClearRenderTarget(true, true, Color.red);
        commandBuffer.IssuePluginEvent(handlePtr, 0);

        targetCamera.AddCommandBuffer(CameraEvent.AfterForwardOpaque, commandBuffer);
    }

    void Update()
    {
        if (targetCamera == null) return;

        Matrix4x4 proj = Matrix4x4.identity;
        const float near = 0.002f, far = 100.0f;
        XREALPlugin.GetCameraProjectionMatrix(XREALComponent.XREAL_COMPONENT_DISPLAY_LEFT, near, far, ref proj);
        Matrix4x4 view = targetCamera.worldToCameraMatrix;
        cachedVpMatrix = proj * view;
    }

    void OnDisable()
    {
        if (targetCamera != null && commandBuffer != null)
        {
            targetCamera.RemoveCommandBuffer(CameraEvent.AfterForwardOpaque, commandBuffer);
            commandBuffer.Release();
            commandBuffer = null;
        }
    }

    [AOT.MonoPInvokeCallback(typeof(RenderEventHandler))]
    static void HandlePluginEvent(int eventID)
    {
        Matrix4x4 vp_mat = cachedVpMatrix;
        try {
            yabe_compositor_draw(ref vp_mat);
        } catch (Exception e) {
            Debug.LogError("Error calling yabe_compositor_draw: " + e.Message);
        }
    }
}
