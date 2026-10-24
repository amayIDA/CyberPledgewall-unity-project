using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Network entry point on the wall. Drawings arrive in small chunks (a single multi-hundred-KB RPC
/// overflows the transport's packet queue), are reassembled here, handed to the WallManager, and the
/// sending phone is told the pledge landed so it can show "Thank you" only when it really did.
/// </summary>
public class WallReceiver : NetworkBehaviour
{
    public const int ChunkSize = 16 * 1024;

    public static WallReceiver Instance { get; private set; }

    /// <summary>Raised on the drawing client when the wall confirms a pledge id.</summary>
    public static event Action<string> PledgeAccepted;

    private class Partial
    {
        public string name;
        public string email;
        public byte[][] chunks;
        public int received;
    }

    private readonly Dictionary<string, Partial> partials = new Dictionary<string, Partial>();

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
    public void SubmitPledgeChunkServerRpc(string pledgeId, int index, int count, string visitorName, string emailId,
        byte[] chunk, ServerRpcParams rpcParams = default)
    {
        if (!IsServer || string.IsNullOrEmpty(pledgeId) || count < 1 || index < 0 || index >= count)
            return;

        if (!partials.TryGetValue(pledgeId, out Partial p))
        {
            p = new Partial { name = visitorName, email = emailId, chunks = new byte[count][] };
            partials[pledgeId] = p;
        }

        if (p.chunks.Length != count || p.chunks[index] != null)
            return;

        p.chunks[index] = chunk ?? Array.Empty<byte>();
        p.received++;
        if (p.received < count)
            return;

        partials.Remove(pledgeId);

        int total = 0;
        foreach (byte[] part in p.chunks)
            total += part.Length;

        var bytes = new byte[total];
        int offset = 0;
        foreach (byte[] part in p.chunks)
        {
            Buffer.BlockCopy(part, 0, bytes, offset, part.Length);
            offset += part.Length;
        }

        if (WallManager.Instance == null)
        {
            Debug.LogWarning("WallReceiver received a pledge but WallManager is missing.");
            return;
        }

        Debug.Log($"Wall received pledge from '{p.name}' ({bytes.Length} bytes).");
        WallManager.Instance.ReceiveNetworkPledge(p.name, p.email, bytes);

        var target = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { rpcParams.Receive.SenderClientId } }
        };
        PledgeAcceptedClientRpc(pledgeId, target);
    }

    [ClientRpc]
    private void PledgeAcceptedClientRpc(string pledgeId, ClientRpcParams clientRpcParams = default)
    {
        PledgeAccepted?.Invoke(pledgeId);
    }
}
