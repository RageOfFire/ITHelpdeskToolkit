using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.ServiceProcess;
using System.Threading.Tasks;
using ITHelpdeskToolkit.Models;

namespace ITHelpdeskToolkit.Services
{
    public static class PrinterService
    {
        public static async Task<List<PrinterInfo>> GetPrintersAsync()
        {
            string script = "Get-Printer | Select-Object Name,Default,PrinterStatus,DriverName,PortName | ConvertTo-Csv -NoTypeInformation";
            var (success, output) = await ShellService.RunPowerShellAsync(script);
            var list = new List<PrinterInfo>();
            if (!success || string.IsNullOrWhiteSpace(output)) return list;

            string[] lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2) return list;

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                string[] parts = ParseCsvLine(line);
                if (parts.Length >= 5)
                {
                    list.Add(new PrinterInfo
                    {
                        Name = parts[0].Trim('"').Trim(),
                        IsDefault = parts[1].Trim('"').Equals("True", StringComparison.OrdinalIgnoreCase),
                        Status = parts[2].Trim('"').Trim(),
                        Driver = parts[3].Trim('"').Trim(),
                        Port = parts[4].Trim('"').Trim()
                    });
                }
            }
            return list;
        }

        public static string GetSpoolerStatus()
        {
            try
            {
                using ServiceController sc = new("Spooler");
                return sc.Status.ToString();
            }
            catch
            {
                return "Unavailable";
            }
        }

        public static async Task<string> GetPrintJobsAsync(string? printerName = null)
        {
            string script;
            if (!string.IsNullOrWhiteSpace(printerName))
            {
                string safe = printerName.Replace("'", "''");
                script = $"Get-PrintJob -PrinterName '{safe}' | Select-Object ID,DocumentName,UserName,JobStatus,Size | ConvertTo-Csv -NoTypeInformation";
            }
            else
            {
                script = "Get-PrintJob -PrinterName * | Select-Object PrinterName,ID,DocumentName,UserName,JobStatus,Size | ConvertTo-Csv -NoTypeInformation";
            }

            var (success, output) = await ShellService.RunPowerShellAsync(script);
            return success ? output : "";
        }

        public static async Task<(bool Success, string Detail)> CheckPrinterConnectivityAsync(string port)
        {
            if (string.IsNullOrWhiteSpace(port))
                return (false, "No printer port reported.");

            string portUpper = port.ToUpperInvariant();
            if (portUpper.StartsWith("USB") || portUpper.StartsWith("LPT") || portUpper.StartsWith("COM"))
                return (true, $"{port} is a local printer port.");

            string candidate = portUpper.StartsWith("IP_") ? port[3..] : port;

            if (System.Net.IPAddress.TryParse(candidate, out _))
            {
                return await Task.Run(() =>
                {
                    try
                    {
                        using Ping ping = new();
                        PingReply reply = ping.Send(candidate, 4000);
                        if (reply.Status == IPStatus.Success)
                        {
                            return (true, $"Printer IP {candidate} responded to ping.");
                        }
                        return (false, $"Printer IP {candidate} did not respond to ping ({reply.Status}).");
                    }
                    catch (Exception ex)
                    {
                        return (false, $"Ping failed for IP {candidate}: {ex.Message}");
                    }
                });
            }

            return (true, $"Port '{port}' is configured; automatic IP testing was not possible.");
        }

        public static async Task<(bool Success, string Output)> RestartSpoolerAsync()
        {
            return await ShellService.RunRepairCommandAsync("net stop spooler && net start spooler", 30, admin: true);
        }

        public static async Task<(bool Success, string Output)> ClearSelectedJobsAsync(string printerName)
        {
            if (string.IsNullOrWhiteSpace(printerName))
                return (false, "No printer selected.");

            string safe = printerName.Replace("'", "''");
            string script = $"Get-PrintJob -PrinterName '{safe}' | Remove-PrintJob -Confirm:$false";
            return await ShellService.RunPowerShellAsync(script, 30);
        }

        private static string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            string current = "";

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current);
                    current = "";
                }
                else
                {
                    current += c;
                }
            }
            result.Add(current);
            return result.ToArray();
        }
    }
}
