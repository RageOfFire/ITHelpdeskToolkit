using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ITHelpdeskToolkit.Services
{
    /// <summary>
    /// Removes a specific faulty update (by KB number) and blocks it from being offered again.
    ///
    /// Uninstall: looks for the KB as a Windows Installer (MSI) Office patch and as a regular
    ///            Windows update (wusa), and removes whichever it finds.
    /// Block:     "hides" the update through the Windows Update Agent API - the same thing the
    ///            old wushowhide.diagcab tool does - so Windows Update / Microsoft Update stops
    ///            offering it. Unblock reverses that.
    ///
    /// Everything runs as ONE elevated PowerShell script, so the user only sees a single UAC prompt.
    /// </summary>
    public static class OfficeUpdateService
    {
        /// <summary>Pre-filled in the dialog: the update reported to break Excel copy/paste.</summary>
        public const string DefaultKb = "KB5002914";

        private static readonly Regex KbPattern =
            new(@"^\s*(?:KB)?(\d{6,8})\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Returns "KB1234567" for input like "kb1234567" / "1234567", or null when the input isn't a
        /// plain KB number. Strict on purpose: the value is placed into a PowerShell script.
        /// </summary>
        public static string? NormalizeKb(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            Match m = KbPattern.Match(input);
            return m.Success ? "KB" + m.Groups[1].Value : null;
        }

        public static Task<(bool Success, string Output)> UninstallAndBlockAsync(string kb)
            => RunAsync(kb, UninstallAndBlockBody);

        public static Task<(bool Success, string Output)> UnblockAsync(string kb)
            => RunAsync(kb, UnblockBody);

        private static async Task<(bool Success, string Output)> RunAsync(string kbInput, string scriptBody)
        {
            string? kb = NormalizeKb(kbInput);
            if (kb == null)
                return (false, "Invalid KB number. Use the form KB5002914 (6 to 8 digits).");

            string script = ScriptPrelude
                .Replace("__KB__", kb)
                .Replace("__NUM__", kb.Substring(2)) + "\r\n" + scriptBody;

            string scriptPath = Path.Combine(
                Path.GetTempPath(),
                $"helpdesk_kbfix_{Environment.ProcessId}_{DateTime.Now.Ticks}.ps1");

            try
            {
                await File.WriteAllTextAsync(scriptPath, script, new UTF8Encoding(true));

                // Elevates with a single UAC prompt when needed. Generous timeout: the Windows Update
                // search and an Office patch uninstall can each take a while.
                var (_, output) = await ShellService.RunRepairCommandAsync(
                    $"powershell -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    900,
                    admin: true);

                // The elevated path reports "success" whenever it captured any output, so the script
                // prints an explicit RESULT line and that is what decides success here.
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
        // PowerShell (kept ASCII-only on purpose: Windows PowerShell 5.1 misreads non-ASCII in
        // scripts that lack a BOM, and a stray smart quote would silently break a string).
        // ---------------------------------------------------------------------------------------

        private const string ScriptPrelude = """
$ErrorActionPreference = 'Continue'
$kb  = '__KB__'
$num = '__NUM__'

$script:matchCount = 0
$script:session    = $null
$script:targets    = @()

# Sets up the Windows Update Agent and works out which update sources to search.
function Initialize-Wua {
    $script:session = New-Object -ComObject Microsoft.Update.Session
    $script:targets = @( @{ Name = 'Windows Update (default source)'; Sel = 0; Id = '' } )
    $muId = '7971f918-a847-4430-9279-4a52d1efe18d'
    try {
        $svcMgr = New-Object -ComObject Microsoft.Update.ServiceManager
        $hasMu = $false
        foreach ($svc in $svcMgr.Services) { if ($svc.ServiceID -eq $muId) { $hasMu = $true } }
        if ($hasMu) {
            $script:targets += @{ Name = 'Microsoft Update (Office and other products)'; Sel = 3; Id = $muId }
        } else {
            Write-Output '  Note: the Microsoft Update service (used for Office updates) is not enabled on this PC.'
        }
    } catch {
        Write-Output "  Could not query update services: $($_.Exception.Message)"
    }
}

# Searches every source for updates matching the KB. When $setHidden is $true/$false the matches are
# hidden/unhidden; when it is $null they are only counted. Result is left in $script:matchCount.
function Invoke-Match([string]$criteria, [object]$setHidden) {
    $script:matchCount = 0
    foreach ($t in $script:targets) {
        Write-Output "  Searching $($t.Name)..."
        try {
            $searcher = $script:session.CreateUpdateSearcher()
            $searcher.ServerSelection = $t.Sel
            if ($t.Id) { $searcher.ServiceID = $t.Id }
            $result = $searcher.Search($criteria)
            foreach ($u in $result.Updates) {
                $isMatch = ($u.Title -match $kb)
                if (-not $isMatch) {
                    foreach ($id in $u.KBArticleIDs) { if ("$id" -eq $num) { $isMatch = $true } }
                }
                if (-not $isMatch) { continue }
                $script:matchCount++
                if ($null -ne $setHidden) {
                    $u.IsHidden = [bool]$setHidden
                    if ($setHidden) { Write-Output "    Hidden: $($u.Title)" } else { Write-Output "    Unhidden: $($u.Title)" }
                } else {
                    Write-Output "    Found: $($u.Title)"
                }
            }
        } catch {
            Write-Output "    Search on '$($t.Name)' failed: $($_.Exception.Message)"
        }
    }
}
""";

        private const string UninstallAndBlockBody = """
Write-Output "=== Step 1 of 2: uninstall $kb ==="
$uninstallOk  = $true
$rebootNeeded = $false
$foundAny     = $false

if (Test-Path 'HKLM:\SOFTWARE\Microsoft\Office\ClickToRun\Configuration') {
    Write-Output '  Note: Click-to-Run Office (Microsoft 365) detected. Click-to-Run updates are not tracked by KB number, so an update delivered that way will not be listed below.'
}

# (a) Office updates installed through Windows Installer (MSI). They show up under Uninstall with the KB in the name.
$patches = @()
foreach ($root in @('HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall', 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall')) {
    if (-not (Test-Path $root)) { continue }
    foreach ($key in Get-ChildItem $root -ErrorAction SilentlyContinue) {
        $props = Get-ItemProperty $key.PSPath -ErrorAction SilentlyContinue
        if ($props -and $props.DisplayName -and ($props.DisplayName -match $kb)) {
            $patches += [pscustomobject]@{ Key = $key.PSChildName; Name = $props.DisplayName }
        }
    }
}

foreach ($pt in $patches) {
    $foundAny = $true
    Write-Output "  Found installed update: $($pt.Name)"
    if ($pt.Key -match '^(\{[0-9A-Fa-f-]{36}\})_.+_(\{[0-9A-Fa-f-]{36}\})$') {
        $productCode = $Matches[1]
        $patchCode   = $Matches[2]
        Write-Output '  Removing it with Windows Installer (make sure Excel and other Office apps are closed)...'
        $proc = Start-Process -FilePath 'msiexec.exe' -ArgumentList @('/package', $productCode, '/uninstall', $patchCode, '/qn', '/norestart') -Wait -PassThru
        $code = $proc.ExitCode
        if ($code -eq 0) {
            Write-Output '    Uninstalled.'
        } elseif ($code -eq 3010 -or $code -eq 1641) {
            Write-Output '    Uninstalled (a restart is required to finish).'
            $rebootNeeded = $true
        } else {
            Write-Output "    msiexec failed with exit code $code. Close all Office apps and try again."
            $uninstallOk = $false
        }
    } else {
        Write-Output '    Could not work out the Windows Installer product/patch codes for this entry, so it was not removed automatically.'
        Write-Output '    Remove it by hand: Control Panel > Programs and Features > View installed updates.'
        $uninstallOk = $false
    }
}

# (b) A regular Windows update with this KB.
$hotfix = $null
try { $hotfix = Get-HotFix -Id $kb -ErrorAction Stop } catch { }
if ($hotfix) {
    $foundAny = $true
    Write-Output "  Found installed Windows update $kb. Removing it with wusa..."
    $proc = Start-Process -FilePath 'wusa.exe' -ArgumentList @('/uninstall', "/kb:$num", '/quiet', '/norestart') -Wait -PassThru
    $code = $proc.ExitCode
    if ($code -eq 0) {
        Write-Output '    Uninstalled.'
    } elseif ($code -eq 3010) {
        Write-Output '    Uninstalled (a restart is required to finish).'
        $rebootNeeded = $true
    } else {
        Write-Output "    wusa failed with exit code $code."
        $uninstallOk = $false
    }
}

if (-not $foundAny) {
    Write-Output "  $kb is not installed on this PC - nothing to uninstall."
}

Write-Output ''
Write-Output "=== Step 2 of 2: block $kb so it is not offered again ==="
$blockOk = $false
try {
    Initialize-Wua
    Invoke-Match 'IsInstalled=0 and IsHidden=0' $true
    if ($script:matchCount -gt 0) {
        $blockOk = $true
        Write-Output "  $kb is now hidden and will not be downloaded or installed by Windows/Microsoft Update on this PC."
    } else {
        Invoke-Match 'IsHidden=1' $null
        if ($script:matchCount -gt 0) {
            $blockOk = $true
            Write-Output "  $kb was already hidden - it will not be offered."
        } else {
            Write-Output "  $kb is not currently offered by Windows/Microsoft Update on this PC, so there was nothing to hide yet."
            Write-Output '  If you just uninstalled it and a restart is pending, restart and run this tool again: it will skip the uninstall and apply the block.'
            Write-Output '  If this PC gets updates from WSUS, Intune or Configuration Manager, block the update there instead.'
        }
    }
} catch {
    Write-Output "  Blocking failed: $($_.Exception.Message)"
}

Write-Output ''
if ($rebootNeeded) { Write-Output 'A restart is required to finish removing the update.' }
if ($uninstallOk -and $blockOk) { Write-Output 'RESULT: SUCCESS' }
elseif ($uninstallOk -or $blockOk) { Write-Output 'RESULT: PARTIAL' }
else { Write-Output 'RESULT: FAILED' }
""";

        private const string UnblockBody = """
Write-Output "=== Unblock $kb ==="
$ok = $false
try {
    Initialize-Wua
    Invoke-Match 'IsHidden=1' $false
    if ($script:matchCount -gt 0) {
        Write-Output "  $kb will be offered by Windows/Microsoft Update again."
    } else {
        Write-Output "  $kb is not currently hidden - nothing to unblock."
    }
    $ok = $true
} catch {
    Write-Output "  Unblock failed: $($_.Exception.Message)"
}

Write-Output ''
if ($ok) { Write-Output 'RESULT: SUCCESS' } else { Write-Output 'RESULT: FAILED' }
""";
    }
}
