"""Printer troubleshooter page."""

import threading
import tkinter as tk
from tkinter import messagebox, ttk

from services.printer import get_print_jobs, get_printers, get_spooler_status, printer_connectivity
from services.shell import run_command, run_powershell


class PrinterMixin:
    def show_printer(self):
        self._clear_content()
        self.current_page = "printer"
        self._page_header(
            "Printer Troubleshooter",
            "Inspect printers, print jobs, ports and the Windows Print Spooler.",
        )

        top = ttk.Frame(self.content)
        top.pack(fill="x", pady=(0, 10))

        ttk.Button(
            top, text="🔄 Refresh Printers", command=self.refresh_printers,
            style="Action.TButton"
        ).pack(side="left", padx=(0, 8))
        ttk.Button(
            top, text="▶ Diagnose Selected", command=self.diagnose_selected_printer,
            style="Action.TButton"
        ).pack(side="left", padx=(0, 8))
        ttk.Button(
            top, text="🧹 Clear Selected Jobs", command=self.clear_selected_jobs,
            style="Action.TButton"
        ).pack(side="left", padx=(0, 8))
        ttk.Button(
            top, text="🔧 Restart Spooler", command=self.restart_spooler,
            style="Action.TButton"
        ).pack(side="left")

        spooler_frame = ttk.LabelFrame(self.content, text="Print Spooler", padding=10)
        spooler_frame.pack(fill="x", pady=(0, 10))

        self.spooler_status_var = tk.StringVar(value="Checking...")
        ttk.Label(
            spooler_frame, text="Service Status:"
        ).pack(side="left", padx=(0, 8))
        ttk.Label(
            spooler_frame, textvariable=self.spooler_status_var,
            font=("Segoe UI", 10, "bold")
        ).pack(side="left")

        printer_frame = ttk.Frame(self.content)
        printer_frame.pack(fill="both", expand=True)

        self.printer_tree = ttk.Treeview(
            printer_frame,
            columns=("name", "default", "status", "driver", "port"),
            show="headings",
        )
        headings = [
            ("name", "Printer", 250),
            ("default", "Default", 80),
            ("status", "Status", 120),
            ("driver", "Driver", 260),
            ("port", "Port", 180),
        ]
        for column, heading, width in headings:
            self.printer_tree.heading(column, text=heading)
            self.printer_tree.column(column, width=width)

        scroll = ttk.Scrollbar(
            printer_frame, orient="vertical", command=self.printer_tree.yview
        )
        self.printer_tree.configure(yscrollcommand=scroll.set)
        self.printer_tree.pack(side="left", fill="both", expand=True)
        scroll.pack(side="right", fill="y")
        self.printer_tree.bind("<<TreeviewSelect>>", self.on_printer_select)

        ttk.Label(
            self.content, text="Troubleshooting Details",
            font=("Segoe UI", 12, "bold")
        ).pack(anchor="w", pady=(10, 4))

        self.printer_detail = tk.Text(
            self.content, height=11, font=("Consolas", 10), wrap=tk.WORD
        )
        self.printer_detail.pack(fill="both", expand=True)

        self.refresh_printers()

    def refresh_printers(self):
        self.status_var.set("Refreshing printer information...")
        self.spooler_status_var.set(get_spooler_status())

        for item in self.printer_tree.get_children():
            self.printer_tree.delete(item)

        def worker():
            printers = get_printers()
            self.after(0, lambda: self._printers_loaded(printers))

        threading.Thread(target=worker, daemon=True).start()

    def _printers_loaded(self, printers):
        if not self.printer_tree.winfo_exists():
            return
        self.printers = printers
        for index, printer in enumerate(printers):
            iid = str(index)
            self.printer_tree.insert(
                "",
                "end",
                iid=iid,
                values=(
                    printer["Name"],
                    "Yes" if printer["Default"].lower() == "true" else "No",
                    printer["Status"],
                    printer["Driver"],
                    printer["Port"],
                ),
            )

        self.status_var.set(f"Printer refresh complete — {len(printers)} printer(s) found.")

        if not printers:
            self.printer_detail.delete("1.0", tk.END)
            self.printer_detail.insert(
                tk.END,
                "No printers were returned by Windows.\n\n"
                "Check Settings > Bluetooth & devices > Printers & scanners."
            )

    def on_printer_select(self, event=None):
        selected = self.printer_tree.selection()
        if not selected:
            self.selected_printer = None
            return
        index = int(selected[0])
        if 0 <= index < len(self.printers):
            self.selected_printer = self.printers[index]
            printer = self.selected_printer
            self.printer_detail.delete("1.0", tk.END)
            self.printer_detail.insert(
                tk.END,
                f"PRINTER: {printer['Name']}\n"
                + "=" * 65
                + f"\n\nDefault: {printer['Default']}\n"
                f"Status: {printer['Status']}\n"
                f"Driver: {printer['Driver']}\n"
                f"Port: {printer['Port']}\n\n"
                "Select 'Diagnose Selected' for connectivity and job checks."
            )

    def diagnose_selected_printer(self):
        if not self.selected_printer:
            messagebox.showinfo("Select Printer", "Select a printer first.")
            return

        printer = self.selected_printer
        self.status_var.set("Diagnosing printer...")
        self.printer_detail.delete("1.0", tk.END)
        self.printer_detail.insert(tk.END, "Running printer diagnostics...\n\n")

        def worker():
            spooler = get_spooler_status()
            connectivity_ok, connectivity_detail = printer_connectivity(printer["Port"])
            jobs = get_print_jobs(printer["Name"])

            try:
                status_value = int(printer["Status"])
                status_text = str(status_value)
            except (ValueError, TypeError):
                status_text = printer["Status"] or "Unknown"

            report = (
                f"PRINTER DIAGNOSTIC: {printer['Name']}\n"
                + "=" * 70
                + f"\n\nSpooler: {spooler}\n"
                f"Printer Status Code: {status_text}\n"
                f"Default Printer: {printer['Default']}\n"
                f"Driver: {printer['Driver']}\n"
                f"Port: {printer['Port']}\n\n"
                f"Connectivity: {'PASS' if connectivity_ok else 'FAIL'}\n"
                f"{connectivity_detail}\n\n"
                f"Print Job Query:\n{jobs if jobs else 'No print jobs found or jobs could not be queried.'}\n\n"
            )

            if spooler.lower() != "running":
                report += "RECOMMENDATION: Start/restart the Print Spooler service.\n"
            elif not connectivity_ok:
                report += "RECOMMENDATION: Check printer power, network connection, IP address and firewall.\n"
            elif jobs:
                report += "RECOMMENDATION: Review or clear stuck print jobs.\n"
            else:
                report += "RESULT: No obvious issue detected by these checks.\n"

            self.after(0, lambda: self._printer_diagnosis_finished(report))

        threading.Thread(target=worker, daemon=True).start()

    def _printer_diagnosis_finished(self, report):
        if not self.printer_detail.winfo_exists():
            return
        self.printer_detail.delete("1.0", tk.END)
        self.printer_detail.insert(tk.END, report)
        self.spooler_status_var.set(get_spooler_status())
        self.status_var.set("Printer diagnosis complete.")

    def restart_spooler(self):
        answer = messagebox.askyesno(
            "Restart Print Spooler",
            "Restart the Windows Print Spooler service?\n\n"
            "This can interrupt active print jobs."
        )
        if not answer:
            return

        self.status_var.set("Restarting Print Spooler...")

        def worker():
            ok, output = run_command(
                "net stop spooler && net start spooler",
                timeout=30,
            )
            self.after(0, lambda: self._spooler_finished(ok, output))

        threading.Thread(target=worker, daemon=True).start()

    def _spooler_finished(self, ok, output):
        if not self.printer_detail.winfo_exists():
            return
        self.spooler_status_var.set(get_spooler_status())
        if ok:
            self.status_var.set("Print Spooler restarted successfully.")
            messagebox.showinfo("Spooler Restarted", "The Print Spooler service was restarted.")
        else:
            self.status_var.set("Unable to restart Print Spooler.")
            messagebox.showerror(
                "Spooler Error",
                "Could not restart the Print Spooler.\n\n"
                + output
                + "\n\nTry running the application as Administrator."
            )

    def clear_selected_jobs(self):
        if not self.selected_printer:
            messagebox.showinfo("Select Printer", "Select a printer first.")
            return

        name = self.selected_printer["Name"]
        answer = messagebox.askyesno(
            "Clear Print Jobs",
            f"Delete all queued jobs for:\n\n{name}\n\nContinue?"
        )
        if not answer:
            return

        safe = name.replace("'", "''")
        self.status_var.set("Clearing print jobs...")

        def worker():
            ok, output = run_powershell(
                f"Get-PrintJob -PrinterName '{safe}' | Remove-PrintJob -Confirm:$false",
                timeout=30,
            )
            self.after(0, lambda: self._clear_jobs_finished(ok, output))

        threading.Thread(target=worker, daemon=True).start()

    def _clear_jobs_finished(self, ok, output):
        if ok:
            self.status_var.set("Print jobs cleared.")
            messagebox.showinfo("Jobs Cleared", "Queued print jobs were cleared.")
        else:
            self.status_var.set("Unable to clear print jobs.")
            messagebox.showerror(
                "Print Job Error",
                "Unable to clear the selected printer's jobs.\n\n"
                + output
                + "\n\nTry running the application as Administrator."
            )
