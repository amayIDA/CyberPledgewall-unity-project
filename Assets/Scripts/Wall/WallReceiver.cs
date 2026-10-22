using Unity.Netcode;
using UnityEngine;

public class WallReceiver : NetworkBehaviour
{
    public static WallReceiver Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        Instance = this;
    }

    public override void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        base.OnDestroy();
    }

[ServerRpc(RequireOwnership = false)]
    public void SubmitPledgeServerRpc(string visitorName, string employeeId, byte[] drawingBytes)
    {
        if (!IsServer)
            return;

        if (WallManager.Instance == null)
        {
            Debug.LogWarning("WallReceiver received a pledge but WallManager is missing.");
            return;
        }

        WallManager.Instance.ReceiveNetworkPledge(visitorName, employeeId, drawingBytes);
    }
}
