using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ITHelpdeskToolkit.Models;

namespace ITHelpdeskToolkit.Services
{
    public static class NetworkService
    {
        // Matches a bare IP-address-shaped continuation line, e.g. the
        // "192.168.1.1" that ipconfig prints on the line *after* the
        // "Default Gateway" label when an adapter has both an IPv6
        // link-local gateway and an IPv4 gateway.
        private static readonly Regex IpLikeLine = new(@"^[0-9a-fA-F:.%]+$", RegexOptions.Compiled);

        public static async Task<string?> GetGatewayAsync()
        {
            var (success, output) = await ShellService.RunCommandAsync("ipconfig");
            if (!success) return null;

            // ipconfig often prints an adapter's gateway as TWO lines when the
            // adapter is dual-stack:
            //   Default Gateway . . . . . . . . . : fe80::1%7
            //                                        192.168.1.1
            // Previously we only ever captured the first ("Default Gateway")
            // line, which meant a dual-stack adapter always showed the IPv6
            // link-local address instead of the plain IPv4 one people expect.
            // Now we collect every candidate (label line + any continuation
            // value lines that follow it) and prefer IPv4 among them.
            var candidates = new List<string>();
            bool inGatewayBlock = false;

            foreach (string raw in output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                if (raw.Contains("Default Gateway"))
                {
                    inGatewayBlock = true;
                    int idx = raw.IndexOf(':');
                    if (idx >= 0 && idx < raw.Length - 1)
                    {
                        string gw = raw[(idx + 1)..].Trim();
                        if (!string.IsNullOrWhiteSpace(gw) && gw != "0.0.0.0")
                        {
                            candidates.Add(gw);
                        }
                    }
                    continue;
                }

                if (inGatewayBlock)
                {
                    string trimmed = raw.Trim();
                    if (trimmed.Length > 0 && IpLikeLine.IsMatch(trimmed))
                    {
                        candidates.Add(trimmed);
                        continue;
                    }

                    // Any other line (a new label, or a blank line) means we've
                    // moved past this adapter's gateway value(s).
                    inGatewayBlock = false;
                }
            }

            if (candidates.Count == 0) return null;

            // Prefer a real IPv4 gateway (no ':') over an IPv6 link-local one.
            string? ipv4 = candidates.Find(c => !c.Contains(':'));
            return ipv4 ?? candidates[0];
        }

        public static async Task<NetworkDiagnosticResult> CheckAdapterAsync()
        {
            string script = "(Get-NetAdapter -Physical | Where-Object {$_.Status -eq 'Up'} | Select-Object Name,Status,LinkSpeed | ConvertTo-Csv -NoTypeInformation)";
            var (success, output) = await ShellService.RunPowerShellAsync(script);
            if (!success)
            {
                return new NetworkDiagnosticResult
                {
                    TestName = "Network Adapter",
                    Success = false,
                    Output = output,
                    LikelyProblem = "Unable to query Windows network adapters."
                };
            }

            string[] lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            bool isUp = lines.Length >= 2;
            return new NetworkDiagnosticResult
            {
                TestName = "Network Adapter",
                Success = isUp,
                Output = output,
                LikelyProblem = isUp ? "At least one physical network adapter is connected." : "No physical network adapter is currently connected."
            };
        }

        public static async Task<NetworkDiagnosticResult> CheckGatewayAsync()
        {
            string? gw = await GetGatewayAsync();
            if (string.IsNullOrWhiteSpace(gw))
            {
                return new NetworkDiagnosticResult
                {
                    TestName = "Default Gateway",
                    Success = false,
                    Output = "No default gateway found.",
                    LikelyProblem = "DHCP, network adapter, or local network configuration."
                };
            }

            var (success, output) = await ShellService.RunCommandAsync($"ping -n 3 {gw}");
            return new NetworkDiagnosticResult
            {
                TestName = "Default Gateway",
                Success = success,
                Output = output,
                LikelyProblem = success ? "Local network and gateway are reachable." : "LAN/Wi-Fi connection, switch, router, or gateway problem."
            };
        }

        public static async Task<NetworkDiagnosticResult> CheckInternetAsync()
        {
            var (success, output) = await ShellService.RunCommandAsync("ping -n 3 8.8.8.8");
            return new NetworkDiagnosticResult
            {
                TestName = "Internet (8.8.8.8)",
                Success = success,
                Output = output,
                LikelyProblem = success ? "Internet connection is reachable." : "Internet connection, firewall, router, or ISP problem."
            };
        }

        public static async Task<NetworkDiagnosticResult> CheckDnsAsync()
        {
            var (success, output) = await ShellService.RunCommandAsync("nslookup google.com");
            bool ok = success && output.Contains("Address");
            return new NetworkDiagnosticResult
            {
                TestName = "DNS",
                Success = ok,
                Output = output,
                LikelyProblem = ok ? "DNS resolution is working." : "DNS server or DNS configuration problem."
            };
        }

        public static async Task<NetworkDiagnosticResult> CheckGoogleAsync()
        {
            var (success, output) = await ShellService.RunCommandAsync("ping -n 3 google.com");
            return new NetworkDiagnosticResult
            {
                TestName = "Internet + DNS",
                Success = success,
                Output = output,
                LikelyProblem = success ? "Internet and DNS are working." : "DNS or Internet connectivity problem."
            };
        }

        public static async Task<NetworkDiagnosticResult> CheckRouteAsync(int maxHops = 30)
        {
            if (maxHops <= 0) maxHops = 30;
            int timeout = Math.Max(30, maxHops * 3);
            var (success, output) = await ShellService.RunCommandAsync($"tracert -d -h {maxHops} 8.8.8.8", timeout);
            return new NetworkDiagnosticResult
            {
                TestName = "Route",
                Success = success,
                Output = output,
                LikelyProblem = success ? "A route to the Internet was found." : "Routing, firewall, gateway, or Internet connection problem."
            };
        }

        public static async Task<NetworkDiagnosticResult> CheckTcpDnsAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    using TcpClient client = new();
                    var connectTask = client.ConnectAsync("8.8.8.8", 53);
                    if (connectTask.Wait(5000))
                    {
                        return new NetworkDiagnosticResult
                        {
                            TestName = "TCP DNS",
                            Success = true,
                            Output = "TCP connection to 8.8.8.8:53 succeeded.",
                            LikelyProblem = "DNS server is reachable."
                        };
                    }
                    return new NetworkDiagnosticResult
                    {
                        TestName = "TCP DNS",
                        Success = false,
                        Output = "Connection timed out after 5 seconds.",
                        LikelyProblem = "Firewall, routing, or network connectivity problem."
                    };
                }
                catch (Exception ex)
                {
                    return new NetworkDiagnosticResult
                    {
                        TestName = "TCP DNS",
                        Success = false,
                        Output = ex.Message,
                        LikelyProblem = "Firewall, routing, or network connectivity problem."
                    };
                }
            });
        }
    }
}
