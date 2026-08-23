"""Password generator page."""

import secrets
import string
import tkinter as tk
from tkinter import ttk


class PasswordMixin:
    def show_password(self):
        self._clear_content()
        self.current_page = "password"
        self._page_header(
            "Password Generator",
            "Generate random passwords for authorized IT administration use.",
        )

        panel = ttk.LabelFrame(self.content, text="Password Generator", padding=20)
        panel.pack(fill="x", pady=10)

        self.password_var = tk.StringVar()
        self.password_length = tk.IntVar(value=16)
        self.upper_var = tk.BooleanVar(value=True)
        self.lower_var = tk.BooleanVar(value=True)
        self.number_var = tk.BooleanVar(value=True)
        self.special_var = tk.BooleanVar(value=True)

        ttk.Entry(
            panel, textvariable=self.password_var, font=("Consolas", 14), width=55
        ).pack(pady=8)

        length_frame = ttk.Frame(panel)
        length_frame.pack(pady=8)
        ttk.Label(length_frame, text="Password length:").pack(side="left", padx=5)
        ttk.Spinbox(
            length_frame, from_=8, to=128, textvariable=self.password_length, width=8
        ).pack(side="left")

        options = ttk.LabelFrame(panel, text="Options", padding=10)
        options.pack(fill="x", pady=10)
        ttk.Checkbutton(options, text="Uppercase letters (A-Z)", variable=self.upper_var).pack(anchor="w")
        ttk.Checkbutton(options, text="Lowercase letters (a-z)", variable=self.lower_var).pack(anchor="w")
        ttk.Checkbutton(options, text="Numbers (0-9)", variable=self.number_var).pack(anchor="w")
        ttk.Checkbutton(options, text="Special characters", variable=self.special_var).pack(anchor="w")

        buttons = ttk.Frame(panel)
        buttons.pack(pady=10)
        ttk.Button(
            buttons, text="🔄 Generate", command=self.generate_password,
            style="Action.TButton"
        ).pack(side="left", padx=5)
        ttk.Button(
            buttons, text="📋 Copy", command=self.copy_password,
            style="Action.TButton"
        ).pack(side="left", padx=5)

        self.password_status = tk.StringVar()
        ttk.Label(panel, textvariable=self.password_status).pack(pady=5)

        self.generate_password()

    def generate_password(self):
        characters = ""
        if self.upper_var.get():
            characters += string.ascii_uppercase
        if self.lower_var.get():
            characters += string.ascii_lowercase
        if self.number_var.get():
            characters += string.digits
        if self.special_var.get():
            characters += "!@#$%^&*()-_=+"

        if not characters:
            self.password_var.set("")
            self.password_status.set("Select at least one option.")
            return

        try:
            length = int(self.password_length.get())
        except (ValueError, tk.TclError):
            self.password_status.set("Invalid password length.")
            return

        if length < 8 or length > 128:
            self.password_status.set("Length must be between 8 and 128.")
            return

        self.password_var.set(
            "".join(secrets.choice(characters) for _ in range(length))
        )
        self.password_status.set("Password generated.")

    def copy_password(self):
        password = self.password_var.get()
        if not password:
            return
        self.clipboard_clear()
        self.clipboard_append(password)
        self.update()
        self.password_status.set("Password copied to clipboard.")
