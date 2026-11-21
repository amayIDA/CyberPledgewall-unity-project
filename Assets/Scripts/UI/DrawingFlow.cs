using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Drives the visitor journey on the drawing station:
/// Welcome (Touch to Explore) -> Register (name + email ID) -> Draw (My Cyber Pledge) -> Thank You -> back to Welcome.
/// Screens are scene objects built by Cyber Pledge Wall/Rebuild Drawing UI; this only animates and wires them.
/// </summary>
public class DrawingFlow : MonoBehaviour
{
    public enum Step { Welcome = 0, Register = 1, Draw = 2, Thanks = 3 }

    [Header("Screens (indexed by Step)")]
    [SerializeField] private CanvasGroup[] screens;

    [Header("Shared")]
    [SerializeField] private RectTransform logo;
    [SerializeField] private Vector2[] logoPositions;
    [SerializeField] private float[] logoWidths;
    [SerializeField] private CyberBackdrop backdrop;
    [SerializeField] private SceneVideoBackground videoBackground;
    [SerializeField] private DrawingAppController network;
    [SerializeField] private DrawingCanvas drawing;

    [Header("Controls")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button registerButton;
    [SerializeField] private Button sendButton;
    [SerializeField] private Button clearButton;
    [SerializeField] private Button undoButton;
    [SerializeField] private CyberField nameField;
    [SerializeField] private CyberField employeeField;
    [SerializeField] private RectTransform drawPanel;
    [SerializeField] private RectTransform thanksPanel;
    [SerializeField] private RectTransform thanksText;

    [Header("Timing")]
    [SerializeField] private float thanksSeconds = 5f;
    private const float IdleRestartSeconds = 60f;

    [Header("Pen")]
    [SerializeField] private Color ink = new Color(0.03f, 0.03f, 0.06f, 1f);
    [SerializeField] private float penSize = 3.6f;

    private readonly Dictionary<RectTransform, Vector2> restPositions = new Dictionary<RectTransform, Vector2>();
    private readonly Dictionary<RectTransform, Vector3> restScales = new Dictionary<RectTransform, Vector3>();
    private Step current = Step.Welcome;
    private bool transitioning;
    private bool sending;
    private bool restarting;
    private float lastActivity;

    private void Awake()
    {
        if (drawing == null)
            drawing = FindAnyObjectByType<DrawingCanvas>();
        if (network == null)
            network = FindAnyObjectByType<DrawingAppController>();

        foreach (CanvasGroup screen in screens)
        {
            foreach (Transform child in screen.transform)
            {
                var childRect = (RectTransform)child;
                restPositions[childRect] = childRect.anchoredPosition;
                restScales[childRect] = childRect.localScale;
            }
        }

        startButton.onClick.AddListener(() => GoTo(Step.Register));
        registerButton.onClick.AddListener(SubmitRegistration);
        sendButton.onClick.AddListener(SendPledge);
        if (clearButton != null) clearButton.onClick.AddListener(() => drawing.Clear());
        if (undoButton != null) undoButton.onClick.AddListener(() => drawing.Undo());

        nameField.Input.onSubmit.AddListener(_ => SubmitRegistration());

        var register = screens != null && screens.Length > (int)Step.Register ? screens[(int)Step.Register] : null;
        if (register != null && register.GetComponent<KeyboardAvoider>() == null)
            register.gameObject.AddComponent<KeyboardAvoider>();

        drawing.Tool = BrushTool.Pen;
        drawing.BrushColor = ink;
        drawing.BrushSize = penSize;

        if (logo != null)
            logo.gameObject.SetActive(false);
        if (backdrop != null)
            backdrop.gameObject.SetActive(false);
    }

    private void Start()
    {
        for (int i = 0; i < screens.Length; i++)
            SetScreenActive(screens[i], false);

        if (videoBackground != null)
            videoBackground.PlayIdle();
        StartCoroutine(Reveal(Step.Welcome, 0.9f));
        lastActivity = Time.unscaledTime;
    }

    private void Update()
    {
        if (undoButton != null)
            undoButton.interactable = drawing.CanUndo;

        if (IsPressed())
            lastActivity = Time.unscaledTime;

        float idle = Time.unscaledTime - lastActivity;
        if (!restarting && !transitioning && current != Step.Welcome && idle > IdleRestartSeconds)
        {
            restarting = true;
            SceneManager.LoadScene(gameObject.scene.buildIndex);
        }
    }

    private static bool IsPressed()
    {
        if (Pointer.current != null && Pointer.current.press.isPressed)
            return true;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            return true;
        if (Pen.current != null && (Pen.current.tip.isPressed || Pen.current.press.isPressed))
            return true;
        return Keyboard.current != null && Keyboard.current.anyKey.isPressed;
    }

    // ---- actions -----------------------------------------------------------------------------

    private void SubmitRegistration()
    {
        if (transitioning || current != Step.Register)
            return;

        bool nameOk = !string.IsNullOrWhiteSpace(nameField.Input.text);
        bool idOk = employeeField == null || !string.IsNullOrWhiteSpace(employeeField.Input.text);
        if (!nameOk) nameField.Reject();
        if (!idOk) employeeField.Reject();
        if (!nameOk || !idOk)
        {
            (nameOk ? employeeField : nameField).Input.ActivateInputField();
            return;
        }

        VisitorLog.Append(nameField.Input.text, employeeField != null ? employeeField.Input.text : string.Empty);
        GoTo(Step.Draw);
    }

    private void SendPledge()
    {
        if (transitioning || current != Step.Draw)
            return;

        if (!drawing.HasInk)
        {
            if (network != null)
                network.SetStatus("Draw your pledge before sending.");
            StartCoroutine(Nudge(drawPanel));
            return;
        }

        if (sending)
            return;
        StartCoroutine(SendRoutine());
    }

    private IEnumerator SendRoutine()
    {
        sending = true;
        sendButton.interactable = false;

        if (!network.TrySubmit(nameField.Input.text.Trim(), employeeField != null ? employeeField.Input.text.Trim() : string.Empty, out string pledgeId, out _))
        {
            // The wall isn't reachable: try to reconnect, keep the drawing, and tell the visitor.
            network.ConnectToWall();
            yield return Nudge(drawPanel);
            sendButton.interactable = true;
            sending = false;
            yield break;
        }

        // Only say thank you once the wall confirms it received the pledge.
        float waited = 0f;
        while (!network.IsAcknowledged(pledgeId) && waited < 15f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        sendButton.interactable = true;
        sending = false;

        if (waited >= 15f)
        {
            network.SetStatus("The wall did not confirm your pledge. Check the connection and tap Send again.");
            yield return Nudge(drawPanel);
            yield break;
        }

        network.SetStatus(string.Empty);
        GoTo(Step.Thanks);
    }

    public void ResetToWelcome()
    {
        if (current == Step.Welcome)
            return;
        GoTo(Step.Welcome);
    }

    private void GoTo(Step next)
    {
        if (transitioning || next == current)
            return;
        StartCoroutine(Transition(next));
    }

    // ---- transitions -------------------------------------------------------------------------

    private IEnumerator Transition(Step next)
    {
        transitioning = true;
        CanvasGroup outgoing = screens[(int)current];
        yield return Hide(outgoing);

        if (next == Step.Welcome)
        {
            nameField.Input.text = string.Empty;
            if (employeeField != null) employeeField.Input.text = string.Empty;
            drawing.Clear(true);
        }

        current = next;
        if (videoBackground != null)
            videoBackground.SetDrawMode(next == Step.Draw);
        yield return Reveal(next, 0.55f);

        lastActivity = Time.unscaledTime;
        transitioning = false;

        if (next == Step.Register)
            nameField.Input.ActivateInputField();
        else if (next == Step.Thanks)
            StartCoroutine(ThanksSequence());
    }

    private IEnumerator Hide(CanvasGroup screen)
    {
        screen.interactable = false;
        screen.blocksRaycasts = false;
        var rt = (RectTransform)screen.transform;
        yield return UITween.Run(0.32f, t =>
        {
            screen.alpha = 1f - t;
            rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.96f, t);
            rt.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, 36f, t));
        }, UITween.InCubic);
        SetScreenActive(screen, false);
        rt.localScale = Vector3.one;
        rt.anchoredPosition = Vector2.zero;
    }

    private IEnumerator Reveal(Step step, float duration)
    {
        CanvasGroup screen = screens[(int)step];
        SetScreenActive(screen, true);
        screen.alpha = 1f;
        screen.interactable = false;
        screen.blocksRaycasts = false;

        var children = new List<RectTransform>();
        foreach (Transform child in screen.transform)
            children.Add((RectTransform)child);

        for (int i = 0; i < children.Count; i++)
        {
            RectTransform child = children[i];
            Vector2 rest = restPositions[child];
            CanvasGroup group = child.GetComponent<CanvasGroup>();
            var button = child.GetComponent<CyberButton>();
            float delay = i * 0.09f;

            Vector2 from = rest + new Vector2(0f, -70f);
            Vector3 restScale = restScales[child];
            if (button == null)
                child.anchoredPosition = from;
            if (group != null) group.alpha = 0f;
            if (button != null) button.EntranceScale = 0.85f;
            else child.localScale = restScale * 0.9f;

            StartCoroutine(UITween.Run(duration, t =>
            {
                if (button == null)
                    child.anchoredPosition = Vector2.LerpUnclamped(from, rest, t);
                float scale = Mathf.LerpUnclamped(0.88f, 1f, t);
                if (button != null) button.EntranceScale = scale; else child.localScale = restScale * scale;
            }, UITween.OutBack, delay));

            StartCoroutine(UITween.Run(duration * 0.7f, t =>
            {
                if (group != null) group.alpha = t;
            }, UITween.OutCubic, delay));
        }

        yield return new WaitForSecondsRealtime(duration * 0.8f + children.Count * 0.09f);
        screen.interactable = true;
        screen.blocksRaycasts = true;

        foreach (RectTransform child in children)
        {
            if (child.GetComponent<CyberButton>() == null)
                child.anchoredPosition = restPositions[child];
            var button = child.GetComponent<CyberButton>();
            if (button != null) button.EntranceScale = 1f; else child.localScale = restScales[child];
            CanvasGroup group = child.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = 1f;
        }
    }

    private IEnumerator ThanksSequence()
    {
        yield return new WaitForSecondsRealtime(0.25f);
        UiSfx.Success();
        if (backdrop != null && backdrop.gameObject.activeInHierarchy)
            backdrop.Burst(Vector2.zero);

        if (thanksText != null)
        {
            yield return UITween.Run(0.7f, t =>
            {
                float pulse = 1f + 0.06f * Mathf.Sin(t * Mathf.PI);
                thanksText.localScale = Vector3.one * pulse;
            }, x => x);
        }

        yield return new WaitForSecondsRealtime(thanksSeconds);
        if (current == Step.Thanks)
            GoTo(Step.Welcome);
    }

    private IEnumerator Nudge(RectTransform target)
    {
        if (target == null)
            yield break;

        UiSfx.Error();
        Vector2 start = target.anchoredPosition;
        yield return UITween.Run(0.4f, t =>
        {
            target.anchoredPosition = start + new Vector2(Mathf.Sin(t * 30f) * 14f * (1f - t), 0f);
        }, x => x);
        target.anchoredPosition = start;
    }

    private static void SetScreenActive(CanvasGroup screen, bool active)
    {
        screen.gameObject.SetActive(active);
        screen.interactable = active;
        screen.blocksRaycasts = active;
    }
}
