using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Lifts the registration panel when the Android keyboard is open so Name / Email stay visible.
/// The lift is always computed from the field's *resting* position (current position minus the lift
/// already applied) and eased toward its target, so it can't feed back on itself and flicker.
/// </summary>
public class KeyboardAvoider : MonoBehaviour
{
    [SerializeField] private RectTransform panel;
    [SerializeField] private float padding = 64f;
    [SerializeField] private float smoothTime = 0.12f;

    private readonly Vector3[] corners = new Vector3[4];
    private Vector2 rest;
    private Canvas canvas;
    private float lift;
    private float liftVelocity;
    private float stableKeyboard;
    private float targetLift;

    private void Awake()
    {
        if (panel == null)
            panel = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>();
    }

    private void OnEnable()
    {
        rest = panel.anchoredPosition;
        lift = targetLift = liftVelocity = stableKeyboard = 0f;
        TouchScreenKeyboard.hideInput = true;
    }

    private void OnDisable()
    {
        if (panel != null)
            panel.anchoredPosition = rest;
    }

    private void LateUpdate()
    {
        if (panel == null)
            return;

        float scale = canvas != null && canvas.scaleFactor > 0.01f ? canvas.scaleFactor : 1f;

        float keyboard = GetKeyboardHeight();
        // Ignore tiny jitters from the OS so the target stays put while the keyboard is steady.
        if (Mathf.Abs(keyboard - stableKeyboard) > 24f)
            stableKeyboard = keyboard;

        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        bool editing = stableKeyboard > 80f && selected != null && selected.transform.IsChildOf(panel);

        if (editing && selected.transform is RectTransform field)
        {
            field.GetWorldCorners(corners);
            // Where the field would be with no lift applied (canvas units -> screen pixels).
            float restingBottom = corners[0].y - lift * scale;
            float needed = stableKeyboard + padding * scale;
            targetLift = Mathf.Max(0f, (needed - restingBottom) / scale);
        }
        else if (!editing)
        {
            targetLift = 0f;
        }

        lift = Mathf.SmoothDamp(lift, targetLift, ref liftVelocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        if (Mathf.Abs(lift - targetLift) < 0.25f)
            lift = targetLift;

        panel.anchoredPosition = rest + new Vector2(0f, lift);
    }

    private static float GetKeyboardHeight()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var unity = activity.Get<AndroidJavaObject>("mUnityPlayer"))
            using (var view = unity.Call<AndroidJavaObject>("getView"))
            using (var rect = new AndroidJavaObject("android.graphics.Rect"))
            {
                view.Call("getWindowVisibleDisplayFrame", rect);
                int visibleBottom = rect.Get<int>("bottom");
                int keyboard = Screen.height - visibleBottom;
                return keyboard > 80 ? keyboard : 0f;
            }
        }
        catch
        {
            // Fall through to Unity's keyboard API.
        }
#endif
        if (!TouchScreenKeyboard.visible)
            return 0f;

        Rect area = TouchScreenKeyboard.area;
        return area.height > 0f ? area.height : 0f;
    }
}
