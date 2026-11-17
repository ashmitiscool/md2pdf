"""Local HTTP server for Gemini PDF Studio (static UI + sample loader + vector PDF export)."""

from __future__ import annotations

import argparse
import json
import mimetypes
import re
from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import unquote

from pdf_engine import APP_DIR, SAMPLE_TXT_PATH, html_to_pdf_bytes, read_text_auto

# Register .woff2 explicitly in case Windows registry lacks it
mimetypes.add_type("font/woff2", ".woff2")
mimetypes.add_type("text/javascript", ".js")
mimetypes.add_type("text/css", ".css")


def sanitize_filename(name: str) -> str:
    """Restrict download filenames to safe ASCII characters ending in .pdf."""
    stem = Path(name or "Gemini_Document.pdf").stem
    clean = re.sub(r"[^A-Za-z0-9._-]+", "_", stem).strip("._") or "Gemini_Document"
    return f"{clean[:80]}.pdf"


class StudioHandler(BaseHTTPRequestHandler):
    """Serves the local studio UI and handles sample loading + PDF export."""

    server_version = "GeminiPDFStudio/1.0"

    def log_message(self, format: str, *args) -> None:  # noqa: A002
        return

    def _send_json(self, status: int, payload: dict) -> None:
        data = json.dumps(payload).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(data)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(data)

    def _serve_file(self, rel_path: str) -> None:
        clean_rel = unquote(rel_path.split("?", 1)[0]).lstrip("/") or "index.html"
        target = (APP_DIR / clean_rel).resolve()
        if not str(target).startswith(str(APP_DIR.resolve())) or not target.is_file():
            self.send_error(HTTPStatus.NOT_FOUND, "File not found")
            return

        ctype, _ = mimetypes.guess_type(str(target))
        content = target.read_bytes()
        self.send_response(HTTPStatus.OK)
        self.send_header("Content-Type", ctype or "application/octet-stream")
        self.send_header("Content-Length", str(len(content)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(content)

    def do_GET(self) -> None:  # noqa: N802
        path = self.path.split("?", 1)[0]
        if path == "/api/health":
            self._send_json(HTTPStatus.OK, {"status": "ok"})
            return

        if path == "/api/sample":
            if SAMPLE_TXT_PATH.is_file():
                text = read_text_auto(SAMPLE_TXT_PATH)
                self._send_json(
                    HTTPStatus.OK,
                    {"filename": SAMPLE_TXT_PATH.name, "content": text},
                )
            else:
                self._send_json(HTTPStatus.NOT_FOUND, {"error": "Sample file not found"})
            return

        self._serve_file(path)

    def do_POST(self) -> None:  # noqa: N802
        path = self.path.split("?", 1)[0]
        if path != "/api/export-pdf":
            self.send_error(HTTPStatus.NOT_FOUND, "Unknown endpoint")
            return

        try:
            length = int(self.headers.get("Content-Length", "0"))
        except ValueError:
            self._send_json(HTTPStatus.BAD_REQUEST, {"error": "Invalid Content-Length"})
            return

        if length <= 0 or length > 20 * 1024 * 1024:
            self._send_json(HTTPStatus.BAD_REQUEST, {"error": "Payload empty or too large"})
            return

        try:
            body = json.loads(self.rfile.read(length).decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError) as exc:
            self._send_json(HTTPStatus.BAD_REQUEST, {"error": f"Invalid JSON: {exc}"})
            return

        html = body.get("html")
        if not isinstance(html, str) or not html.strip():
            self._send_json(HTTPStatus.BAD_REQUEST, {"error": "Missing html field"})
            return

        filename = sanitize_filename(str(body.get("filename", "Gemini_Document.pdf")))

        try:
            pdf_bytes = html_to_pdf_bytes(html)
        except Exception as exc:  # noqa: BLE001
            self._send_json(HTTPStatus.INTERNAL_SERVER_ERROR, {"error": str(exc)})
            return

        self.send_response(HTTPStatus.OK)
        self.send_header("Content-Type", "application/pdf")
        self.send_header("Content-Disposition", f'attachment; filename="{filename}"')
        self.send_header("Content-Length", str(len(pdf_bytes)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(pdf_bytes)


def main() -> None:
    parser = argparse.ArgumentParser(description="Run Gemini PDF Studio server.")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8765)
    args = parser.parse_args()

    httpd = ThreadingHTTPServer((args.host, args.port), StudioHandler)
    print(f"Gemini PDF Studio running at http://{args.host}:{args.port}", flush=True)
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        httpd.server_close()


if __name__ == "__main__":
    main()
