using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// One-time Windows setup for a wall PC that has no internet:
/// allow the pledge port through the firewall, and when ethernet has no real
/// address (direct cable, no router), offer to set 192.168.4.1.
/// </summary>
public static class WindowsLanSetup
{
    public const string DirectWallIp = "192.168.4.1";
    public const string DirectTabletIp = "192.168.4.2";

    private static bool _prompted;

    public static string DirectCableInstructions =>
        "Approve the Windows prompt, then restart the wall app. On the tablet open Ethernet settings and set IP "
        + DirectTabletIp + ", mask 255.255.255.0, gateway blank. The wall is " + DirectWallIp + ".";

    /// <summary>
    /// Returns a message the wall should show, or null when the PC already has a usable address.
    /// </summary>
    public static string Prepare(ushort gamePort, string bestIp)
    {
        if (Application.platform != RuntimePlatform.WindowsPlayer &&
            Application.platform != RuntimePlatform.WindowsEditor)
            return null;

        bool unusable = LanAddress.IsUnusable(bestIp);
        if (_prompted)
            return unusable ? DirectCableInstructions : null;

        string ports = NetworkSession.DefaultPort + "," + NetworkSession.BeaconPort;
        if (gamePort != NetworkSession.DefaultPort && gamePort != NetworkSession.BeaconPort)
            ports = ports + "," + gamePort;

        bool needFirewall = !RuleExists();
        if (!needFirewall && !unusable)
            return null;

        _prompted = true;
        string script = FirewallCommands(ports);
        if (unusable)
            script += StaticIpCommands();

        RunElevated(script);
        return unusable ? DirectCableInstructions : "Approve the Windows prompt so the tablet is allowed to connect, then try again.";
    }

    private static bool RuleExists()
    {
        try
        {
            var start = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "advfirewall firewall show rule name=\"Cyber Pledge Wall\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using (Process process = Process.Start(start))
            {
                if (process == null)
                    return false;
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(4000);
                return process.ExitCode == 0 && output.IndexOf("No rules match", System.StringComparison.OrdinalIgnoreCase) < 0;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("Could not check the firewall rule: " + ex.Message);
            return false;
        }
    }

    private static string FirewallCommands(string ports)
    {
        return "netsh advfirewall firewall add rule name=\"Cyber Pledge Wall\" dir=in action=allow protocol=UDP localport="
            + ports + " profile=any\r\n";
    }

    private static string StaticIpCommands()
    {
        string name = Sanitize(LanAddress.FirstEthernetName());
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        return "netsh interface ip set address name=\"" + name + "\" static " + DirectWallIp + " 255.255.255.0\r\n";
    }

    private static void RunElevated(string commands)
    {
        try
        {
            string directory = AppConfig.Directory;
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string path = Path.Combine(directory, "SetupWallNetwork.bat");
            File.WriteAllText(path, "@echo off\r\n" + commands);
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                Verb = "runas"
            });
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("Network setup was not started: " + ex.Message);
        }
    }

    private static string Sanitize(string name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        var kept = new System.Text.StringBuilder();
        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_')
                kept.Append(c);
        }

        return kept.ToString().Trim();
    }
}
