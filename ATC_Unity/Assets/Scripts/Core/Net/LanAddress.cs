using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

/// Works out which of this PC's addresses the other player should actually type in.
///
/// Picking the first adapter that happens to be up is wrong on a real machine: Bluetooth,
/// VirtualBox, Hyper-V, WSL and VPN adapters are all "up" and all carry IPv4 addresses that
/// no other PC can reach. Enumeration order decides which one you get, so the host can
/// confidently display an address that could never work.
///
/// So candidates are filtered (no virtual adapters, no 169.254 auto-assigned addresses) and
/// ranked, with "has a default gateway" weighted highest — that is the strongest signal that
/// an adapter is on the network you are really joined to.
public static class LanAddress
{
    private static readonly string[] VirtualAdapterHints =
    {
        "virtual", "vmware", "hyper-v", "vethernet", "virtualbox", "vbox", "docker", "wsl",
        "bluetooth", "tap-", "tunnel", "loopback", "zerotier", "hamachi", "radmin", "npcap",
        "pseudo", "teredo", "isatap", "vpn", "wireguard", "tailscale",
    };

    /// Every address another PC could plausibly reach, best first.
    public static List<string> Candidates()
    {
        var ranked = new List<KeyValuePair<int, string>>();

        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up) continue;

            var type = adapter.NetworkInterfaceType;
            if (type == NetworkInterfaceType.Loopback || type == NetworkInterfaceType.Tunnel) continue;
            if (LooksVirtual(adapter)) continue;

            var properties = adapter.GetIPProperties();
            bool hasGateway = HasUsableGateway(properties);

            foreach (var unicast in properties.UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                if (IPAddress.IsLoopback(unicast.Address)) continue;

                string address = unicast.Address.ToString();

                // 169.254.x.x means the adapter never got a DHCP lease. It is up, it has an
                // address, and it is unreachable — exactly the trap this class exists to avoid.
                if (address.StartsWith("169.254.")) continue;

                int score = 0;
                if (hasGateway) score += 100;
                if (type == NetworkInterfaceType.Wireless80211) score += 20;
                else if (type == NetworkInterfaceType.Ethernet) score += 10;

                ranked.Add(new KeyValuePair<int, string>(score, address));
            }
        }

        ranked.Sort((a, b) => b.Key.CompareTo(a.Key));

        var addresses = new List<string>();
        foreach (var entry in ranked)
            if (!addresses.Contains(entry.Value)) addresses.Add(entry.Value);
        return addresses;
    }

    /// The single address to lead with, or "unknown" when this PC is on no network at all.
    public static string Best()
    {
        var candidates = Candidates();
        return candidates.Count > 0 ? candidates[0] : "unknown";
    }

    private static bool HasUsableGateway(IPInterfaceProperties properties)
    {
        foreach (var gateway in properties.GatewayAddresses)
        {
            if (gateway.Address == null) continue;
            if (gateway.Address.AddressFamily != AddressFamily.InterNetwork) continue;
            if (gateway.Address.Equals(IPAddress.Any)) continue;
            return true;
        }
        return false;
    }

    private static bool LooksVirtual(NetworkInterface adapter)
    {
        string haystack = (adapter.Name + " " + adapter.Description).ToLowerInvariant();
        foreach (var hint in VirtualAdapterHints)
            if (haystack.Contains(hint)) return true;
        return false;
    }
}
