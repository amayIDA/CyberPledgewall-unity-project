using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Input field polish: glow on focus, subtle lift, red flash + shake when invalid.</summary>
[RequireComponent(typeof(TMP_InputField))]
public class CyberField : MonoBehaviour
{
    [SerializeField] private Image frame;

    private TMP_InputField input;
    private RectTransform rt;
    private Vector2 basePosition;
    private float focus;
    private float focusTarget;
    private float error;
    private Coroutine shakeRoutine;

    private static readonly Color Idle = Color.white;
    private static readonly Color Focused = new Color(0.62f, 0.9f, 1.25f, 1f);
    private static readonly Color Error = new Color(1f, 0.45f, 0.5f, 1f);

    public TMP_InputField Input => input != null ? input : input = GetComponent<TMP_InputField>();

    private void Awake()
    {
        input = GetComponent<TMP_InputField>();
        rt = (RectTransform)transform;
        if (frame == null)
            frame = GetComponent<Image>();

        input.shouldHideMobileInput = true;
        input.onSelect.AddListener(_ => focusTarget = 1f);
        input.onDeselect.AddListener(_ => focusTarget = 0f);
        input.onValueChanged.AddListener(_ => error = 0f);
    }

    private void OnEnable()
    {
        basePosition = rt.anchoredPosition;
        focus = focusTarget = error = 0f;
    }

    /// <summary>Call after a layout change moves the field so the shake returns to the right place.</summary>
    public void RememberPosition() => basePosition = rt.anchoredPosition;

    public void Reject()
    {
        error = 1f;
        UiSfx.Error();
        if (shakeRoutine != null)
            StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(Shake());
    }

    private IEnumerator Shake()
    {
        basePosition = rt.anchoredPosition;
        yield return UITween.Run(0.45f, t =>
        {
            float damp = 1f - t;
            rt.anchoredPosition = basePosition + new Vector2(Mathf.Sin(t * 34f) * 16f * damp, 0f);
        }, x => x);
        rt.anchoredPosition = basePosition;
        shakeRoutine = null;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        focus = Mathf.MoveTowards(focus, focusTarget, dt * 6f);
        error = Mathf.MoveTowards(error, 0f, dt * 0.25f);

        if (frame != null)
        {
            Color c = Color.Lerp(Idle, Focused, UITween.OutCubic(focus));
            frame.color = Color.Lerp(c, Error, Mathf.Clamp01(error));
        }

        if (shakeRoutine == null)
            rt.localScale = Vector3.one * (1f + 0.015f * UITween.OutCubic(focus));
    }
}
