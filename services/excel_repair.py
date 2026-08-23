"""Fixes for the most common Microsoft Excel problems.

Every function returns (success: bool, message: str) so it can be
dropped straight into EXCEL_FIXES and run uniformly by the UI layer.
All fixes only touch the current Windows user's profile — nothing here
requires administrator rights except the Office Quick Repair action.
"""

import os
import shutil
import subprocess
import tempfile
import winreg

from .shell import kill_process, run_command


def kill_excel():
    return kill_process("EXCEL.EXE")


def open_excel_safe_mode():
    try:
        subprocess.Popen(
            ["cmd", "/c", "start", "", "excel.exe", "/safe"],
            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
        )
        return True, "Excel Safe Mode launch requested."
    except Exception as exc:
        return False, str(exc)


def get_office_paths():
    paths = []
    candidates = [
        os.path.expandvars(r"%ProgramFiles%\Microsoft Office"),
        os.path.expandvars(r"%ProgramFiles(x86)%\Microsoft Office"),
        os.path.expandvars(r"%LocalAppData%\Microsoft\Office"),
        os.path.expandvars(r"%AppData%\Microsoft\Excel"),
    ]
    for path in candidates:
        if os.path.exists(path):
            paths.append(path)
    return paths


def get_common_app_paths():
    return {
        "TEMP": tempfile.gettempdir(),
        "APPDATA": os.environ.get("APPDATA", ""),
        "LOCALAPPDATA": os.environ.get("LOCALAPPDATA", ""),
        "XLSTART": os.path.join(
            os.environ.get("APPDATA", ""),
            "Microsoft", "Excel", "XLSTART"
        ),
    }


def _office_version_keys():
    """Return Office version strings found under HKCU\\...\\Office, e.g. ['16.0']."""
    versions = []
    try:
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, r"Software\Microsoft\Office") as key:
            index = 0
            while True:
                try:
                    sub = winreg.EnumKey(key, index)
                except OSError:
                    break
                index += 1
                if sub.replace(".", "").isdigit():
                    versions.append(sub)
    except OSError:
        pass
    return versions or ["16.0"]


def clear_excel_lock_files():
    """Remove leftover ~$*.xls* lock files left behind by a crashed Excel."""
    removed = 0
    failed = 0
    folders = set()
    home = os.path.expanduser("~")
    for sub in ("Documents", "Desktop", "Downloads"):
        folders.add(os.path.join(home, sub))
    folders.add(tempfile.gettempdir())

    for folder in folders:
        if not os.path.isdir(folder):
            continue
        try:
            entries = os.listdir(folder)
        except OSError:
            continue
        for name in entries:
            if name.startswith("~$") and name.lower().endswith((".xlsx", ".xls", ".xlsm", ".xlsb")):
                path = os.path.join(folder, name)
                try:
                    os.remove(path)
                    removed += 1
                except OSError:
                    failed += 1

    return True, f"Removed {removed} Excel lock file(s). Skipped/locked: {failed}."


def disable_com_addins():
    """Disable third-party COM add-ins — the #1 cause of Excel crashing on startup."""
    disabled = []
    roots = [r"Software\Microsoft\Office\Excel\Addins"]
    for version in _office_version_keys():
        roots.append(rf"Software\Microsoft\Office\{version}\Excel\Addins")

    for root in roots:
        try:
            key = winreg.OpenKey(winreg.HKEY_CURRENT_USER, root, 0, winreg.KEY_ALL_ACCESS)
        except OSError:
            continue

        with key:
            names = []
            index = 0
            while True:
                try:
                    names.append(winreg.EnumKey(key, index))
                except OSError:
                    break
                index += 1

            for name in names:
                try:
                    with winreg.OpenKey(key, name, 0, winreg.KEY_SET_VALUE) as sub_key:
                        winreg.SetValueEx(sub_key, "LoadBehavior", 0, winreg.REG_DWORD, 0)
                    disabled.append(name)
                except OSError:
                    continue

    if disabled:
        return True, "Disabled add-in(s): " + ", ".join(disabled)
    return True, "No enabled COM add-ins were found — nothing to disable."


