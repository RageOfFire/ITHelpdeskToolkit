using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
    $wb = $excel.Workbooks.Open($sourcePath)
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
            if (success && output.Contains("CONVERTED_OK"))
            {
                return (true, $"Successfully converted using Excel engine:\n  From: {xlsPath}\n  To:   {outputPath}");
            }

            return (false, $"Failed to convert file.\nExcel COM error: {output}");
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
            };
        }
    }
}
