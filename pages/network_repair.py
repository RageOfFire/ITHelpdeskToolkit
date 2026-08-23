"""Network Repair page.

Runs the standard network-stack repair chain: DNS flush, DHCP
release/renew, Winsock and TCP/IP reset, and a network adapter restart.
Winsock/TCP/IP resets change deep OS network configuration, so this
page recommends a reboot afterward, same as ipconfig itself does.
"""

import threading
import tkinter as tk
from tkinter import messagebox, ttk

from services.network import get_gateway
from services.system_repair import NETWORK_REPAIR_ACTIONS


class NetworkRepairMixin:
    def show_network_repair(self):
        self._clear_content()
        self.current_page = "network_repair"
        self._page_header(
            "Network Repair",
            "Automated fixes for common 'no internet' and connectivity problems.",
        )

        ttk.Label(
            self.content,
            text=(
                "Recommended order for 'no internet' issues: Flush DNS → "
                "Release/Renew IP → Reset Winsock → Reset TCP/IP → restart the PC. "
                "Winsock and TCP/IP resets only take full effect after a reboot."
            ),
            style="Subtitle.TLabel",
            wraplength=850,
            justify="left",
        ).pack(anchor="w", pady=(0, 10))

        actions = ttk.Frame(self.content)
        actions.pack(fill="x", pady=(0, 10))

        self.netrepair_run_all_button = ttk.Button(
            actions, text="▶ Run All Repairs", command=self.run_all_network_repairs,
            style="Action.TButton"
        )
        self.netrepair_run_all_button.pack(side="left", padx=(0, 8))
        ttk.Button(
            actions, text="▶ Run Selected", command=self.run_selected_network_repair,
            style="Action.TButton"
        ).pack(side="left", padx=(0, 8))
        ttk.Button(
            actions, text="🔄 Refresh Gateway Info", command=self.refresh_network_repair_gateway,
            style="Action.TButton"
        ).pack(side="left")

        gateway_frame = ttk.LabelFrame(self.content, text="Current Default Gateway", padding=10)
        gateway_frame.pack(fill="x", pady=(0, 10))
        self.netrepair_gateway_var = tk.StringVar(value="Checking...")
        ttk.Label(
            gateway_frame, textvariable=self.netrepair_gateway_var,
            font=("Consolas", 10, "bold")
        ).pack(anchor="w")

        table_frame = ttk.Frame(self.content)
        table_frame.pack(fill="x", pady=10)

        self.netrepair_tree = ttk.Treeview(
            table_frame, columns=("fix", "status"), show="headings",
            height=len(NETWORK_REPAIR_ACTIONS)
        )
        self.netrepair_tree.heading("fix", text="Repair")
        self.netrepair_tree.heading("status", text="Status")
        self.netrepair_tree.column("fix", width=400)
        self.netrepair_tree.column("status", width=160, anchor="center")
        self.netrepair_tree.pack(side="left", fill="x", expand=True)

        scroll = ttk.Scrollbar(table_frame, orient="vertical", command=self.netrepair_tree.yview)
        scroll.pack(side="right", fill="y")
        self.netrepair_tree.configure(yscrollcommand=scroll.set)

        self.netrepair_tree.tag_configure("done", foreground="#008000")
        self.netrepair_tree.tag_configure("failed", foreground="#CC0000")
        self.netrepair_tree.tag_configure("pending", foreground="#777777")

        for index, (label, _description, _func, _admin) in enumerate(NETWORK_REPAIR_ACTIONS):
            self.netrepair_tree.insert(
                "", "end", iid=str(index), values=(label, "NOT RUN"), tags=("pending",)
            )

        self.netrepair_tree.bind("<<TreeviewSelect>>", self._on_network_repair_select)

        ttk.Label(
            self.content, text="Details", font=("Segoe UI", 12, "bold")
        ).pack(anchor="w", pady=(10, 4))
        self.netrepair_detail = tk.Text(
            self.content, height=12, font=("Consolas", 10), wrap=tk.WORD
        )
        self.netrepair_detail.pack(fill="both", expand=True)
        self.netrepair_detail.insert(
            tk.END,
            "Select a repair below and click 'Run Selected', or 'Run All Repairs' "
            "to run the full chain.\n\n"
            "If the issue is Wi-Fi/LAN-specific rather than 'no internet at all', "
            "check the Network Diagnostics page first to confirm where the "
            "connection is actually breaking down."
        )

        self.network_repair_results = {}
        self.refresh_network_repair_gateway()

    def refresh_network_repair_gateway(self):
        def worker():
            gateway = get_gateway() or "Not detected"
            self.after(0, lambda: self._set_network_repair_gateway(gateway))

        threading.Thread(target=worker, daemon=True).start()

    def _set_network_repair_gateway(self, gateway):
        if not self.netrepair_tree.winfo_exists():
            return
        self.netrepair_gateway_var.set(gateway)

    def _on_network_repair_select(self, event=None):
        selected = self.netrepair_tree.selection()
        if not selected:
            return
        index = int(selected[0])
        label, description, _func, admin = NETWORK_REPAIR_ACTIONS[index]
        result = self.network_repair_results.get(index)

        text = f"{label}\n" + "=" * 60 + f"\n\n{description}\n"
        if admin:
            text += "\n(Requires administrator privileges — you may see a UAC prompt.)\n"
        if result:
            text += (
                "\n" + "-" * 60 +
                f"\nResult: {'SUCCESS' if result['success'] else 'FAILED'}\n{result['output']}\n"
            )

        self.netrepair_detail.delete("1.0", tk.END)
        self.netrepair_detail.insert(tk.END, text)

    def run_selected_network_repair(self):
        selected = self.netrepair_tree.selection()
        if not selected:
            messagebox.showinfo("Select a Repair", "Select a repair from the list first.")
            return
        self._run_network_repairs([int(selected[0])])

    def run_all_network_repairs(self):
        answer = messagebox.askyesno(
            "Run All Repairs",
            "This resets Winsock and TCP/IP and restarts network adapters — "
            "the connection will briefly drop, and a reboot is recommended "
            "afterward for the changes to fully apply.\n\nContinue?"
        )
        if not answer:
            return
        self._run_network_repairs(list(range(len(NETWORK_REPAIR_ACTIONS))))

    def _run_network_repairs(self, indexes):
        self.netrepair_run_all_button.config(state=tk.DISABLED)
        self.status_var.set("Running network repairs...")
        for index in indexes:
            label = NETWORK_REPAIR_ACTIONS[index][0]
            self.netrepair_tree.item(str(index), values=(label, "RUNNING..."), tags=("pending",))
        threading.Thread(target=self._network_repair_worker, args=(indexes,), daemon=True).start()

    def _network_repair_worker(self, indexes):
        for index in indexes:
            _label, _description, func, _admin = NETWORK_REPAIR_ACTIONS[index]
            try:
                success, output = func()
            except Exception as exc:
                success, output = False, str(exc)
            self.network_repair_results[index] = {"success": success, "output": output}
            self.after(0, lambda i=index, ok=success: self._update_network_repair_status(i, ok))
        self.after(0, self._network_repairs_finished)

    def _update_network_repair_status(self, index, success):
        if not self.netrepair_tree.winfo_exists():
            return
        label = NETWORK_REPAIR_ACTIONS[index][0]
        self.netrepair_tree.item(
            str(index),
            values=(label, "✓ DONE" if success else "✗ FAILED"),
            tags=("done" if success else "failed"),
        )

    def _network_repairs_finished(self):
        if not self.netrepair_tree.winfo_exists():
            return
        self.netrepair_run_all_button.config(state=tk.NORMAL)
        done = sum(1 for r in self.network_repair_results.values() if r["success"])
        failed = len(self.network_repair_results) - done
        self.status_var.set(f"Network repairs complete — {done} succeeded, {failed} failed.")
        self.refresh_network_repair_gateway()
        selected = self.netrepair_tree.selection()
        if selected:
            self._on_network_repair_select()
