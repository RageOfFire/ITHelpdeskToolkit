"""Helper for any page that hasn't been built out yet.

Every sidebar section now has a real implementation — this class just
keeps the placeholder helper around for whenever the next one starts
here before getting its own module (see pages/excel.py, pages/apps.py,
pages/system_repair.py, pages/network_repair.py, pages/cleanup.py for
that pattern).
"""

from tkinter import ttk


class PlaceholderMixin:
    def _show_placeholder(self, title, subtitle):
        self._clear_content()
        self.current_page = title.lower().replace(" ", "_")
        self._page_header(title, subtitle)
        ttk.Label(
            self.content,
            text="This section hasn't been built yet.",
            font=("Segoe UI", 11),
        ).pack(anchor="w", pady=20)
