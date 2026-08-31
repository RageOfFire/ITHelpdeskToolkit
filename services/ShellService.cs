using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace ITHelpdeskToolkit.Services
{
    public static class ShellService
    {
        public static bool IsAdmin()
        {
            try
            {
                using WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        public static async Task<(bool Success, string Output)> RunCommandAsync(string command, int timeoutSeconds = 30)
        {
            try
            {
                ProcessStartInfo psi = new()
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using Process? process = Process.Start(psi);
                if (process == null)
                    return (false, "Failed to start process.");

                Task<string> stdOutTask = process.StandardOutput.ReadToEndAsync();
                Task<string> stdErrTask = process.StandardError.ReadToEndAsync();

                using CancellationTokenSource cts = new(TimeSpan.FromSeconds(timeoutSeconds));
                Task waitForExit = process.WaitForExitAsync(cts.Token);

                if (await Task.WhenAny(waitForExit, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds))) != waitForExit)
                {
                    try { process.Kill(true); } catch { }
                    return (false, "Command timed out.");
                }

                string stdOut = await stdOutTask;
                string stdErr = await stdErrTask;
                string output = !string.IsNullOrWhiteSpace(stdOut) ? stdOut.Trim() : stdErr.Trim();

                return (process.ExitCode == 0, output);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public static async Task<(bool Success, string Output)> RunPowerShellAsync(string psScript, int timeoutSeconds = 30)
        {
            string tempScript = Path.Combine(
                Path.GetTempPath(),
                $"helpdesk_ps_{Environment.ProcessId}_{DateTime.Now.Ticks}.ps1"
            );

            try
            {
                await File.WriteAllTextAsync(tempScript, psScript, Encoding.UTF8);
                return await RunCommandAsync($"powershell -NoProfile -ExecutionPolicy Bypass -File \"{tempScript}\"", timeoutSeconds);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
            finally
            {
                try
                {
                    if (File.Exists(tempScript))
                        File.Delete(tempScript);
                }
                catch { }
            }
        }

        public static async Task<(bool Success, string Output)> RunElevatedAsync(string command, int timeoutSeconds = 300)
        {
            if (IsAdmin())
            {
                return await RunCommandAsync(command, timeoutSeconds);
            }

            string tempDir = Path.GetTempPath();
            string token = $"{Environment.ProcessId}_{DateTime.Now.Ticks}";
            string batPath = Path.Combine(tempDir, $"helpdesk_elevated_{token}.bat");
            string logPath = Path.Combine(tempDir, $"helpdesk_elevated_{token}.log");

            try
            {
                await File.WriteAllTextAsync(batPath, $"@echo off\r\n{command} > \"{logPath}\" 2>&1\r\n", Encoding.UTF8);

                string escapedBat = batPath.Replace("'", "''");
                string ps = $"Start-Process -FilePath '{escapedBat}' -Verb RunAs -Wait -WindowStyle Hidden";

                var (psSuccess, psOutput) = await RunPowerShellAsync(ps, timeoutSeconds);

                string logContent = "";
                if (File.Exists(logPath))
                {
                    try
                    {
                        logContent = (await File.ReadAllTextAsync(logPath)).Trim();
                    }
                    catch { }
                }

                if (!psSuccess && string.IsNullOrWhiteSpace(logContent))
                {
                    return (false, !string.IsNullOrWhiteSpace(psOutput) ? psOutput : "Elevation was cancelled or failed (UAC declined).");
                }

                return (true, !string.IsNullOrWhiteSpace(logContent) ? logContent : "Command completed with no output.");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
            finally
            {
                try
                {
                    if (File.Exists(batPath)) File.Delete(batPath);
                    if (File.Exists(logPath)) File.Delete(logPath);
                }
                catch { }
            }
        }

        public static async Task<(bool Success, string Output)> RunRepairCommandAsync(string command, int timeoutSeconds = 300, bool admin = false)
        {
            if (admin && !IsAdmin())
            {
                return await RunElevatedAsync(command, timeoutSeconds);
            }
            return await RunCommandAsync(command, timeoutSeconds);
        }

        public static async Task<(bool Success, string Output)> KillProcessAsync(string processName)
        {
            string safe = processName.Replace("\"", "");
            return await RunCommandAsync($"taskkill /F /IM \"{safe}\"", 60);
        }

        public static string GetCimValue(string className, string propertyName)
        {
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher($"SELECT {propertyName} FROM {className}");
                foreach (var obj in searcher.Get())
                {
                    var val = obj[propertyName];
                    if (val != null) return val.ToString()?.Trim() ?? "";
                }
            }
            catch { }
            return "";
        }
    }
}
