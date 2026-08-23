"""Application Fixes page.

Two tools in one page:
1. General fixes — system-wide problems that break many apps at once
   (frozen taskbar, broken icons, Store apps, fonts, search).
2. Target a Specific App — force-close, clear cache, open data folder,
   or relaunch-as-admin for one app the user names.
"""

import threading
import tkinter as tk
from tkinter import filedialog, messagebox, ttk

from services.app_repair import (
    GENERAL_APP_FIXES,
    clear_app_cache,
    force_close_app,
    launch_as_admin,
    open_app_data_folder,
)


class AppsMixin:
    def show_apps(self):
        self._clear_content()
        self.current_page = "apps"
        self._page_header(
            "Application Fixes",
            "Fix common Windows app problems, or target one specific app by name.",
        )

        # ---------------- General fixes ----------------
        ttk.Label(
            self.content, text="General Fixes", font=("Segoe UI", 13, "bold")
        ).pack(anchor="w", pady=(0, 4))

        actions = ttk.Frame(self.content)
        actions.pack(fill="x", pady=(0, 8))

        self.apps_run_all_button = ttk.Button(
            actions, text="▶ Run All Fixes", command=self.run_all_app_fixes,
            style="Action.TButton"
        )
        self.apps_run_all_button.pack(side="left", padx=(0, 8))
        ttk.Button(
            actions, text="▶ Run Selected", command=self.run_selected_app_fix,
            style="Action.TButton"
        ).pack(side="left")

        table_frame = ttk.Frame(self.content)
        table_frame.pack(fill="x", pady=(0, 10))

        self.apps_tree = ttk.Treeview(
            table_frame, columns=("fix", "status"), show="headings",
            height=len(GENERAL_APP_FIXES)
        )
        self.apps_tree.heading("fix", text="Fix")
        self.apps_tree.heading("status", text="Status")
        self.apps_tree.column("fix", width=400)
        self.apps_tree.column("status", width=160, anchor="center")
        self.apps_tree.pack(side="left", fill="x", expand=True)

        scroll = ttk.Scrollbar(table_frame, orient="vertical", command=self.apps_tree.yview)
        scroll.pack(side="right", fill="y")
        self.apps_tree.configure(yscrollcommand=scroll.set)

        self.apps_tree.tag_configure("done", foreground="#008000")
        self.apps_tree.tag_configure("failed", foreground="#CC0000")
        self.apps_tree.tag_configure("pending", foreground="#777777")

        for index, (label, _description, _func, _admin) in enumerate(GENERAL_APP_FIXES):
            self.apps_tree.insert(
                "", "end", iid=str(index), values=(label, "NOT RUN"), tags=("pending",)
            )

        self.apps_tree.bind("<<TreeviewSelect>>", self._on_app_fix_select)

        # ---------------- Target a specific app ----------------
        ttk.Separator(self.content).pack(fill="x", pady=12)

        ttk.Label(
            self.content, text="Target a Specific App", font=("Segoe UI", 13, "bold")
        ).pack(anchor="w", pady=(0, 4))
        ttk.Label(
            self.content,
            text="Enter a process name (e.g. 'chrome.exe') or app name (e.g. 'Spotify').",
            style="Subtitle.TLabel",
        ).pack(anchor="w", pady=(0, 6))

        target_frame = ttk.Frame(self.content)
        target_frame.pack(fill="x", pady=(0, 8))

        self.target_app_var = tk.StringVar()
        ttk.Entry(
            target_frame, textvariable=self.target_app_var, font=("Segoe UI", 10), width=35
        ).pack(side="left", padx=(0, 8))

        ttk.Button(
            target_frame, text="🛑 Force Close", command=self.force_close_target_app,
            style="Action.TButton"
        ).pack(side="left", padx=(0, 6))
        ttk.Button(
            target_frame, text="🧹 Clear Cache", command=self.clear_target_app_cache,
            style="Action.TButton"
        ).pack(side="left", padx=(0, 6))
        ttk.Button(
            target_frame, text="📂 Open Data Folder", command=self.open_target_app_folder,
            style="Action.TButton"
        ).pack(side="left", padx=(0, 6))
        ttk.Button(
            target_frame, text="🛡 Launch as Admin...", command=self.launch_target_app_as_admin,
            style="Action.TButton"
        ).pack(side="left")

        # ---------------- Shared details panel ----------------
        ttk.Label(
            self.content, text="Details", font=("Segoe UI", 12, "bold")
        ).pack(anchor="w", pady=(10, 4))
        self.apps_detail = tk.Text(
            self.content, height=10, font=("Consolas", 10), wrap=tk.WORD
        )
        self.apps_detail.pack(fill="both", expand=True)
        self.apps_detail.insert(
            tk.END,
            "General fixes: select one and click 'Run Selected', or 'Run All Fixes' "
            "to apply everything.\n\n"
            "Targeted tools: type an app or process name above, then pick an action. "
            "'Clear Cache' only removes Cache/Temp/Logs subfolders — app settings and "
            "logins are left alone."
        )

        self.app_fix_results = {}

    # ---------------- General fixes ----------------

    def _on_app_fix_select(self, event=None):
        selected = self.apps_tree.selection()
        if not selected:
            return
        index = int(selected[0])
        label, description, _func, admin = GENERAL_APP_FIXES[index]
        result = self.app_fix_results.get(index)

        text = f"{label}\n" + "=" * 60 + f"\n\n{description}\n"
        if admin:
            text += "\n(May require administrator privileges — a UAC prompt can appear.)\n"
        if result:
            text += (
                "\n" + "-" * 60 +
                f"\nResult: {'SUCCESS' if result['success'] else 'FAILED'}\n{result['output']}\n"
            )

        self.apps_detail.delete("1.0", tk.END)
        self.apps_detail.insert(tk.END, text)

    def run_selected_app_fix(self):
        selected = self.apps_tree.selection()
        if not selected:
            messagebox.showinfo("Select a Fix", "Select a fix from the list first.")
            return
        self._run_app_fixes([int(selected[0])])

    def run_all_app_fixes(self):
        answer = messagebox.askyesno(
            "Run All Fixes",
            "This will restart Explorer, reset Store apps, and may restart other "
            "system services. Save any open work first.\n\nContinue?"
        )
        if not answer:
            return
        self._run_app_fixes(list(range(len(GENERAL_APP_FIXES))))

    def _run_app_fixes(self, indexes):
        self.apps_run_all_button.config(state=tk.DISABLED)
        self.status_var.set("Running application fixes...")
        for index in indexes:
            label = GENERAL_APP_FIXES[index][0]
            self.apps_tree.item(str(index), values=(label, "RUNNING..."), tags=("pending",))
        threading.Thread(target=self._app_fix_worker, args=(indexes,), daemon=True).start()

    def _app_fix_worker(self, indexes):
        for index in indexes:
            _label, _description, func, _admin = GENERAL_APP_FIXES[index]
            try:
                success, output = func()
            except Exception as exc:
                success, output = False, str(exc)
            self.app_fix_results[index] = {"success": success, "output": output}
            self.after(0, lambda i=index, ok=success: self._update_app_fix_status(i, ok))
        self.after(0, self._app_fixes_finished)

    def _update_app_fix_status(self, index, success):
        if not self.apps_tree.winfo_exists():
            return
        label = GENERAL_APP_FIXES[index][0]
        self.apps_tree.item(
            str(index),
            values=(label, "✓ DONE" if success else "✗ FAILED"),
            tags=("done" if success else "failed"),
        )

    def _app_fixes_finished(self):
        if not self.apps_tree.winfo_exists():
            return
        self.apps_run_all_button.config(state=tk.NORMAL)
        done = sum(1 for r in self.app_fix_results.values() if r["success"])
        failed = len(self.app_fix_results) - done
        self.status_var.set(f"Application fixes complete — {done} succeeded, {failed} failed.")
        selected = self.apps_tree.selection()
        if selected:
            self._on_app_fix_select()

    # ---------------- Targeted app tools ----------------

    def _show_target_result(self, title, success, output):
        self.apps_detail.delete("1.0", tk.END)
        self.apps_detail.insert(
            tk.END,
            f"{title}\n" + "=" * 60 +
            f"\n\nResult: {'SUCCESS' if success else 'FAILED'}\n{output}"
        )
        self.status_var.set(title + (" complete." if success else " failed."))

    def force_close_target_app(self):
        name = self.target_app_var.get()
        success, output = force_close_app(name)
        self._show_target_result(f"Force Close: {name}", success, output)

    def clear_target_app_cache(self):
        name = self.target_app_var.get()
        success, output = clear_app_cache(name)
        self._show_target_result(f"Clear Cache: {name}", success, output)

    def open_target_app_folder(self):
        name = self.target_app_var.get()
        success, output = open_app_data_folder(name)
        self._show_target_result(f"Open Data Folder: {name}", success, output)

    def launch_target_app_as_admin(self):
        exe_path = filedialog.askopenfilename(
            title="Choose an application",
            filetypes=[("Applications", "*.exe"), ("All files", "*.*")],
        )
        if not exe_path:
            return
        success, output = launch_as_admin(exe_path)
        self._show_target_result(f"Launch as Admin: {exe_path}", success, output)
