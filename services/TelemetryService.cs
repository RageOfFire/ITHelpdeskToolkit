using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ITHelpdeskToolkit.Models;
using AppInfo = ITHelpdeskToolkit.AppInfo;

namespace ITHelpdeskToolkit.Services
{
    /// <summary>
    /// Collects machine hardware telemetry (mirroring InventoryService's
    /// scan, but shaped for the corporate telemetry API) and POSTs it to
    /// POST /api/client-telemetry/collect.
    /// </summary>
    public static class TelemetryService
    {
        private static readonly HttpClient _http = new()
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        public const string DefaultEndpointPath = "/api/client-telemetry/collect";

        public static async Task<TelemetryPayload> CollectAsync()
        {
            return await Task.Run(() =>
            {
                string osCaption = ShellService.GetCimValue("Win32_OperatingSystem", "Caption");
                if (string.IsNullOrWhiteSpace(osCaption)) osCaption = RuntimeInformation.OSDescription;

                string cpuName = ShellService.GetCimValue("Win32_Processor", "Name");
                if (string.IsNullOrWhiteSpace(cpuName)) cpuName = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Unknown CPU";

                int logicalCpus = Environment.ProcessorCount;
                int physicalCores = Math.Max(1, logicalCpus / 2);

                double cpuUsagePercent = GetCpuUsagePercent();

                double totalRamGb = GetTotalRamGb();
                double freeRamGb = GetFreeRamGb();
                double memUsagePercent = totalRamGb > 0
                    ? Math.Round((1.0 - (freeRamGb / totalRamGb)) * 100, 1)
                    : 0;

                string gpuRenderer = ShellService.GetCimValue("Win32_VideoController", "Name");
                if (string.IsNullOrWhiteSpace(gpuRenderer)) gpuRenderer = "Unavailable";

                string systemDrive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
                System.IO.DriveInfo drive = new(systemDrive);
                double diskTotalGb = Math.Round((double)drive.TotalSize / (1024 * 1024 * 1024), 2);
                double diskFreeGb = Math.Round((double)drive.AvailableFreeSpace / (1024 * 1024 * 1024), 2);

                var (batteryLevel, isCharging) = GetBatteryInfo();
                string ipAddress = InventoryService.GetIpAddress();
                string networkType = GetNetworkType(out bool onlineStatus);

                return new TelemetryPayload
                {
                    Os = osCaption,
                    Platform = "Win32",
                    CpuModel = cpuName,
                    CpuCores = physicalCores,
                    CpuUsagePercent = cpuUsagePercent,
                    TotalRamGb = totalRamGb,
                    FreeRamGb = freeRamGb,
                    MemoryUsagePercent = memUsagePercent,
                    GpuRenderer = gpuRenderer,
                    DiskTotalGb = diskTotalGb,
                    DiskFreeGb = diskFreeGb,
                    BatteryLevel = batteryLevel,
                    IsCharging = isCharging,
                    IpAddress = ipAddress,
                    NetworkType = networkType,
                    OnlineStatus = onlineStatus,
                    CollectedVia = "client_hardware_agent_api",
                    AgentVersion = $"v{AppInfo.Version}-ithelpdesk-toolkit"
                };
            });
        }

        /// <summary>
        /// POSTs the payload as JSON to {baseUrl}{DefaultEndpointPath} (or to
        /// baseUrl itself if it already contains a path). Returns the raw
        /// response body/error so the UI can display it in a log console.
        /// </summary>
        public static async Task<(bool Success, string Detail)> SendTelemetryAsync(TelemetryPayload payload, string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                return (false, "No telemetry endpoint configured.");

            string url = BuildEndpointUrl(baseUrl);

            try
            {
                using CancellationTokenSource cts = new(TimeSpan.FromSeconds(20));
                HttpResponseMessage response = await _http.PostAsJsonAsync(url, payload, cts.Token);
                string body = await response.Content.ReadAsStringAsync(cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    return (true, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}\r\n{body}");
                }

                return (false, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}\r\n{body}");
            }
            catch (TaskCanceledException)
            {
                return (false, "Request timed out contacting the telemetry endpoint.");
            }
            catch (HttpRequestException ex)
            {
                return (false, $"Network error contacting telemetry endpoint: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, $"Failed to send telemetry: {ex.Message}");
            }
        }

        public static string BuildEndpointUrl(string baseUrl)
        {
            string trimmed = baseUrl.Trim().TrimEnd('/');
            return trimmed.Contains("/api/client-telemetry/collect", StringComparison.OrdinalIgnoreCase)
                ? trimmed
                : trimmed + DefaultEndpointPath;
        }

        public static string ToJsonPreview(TelemetryPayload payload)
        {
            return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        }

        private static double GetCpuUsagePercent()
        {
            try
            {
                string loadStr = ShellService.GetCimValue("Win32_Processor", "LoadPercentage");
                if (double.TryParse(loadStr, out double load)) return load;
            }
            catch { }
            return 0;
        }

        private static double GetTotalRamGb()
        {
            try
            {
                string ramBytesStr = ShellService.GetCimValue("Win32_ComputerSystem", "TotalPhysicalMemory");
                if (long.TryParse(ramBytesStr, out long bytes))
                    return Math.Round((double)bytes / (1024 * 1024 * 1024), 2);
            }
            catch { }
            return 0;
        }

        private static double GetFreeRamGb()
        {
            try
            {
                string freeKbStr = ShellService.GetCimValue("Win32_OperatingSystem", "FreePhysicalMemory");
                if (long.TryParse(freeKbStr, out long kb))
                    return Math.Round((double)kb / (1024 * 1024), 2);
            }
            catch { }
            return 0;
        }

        private static (int? Level, bool? Charging) GetBatteryInfo()
        {
            try
            {
                PowerStatus status = SystemInformation.PowerStatus;
                if (status.BatteryChargeStatus.HasFlag(BatteryChargeStatus.NoSystemBattery))
                    return (null, null);

                int level = (int)Math.Round(status.BatteryLifePercent * 100);
                bool charging = status.PowerLineStatus == PowerLineStatus.Online;
                return (level, charging);
            }
            catch
            {
                return (null, null);
            }
        }

        private static string GetNetworkType(out bool onlineStatus)
        {
            onlineStatus = NetworkInterface.GetIsNetworkAvailable();
            try
            {
                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                        return "WI-FI";

                    if (nic.NetworkInterfaceType.ToString().Contains("Ethernet", StringComparison.OrdinalIgnoreCase))
                    {
                        long speedMbps = nic.Speed / 1_000_000;
                        return speedMbps >= 1000 ? "GIGABIT ETHERNET" : "ETHERNET";
                    }

                    return nic.NetworkInterfaceType.ToString().ToUpperInvariant();
                }
            }
            catch { }
            return "UNKNOWN";
        }
    }
}
