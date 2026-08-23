"""Printer, print-job and spooler diagnostic helpers."""

import csv
import socket

from .shell import run_command, run_powershell


def get_printers():
    ok, output = run_powershell(
        "Get-Printer | Select-Object Name,Default,PrinterStatus,DriverName,PortName | "
        "ConvertTo-Csv -NoTypeInformation"
    )
    if not ok:
        return []

    lines = output.splitlines()
    if len(lines) < 2:
        return []

    rows = []
    reader = csv.DictReader(lines)
    for row in reader:
        rows.append({
            "Name": row.get("Name", "").strip(),
            "Default": row.get("Default", "").strip(),
            "Status": row.get("PrinterStatus", "").strip(),
            "Driver": row.get("DriverName", "").strip(),
            "Port": row.get("PortName", "").strip(),
        })
    return rows


def get_spooler_status():
    ok, output = run_powershell(
        "(Get-Service -Name Spooler).Status"
    )
    return output.strip() if ok else "Unavailable"


def get_print_jobs(printer_name=None):
    if printer_name:
        safe = printer_name.replace("'", "''")
        command = (
            f"Get-PrintJob -PrinterName '{safe}' | "
            "Select-Object ID,DocumentName,UserName,JobStatus,Size | "
            "ConvertTo-Csv -NoTypeInformation"
        )
    else:
        command = (
            "Get-PrintJob -PrinterName * | "
            "Select-Object PrinterName,ID,DocumentName,UserName,JobStatus,Size | "
            "ConvertTo-Csv -NoTypeInformation"
        )

    ok, output = run_powershell(command)
    if not ok or not output:
        return []
    return output


def printer_connectivity(port):
    if not port:
        return False, "No printer port reported."

    port_upper = port.upper()

    if port_upper.startswith("USB"):
        return True, f"{port} is a local USB printer port."

    # Common TCP/IP port format: IP_192.168.1.50 or raw IPv4 address.
    candidate = port
    if port_upper.startswith("IP_"):
        candidate = port[3:]

    try:
        socket.inet_aton(candidate)
        host = candidate
    except OSError:
        return True, f"Port '{port}' is configured; automatic IP testing was not possible."

    ok, output = run_command(f"ping -n 2 {host}", timeout=8)
    if ok:
        return True, f"Printer IP {host} responded to ping."
    return False, f"Printer IP {host} did not respond to ping."
