using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ITHelpdeskToolkit.Models;

namespace ITHelpdeskToolkit.Services
{
    public static class CleanupService
    {
        public static async Task<(bool Success, string Output)> CleanupTempFilesAsync()
        {
            return await Task.Run(() =>
            {
                int removed = 0;
                int failed = 0;
                var locations = new List<string> { Path.GetTempPath(), Environment.GetEnvironmentVariable("TEMP") ?? "" };
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (string loc in locations)
                {
                    if (string.IsNullOrWhiteSpace(loc)) continue;
                    string fullPath = Path.GetFullPath(loc);
                    if (!Directory.Exists(fullPath) || seen.Contains(fullPath)) continue;
                    seen.Add(fullPath);

                    try
                    {
                        var dirInfo = new DirectoryInfo(fullPath);
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
                    }
                    catch { }
                }

                return (true, $"Temporary cleanup complete. Removed: {removed} file(s). Skipped/locked: {failed}.");
            });
        }

        public static async Task<(bool Success, string Output)> ClearPrefetchAsync()
        {
            return await Task.Run(() =>
            {
                string windir = Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";
                string prefetch = Path.Combine(windir, "Prefetch");
                if (!Directory.Exists(prefetch))
                {
                    return (true, "Prefetch folder not found — nothing to clear.");
                }

                int removed = 0;
                int failed = 0;
                try
                {
                    var dirInfo = new DirectoryInfo(prefetch);
                    foreach (FileInfo file in dirInfo.GetFiles())
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
                catch (Exception ex)
                {
                    return (false, ex.Message);
                }

                string note = (failed > 0 && removed == 0) ? " (may need administrator rights to clear this folder)" : "";
                return (true, $"Removed {removed} prefetch file(s). Skipped/locked: {failed}.{note}");
            });
        }

        public static async Task<(bool Success, string Output)> ClearBrowserCachesAsync()
        {
            return await Task.Run(() =>
            {
                string localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? "";
                var targets = new List<string>();

                if (!string.IsNullOrWhiteSpace(localAppData))
                {
                    targets.Add(Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "Cache"));
                    targets.Add(Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "Cache"));

                    string firefoxProfiles = Path.Combine(localAppData, "Mozilla", "Firefox", "Profiles");
                    if (Directory.Exists(firefoxProfiles))
                    {
                        try
                        {
                            foreach (string profileDir in Directory.GetDirectories(firefoxProfiles))
                            {
                                targets.Add(Path.Combine(profileDir, "cache2"));
                            }
                        }
                        catch { }
                    }
                }

                int removed = 0;
                int failed = 0;
                var cleared = new List<string>();

                foreach (string target in targets)
                {
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

                if (cleared.Count == 0)
                {
                    return (true, "No Chrome, Edge or Firefox cache folders were found for the current user.");
                }

                return (true, $"Cleared browser cache. Removed {removed} file(s), skipped {failed} locked file(s).\n\nFolders cleared:\n" + string.Join("\n", cleared));
            });
        }

        public static async Task<(bool Success, string Output)> EmptyRecycleBinAsync()
        {
            return await ShellService.RunPowerShellAsync("Clear-RecycleBin -Force -ErrorAction SilentlyContinue", 120);
        }

        public static async Task<(bool Success, string Output)> WindowsUpdateCleanupAsync()
        {
            return await ShellService.RunRepairCommandAsync("DISM /Online /Cleanup-Image /StartComponentCleanup /ResetBase", 1200, admin: true);
        }

        public static (bool Success, string Output) OpenDiskCleanup()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cleanmgr.exe",
                    UseShellExecute = true
                });
                return (true, "Disk Cleanup launched.");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public static string GetDiskSpaceSummary()
        {
            string driveLetter = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
            try
            {
                DriveInfo drive = new(driveLetter);
                double totalGb = Math.Round((double)drive.TotalSize / (1024 * 1024 * 1024), 2);
                double freeGb = Math.Round((double)drive.AvailableFreeSpace / (1024 * 1024 * 1024), 2);
                double usedGb = Math.Round(totalGb - freeGb, 2);
                double percentUsed = Math.Round((usedGb / totalGb) * 100, 1);

                return $"{driveLetter}   Total: {totalGb} GB   Used: {usedGb} GB ({percentUsed}%)   Free: {freeGb} GB";
            }
            catch (Exception ex)
            {
                return $"Unable to read disk usage: {ex.Message}";
            }
        }

        public static List<RepairActionItem> GetCleanupActions()
        {
            return new List<RepairActionItem>
            {
                new(0, "Clear TEMP Files", "Remove files from the current user's temporary folders.", CleanupTempFilesAsync, false),
                new(1, "Clear Prefetch", "Remove Windows Prefetch cache files (Windows rebuilds these automatically).", ClearPrefetchAsync, false),
                new(2, "Clear Browser Caches", "Clear Chrome, Edge and Firefox cache for the current user (history/logins untouched).", ClearBrowserCachesAsync, false),
                new(3, "Empty Recycle Bin", "Empty the Windows Recycle Bin.", EmptyRecycleBinAsync, false),
                new(4, "Windows Update Cleanup", "Free space used by superseded Windows Update files — often the single biggest space recovery on an old install.", WindowsUpdateCleanupAsync, true),
            };
        }
    }
}
