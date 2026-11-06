using System.Runtime.InteropServices;
using UnityEngine;

internal static class LogFilter
{
  private const string XrealLibraryName = "XREALXRPlugin";

  [DllImport(XrealLibraryName)]
  private static extern void SetNativeLogLevel(LogType logType);

  // runs after XR Management initialized the XREAL loader (AfterAssembliesLoaded)
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
  private static void Apply()
  {
    Debug.unityLogger.filterLogType = LogType.Warning;
    SetNativeLogLevel(LogType.Warning);
  }
}
