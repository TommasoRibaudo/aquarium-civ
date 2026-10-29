<#
.SYNOPSIS
  Finds modal dialogs (and native popup menus) owned by the Unity Editor: the usual cause of a "stuck" Unity call.

.DESCRIPTION
  Default: report only. Prints every Unity dialog with its title and buttons.
  -Click: also answers the dialogs on the safe list (answers that never discard work):
    "Operation running ... quit?"         -> No   (Yes would quit the Editor)
    "Scene(s) Have Been Modified"          -> Cancel (aborts the play/test start instead of saving or discarding)
    native popup menu (#32768)             -> Esc
  Anything else (Recover Scene Backups, Reload/Ignore, Safe Mode, license, unknown) is printed as UNKNOWN for a person
  or the agent to decide by hand.
  -Loop -Seconds N: repeat every 2 s for N seconds (use in the background while a long test run goes).

.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File Tools/unity-dialogs/dismiss-dialogs.ps1
  powershell -NoProfile -ExecutionPolicy Bypass -File Tools/unity-dialogs/dismiss-dialogs.ps1 -Click -Loop -Seconds 600
#>
param(
    [switch]$Click,
    [switch]$Loop,
    [int]$Seconds = 600,
    [string]$ProcessName = 'Unity'   # override only to test the script against a dummy dialog
)

Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class UnityDialogs
{
    public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent, EnumProc cb, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder sb, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr w, IntPtr l);

    public static string ClassOf(IntPtr h) { var sb = new StringBuilder(256); GetClassName(h, sb, 256); return sb.ToString(); }
    public static string TextOf(IntPtr h) { var sb = new StringBuilder(512); GetWindowText(h, sb, 512); return sb.ToString(); }

    public static List<IntPtr> TopWindows(HashSet<uint> pids)
    {
        var list = new List<IntPtr>();
        EnumWindows((h, l) => {
            uint pid; GetWindowThreadProcessId(h, out pid);
            if (pids.Contains(pid) && IsWindowVisible(h)) list.Add(h);
            return true;
        }, IntPtr.Zero);
        return list;
    }

    public static List<IntPtr> Buttons(IntPtr parent)
    {
        var list = new List<IntPtr>();
        EnumChildWindows(parent, (h, l) => { if (ClassOf(h) == "Button") list.Add(h); return true; }, IntPtr.Zero);
        return list;
    }

    /// <summary>All static text inside a dialog (the message body), joined.</summary>
    public static string Body(IntPtr parent)
    {
        var sb = new StringBuilder();
        EnumChildWindows(parent, (h, l) => { if (ClassOf(h) == "Static") sb.Append(TextOf(h)).Append(' '); return true; }, IntPtr.Zero);
        return sb.ToString().Trim();
    }
}
"@

$BM_CLICK = 0x00F5
$WM_KEYDOWN = 0x0100
$VK_ESCAPE = 0x1B

# Safe answers: [regex on title + body] -> button label to press. Never add an answer that discards work.
$safe = @(
    @{ Match = 'quit';                     Button = 'No' },
    @{ Match = 'Scene\(s\) Have Been Modified|have been modified\b(?!.*externally)'; Button = 'Cancel' }
)

function Invoke-Pass {
    $pids = New-Object 'System.Collections.Generic.HashSet[uint32]'
    Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | ForEach-Object { [void]$pids.Add([uint32]$_.Id) }
    if ($pids.Count -eq 0) { Write-Output "NO_UNITY: no Unity.exe process"; $script:found = -1; return }

    $script:found = 0
    foreach ($h in [UnityDialogs]::TopWindows($pids)) {
        $cls = [UnityDialogs]::ClassOf($h)
        if ($cls -eq '#32768') {
            $script:found++
            if ($Click) { [void][UnityDialogs]::PostMessage($h, $WM_KEYDOWN, [IntPtr]$VK_ESCAPE, [IntPtr]::Zero); Write-Output "CLOSED popup menu (Esc)" }
            else { Write-Output "POPUP_MENU open (blocks the main thread)" }
            continue
        }
        if ($cls -ne '#32770') { continue }

        $script:found++
        $title = [UnityDialogs]::TextOf($h)
        $body = [UnityDialogs]::Body($h)
        $buttons = [UnityDialogs]::Buttons($h)
        $labels = ($buttons | ForEach-Object { ([UnityDialogs]::TextOf($_)) -replace '&', '' }) -join ' | '

        $rule = $safe | Where-Object { "$title $body" -match $_.Match } | Select-Object -First 1
        if ($Click -and $rule) {
            $btn = $buttons | Where-Object { (([UnityDialogs]::TextOf($_)) -replace '&', '') -eq $rule.Button } | Select-Object -First 1
            if ($btn) {
                [void][UnityDialogs]::SendMessage($btn, $BM_CLICK, [IntPtr]::Zero, [IntPtr]::Zero)
                Write-Output "CLICKED '$($rule.Button)' on '$title'"
                continue
            }
        }
        $tag = if ($rule) { 'KNOWN' } else { 'UNKNOWN' }
        Write-Output "$tag Unity dialog '$title': $body  [buttons: $labels]"
    }
}

if ($Loop) {
    $end = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $end) { Invoke-Pass; Start-Sleep -Seconds 2 }
} else {
    Invoke-Pass
    if ($script:found -eq 0) { Write-Output "NO_DIALOGS" }
}
