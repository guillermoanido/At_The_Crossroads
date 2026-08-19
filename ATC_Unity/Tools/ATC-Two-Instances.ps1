# Launches TWO copies of the game side by side on this PC, for testing the LAN match
# without a second machine.
#
#   Right-click -> "Run with PowerShell"   (no admin needed)
#   or:  powershell -ExecutionPolicy Bypass -File ATC-Two-Instances.ps1
#
# Put this file next to ATC_Unity.exe, or pass -ExePath "D:\ATC_Build\ATC_Unity.exe".
#
# Then: left window -> Host.   Right window -> Join -> localhost.

param(
    [string]$ExePath = "",
    [int]$Width = 0,
    [int]$Height = 0
)

Add-Type -AssemblyName System.Windows.Forms

Add-Type @'
using System;
using System.Runtime.InteropServices;
public class ATCWin {
    [DllImport("user32.dll")]
    public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
}
'@

# --- locate the build ---------------------------------------------------------
if ([string]::IsNullOrWhiteSpace($ExePath)) {
    $here = Split-Path -Parent $MyInvocation.MyCommand.Path
    $candidates = @()
    $candidates += Get-ChildItem -Path $here -Filter "*.exe" -ErrorAction SilentlyContinue
    $candidates += Get-ChildItem -Path $here -Filter "*.exe" -Recurse -Depth 2 -ErrorAction SilentlyContinue

    # Unity ships UnityCrashHandler64.exe next to the player - never launch that one.
    $game = $candidates | Where-Object { $_.Name -notlike "UnityCrashHandler*" } | Select-Object -First 1
    if ($game) { $ExePath = $game.FullName }
}

if ([string]::IsNullOrWhiteSpace($ExePath) -or -not (Test-Path $ExePath)) {
    Write-Host "Could not find the game .exe." -ForegroundColor Red
    Write-Host "Put this script next to ATC_Unity.exe, or run it as:" -ForegroundColor Yellow
    Write-Host '    .\ATC-Two-Instances.ps1 -ExePath "D:\ATC_Build\ATC_Unity.exe"' -ForegroundColor Yellow
    Read-Host "Press Enter to close"
    exit 1
}

Write-Host ""
Write-Host "Launching two instances of:" -ForegroundColor Cyan
Write-Host "  $ExePath"
Write-Host ""

# --- work out a side-by-side layout ------------------------------------------
$area = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
if ($Width -le 0) { $Width = [int]($area.Width / 2) }

# Half a screen is a tall, narrow slot; the game is 16:9, so size to that and centre the pair
# vertically rather than stretching each window to full height.
if ($Height -le 0) {
    $Height = [int]($Width * 9 / 16)
    if ($Height -gt $area.Height) {
        $Height = $area.Height
        $Width  = [int]($Height * 16 / 9)
    }
}

$top = $area.Y + [int](($area.Height - $Height) / 2)
if ($top -lt $area.Y) { $top = $area.Y }

Write-Host ("Screen {0}x{1}  ->  each window {2}x{3}" -f $area.Width, $area.Height, $Width, $Height) -ForegroundColor DarkGray
Write-Host ""

function Start-Instance {
    param([string]$Path, [int]$X, [int]$Y, [int]$W, [int]$H, [string]$Label)

    # -screen-fullscreen 0 forces windowed even when the build itself defaults to fullscreen,
    # so this works before the Player settings below are changed.
    $playerArgs = @("-screen-fullscreen", "0", "-screen-width", "$W", "-screen-height", "$H")

    $proc = Start-Process -FilePath $Path -ArgumentList $playerArgs -PassThru

    # The window does not exist the instant the process does - wait for its handle.
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 250
        $proc.Refresh()
        if ($proc.HasExited) {
            Write-Host "  [$Label] exited before showing a window." -ForegroundColor Red
            return $null
        }
        if ($proc.MainWindowHandle -ne [IntPtr]::Zero) { break }
    }

    if ($proc.MainWindowHandle -eq [IntPtr]::Zero) {
        Write-Host "  [$Label] started but no window appeared - move it yourself." -ForegroundColor Yellow
        return $proc
    }

    # Unity finishes sizing its window a moment after it first appears; nudging it after a
    # short settle avoids the player overwriting our position.
    Start-Sleep -Milliseconds 800
    [void][ATCWin]::MoveWindow($proc.MainWindowHandle, $X, $Y, $W, $H, $true)
    Write-Host ("  [{0}] pid {1} placed at {2},{3}" -f $Label, $proc.Id, $X, $Y) -ForegroundColor Green
    return $proc
}

$left  = Start-Instance -Path $ExePath -X $area.X -Y $top -W $Width -H $Height -Label "LEFT / Host"
$right = Start-Instance -Path $ExePath -X ($area.X + $Width) -Y $top -W $Width -H $Height -Label "RIGHT / Join"

if ($right -ne $null -and $right.MainWindowHandle -ne [IntPtr]::Zero) {
    [void][ATCWin]::SetForegroundWindow($right.MainWindowHandle)
}

Write-Host ""
Write-Host "Both instances are up." -ForegroundColor Cyan
Write-Host "  LEFT  window : pick a deck -> Host"
Write-Host "  RIGHT window : pick a deck -> Join -> localhost"
Write-Host ""
Write-Host "localhost never touches the network, so this works with Wi-Fi off." -ForegroundColor DarkGray
Write-Host ""
Read-Host "Press Enter to close this launcher (the game windows stay open)"
