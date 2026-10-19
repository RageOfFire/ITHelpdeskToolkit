using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace ITHelpdeskToolkit.Services
{
    public static class VirusScanService
    {
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(3) };

        // ---------------------------------------------------------------
        // OFFLINE — uses the Windows Defender CLI already on every
        // Windows 10/11 PC. No network access, no API key required.
        // ---------------------------------------------------------------

        public static string? FindDefenderCli()
        {
            // Windows Defender auto-updates into a versioned folder under ProgramData;
            // that copy is more current than the one under Program Files, so prefer it.
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string platformRoot = Path.Combine(programData, "Microsoft", "Windows Defender", "Platform");

            try
            {
                if (Directory.Exists(platformRoot))
                {
                    string? newest = Directory.GetDirectories(platformRoot)
                        .OrderByDescending(d => d)
                        .FirstOrDefault(d => File.Exists(Path.Combine(d, "MpCmdRun.exe")));

                    if (newest != null)
                    {
                        return Path.Combine(newest, "MpCmdRun.exe");
                    }
                }
            }
            catch { }

            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string fallback = Path.Combine(programFiles, "Windows Defender", "MpCmdRun.exe");
            return File.Exists(fallback) ? fallback : null;
        }

        public static async Task<(bool Success, bool ThreatFound, string Output)> OfflineScanAsync(string path)
        {
            path = Path.GetFullPath(path);
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                return (false, false, $"Path does not exist: {path}");
            }

            string? cli = FindDefenderCli();
            if (cli == null)
            {
                return (false, false, "Could not find MpCmdRun.exe — Windows Defender does not appear to be " +
                                      "installed or is disabled (common on PCs running a third-party antivirus, " +
                                      "which replaces Defender's real-time engine).");
            }

            // -ScanType 3 = a custom scan targeting a specific file or folder path.
            string command = $"\"{cli}\" -Scan -ScanType 3 -File \"{path}\" -DisableRemediation";
            var (success, output) = await ShellService.RunCommandAsync(command, 180);

            // MpCmdRun exits non-zero both on a real error and when a threat is found and
            // remediation is disabled, so we look at the actual text instead of just ExitCode.
            bool threatFound = output.Contains("Threat", StringComparison.OrdinalIgnoreCase) &&
                                !output.Contains("Threats found: 0", StringComparison.OrdinalIgnoreCase) &&
                                !output.Contains("0 threat", StringComparison.OrdinalIgnoreCase);

            string summary = threatFound
                ? $"⚠ THREAT DETECTED by Windows Defender:\n{output}"
                : $"No threats found by Windows Defender (offline scan).\n\n{output}";

            return (true, threatFound, summary);
        }

        // ---------------------------------------------------------------
        // ONLINE — VirusTotal v3 API. Requires the user's own free API key
        // (from virustotal.com) and outbound internet access. We hash-check
        // first (no upload needed for files VT has already seen); only
        // unseen files get uploaded, with the user's consent handled by
        // the caller since that means the file leaves the machine.
        // ---------------------------------------------------------------

        // ---------------------------------------------------------------
        // Local API key storage — plain text under the user's AppData.
        // Good enough for a helpdesk utility; not meant for shared/kiosk PCs.
        // ---------------------------------------------------------------

        private static string ApiKeyConfigPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ITHelpdeskToolkit", "vt_apikey.txt");

        public static string LoadSavedApiKey()
        {
            try
            {
                return File.Exists(ApiKeyConfigPath) ? File.ReadAllText(ApiKeyConfigPath).Trim() : "";
            }
            catch
            {
                return "";
            }
        }

        public static void SaveApiKey(string apiKey)
        {
            try
            {
                string dir = Path.GetDirectoryName(ApiKeyConfigPath)!;
                Directory.CreateDirectory(dir);
                File.WriteAllText(ApiKeyConfigPath, apiKey.Trim());
            }
            catch { }
        }

        public static async Task<string> ComputeSha256Async(string filePath)
        {
            await using FileStream stream = File.OpenRead(filePath);
            byte[] hash = await SHA256.HashDataAsync(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        public static async Task<(bool Success, bool Found, bool Malicious, string Output)> OnlineScanByHashAsync(
            string filePath, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return (false, false, false, "No VirusTotal API key configured. Get a free key at " +
                                             "virustotal.com/gui/join-us and paste it in before scanning online.");
            }

            if (!File.Exists(filePath))
            {
                return (false, false, false, $"File does not exist: {filePath}");
            }

            string hash;
            try
            {
                hash = await ComputeSha256Async(filePath);
            }
            catch (Exception ex)
            {
                return (false, false, false, $"Could not hash file: {ex.Message}");
            }

            try
            {
                using HttpRequestMessage req = new(HttpMethod.Get, $"https://www.virustotal.com/api/v3/files/{hash}");
                req.Headers.Add("x-apikey", apiKey);

                using HttpResponseMessage resp = await _http.SendAsync(req);

                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return (true, false, false,
                        $"SHA-256: {hash}\n\nVirusTotal has no record of this exact file yet. " +
                        "Use 'Upload for Online Scan' to submit it for a fresh analysis (the file will be sent to VirusTotal).");
                }

                if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return (false, false, false, "VirusTotal rejected the API key (401/403). Double-check the key.");
                }

                if (!resp.IsSuccessStatusCode)
                {
                    string body = await resp.Content.ReadAsStringAsync();
                    return (false, false, false, $"VirusTotal returned HTTP {(int)resp.StatusCode}: {body}");
                }

                string json = await resp.Content.ReadAsStringAsync();
                return ParseVirusTotalFileReport(hash, json);
            }
            catch (TaskCanceledException)
            {
                return (false, false, false, "Request to VirusTotal timed out. Check the internet connection and retry.");
            }
            catch (HttpRequestException ex)
            {
                return (false, false, false, $"Could not reach VirusTotal: {ex.Message}");
            }
        }

        public static async Task<(bool Success, bool Malicious, string Output)> UploadForOnlineScanAsync(
            string filePath, string apiKey, int pollTimeoutSeconds = 120)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return (false, false, "No VirusTotal API key configured.");
            }
            if (!File.Exists(filePath))
            {
                return (false, false, $"File does not exist: {filePath}");
            }

            var fileInfo = new FileInfo(filePath);
            // VirusTotal's public API caps direct uploads at 32MB; larger files need a
            // special upload-URL endpoint that's out of scope for a helpdesk quick-scan tool.
            if (fileInfo.Length > 32 * 1024 * 1024)
            {
                return (false, false, "File is larger than 32MB — VirusTotal's standard upload endpoint can't accept it. " +
                                      "Use the offline Windows Defender scan for large files instead.");
            }

            string analysisId;
            try
            {
                using MultipartFormDataContent form = new();
                byte[] bytes = await File.ReadAllBytesAsync(filePath);
                ByteArrayContent fileContent = new(bytes);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                form.Add(fileContent, "file", fileInfo.Name);

                using HttpRequestMessage req = new(HttpMethod.Post, "https://www.virustotal.com/api/v3/files")
                {
                    Content = form
                };
                req.Headers.Add("x-apikey", apiKey);

                using HttpResponseMessage resp = await _http.SendAsync(req);
                string body = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    return (false, false, $"Upload failed: HTTP {(int)resp.StatusCode} — {body}");
                }

                using JsonDocument doc = JsonDocument.Parse(body);
                analysisId = doc.RootElement.GetProperty("data").GetProperty("id").GetString() ?? "";
                if (string.IsNullOrEmpty(analysisId))
                {
                    return (false, false, "Upload succeeded but VirusTotal did not return an analysis ID.");
                }
            }
            catch (Exception ex)
            {
                return (false, false, $"Upload failed: {ex.Message}");
            }

            // Poll the analysis endpoint until VirusTotal finishes scanning with its engines.
            DateTime deadline = DateTime.UtcNow.AddSeconds(pollTimeoutSeconds);
            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(5000);

                try
                {
                    using HttpRequestMessage pollReq = new(HttpMethod.Get,
                        $"https://www.virustotal.com/api/v3/analyses/{analysisId}");
                    pollReq.Headers.Add("x-apikey", apiKey);

                    using HttpResponseMessage pollResp = await _http.SendAsync(pollReq);
                    if (!pollResp.IsSuccessStatusCode) continue;

                    string pollBody = await pollResp.Content.ReadAsStringAsync();
                    using JsonDocument pollDoc = JsonDocument.Parse(pollBody);
                    JsonElement attrs = pollDoc.RootElement.GetProperty("data").GetProperty("attributes");
                    string status = attrs.GetProperty("status").GetString() ?? "";

                    if (status == "completed")
                    {
                        var (malicious, suspicious, harmless, undetected) = ExtractStats(attrs.GetProperty("stats"));
                        string verdict = malicious > 0
                            ? $"⚠ {malicious} of {malicious + suspicious + harmless + undetected} engines flagged this file as MALICIOUS."
                            : suspicious > 0
                                ? $"⚠ {suspicious} engine(s) flagged this file as suspicious (0 as outright malicious)."
                                : "No engines flagged this file as malicious.";

                        string report = $"VirusTotal analysis complete.\n{verdict}\n\n" +
                                        $"Malicious: {malicious}  Suspicious: {suspicious}  " +
                                        $"Harmless: {harmless}  Undetected: {undetected}";
                        return (true, malicious > 0, report);
                    }
                }
                catch { /* keep polling until deadline */ }
            }

            return (true, false, "Uploaded successfully, but analysis did not complete within the wait time. " +
                                 "Check the file's hash on virustotal.com directly in a few minutes.");
        }

        private static (bool Success, bool Found, bool Malicious, string Output) ParseVirusTotalFileReport(string hash, string json)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement attrs = doc.RootElement.GetProperty("data").GetProperty("attributes");
                var (malicious, suspicious, harmless, undetected) = ExtractStats(attrs.GetProperty("last_analysis_stats"));

                string fileName = attrs.TryGetProperty("meaningful_name", out var nameEl)
                    ? nameEl.GetString() ?? "(unknown)"
                    : "(unknown)";

                string verdict = malicious > 0
                    ? $"⚠ {malicious} of {malicious + suspicious + harmless + undetected} engines flag this file as MALICIOUS."
                    : suspicious > 0
                        ? $"⚠ {suspicious} engine(s) flag this file as suspicious (0 as outright malicious)."
                        : "No engines flag this file as malicious.";

                string output = $"SHA-256: {hash}\nKnown as: {fileName}\n\n{verdict}\n\n" +
                                $"Malicious: {malicious}  Suspicious: {suspicious}  " +
                                $"Harmless: {harmless}  Undetected: {undetected}";

                return (true, true, malicious > 0, output);
            }
            catch (Exception ex)
            {
                return (false, false, false, $"Could not parse VirusTotal response: {ex.Message}");
            }
        }

        private static (int Malicious, int Suspicious, int Harmless, int Undetected) ExtractStats(JsonElement stats)
        {
            int Get(string name) => stats.TryGetProperty(name, out var v) ? v.GetInt32() : 0;
            return (Get("malicious"), Get("suspicious"), Get("harmless"), Get("undetected"));
        }
    }
}
