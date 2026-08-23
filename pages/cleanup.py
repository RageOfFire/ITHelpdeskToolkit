"""System Cleanup page.

Frees up disk space: temp files, Prefetch, browser caches, Recycle Bin,
and (admin) Windows Update's superseded-file cleanup — usually the
single biggest space recovery on an old install.
"""

import threading
import tkinter as tk
from tkinter import messagebox, ttk

from services.cleanup import CLEANUP_ACTIONS, get_disk_space_summary, open_disk_cleanup


class CleanupMixin:
    def show_cleanup(self):
        self._clear_content()
        self.current_page = "cleanup"
        self._page_header(
            "System Cleanup",
            "Free up disk space by clearing temp files, caches and Windows Update leftovers.",
        )

        space_frame = ttk.LabelFrame(self.content, text="Disk Space", padding=10)
        space_frame.pack(fill="x", pady=(0, 10))
        self.cleanup_space_var = tk.StringVar(value="Checking...")
        ttk.Label(
            space_frame, textvariable=self.cleanup_space_var, font=("Consolas", 10, "bold")
        ).pack(anchor="w")

        actions = ttk.Frame(self.content)
        actions.pack(fill="x", pady=(0, 10))

        self.cleanup_run_all_button = ttk.Button(
            actions, text="▶ Run All Cleanup", command=self.run_all_cleanup,
            style="Action.TButton"
        )
        self.cleanup_run_all_button.pack(side="left", padx=(0, 8))
        ttk.Button(
            actions, text="▶ Run Selected", command=self.run_selected_cleanup,
            style="Action.TButton"
        ).pack(side="left", padx=(0, 8))
        ttk.Button(
            actions, text="🧰 Open Disk Cleanup", command=self.launch_disk_cleanup,
            style="Action.TButton"
        ).pack(side="left")

        table_frame = ttk.Frame(self.content)
        table_frame.pack(fill="x", pady=10)

        self.cleanup_tree = ttk.Treeview(
            table_frame, columns=("fix", "status"), show="headings",
            height=len(CLEANUP_ACTIONS)
        )
        self.cleanup_tree.heading("fix", text="Cleanup Action")
        self.cleanup_tree.heading("status", text="Status")
        self.cleanup_tree.column("fix", width=400)
        self.cleanup_tree.column("status", width=160, anchor="center")
        self.cleanup_tree.pack(side="left", fill="x", expand=True)

        scroll = ttk.Scrollbar(table_frame, orient="vertical", command=self.cleanup_tree.yview)
        scroll.pack(side="right", fill="y")
        self.cleanup_tree.configure(yscrollcommand=scroll.set)

        self.cleanup_tree.tag_configure("done", foreground="#008000")
        self.cleanup_tree.tag_configure("failed", foreground="#CC0000")
        self.cleanup_tree.tag_configure("pending", foreground="#777777")

        for index, (label, _description, _func, _admin) in enumerate(CLEANUP_ACTIONS):
            self.cleanup_tree.insert(
                "", "end", iid=str(index), values=(label, "NOT RUN"), tags=("pending",)
            )

        self.cleanup_tree.bind("<<TreeviewSelect>>", self._on_cleanup_select)

        ttk.Label(
            self.content, text="Details", font=("Segoe UI", 12, "bold")
        ).pack(anchor="w", pady=(10, 4))
        self.cleanup_detail = tk.Text(
            self.content, height=12, font=("Consolas", 10), wrap=tk.WORD
        )
        self.cleanup_detail.pack(fill="both", expand=True)
        self.cleanup_detail.insert(
            tk.END,
            "Select a cleanup action below and click 'Run Selected', or 'Run All "
            "Cleanup' to run everything.\n\n"
            "These only clear Cache/Temp-style folders — browser history, logins, "
            "app settings and personal files are never touched.\n\n"
            "'Windows Update Cleanup' can take several minutes and needs "
            "administrator rights, but is often the biggest single space recovery."
        )

        self.cleanup_results = {}
        self.refresh_disk_space()

    def refresh_disk_space(self):
        def worker():
            summary = get_disk_space_summary()
            self.after(0, lambda: self._set_disk_space_summary(summary))

        threading.Thread(target=worker, daemon=True).start()

    def _set_disk_space_summary(self, summary):
        if not self.cleanup_tree.winfo_exists():
            return
        self.cleanup_space_var.set(summary)

    def _on_cleanup_select(self, event=None):
        selected = self.cleanup_tree.selection()
        if not selected:
            return
        index = int(selected[0])
        label, description, _func, admin = CLEANUP_ACTIONS[index]
        result = self.cleanup_results.get(index)

        text = f"{label}\n" + "=" * 60 + f"\n\n{description}\n"
        if admin:
            text += "\n(Requires administrator privileges — you may see a UAC prompt.)\n"
        if result:
            text += (
                "\n" + "-" * 60 +
                f"\nResult: {'SUCCESS' if result['success'] else 'FAILED'}\n{result['output']}\n"
            )

        self.cleanup_detail.delete("1.0", tk.END)
        self.cleanup_detail.insert(tk.END, text)

    def run_selected_cleanup(self):
        selected = self.cleanup_tree.selection()
        if not selected:
            messagebox.showinfo("Select an Action", "Select a cleanup action from the list first.")
            return
        self._run_cleanup([int(selected[0])])

    def run_all_cleanup(self):
        answer = messagebox.askyesno(
            "Run All Cleanup",
            "This clears temp files, Prefetch, browser caches and the Recycle "
            "Bin, and runs Windows Update Cleanup (can take several minutes).\n\n"
            "Continue?"
        )
        if not answer:
            return
        self._run_cleanup(list(range(len(CLEANUP_ACTIONS))))

    def _run_cleanup(self, indexes):
        self.cleanup_run_all_button.config(state=tk.DISABLED)
        self.status_var.set("Running cleanup...")
        for index in indexes:
            label = CLEANUP_ACTIONS[index][0]
            self.cleanup_tree.item(str(index), values=(label, "RUNNING..."), tags=("pending",))
        threading.Thread(target=self._cleanup_worker, args=(indexes,), daemon=True).start()

    def _cleanup_worker(self, indexes):
        for index in indexes:
            _label, _description, func, _admin = CLEANUP_ACTIONS[index]
            try:
                success, output = func()
            except Exception as exc:
                success, output = False, str(exc)
            self.cleanup_results[index] = {"success": success, "output": output}
            self.after(0, lambda i=index, ok=success: self._update_cleanup_status(i, ok))
        self.after(0, self._cleanup_finished)

    def _update_cleanup_status(self, index, success):
        if not self.cleanup_tree.winfo_exists():
            return
        label = CLEANUP_ACTIONS[index][0]
        self.cleanup_tree.item(
            str(index),
            values=(label, "✓ DONE" if success else "✗ FAILED"),
            tags=("done" if success else "failed"),
        )

    def _cleanup_finished(self):
        if not self.cleanup_tree.winfo_exists():
            return
        self.cleanup_run_all_button.config(state=tk.NORMAL)
        done = sum(1 for r in self.cleanup_results.values() if r["success"])
        failed = len(self.cleanup_results) - done
        self.status_var.set(f"Cleanup complete — {done} succeeded, {failed} failed.")
        self.refresh_disk_space()
        selected = self.cleanup_tree.selection()
        if selected:
            self._on_cleanup_select()

    def launch_disk_cleanup(self):
        success, output = open_disk_cleanup()
        if success:
            self.status_var.set("Disk Cleanup launched.")
        else:
            messagebox.showerror("Disk Cleanup", f"Could not launch Disk Cleanup.\n\n{output}")
