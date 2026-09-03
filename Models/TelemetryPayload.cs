using System.Text.Json.Serialization;

namespace ITHelpdeskToolkit.Models
{
    /// <summary>
    /// Matches the JSON body expected by POST /api/client-telemetry/collect.
    /// Property names are camelCase via JsonPropertyName so the C# model can
    /// keep normal PascalCase members.
    /// </summary>
    public class TelemetryPayload
    {
        [JsonPropertyName("os")]
        public string Os { get; set; } = string.Empty;

        [JsonPropertyName("platform")]
        public string Platform { get; set; } = string.Empty;

        [JsonPropertyName("cpuModel")]
        public string CpuModel { get; set; } = string.Empty;

        [JsonPropertyName("cpuCores")]
        public int CpuCores { get; set; }

        [JsonPropertyName("cpuUsagePercent")]
        public double CpuUsagePercent { get; set; }

        [JsonPropertyName("totalRamGb")]
        public double TotalRamGb { get; set; }

        [JsonPropertyName("freeRamGb")]
        public double FreeRamGb { get; set; }

        [JsonPropertyName("memoryUsagePercent")]
        public double MemoryUsagePercent { get; set; }

        [JsonPropertyName("gpuRenderer")]
        public string GpuRenderer { get; set; } = string.Empty;

        [JsonPropertyName("diskTotalGb")]
        public double DiskTotalGb { get; set; }

        [JsonPropertyName("diskFreeGb")]
        public double DiskFreeGb { get; set; }

        [JsonPropertyName("batteryLevel")]
        public int? BatteryLevel { get; set; }

        [JsonPropertyName("isCharging")]
        public bool? IsCharging { get; set; }

        [JsonPropertyName("ipAddress")]
        public string IpAddress { get; set; } = string.Empty;

        [JsonPropertyName("networkType")]
        public string NetworkType { get; set; } = string.Empty;

        [JsonPropertyName("onlineStatus")]
        public bool OnlineStatus { get; set; }

        [JsonPropertyName("collectedVia")]
        public string CollectedVia { get; set; } = "client_hardware_agent_api";

        [JsonPropertyName("agentVersion")]
        public string AgentVersion { get; set; } = "v1.0.0-ithelpdesk-toolkit";
    }
}
