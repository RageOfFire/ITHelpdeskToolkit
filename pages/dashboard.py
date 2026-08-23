"""Dashboard (home) page."""

import os
import platform
import socket
import tkinter as tk
from tkinter import ttk

from services.inventory import get_ip_address


class DashboardMixin:
    def show_dashboard(self):
        self._clear_content()
        self.current_page = "dashboard"
        self._page_header(
            "IT Helpdesk Toolkit",
            "A single application for common endpoint support tasks.",
        )

        cards = ttk.Frame(self.content)
        cards.pack(fill="x", pady=10)

        card_data = [
            ("Computer", socket.gethostname()),
            ("User", os.environ.get("USERNAME", "Unknown")),
            ("IP Address", get_ip_address()),
            ("OS", platform.system()),
        ]

        for label, value in card_data:
            frame = ttk.LabelFrame(cards, text=label, padding=15)
            frame.pack(side="left", fill="both", expand=True, padx=5)
            ttk.Label(frame, text=value, font=("Segoe UI", 12, "bold")).pack()

        ttk.Label(
            self.content, text="Quick Actions", font=("Segoe UI", 14, "bold")
        ).pack(anchor="w", pady=(25, 10))

        actions = ttk.Frame(self.content)
        actions.pack(anchor="w")

        ttk.Button(
            actions, text="🖥 Scan This PC", command=self.show_inventory,
            style="Action.TButton"
        ).pack(side="left", padx=(0, 8))
        ttk.Button(
            actions, text="🌐 Run Network Checks", command=self.show_network,
            style="Action.TButton"
        ).pack(side="left", padx=(0, 8))
        ttk.Button(
            actions, text="🛠 System Repair", command=self.show_system_repair,
            style="Action.TButton"
        ).pack(side="left", padx=(0, 8))
        ttk.Button(
            actions, text="📊 Excel Fixes", command=self.show_excel,
            style="Action.TButton"
        ).pack(side="left", padx=(0, 8))
        ttk.Button(
            actions, text="🖨 Check Printers", command=self.show_printer,
            style="Action.TButton"
        ).pack(side="left", padx=(0, 8))
        ttk.Button(
            actions, text="🔐 Generate Password", command=self.show_password,
            style="Action.TButton"
        ).pack(side="left")

        ttk.Label(
            self.content,
            text=(
                "\nRecommended workflow:\n"
                "1. Scan the endpoint → 2. Check connectivity → "
                "3. Troubleshoot printers if needed → 4. Document the ticket."
            ),
            foreground="#555555",
            justify="left",
        ).pack(anchor="w", pady=25)
