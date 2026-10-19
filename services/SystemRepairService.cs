using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ITHelpdeskToolkit.Models;

namespace ITHelpdeskToolkit.Services
{
    public static class SystemRepairService
    {
        public static async Task<(bool Success, string Output)> RepairSfcAsync()
        {
            return await ShellService.RunRepairCommandAsync("sfc /scannow", 900, admin: true);
        }

        public static async Task<(bool Success, string Output)> RepairDismCheckAsync()
        {
            return await ShellService.RunRepairCommandAsync("DISM /Online /Cleanup-Image /CheckHealth", 300, admin: true);
        }

        public static async Task<(bool Success, string Output)> RepairDismScanAsync()
        {
            return await ShellService.RunRepairCommandAsync("DISM /Online /Cleanup-Image /ScanHealth", 600, admin: true);
        }

        public static async Task<(bool Success, string Output)> RepairDismRestoreAsync()
        {
            return await ShellService.RunRepairCommandAsync("DISM /Online /Cleanup-Image /RestoreHealth", 1200, admin: true);
        }

        public static async Task<(bool Success, string Output)> RepairChkdskAsync()
        {
            string drive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
            return await ShellService.RunRepairCommandAsync($"chkdsk {drive} /scan", 600, admin: true);
        }

        public static async Task<(bool Success, string Output)> ScheduleChkdskFixAsync()
        {
            string drive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
            return await ShellService.RunRepairCommandAsync($"echo Y| chkdsk {drive} /f /r", 60, admin: true);
        }

        public static async Task<(bool Success, string Output)> CheckDiskHealthAsync()
        {
            string script = "Get-PhysicalDisk | Select-Object DeviceId,FriendlyName,MediaType,HealthStatus,OperationalStatus | Format-Table -AutoSize | Out-String -Width 200";
            return await ShellService.RunPowerShellAsync(script, 30);
        }

        // Network Repairs
        public static async Task<(bool Success, string Output)> FlushDnsAsync()
        {
            return await ShellService.RunCommandAsync("ipconfig /flushdns", 60);
        }

        public static async Task<(bool Success, string Output)> ReleaseIpAsync()
        {
            return await ShellService.RunCommandAsync("ipconfig /release", 60);
        }

        public static async Task<(bool Success, string Output)> RenewIpAsync()
        {
            return await ShellService.RunCommandAsync("ipconfig /renew", 120);
        }

        public static async Task<(bool Success, string Output)> ResetWinsockAsync()
        {
            return await ShellService.RunRepairCommandAsync("netsh winsock reset", 120, admin: true);
        }

        public static async Task<(bool Success, string Output)> ResetTcpIpAsync()
        {
            return await ShellService.RunRepairCommandAsync("netsh int ip reset", 120, admin: true);
        }

        public static async Task<(bool Success, string Output)> RestartNetworkAdaptersAsync()
        {
            string psCommand = "Get-NetAdapter | Where-Object {$_.Status -eq 'Up'} | Restart-NetAdapter -Confirm:$false";
            string wrapped = $"powershell -NoProfile -ExecutionPolicy Bypass -Command \"{psCommand}\"";
            return await ShellService.RunRepairCommandAsync(wrapped, 60, admin: true);
        }

        public static async Task<(bool Success, string Output)> RestartExplorerAsync()
        {
            return await ShellService.RunCommandAsync("taskkill /f /im explorer.exe && start explorer.exe", 60);
        }

        public static List<RepairActionItem> GetSystemRepairActions()
        {
            return new List<RepairActionItem>
            {
                new(0, "SFC /scannow", "Repair protected Windows system files.", RepairSfcAsync, true),
                new(1, "DISM CheckHealth", "Quick component-store health check.", RepairDismCheckAsync, true),
                new(2, "DISM ScanHealth", "Deep component-store health scan.", RepairDismScanAsync, true),
                new(3, "DISM RestoreHealth", "Repair the Windows component store.", RepairDismRestoreAsync, true),
                new(4, "CHKDSK /scan", "Online NTFS file system scan without scheduling a reboot.", RepairChkdskAsync, true),
                new(5, "Check Disk Health", "Report SMART/reliability health status for each physical disk.", CheckDiskHealthAsync, false),
                new(6, "Schedule CHKDSK /f Repair", "Schedule a full file-system error repair for the next restart.", ScheduleChkdskFixAsync, true),
            };
        }

        public static List<RepairActionItem> GetNetworkRepairActions()
        {
            return new List<RepairActionItem>
            {
                new(0, "Flush DNS", "Clear the Windows DNS resolver cache.", FlushDnsAsync, false),
                new(1, "Release IP", "Release the current DHCP-assigned IP address.", ReleaseIpAsync, false),
                new(2, "Renew IP", "Request a fresh DHCP lease.", RenewIpAsync, false),
                new(3, "Reset Winsock", "Reset the Windows Winsock catalog (fixes many 'no internet' issues).", ResetWinsockAsync, true),
                new(4, "Reset TCP/IP", "Reset Windows TCP/IP configuration to defaults.", ResetTcpIpAsync, true),
                new(5, "Restart Network Adapters", "Disable and re-enable all active network adapters.", RestartNetworkAdaptersAsync, true),
            };
        }
    }
}
