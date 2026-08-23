"""Generic process, PowerShell and privilege-elevation helpers.

Every other service module builds on top of these — nothing in here is
specific to networking, printers, Excel, etc.
"""

import ctypes
import os
import subprocess
import tempfile
import time


def run_command(command, timeout=15):
    try:
        result = subprocess.run(
            command,
            capture_output=True,
            text=True,
            timeout=timeout,
            shell=True,
            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
        )
        output = result.stdout.strip() or result.stderr.strip()
        return result.returncode == 0, output
    except subprocess.TimeoutExpired:
        return False, "Command timed out."
    except Exception as exc:
        return False, str(exc)


def run_powershell(command, timeout=15):
    """Run a PowerShell command by writing it to a temp .ps1 script and
    executing that file.

    Embedding a PowerShell command inline as `-Command "<command>"` means
    it gets re-parsed by cmd.exe first (subprocess uses shell=True on
    Windows). Any literal double quote inside the command — needed for
    things like string interpolation ("$($_.Property)") or a quoted path
    — collides with that outer double-quoted wrapper and corrupts the
    whole command line. Writing to a script file sidesteps that shell
    layer entirely: the file's content is never re-parsed by anything.
    """
    script_path = os.path.join(
        tempfile.gettempdir(),
        f"helpdesk_ps_{os.getpid()}_{int(time.time() * 1000)}.ps1",
    )
    try:
        with open(script_path, "w", encoding="utf-8") as file:
            file.write(command)
    except OSError as exc:
        return False, str(exc)

    try:
        return run_command(
            f'powershell -NoProfile -ExecutionPolicy Bypass -File "{script_path}"',
            timeout=timeout,
        )
    finally:
        try:
            os.remove(script_path)
        except OSError:
            pass


def get_cim_value(class_name, property_name):
    ok, output = run_powershell(
        f"(Get-CimInstance -ClassName {class_name}).{property_name}"
    )
    return output.strip() if ok else ""


def bytes_to_gb(value):
    return round(value / (1024 ** 3), 2)


def is_admin():
    try:
        return bool(ctypes.windll.shell32.IsUserAnAdmin())
    except Exception:
        return False


def run_elevated(command, timeout=300):
    """Run a command elevated and capture its output.

    Start-Process -Verb RunAs can't use -RedirectStandardOutput directly
    (PowerShell forbids combining them), and any command containing a
    literal double quote (e.g. a quoted path) can't be embedded inline
    without colliding with the outer double-quoted -Command wrapper that
    run_powershell/cmd.exe adds.

    To sidestep both problems, the command is written to a real .bat
    script (ordinary file I/O — no shell-quoting layers involved) that
    redirects its own output to a log file, and that script is what gets
    elevated and launched. -FilePath takes the .bat path as a single
    PowerShell token, so spaces in the path need no quoting either.
    """
    temp_dir = tempfile.gettempdir()
    token = f"{os.getpid()}_{int(time.time() * 1000)}"
    bat_path = os.path.join(temp_dir, f"helpdesk_elevated_{token}.bat")
    out_path = os.path.join(temp_dir, f"helpdesk_elevated_{token}.log")

    try:
        with open(bat_path, "w", encoding="utf-8") as file:
            file.write("@echo off\r\n")
            file.write(f'{command} > "{out_path}" 2>&1\r\n')
    except OSError as exc:
        return False, f"Could not create elevation script: {exc}"

    # Single-quoted PS literal — no double quotes anywhere in this string,
    # so it survives being re-wrapped in outer double quotes untouched.
    escaped_bat = bat_path.replace("'", "''")
    ps = f"Start-Process -FilePath '{escaped_bat}' -Verb RunAs -Wait -WindowStyle Hidden"

    try:
        ok, ps_output = run_powershell(ps, timeout=timeout)
    except Exception as exc:
        ok, ps_output = False, str(exc)

    output = ""
    try:
        if os.path.exists(out_path):
            with open(out_path, "r", errors="ignore") as file:
                output = file.read().strip()
    except OSError:
        pass
    finally:
        for path in (bat_path, out_path):
            try:
                os.remove(path)
            except OSError:
                pass

    if not ok:
        return False, output or ps_output or "Elevation was cancelled or failed (UAC declined?)."

    return True, output or "Command completed with no output."


def run_repair_command(command, timeout=300, admin=False):
    if admin and not is_admin():
        return run_elevated(command)
    return run_command(command, timeout=timeout)


def kill_process(process_name):
    safe = process_name.replace('"', "")
    return run_command(f'taskkill /F /IM "{safe}"', timeout=60)
