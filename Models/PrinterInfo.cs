namespace ITHelpdeskToolkit.Models
{
    public class PrinterInfo
    {
        public string Name { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Driver { get; set; } = string.Empty;
        public string Port { get; set; } = string.Empty;
    }
}
