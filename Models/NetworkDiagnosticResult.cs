namespace ITHelpdeskToolkit.Models
{
    public class NetworkDiagnosticResult
    {
        public string TestName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Output { get; set; } = string.Empty;
        public string LikelyProblem { get; set; } = string.Empty;
    }
}
