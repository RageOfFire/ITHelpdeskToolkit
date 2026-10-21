using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Management;
using System.Text;
using System.Threading.Tasks;

namespace ITHelpdeskToolkit.Services
{
    public enum LicenseStatus { Unknown, Licensed, Unlicensed, Notification, Trial, Expired }

    public class LicenseResult
    {
        public string Product       { get; set; } = "";
        public string Version       { get; set; } = "";
        public string ProductKey    { get; set; } = "";
        public string LicenseType   { get; set; } = "";
        public LicenseStatus Status { get; set; } = LicenseStatus.Unknown;
        public string StatusDetail  { get; set; } = "";
        public string Channel       { get; set; } = "";
        public string RawOutput     { get; set; } = "";
    }

    public static class LicenseService
    {
        // ── Windows ──────────────────────────────────────────────────────────

        public static Task<LicenseResult> CheckWindowsAsync() => Task.Run(() =>
        {
            var result = new LicenseResult { Product = "Windows" };
            var sb = new StringBuilder();

            try
            {
                // Edition + build from registry
                using var key = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                string edition    = key?.GetValue("EditionID")?.ToString()    ?? "Unknown";
                string build      = key?.GetValue("CurrentBuild")?.ToString() ?? "Unknown";
                string ubr        = key?.GetValue("UBR")?.ToString()          ?? "0";
                string displayVer = key?.GetValue("DisplayVersion")?.ToString() ?? "";

                result.Version = $"Windows {edition}  {displayVer}  (Build {build}.{ubr})";
                sb.AppendLine($"Edition:  {edition}");
                sb.AppendLine($"Build:    {build}.{ubr}");
                sb.AppendLine($"Version:  {displayVer}");

                // Product key (digital license / OEM from firmware)
                string productKey = DecodeWindowsKey();
                result.ProductKey = string.IsNullOrEmpty(productKey) ? "N/A (Digital License)" : productKey;
                sb.AppendLine($"Key:      {result.ProductKey}");

                // Activation status via WMI
                using var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM SoftwareLicensingProduct " +
                    "WHERE PartialProductKey IS NOT NULL AND ApplicationId='55c92734-d682-4d71-983e-d6ec3f16059f'");

                foreach (ManagementObject obj in searcher.Get())
                {
                    uint licenseState = Convert.ToUInt32(obj["LicenseStatus"]);
                    string name       = obj["Name"]?.ToString() ?? "";
                    string channel    = obj["ProductKeyChannel"]?.ToString() ?? "";

                    result.LicenseType = channel;
                    result.Channel     = channel;

                    sb.AppendLine($"WMI Name: {name}");
                    sb.AppendLine($"Channel:  {channel}");

                    result.Status = licenseState switch
                    {
                        1 => LicenseStatus.Licensed,
                        2 => LicenseStatus.Notification,
                        5 => LicenseStatus.Notification,
                        0 => LicenseStatus.Unlicensed,
                        _ => LicenseStatus.Unknown
                    };

                    result.StatusDetail = licenseState switch
                    {
                        0 => "Unlicensed",
                        1 => "Licensed",
                        2 => "Out-of-box grace period",
                        3 => "Out-of-tolerance grace period",
                        4 => "Non-genuine grace period",
                        5 => "Notification (not activated)",
                        6 => "Extended grace period",
                        _ => $"Unknown state ({licenseState})"
                    };

                    sb.AppendLine($"State:    {result.StatusDetail}");
                    break;
                }

                if (result.Status == LicenseStatus.Unknown)
                {
                    result.StatusDetail = "Could not query WMI licensing info";
                }
            }
            catch (Exception ex)
            {
                result.Status      = LicenseStatus.Unknown;
                result.StatusDetail = $"Error: {ex.Message}";
                sb.AppendLine(ex.ToString());
            }

            result.RawOutput = sb.ToString();
            return result;
        });

        /// <summary>
        /// Decodes the Windows product key stored in the registry (works for
        /// OEM/retail keys embedded in firmware). Returns empty string for
        /// digital licenses that don't store a readable key.
        /// </summary>
        private static string DecodeWindowsKey()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                var digitalProductId = key?.GetValue("DigitalProductId") as byte[];
                if (digitalProductId == null) return "";

