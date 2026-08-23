"""Asset inventory page."""

import csv
import threading
import tkinter as tk
from tkinter import filedialog, messagebox, ttk

from constants import APP_NAME, APP_VERSION
from services.inventory import collect_inventory


class InventoryMixin:
    def show_inventory(self):
        self._clear_content()
        self.current_page = "inventory"
        self._page_header(
            "Asset Inventory",
            "Collect hardware, Windows, storage and network information from this PC.",
        )

        actions = ttk.Frame(self.content)
        actions.pack(fill="x", pady=(0, 10))
        ttk.Button(
            actions, text="🔄 Scan This PC", command=self.scan_inventory,
            style="Action.TButton"
        ).pack(side="left", padx=(0, 8))
        ttk.Button(
            actions, text="💾 Export CSV", command=self.export_inventory_csv,
            style="Action.TButton"
        ).pack(side="left", padx=(0, 8))
        ttk.Button(
            actions, text="📄 Save Report", command=self.save_inventory_report,
            style="Action.TButton"
        ).pack(side="left")

        frame = ttk.Frame(self.content)
        frame.pack(fill="both", expand=True)
        self.inventory_tree = ttk.Treeview(
            frame, columns=("property", "value"), show="headings"
        )
        self.inventory_tree.heading("property", text="Property")
        self.inventory_tree.heading("value", text="Value")
        self.inventory_tree.column("property", width=250)
        self.inventory_tree.column("value", width=650)
        scroll = ttk.Scrollbar(frame, orient="vertical", command=self.inventory_tree.yview)
        self.inventory_tree.configure(yscrollcommand=scroll.set)
        self.inventory_tree.pack(side="left", fill="both", expand=True)
        scroll.pack(side="right", fill="y")
        self.scan_inventory()

    def scan_inventory(self):
        self.status_var.set("Scanning endpoint...")
        self.update_idletasks()

        def worker():
            try:
                data = collect_inventory()
                self.after(0, lambda: self._inventory_finished(data))
            except Exception as exc:
                self.after(0, lambda: messagebox.showerror("Inventory Error", str(exc)))
                self.after(0, lambda: self.status_var.set("Inventory scan failed."))

        threading.Thread(target=worker, daemon=True).start()

    def _inventory_finished(self, data):
        if not self.inventory_tree.winfo_exists():
            return
        self.inventory = data
        for item in self.inventory_tree.get_children():
            self.inventory_tree.delete(item)
        for key, value in data.items():
            self.inventory_tree.insert("", "end", values=(key, value))
        self.status_var.set(f"Inventory scan complete — {len(data)} properties collected.")

    def export_inventory_csv(self):
        if not self.inventory:
            messagebox.showinfo("No Data", "Run an inventory scan first.")
            return
        filename = filedialog.asksaveasfilename(
            defaultextension=".csv",
            filetypes=[("CSV files", "*.csv")],
            initialfile=f"{self.inventory.get('Computer Name', 'computer')}_inventory.csv",
        )
        if not filename:
            return
        with open(filename, "w", newline="", encoding="utf-8-sig") as file:
            writer = csv.writer(file)
            writer.writerow(["Property", "Value"])
            for key, value in self.inventory.items():
                writer.writerow([key, value])
        messagebox.showinfo("Export Complete", f"Inventory exported to:\n{filename}")

    def save_inventory_report(self):
        if not self.inventory:
            messagebox.showinfo("No Data", "Run an inventory scan first.")
            return
        filename = filedialog.asksaveasfilename(
            defaultextension=".txt",
            filetypes=[("Text files", "*.txt")],
            initialfile=f"{self.inventory.get('Computer Name', 'computer')}_inventory.txt",
        )
        if not filename:
            return
        with open(filename, "w", encoding="utf-8") as file:
            file.write(f"{APP_NAME} v{APP_VERSION}\n")
            file.write("=" * 65 + "\n\n")
            for key, value in self.inventory.items():
                file.write(f"{key}: {value}\n")
        messagebox.showinfo("Report Saved", f"Report saved to:\n{filename}")
