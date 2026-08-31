using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ITHelpdeskToolkit.Models;

namespace ITHelpdeskToolkit.Services
{
    public static class AppRepairService
    {
        public static async Task<(bool Success, string Output)> ClearIconCacheAsync()
        {
            await ShellService.RunCommandAsync("taskkill /f /im explorer.exe", 30);

            return await Task.Run(async () =>
            {
                string localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? "";
                int removed = 0;
                int failed = 0;
                var candidates = new List<string>();

                if (!string.IsNullOrWhiteSpace(localAppData))
                {
                    candidates.Add(Path.Combine(localAppData, "IconCache.db"));
                    string explorerCacheDir = Path.Combine(localAppData, "Microsoft", "Windows", "Explorer");
                    if (Directory.Exists(explorerCacheDir))
                    {
                        try
                        {
                            foreach (string file in Directory.GetFiles(explorerCacheDir))
                            {
                                string lower = Path.GetFileName(file).ToLower();
                                if (lower.StartsWith("iconcache") || lower.StartsWith("thumbcache"))
                                {
                                    candidates.Add(file);
                                }
                            }
                        }
                        catch { }
                    }
                }

                foreach (string path in candidates)
                {
                    if (File.Exists(path))
                    {
                        try
                        {
                            File.Delete(path);
                            removed++;
                        }
                        catch
                        {
                            failed++;
                        }
                    }
                }

                await ShellService.RunCommandAsync("start explorer.exe", 10);
                return (true, $"Removed {removed} icon/thumbnail cache file(s). Skipped/locked: {failed}. Explorer restarted.");
            });
        }

        public static async Task<(bool Success, string Output)> ResetStoreAppsAsync()
        {
            return await ShellService.RunCommandAsync("wsreset.exe", 60);
        }

        public static async Task<(bool Success, string Output)> ReregisterStoreAppsAsync()
        {
            string psScript = "Get-AppXPackage | Foreach-Object {Add-AppxPackage -DisableDevelopmentMode -Register \"$($_.InstallLocation)\\AppXManifest.xml\" -ErrorAction SilentlyContinue}";
            return await ShellService.RunPowerShellAsync(psScript, 600);
        }

        public static async Task<(bool Success, string Output)> ClearFontCacheAsync()
        {
            string command = "net stop FontCache & del /f /q \"%WinDir%\\ServiceProfiles\\LocalService\\AppData\\Local\\FontCache\\*.dat\" & net start FontCache";
            return await ShellService.RunRepairCommandAsync(command, 60, admin: true);
        }

        public static async Task<(bool Success, string Output)> RestartSearchAsync()
        {
            return await ShellService.RunRepairCommandAsync("net stop WSearch & net start WSearch", 60, admin: true);
        }

        public static async Task<(bool Success, string Output)> ForceCloseAppAsync(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return (false, "Enter a process name first, e.g. 'chrome.exe'.");
            return await ShellService.KillProcessAsync(processName);
        }

        public static List<string> FindAppDataFolders(string appName)
        {
            var found = new List<string>();
            if (string.IsNullOrWhiteSpace(appName)) return found;

            string localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? "";
            string appData = Environment.GetEnvironmentVariable("APPDATA") ?? "";

            foreach (string baseDir in new[] { localAppData, appData })
            {
                if (string.IsNullOrWhiteSpace(baseDir) || !Directory.Exists(baseDir)) continue;
                try
                {
                    foreach (string dir in Directory.GetDirectories(baseDir))
                    {
                        string folderName = Path.GetFileName(dir);
                        if (folderName.Contains(appName, StringComparison.OrdinalIgnoreCase))
                        {
                            found.Add(dir);
                        }
                    }
                }
                catch { }
            }
            return found;
        }

        public static async Task<(bool Success, string Output)> ClearAppCacheAsync(string appName)
        {
            if (string.IsNullOrWhiteSpace(appName))
                return (false, "Enter an application name first, e.g. 'Spotify' or 'Slack'.");

            return await Task.Run(() =>
            {
                var folders = FindAppDataFolders(appName);
                if (folders.Count == 0)
                    return (false, $"No AppData folder matching '{appName}' was found.");

                int removed = 0;
                int failed = 0;
                var cleared = new List<string>();
                string[] cacheSubfolderNames = { "Cache", "Cache2", "GPUCache", "Code Cache", "Temp", "temp", "Logs" };

                foreach (string folder in folders)
                {
                    foreach (string sub in cacheSubfolderNames)
                    {
                        string target = Path.Combine(folder, sub);
                        if (!Directory.Exists(target)) continue;
                        try
                        {
                            var dirInfo = new DirectoryInfo(target);
                            foreach (FileInfo file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
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
                            cleared.Add(target);
                        }
                        catch { }
                    }
                }

                if (cleared.Count == 0)
                {
                    return (true, $"Found '{appName}' data at:\n" + string.Join("\n", folders) + "\n\nNo obvious cache subfolder was found, so nothing was removed.");
                }

                return (true, $"Cleared cache for '{appName}'. Removed {removed} file(s), skipped {failed} locked file(s).\n\nFolders cleared:\n" + string.Join("\n", cleared));
            });
        }

        public static (bool Success, string Output) OpenAppDataFolder(string appName)
        {
            var folders = FindAppDataFolders(appName);
            if (folders.Count == 0)
                return (false, $"No AppData folder matching '{appName}' was found.");

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = folders[0],
                    UseShellExecute = true
                });
                return (true, $"Opened: {folders[0]}");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public static async Task<(bool Success, string Output)> LaunchAsAdminAsync(string exePath)
        {
            if (string.IsNullOrWhiteSpace(exePath))
                return (false, "Choose an application (.exe) first.");
            if (!File.Exists(exePath))
                return (false, "That file does not exist.");

            string safePath = exePath.Replace("\"", "\"\"");
            return await ShellService.RunPowerShellAsync($"Start-Process -FilePath \"{safePath}\" -Verb RunAs", 15);
        }

        public static List<RepairActionItem> GetGeneralAppFixes()
        {
            return new List<RepairActionItem>
            {
                new(0, "Restart Windows Explorer", "Fix a frozen taskbar, Start menu, or missing desktop icons.", SystemRepairService.RestartExplorerAsync, false),
                new(1, "Rebuild Icon Cache", "Fix wrong or missing icons on the desktop and taskbar.", ClearIconCacheAsync, false),
                new(2, "Reset Windows Store Apps", "Fix Store/UWP apps (Mail, Photos, Settings, etc.) that won't open.", ResetStoreAppsAsync, false),
                new(3, "Re-register Store Apps", "Fix \"This app can't open\" errors for built-in Windows apps.", ReregisterStoreAppsAsync, false),
                new(4, "Clear Font Cache", "Fix garbled or missing text rendering across applications.", ClearFontCacheAsync, true),
                new(5, "Restart Windows Search", "Fix Start menu search not finding installed apps or files.", RestartSearchAsync, true),
            };
        }
    }
}
