"""Fixes for the most common "an application won't behave" problems.

Two kinds of fix live here:

- GENERAL_APP_FIXES: system-wide fixes that aren't tied to one app —
  a frozen taskbar, broken icons, Store apps that won't open, garbled
  fonts, Start menu search not finding things.
- The targeted functions (force_close_app, clear_app_cache,
  open_app_data_folder, launch_as_admin) act on one app the user names,
  for when the problem is specific to that program.
"""

import os

from .shell import kill_process, run_command, run_powershell, run_repair_command
from .system_repair import restart_explorer


def clear_icon_cache():
    """Fix wrong/missing icons on the desktop and taskbar."""
    run_command("taskkill /f /im explorer.exe", timeout=30)

    localappdata = os.environ.get("LOCALAPPDATA", "")
    removed = 0
    failed = 0
    candidates = []
    if localappdata:
        candidates.append(os.path.join(localappdata, "IconCache.db"))
        explorer_cache_dir = os.path.join(localappdata, "Microsoft", "Windows", "Explorer")
        if os.path.isdir(explorer_cache_dir):
            for name in os.listdir(explorer_cache_dir):
                lower = name.lower()
                if lower.startswith("iconcache") or lower.startswith("thumbcache"):
                    candidates.append(os.path.join(explorer_cache_dir, name))

    for path in candidates:
        if os.path.exists(path):
            try:
                os.remove(path)
                removed += 1
            except OSError:
                failed += 1

    run_command("start explorer.exe", timeout=10)
    return True, f"Removed {removed} icon/thumbnail cache file(s). Skipped/locked: {failed}. Explorer restarted."


def reset_store_apps():
    """Fix built-in Store/UWP apps (Mail, Photos, Settings, ...) that won't open."""
    return run_command("wsreset.exe", timeout=60)


def reregister_store_apps():
    """Fix 'This app can't open' errors for built-in Windows apps."""
    return run_powershell(
        "Get-AppXPackage | Foreach-Object "
        '{Add-AppxPackage -DisableDevelopmentMode -Register "$($_.InstallLocation)\\AppXManifest.xml" '
        "-ErrorAction SilentlyContinue}",
        timeout=600,
    )


def clear_font_cache():
    """Fix garbled or missing text rendering across applications."""
    command = (
        "net stop FontCache & "
        'del /f /q "%WinDir%\\ServiceProfiles\\LocalService\\AppData\\Local\\FontCache\\*.dat" & '
        "net start FontCache"
    )
    return run_repair_command(command, timeout=60, admin=True)


def restart_search():
    """Fix Start menu search not finding installed apps or files."""
    return run_repair_command("net stop WSearch & net start WSearch", timeout=60, admin=True)


# (label, description, function, requires_admin)
GENERAL_APP_FIXES = [
    (
        "Restart Windows Explorer",
        "Fix a frozen taskbar, Start menu, or missing desktop icons.",
        restart_explorer,
        False,
    ),
    (
        "Rebuild Icon Cache",
        "Fix wrong or missing icons on the desktop and taskbar.",
        clear_icon_cache,
        False,
    ),
    (
        "Reset Windows Store Apps",
        "Fix Store/UWP apps (Mail, Photos, Settings, etc.) that won't open.",
        reset_store_apps,
        False,
    ),
    (
        "Re-register Store Apps",
        "Fix \"This app can't open\" errors for built-in Windows apps.",
        reregister_store_apps,
        False,
    ),
    (
        "Clear Font Cache",
        "Fix garbled or missing text rendering across applications.",
        clear_font_cache,
        True,
    ),
    (
        "Restart Windows Search",
        "Fix Start menu search not finding installed apps or files.",
        restart_search,
        True,
    ),
]


# --------------------------------------------------------------------
# Tools that target one specific app the user names
# --------------------------------------------------------------------

def force_close_app(process_name):
    if not process_name.strip():
        return False, "Enter a process name first, e.g. 'chrome.exe'."
    return kill_process(process_name)


def find_app_data_folders(app_name):
    """Best-effort guess at where an app's settings/cache folders live."""
    localappdata = os.environ.get("LOCALAPPDATA", "")
    appdata = os.environ.get("APPDATA", "")
    found = []
    for base in (localappdata, appdata):
        if not base or not os.path.isdir(base):
            continue
        try:
            entries = os.listdir(base)
        except OSError:
            continue
        for name in entries:
            if app_name.lower() in name.lower():
                found.append(os.path.join(base, name))
    return found


def clear_app_cache(app_name):
    """Clear an app's Cache/Temp/Logs subfolders without touching its settings."""
    if not app_name.strip():
        return False, "Enter an application name first, e.g. 'Spotify' or 'Slack'."

    folders = [f for f in find_app_data_folders(app_name) if os.path.isdir(f)]
    if not folders:
        return False, f"No AppData folder matching '{app_name}' was found."

    removed = 0
    failed = 0
    cleared = []
    cache_subfolder_names = ("Cache", "Cache2", "GPUCache", "Code Cache", "Temp", "temp", "Logs")

    for folder in folders:
        for sub in cache_subfolder_names:
            target = os.path.join(folder, sub)
            if not os.path.isdir(target):
                continue
            for root, _dirs, files in os.walk(target, topdown=False):
                for name in files:
                    try:
                        os.remove(os.path.join(root, name))
                        removed += 1
                    except OSError:
                        failed += 1
            cleared.append(target)

    if not cleared:
        return True, (
            f"Found '{app_name}' data at:\n" + "\n".join(folders) +
            "\n\nNo obvious cache subfolder was found, so nothing was removed "
            "(this avoids deleting settings/login data)."
        )

    return True, (
        f"Cleared cache for '{app_name}'. Removed {removed} file(s), skipped {failed} locked file(s).\n\n"
        "Folders cleared:\n" + "\n".join(cleared)
    )


def open_app_data_folder(app_name):
    if not app_name.strip():
        return False, "Enter an application name first."
    folders = find_app_data_folders(app_name)
    if not folders:
        return False, f"No AppData folder matching '{app_name}' was found."
    try:
        os.startfile(folders[0])
        return True, f"Opened: {folders[0]}"
    except Exception as exc:
        return False, str(exc)


def launch_as_admin(exe_path):
    if not exe_path:
        return False, "Choose an application (.exe) first."
    if not os.path.exists(exe_path):
        return False, "That file does not exist."
    safe_path = exe_path.replace('"', '""')
    return run_powershell(f'Start-Process -FilePath "{safe_path}" -Verb RunAs', timeout=15)
