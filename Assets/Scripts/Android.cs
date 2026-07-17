using UnityEngine;

public class Android
{
  public static void Vibrate()
  {
    using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
    using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
    {
      activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
      {
        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        using (var window = activity.Call<AndroidJavaObject>("getWindow"))
        using (var decor = window.Call<AndroidJavaObject>("getDecorView"))
        {
          decor.Call<bool>("performHapticFeedback", 0);
        }
      }));
    }
  }
}
