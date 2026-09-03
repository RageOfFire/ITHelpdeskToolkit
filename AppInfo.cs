namespace ITHelpdeskToolkit
{
    /// <summary>
    /// Single source of truth for the app's version number. Change VERSION
    /// here and every place it's shown (title bar, sidebar badge, exported
    /// report headers, etc.) updates automatically — no need to hunt through
    /// multiple files.
    /// </summary>
    public static class AppInfo
    {
        public const string Version = "4.1.0";

        /// <summary>
        /// Base URL for the corporate telemetry API
        /// (POST {TelemetryEndpoint}/api/client-telemetry/collect).
        /// Change this here and rebuild — it's not editable from the UI so
        /// every deployed copy always reports to the same endpoint.
        /// </summary>
        public const string TelemetryEndpoint = "https://helpdesk.company.local";
    }
}
