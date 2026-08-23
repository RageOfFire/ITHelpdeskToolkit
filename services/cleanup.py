"""Fixes for freeing up disk space and clearing accumulated junk.

Everything here only touches Cache/Temp-style folders — never user
documents or app settings — except Windows Update Cleanup, which uses
DISM's own component-store cleanup (the same thing Disk Cleanup's
"Windows Update Cleanup" checkbox runs) and needs admin rights.
"""

import os
import subprocess
import tempfile

from .shell import run_powershell, run_repair_command


def cleanup_temp_files():
    """Remove files from the current user's temporary folders."""
    removed = 0
    failed = 0
    locations = [tempfile.gettempdir(), os.environ.get("TEMP", "")]
    seen = set()

    for location in locations:
        if not location:
            continue
        location = os.path.abspath(location)
        if location in seen or not os.path.isdir(location):
            continue
        seen.add(location)

        for root, dirs, files in os.walk(location, topdown=False):
            for name in files:
                path = os.path.join(root, name)
                try:
                    os.remove(path)
                    removed += 1
                except OSError:
                    failed += 1
            for name in dirs:
                path = os.path.join(root, name)
                try:
                    os.rmdir(path)
                except OSError:
                    pass

    return True, f"Temporary cleanup complete. Removed: {removed} file(s). Skipped/locked: {failed}."


def clear_prefetch():
    """Remove Windows Prefetch cache files (rebuilt automatically as needed)."""
    prefetch = os.path.join(os.environ.get("SystemRoot", r"C:\Windows"), "Prefetch")
    if not os.path.isdir(prefetch):
        return True, "Prefetch folder not found — nothing to clear."

    removed = 0
    failed = 0
    try:
        entries = os.listdir(prefetch)
    except OSError as exc:
        return False, str(exc)

    for name in entries:
        path = os.path.join(prefetch, name)
        try:
            os.remove(path)
            removed += 1
        except OSError:
            failed += 1

    note = ""
    if failed and removed == 0:
        note = " (may need administrator rights to clear this folder)"
    return True, f"Removed {removed} prefetch file(s). Skipped/locked: {failed}.{note}"


def clear_browser_caches():
    """Clear cache folders for Chrome, Edge and Firefox for the current user only.

    Only removes Cache/cache2 subfolders — bookmarks, history, passwords
    and open tabs are untouched.
    """
    localappdata = os.environ.get("LOCALAPPDATA", "")
    targets = []

    if localappdata:
        targets.append(os.path.join(localappdata, "Google", "Chrome", "User Data", "Default", "Cache"))
        targets.append(os.path.join(localappdata, "Microsoft", "Edge", "User Data", "Default", "Cache"))

        firefox_profiles = os.path.join(localappdata, "Mozilla", "Firefox", "Profiles")
        if os.path.isdir(firefox_profiles):
            try:
                for profile in os.listdir(firefox_profiles):
                    targets.append(os.path.join(firefox_profiles, profile, "cache2"))
            except OSError:
                pass

    removed = 0
    failed = 0
    cleared = []

    for target in targets:
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
        return True, "No Chrome, Edge or Firefox cache folders were found for the current user."

    return True, (
        f"Cleared browser cache. Removed {removed} file(s), skipped {failed} locked file(s).\n\n"
        "Folders cleared:\n" + "\n".join(cleared)
    )


def empty_recycle_bin():
    return run_powershell(
        "Clear-RecycleBin -Force -ErrorAction SilentlyContinue", timeout=120
    )


def windows_update_cleanup():
    """Free space used by superseded Windows Update files (DISM component cleanup)."""
    return run_repair_command(
        "DISM /Online /Cleanup-Image /StartComponentCleanup /ResetBase",
        timeout=1200,
        admin=True,
    )


def open_disk_cleanup():
    """Launch the built-in Windows Disk Cleanup utility for manual review."""
    try:
        subprocess.Popen(
            ["cleanmgr.exe"],
            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
        )
        return True, "Disk Cleanup launched."
    except Exception as exc:
        return False, str(exc)


def get_disk_space_summary():
    """Return a one-line free/used space summary for the system drive."""
    drive = os.environ.get("SystemDrive", "C:")
    try:
        import shutil as _shutil
        stat = _shutil.disk_usage(drive + "\\")
        total_gb = round(stat.total / (1024 ** 3), 2)
        used_gb = round((stat.total - stat.free) / (1024 ** 3), 2)
        free_gb = round(stat.free / (1024 ** 3), 2)
        percent_used = round((stat.total - stat.free) / stat.total * 100, 1)
        return f"{drive}  Total: {total_gb} GB   Used: {used_gb} GB ({percent_used}%)   Free: {free_gb} GB"
    except Exception as exc:
        return f"Unable to read disk usage: {exc}"


# (label, description, function, requires_admin)
CLEANUP_ACTIONS = [
    (
        "Clear TEMP Files",
        "Remove files from the current user's temporary folders.",
        cleanup_temp_files,
        False,
    ),
    (
        "Clear Prefetch",
        "Remove Windows Prefetch cache files (Windows rebuilds these automatically).",
        clear_prefetch,
        False,
    ),
    (
        "Clear Browser Caches",
        "Clear Chrome, Edge and Firefox cache for the current user (history/logins untouched).",
        clear_browser_caches,
        False,
    ),
    (
        "Empty Recycle Bin",
        "Empty the Windows Recycle Bin.",
        empty_recycle_bin,
        False,
    ),
    (
        "Windows Update Cleanup",
        "Free space used by superseded Windows Update files — often the "
        "single biggest space recovery on an old install.",
        windows_update_cleanup,
        True,
    ),
]
