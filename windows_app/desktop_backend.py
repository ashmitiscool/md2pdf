"""Shared Python backend bridge for the md2pdf Windows Desktop App."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

ROOT_DIR = Path(__file__).resolve().parent.parent
if str(ROOT_DIR) not in sys.path:
    sys.path.insert(0, str(ROOT_DIR))

from md2pdf import build_cli_html  # noqa: E402
from pdf_engine import SAMPLE_TXT_PATH, html_to_pdf_bytes, read_text_auto  # noqa: E402


def _configure_stdio() -> None:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    if hasattr(sys.stderr, "reconfigure"):
        sys.stderr.reconfigure(encoding="utf-8")


def _emit(payload: dict, exit_code: int = 0) -> int:
    print(json.dumps(payload, ensure_ascii=False), flush=True)
    return exit_code


def _normalize_katex_links(html: str) -> str:
    return (
        html.replace('href="https://studio.local/vendor/katex.min.css"', 'href="vendor/katex.min.css"')
        .replace("href='https://studio.local/vendor/katex.min.css'", 'href="vendor/katex.min.css"')
    )


def cmd_read(file_path: str) -> int:
    path = Path(file_path).resolve()
    if not path.is_file():
        return _emit({"ok": False, "error": f"File not found: {path}"}, 1)
    content = read_text_auto(path)
    return _emit(
        {
            "ok": True,
            "filename": path.name,
            "path": str(path),
            "content": content,
        }
    )


def cmd_sample() -> int:
    if SAMPLE_TXT_PATH.is_file():
        content = read_text_auto(SAMPLE_TXT_PATH)
        return _emit(
            {
                "ok": True,
                "filename": SAMPLE_TXT_PATH.name,
                "path": str(SAMPLE_TXT_PATH),
                "content": content,
            }
        )
    return _emit({"ok": False, "error": "Sample file not found"})


def cmd_export_pdf(html_file: str, output_path: str) -> int:
    src = Path(html_file).resolve()
    if not src.is_file():
        return _emit({"ok": False, "error": f"HTML file not found: {src}"}, 1)

    html = _normalize_katex_links(src.read_text(encoding="utf-8"))
    pdf_bytes = html_to_pdf_bytes(html)

    out = Path(output_path).resolve()
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_bytes(pdf_bytes)
    return _emit({"ok": True, "output": str(out), "bytes": len(pdf_bytes)})


def cmd_batch_export(config_path: str) -> int:
    cfg_file = Path(config_path).resolve()
    if not cfg_file.is_file():
        return _emit({"ok": False, "error": f"Config file not found: {cfg_file}"}, 1)

    cfg = json.loads(cfg_file.read_text(encoding="utf-8"))
    files = cfg.get("files") or []
    output_dir = Path(cfg.get("outputDir") or ".").resolve()
    theme = str(cfg.get("theme") or "dark")
    paper = str(cfg.get("paper") or "A4")
    margins = str(cfg.get("margins") or "normal")
    font_size = int(cfg.get("fontSize") or 15)

    output_dir.mkdir(parents=True, exist_ok=True)
    results = []
    any_error = False

    for raw_file in files:
        in_path = Path(raw_file).resolve()
        if not in_path.is_file():
            any_error = True
            results.append(
                {"ok": False, "input": str(in_path), "error": "Input file not found"}
            )
            continue

        out_path = output_dir / f"{in_path.stem}.pdf"
        try:
            raw_text = read_text_auto(in_path)
            runner_html = build_cli_html(
                raw_text=raw_text,
                theme=theme,
                paper=paper,
                margins=margins,
                font_size=font_size,
            )
            pdf_bytes = html_to_pdf_bytes(runner_html)
            out_path.write_bytes(pdf_bytes)
            results.append(
                {
                    "ok": True,
                    "input": str(in_path),
                    "output": str(out_path),
                    "bytes": len(pdf_bytes),
                }
            )
        except Exception as exc:  # noqa: BLE001
            any_error = True
            results.append(
                {"ok": False, "input": str(in_path), "output": str(out_path), "error": str(exc)}
            )

    return _emit(
        {
            "ok": not any_error,
            "outputDir": str(output_dir),
            "total": len(files),
            "succeeded": sum(1 for r in results if r.get("ok")),
            "results": results,
        },
        0 if not any_error else 1,
    )


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Desktop backend bridge for md2pdf.")
    sub = parser.add_subparsers(dest="command", required=True)

    p_read = sub.add_parser("read", help="Read a text/markdown file with encoding detection.")
    p_read.add_argument("--file", required=True, help="Path to input file")

    sub.add_parser("sample", help="Read default sample file if available.")

    p_export = sub.add_parser("export-pdf", help="Render standalone HTML to vector PDF.")
    p_export.add_argument("--html-file", required=True, help="Path to standalone HTML file")
    p_export.add_argument("--output", required=True, help="Target PDF output path")

    p_batch = sub.add_parser("batch-export", help="Batch convert files to PDF.")
    p_batch.add_argument("--config", required=True, help="Path to JSON batch configuration")

    return parser


def main() -> int:
    _configure_stdio()
    parser = build_parser()
    args = parser.parse_args()

    try:
        if args.command == "read":
            return cmd_read(args.file)
        if args.command == "sample":
            return cmd_sample()
        if args.command == "export-pdf":
            return cmd_export_pdf(args.html_file, args.output)
        if args.command == "batch-export":
            return cmd_batch_export(args.config)
        return _emit({"ok": False, "error": f"Unknown command: {args.command}"}, 1)
    except Exception as exc:  # noqa: BLE001
        return _emit({"ok": False, "error": str(exc)}, 1)


if __name__ == "__main__":
    raise SystemExit(main())
