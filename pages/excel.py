"""Excel Troubleshooter page.

Runs a small, safe set of fixes for the most common Excel complaints:
crashes/freezes on startup, stuck lock files, a broken toolbar/ribbon,
a bloated recent-files list, and corrupted Office program files.
"""

import threading
import tkinter as tk
from tkinter import filedialog, messagebox, ttk

from services.excel_repair import EXCEL_FIXES, convert_multiple_xls, open_excel_safe_mode


class ExcelMixin:
    def show_excel(self):
        self._clear_content()
        self.current_page = "excel"
        self._page_header(
            "Excel Troubleshooter",
            "Quick fixes for Excel crashes, slow startup, stuck add-ins and "
            "corrupted settings.",
        )

        actions = ttk.Frame(self.content)
        actions.pack(fill="x", pady=(0, 10))

        self.excel_run_all_button = ttk.Button(
            actions, text="▶ Run All Fixes", command=self.run_all_excel_fixes,
            style="Action.TButton"
        )
        self.excel_run_all_button.pack(side="left", padx=(0, 8))
        ttk.Button(
            actions, text="▶ Run Selected", command=self.run_selected_excel_fix,
            style="Action.TButton"
        ).pack(side="left", padx=(0, 8))
        ttk.Button(
            actions, text="📄 Convert .xls to .xlsx", command=self.convert_xls_to_xlsx_dialog,
            style="Action.TButton"
        ).pack(side="left", padx=(0, 8))
        ttk.Button(
            actions, text="🛟 Open Excel Safe Mode", command=self.launch_excel_safe_mode,
            style="Action.TButton"
        ).pack(side="left")


        table_frame = ttk.Frame(self.content)
        table_frame.pack(fill="x", pady=10)

        self.excel_tree = ttk.Treeview(
            table_frame, columns=("fix", "status"), show="headings",
            height=len(EXCEL_FIXES)
        )
        self.excel_tree.heading("fix", text="Fix")
        self.excel_tree.heading("status", text="Status")
        self.excel_tree.column("fix", width=400)
        self.excel_tree.column("status", width=160, anchor="center")
        self.excel_tree.pack(side="left", fill="x", expand=True)

        scroll = ttk.Scrollbar(table_frame, orient="vertical", command=self.excel_tree.yview)
        scroll.pack(side="right", fill="y")
        self.excel_tree.configure(yscrollcommand=scroll.set)

        self.excel_tree.tag_configure("done", foreground="#008000")
        self.excel_tree.tag_configure("failed", foreground="#CC0000")
        self.excel_tree.tag_configure("pending", foreground="#777777")

        for index, (label, _description, _func, _admin) in enumerate(EXCEL_FIXES):
            self.excel_tree.insert(
                "", "end", iid=str(index), values=(label, "NOT RUN"), tags=("pending",)
            )

        self.excel_tree.bind("<<TreeviewSelect>>", self._on_excel_select)

        ttk.Label(
            self.content, text="Details", font=("Segoe UI", 12, "bold")
        ).pack(anchor="w", pady=(10, 4))
        self.excel_detail = tk.Text(
            self.content, height=13, font=("Consolas", 10), wrap=tk.WORD
        )
        self.excel_detail.pack(fill="both", expand=True)
        self.excel_detail.insert(
            tk.END,
            "Select a fix below and click 'Run Selected', or click "
            "'Run All Fixes' to apply everything.\n\n"
            "Each fix only touches the current Windows user profile and is "
            "safe to run repeatedly. Close Excel before running these fixes."
        )

        self.excel_results = {}

    def _on_excel_select(self, event=None):
        selected = self.excel_tree.selection()
        if not selected:
            return
        index = int(selected[0])
        label, description, _func, admin = EXCEL_FIXES[index]
        result = self.excel_results.get(index)

        text = f"{label}\n" + "=" * 60 + f"\n\n{description}\n"
        if admin:
            text += "\n(May require administrator privileges — a UAC prompt can appear.)\n"
        if result:
            text += (
                "\n" + "-" * 60 +
                f"\nResult: {'SUCCESS' if result['success'] else 'FAILED'}\n{result['output']}\n"
            )

        self.excel_detail.delete("1.0", tk.END)
        self.excel_detail.insert(tk.END, text)

    def run_selected_excel_fix(self):
        selected = self.excel_tree.selection()
        if not selected:
            messagebox.showinfo("Select a Fix", "Select a fix from the list first.")
            return
        self._run_excel_fixes([int(selected[0])])

    def run_all_excel_fixes(self):
        answer = messagebox.askyesno(
            "Run All Fixes",
            "This will run all Excel fixes, including disabling add-ins and "
            "resetting the toolbar/ribbon. Make sure Excel is closed first.\n\n"
            "Continue?"
        )
        if not answer:
            return
        self._run_excel_fixes(list(range(len(EXCEL_FIXES))))

    def _run_excel_fixes(self, indexes):
        self.excel_run_all_button.config(state=tk.DISABLED)
        self.status_var.set("Running Excel fixes...")
        for index in indexes:
            label = EXCEL_FIXES[index][0]
            self.excel_tree.item(str(index), values=(label, "RUNNING..."), tags=("pending",))
        threading.Thread(target=self._excel_worker, args=(indexes,), daemon=True).start()

    def _excel_worker(self, indexes):
        for index in indexes:
            _label, _description, func, _admin = EXCEL_FIXES[index]
            try:
                success, output = func()
            except Exception as exc:
                success, output = False, str(exc)
            self.excel_results[index] = {"success": success, "output": output}
            self.after(0, lambda i=index, ok=success: self._update_excel_status(i, ok))
        self.after(0, self._excel_finished)

    def _update_excel_status(self, index, success):
        if not self.excel_tree.winfo_exists():
            return
        label = EXCEL_FIXES[index][0]
        self.excel_tree.item(
            str(index),
            values=(label, "✓ DONE" if success else "✗ FAILED"),
            tags=("done" if success else "failed"),
        )

    def _excel_finished(self):
        if not self.excel_tree.winfo_exists():
            return
        self.excel_run_all_button.config(state=tk.NORMAL)
        done = sum(1 for r in self.excel_results.values() if r["success"])
        failed = len(self.excel_results) - done
        self.status_var.set(f"Excel fixes complete — {done} succeeded, {failed} failed.")
        selected = self.excel_tree.selection()
        if selected:
            self._on_excel_select()

    def launch_excel_safe_mode(self):
        success, output = open_excel_safe_mode()
        if success:
            self.status_var.set("Excel Safe Mode launch requested.")
        else:
            messagebox.showerror("Excel Safe Mode", f"Could not launch Excel Safe Mode.\n\n{output}")

    def convert_xls_to_xlsx_dialog(self):
        files = filedialog.askopenfilenames(
            title="Select .xls file(s) to convert to .xlsx",
            filetypes=[("Excel 97-2003 Workbook (*.xls)", "*.xls"), ("All Files", "*.*")],
        )
        if not files:
            return

        self.status_var.set(f"Converting {len(files)} .xls file(s) to .xlsx...")
        self.excel_detail.delete("1.0", tk.END)
        self.excel_detail.insert(tk.END, f"Starting conversion of {len(files)} .xls file(s)...\n\n")

        def worker():
            ok, summary, details = convert_multiple_xls(files)
            self.after(0, lambda: self._on_xls_conversion_done(ok, summary, details))

        threading.Thread(target=worker, daemon=True).start()

    def _on_xls_conversion_done(self, ok, summary, details):
        self.status_var.set(f"Conversion complete: {summary}")
        self.excel_detail.delete("1.0", tk.END)
        self.excel_detail.insert(
            tk.END,
            f"File Conversion Results\n{'=' * 60}\n{summary}\n\n{details}"
        )
        if ok:
            messagebox.showinfo("Conversion Complete", summary)
        else:
            messagebox.showwarning(
                "Conversion Finished with Issues",
                f"{summary}\n\nCheck the Details box for per-file results."
            )

