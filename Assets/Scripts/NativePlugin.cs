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

    [DllImport(LibraryName)]
    internal static extern void yabe_compositor_move_pointer(float dx, float dy);

    internal const uint PointerButtonLeft = 0x110;
    internal const uint PointerButtonRight = 0x111;
    [DllImport(LibraryName)]
    internal static extern void yabe_compositor_button(
        uint button,
        [MarshalAs(UnmanagedType.I1)] bool isPressed);

    [DllImport(LibraryName)]
    internal static extern void yabe_compositor_axis(uint axis, float value);

    [DllImport(LibraryName)]
    internal static extern void yabe_compositor_key(
        uint key,
        [MarshalAs(UnmanagedType.I1)] bool isPressed);

    [DllImport(LibraryName)]
    internal static extern void yabe_compositor_keymap(uint format, int fd, uint size);

    [DllImport(LibraryName)]
    internal static extern void yabe_compositor_repeat_info(int rate, int delay);

    [DllImport(LibraryName)]
    internal static extern void yabe_compositor_modifiers(
        uint modsDepressed,
        uint modsLatched,
        uint modsLocked,
        uint group);
}
