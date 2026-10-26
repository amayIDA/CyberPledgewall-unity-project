using UnityEngine;

/// <summary>
/// Keeps the drawing tablet awake. Android otherwise turns the screen off after
/// 10-20 minutes, Unity pauses, Netcode heartbeats stop, and the wall drops the client.
/// </summary>
public static class DrawingKiosk
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        Application.runInBackground = true;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        KeepScreenOn();
    }

    public static void KeepScreenOn()
    {
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var window = activity.Call<AndroidJavaObject>("getWindow"))
            {
                const int flagKeepScreenOn = 128;
                activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    window.Call("addFlags", flagKeepScreenOn);
                }));
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("Could not keep the screen on: " + ex.Message);
        }
#endif
    }
}
