using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Invisible strip at the top of the drawing station. Long-press (~1.2s) opens a wall-IP field
/// so a tablet can be pointed at the PC host without editing JSON on device.
/// </summary>
public class HostIpPanel : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private DrawingAppController network;
    [SerializeField] private float holdSeconds = 1.2f;

    private RectTransform _panel;
    private TMP_InputField _input;
    private TMP_Text _status;
    private bool _holding;
    private float _holdStart;

    private void Awake()
    {
        if (network == null)
            network = FindAnyObjectByType<DrawingAppController>();

        EnsureUi();
        SetOpen(false);
    }

    private void Update()
    {
        if (!_holding)
            return;

        if (Time.unscaledTime - _holdStart >= holdSeconds)
        {
            _holding = false;
            SetOpen(true);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_panel != null && _panel.gameObject.activeSelf)
            return;

        _holding = true;
        _holdStart = Time.unscaledTime;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _holding = false;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _holding = false;
    }

    private void Connect()
    {
        string ip = _input != null ? _input.text.Trim() : string.Empty;
        if (string.IsNullOrEmpty(ip))
        {
            SetStatus("Enter the wall PC IP, e.g. 192.168.1.10");
            return;
        }

        if (network == null)
            network = FindAnyObjectByType<DrawingAppController>();
        if (network == null)
        {
            SetStatus("Network controller is missing.");
            return;
        }

        SetStatus($"Saving {ip} and connecting...");
        network.ApplyHostAddress(ip);
    }

    private void SetOpen(bool open)
    {
        if (_panel == null)
            return;

        _panel.gameObject.SetActive(open);
        if (!open)
            return;

        if (_input != null)
        {
            _input.text = network != null ? network.CurrentHost : string.Empty;
            _input.ActivateInputField();
        }

        SetStatus(network != null && network.IsConnected
            ? "Connected. Long-press the top edge again if you need to change IP."
            : "Tablet and wall PC must be on the same Wi-Fi.");
    }

    private void SetStatus(string message)
    {
        if (_status != null)
            _status.text = message;
    }

    private void EnsureUi()
    {
        var rt = (RectTransform)transform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(420f, 90f);

        var hit = GetComponent<Image>();
        if (hit == null)
            hit = gameObject.AddComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0.01f);
        hit.raycastTarget = true;

        Transform existing = transform.Find("Panel");
        if (existing != null)
        {
            _panel = (RectTransform)existing;
            _input = _panel.GetComponentInChildren<TMP_InputField>(true);
            var statusGo = _panel.Find("Status");
            if (statusGo != null)
                _status = statusGo.GetComponent<TMP_Text>();
            return;
        }

        var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panelGo.transform.SetParent(transform, false);
        _panel = (RectTransform)panelGo.transform;
        _panel.anchorMin = new Vector2(0.5f, 1f);
        _panel.anchorMax = new Vector2(0.5f, 1f);
        _panel.pivot = new Vector2(0.5f, 1f);
        _panel.anchoredPosition = new Vector2(0f, -8f);
        _panel.sizeDelta = new Vector2(760f, 280f);
        var panelImage = panelGo.GetComponent<Image>();
        panelImage.color = new Color(0.02f, 0.06f, 0.18f, 0.94f);
        panelImage.raycastTarget = true;

        var title = CreateTmp("Title", _panel, "Wall IP", 28f, new Vector2(0f, -28f), new Vector2(700f, 40f));
        title.color = Color.white;

        _input = CreateInput(_panel, "IpInput", "192.168.1.10", new Vector2(0f, -90f), new Vector2(700f, 56f));
        _input.onSubmit.AddListener(_ => Connect());

        CreateButton(_panel, "ConnectButton", "Connect", new Vector2(-90f, -168f), new Vector2(200f, 48f), Connect);
        CreateButton(_panel, "CloseButton", "Close", new Vector2(130f, -168f), new Vector2(180f, 48f), () => SetOpen(false));

        _status = CreateTmp("Status", _panel, string.Empty, 18f, new Vector2(0f, -230f), new Vector2(720f, 36f));
        _status.color = new Color(0.75f, 0.9f, 1f, 1f);
    }

    private static TextMeshProUGUI CreateTmp(string name, Transform parent, string text, float size, Vector2 pos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static TMP_InputField CreateInput(Transform parent, string name, string placeholder, Vector2 pos, Vector2 sizeDelta)
    {
        var resources = new TMP_DefaultControls.Resources();
        GameObject go = TMP_DefaultControls.CreateInputField(resources);
        go.name = name;
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;

        var input = go.GetComponent<TMP_InputField>();
        input.contentType = TMP_InputField.ContentType.Standard;
        input.characterLimit = 45;
        if (input.placeholder is TMP_Text ph)
            ph.text = placeholder;
        return input;
    }

    private static void CreateButton(Transform parent, string name, string label, Vector2 pos, Vector2 sizeDelta, UnityEngine.Events.UnityAction onClick)
    {
        var resources = new TMP_DefaultControls.Resources();
        GameObject go = TMP_DefaultControls.CreateButton(resources);
        go.name = name;
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;
        var tmp = go.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text = label;
            tmp.fontSize = 22;
        }
        go.GetComponent<Button>().onClick.AddListener(onClick);
    }
}
