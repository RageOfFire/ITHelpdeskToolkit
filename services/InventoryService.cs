using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ITHelpdeskToolkit.Models;

namespace ITHelpdeskToolkit.Services
{
    public static class InventoryService
    {
        public static async Task<List<InventoryItem>> CollectInventoryAsync()
        {
            return await Task.Run(() =>
            {
                var list = new List<InventoryItem>();

                string scanDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string compName = Environment.MachineName;
                string userName = Environment.UserName;

                string manufacturer = ShellService.GetCimValue("Win32_ComputerSystem", "Manufacturer");
                if (string.IsNullOrWhiteSpace(manufacturer)) manufacturer = "Unavailable";

                string model = ShellService.GetCimValue("Win32_ComputerSystem", "Model");
                if (string.IsNullOrWhiteSpace(model)) model = "Unavailable";

                string serial = ShellService.GetCimValue("Win32_BIOS", "SerialNumber");
                if (string.IsNullOrWhiteSpace(serial)) serial = "Unavailable";

                string osCaption = ShellService.GetCimValue("Win32_OperatingSystem", "Caption");
                if (string.IsNullOrWhiteSpace(osCaption)) osCaption = RuntimeInformation.OSDescription;

                string osVersion = ShellService.GetCimValue("Win32_OperatingSystem", "Version");
                if (string.IsNullOrWhiteSpace(osVersion)) osVersion = Environment.OSVersion.Version.ToString();

                string osBuild = ShellService.GetCimValue("Win32_OperatingSystem", "BuildNumber");
                if (string.IsNullOrWhiteSpace(osBuild)) osBuild = "Unavailable";

                string cpuName = ShellService.GetCimValue("Win32_Processor", "Name");
                if (string.IsNullOrWhiteSpace(cpuName)) cpuName = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Unknown CPU";

                int logicalCpus = Environment.ProcessorCount;
                int physicalCores = logicalCpus / 2;
                if (physicalCores < 1) physicalCores = 1;

                string systemDrive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
                DriveInfo drive = new(systemDrive);

                double diskTotalGb = Math.Round((double)drive.TotalSize / (1024 * 1024 * 1024), 2);
                double diskFreeGb = Math.Round((double)drive.AvailableFreeSpace / (1024 * 1024 * 1024), 2);
                double diskUsedPercent = Math.Round((1.0 - ((double)drive.AvailableFreeSpace / drive.TotalSize)) * 100, 1);

                double ramTotalGb = GetTotalRamGb();
                double ramAvailableGb = GetAvailableRamGb();

                string ipAddress = GetIpAddress();
                string macAddress = GetMacAddress();
                string wifiMacAddress = GetWifiMacAddress();
                string architecture = RuntimeInformation.OSArchitecture.ToString();

                list.Add(new InventoryItem("Scan Date", scanDate));
                list.Add(new InventoryItem("Computer Name", compName));
                list.Add(new InventoryItem("Current User", userName));
                list.Add(new InventoryItem("Manufacturer", manufacturer));
                list.Add(new InventoryItem("Model", model));
                list.Add(new InventoryItem("Serial Number", serial));
                list.Add(new InventoryItem("Operating System", osCaption));
                list.Add(new InventoryItem("Windows Version", osVersion));
                list.Add(new InventoryItem("Windows Build", osBuild));
                list.Add(new InventoryItem("CPU", cpuName));
                list.Add(new InventoryItem("CPU Cores", physicalCores.ToString()));
                list.Add(new InventoryItem("Logical CPUs", logicalCpus.ToString()));
                list.Add(new InventoryItem("RAM Total (GB)", ramTotalGb.ToString("F2")));
                list.Add(new InventoryItem("RAM Available (GB)", ramAvailableGb > 0 ? ramAvailableGb.ToString("F2") : "Unavailable"));
                list.Add(new InventoryItem("Disk", systemDrive));
                list.Add(new InventoryItem("Disk Total (GB)", diskTotalGb.ToString("F2")));
                list.Add(new InventoryItem("Disk Free (GB)", diskFreeGb.ToString("F2")));
                list.Add(new InventoryItem("Disk Used (%)", diskUsedPercent.ToString("F1") + "%"));
                list.Add(new InventoryItem("IP Address", ipAddress));
                list.Add(new InventoryItem("MAC Address", macAddress));
                list.Add(new InventoryItem("Wi-Fi MAC Address", wifiMacAddress));
                list.Add(new InventoryItem("Architecture", architecture));

                return list;
            });
        }

        public static string GetIpAddress()
        {
            try
            {
                string hostName = Dns.GetHostName();
                IPHostEntry hostEntry = Dns.GetHostEntry(hostName);
                foreach (IPAddress ip in hostEntry.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        return ip.ToString();
                    }
                }
            }
            catch { }
            return "Unavailable";
        }

        public static string GetMacAddress()
        {
            try
            {
                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus == OperationalStatus.Up &&
                        nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                        nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                    {
                        string mac = nic.GetPhysicalAddress().ToString();
                        if (!string.IsNullOrEmpty(mac))
                        {
                            return string.Join(":", System.Text.RegularExpressions.Regex.Split(mac, "(.{2})")).Trim(':');
                        }
                    }
                }
            }
            catch { }
            return "Unavailable";
        }

        /// <summary>
        /// Specifically the MAC address of the wireless (Wi-Fi) adapter, as
        /// opposed to GetMacAddress() above which returns whichever adapter
        /// (often Ethernet) happens to be up first.
        /// </summary>
        public static string GetWifiMacAddress()
        {
            try
            {
                // Prefer an active/connected Wi-Fi adapter, but fall back to
                // any Wi-Fi adapter present (e.g. currently disconnected) so
                // the field still shows something useful.
                NetworkInterface? upWifi = null;
                NetworkInterface? anyWifi = null;

                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.NetworkInterfaceType != NetworkInterfaceType.Wireless80211)
                        continue;

                    anyWifi ??= nic;
                    if (nic.OperationalStatus == OperationalStatus.Up)
                    {
                        upWifi = nic;
                        break;
                    }
                }

                NetworkInterface? chosen = upWifi ?? anyWifi;
                if (chosen != null)
                {
                    string mac = chosen.GetPhysicalAddress().ToString();
                    if (!string.IsNullOrEmpty(mac))
                    {
                        return string.Join(":", System.Text.RegularExpressions.Regex.Split(mac, "(.{2})")).Trim(':');
                    }
                }
            }
            catch { }
            return "No Wi-Fi adapter found";
        }

        private static double GetTotalRamGb()
        {
            try
            {
                string ramBytesStr = ShellService.GetCimValue("Win32_ComputerSystem", "TotalPhysicalMemory");
                if (long.TryParse(ramBytesStr, out long bytes))
                {
                    return Math.Round((double)bytes / (1024 * 1024 * 1024), 2);
                }
            }
            catch { }
            return 0;
        }

        private static double GetAvailableRamGb()
        {
            try
            {
                string freeKbStr = ShellService.GetCimValue("Win32_OperatingSystem", "FreePhysicalMemory");
                if (long.TryParse(freeKbStr, out long kb))
                {
                    return Math.Round((double)kb / (1024 * 1024), 2);
                }
            }
            catch { }
            return 0;
        }
    }
}
