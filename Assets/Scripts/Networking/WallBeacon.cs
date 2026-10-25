using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

/// <summary>
/// The wall announces itself on the local ethernet with a UDP broadcast.
/// The tablet picks that up, so it does not depend on a typed-in address.
/// </summary>
public class WallBeaconHost : MonoBehaviour
{
    private const float Interval = 1f;

    private ushort _gamePort;
    private float _nextSend;
    private UdpClient _socket;

    public void Begin(ushort gamePort)
    {
        _gamePort = gamePort;
        Open();
    }

    private void Update()
    {
        if (_socket == null || Time.unscaledTime < _nextSend)
            return;

        _nextSend = Time.unscaledTime + Interval;
        Send();
    }

    private void OnDestroy()
    {
        Close();
    }

    private void Open()
    {
        try
        {
            _socket = new UdpClient();
            _socket.EnableBroadcast = true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Wall beacon could not open: " + ex.Message);
            Close();
        }
    }

    private void Send()
    {
        string ip = LanAddress.Best();
        if (string.IsNullOrEmpty(ip) || ip == "127.0.0.1")
            return;

        byte[] payload = Encoding.UTF8.GetBytes("CPW1|" + ip + "|" + _gamePort);
        try
        {
            _socket.Send(payload, payload.Length, new IPEndPoint(IPAddress.Broadcast, NetworkSession.BeaconPort));
            foreach (LanAddress.Candidate candidate in LanAddress.List())
            {
                if (candidate.Broadcast == null || candidate.Ip != ip)
                    continue;
                _socket.Send(payload, payload.Length, new IPEndPoint(candidate.Broadcast, NetworkSession.BeaconPort));
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Wall beacon send failed: " + ex.Message);
        }
    }

    private void Close()
    {
        if (_socket == null)
            return;

        _socket.Close();
        _socket = null;
    }
}

/// <summary>Listens for the wall's broadcast and remembers the newest address.</summary>
public class WallBeaconClient : MonoBehaviour
{
    public static string FoundIp { get; private set; }
    public static ushort FoundPort { get; private set; }
    public static bool HasFound { get; private set; }

    private UdpClient _socket;

    private void Start()
    {
        try
        {
            _socket = new UdpClient();
            _socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _socket.Client.Bind(new IPEndPoint(IPAddress.Any, NetworkSession.BeaconPort));
            _socket.EnableBroadcast = true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Could not listen for the wall: " + ex.Message);
            if (_socket != null)
            {
                _socket.Close();
                _socket = null;
            }
        }
    }

    private void Update()
    {
        if (_socket == null)
            return;

        try
        {
            while (_socket.Available > 0)
            {
                var remote = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = _socket.Receive(ref remote);
                Parse(Encoding.UTF8.GetString(data));
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Wall beacon listen failed: " + ex.Message);
        }
    }

    private void OnDestroy()
    {
        if (_socket == null)
            return;

        _socket.Close();
        _socket = null;
    }

    private static void Parse(string text)
    {
        if (string.IsNullOrEmpty(text) || !text.StartsWith("CPW1|"))
            return;

        string[] parts = text.Split('|');
        if (parts.Length < 3)
            return;
        if (!IPAddress.TryParse(parts[1], out IPAddress ip) || ip.AddressFamily != AddressFamily.InterNetwork)
            return;
        if (!ushort.TryParse(parts[2], out ushort port))
            return;

        FoundIp = ip.ToString();
        FoundPort = port;
        HasFound = true;
    }
}
