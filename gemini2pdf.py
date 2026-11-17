"""CLI converter turning Gemini .txt/.md files into vector PDFs using the shared renderer."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from pdf_engine import APP_DIR, VENDOR_DIR, html_to_pdf_bytes, read_text_auto


def build_cli_html(raw_text: str, theme: str, paper: str, margins: str, font_size: int) -> str:
    """Create a self-executing HTML runner that uses the exact shared JS renderer."""
    katex_css = (VENDOR_DIR / "katex.min.css").as_uri()
    marked_js = (VENDOR_DIR / "marked.min.js").as_uri()
    katex_js = (VENDOR_DIR / "katex.min.js").as_uri()
    renderer_js = (APP_DIR / "gemini-renderer.js").as_uri()
    payload_json = json.dumps(
        {
            "rawText": raw_text,
            "theme": theme,
            "paper": paper,
            "margins": margins,
            "fontSize": font_size,
        }
    ).replace("</", "<\\/")

    return f"""<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <title>Gemini PDF Export</title>
  <link rel="stylesheet" href="{katex_css}" />
  <style id="docCss"></style>
</head>
<body>
  <article class="gemini-doc" id="docRoot"></article>
  <script src="{marked_js}"></script>
  <script src="{katex_js}"></script>
  <script src="{renderer_js}"></script>
  <script>
    (function() {{
      var cfg = {payload_json};
      document.getElementById('docCss').textContent = GeminiRenderer.getDocumentStyles({{
        theme: cfg.theme,
        paper: cfg.paper,
        margins: cfg.margins,
        fontSize: cfg.fontSize
      }});
      var res = GeminiRenderer.renderGeminiMarkdown(cfg.rawText, {{
        reconstructLists: true,
        cleanInvisible: true
      }});
      var root = document.getElementById('docRoot');
      root.innerHTML = res.html;
      document.title = res.title;
      function fit() {{
        GeminiRenderer.fitWideEquations(root);
      }}
      fit();
      if (document.fonts && document.fonts.ready) {{
        document.fonts.ready.then(fit);
      }}
    }})();
  </script>
</body>
</html>"""


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Convert a Gemini .txt or .md export into a vector PDF."
    )
    parser.add_argument("input_file", help="Path to the .txt or .md file copied from Gemini")
    parser.add_argument("output_file", nargs="?", help="Optional output .pdf path")
    parser.add_argument("--theme", choices=["dark", "light"], default="dark")
    parser.add_argument("--paper", choices=["A4", "Letter"], default="A4")
    parser.add_argument("--margins", choices=["compact", "normal", "academic"], default="normal")
    parser.add_argument("--font-size", type=int, default=15)
    args = parser.parse_args()

    in_path = Path(args.input_file).resolve()
    if not in_path.is_file():
        print(f"Error: Input file not found: {in_path}", file=sys.stderr)
        return 1

    out_path = (
        Path(args.output_file).resolve()
        if args.output_file
        else in_path.with_suffix(".pdf")
    )

    raw_text = read_text_auto(in_path)
    runner_html = build_cli_html(
        raw_text=raw_text,
        theme=args.theme,
        paper=args.paper,
        margins=args.margins,
        font_size=args.font_size,
    )
    pdf_bytes = html_to_pdf_bytes(runner_html)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_bytes(pdf_bytes)
    print(f"Saved PDF ({len(pdf_bytes):,} bytes): {out_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
