# Run this ONCE on the PC that will HOST the match, as Administrator.
#   Right-click the file -> "Run with PowerShell"  (or)
#   powershell -ExecutionPolicy Bypass -File ATC-Host-Setup.ps1
#
# It opens the port ATC listens on, enables ping so the other PC can prove it can
# reach this one, and prints the address the other player must type in.

$Port = 7777

function Require-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        Write-Host "This script must run as Administrator." -ForegroundColor Red
        Write-Host "Right-click it and choose 'Run with PowerShell' from an admin account." -ForegroundColor Red
        Read-Host "Press Enter to close"
        exit 1
    }
}

Require-Admin

Write-Host ""
Write-Host "=== ATC host setup ===" -ForegroundColor Cyan
Write-Host ""

# --- 1. Open the game port on every profile -----------------------------------
# School Wi-Fi is almost always classified "Public", which is the most restrictive
# profile, so the rule is registered for all of them rather than the current one.
foreach ($proto in @("UDP", "TCP")) {
    $name = "ATC LAN Match ($proto $Port)"
    Get-NetFirewallRule -DisplayName $name -ErrorAction SilentlyContinue | Remove-NetFirewallRule -ErrorAction SilentlyContinue
    New-NetFirewallRule -DisplayName $name `
                        -Direction Inbound `
                        -Protocol $proto `
                        -LocalPort $Port `
                        -Action Allow `
                        -Profile Any | Out-Null
    Write-Host ("  [ok] inbound {0} {1} allowed" -f $proto, $Port) -ForegroundColor Green
}

# --- 2. Allow ping, so the other PC can test reachability ---------------------
$pingRule = "ATC Allow Ping (ICMPv4)"
Get-NetFirewallRule -DisplayName $pingRule -ErrorAction SilentlyContinue | Remove-NetFirewallRule -ErrorAction SilentlyContinue
New-NetFirewallRule -DisplayName $pingRule `
                    -Direction Inbound `
                    -Protocol ICMPv4 `
                    -IcmpType 8 `
                    -Action Allow `
                    -Profile Any | Out-Null
Write-Host "  [ok] inbound ping allowed" -ForegroundColor Green

# --- 3. Report this PC's usable addresses ------------------------------------
Write-Host ""
Write-Host "This PC's address(es) - the other player types one of these into Join:" -ForegroundColor Cyan

$addresses = Get-NetIPAddress -AddressFamily IPv4 |
    Where-Object {
        $_.IPAddress -ne '127.0.0.1' -and
        $_.IPAddress -notlike '169.254.*' -and
        $_.InterfaceAlias -notmatch 'Bluetooth|Virtual|VMware|Hyper-V|Loopback|WSL|Docker'
    }

if (-not $addresses) {
    Write-Host "  (none found - this PC is not joined to any network)" -ForegroundColor Red
} else {
    foreach ($a in $addresses) {
        $profileName = (Get-NetConnectionProfile -InterfaceIndex $a.InterfaceIndex -ErrorAction SilentlyContinue).NetworkCategory
        Write-Host ("    {0}   (adapter: {1}, profile: {2})" -f $a.IPAddress, $a.InterfaceAlias, $profileName) -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host ("Port: {0}" -f $Port) -ForegroundColor Cyan
Write-Host ""
Write-Host "Now, from the OTHER PC, run:   ping <the address above>" -ForegroundColor Cyan
Write-Host "  replies      -> the network is fine, ATC will connect."
Write-Host "  timed out    -> the Wi-Fi is isolating clients. Use a phone hotspot instead."
Write-Host ""
Read-Host "Press Enter to close"
