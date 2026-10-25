# Cyber Pledge Wall — connection handoff

Give this document to an assistant that is helping get the Android drawing tablet connected to the wall PC. The goal is a local connection only. Internet is not required and does not fix the connection.

## What the two apps are

This Unity project builds two roles:

- **Wall** runs on a Windows PC. It hosts the session and shows the pledges.
- **Drawing** runs on an Android tablet. The visitor draws there and sends the pledge to the wall.

The PC and the tablet must be on the **same local network**. The tablet connects to the wall PC's IP. The wall does not connect to the tablet's IP.

They talk with Unity Netcode over **UDP**.

- Game port: **7777**
- Discovery broadcast port: **7778**
- The wall listens on all adapters (`0.0.0.0`).
- The tablet is the client.

## What to type in the tablet Config button

The Config button is on the drawing app. It saves the **wall PC address** onto the tablet. It does not change the wall PC's config.

Type the wall PC's IP only. Do not type the tablet's own IP. Do not type the port.

Example: if the wall screen says `Tablet connects to 192.168.1.20:7777`, type `192.168.1.20` and tap Connect.

That value is stored on the tablet in both places:

- `drawing_config.json` inside the app's private storage (`Android/data/<package>/files/`)
- Android preferences under the key `drawing_host`

On the next launch the tablet reads `drawing_host` first. Editing a json file on the PC does not change the tablet.

## The two json files

| File | Where it matters | What it is for |
|---|---|---|
| `wall_config.json` | Next to the Windows wall exe, or the project root in the Editor | Port, whether the wall shows its address, and `detectedHostAddress` (rewritten every time the wall starts). |
| `drawing_config.json` | On the tablet's private storage. A copy may also sit next to a Windows exe if the drawing scene was run on Windows. | `hostAddress` (the wall IP), port `7777`, `autoConnect`, retry interval. |

The tablet never reads the `drawing_config.json` that sits next to the Windows exe. The wall never uses the tablet's file.

`127.0.0.1` means "this same machine". It is the default drawing address and will never reach another device. Replace it with the wall PC's real LAN address.

## Recommended setup: a router, no internet needed

Use this. A direct cable is harder and is what failed in testing.

1. Connect the wall PC to the router with ethernet.
2. Join the tablet to **that same router's Wi-Fi**.
3. Set both devices to **automatic IP (DHCP)**. Remove any manual addresses left over from a direct-cable attempt, especially `192.168.4.1` and `192.168.4.2`. Those are a different network from a normal router (`192.168.0.x` or `192.168.1.x`) and will keep the tablet from reaching the PC.
4. Start the wall app. Approve the Windows administrator prompt if it appears. That adds a firewall rule named **Cyber Pledge Wall** for inbound UDP **7777** and **7778**.
5. Read the address on the wall. Put that IP into the tablet Config button and tap Connect.
6. If it still fails, open the router settings and turn **off** AP isolation, client isolation, or guest-network isolation. Those settings block the tablet from seeing the PC even when both have an address.

The wall chooses a real ethernet address when it has one, and skips virtual adapters such as Hyper-V, WSL, and Docker. If both Wi-Fi and ethernet are up on the PC, it prefers the physical ethernet address. That address is still reachable from the router's Wi-Fi when they are the same network.

The tablet also listens for a UDP broadcast (`CPW1|<wall ip>|<port>`) and can connect without typing, once both devices share a subnet and the firewall allows it.

## Direct ethernet cable (only if there is no router)

There is no DHCP on a loose cable, so automatic IP usually does not put them on one subnet. Android often does not get a usable link-local address. Changing only the tablet's IP is not enough.

Use a mask of `255.255.255.0` and leave the gateway blank on both devices:

- Wall PC ethernet: `192.168.4.1`
- Tablet ethernet: `192.168.4.2`
- Tablet Config: `192.168.4.1`

If the wall PC has no normal private address (`10.x`, `192.168.x`, `172.16–31.x`), the wall app asks Windows to set the ethernet adapter to `192.168.4.1`. Approve that prompt, then **restart the wall app**. Set the tablet by hand. Android cannot be given that address by this Unity app.

Do not use `192.168.4.x` once a router is in the path.

## If 192.168.4.1 and 192.168.4.2 still fail

The IP settings can be correct and the tablet still sends the app through Wi-Fi. Android ignores a cable that has no internet unless the app binds to that ethernet adapter. The drawing app does that when the USB ethernet adapter is plugged in. Rebuild and reinstall the tablet app after that change. Turn the tablet's Wi-Fi off for the cable test so nothing else can steal the traffic.

On the wall PC, the adapter that has the cable plugged in must be the one set to `192.168.4.1`, and it must show Connected. The wall screen must say `192.168.4.1`. If it shows a different address, the static IP was applied to the wrong adapter. Approve the Windows firewall prompt. The inbound rule is named Cyber Pledge Wall and must allow UDP 7777 and 7778.

From the wall PC, `ping 192.168.4.2` has to get a reply before the app can connect. No reply means the cable path itself is down.

## Checks when it still fails

1. Wall app is running and shows a private IP, not `127.0.0.1` and not `169.254.x.x`.
2. Tablet Config contains that same wall IP, not the tablet's IP.
3. The first three numbers match on both devices when the mask is `255.255.255.0`. Example: both start with `192.168.1`.
4. Windows firewall has an inbound allow rule named **Cyber Pledge Wall** for UDP 7777 and 7778. A rule for "public" networks matters, because Windows marks a cable with no internet as public and blocks inbound traffic.
5. The tablet is not on mobile data, a phone hotspot, or a guest Wi-Fi while the PC is on the router.
6. Ping the wall IP from another device on that network, or at least confirm both addresses in each device's network settings.

## Code map

- `Assets/Scripts/Networking/NetworkSession.cs` — ports 7777 and 7778, host listen address, client target.
- `Assets/Scripts/Networking/AppConfig.cs` — the two json files and the `drawing_host` preference.
- `Assets/Scripts/Networking/LanAddress.cs` — which PC address is announced.
- `Assets/Scripts/Networking/WallBeacon.cs` — wall broadcast and tablet listener.
- `Assets/Scripts/Networking/WindowsLanSetup.cs` — firewall rule and the direct-cable addresses `192.168.4.1` / `192.168.4.2`.
- `Assets/Scripts/Wall/WallManager.cs` — starts the host and writes the on-screen address.
- `Assets/Scripts/Drawing/DrawingAppController.cs` — connects, retries, and saves the Config IP.
- `Assets/Scripts/UI/HostIpPanel.cs` — the Config button. Its position and color in the scene are left as authored.

## Do not

- Do not point the tablet at `127.0.0.1`.
- Do not put the tablet's own IP in Config.
- Do not expect internet, a SIM, or "data" to make this work.
- Do not leave direct-cable static IPs in place after moving to a router.
- Do not edit `wall_config.json` on the PC and expect the tablet to notice.