                // Windows 8/10/11 key encoding offset is 52
                const int keyOffset = 52;
                bool isWin8Plus = (digitalProductId[66] >> 3 & 1) == 1;
                digitalProductId[66] = (byte)((digitalProductId[66] & 0xF7) | ((isWin8Plus ? 1 : 0) << 2));

                const string chars = "BCDFGHJKMPQRTVWXY2346789";
                char[] keyChars = new char[25];
                for (int i = 24; i >= 0; i--)
                {
                    int current = 0;
                    for (int j = 14; j >= 0; j--)
                    {
                        current = (current * 256) ^ digitalProductId[j + keyOffset];
                        digitalProductId[j + keyOffset] = (byte)(current / 24);
                        current %= 24;
                    }
                    keyChars[i] = chars[current];
                    if (isWin8Plus && i == 0)
                    {
                        keyChars[i] = chars[current];
                    }
                }

                if (isWin8Plus)
                {
                    int last = keyChars[24];
                    for (int i = 24; i >= 1; i--)
                        keyChars[i] = keyChars[i - 1];
                    keyChars[0] = (char)last;
                }

                // Format as XXXXX-XXXXX-XXXXX-XXXXX-XXXXX
                string raw = new string(keyChars);
                return $"{raw[..5]}-{raw[5..10]}-{raw[10..15]}-{raw[15..20]}-{raw[20..25]}";
            }
            catch
            {
                return "";
            }
        }

        // ── Microsoft Office ─────────────────────────────────────────────────

        public static Task<LicenseResult> CheckOfficeAsync() => Task.Run(() =>
        {
            var result = new LicenseResult { Product = "Microsoft Office" };
            var sb     = new StringBuilder();

            try
            {
                // Detect installed Office version from registry
                (string versionKey, string displayName, string versionTag)? found = null;

                var versions = new[]
                {
                    ("16.0", "Office 2016 / 2019 / 2021 / 365", "16.0"),
                    ("15.0", "Office 2013",                      "15.0"),
                    ("14.0", "Office 2010",                      "14.0"),
                };

                foreach (var (ver, name, tag) in versions)
                {
                    string regPath32 = $@"SOFTWARE\Microsoft\Office\{ver}\Common\InstallRoot";
                    string regPath64 = $@"SOFTWARE\WOW6432Node\Microsoft\Office\{ver}\Common\InstallRoot";

                    using var k32 = Registry.LocalMachine.OpenSubKey(regPath32);
                    using var k64 = Registry.LocalMachine.OpenSubKey(regPath64);

                    if (k32?.GetValue("Path") != null || k64?.GetValue("Path") != null)
                    {
                        found = (ver, name, tag);
                        break;
                    }
                }

                if (found == null)
                {
                    result.Status      = LicenseStatus.Unknown;
                    result.StatusDetail = "Microsoft Office not detected";
                    result.Version     = "Not installed";
                    result.RawOutput   = "No Office installation found in registry.";
                    return result;
                }

                var (verKey, displayName, versionTag) = found.Value;
                result.Version = displayName;
                sb.AppendLine($"Detected: {displayName}  (registry key: {verKey})");

                // Try to get more specific version / build from Click-to-Run
                using var c2r = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Office\ClickToRun\Configuration");
                if (c2r != null)
                {
                    string platform   = c2r.GetValue("Platform")?.ToString()         ?? "";
                    string updateChan = c2r.GetValue("CDNBaseUrl")?.ToString()        ?? "";
                    string build      = c2r.GetValue("VersionToReport")?.ToString()   ?? "";
                    string products   = c2r.GetValue("ProductReleaseIds")?.ToString() ?? "";

                    sb.AppendLine($"Build:    {build}");
                    sb.AppendLine($"Platform: {platform}");
                    sb.AppendLine($"Products: {products}");

                    result.Channel = updateChan.Contains("Monthly")    ? "Monthly Channel"    :
                                     updateChan.Contains("SemiAnnual") ? "Semi-Annual Channel" :
                                     updateChan.Contains("Insider")    ? "Insider Channel"    :
                                     !string.IsNullOrEmpty(updateChan) ? "Click-to-Run"        : "";

                    if (products.Contains("O365") || products.Contains("Microsoft365") || products.Contains("365"))
                        result.LicenseType = "Microsoft 365 (Subscription)";
                    else if (products.Contains("2021"))
                        result.LicenseType = "Office 2021 (Perpetual)";
                    else if (products.Contains("2019"))
                        result.LicenseType = "Office 2019 (Perpetual)";
                    else if (products.Contains("2016"))
                        result.LicenseType = "Office 2016 (Perpetual)";
                    else
                        result.LicenseType = "Click-to-Run";

                    sb.AppendLine($"Type:     {result.LicenseType}");
                    sb.AppendLine($"Channel:  {result.Channel}");
                }
                else
                {
                    result.LicenseType = "MSI / Volume";
                    sb.AppendLine("Type:     MSI / Volume License (no Click-to-Run registry found)");
                }

                // Activation status via OSPP.VBS (works for all Office versions)
                string osppResult = RunOsppCheck(verKey);
                sb.AppendLine();
                sb.AppendLine("── OSPP.VBS output ──");
                sb.AppendLine(osppResult);

                ParseOsppOutput(osppResult, result);

                // Fallback: check registry activation flag
                if (result.Status == LicenseStatus.Unknown)
                {
                    using var actKey = Registry.LocalMachine.OpenSubKey(
                        $@"SOFTWARE\Microsoft\Office\{verKey}\Registration");
                    if (actKey != null)
                    {
                        result.Status      = LicenseStatus.Unknown;
                        result.StatusDetail = "Activation status unclear — run OSPP manually";
                    }
                }
            }
            catch (Exception ex)
            {
                result.Status      = LicenseStatus.Unknown;
                result.StatusDetail = $"Error: {ex.Message}";
                sb.AppendLine(ex.ToString());
            }

            result.RawOutput = sb.ToString();
            return result;
        });

        private static string RunOsppCheck(string officeVersion)
        {
            // Common Office install paths (32-bit and 64-bit)
            string[] paths =
            {
                $@"C:\Program Files\Microsoft Office\Office{officeVersion.Replace(".", "")}",
                $@"C:\Program Files (x86)\Microsoft Office\Office{officeVersion.Replace(".", "")}",
                @"C:\Program Files\Microsoft Office\root\Office16",
                @"C:\Program Files (x86)\Microsoft Office\root\Office16",
            };

            foreach (string path in paths)
            {
                string ospp = System.IO.Path.Combine(path, "OSPP.VBS");
                if (!System.IO.File.Exists(ospp)) continue;

                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName               = "cscript.exe",
                        Arguments              = $"//Nologo \"{ospp}\" /dstatus",
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true,
                        UseShellExecute        = false,
                        CreateNoWindow         = true
                    };
                    using var proc = System.Diagnostics.Process.Start(psi)!;
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(15000);
                    return string.IsNullOrWhiteSpace(output) ? "No output from OSPP.VBS" : output;
                }
                catch (Exception ex)
                {
                    return $"OSPP.VBS error: {ex.Message}";
                }
            }

            return "OSPP.VBS not found — Office may not be fully installed or path is non-standard.";
        }

        private static void ParseOsppOutput(string output, LicenseResult result)
        {
            if (string.IsNullOrWhiteSpace(output)) return;

            string lower = output.ToLowerInvariant();

            if (lower.Contains("license status: ---licensed---") ||
                lower.Contains("licensed"))
            {
                result.Status      = LicenseStatus.Licensed;
                result.StatusDetail = "Activated";
            }
            else if (lower.Contains("notification") || lower.Contains("grace period"))
            {
                result.Status      = LicenseStatus.Notification;
                result.StatusDetail = "Grace period / Notification mode";
            }
            else if (lower.Contains("unlicensed") || lower.Contains("not activated"))
            {
                result.Status      = LicenseStatus.Unlicensed;
                result.StatusDetail = "Not activated";
            }
            else if (lower.Contains("expired"))
            {
                result.Status      = LicenseStatus.Expired;
                result.StatusDetail = "License expired";
            }

            // Extract last 5 of product key if present
            foreach (string line in output.Split('\n'))
            {
                if (line.Contains("Last 5 characters of installed product key:", StringComparison.OrdinalIgnoreCase))
                {
                    string partial = line.Split(':')[^1].Trim();
                    if (!string.IsNullOrEmpty(partial))
                        result.ProductKey = $"*****-*****-*****-*****-{partial}";
                }
            }
        }
    }
}
