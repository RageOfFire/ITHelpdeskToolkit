using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using OrbitDesk.Models;

namespace OrbitDesk.Services
{
    /// <summary>
    /// Advanced audio/microphone repairs. The four grid actions run through PowerShell (elevated where
    /// the underlying change needs it); the three "open" actions launch a visible Windows tool directly
    /// (troubleshooter wizards need to show their UI and take input, so they are not run hidden/elevated
    /// like the others - same reasoning as AppRepairService.OpenAppDataFolder).
    /// </summary>
    public static class AudioService
    {
        public static Task<(bool Success, string Output)> RestartAudioServicesAsync()
            => RunScriptAsync(RestartServicesBody, admin: true);

        public static Task<(bool Success, string Output)> ReEnableAudioDevicesAsync()
            => RunScriptAsync(ReEnableDevicesBody, admin: true);

        public static Task<(bool Success, string Output)> FixMicrophonePrivacyAsync()
            => RunScriptAsync(FixMicPrivacyBody, admin: true);

        public static Task<(bool Success, string Output)> ListAudioDeviceStatusAsync()
            => RunScriptAsync(ListDevicesBody, admin: false);

        public static List<RepairActionItem> GetAudioRepairActions() => new()
        {
            new(0, "Restart Audio Services",
                "Restarts the Windows Audio and Windows Audio Endpoint Builder services, in the correct " +
                "order. Fixes most \"no sound\", \"no mic\", or \"device just disappeared\" issues without a reboot.",
                RestartAudioServicesAsync, true),

            new(1, "Re-enable Disabled/Errored Audio Devices",
                "Finds any playback device, recording device, or audio driver that Windows has disabled " +
                "or flagged with an error, and re-enables it.",
                ReEnableAudioDevicesAsync, true),

            new(2, "Fix Microphone Privacy Access",
                "Turns on \"Allow apps to access your microphone\" and \"Allow desktop apps to access your " +
                "microphone\", and clears a policy override if one is forcing it off. The most common cause " +
                "of \"the mic works in one app but not another\".",
                FixMicrophonePrivacyAsync, true),

            new(3, "List Audio Device Status",
                "Lists every playback/recording device and audio driver Windows currently knows about, and " +
                "whether each is OK or in a problem state. Read-only - useful before/after the fixes above.",
                ListAudioDeviceStatusAsync, false)
        };

        // ---------------------------------------------------------------------------------------
        // Visible/interactive launches - no elevation, no hidden window.
        // ---------------------------------------------------------------------------------------

        public static (bool Success, string Output) OpenPlaybackTroubleshooter()
            => LaunchVisible("msdt.exe", "/id AudioPlaybackDiagnostic", "the Playback Audio troubleshooter");

        public static (bool Success, string Output) OpenRecordingTroubleshooter()
            => LaunchVisible("msdt.exe", "/id AudioRecordingDiagnostic", "the Recording Audio troubleshooter");

        public static (bool Success, string Output) OpenSoundSettings()
            => LaunchVisible("ms-settings:sound", "", "Sound settings");