def clear_excel_startup_addins():
    """Move XLSTART add-ins (.xlam/.xla/.xltm) out of Excel's auto-load folder."""
    xlstart = get_common_app_paths()["XLSTART"]
    if not xlstart or not os.path.isdir(xlstart):
        return True, "No XLSTART folder found — nothing to disable."

    backup = xlstart + "_disabled"
    os.makedirs(backup, exist_ok=True)

    moved = 0
    try:
        entries = os.listdir(xlstart)
    except OSError as exc:
        return False, str(exc)

    for name in entries:
        src = os.path.join(xlstart, name)
        if os.path.isfile(src):
            try:
                shutil.move(src, os.path.join(backup, name))
                moved += 1
            except OSError:
                continue

    if moved:
        return True, f"Moved {moved} startup add-in file(s) to:\n{backup}\n(restore them there if Excel needs them again)."
    return True, "XLSTART folder was already empty — nothing to disable."


def reset_excel_toolbar():
    """Reset Excel's customized Quick Access Toolbar / ribbon layout."""
    changed = []
    for version in _office_version_keys():
        path = rf"Software\Microsoft\Office\{version}\Excel\Options"
        try:
            winreg.DeleteKey(winreg.HKEY_CURRENT_USER, path)
            changed.append(version)
        except FileNotFoundError:
            continue
        except OSError:
            continue

    if changed:
        return True, f"Reset toolbar/ribbon customizations for Office version(s): {', '.join(changed)}."
    return True, "No customized toolbar/ribbon settings were found — nothing to reset."


def clear_excel_mru():
    """Clear Excel's recently-used file list."""
    cleared = []
    for version in _office_version_keys():
        path = rf"Software\Microsoft\Office\{version}\Excel\File MRU"
        try:
            winreg.DeleteKey(winreg.HKEY_CURRENT_USER, path)
            cleared.append(version)
        except FileNotFoundError:
            continue
        except OSError:
            continue

    if cleared:
        return True, f"Cleared the recent files list for Office version(s): {', '.join(cleared)}."
    return True, "No recent files list entries were found — nothing to clear."


def repair_office_quick():
    """Run Microsoft's own Quick Repair (Click-to-Run installs only)."""
    click_to_run = os.path.expandvars(
        r"%CommonProgramFiles%\Microsoft Shared\ClickToRun\OfficeClickToRun.exe"
    )
    if not os.path.exists(click_to_run):
        return False, (
            "Office Click-to-Run was not found on this PC. Quick Repair only "
            "works for Microsoft 365 / Click-to-Run installs — for an MSI "
            "install, use Control Panel > Programs > Change instead."
        )

    command = (
        f'"{click_to_run}" scenario=Repair platform=x64 culture=en-us '
        f'RepairType=QuickRepair DisplayLevel=True'
    )
    return run_command(command, timeout=300)


# (label, description, function, requires_admin)
EXCEL_FIXES = [
    (
        "Close Stuck Excel Process",
        "Force-close any unresponsive EXCEL.EXE process.",
        kill_excel,
        False,
    ),
    (
        "Clear Lock Files",
        "Remove leftover ~$ lock files from Documents, Desktop, Downloads and TEMP "
        "that block a file from reopening after a crash.",
        clear_excel_lock_files,
        False,
    ),
    (
        "Disable COM Add-ins",
        "Disable third-party COM add-ins — the most common cause of Excel "
        "crashing or freezing on startup.",
        disable_com_addins,
        False,
    ),
    (
        "Disable Startup (XLSTART) Add-ins",
        "Move files out of the XLSTART auto-load folder so they stop loading "
        "automatically with Excel.",
        clear_excel_startup_addins,
        False,
    ),
    (
        "Reset Toolbar/Ribbon",
        "Reset a corrupted or broken Quick Access Toolbar / ribbon layout back "
        "to defaults.",
        reset_excel_toolbar,
        False,
    ),
    (
        "Clear Recent Files List",
        "Clear Excel's recently-used file list (useful when it references "
        "missing/renamed files and slows down startup).",
        clear_excel_mru,
        False,
    ),
    (
        "Quick Repair Office",
        "Run Microsoft's built-in Quick Repair to fix corrupted Office/Excel "
        "program files (Microsoft 365 / Click-to-Run installs only).",
        repair_office_quick,
        True,
    ),
]
