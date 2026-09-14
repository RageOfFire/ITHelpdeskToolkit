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

        /// <summary>
        /// Creates a Scheduled Task that launches the given app elevated (as SYSTEM, "highest privileges"),
        /// then grants the current logged-in user permission to *run* that task. This lets a standard user
        /// launch one specific admin-only app afterward without a UAC prompt and without being made an admin.
        /// Must be run elevated (the tool itself needs admin rights to create the task and edit its ACL).
        /// </summary>
        /// <summary>
        /// Grants the current user Modify rights on the app's install folder (and its HKLM registry key,
        /// if present) so it no longer needs to run elevated. This is the fix for apps that only demand
        /// admin/UAC because they try to write config/log/data files next to their own .exe inside a
        /// protected location like Program Files. Must be run elevated once to change the ACLs.
        /// </summary>
        public static async Task<(bool Success, string Output)> AllowAppToRunWithoutAdminAsync(string exePath)
        {
            if (string.IsNullOrWhiteSpace(exePath))
                return (false, "Choose an application (.exe) first.");
            if (!File.Exists(exePath))
                return (false, "That file does not exist.");
            if (!ShellService.IsAdmin())
                return (false, "This tool must be running as Administrator to change folder permissions (one-time setup).");

            string? folder = Path.GetDirectoryName(exePath);
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                return (false, "Could not determine the application's install folder.");

            string userAccount = $"{Environment.UserDomainName}\\{Environment.UserName}";
            string safeFolder = folder.Replace("\"", "\"\"");

            // (OI)(CI)M = apply to this folder, subfolders and files, grant Modify.
            string command = $"icacls \"{safeFolder}\" /grant \"{userAccount}\":(OI)(CI)M /T /C";
            var (success, output) = await ShellService.RunRepairCommandAsync(command, 120, admin: true);

            if (!success)
                return (false, output);

            return (true,
                $"Granted '{Environment.UserName}' Modify access to:\r\n{folder}\r\n(and all files/subfolders inside it)\r\n\r\n" +
                "If the app still asks for admin after this, it's probably also writing to a protected " +
                "registry key (e.g. under HKEY_LOCAL_MACHINE) or requesting elevation via its own manifest " +
                "(check Properties > Compatibility > 'Run this program as an administrator' — untick that too), " +
                "rather than just the install folder.\r\n\r\n" +
                $"icacls output:\r\n{output}");
        }

        public static async Task<(bool Success, string Output, string TaskName)> GrantUserLaunchPermissionAsync(string exePath)
        {
            if (string.IsNullOrWhiteSpace(exePath))
                return (false, "Choose an application (.exe) first.", "");
            if (!File.Exists(exePath))
                return (false, "That file does not exist.", "");
            if (!ShellService.IsAdmin())
                return (false, "This tool must be running as Administrator to grant launch permissions (it needs to create the Scheduled Task once).", "");

            string appName = Path.GetFileNameWithoutExtension(exePath);
            string userName = Environment.UserName;
            string taskName = $"IT-Helpdesk-Elevated-{appName}-{userName}";
            string safePath = exePath.Replace("'", "''");
            string userAccount = $"{Environment.UserDomainName}\\{userName}".Replace("'", "''");
            string safeTaskName = taskName.Replace("'", "''");

            string psScript = $@"
$ErrorActionPreference = 'Stop'
$taskName = '{safeTaskName}'
$exePath  = '{safePath}'

Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue

$action    = New-ScheduledTaskAction -Execute $exePath
$principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
$settings  = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries

Register-ScheduledTask -TaskName $taskName -Action $action -Principal $principal -Settings $settings | Out-Null

$service = New-Object -ComObject 'Schedule.Service'
$service.Connect()
$folder = $service.GetFolder('\')
$task = $folder.GetTask($taskName)
$sd = $task.GetSecurityDescriptor(0xF)

$sid = (New-Object System.Security.Principal.NTAccount('{userAccount}')).Translate([System.Security.Principal.SecurityIdentifier]).Value
$ace = ""(A;;GRGX;;;$sid)""
if ($sd -notmatch [regex]::Escape($ace)) {{
    $sd = $sd + $ace
}}
$task.SetSecurityDescriptor($sd, 0)

Write-Output ""OK""
";

            var (success, output) = await ShellService.RunPowerShellAsync(psScript, 30);
            if (!success)
                return (false, output, "");

            string msg =
                $"Granted '{userName}' permission to launch '{Path.GetFileName(exePath)}' elevated, without a UAC prompt.\r\n\r\n" +
                $"Scheduled task created: {taskName}\r\n\r\n" +
                "The user can now run it any time (as themselves, no admin password needed) with:\r\n" +
                $"  schtasks /Run /TN \"{taskName}\"\r\n\r\n" +
                "Use 'Create Desktop Shortcut' to give them a clickable icon that runs that command, or 'Revoke Permission' below to undo this later.";

            return (true, msg, taskName);
        }

        /// <summary>
        /// Removes a previously created elevated-launch scheduled task, revoking the permission.
        /// </summary>
        public static async Task<(bool Success, string Output)> RevokeUserLaunchPermissionAsync(string taskName)
        {
            if (string.IsNullOrWhiteSpace(taskName))
                return (false, "Enter the scheduled task name to remove.");
            if (!ShellService.IsAdmin())
                return (false, "This tool must be running as Administrator to revoke this.");

            string safeName = taskName.Replace("\"", "");
            return await ShellService.RunCommandAsync($"schtasks /Delete /TN \"{safeName}\" /F", 15);
        }

        /// <summary>
        /// Creates a desktop shortcut for the current user that triggers the elevated scheduled task.
        /// Does not require admin rights (creating a shortcut in the user's own Desktop folder).
        /// </summary>
        public static (bool Success, string Output) CreateLaunchShortcut(string taskName, string appDisplayName)
        {
            if (string.IsNullOrWhiteSpace(taskName))
                return (false, "No scheduled task name provided.");

            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string safeDisplayName = string.IsNullOrWhiteSpace(appDisplayName) ? taskName : appDisplayName;
                foreach (char c in Path.GetInvalidFileNameChars())
                    safeDisplayName = safeDisplayName.Replace(c, '_');
                string shortcutPath = Path.Combine(desktop, $"{safeDisplayName} (Admin).lnk");

                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null)
                    return (false, "Could not create shortcut (WScript.Shell is unavailable on this system).");

                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = Environment.ExpandEnvironmentVariables(@"%WinDir%\System32\schtasks.exe");
                shortcut.Arguments = $"/Run /TN \"{taskName}\"";
                shortcut.WorkingDirectory = desktop;
                shortcut.Description = $"Launch {safeDisplayName} elevated without a UAC prompt";
                shortcut.Save();

                return (true, $"Desktop shortcut created:\r\n{shortcutPath}");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        // Registry path behind gpedit.msc > Computer Configuration > Administrative Templates >
        // Windows Components > Windows Update > "Configure Automatic Updates". Writing here has the
        // exact same effect as setting that policy through the Group Policy editor, so it also works
        // on Windows editions (e.g. Home) where gpedit.msc itself isn't available.
        private const string WindowsUpdatePolicyKey = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";

        /// <summary>
        /// Option 1: fully turn Windows Update off. Equivalent to setting "Configure Automatic Updates"
        /// to Disabled in gpedit.msc — no automatic checking, downloading, or installing of updates.
        /// </summary>
        public static async Task<(bool Success, string Output)> DisableWindowsUpdateFullyAsync()
        {
            string command = $"reg add \"{WindowsUpdatePolicyKey}\" /v NoAutoUpdate /t REG_DWORD /d 1 /f";
            var (success, output) = await ShellService.RunRepairCommandAsync(command, 30, admin: true);
            if (!success) return (false, output);

            return (true,
                "Windows Update has been turned off completely — automatic checking, downloading, and " +
                "installing of updates is now disabled.\r\n\r\n" +
                "Equivalent gpedit.msc setting:\r\nComputer Configuration > Administrative Templates > " +
                "Windows Components > Windows Update > \"Configure Automatic Updates\" = Disabled\r\n\r\n" + output);
        }

        /// <summary>
        /// Option 2: keep automatic download and notification, but never auto-install.
        /// Equivalent to "Configure Automatic Updates" = Enabled, option 3
        /// ("Auto download and notify for install") in gpedit.msc.
        /// </summary>
        public static async Task<(bool Success, string Output)> SetWindowsUpdateAutoDownloadNotifyInstallAsync()
        {
            string command = $"reg add \"{WindowsUpdatePolicyKey}\" /v NoAutoUpdate /t REG_DWORD /d 0 /f & " +
                              $"reg add \"{WindowsUpdatePolicyKey}\" /v AUOptions /t REG_DWORD /d 3 /f";
            var (success, output) = await ShellService.RunRepairCommandAsync(command, 30, admin: true);
            if (!success) return (false, output);

            return (true,
                "Windows Update will now auto-download updates in the background but will NOT install " +
                "them — the user is only notified once updates are ready to install.\r\n\r\n" +
                "Equivalent gpedit.msc setting:\r\nComputer Configuration > Administrative Templates > " +
                "Windows Components > Windows Update > \"Configure Automatic Updates\" = Enabled, option 3 " +
                "(\"Auto download and notify for install\")\r\n\r\n" + output);
        }

        /// <summary>
        /// Option 3: no auto-download and no auto-install — only notify. Equivalent to
        /// "Configure Automatic Updates" = Enabled, option 2 ("Notify for download and notify for
        /// install") in gpedit.msc.
        /// </summary>
        public static async Task<(bool Success, string Output)> SetWindowsUpdateNotifyOnlyAsync()
        {
            string command = $"reg add \"{WindowsUpdatePolicyKey}\" /v NoAutoUpdate /t REG_DWORD /d 0 /f & " +
                              $"reg add \"{WindowsUpdatePolicyKey}\" /v AUOptions /t REG_DWORD /d 2 /f";
            var (success, output) = await ShellService.RunRepairCommandAsync(command, 30, admin: true);
            if (!success) return (false, output);

            return (true,
                "Windows Update will now only notify about available updates — nothing is downloaded or " +
                "installed automatically; the user has to trigger both manually.\r\n\r\n" +
                "Equivalent gpedit.msc setting:\r\nComputer Configuration > Administrative Templates > " +
                "Windows Components > Windows Update > \"Configure Automatic Updates\" = Enabled, option 2 " +
                "(\"Notify for download and notify for install\")\r\n\r\n" + output);
        }

        /// <summary>
        /// Removes the policy values set by the three options above, restoring Windows Update to its
        /// default, OS-managed behavior. Equivalent to setting "Configure Automatic Updates" back to
        /// Not Configured in gpedit.msc.
        /// </summary>
        public static async Task<(bool Success, string Output)> RestoreWindowsUpdateDefaultsAsync()
        {
            // "reg delete /v" exits non-zero when the value isn't present, which just means there was
            // nothing to restore — chain with "& exit /b 0" so that isn't reported as a failure.
            string command = $"(reg delete \"{WindowsUpdatePolicyKey}\" /v NoAutoUpdate /f) & " +
                              $"(reg delete \"{WindowsUpdatePolicyKey}\" /v AUOptions /f) & exit /b 0";
            var (_, output) = await ShellService.RunRepairCommandAsync(command, 30, admin: true);

            return (true,
                "Windows Update policy overrides have been removed — Windows Update is back to its " +
                "default, OS-managed behavior (equivalent to \"Configure Automatic Updates\" = Not " +
                "Configured in gpedit.msc).\r\n\r\n" + output);
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
