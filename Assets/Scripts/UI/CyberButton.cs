using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Hologram button feel: idle breathing + expanding glow ring, springy hover and press,
/// and a flash on click. Add next to a Button; assign the optional glow ring Image.
/// </summary>
[RequireComponent(typeof(Button))]
public class CyberButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private Image glowRing;
    [SerializeField] private float breathe = 0.025f;
    [SerializeField] private float pingInterval = 2.4f;

    /// <summary>Set by screen transitions so entrance scaling doesn't fight the hover/idle scale.</summary>
    public float EntranceScale { get; set; } = 1f;

    private Button button;
    private Vector3 baseScale = Vector3.one;
    private float hoverTarget;
    private float pressTarget;
    private float hover;
    private float press;
    private float flash;
    private float offset;

    private void Awake()
    {
        button = GetComponent<Button>();
        offset = Random.value * 3f;
    }

    private void OnEnable()
    {
        hover = press = hoverTarget = pressTarget = 0f;
    }

    public void OnPointerEnter(PointerEventData e) => hoverTarget = 1f;
    public void OnPointerExit(PointerEventData e) { hoverTarget = 0f; pressTarget = 0f; }
    public void OnPointerDown(PointerEventData e)
    {
        pressTarget = 1f;
        if (button != null && button.interactable)
            UiSfx.Click();
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (pressTarget > 0f && button.interactable)
            flash = 1f;
        pressTarget = 0f;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        hover = Mathf.MoveTowards(hover, hoverTarget, dt * 6f);
        press = Mathf.MoveTowards(press, pressTarget, dt * 12f);
        flash = Mathf.MoveTowards(flash, 0f, dt * 2.2f);

        float time = Time.unscaledTime + offset;
        float idle = 1f + breathe * Mathf.Sin(time * 2.1f);
        float scale = idle * (1f + 0.06f * UITween.OutCubic(hover)) * (1f - 0.07f * press);
        transform.localScale = baseScale * (scale * EntranceScale);

        if (glowRing != null)
        {
            // A soft ring that periodically expands off the button edge.
            float cycle = (time % pingInterval) / pingInterval;
            float ping = cycle < 0.55f ? cycle / 0.55f : 1f;
            float alpha = cycle < 0.55f ? (1f - ping) * 0.4f : 0f;
            alpha = Mathf.Max(alpha, flash * 0.7f, hover * 0.2f);
            glowRing.rectTransform.localScale = Vector3.one * (1f + 0.22f * UITween.OutCubic(ping) + flash * 0.12f);
            Color c = glowRing.color;
            c.a = button.interactable ? alpha : 0f;
            glowRing.color = c;
        }
    }
}
