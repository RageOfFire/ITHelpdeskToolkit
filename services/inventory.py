"""Endpoint asset inventory collection."""

import datetime
import os
import platform
import socket

from .shell import bytes_to_gb, get_cim_value, run_powershell

try:
    import psutil
except ImportError:
    psutil = None


def get_windows_info():
    ok, output = run_powershell(
        "(Get-CimInstance Win32_OperatingSystem | "
        "Select-Object Caption,Version,BuildNumber | "
        "Format-List | Out-String)"
    )
    info = {"caption": "", "version": "", "build": ""}
    if not ok:
        return info

    for line in output.splitlines():
        if ":" not in line:
            continue
        key, value = line.split(":", 1)
        key = key.strip().lower()
        value = value.strip()
        if key == "caption":
            info["caption"] = value
        elif key == "version":
            info["version"] = value
        elif key == "buildnumber":
            info["build"] = value
    return info


def get_ip_address():
    try:
        addresses = socket.getaddrinfo(socket.gethostname(), None, socket.AF_INET)
        for address in addresses:
            ip = address[4][0]
            if not ip.startswith("127."):
                return ip
    except Exception:
        pass
    return "Unavailable"


def get_mac_address():
    if psutil is None:
        return "Unavailable"
    try:
        interfaces = psutil.net_if_addrs()
        stats = psutil.net_if_stats()
        for name, addresses in interfaces.items():
            if name not in stats or not stats[name].isup:
                continue
            for address in addresses:
                if address.family == psutil.AF_LINK and address.address:
                    return address.address
    except Exception:
        pass
    return "Unavailable"


def collect_inventory():
    if psutil is None:
        raise RuntimeError("psutil is not installed. Run: pip install -r requirements.txt")

    windows = get_windows_info()
    system_drive = os.environ.get("SystemDrive", "C:")
    disk = psutil.disk_usage(system_drive + "\\")
    memory = psutil.virtual_memory()

    return {
        "Scan Date": datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
        "Computer Name": socket.gethostname(),
        "Current User": os.environ.get("USERNAME", "Unknown"),
        "Manufacturer": get_cim_value("Win32_ComputerSystem", "Manufacturer") or "Unavailable",
        "Model": get_cim_value("Win32_ComputerSystem", "Model") or "Unavailable",
        "Serial Number": get_cim_value("Win32_BIOS", "SerialNumber") or "Unavailable",
        "Operating System": windows["caption"] or platform.system(),
        "Windows Version": windows["version"] or platform.version(),
        "Windows Build": windows["build"] or "Unavailable",
        "CPU": get_cim_value("Win32_Processor", "Name") or platform.processor(),
        "CPU Cores": psutil.cpu_count(logical=False) or 0,
        "Logical CPUs": psutil.cpu_count(logical=True) or 0,
        "RAM Total (GB)": bytes_to_gb(memory.total),
        "RAM Available (GB)": bytes_to_gb(memory.available),
        "Disk": system_drive,
        "Disk Total (GB)": bytes_to_gb(disk.total),
        "Disk Free (GB)": bytes_to_gb(disk.free),
        "Disk Used (%)": disk.percent,
        "IP Address": get_ip_address(),
        "MAC Address": get_mac_address(),
        "Architecture": platform.machine(),
    }
