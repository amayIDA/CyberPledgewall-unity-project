using TMPro;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Networking half of the drawing station: connects to the wall (address from drawing_config.json)
/// and sends the finished pledge. The visitor-facing screens live in <see cref="DrawingFlow"/>.
/// </summary>
public class DrawingAppController : MonoBehaviour
{
    [SerializeField] private DrawingCanvas drawingCanvas;
    [SerializeField] private TMP_Text statusText;

    private DrawingConfig config;
    private Coroutine waitRoutine;

    public bool IsConnected => NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient;
    public string CurrentHost => config != null && !string.IsNullOrEmpty(config.hostAddress) ? config.hostAddress : "127.0.0.1";

    private void Awake()
    {
        if (drawingCanvas == null)
            drawingCanvas = FindAnyObjectByType<DrawingCanvas>();

        if (statusText == null)
        {
            var go = GameObject.Find("ClientStatusText");
            if (go != null)
                statusText = go.GetComponent<TMP_Text>();
        }

        config = AppConfig.LoadDrawing();
    }

    private void Start()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            SetStatus("NetworkManager is missing.");
            return;
        }

        nm.OnClientConnectedCallback += HandleClientConnected;
        nm.OnClientDisconnectCallback += HandleClientDisconnected;

        if (config != null && config.autoConnect)
            StartCoroutine(AutoConnectLoop());
    }

    private System.Collections.IEnumerator AutoConnectLoop()
    {
        float interval = Mathf.Max(1f, config.retryIntervalSeconds);
        while (true)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && !nm.IsListening)
                ConnectToWall();

            yield return new WaitForSeconds(interval);
        }
    }

    private void OnDisable()
    {
        if (subscribedAck)
        {
            WallReceiver.PledgeAccepted -= OnPledgeAccepted;
            subscribedAck = false;
        }

        if (NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
    }

    public void ConnectToWall()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            SetStatus("NetworkManager is missing.");
            return;
        }

        if (nm.IsConnectedClient || nm.IsListening)
            return;

        string ip = config != null && !string.IsNullOrEmpty(config.hostAddress) ? config.hostAddress : "127.0.0.1";
        ushort port = config != null ? config.port : NetworkSession.DefaultPort;

        var session = nm.GetComponent<NetworkSession>();
        if (session != null)
        {
            session.SetPort(port);
            session.ConfigureAsClient(ip);
        }
        else
            nm.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>()?.SetConnectionData(ip, port);

        SetStatus($"Connecting to {ip}:{port}...");
        if (!nm.StartClient())
        {
            SetStatus("Failed to start client.");
            return;
        }

        if (waitRoutine != null)
            StopCoroutine(waitRoutine);
        waitRoutine = StartCoroutine(WaitForConnection(ip, port));
    }

    private System.Collections.IEnumerator WaitForConnection(string ip, ushort port)
    {
        float timeout = 5f;
        float elapsed = 0f;
        var nm = NetworkManager.Singleton;
        while (nm != null && !nm.IsConnectedClient && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (nm != null && nm.IsConnectedClient)
            yield break;

        if (nm != null && nm.IsListening)
            nm.Shutdown();

        SetStatus($"Could not reach wall at {ip}:{port}. Long-press the top of the screen and enter the wall PC IP.");
    }

    public void ApplyHostAddress(string ip)
    {
        if (config == null)
            config = AppConfig.LoadDrawing();

        config.hostAddress = string.IsNullOrWhiteSpace(ip) ? "127.0.0.1" : ip.Trim();
        AppConfig.SaveDrawing(config);
        StartCoroutine(Reconnect());
    }

    private System.Collections.IEnumerator Reconnect()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null && (nm.IsListening || nm.IsConnectedClient))
            nm.Shutdown();

        float timeout = 2f;
        float elapsed = 0f;
        while (nm != null && nm.IsListening && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        ConnectToWall();
    }

    private readonly System.Collections.Generic.HashSet<string> acknowledged = new System.Collections.Generic.HashSet<string>();
    private bool subscribedAck;

    private void OnPledgeAccepted(string pledgeId) => acknowledged.Add(pledgeId);

    public bool IsAcknowledged(string pledgeId) => acknowledged.Remove(pledgeId);

    /// <summary>
    /// Sends the visitor's name, email ID and drawing to the wall in chunks. Returns false (with a reason)
    /// if it can't; otherwise <paramref name="pledgeId"/> can be polled with <see cref="IsAcknowledged"/>.
    /// </summary>
    public bool TrySubmit(string visitorName, string emailId, out string pledgeId, out string error)
    {
        error = null;
        pledgeId = null;
        if (!subscribedAck)
        {
            WallReceiver.PledgeAccepted += OnPledgeAccepted;
            subscribedAck = true;
        }

        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsClient || !nm.IsConnectedClient)
        {
            error = "Not connected to the wall yet. Please try again in a moment.";
            SetStatus(error);
            return false;
        }

        var receiver = WallReceiver.Instance;
        if (receiver == null)
            receiver = FindAnyObjectByType<WallReceiver>();

        if (receiver == null || !receiver.IsSpawned)
        {
            error = "Wall receiver is not ready yet. Wait a moment after Connected, then Send again.";
            SetStatus(error);
            return false;
        }

        byte[] drawingBytes = drawingCanvas != null ? drawingCanvas.EncodeForNetwork() : System.Array.Empty<byte>();
        if (drawingBytes.Length > NetworkSession.DefaultMaxPayloadSize - 4096)
        {
            error = "Drawing is too large to send. Try a simpler sketch.";
            SetStatus(error);
            return false;
        }

        pledgeId = System.Guid.NewGuid().ToString("N");
        int count = Mathf.Max(1, Mathf.CeilToInt(drawingBytes.Length / (float)WallReceiver.ChunkSize));
        for (int i = 0; i < count; i++)
        {
            int start = i * WallReceiver.ChunkSize;
            int length = Mathf.Min(WallReceiver.ChunkSize, drawingBytes.Length - start);
            var chunk = new byte[Mathf.Max(0, length)];
            if (length > 0)
                System.Buffer.BlockCopy(drawingBytes, start, chunk, 0, length);
            receiver.SubmitPledgeChunkServerRpc(pledgeId, i, count, visitorName, emailId, chunk);
        }

        SetStatus($"Sending pledge ({drawingBytes.Length / 1024} KB)...");
        return true;
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId)
            SetStatus("Connected to wall.");
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null || clientId != NetworkManager.Singleton.LocalClientId)
            return;

        string reason = NetworkManager.Singleton.DisconnectReason;
        if (string.IsNullOrEmpty(reason))
            SetStatus("Disconnected from wall. Is WallScene hosting on that IP?");
        else
            SetStatus($"Disconnected: {reason}");
    }

    public void SetStatus(string message)
    {
        Debug.Log(message);
        if (statusText != null)
            statusText.text = message;
    }
}
