using System;

namespace ITHelpdeskToolkit.Models
{
    public class EventLogEntryInfo
    {
        public DateTime? TimeCreated { get; set; }
        public string Level { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public int EventId { get; set; }
        public string Message { get; set; } = string.Empty;
        public string LogName { get; set; } = string.Empty;
    }
}
