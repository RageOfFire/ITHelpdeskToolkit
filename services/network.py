"""Network connectivity diagnostic checks."""

import socket

from .shell import run_command, run_powershell


def get_gateway():
    ok, output = run_command("ipconfig")
    if not ok:
        return None
    for line in output.splitlines():
        if "Default Gateway" in line:
            gateway = line.split(":", 1)[-1].strip()
            if gateway:
                return gateway
    return None


def check_adapter():
    ok, output = run_powershell(
        "(Get-NetAdapter -Physical | "
        "Where-Object {$_.Status -eq 'Up'} | "
        "Select-Object Name,Status,LinkSpeed | "
        "ConvertTo-Csv -NoTypeInformation)"
    )
    if not ok:
        return False, output, "Unable to query Windows network adapters."
    if len(output.strip().splitlines()) >= 2:
        return True, output, "At least one physical network adapter is connected."
    return False, output, "No physical network adapter is currently connected."


def check_gateway():
    gateway = get_gateway()
    if not gateway:
        return False, "No default gateway found.", "DHCP, network adapter, or local network configuration."
    ok, output = run_command(f"ping -n 3 {gateway}")
    if ok:
        return True, output, "Local network and gateway are reachable."
    return False, output, "LAN/Wi-Fi connection, switch, router, or gateway problem."


def check_internet():
    ok, output = run_command("ping -n 3 8.8.8.8")
    if ok:
        return True, output, "Internet connection is reachable."
    return False, output, "Internet connection, firewall, router, or ISP problem."


def check_dns():
    ok, output = run_command("nslookup google.com")
    if ok and "Address" in output:
        return True, output, "DNS resolution is working."
    return False, output, "DNS server or DNS configuration problem."


def check_google():
    ok, output = run_command("ping -n 3 google.com")
    if ok:
        return True, output, "Internet and DNS are working."
    return False, output, "DNS or Internet connectivity problem."


def check_route(max_hops=30):
    try:
        max_hops = int(max_hops)
        if max_hops <= 0:
            max_hops = 30
    except (ValueError, TypeError):
        max_hops = 30
    timeout = max(30, max_hops * 3)
    ok, output = run_command(f"tracert -d -h {max_hops} 8.8.8.8", timeout=timeout)
    if ok:
        return True, output, "A route to the Internet was found."
    return False, output, "Routing, firewall, gateway, or Internet connection problem."


def check_tcp_dns():
    try:
        sock = socket.create_connection(("8.8.8.8", 53), timeout=5)
        sock.close()
        return True, "TCP connection to 8.8.8.8:53 succeeded.", "DNS server is reachable."
    except Exception as exc:
        return False, str(exc), "Firewall, routing, or network connectivity problem."


NETWORK_TESTS = {
    "Network Adapter": check_adapter,
    "Default Gateway": check_gateway,
    "Internet (8.8.8.8)": check_internet,
    "DNS": check_dns,
    "Internet + DNS": check_google,
    "Route": check_route,
    "TCP DNS": check_tcp_dns,
}
