"""IT Helpdesk Toolkit — entry point.

This is the file to point PyInstaller at, e.g.:
    pyinstaller --onefile --windowed --name HelpdeskToolkit main.py

PyInstaller follows the imports below and bundles every module under
app.py, services/ and pages/ into the single .exe — the split across
files does not change the one-exe build at all.
"""

from app import HelpdeskToolkit

if __name__ == "__main__":
    app = HelpdeskToolkit()
    app.mainloop()