        private static (bool Success, string Output) LaunchVisible(string fileName, string arguments, string label)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = true
                });
                return (true, $"Opened {label}.");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        // ---------------------------------------------------------------------------------------

        private static async Task<(bool Success, string Output)> RunScriptAsync(string scriptBody, bool admin)
        {
            string script = ScriptPrelude + "\r\n" + scriptBody;

            string scriptPath = Path.Combine(
                Path.GetTempPath(),
                $"helpdesk_audio_{Environment.ProcessId}_{DateTime.Now.Ticks}.ps1");

            try
            {
                await File.WriteAllTextAsync(scriptPath, script, new UTF8Encoding(true));

                var (_, output) = await ShellService.RunRepairCommandAsync(
                    $"powershell -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    120,
                    admin: admin);

                bool ok = output.Contains("RESULT: SUCCESS", StringComparison.Ordinal);
                return (ok, output);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
            finally
            {
                try { if (File.Exists(scriptPath)) File.Delete(scriptPath); } catch { }
            }
        }

        // ---------------------------------------------------------------------------------------
        // PowerShell (ASCII-only, same reasoning as OfficeUpdateService: Windows PowerShell 5.1 can
        // misread non-ASCII text in a script without a BOM).
        // ---------------------------------------------------------------------------------------

        private const string ScriptPrelude = "$ErrorActionPreference = 'Continue'";

        private const string RestartServicesBody = """
Write-Output '=== Restarting audio services ==='

Write-Output '  Stopping...'
foreach ($svc in @('AudioSrv', 'AudioEndpointBuilder')) {
    try {
        Stop-Service -Name $svc -Force -ErrorAction Stop
        Write-Output "    Stopped $svc"
    } catch {
        Write-Output "    Could not stop $svc - $($_.Exception.Message)"
    }
}

Start-Sleep -Seconds 2

Write-Output '  Starting...'
$allOk = $true
foreach ($svc in @('AudioEndpointBuilder', 'AudioSrv')) {
    try {
        Start-Service -Name $svc -ErrorAction Stop
        Write-Output "    Started $svc"
    } catch {
        Write-Output "    Could not start $svc - $($_.Exception.Message)"
        $allOk = $false
    }
}

Write-Output ''
if ($allOk) { Write-Output 'RESULT: SUCCESS' } else { Write-Output 'RESULT: PARTIAL' }
""";

        private const string ReEnableDevicesBody = """
Write-Output '=== Re-enabling disabled/errored audio devices ==='

$queryOk = $true
$devices = @()
try {
    # Wrapped in @(...) INSIDE the try block on purpose: a PowerShell pipeline that matches zero
    # objects collapses to $null, not an empty array, which would otherwise be indistinguishable
    # from a failed query below. $queryOk is the only thing that signals failure.
    $devices = @(Get-PnpDevice -Class AudioEndpoint, MEDIA -ErrorAction Stop | Where-Object { $_.Status -ne 'OK' })
} catch {
    Write-Output "  Could not query audio devices: $($_.Exception.Message)"
    $queryOk = $false
}

if (-not $queryOk) {
    Write-Output 'RESULT: FAILED'
} elseif ($devices.Count -eq 0) {
    Write-Output '  No disabled or errored audio devices were found.'
    Write-Output 'RESULT: SUCCESS'
} else {
    $allOk = $true
    foreach ($d in $devices) {
        Write-Output "  Enabling: $($d.FriendlyName) [was: $($d.Status)]"
        try {
            Enable-PnpDevice -InstanceId $d.InstanceId -Confirm:$false -ErrorAction Stop | Out-Null
            Write-Output '    Enabled.'
        } catch {
            Write-Output "    Could not enable it: $($_.Exception.Message)"
            $allOk = $false
        }
    }
    if ($allOk) { Write-Output 'RESULT: SUCCESS' } else { Write-Output 'RESULT: PARTIAL' }
}
""";

        private const string FixMicPrivacyBody = """
Write-Output '=== Fixing microphone privacy access ==='

function Set-AllowValue([string]$path) {
    try {
        if (-not (Test-Path $path)) { New-Item -Path $path -Force | Out-Null }
        Set-ItemProperty -Path $path -Name 'Value' -Value 'Allow' -Type String -Force -ErrorAction Stop
        Write-Output "  Set $path -> Allow"
        return $true
    } catch {
        Write-Output "  Could not set $path - $($_.Exception.Message)"
        return $false
    }
}

$ok = $true
$ok = (Set-AllowValue 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone') -and $ok
$ok = (Set-AllowValue 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone') -and $ok
$ok = (Set-AllowValue 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone\NonPackaged') -and $ok

# Best-effort: if an org policy is forcing microphone access off (value 2 = force-deny in Microsoft's
# AppPrivacy policy set), clear it so the settings above can actually take effect. Left alone otherwise.
try {
    $policyPath = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy'
    if (Test-Path $policyPath) {
        $existing = Get-ItemProperty -Path $policyPath -Name 'LetAppsAccessMicrophone' -ErrorAction SilentlyContinue
        if ($existing -and $existing.LetAppsAccessMicrophone -eq 2) {
            Remove-ItemProperty -Path $policyPath -Name 'LetAppsAccessMicrophone' -ErrorAction Stop
            Write-Output '  Cleared a policy that was forcing microphone access off.'
        }
    }
} catch {
    Write-Output "  Could not check/clear the microphone access policy: $($_.Exception.Message)"
}

Write-Output ''
Write-Output 'Apps that were already open may need to be restarted to notice the change.'
if ($ok) { Write-Output 'RESULT: SUCCESS' } else { Write-Output 'RESULT: PARTIAL' }
""";

        private const string ListDevicesBody = """
Write-Output '=== Audio device status ==='
try {
    $devices = Get-PnpDevice -Class AudioEndpoint, MEDIA -ErrorAction Stop | Sort-Object Class, FriendlyName
    if ($null -eq $devices -or @($devices).Count -eq 0) {
        Write-Output '  No audio devices found.'
    } else {
        foreach ($d in @($devices)) {
            Write-Output "  [$($d.Status)] $($d.FriendlyName) ($($d.Class))"
        }
    }
    Write-Output 'RESULT: SUCCESS'
} catch {
    Write-Output "  Could not list devices: $($_.Exception.Message)"
    Write-Output 'RESULT: FAILED'
}
""";
    }
}
