"""Application shell: window setup, sidebar navigation, shared style/layout
helpers. Each page's actual content lives in pages/*.py as a mixin —
this class just wires them all together.
"""

import ctypes
import tkinter as tk
from tkinter import ttk

from constants import APP_NAME, APP_VERSION
from pages.apps import AppsMixin
from pages.cleanup import CleanupMixin
from pages.dashboard import DashboardMixin
from pages.excel import ExcelMixin
from pages.inventory import InventoryMixin
from pages.network import NetworkMixin
from pages.network_repair import NetworkRepairMixin
from pages.password import PasswordMixin
from pages.placeholders import PlaceholderMixin
from pages.printer import PrinterMixin
from pages.system_repair import SystemRepairMixin


class HelpdeskToolkit(
    tk.Tk,
    DashboardMixin,
    InventoryMixin,
    NetworkMixin,
    PrinterMixin,
    PasswordMixin,
    ExcelMixin,
    AppsMixin,
    SystemRepairMixin,
    NetworkRepairMixin,
    CleanupMixin,
    PlaceholderMixin,
):
    def __init__(self):
        super().__init__()
        self.title(f"{APP_NAME} v{APP_VERSION}")
        self.geometry("1150x760")
        self.minsize(1000, 650)

        try:
            ctypes.windll.shcore.SetProcessDpiAwareness(1)
        except Exception:
            pass

        self.inventory = {}
        self.network_results = {}
        self.printers = []
        self.selected_printer = None
        self.current_page = None

        self._build_style()
        self._build_shell()
        self.show_dashboard()

    def _build_style(self):
        style = ttk.Style(self)
        try:
            style.theme_use("vista")
        except tk.TclError:
            pass

        style.configure("Title.TLabel", font=("Segoe UI", 22, "bold"))
        style.configure("Subtitle.TLabel", font=("Segoe UI", 10), foreground="#666666")
        style.configure("Nav.TButton", font=("Segoe UI", 10, "bold"), padding=9)
        style.configure("Action.TButton", font=("Segoe UI", 10, "bold"), padding=8)
        style.configure("Treeview", rowheight=28)
        style.configure("Treeview.Heading", font=("Segoe UI", 10, "bold"))

    def _build_shell(self):
        self.sidebar = ttk.Frame(self, padding=12)
        self.sidebar.pack(side="left", fill="y")

        ttk.Label(self.sidebar, text="IT HELPDESK", font=("Segoe UI", 15, "bold")).pack(
            pady=(5, 0)
        )
        ttk.Label(self.sidebar, text="TOOLKIT", font=("Segoe UI", 15, "bold")).pack(
            pady=(0, 15)
        )

        nav = [
            ("🏠 Dashboard", self.show_dashboard),
            ("🖥 Asset Inventory", self.show_inventory),
            ("🌐 Network Diagnostics", self.show_network),
            ("🔧 Network Repair", self.show_network_repair),
            ("🛠 Windows / File System Repair", self.show_system_repair),
            ("🧹 System Cleanup", self.show_cleanup),
            ("📊 Excel Troubleshooter", self.show_excel),
            ("📦 Application Fixes", self.show_apps),
            ("🖨 Printer Troubleshooter", self.show_printer),
            ("🔐 Password Generator", self.show_password),
        ]
        for text, command in nav:
            ttk.Button(
                self.sidebar, text=text, command=command,
                style="Nav.TButton", width=24
            ).pack(fill="x", pady=4)

        ttk.Separator(self.sidebar).pack(fill="x", pady=14)
        ttk.Label(
            self.sidebar,
            text=f"v{APP_VERSION}\nWindows Repair Edition",
            foreground="#666666",
            justify="center",
        ).pack(pady=8)

        self.content = ttk.Frame(self, padding=(10, 15, 20, 15))
        self.content.pack(side="left", fill="both", expand=True)

        self.status_var = tk.StringVar(value="Ready")
        ttk.Label(
            self,
            textvariable=self.status_var,
            relief="sunken",
            anchor="w",
            padding=5,
        ).pack(side="bottom", fill="x")

    def _clear_content(self):
        for widget in self.content.winfo_children():
            widget.destroy()

    def _page_header(self, title, subtitle):
        ttk.Label(self.content, text=title, style="Title.TLabel").pack(anchor="w")
        ttk.Label(self.content, text=subtitle, style="Subtitle.TLabel").pack(
            anchor="w", pady=(0, 15)
        )
