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
    private NetworkManager subscribedManager;
    private bool started;
    private bool paused;
    private bool quitting;
    private bool connecting;
    private bool resetRequested;
    private float nextAttempt;
    private float connectionDeadline;
    private const float RetrySeconds = 3f;
    private const float ConnectTimeoutSeconds = 10f;

    public string ConnectionStatus { get; private set; } = "Enter the wall PC IP and tap Connect.";

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
        SetStatusVisible(false);
        DrawingKiosk.KeepScreenOn();
        Application.runInBackground = true;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }

    private void Start()
    {
        started = true;
        SubscribeNetwork();
        if (GetComponent<WallBeaconClient>() == null)
            gameObject.AddComponent<WallBeaconClient>();
        nextAttempt = Time.realtimeSinceStartup + 1.5f;
    }

    private void OnEnable()
    {
        if (started)
        {
            SubscribeNetwork();
            resetRequested = true;
        }
    }

    private void SubscribeNetwork()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || subscribedManager == nm)
            return;
        UnsubscribeNetwork();
        subscribedManager = nm;
        nm.OnClientConnectedCallback += HandleClientConnected;
        nm.OnClientDisconnectCallback += HandleClientDisconnected;
    }

    private void UnsubscribeNetwork()
    {
        if (subscribedManager != null)
        {
            subscribedManager.OnClientConnectedCallback -= HandleClientConnected;
            subscribedManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
        subscribedManager = null;
    }

    private void Update()
    {
        if (!started || paused || quitting)
            return;

        SubscribeNetwork();
        if (WallBeaconClient.HasFound)
            UseDiscoveredHost(WallBeaconClient.FoundIp, WallBeaconClient.FoundPort);

        var nm = NetworkManager.Singleton;
        if (nm == null || nm.IsServer)
            return;

        // Lifecycle and UI events only request recovery. Shutdown/start happen here,
        // outside Netcode callbacks, and only after the previous shutdown finishes.
        if (resetRequested)
        {
            resetRequested = false;
            connecting = false;
            if (!nm.ShutdownInProgress && (nm.IsListening || nm.IsClient))
                nm.Shutdown();
            nextAttempt = Time.realtimeSinceStartup + 1f;
        }
        if (nm.ShutdownInProgress)
            return;
        if (nm.IsConnectedClient)
            return;

        float now = Time.realtimeSinceStartup;
        if (connecting)
        {
            if (now < connectionDeadline)
                return;
            connecting = false;
            nm.Shutdown();
            nextAttempt = now + RetrySeconds;
            SetStatus($"Could not reach wall at {CurrentHost}. Retrying automatically...");
            return;
        }
        if (now < nextAttempt)
            return;
        if (nm.IsListening || nm.IsClient)
        {
            nm.Shutdown();
            nextAttempt = now + RetrySeconds;
            return;
        }
        if (config == null || !AppConfig.IsRealHost(config.hostAddress))
            return;

        string ip = config.hostAddress;
        var session = nm.GetComponent<NetworkSession>();
        if (session != null)
        {
            session.SetPort(config.port);
            session.ConfigureAsClient(ip);
        }
        else
            nm.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>()?.SetConnectionData(ip, config.port);

        SetStatus($"Connecting to {ip}:{config.port}...");
        nextAttempt = now + RetrySeconds;
        connecting = nm.StartClient();
        connectionDeadline = now + ConnectTimeoutSeconds;
        if (!connecting)
            SetStatus("Could not start connection. Retrying automatically...");
    }

    private void OnApplicationPause(bool isPaused)
    {
        paused = isPaused;
        if (isPaused)
            return;

        DrawingKiosk.KeepScreenOn();
        // Android may have suspended heartbeats while the PC dropped this client.
        // Discard even a locally 'connected' transport and use the saved endpoint.
        config = AppConfig.LoadDrawing();
        resetRequested = true;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            return;
        DrawingKiosk.KeepScreenOn();
        // Focus also changes for the keyboard/dialogs. Don't tear down a healthy
        // connection here or race the pause/resume recovery request.
        if (!IsConnected)
            nextAttempt = Time.realtimeSinceStartup;
    }

    private void OnApplicationQuit() => quitting = true;

    private void OnDisable()
    {
        UnsubscribeNetwork();
        if (subscribedAck)
        {
            WallReceiver.PledgeAccepted -= OnPledgeAccepted;
            subscribedAck = false;
        }
    }

    public void ConnectToWall()
    {
        if (!IsConnected && !connecting)
            nextAttempt = Time.realtimeSinceStartup;
    }

    private void UseDiscoveredHost(string ip, ushort port)
    {
        if (string.IsNullOrEmpty(ip))
            return;
        if (config == null)
            config = AppConfig.LoadDrawing();
        if (AppConfig.IsRealHost(config.hostAddress) && config.hostAddress != ip)
            return;
        if (config.hostAddress == ip && config.port == port)
            return;

        config.hostAddress = ip;
        config.port = port;
        AppConfig.SaveDrawing(config);
        resetRequested = true;
    }

    public void ApplyHostAddress(string ip)
    {
        if (config == null)
            config = AppConfig.LoadDrawing();
        config.hostAddress = string.IsNullOrWhiteSpace(ip) ? "127.0.0.1" : ip.Trim();
        AppConfig.SaveDrawing(config);
        SetStatus($"Connecting to {config.hostAddress}:{config.port}...");
        resetRequested = true;
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
        {
            connecting = false;
            AppConfig.SaveDrawing(config);
            SetStatus("Connected to wall.");
        }
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null || clientId != NetworkManager.Singleton.LocalClientId)
            return;

        // Intentional recovery shutdown already has its next attempt scheduled.
        if (NetworkManager.Singleton.ShutdownInProgress)
            return;

        string reason = NetworkManager.Singleton.DisconnectReason;
        if (string.IsNullOrEmpty(reason))
            SetStatus("Disconnected from wall. Reconnecting...");
        else
            SetStatus($"Disconnected: {reason}");

        connecting = false;
        nextAttempt = Time.realtimeSinceStartup + RetrySeconds;
    }

    public void SetStatus(string message)
    {
        ConnectionStatus = message;
        Debug.Log(message);
        if (statusText != null)
            statusText.text = message;
    }

    public void SetStatusVisible(bool visible)
    {
        if (statusText != null)
            statusText.gameObject.SetActive(visible);
    }
}
