using System.Runtime.InteropServices;
using UnityEngine;

internal static class NativePlugin
{
    private const string LibraryName = "compositor";

    [DllImport(LibraryName)]
    internal static extern void yabe_compositor_draw(ref Matrix4x4 pvMatrix);

    [DllImport(LibraryName)]
    internal static extern bool is_compositor_running();

    [DllImport(LibraryName)]
    internal static extern void terminate_compositor();
}
