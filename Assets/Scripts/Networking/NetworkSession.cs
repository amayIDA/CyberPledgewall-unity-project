using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

[RequireComponent(typeof(NetworkManager))]
[RequireComponent(typeof(UnityTransport))]
public class NetworkSession : MonoBehaviour
{
    public const ushort DefaultPort = 7777;
    public const int DefaultMaxPayloadSize = 524288;

    [SerializeField] private GameObject wallReceiverPrefab;
    [SerializeField] private ushort port = DefaultPort;

    public GameObject WallReceiverPrefab => wallReceiverPrefab;
    public ushort Port => port;

    public void SetPort(ushort newPort)
    {
        port = newPort;
    }

private void Awake()
    {
        DontDestroyOnLoad(gameObject);

        var nm = GetComponent<NetworkManager>();
        if (nm != null)
        {
            nm.NetworkConfig.EnableSceneManagement = false;
            nm.NetworkConfig.PlayerPrefab = null;
        }

        var transport = GetComponent<UnityTransport>();
        if (transport != null)
            transport.MaxPayloadSize = DefaultMaxPayloadSize;
    }



    public void ConfigureAsHost()
    {
        var transport = GetComponent<UnityTransport>();
        if (transport == null)
            return;

        transport.SetConnectionData("0.0.0.0", port, "0.0.0.0");
        transport.MaxPayloadSize = DefaultMaxPayloadSize;
    }

    public void ConfigureAsClient(string ipv4Address)
    {
        var transport = GetComponent<UnityTransport>();
        if (transport == null)
            return;

        transport.SetConnectionData(ipv4Address, port);
        transport.MaxPayloadSize = DefaultMaxPayloadSize;
    }
}
