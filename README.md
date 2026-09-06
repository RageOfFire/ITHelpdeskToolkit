# IT Helpdesk Toolkit

![GitHub all releases](https://img.shields.io/github/downloads/RageOfFire/ITHelpdeskToolkit/total)
![Discord](https://img.shields.io/discord/752171524919918672)
![GitHub commit activity](https://img.shields.io/github/commit-activity/m/RageOfFire/ITHelpdeskToolkit)
![GitHub last commit](https://img.shields.io/github/last-commit/RageOfFire/ITHelpdeskToolkit)
![GitHub followers](https://img.shields.io/github/followers/RageOfFire)
![GitHub Repo stars](https://img.shields.io/github/stars/RageOfFire/ITHelpdeskToolkit)

A single Windows desktop application for common endpoint support tasks —
one WinForms app, no per-tool scripts to hunt down. Built in C# on .NET 8.

The current version is tracked in one place: [`AppInfo.cs`](AppInfo.cs).

## Tools included

- **Dashboard** — quick-launch buttons and at-a-glance PC info
- **Asset Inventory** — hardware, OS, disk, and network info (including a
  dedicated Wi-Fi adapter MAC address lookup); export to CSV or a text report
- **Network Diagnostics** — adapter, gateway, DNS, internet and route checks
- **Network Repair** — DNS flush, DHCP release/renew, Winsock/TCP-IP reset,
  adapter restart
- **Windows / File System Repair** — SFC, DISM (check/scan/restore health),
  CHKDSK, disk health
- **System Cleanup** — temp files, Prefetch, browser caches, Recycle Bin,
  Windows Update cleanup
- **Excel Troubleshooter** — clears lock files, disables crash-causing
  add-ins, resets the ribbon/toolbar, Office Quick Repair
- **Application Fixes** — general fixes (Explorer/icon cache/Store
  apps/fonts/search) plus tools targeting one named app (force-close, clear
  cache, open data folder, launch as admin)
- **Printer Troubleshooter** — printer/spooler status, connectivity check,
  clear stuck jobs, restart spooler
- **Password Generator** — configurable random password generation
- **Device Telemetry** — collects hardware/OS/network telemetry and POSTs it
  to a configurable corporate telemetry endpoint (see
  [Telemetry API](#telemetry-api) below)

Most repair actions run in a background thread so the UI stays responsive,
and elevate via a UAC prompt automatically when administrator rights are
needed.

## Telemetry API

`TelemetryService` collects a snapshot of the local machine's hardware,
OS, and network state and sends it as JSON to a corporate telemetry
endpoint that you configure. The service does not send anything
automatically or on a schedule — it only runs when the app calls
`TelemetryService.CollectAsync()` / `SendTelemetryAsync()`.

### Endpoint

```
POST {baseUrl}/api/client-telemetry/collect
Content-Type: application/json
```

- `baseUrl` is whatever host/base URL is configured in the app
  (e.g. `https://helpdesk.example.com`). `TelemetryService.BuildEndpointUrl`
  appends `/api/client-telemetry/collect` automatically unless the
  configured URL already contains that path.
- No authentication is added by the client — if your endpoint requires
  auth, put it behind a reverse proxy/VPN or extend `TelemetryService`
  to add the appropriate headers.
- Request timeout is 20 seconds.

### Request body

All fields are sent as a single JSON object (camelCase), built by
`TelemetryService.CollectAsync()` from `Models/TelemetryPayload.cs`:

| Field | Type | Description |
|---|---|---|
| `os` | string | OS caption, e.g. `Windows 11 Pro` |
| `platform` | string | Always `Win32` |
| `cpuModel` | string | CPU name string |
| `cpuCores` | int | Physical core count (approximated as `logicalCpus / 2`) |
| `cpuUsagePercent` | double | Current CPU load percentage |
| `totalRamGb` | double | Total physical RAM, GB |
| `freeRamGb` | double | Free physical RAM, GB |
| `memoryUsagePercent` | double | `(1 - free/total) * 100`, rounded to 1 decimal |
| `gpuRenderer` | string | Primary GPU/video controller name |
| `diskTotalGb` | double | Total size of the system drive, GB |
| `diskFreeGb` | double | Free space on the system drive, GB |
| `batteryLevel` | int? | Battery charge percentage, or `null` if no battery |
| `isCharging` | bool? | Whether AC power is connected, or `null` if no battery |
| `ipAddress` | string | Local IP address |
| `networkType` | string | `WI-FI`, `ETHERNET`, `GIGABIT ETHERNET`, or the raw adapter type; `UNKNOWN` if none found |
| `onlineStatus` | bool | Whether the OS reports network availability |
| `collectedVia` | string | Always `client_hardware_agent_api` |
| `agentVersion` | string | e.g. `v1.0.0-ithelpdesk-toolkit`, from [`AppInfo.cs`](AppInfo.cs) |

Example payload:

```json
{
  "os": "Windows 11 Pro",
  "platform": "Win32",
  "cpuModel": "Intel(R) Core(TM) i7-9700 CPU @ 3.00GHz",
  "cpuCores": 4,
  "cpuUsagePercent": 12.5,
  "totalRamGb": 16.0,
  "freeRamGb": 6.42,
  "memoryUsagePercent": 59.9,
  "gpuRenderer": "Intel(R) UHD Graphics 630",
  "diskTotalGb": 476.94,
  "diskFreeGb": 128.11,
  "batteryLevel": null,
  "isCharging": null,
  "ipAddress": "192.168.1.42",
  "networkType": "GIGABIT ETHERNET",
  "onlineStatus": true,
  "collectedVia": "client_hardware_agent_api",
  "agentVersion": "v1.0.0-ithelpdesk-toolkit"
}
```

### Response handling

`SendTelemetryAsync` returns `(bool Success, string Detail)`:

- **Success** — any 2xx response. `Detail` contains the HTTP status line
  and the raw response body (shown in the app's log console).
- **Failure** — any non-2xx response, a request timeout (20s), a network
  error, or any other exception. `Detail` contains a human-readable
  reason. The app does not retry automatically.

The client does not require any particular response schema — the raw
body is only displayed for troubleshooting, so your server can return
whatever it likes on success (`200 OK` with an empty body is fine).

### Using it from code

```csharp
TelemetryPayload payload = await TelemetryService.CollectAsync();
(bool success, string detail) = await TelemetryService.SendTelemetryAsync(
    payload,
    baseUrl: "https://helpdesk.example.com");
```

`TelemetryService.ToJsonPreview(payload)` returns a pretty-printed JSON
string of the payload, useful for previewing what will be sent before
hitting "Send".

## Interface

- **Collapsible sidebar** — toggle it from the `☰` button in the top bar to
  reclaim horizontal space
- **Light/Dark theme** — toggle from the top bar; the whole UI rebuilds
  itself against the new palette immediately
- Sidebar and content panel use explicit, resize-safe layout rather than
  relying on WinForms `Dock` resolution alone, so the shell stays correct
  across window sizes and theme switches

## Requirements

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) for
  development/building
- Administrator rights for actions that need them (the app will prompt via
  UAC automatically)

## Project structure

```text
Program.cs                  # entry point
AppInfo.cs                  # single source of truth for the app version
ITHelpdeskToolkit.csproj    # project file (net8.0-windows, WinForms)

Models/                     # plain data types shared across services/views
    InventoryItem.cs
    NetworkDiagnosticResult.cs
    PrinterDriverInfo.cs
    PrinterInfo.cs
    RepairActionItem.cs
    TelemetryPayload.cs        # JSON shape POSTed to the telemetry API

services/                   # backend logic — no UI dependency, pure Windows calls
    ShellService.cs            # run_command / run_powershell / elevation helpers
    SystemRepairService.cs     # SFC, DISM, CHKDSK, network stack repair
    CleanupService.cs          # temp/cache/Recycle Bin/Windows Update cleanup
    InventoryService.cs        # hardware/OS/disk/network inventory collection
    NetworkService.cs          # connectivity diagnostic checks, gateway lookup
    PrinterService.cs          # printer/spooler/job queries
    ExcelRepairService.cs      # Excel-specific fixes
    AppRepairService.cs        # general app fixes + targeted-app tools
    PasswordService.cs         # password generation
    TelemetryService.cs        # collects + POSTs device telemetry (see Telemetry API)

UI/
    MainForm.cs              # window shell: sidebar, top bar, view host, theming
    Theme/Colors.cs           # switchable Dark/Light palette (DarkColors)
    Controls/                 # shared custom controls (NavButton, ModernButton, CardPanel)
    Views/                    # one UserControl per sidebar page
        DashboardView.cs, InventoryView.cs, NetworkView.cs,
        NetworkRepairView.cs, SystemRepairView.cs, CleanupView.cs,
        ExcelView.cs, AppRepairView.cs, PrinterView.cs, PasswordView.cs

Assets/
    ITHelpdeskToolkit.ico    # app icon (embedded via <ApplicationIcon> in the .csproj)
```

Each `services/*.cs` class is plain C# with no UI dependency, so the
repair/diagnostic logic can be tested or reused on its own. Each
`UI/Views/*.cs` class is a `UserControl` for one sidebar page;
`MainForm.cs` builds the shell (sidebar, top bar, status strip) and swaps
the active view into a dedicated host panel via `NavigateTo(pageId)`.

## Run during development

```bat
dotnet restore
dotnet run
```

## Build a release EXE

```bat
dotnet clean
dotnet build -c Release
```

Or for a single self-contained executable:

```bat
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The output will be under:

```text
bin\Release\net8.0-windows\win-x64\publish\ITHelpdeskToolkit.exe
```

## Notes

- Some actions (SFC, DISM, CHKDSK repair, Winsock/TCP-IP reset, Windows
  Update cleanup, font cache reset, network search restart) require
  administrator privileges and will trigger a UAC prompt.
- The application is intended for authorized IT support and
  asset-management use.
