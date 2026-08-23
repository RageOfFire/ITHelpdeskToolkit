"""Network diagnostics page."""

import datetime
import threading
import tkinter as tk
from tkinter import filedialog, messagebox, ttk

from constants import APP_NAME
from services.network import NETWORK_TESTS


class NetworkMixin:
    def show_network(self):
        self._clear_content()
        self.current_page = "network"
        self._page_header(
            "Network Diagnostics",
            "Run common connectivity tests and identify likely problems.",
        )

        actions = ttk.Frame(self.content)
        actions.pack(fill="x", pady=(0, 10))

        self.network_run_button = ttk.Button(
            actions, text="▶ RUN ALL CHECKS", command=self.run_network_checks,
            style="Action.TButton"
        )
        self.network_run_button.pack(side="left", padx=(0, 8))
        ttk.Button(
            actions, text="💾 Export Report", command=self.export_network_report,
            style="Action.TButton"
        ).pack(side="left")

        table_frame = ttk.Frame(self.content)
        table_frame.pack(fill="x", pady=10)

        self.network_tree = ttk.Treeview(
            table_frame, columns=("test", "status"), show="headings", height=8
        )
        self.network_tree.heading("test", text="Network Test")
        self.network_tree.heading("status", text="Status")
        self.network_tree.column("test", width=520)
        self.network_tree.column("status", width=220, anchor="center")
        self.network_tree.pack(side="left", fill="x", expand=True)

        scroll = ttk.Scrollbar(table_frame, orient="vertical", command=self.network_tree.yview)
        scroll.pack(side="right", fill="y")
        self.network_tree.configure(yscrollcommand=scroll.set)

        self.network_tree.tag_configure("working", foreground="#008000")
        self.network_tree.tag_configure("failed", foreground="#CC0000")
        self.network_tree.tag_configure("unknown", foreground="#777777")

        for name in NETWORK_TESTS:
            self.network_tree.insert(
                "", "end", iid=name, values=(name, "NOT TESTED"), tags=("unknown",)
            )

        self.network_tree.bind("<<TreeviewSelect>>", self.show_network_detail)

        ttk.Label(
            self.content, text="Diagnostic Details", font=("Segoe UI", 12, "bold")
        ).pack(anchor="w", pady=(10, 4))
        self.network_detail = tk.Text(
            self.content, height=13, font=("Consolas", 10), wrap=tk.WORD
        )
        self.network_detail.pack(fill="both", expand=True)

    def run_network_checks(self):
        self.network_run_button.config(state=tk.DISABLED, text="CHECKING...")
        self.network_results = {}
        self.network_detail.delete("1.0", tk.END)
        self.network_detail.insert(tk.END, "Running network diagnostics...\n\n")
        for name in NETWORK_TESTS:
            self.network_tree.item(name, values=(name, "TESTING..."), tags=("unknown",))
        threading.Thread(target=self._network_worker, daemon=True).start()

    def _network_worker(self):
        for name, function in NETWORK_TESTS.items():
            try:
                success, output, problem = function()
                self.network_results[name] = {
                    "success": success, "output": output, "problem": problem
                }
                self.after(0, lambda n=name, ok=success: self._update_network_status(n, ok))
            except Exception as exc:
                self.network_results[name] = {
                    "success": False, "output": str(exc),
                    "problem": "Unexpected diagnostic error."
                }
                self.after(0, lambda n=name: self._update_network_status(n, False))
        self.after(0, self._network_finished)

    def _update_network_status(self, name, success):
        if not self.network_tree.winfo_exists():
            return
        self.network_tree.item(
            name,
            values=(name, "✓ WORKING" if success else "✗ FAILED"),
            tags=("working" if success else "failed"),
        )

    def _network_finished(self):
        if not self.network_tree.winfo_exists():
            return
        self.network_run_button.config(state=tk.NORMAL, text="▶ RUN ALL CHECKS")
        working = sum(1 for r in self.network_results.values() if r["success"])
        failed = len(self.network_results) - working
        self.network_detail.delete("1.0", tk.END)
        self.network_detail.insert(
            tk.END,
            "NETWORK DIAGNOSTIC COMPLETE\n" + "=" * 50 +
            f"\n\nWorking: {working}\nFailed:  {failed}\n\n"
        )
        if failed:
            self.network_detail.insert(tk.END, "LIKELY PROBLEMS\n" + "-" * 50 + "\n")
            for name, result in self.network_results.items():
                if not result["success"]:
                    self.network_detail.insert(
                        tk.END, f"• {name}: {result['problem']}\n"
                    )
        else:
            self.network_detail.insert(tk.END, "✓ All network checks passed.\n")
        self.status_var.set(f"Network diagnostics complete — {working} passed, {failed} failed.")

    def show_network_detail(self, event=None):
        selected = self.network_tree.selection()
        if not selected:
            return
        name = selected[0]
        if name not in self.network_results:
            return
        result = self.network_results[name]
        self.network_detail.delete("1.0", tk.END)
        self.network_detail.insert(
            tk.END,
            f"TEST: {name}\n" + "=" * 60 +
            f"\n\nStatus: {'WORKING' if result['success'] else 'FAILED'}\n\n" +
            f"Likely problem:\n{result['problem']}\n\n" +
            "Command output:\n" + "-" * 60 + f"\n{result['output']}"
        )

    def export_network_report(self):
        if not self.network_results:
            messagebox.showinfo("No Results", "Run the network checks first.")
            return
        filename = filedialog.asksaveasfilename(
            defaultextension=".txt",
            filetypes=[("Text files", "*.txt")],
            initialfile=f"network_report_{datetime.datetime.now().strftime('%Y%m%d_%H%M%S')}.txt",
        )
        if not filename:
            return
        with open(filename, "w", encoding="utf-8") as file:
            file.write(f"{APP_NAME} - NETWORK DIAGNOSTIC REPORT\n")
            file.write("=" * 65 + "\n\n")
            file.write(datetime.datetime.now().strftime("Time: %Y-%m-%d %H:%M:%S\n\n"))
            for name, result in self.network_results.items():
                file.write(f"{name}: {'WORKING' if result['success'] else 'FAILED'}\n")
                file.write(f"Likely problem: {result['problem']}\n")
                file.write("Output:\n" + result["output"] + "\n")
                file.write("-" * 65 + "\n")
        messagebox.showinfo("Report Saved", f"Network report saved to:\n{filename}")
