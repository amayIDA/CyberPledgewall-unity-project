using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Config button on the drawing station. Tap it to enter the wall PC IP.
/// This object's rect, image color, and label are authored in the editor and are not changed at runtime.
/// </summary>
public class HostIpPanel : MonoBehaviour
{
    [SerializeField] private DrawingAppController network;
    [SerializeField] private Button configButton;

    private RectTransform _panel;
    private TMP_InputField _input;
    private TMP_Text _status;

    private void Awake()
    {
        if (network == null)
            network = FindAnyObjectByType<DrawingAppController>();

        if (configButton == null)
            configButton = GetComponent<Button>();

        EnsureDialog();

        if (configButton != null)
            configButton.onClick.AddListener(() => SetOpen(true));

        SetOpen(false);
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
        if (network != null)
            network.SetStatusVisible(open);
        if (!open)
            return;

        _panel.SetAsLastSibling();

        if (_input != null)
        {
            _input.text = network != null ? network.CurrentHost : string.Empty;
            _input.ActivateInputField();
        }

        SetStatus(network != null && network.IsConnected
            ? "Connected. Tap Config again if you need to change the IP."
            : "Both ethernet cables must be on the same network. With no router, set this tablet's Ethernet IP to "
              + WindowsLanSetup.DirectTabletIp + ", mask 255.255.255.0, gateway blank. The wall PC is "
              + WindowsLanSetup.DirectWallIp + ".");
    }

    private void SetStatus(string message)
    {
        if (_status != null)
            _status.text = message;
    }

    private void EnsureDialog()
    {
        Transform existing = transform.Find("Panel");
        if (existing == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                existing = canvas.transform.Find("WallIpPanel");
        }

        if (existing != null)
        {
            _panel = (RectTransform)existing;
            _input = _panel.GetComponentInChildren<TMP_InputField>(true);
            Transform statusGo = _panel.Find("Status");
            if (statusGo != null)
                _status = statusGo.GetComponent<TMP_Text>();
            WireDialogButtons();
            return;
        }

        Canvas parentCanvas = GetComponentInParent<Canvas>();
        Transform parent = parentCanvas != null ? parentCanvas.transform : transform;

        var panelGo = new GameObject("WallIpPanel", typeof(RectTransform), typeof(Image));
        panelGo.transform.SetParent(parent, false);
        _panel = (RectTransform)panelGo.transform;
        _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0.5f);
        _panel.pivot = new Vector2(0.5f, 0.5f);
        _panel.anchoredPosition = Vector2.zero;
        _panel.sizeDelta = new Vector2(760f, 280f);
        var panelImage = panelGo.GetComponent<Image>();
        panelImage.color = new Color(0.02f, 0.06f, 0.18f, 0.94f);
        panelImage.raycastTarget = true;

        var title = CreateTmp("Title", _panel, "Wall IP", 28f, new Vector2(0f, -28f), new Vector2(700f, 40f));
        title.color = Color.white;

        _input = CreateInput(_panel, "IpInput", "192.168.1.10", new Vector2(0f, -118f), new Vector2(700f, 56f));
        _input.onSubmit.AddListener(_ => Connect());

        CreateButton(_panel, "ConnectButton", "Connect", new Vector2(-110f, -196f), new Vector2(200f, 48f), Connect);
        CreateButton(_panel, "CloseButton", "Close", new Vector2(110f, -196f), new Vector2(180f, 48f), () => SetOpen(false));

        _status = CreateTmp("Status", _panel, string.Empty, 18f, new Vector2(0f, -250f), new Vector2(720f, 36f));
        _status.color = new Color(0.75f, 0.9f, 1f, 1f);
    }

    private void WireDialogButtons()
    {
        Bind(_panel.Find("ConnectButton"), Connect);
        Bind(_panel.Find("CloseButton"), () => SetOpen(false));
    }

    private static void Bind(Transform target, UnityEngine.Events.UnityAction action)
    {
        if (target == null)
            return;

        Button button = target.GetComponent<Button>();
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private static TMP_FontAsset Font()
    {
        if (TMP_Settings.defaultFontAsset != null)
            return TMP_Settings.defaultFontAsset;

        TextMeshProUGUI sample = FindAnyObjectByType<TextMeshProUGUI>();
        return sample != null ? sample.font : null;
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
        tmp.font = Font();
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
        TMP_FontAsset font = Font();
        if (input.textComponent != null)
            input.textComponent.font = font;
        if (input.placeholder is TMP_Text ph)
        {
            ph.text = placeholder;
            ph.font = font;
        }
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
            tmp.font = Font();
            tmp.text = label;
            tmp.fontSize = 22;
        }
        go.GetComponent<Button>().onClick.AddListener(onClick);
    }
}
