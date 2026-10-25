using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class WallManager : MonoBehaviour
{
    public static WallManager Instance { get; private set; }

    [SerializeField] private WallBoard board;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject wallReceiverPrefab;
    private bool statusVisible;


private void Awake()
    {
        Instance = this;
        if (board == null)
            board = FindAnyObjectByType<WallBoard>();

        if (statusText == null)
        {
            var status = GameObject.Find("WallStatusText");
            if (status != null)
                statusText = status.GetComponent<TMP_Text>();
        }

        SetStatusVisible(false);

        var clearButtonGo = GameObject.Find("ClearAllButton");
        if (clearButtonGo != null)
        {
            var clearButton = clearButtonGo.GetComponent<UnityEngine.UI.Button>();
            if (clearButton != null)
                clearButton.onClick.AddListener(ClearAllPledges);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        bool ctrl = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
        if (ctrl && keyboard.aKey.wasPressedThisFrame)
            SetStatusVisible(!statusVisible);
    }

private void Start()
    {
        EnsureStorageManager();
        LoadSavedPledgesOntoWall();

        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            SetStatus("NetworkManager is missing.");
            return;
        }

        if (nm.IsListening)
        {
            SetStatus("Already listening as host.");
            return;
        }

        WallConfig config = AppConfig.LoadWall();
        var session = nm.GetComponent<NetworkSession>();
        if (session != null)
        {
            session.SetPort(config.port);
            session.ConfigureAsHost();
        }

        if (!nm.StartHost())
        {
            SetStatus($"Cannot start the wall: port {config.port} is in use. Another wall window is probably already running - close it and restart.");
            if (statusText != null)
            {
                statusText.color = new Color(1f, 0.55f, 0.55f, 1f);
                statusText.fontSize = 30f;
            }

            return;
        }

        SpawnWallReceiver(nm);
        string nic;
        string lan = LanAddress.Best(out nic);
        config.detectedHostAddress = lan;
        AppConfig.SaveWall(config);

        var beacon = gameObject.GetComponent<WallBeaconHost>();
        if (beacon == null)
            beacon = gameObject.AddComponent<WallBeaconHost>();
        beacon.Begin(config.port);

        string setup = WindowsLanSetup.Prepare(config.port, lan);
        string where = string.IsNullOrEmpty(nic) ? lan : lan + " (" + nic + ")";
        string message = setup ?? $"Tablet connects to {where}:{config.port}";
        Debug.Log(message);
        if (statusText != null)
        {
            statusText.text = message;
            if (setup != null)
            {
                statusText.color = new Color(1f, 0.85f, 0.45f, 1f);
                statusText.fontSize = 28f;
            }
        }
    }

    private void SpawnWallReceiver(NetworkManager nm)
    {
        GameObject prefab = wallReceiverPrefab;
        if (prefab == null)
        {
            var session = nm.GetComponent<NetworkSession>();
            if (session != null)
                prefab = session.WallReceiverPrefab;
        }

        if (prefab == null)
        {
            Debug.LogError("WallReceiver prefab is not assigned.");
            return;
        }

        var instance = Instantiate(prefab);
        instance.name = "WallReceiver";
        var netObj = instance.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError("WallReceiver prefab needs a NetworkObject.");
            return;
        }

        netObj.Spawn();
    }

    public void AddPledgeCard(string visitorName, byte[] drawingBytes)
    {
        if (board == null)
        {
            Debug.LogError("WallBoard is not assigned.");
            return;
        }

        board.Add(visitorName, drawingBytes);
    }

public void ReceiveNetworkPledge(string visitorName, string emailId, byte[] drawingBytes)
    {
        EnsureStorageManager();
        if (PledgeStorageManager.Instance != null)
            PledgeStorageManager.Instance.SavePledge(visitorName, emailId, drawingBytes);

        AddPledgeCard(visitorName, drawingBytes);
    }

public void LoadSavedPledgesOntoWall()
    {
        EnsureStorageManager();
        if (PledgeStorageManager.Instance == null)
            return;

        Debug.Log($"Loading pledges from {PledgeStorageManager.SavePath}");
        PledgeDatabase database = PledgeStorageManager.Instance.LoadPledges();
        if (database == null || database.pledges == null || database.pledges.Count == 0)
        {
            Debug.Log("No saved pledges found yet. Pledges are only stored after a successful host RPC from a connected drawing client.");
            return;
        }

        foreach (PledgeEntry entry in database.pledges)
        {
            if (entry == null)
                continue;

            byte[] imageBytes = PledgeStorageManager.DecodeImageBytes(entry.imageBase64);
            string label = !string.IsNullOrEmpty(entry.visitorName) ? entry.visitorName : entry.pledgeText;
            if (board != null)
                board.AddSilently(label, imageBytes);
        }

        if (board != null)
            board.Refresh();

        Debug.Log($"Restored {database.pledges.Count} saved pledges onto the wall.");
    }

    public void ClearDisplayedPledges()
    {
        if (board != null)
            board.Clear();
    }

    public void ClearAllPledges()
    {
        EnsureStorageManager();
        if (PledgeStorageManager.Instance != null)
            PledgeStorageManager.Instance.ClearAllPledges();
        else
            ClearDisplayedPledges();
    }

    private void EnsureStorageManager()
    {
        if (PledgeStorageManager.Instance != null)
            return;

        var existing = FindAnyObjectByType<PledgeStorageManager>();
        if (existing != null)
            return;

        var go = new GameObject("PledgeStorageManager");
        go.AddComponent<PledgeStorageManager>();
    }


    private void SetStatus(string message)
    {
        Debug.Log(message);
        if (statusText != null)
            statusText.text = message;
    }

    private void SetStatusVisible(bool visible)
    {
        statusVisible = visible;
        if (statusText != null)
            statusText.gameObject.SetActive(visible);
    }

    private static string GetLanIPv4()
    {
        return LanAddress.Best();
    }
}
