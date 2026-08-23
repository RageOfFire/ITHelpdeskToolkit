"""System-integrity and network-stack repair actions.

These back the "Windows / File System Repair" and "Network Repair"
pages. Most require administrator privileges, handled transparently by
run_repair_command via UAC elevation. Disk-cleanup actions live in
cleanup.py instead.
"""

import os

from .shell import run_command, run_powershell, run_repair_command


def repair_sfc():
    return run_repair_command("sfc /scannow", timeout=900, admin=True)


def repair_dism_check():
    return run_repair_command(
        "DISM /Online /Cleanup-Image /CheckHealth", timeout=300, admin=True
    )


def repair_dism_scan():
    return run_repair_command(
        "DISM /Online /Cleanup-Image /ScanHealth", timeout=600, admin=True
    )


def repair_dism_restore():
    return run_repair_command(
        "DISM /Online /Cleanup-Image /RestoreHealth", timeout=1200, admin=True
    )


def repair_chkdsk():
    drive = os.environ.get("SystemDrive", "C:")
    return run_repair_command(
        f"chkdsk {drive} /scan", timeout=600, admin=True
    )


def schedule_chkdsk_fix():
    """Schedule a full chkdsk /f /r repair pass for the next restart.

    The system drive can't be locked while Windows is running, so chkdsk
    can only *schedule* the fix — it needs a reboot to actually run.
    """
    drive = os.environ.get("SystemDrive", "C:")
    return run_repair_command(
        f"echo Y| chkdsk {drive} /f /r", timeout=60, admin=True
    )


def check_disk_health():
    """Report SMART/reliability health status for each physical disk."""
    return run_powershell(
        "Get-PhysicalDisk | "
        "Select-Object DeviceId,FriendlyName,MediaType,HealthStatus,OperationalStatus | "
        "Format-Table -AutoSize | Out-String -Width 200",
        timeout=30,
    )


def flush_dns():
    return run_command("ipconfig /flushdns", timeout=60)


def release_ip():
    return run_command("ipconfig /release", timeout=60)


def renew_ip():
    return run_command("ipconfig /renew", timeout=120)


def reset_winsock():
    return run_repair_command("netsh winsock reset", timeout=120, admin=True)


def reset_tcpip():
    return run_repair_command("netsh int ip reset", timeout=120, admin=True)


def restart_network_adapters():
    ps_command = (
        "Get-NetAdapter | Where-Object {$_.Status -eq 'Up'} | "
        "Restart-NetAdapter -Confirm:$false"
    )
    wrapped = f'powershell -NoProfile -ExecutionPolicy Bypass -Command "{ps_command}"'
    return run_repair_command(wrapped, timeout=60, admin=True)


def restart_explorer():
    return run_command(
        'taskkill /f /im explorer.exe && start explorer.exe', timeout=60
    )


SYSTEM_REPAIR_ACTIONS = [
    ("SFC /scannow", "Repair protected Windows system files.", repair_sfc, True),
    ("DISM CheckHealth", "Quick component-store health check.", repair_dism_check, True),
    ("DISM ScanHealth", "Deep component-store health scan.", repair_dism_scan, True),
    ("DISM RestoreHealth", "Repair the Windows component store.", repair_dism_restore, True),
    ("CHKDSK /scan", "Online NTFS file system scan without scheduling a reboot.", repair_chkdsk, True),
    ("Check Disk Health", "Report SMART/reliability health status for each physical disk.", check_disk_health, False),
    ("Schedule CHKDSK /f Repair", "Schedule a full file-system error repair for the next restart.", schedule_chkdsk_fix, True),
]

NETWORK_REPAIR_ACTIONS = [
    ("Flush DNS", "Clear the Windows DNS resolver cache.", flush_dns, False),
    ("Release IP", "Release the current DHCP-assigned IP address.", release_ip, False),
    ("Renew IP", "Request a fresh DHCP lease.", renew_ip, False),
    ("Reset Winsock", "Reset the Windows Winsock catalog (fixes many 'no internet' issues).", reset_winsock, True),
    ("Reset TCP/IP", "Reset Windows TCP/IP configuration to defaults.", reset_tcpip, True),
    ("Restart Network Adapters", "Disable and re-enable all active network adapters.", restart_network_adapters, True),
]
