"""Shared headless Chromium PDF rendering engine for md2pdf."""

from __future__ import annotations

import os
import shutil
import subprocess
import tempfile
from pathlib import Path

APP_DIR = Path(__file__).resolve().parent
VENDOR_DIR = APP_DIR / "vendor"
SAMPLE_TXT_PATH = APP_DIR / "sample.md"

BROWSER_CANDIDATES = [
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
]


def read_text_auto(path: Path) -> str:
    """Read text file supporting UTF-8, UTF-8-BOM, and Windows Ditto UTF-16 LE/BE."""
    raw = path.read_bytes()
    if raw.startswith(b"\xff\xfe") or raw.startswith(b"\xfe\xff"):
        return raw.decode("utf-16")
    if raw.startswith(b"\xef\xbb\xbf"):
        return raw.decode("utf-8-sig")
    try:
        return raw.decode("utf-8")
    except UnicodeDecodeError:
        return raw.decode("cp1252", errors="replace")


def find_chromium_binary() -> str:
    """Locate an installed Chrome or Edge binary capable of headless PDF export."""
    for candidate in BROWSER_CANDIDATES:
        if os.path.isfile(candidate):
            return candidate
    for cmd in ("chrome", "msedge", "chromium", "google-chrome"):
        found = shutil.which(cmd)
        if found:
            return found
    raise FileNotFoundError(
        "No Chrome or Microsoft Edge executable found for headless PDF generation."
    )


def resolve_local_katex_href(html: str) -> str:
    """Rewrite relative vendor/katex.min.css links to absolute local file:// URIs."""
    katex_uri = (VENDOR_DIR / "katex.min.css").as_uri()
    return html.replace('href="vendor/katex.min.css"', f'href="{katex_uri}"').replace(
        "href='/vendor/katex.min.css'", f'href="{katex_uri}"'
    )


def html_to_pdf_bytes(html: str, timeout_sec: int = 45) -> bytes:
    """Render a standalone HTML string to vector PDF bytes using headless Chromium."""
    browser = find_chromium_binary()
    resolved_html = resolve_local_katex_href(html)

    with tempfile.TemporaryDirectory(prefix="md2pdf_") as tmp:
        tmp_dir = Path(tmp)
        html_path = tmp_dir / "document.html"
        pdf_path = tmp_dir / "output.pdf"
        profile_dir = tmp_dir / "profile"
        profile_dir.mkdir(exist_ok=True)

        html_path.write_text(resolved_html, encoding="utf-8")

        cmd = [
            browser,
            "--headless=new",
            "--disable-gpu",
            "--no-sandbox",
            "--disable-extensions",
            "--allow-file-access-from-files",
            "--no-pdf-header-footer",
            "--virtual-time-budget=3000",
            f"--user-data-dir={profile_dir}",
            f"--print-to-pdf={pdf_path}",
            html_path.as_uri(),
        ]
        proc = subprocess.run(
            cmd,
            capture_output=True,
            text=True,
            timeout=timeout_sec,
            check=False,
        )

        if not pdf_path.is_file() or pdf_path.stat().st_size == 0:
            raise RuntimeError(
                f"Headless PDF generation failed (exit {proc.returncode}): {proc.stderr.strip()}"
            )

        return pdf_path.read_bytes()
