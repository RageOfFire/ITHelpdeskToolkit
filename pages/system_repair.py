"""Windows / File System Repair page.

Runs the standard Windows integrity-repair chain: SFC (protected system
files), DISM (component store), CHKDSK (NTFS file system), plus a disk
health check and a scheduled full CHKDSK repair for problems that can
only be fixed outside a running Windows session.
"""

import threading
import tkinter as tk
from tkinter import messagebox, ttk

from services.system_repair import SYSTEM_REPAIR_ACTIONS


class SystemRepairMixin:
    def show_system_repair(self):
        self._clear_content()
        self.current_page = "system_repair"
        self._page_header(
            "Windows / File System Repair",
            "Repair corrupted system files, the Windows component store, and "
            "NTFS file system errors.",
        )

        ttk.Label(
            self.content,
            text=(
                "Some of these take several minutes (SFC and DISM RestoreHealth "
                "can run 10-20+). Run them one at a time if you want to watch "
                "the result, or Run All and check back."
            ),
            style="Subtitle.TLabel",
            wraplength=850,
            justify="left",
        ).pack(anchor="w", pady=(0, 10))

        actions = ttk.Frame(self.content)
        actions.pack(fill="x", pady=(0, 10))

        self.sysrepair_run_all_button = ttk.Button(
            actions, text="▶ Run All Repairs", command=self.run_all_system_repairs,
            style="Action.TButton"
        )
        self.sysrepair_run_all_button.pack(side="left", padx=(0, 8))
        ttk.Button(
            actions, text="▶ Run Selected", command=self.run_selected_system_repair,
            style="Action.TButton"
        ).pack(side="left")

        table_frame = ttk.Frame(self.content)
        table_frame.pack(fill="x", pady=10)

        self.sysrepair_tree = ttk.Treeview(
            table_frame, columns=("fix", "status"), show="headings",
            height=len(SYSTEM_REPAIR_ACTIONS)
        )
        self.sysrepair_tree.heading("fix", text="Repair")
        self.sysrepair_tree.heading("status", text="Status")
        self.sysrepair_tree.column("fix", width=400)
        self.sysrepair_tree.column("status", width=160, anchor="center")
        self.sysrepair_tree.pack(side="left", fill="x", expand=True)

        scroll = ttk.Scrollbar(table_frame, orient="vertical", command=self.sysrepair_tree.yview)
        scroll.pack(side="right", fill="y")
        self.sysrepair_tree.configure(yscrollcommand=scroll.set)

        self.sysrepair_tree.tag_configure("done", foreground="#008000")
        self.sysrepair_tree.tag_configure("failed", foreground="#CC0000")
        self.sysrepair_tree.tag_configure("pending", foreground="#777777")

        for index, (label, _description, _func, _admin) in enumerate(SYSTEM_REPAIR_ACTIONS):
            self.sysrepair_tree.insert(
                "", "end", iid=str(index), values=(label, "NOT RUN"), tags=("pending",)
            )

        self.sysrepair_tree.bind("<<TreeviewSelect>>", self._on_system_repair_select)

        ttk.Label(
            self.content, text="Details", font=("Segoe UI", 12, "bold")
        ).pack(anchor="w", pady=(10, 4))
        self.sysrepair_detail = tk.Text(
            self.content, height=13, font=("Consolas", 10), wrap=tk.WORD
        )
        self.sysrepair_detail.pack(fill="both", expand=True)
        self.sysrepair_detail.insert(
            tk.END,
            "Select a repair below and click 'Run Selected', or 'Run All Repairs' "
            "to run the full chain.\n\n"
            "Recommended order for a suspected corruption issue: DISM CheckHealth "
            "→ DISM ScanHealth → DISM RestoreHealth → SFC /scannow → CHKDSK.\n\n"
            "'Schedule CHKDSK /f Repair' only schedules the repair — it needs a "
            "restart to actually run, since Windows can't lock the system drive "
            "while it's in use."
        )

        self.system_repair_results = {}

    def _on_system_repair_select(self, event=None):
        selected = self.sysrepair_tree.selection()
        if not selected:
            return
        index = int(selected[0])
        label, description, _func, admin = SYSTEM_REPAIR_ACTIONS[index]
        result = self.system_repair_results.get(index)

        text = f"{label}\n" + "=" * 60 + f"\n\n{description}\n"
        if admin:
            text += "\n(Requires administrator privileges — you may see a UAC prompt.)\n"
        if result:
            text += (
                "\n" + "-" * 60 +
                f"\nResult: {'SUCCESS' if result['success'] else 'FAILED'}\n{result['output']}\n"
            )

        self.sysrepair_detail.delete("1.0", tk.END)
        self.sysrepair_detail.insert(tk.END, text)

    def run_selected_system_repair(self):
        selected = self.sysrepair_tree.selection()
        if not selected:
            messagebox.showinfo("Select a Repair", "Select a repair from the list first.")
            return
        self._run_system_repairs([int(selected[0])])

    def run_all_system_repairs(self):
        answer = messagebox.askyesno(
            "Run All Repairs",
            "This runs SFC, DISM and CHKDSK in sequence and can take 20+ minutes. "
            "Some steps require administrator privileges (UAC prompts may appear).\n\n"
            "Continue?"
        )
        if not answer:
            return
        self._run_system_repairs(list(range(len(SYSTEM_REPAIR_ACTIONS))))

    def _run_system_repairs(self, indexes):
        self.sysrepair_run_all_button.config(state=tk.DISABLED)
        self.status_var.set("Running Windows/file system repairs...")
        for index in indexes:
            label = SYSTEM_REPAIR_ACTIONS[index][0]
            self.sysrepair_tree.item(str(index), values=(label, "RUNNING..."), tags=("pending",))
        threading.Thread(target=self._system_repair_worker, args=(indexes,), daemon=True).start()

    def _system_repair_worker(self, indexes):
        for index in indexes:
            _label, _description, func, _admin = SYSTEM_REPAIR_ACTIONS[index]
            try:
                success, output = func()
            except Exception as exc:
                success, output = False, str(exc)
            self.system_repair_results[index] = {"success": success, "output": output}
            self.after(0, lambda i=index, ok=success: self._update_system_repair_status(i, ok))
        self.after(0, self._system_repairs_finished)

    def _update_system_repair_status(self, index, success):
        if not self.sysrepair_tree.winfo_exists():
            return
        label = SYSTEM_REPAIR_ACTIONS[index][0]
        self.sysrepair_tree.item(
            str(index),
            values=(label, "✓ DONE" if success else "✗ FAILED"),
            tags=("done" if success else "failed"),
        )

    def _system_repairs_finished(self):
        if not self.sysrepair_tree.winfo_exists():
            return
        self.sysrepair_run_all_button.config(state=tk.NORMAL)
        done = sum(1 for r in self.system_repair_results.values() if r["success"])
        failed = len(self.system_repair_results) - done
        self.status_var.set(f"Windows/file system repairs complete — {done} succeeded, {failed} failed.")
        selected = self.sysrepair_tree.selection()
        if selected:
            self._on_system_repair_select()
