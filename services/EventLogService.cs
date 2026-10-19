using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Threading.Tasks;
using ITHelpdeskToolkit.Models;

namespace ITHelpdeskToolkit.Services
{
    public static class EventLogService
    {
        // Standard Windows event levels (System.Diagnostics.Eventing.Reader / wevtapi):
        // 1 = Critical, 2 = Error, 3 = Warning, 4 = Information, 5 = Verbose.
        private const int LevelCritical = 1;
        private const int LevelError = 2;

        /// <summary>
        /// Returns the most recent <paramref name="count"/> entries from the Windows "Application"
        /// event log, newest first, at any level (Information, Warning, Error, Critical).
        /// </summary>
        public static Task<List<EventLogEntryInfo>> GetTopApplicationLogsAsync(int count = 10)
            => GetTopLogsAsync("Application", count, errorsOnly: false);

        /// <summary>
        /// Returns the most recent <paramref name="count"/> Error/Critical entries from the Windows
        /// "System" event log, newest first.
        /// </summary>
        public static Task<List<EventLogEntryInfo>> GetTopSystemErrorLogsAsync(int count = 10)
            => GetTopLogsAsync("System", count, errorsOnly: true);

        private static Task<List<EventLogEntryInfo>> GetTopLogsAsync(string logName, int count, bool errorsOnly)
        {
            return Task.Run(() =>
            {
                var results = new List<EventLogEntryInfo>();

                string xpath = errorsOnly
                    ? $"*[System[(Level={LevelCritical} or Level={LevelError})]]"
                    : "*";

                EventLogQuery query = new(logName, PathType.LogName, xpath)
                {
                    ReverseDirection = true // newest first
                };

                try
                {
                    using EventLogReader reader = new(query);

                    for (int i = 0; i < count; i++)
                    {
                        using EventRecord? record = reader.ReadEvent();
                        if (record == null) break; // fewer than `count` events exist - that's fine

                        results.Add(new EventLogEntryInfo
                        {
                            TimeCreated = record.TimeCreated,
                            Level = record.LevelDisplayName ?? "Unknown",
                            Source = record.ProviderName ?? "Unknown",
                            EventId = record.Id,
                            Message = SafeFormatMessage(record),
                            LogName = logName
                        });
                    }
                }
                catch (EventLogNotFoundException)
                {
                    // The named log doesn't exist on this machine - return whatever was gathered
                    // (normally empty) instead of throwing, since this feeds a UI list.
                }
                catch (UnauthorizedAccessException)
                {
                    // Caller doesn't have permission to read this log (most likely on the Security
                    // log, which needs elevation). Application and System are readable by any
                    // authenticated user, so this should be rare for the two logs used above.
                }

                return results;
            });
        }

        private static string SafeFormatMessage(EventRecord record)
        {
            try
            {
                // FormatDescription() resolves the message template from the provider's registered
                // manifest. It can throw if that manifest isn't installed locally (common for
                // third-party or remote-sourced events), so fall back to a minimal description.
                return record.FormatDescription() ?? $"(Event {record.Id} from {record.ProviderName})";
            }
            catch
            {
                return $"(Event {record.Id} from {record.ProviderName} - description unavailable)";
            }
        }
    }
}
