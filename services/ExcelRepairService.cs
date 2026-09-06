using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using ITHelpdeskToolkit.Models;

namespace ITHelpdeskToolkit.Services
{
    public static class ExcelRepairService
    {
        public static async Task<(bool Success, string Output)> KillExcelAsync()
        {
            return await ShellService.KillProcessAsync("EXCEL.EXE");
        }

        public static (bool Success, string Output) OpenExcelSafeMode()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "excel.exe",
                    Arguments = "/safe",
                    UseShellExecute = true
                });
                return (true, "Excel Safe Mode launch requested.");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private static List<string> GetOfficeVersionKeys()
        {
            var versions = new List<string>();
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Office");
                if (key != null)
                {
                    foreach (string subName in key.GetSubKeyNames())
                    {
                        if (double.TryParse(subName, out _))
                        {
                            versions.Add(subName);
                        }
                    }
                }
            }
            catch { }
            return versions.Count > 0 ? versions : new List<string> { "16.0" };
        }

        public static async Task<(bool Success, string Output)> ClearExcelLockFilesAsync()
        {
            return await Task.Run(() =>
            {
                int removed = 0;
                int failed = 0;
                var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                foreach (string sub in new[] { "Documents", "Desktop", "Downloads" })
                {
                    folders.Add(Path.Combine(home, sub));
                }
                folders.Add(Path.GetTempPath());

                foreach (string folder in folders)
                {
                    if (!Directory.Exists(folder)) continue;
                    try
                    {
                        var dirInfo = new DirectoryInfo(folder);
                        foreach (FileInfo file in dirInfo.GetFiles("~$*.xls*"))
                        {
                            try
                            {
                                file.Delete();
                                removed++;
                            }
                            catch
                            {
                                failed++;
                            }
                        }
                    }
                    catch { }
                }

                return (true, $"Removed {removed} Excel lock file(s). Skipped/locked: {failed}.");
            });
        }

        public static async Task<(bool Success, string Output)> DisableComAddinsAsync()
        {
            return await Task.Run(() =>
            {
                var disabled = new List<string>();
                var roots = new List<string> { @"Software\Microsoft\Office\Excel\Addins" };
                foreach (string version in GetOfficeVersionKeys())
                {
                    roots.Add($@"Software\Microsoft\Office\{version}\Excel\Addins");
                }

                foreach (string root in roots)
                {
                    try
                    {
                        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(root, true);
                        if (key != null)
                        {
                            foreach (string name in key.GetSubKeyNames())
                            {
                                try
                                {
                                    using RegistryKey? subKey = key.OpenSubKey(name, true);
                                    subKey?.SetValue("LoadBehavior", 0, RegistryValueKind.DWord);
                                    disabled.Add(name);
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                }

                if (disabled.Count > 0)
                {
                    return (true, "Disabled add-in(s): " + string.Join(", ", disabled));
                }
                return (true, "No enabled COM add-ins were found — nothing to disable.");
            });
        }

        public static async Task<(bool Success, string Output)> ClearExcelStartupAddinsAsync()
        {
            return await Task.Run(() =>
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string xlStart = Path.Combine(appData, "Microsoft", "Excel", "XLSTART");
                if (!Directory.Exists(xlStart))
                {
                    return (true, "No XLSTART folder found — nothing to disable.");
                }

                string backup = xlStart + "_disabled";
                Directory.CreateDirectory(backup);

                int moved = 0;
                try
                {
                    var dirInfo = new DirectoryInfo(xlStart);
                    foreach (FileInfo file in dirInfo.GetFiles())
                    {
                        try
                        {
                            file.MoveTo(Path.Combine(backup, file.Name));
                            moved++;
                        }
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    return (false, ex.Message);
                }

                if (moved > 0)
                {
                    return (true, $"Moved {moved} startup add-in file(s) to:\n{backup}\n(restore them there if Excel needs them again).");
                }
                return (true, "XLSTART folder was already empty — nothing to disable.");
            });
        }

        public static async Task<(bool Success, string Output)> ResetExcelToolbarAsync()
        {
            return await Task.Run(() =>
            {
                var changed = new List<string>();
                foreach (string version in GetOfficeVersionKeys())
                {
                    string subPath = $@"Software\Microsoft\Office\{version}\Excel\Options";
                    try
                    {
                        Registry.CurrentUser.DeleteSubKeyTree(subPath, false);
                        changed.Add(version);
                    }
                    catch { }
                }

                if (changed.Count > 0)
                {
                    return (true, $"Reset toolbar/ribbon customizations for Office version(s): {string.Join(", ", changed)}.");
                }
                return (true, "No customized toolbar/ribbon settings were found — nothing to reset.");
            });
        }

        public static async Task<(bool Success, string Output)> ClearExcelMruAsync()
        {
            return await Task.Run(() =>
            {
                var cleared = new List<string>();
                foreach (string version in GetOfficeVersionKeys())
                {
                    string subPath = $@"Software\Microsoft\Office\{version}\Excel\File MRU";
                    try
                    {
                        Registry.CurrentUser.DeleteSubKeyTree(subPath, false);
                        cleared.Add(version);
                    }
                    catch { }
                }

                if (cleared.Count > 0)
                {
                    return (true, $"Cleared recent files list for Office version(s): {string.Join(", ", cleared)}.");
                }
                return (true, "No recent files list entries were found — nothing to clear.");
            });
        }

        public static async Task<(bool Success, string Output)> RepairOfficeQuickAsync()
        {
            string commonFiles = Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles);
            string clickToRun = Path.Combine(commonFiles, "Microsoft Shared", "ClickToRun", "OfficeClickToRun.exe");

            if (!File.Exists(clickToRun))
            {
                return (false, "Office Click-to-Run was not found on this PC. Quick Repair only works for Microsoft 365 / Click-to-Run installs.");
            }

            string command = $"\"{clickToRun}\" scenario=Repair platform=x64 culture=en-us RepairType=QuickRepair DisplayLevel=True";
            return await ShellService.RunCommandAsync(command, 300);
        }

        public static async Task<(bool Success, string Summary, string Details)> ConvertXlsToXlsxBatchAsync(string[] filePaths)
        {
            int successCount = 0;
            int failCount = 0;
            var details = new List<string>();

            foreach (string filePath in filePaths)
            {
                var (ok, msg) = await ConvertXlsToXlsxAsync(filePath);
                if (ok) successCount++;
                else failCount++;
                string status = ok ? "SUCCESS" : "FAILED";
                details.Add($"[{status}] {Path.GetFileName(filePath)}\n{msg}");
            }

            string summary = $"Converted {successCount} file(s) successfully, {failCount} failed.";
            return (successCount > 0 && failCount == 0, summary, string.Join("\n\n", details));
        }

        public static async Task<(bool Success, string Output)> ConvertXlsToXlsxAsync(string xlsPath, string? outputPath = null)
        {
            xlsPath = Path.GetFullPath(xlsPath);
            if (!File.Exists(xlsPath)) return (false, $"File does not exist: {xlsPath}");

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = Path.ChangeExtension(xlsPath, ".xlsx");
            }

            // A hidden Excel instance left over from a previous crashed/stuck conversion is the
            // most common cause of RPC_E_SERVERFAULT (0x80010105) — it's sitting behind a modal
            // dialog (compatibility checker, format warning, recovery prompt) that a new COM call
            // can never get past. Clear it out before every attempt.
            await ShellService.KillProcessAsync("EXCEL.EXE");
            await Task.Delay(400);

            string safeSrc = xlsPath.Replace("'", "''");
            string safeTarget = outputPath.Replace("'", "''");

            string psScript = $@"
$sourcePath = '{safeSrc}'
$targetPath = '{safeTarget}'
$excel = $null
try {{
    $excel = New-Object -ComObject Excel.Application
    $excel.Visible = $false
    $excel.DisplayAlerts = $false
    $excel.Interactive = $false
    $excel.AskToUpdateLinks = $false
    $excel.AlertBeforeOverwriting = $false
    try {{ $excel.AutomationSecurity = 3 }} catch {{ }}
    $wb = $excel.Workbooks.Open($sourcePath, 0, $false, 5, '', '', $true, 2, '', $false, $false, 0, $true, $false, $false)
    try {{ $wb.CheckCompatibility = $false }} catch {{ }}
    $wb.SaveAs($targetPath, 51)
    $wb.Close($false)
    Write-Output 'CONVERTED_OK'
}} catch {{
    Write-Error $_.Exception.Message
}} finally {{
    if ($excel -ne $null) {{
        $excel.Quit()
        [System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null
    }}
}}
";
            var (success, output) = await ShellService.RunPowerShellAsync(psScript, 60);

            // RPC_E_SERVERFAULT means Excel's COM server itself crashed/hung mid-call — usually a
            // stray instance that spawned again during this run. One clean retry after another
            // kill resolves it in almost every case; a second failure means it's a real file issue.
            if (!(success && output.Contains("CONVERTED_OK")) && output.Contains("0x80010105"))
            {
                await ShellService.KillProcessAsync("EXCEL.EXE");
                await Task.Delay(800);
                (success, output) = await ShellService.RunPowerShellAsync(psScript, 60);
            }

            if (success && output.Contains("CONVERTED_OK"))
            {
                return (true, $"Successfully converted using Excel engine:\n  From: {xlsPath}\n  To:   {outputPath}");
            }

            if (output.Contains("0x80010105"))
            {
                return (false, "Excel's COM server crashed/hung while converting this file (RPC_E_SERVERFAULT), even after " +
                               "restarting Excel and retrying. This usually means the file itself triggers a dialog Excel " +
                               "can't show while running invisibly — most often:\n" +
                               "  • the file is password-protected\n" +
                               "  • it contains a 'Compatibility Checker' warning (old chart types, features not in .xlsx)\n" +
                               "  • it's genuinely corrupted and needs 'Repair Workbook File' first\n\n" +
                               "Try 'Repair Workbook File' on this file first, or open it manually once in Excel, " +
                               "dismiss any prompts, save it normally, then retry the conversion.\n\n" +
                               $"Raw error:\n{output}");
            }

            return (false, $"Failed to convert file.\nExcel COM error: {output}");
        }

        public static async Task<(bool Success, string Output)> ClearAutoRecoverCacheAsync()
        {
            return await Task.Run(() =>
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string root = Path.Combine(appData, "Microsoft", "Excel");
                if (!Directory.Exists(root))
                {
                    return (true, "No Excel AutoRecover folder found — nothing to clear.");
                }

                int removed = 0;
                int failed = 0;
                try
                {
                    foreach (string file in Directory.GetFiles(root, "*.*", SearchOption.AllDirectories))
                    {
                        string ext = Path.GetExtension(file).ToLowerInvariant();
                        if (ext != ".asd" && ext != ".xlk") continue;
                        try
                        {
                            File.Delete(file);
                            removed++;
                        }
                        catch
                        {
                            failed++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    return (false, ex.Message);
                }

                return (true, $"Removed {removed} AutoRecover cache file(s) (.asd/.xlk). Skipped/locked: {failed}.\n" +
                               "This clears the 'we found a problem with content' recovery-loop prompt on open.");
            });
        }

        public static async Task<(bool Success, string Output)> ResetFileAssociationsAsync()
        {
            string exePath = null!;
            try
            {
                using RegistryKey? appPathKey = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\EXCEL.EXE");
                exePath = appPathKey?.GetValue(null) as string ?? "";
            }
            catch { }

            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            {
                return (false, "Could not locate EXCEL.EXE on this PC — is Excel installed?");
            }

            var extensions = new[] { ".xls", ".xlsx", ".xlsm", ".xlsb", ".csv" };
            var commands = new List<string>();
            foreach (string ext in extensions)
            {
                string progId = $"Excel{ext.Replace(".", "").ToUpperInvariant()}HelpdeskFix";
                commands.Add($"assoc {ext}={progId}");
                commands.Add($"ftype {progId}=\"{exePath}\" \"%1\"");
            }

            string combined = string.Join(" && ", commands);
            var (success, output) = await ShellService.RunRepairCommandAsync(combined, 30, admin: true);
            if (success)
            {
                return (true, $"Re-associated {string.Join(", ", extensions)} with:\n{exePath}");
            }
            return (false, $"Failed to reset file associations: {output}");
        }

        public static async Task<(bool Success, string Output)> ResetPrinterBindingAsync()
        {
            return await Task.Run(() =>
            {
                var cleared = new List<string>();
                foreach (string version in GetOfficeVersionKeys())
                {
                    string subPath = $@"Software\Microsoft\Office\{version}\Excel\Options";
                    try
                    {
                        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(subPath, true);
                        if (key != null)
                        {
                            // Excel stores the last-used printer under a machine-specific "PD..." value
                            // and sometimes under "DefaultPrinter" on older builds.
                            foreach (string valueName in key.GetValueNames())
                            {
                                if (valueName.StartsWith("PD", StringComparison.OrdinalIgnoreCase) ||
                                    valueName.Equals("DefaultPrinter", StringComparison.OrdinalIgnoreCase))
                                {
                                    try
                                    {
                                        key.DeleteValue(valueName, false);
                                        cleared.Add($"{version}\\{valueName}");
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (cleared.Count > 0)
                {
                    return (true, "Cleared stale printer binding value(s):\n" + string.Join("\n", cleared) +
                                  "\nExcel will fall back to the current Windows default printer on next launch.");
                }
                return (true, "No stored printer binding was found — nothing to reset.");
            });
        }

        public static async Task<(bool Success, string Output)> ResetResiliencyKeysAsync()
        {
            return await Task.Run(() =>
            {
                var cleared = new List<string>();
                foreach (string version in GetOfficeVersionKeys())
                {
                    foreach (string sub in new[] { "DisabledItems", "DocumentRecovery" })
                    {
                        string subPath = $@"Software\Microsoft\Office\{version}\Excel\Resiliency\{sub}";
                        try
                        {
                            Registry.CurrentUser.DeleteSubKeyTree(subPath, false);
                            cleared.Add($"{version}\\Resiliency\\{sub}");
                        }
                        catch { }
                    }
                }

                if (cleared.Count > 0)
                {
                    return (true, "Cleared resiliency key(s):\n" + string.Join("\n", cleared) +
                                  "\nFixes phantom crash-recovery prompts and add-ins Excel auto-disabled after a crash.");
                }
                return (true, "No resiliency keys were found — nothing to reset.");
            });
        }

        public static async Task<(bool Success, string Output)> TrustDownloadsFolderAsync()
        {
            return await Task.Run(() =>
            {
                string downloads = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

                var added = new List<string>();
                foreach (string version in GetOfficeVersionKeys())
                {
                    string basePath = $@"Software\Microsoft\Office\{version}\Excel\Security\Trusted Locations";
                    string locKey = $@"{basePath}\LocationHelpdesk1";
                    try
                    {
                        using RegistryKey key = Registry.CurrentUser.CreateSubKey(locKey);
                        key.SetValue("Path", downloads + @"\", RegistryValueKind.String);
                        key.SetValue("AllowSubFolders", 1, RegistryValueKind.DWord);
                        key.SetValue("Description", "Added by IT Helpdesk Toolkit", RegistryValueKind.String);
                        added.Add(version);
                    }
                    catch { }
                }

                if (added.Count > 0)
                {
                    return (true, $"Added Downloads folder as a trusted location for Office version(s): {string.Join(", ", added)}.\n" +
                                  "Files opened from there will skip Protected View read-only mode.");
                }
                return (false, "Could not add a trusted location — no Office version keys were found.");
            });
        }

        public static async Task<(bool Success, string Output)> UnblockDownloadedFilesAsync()
        {
            return await Task.Run(() =>
            {
                string downloads = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

                if (!Directory.Exists(downloads))
                {
                    return (true, "Downloads folder not found — nothing to unblock.");
                }

                int unblocked = 0;
                int failed = 0;
                try
                {
                    var dirInfo = new DirectoryInfo(downloads);
                    foreach (FileInfo file in dirInfo.GetFiles("*.xls*"))
                    {
                        string zoneIdentifier = file.FullName + ":Zone.Identifier";
                        try
                        {
                            if (File.Exists(zoneIdentifier))
                            {
                                File.Delete(zoneIdentifier);
                            }
                            unblocked++;
                        }
                        catch
                        {
                            failed++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    return (false, ex.Message);
                }

                return (true, $"Removed the 'downloaded from the internet' flag (Mark-of-the-Web) from {unblocked} Excel file(s) in Downloads. Skipped/locked: {failed}.\n" +
                               "This clears the 'Protected View: this file came from the internet' banner.");
            });
        }

        public static async Task<(bool Success, string Output)> ResetCalculationModeAsync()
        {
            return await Task.Run(() =>
            {
                var changed = new List<string>();
                foreach (string version in GetOfficeVersionKeys())
                {
                    string subPath = $@"Software\Microsoft\Office\{version}\Excel\Options";
                    try
                    {
                        using RegistryKey key = Registry.CurrentUser.CreateSubKey(subPath);
                        // 0 = Manual, non-zero/absent = Automatic. Remove the override so new
                        // workbooks default back to Automatic calculation.
                        if (key.GetValue("CalculationMode") != null)
                        {
                            key.DeleteValue("CalculationMode", false);
                            changed.Add(version);
                        }
                    }
                    catch { }
                }

                if (changed.Count > 0)
                {
                    return (true, $"Cleared the Manual calculation override for Office version(s): {string.Join(", ", changed)}.\n" +
                                  "New workbooks will default to Automatic. Note: a workbook that was individually saved in Manual mode still needs Formulas > Calculation Options > Automatic set inside that file.");
                }
                return (true, "No Manual calculation override was found in the registry — nothing to reset. " +
                              "If one workbook still seems stuck, check Formulas > Calculation Options in that file.");
            });
        }

        public static async Task<(bool Success, string Output)> ClearRibbonUiCacheAsync()
        {
            return await Task.Run(() =>
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var searchRoots = new[]
                {
                    Path.Combine(localAppData, "Microsoft", "Office"),
                };

                int removed = 0;
                int failed = 0;
                foreach (string root in searchRoots)
                {
                    if (!Directory.Exists(root)) continue;
                    try
                    {
                        foreach (string file in Directory.GetFiles(root, "*.xlb", SearchOption.AllDirectories))
                        {
                            try { File.Delete(file); removed++; } catch { failed++; }
                        }
                        // Legacy per-user command bar / ribbon customization cache.
                        foreach (string file in Directory.GetFiles(root, "Excel.officeUI", SearchOption.AllDirectories))
                        {
                            try { File.Delete(file); removed++; } catch { failed++; }
                        }
                    }
                    catch { }
                }

                if (removed > 0)
                {
                    return (true, $"Removed {removed} ribbon/toolbar cache file(s) (.xlb / Excel.officeUI). Skipped/locked: {failed}.\n" +
                                  "Fixes blank or broken ribbon icons that the registry-based ribbon reset doesn't clear.");
                }
                return (true, "No ribbon/toolbar cache files were found — nothing to clear.");
            });
        }

        public static async Task<(bool Success, string Output)> ListAddinLoadBehaviorAsync()
        {
            return await Task.Run(() =>
            {
                var lines = new List<string>();
                var roots = new List<(string Scope, string Path)>
                {
                    ("Per-user", @"Software\Microsoft\Office\Excel\Addins")
                };
                foreach (string version in GetOfficeVersionKeys())
                {
                    roots.Add(("Per-user", $@"Software\Microsoft\Office\{version}\Excel\Addins"));
                }

                foreach (var (scope, path) in roots)
                {
                    try
                    {
                        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(path);
                        if (key == null) continue;

                        foreach (string name in key.GetSubKeyNames())
                        {
                            try
                            {
                                using RegistryKey? subKey = key.OpenSubKey(name);
                                object? loadBehaviorObj = subKey?.GetValue("LoadBehavior");
                                int loadBehavior = loadBehaviorObj is int i ? i : -1;
                                string desc = loadBehavior switch
                                {
                                    0 => "Disabled",
                                    1 => "Connected (loads on demand)",
                                    2 => "Loads at startup",
                                    3 => "Loads at startup (boot flag set)",
                                    8 => "Loads on first use, then remembers state",
                                    9 => "Disabled by user/crash detection",
                                    16 => "Disabled by Office after repeated crashes",
                                    _ => "Unknown/not set"
                                };
                                lines.Add($"[{scope}] {name} — {desc} (LoadBehavior={loadBehavior})");
                            }
                            catch { }
                        }
                    }
                    catch { }
                }

                if (lines.Count == 0)
                {
                    return (true, "No registered COM add-ins were found for Excel.");
                }

                return (true, "Registered Excel COM add-ins:\n" + string.Join("\n", lines) +
                              "\n\nAn add-in Excel auto-disabled after a crash usually shows LoadBehavior=9 or 16 — " +
                              "use 'Disable COM Add-ins' to fully turn off a suspect one, or re-enable it manually " +
                              "in Excel > File > Options > Add-ins once you've confirmed it isn't the cause.");
            });
        }

        public static async Task<(bool Success, string Output)> RepairCorruptWorkbookAsync(string filePath, string? outputPath = null)
        {
            filePath = Path.GetFullPath(filePath);
            if (!File.Exists(filePath)) return (false, $"File does not exist: {filePath}");

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                string dir = Path.GetDirectoryName(filePath) ?? "";
                string name = Path.GetFileNameWithoutExtension(filePath);
                string ext = Path.GetExtension(filePath);
                outputPath = Path.Combine(dir, $"{name}_repaired{ext}");
            }

            string safeSrc = filePath.Replace("'", "''");
            string safeTarget = outputPath.Replace("'", "''");

            // CorruptLoad: 0 = Normal, 1 = RepairFile, 2 = ExtractData (values only, last resort).
            string psScript = $@"
$sourcePath = '{safeSrc}'
$targetPath = '{safeTarget}'
$excel = $null
try {{
    $excel = New-Object -ComObject Excel.Application
    $excel.Visible = $false
    $excel.DisplayAlerts = $false
    $wb = $excel.Workbooks.Open($sourcePath, 0, $false, 5, '', '', $true, 2, '', $true, $false, 0, $true, $false, 1)
    $wb.SaveAs($targetPath)
    $wb.Close($false)
    Write-Output 'REPAIRED_OK'
}} catch {{
    Write-Error $_.Exception.Message
}} finally {{
    if ($excel -ne $null) {{
        $excel.Quit()
        [System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null
    }}
}}
";
            var (success, output) = await ShellService.RunPowerShellAsync(psScript, 90);
            if (success && output.Contains("REPAIRED_OK"))
            {
                return (true, $"Opened with Excel's built-in repair mode and re-saved:\n  From: {filePath}\n  To:   {outputPath}\n" +
                              "If the original is severely corrupted, formatting/macros may be lost — this recovers formulas/data first.");
            }

            return (false, $"Failed to repair workbook.\nExcel COM error: {output}");
        }

        public static async Task<(bool Success, string Output)> ScanBrokenLinksAsync(string filePath)
        {
            filePath = Path.GetFullPath(filePath);
            if (!File.Exists(filePath)) return (false, $"File does not exist: {filePath}");

            string safeSrc = filePath.Replace("'", "''");

            string psScript = $@"
$sourcePath = '{safeSrc}'
$excel = $null
try {{
    $excel = New-Object -ComObject Excel.Application
    $excel.Visible = $false
    $excel.DisplayAlerts = $false
    $excel.AskToUpdateLinks = $false
    $wb = $excel.Workbooks.Open($sourcePath, 0, $true)
    $links = $wb.LinkSources(1)
    if ($links -eq $null) {{
        Write-Output 'NO_LINKS_FOUND'
    }} else {{
        foreach ($link in $links) {{
            $status = $wb.LinkInfo($link, 2)
            Write-Output ""LINK|$link|status=$status""
        }}
    }}
    $wb.Close($false)
}} catch {{
    Write-Error $_.Exception.Message
}} finally {{
    if ($excel -ne $null) {{
        $excel.Quit()
        [System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null
    }}
}}
";
            var (success, output) = await ShellService.RunPowerShellAsync(psScript, 60);
            if (!success)
            {
                return (false, $"Failed to scan for broken links.\nExcel COM error: {output}");
            }

            if (output.Contains("NO_LINKS_FOUND"))
            {
                return (true, $"No external workbook links found in {Path.GetFileName(filePath)} — nothing to fix.");
            }

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Where(l => l.StartsWith("LINK|"));
            var broken = new List<string>();
            var ok = new List<string>();
            foreach (string line in lines)
            {
                string[] parts = line.Split('|');
                string target = parts.Length > 1 ? parts[1] : line;
                // LinkInfo status: 1 = OK, other values indicate missing/unopenable/needs update.
                bool isOk = line.Contains("status=1");
                (isOk ? ok : broken).Add(target);
            }

            StringBuilder sb = new();
            sb.AppendLine($"Scanned {Path.GetFileName(filePath)}: {ok.Count + broken.Count} external link(s) found.");
            if (broken.Count > 0)
            {
                sb.AppendLine($"\n{broken.Count} broken/unreachable link(s):");
                foreach (string b in broken) sb.AppendLine($"  ✗ {b}");
                sb.AppendLine("\nOpen Data > Edit Links in Excel to relink or break these references.");
            }
            if (ok.Count > 0)
            {
                sb.AppendLine($"\n{ok.Count} healthy link(s):");
                foreach (string o in ok) sb.AppendLine($"  ✓ {o}");
            }

            return (broken.Count == 0, sb.ToString());
        }

        public static List<RepairActionItem> GetExcelFixes()
        {
            return new List<RepairActionItem>
            {
                new(0, "Close Stuck Excel Process", "Force-close any unresponsive EXCEL.EXE process.", KillExcelAsync, false),
                new(1, "Clear Lock Files", "Remove leftover ~$ lock files from Documents, Desktop, Downloads and TEMP that block reopening after a crash.", ClearExcelLockFilesAsync, false),
                new(2, "Disable COM Add-ins", "Disable third-party COM add-ins — the most common cause of Excel crashing on startup.", DisableComAddinsAsync, false),
                new(3, "Disable Startup (XLSTART) Add-ins", "Move files out of the XLSTART auto-load folder so they stop loading automatically.", ClearExcelStartupAddinsAsync, false),
                new(4, "Reset Toolbar/Ribbon", "Reset a corrupted or broken Quick Access Toolbar / ribbon layout back to defaults.", ResetExcelToolbarAsync, false),
                new(5, "Clear Recent Files List", "Clear Excel's recently-used file list.", ClearExcelMruAsync, false),
                new(6, "Quick Repair Office", "Run Microsoft's built-in Quick Repair to fix corrupted Office/Excel program files.", RepairOfficeQuickAsync, true),
                new(7, "Clear AutoRecover Cache", "Delete stuck .asd/.xlk AutoRecover temp files that cause a recovery-prompt loop on open.", ClearAutoRecoverCacheAsync, false),
                new(8, "Reset File Associations", "Re-associate .xls/.xlsx/.xlsm/.xlsb/.csv with Excel if double-clicking opens the wrong app or nothing.", ResetFileAssociationsAsync, true),
                new(9, "Reset Printer Binding", "Clear Excel's stored last-used printer so a missing/offline printer driver can't hang startup.", ResetPrinterBindingAsync, false),
                new(10, "Reset Resiliency Keys", "Clear crash-recovery and auto-disabled-item keys that cause phantom recovery prompts or add-ins stuck disabled after a crash.", ResetResiliencyKeysAsync, false),
                new(11, "Trust Downloads Folder", "Add the Downloads folder as a trusted location so files from there skip Protected View read-only mode.", TrustDownloadsFolderAsync, false),
                new(12, "Unblock Downloaded Files", "Remove the 'downloaded from the internet' flag from Excel files in Downloads to clear the Protected View internet-file banner.", UnblockDownloadedFilesAsync, false),
                new(13, "Reset Calculation Mode", "Clear a Manual calculation override so new workbooks default back to Automatic calculation.", ResetCalculationModeAsync, false),
                new(14, "Clear Ribbon/UI Cache", "Delete .xlb / Excel.officeUI cache files that cause blank or broken ribbon icons (separate from the registry ribbon reset).", ClearRibbonUiCacheAsync, false),
                new(15, "List Add-in Load Behavior", "Diagnostic: list every registered COM add-in and its LoadBehavior state, to spot which one Excel auto-disabled after a crash.", ListAddinLoadBehaviorAsync, false),
            };
        }
    }
}
