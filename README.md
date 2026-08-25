# IT Helpdesk Toolkit v3.1

![GitHub all releases](https://img.shields.io/github/downloads/RageOfFire/ITHelpdeskToolkit/total)
![Discord](https://img.shields.io/discord/752171524919918672)
![GitHub commit activity](https://img.shields.io/github/commit-activity/m/RageOfFire/ITHelpdeskToolkit)
![GitHub last commit](https://img.shields.io/github/last-commit/RageOfFire/ITHelpdeskToolkit)
![GitHub followers](https://img.shields.io/github/followers/RageOfFire)
![GitHub Repo stars](https://img.shields.io/github/stars/RageOfFire/ITHelpdeskToolkit)

A single Windows desktop application for common endpoint support tasks —
one Tkinter app, no per-tool scripts to hunt down.

## Tools included

- **Dashboard** — quick-launch buttons and at-a-glance PC info
- **Asset Inventory** — hardware, OS, disk and network info; export to CSV/report
- **Network Diagnostics** — adapter, gateway, DNS, internet and route checks with plain-language "likely problem" output
- **Network Repair** — DNS flush, DHCP release/renew, Winsock/TCP-IP reset, adapter restart
- **Windows / File System Repair** — SFC, DISM (check/scan/restore health), CHKDSK, disk health
- **System Cleanup** — temp files, Prefetch, browser caches, Recycle Bin, Windows Update cleanup
- **Excel Troubleshooter** — clears lock files, disables crash-causing add-ins, resets the ribbon/toolbar, Office Quick Repair
- **Application Fixes** — general fixes (Explorer/icon cache/Store apps/fonts/search) plus tools targeting one named app (force-close, clear cache, open data folder, launch as admin)
- **Printer Troubleshooter** — printer/spooler status, connectivity check, clear stuck jobs, restart spooler
- **Password Generator** — configurable random password generation

Most repair actions run in a background thread so the UI stays responsive,
and elevate via a UAC prompt automatically when administrator rights are
needed.

## Requirements

- Windows 10/11
- Python 3.10+ for development
- Internet access only when installing packages

## Project structure

```text
main.py                    # entry point — this is what PyInstaller builds
app.py                      # window shell, sidebar nav, shared style/layout
constants.py
services/                  # backend logic — no tkinter, pure Windows calls
    shell.py                 # run_command / run_powershell / elevation helpers
    system_repair.py         # SFC, DISM, CHKDSK, network stack repair
    cleanup.py                # temp/cache/Recycle Bin/Windows Update cleanup
    inventory.py              # hardware/OS/disk/network inventory collection
    network.py                 # connectivity diagnostic checks
    printer.py                  # printer/spooler/job queries
    excel_repair.py              # Excel-specific fixes
    app_repair.py                 # general app fixes + targeted-app tools
pages/                      # UI, one module per sidebar section (mixins)
    dashboard.py, inventory.py, network.py, network_repair.py,
    system_repair.py, cleanup.py, excel.py, apps.py, printer.py,
    password.py, placeholders.py
```

Each `services/*.py` module is plain Python with no UI dependency, so the
repair/diagnostic logic can be tested or reused on its own. Each
`pages/*.py` module is a mixin class contributing one `show_<page>` method
plus its supporting handlers; `app.py` combines all of them into the
`HelpdeskToolkit` window.

## Run during development

```bat
python -m pip install -r requirements.txt
python main.py
```

## Build one EXE

Double-click `build.bat`, or run manually:

```bat
python -m pip install -r requirements.txt
pyinstaller --onefile --windowed --name ITHelpdeskToolkit main.py
```

The final executable will be:

```text
dist\ITHelpdeskToolkit.exe
```

PyInstaller follows the imports from `main.py` through `app.py` and every
`services/`/`pages/` module automatically — the multi-file source layout
still produces a single .exe.

## Notes

- Some actions (SFC, DISM, CHKDSK repair, Winsock/TCP-IP reset, Windows
  Update cleanup, font cache reset, network search restart) require
  administrator privileges and will trigger a UAC prompt.
- The application is intended for authorized IT support and
  asset-management use.
