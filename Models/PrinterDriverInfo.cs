namespace ITHelpdeskToolkit.Models
{
    public class PrinterDriverInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string DriverPath { get; set; } = string.Empty;
        public bool InUse { get; set; }
    }
}
