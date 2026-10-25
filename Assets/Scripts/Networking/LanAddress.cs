using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

/// <summary>
/// Picks the IPv4 address a tablet on the ethernet cable can actually reach.
/// Skips loopback and virtual adapters (Hyper-V, WSL, Docker), which used to be
/// reported first and sent the tablet to an address that is not on the cable.
/// </summary>
public static class LanAddress
{
    public struct Candidate
    {
        public string Ip;
        public string NicName;
        public IPAddress Broadcast;
        public bool Private;
        public bool LinkLocal;
        public bool Ethernet;
    }

    public static bool IsUnusable(string ip)
    {
        return string.IsNullOrEmpty(ip) || ip == "127.0.0.1" || ip.StartsWith("169.254.");
    }

    public static string Best(out string nicName)
    {
        Candidate chosen = default;
        int bestScore = int.MinValue;
        foreach (Candidate candidate in List())
        {
            int score = Score(candidate);
            if (score > bestScore)
            {
                bestScore = score;
                chosen = candidate;
            }
        }

        if (bestScore == int.MinValue)
        {
            nicName = string.Empty;
            return "127.0.0.1";
        }

        nicName = chosen.NicName;
        return chosen.Ip;
    }

    public static string Best()
    {
        return Best(out _);
    }

    public static List<Candidate> List()
    {
        var found = new List<Candidate>();
        NetworkInterface[] nics;
        try
        {
            nics = NetworkInterface.GetAllNetworkInterfaces();
        }
        catch
        {
            return found;
        }

        foreach (NetworkInterface nic in nics)
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
                continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;
            if (IsVirtual(nic))
                continue;

            foreach (UnicastIPAddressInformation addr in nic.GetIPProperties().UnicastAddresses)
            {
                if (addr.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;
                if (IPAddress.IsLoopback(addr.Address))
                    continue;

                string ip = addr.Address.ToString();
                found.Add(new Candidate
                {
                    Ip = ip,
                    NicName = nic.Name,
                    Broadcast = BroadcastOf(addr),
                    Private = IsPrivate(addr.Address),
                    LinkLocal = ip.StartsWith("169.254."),
                    Ethernet = nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet
                });
            }
        }

        return found;
    }

    public static string FirstEthernetName()
    {
        try
        {
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up)
                    continue;
                if (nic.NetworkInterfaceType != NetworkInterfaceType.Ethernet)
                    continue;
                if (IsVirtual(nic))
                    continue;
                return nic.Name;
            }
        }
        catch
        {
            // No ethernet name available.
        }

        return string.Empty;
    }

    private static int Score(Candidate candidate)
    {
        int score = 0;
        if (candidate.Ethernet) score += 50;
        if (candidate.Private) score += 40;
        else if (!candidate.LinkLocal) score += 10;
        return score;
    }

    private static bool IsPrivate(IPAddress ip)
    {
        byte[] b = ip.GetAddressBytes();
        if (b[0] == 10) return true;
        if (b[0] == 192 && b[1] == 168) return true;
        return b[0] == 172 && b[1] >= 16 && b[1] <= 31;
    }

    private static bool IsVirtual(NetworkInterface nic)
    {
        string text = (nic.Name + " " + nic.Description).ToLowerInvariant();
        return text.Contains("virtual")
            || text.Contains("vethernet")
            || text.Contains("hyper-v")
            || text.Contains("vmware")
            || text.Contains("vbox")
            || text.Contains("wsl")
            || text.Contains("docker")
            || text.Contains("bluetooth")
            || text.Contains("loopback")
            || text.Contains("tunnel")
            || text.Contains("vpn")
            || text.Contains("pseudo");
    }

    private static IPAddress BroadcastOf(UnicastIPAddressInformation addr)
    {
        try
        {
            byte[] ip = addr.Address.GetAddressBytes();
            byte[] mask = addr.IPv4Mask != null ? addr.IPv4Mask.GetAddressBytes() : null;
            if (mask == null || mask.Length != 4)
                return IPAddress.Broadcast;

            var broadcast = new byte[4];
            for (int i = 0; i < 4; i++)
                broadcast[i] = (byte)(ip[i] | (mask[i] ^ 255));
            return new IPAddress(broadcast);
        }
        catch
        {
            return IPAddress.Broadcast;
        }
    }
}
