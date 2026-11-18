# md2pdf

Offline-first converter turning Markdown, LaTeX math, Mermaid diagrams, and copied Gemini responses into vector PDFs via a CLI, a local web studio, and a 64-bit Windows desktop app. Uses Python 3 (stdlib), vanilla HTML/JS (`marked`, `KaTeX`, `beautiful-mermaid`), headless Chrome/Edge for PDF printing, and WinForms + Edge WebView2 (`csc.exe`) for the desktop executable.

## Commands

- **Requirements:** Python 3 (stdlib only for runtime), Google Chrome or Microsoft Edge (for `pdf_engine.py`), and Windows 10/11 + Edge WebView2 Runtime (for `windows_app/md2pdf.exe`). No `package.json` or `pyproject.toml`.
- **Web Studio (dev/run):** `python server.py --port 8765` (or double-click `Start-md2pdf.bat`) -> `http://127.0.0.1:8765`
- **CLI convert:** `python md2pdf.py input.md output.pdf --theme dark --paper A4 --margins normal --font-size 15`
  - Choices: `--theme` (`dark`, `light`), `--paper` (`A4`, `Letter`), `--margins` (`compact`, `normal`, `academic`), `--font-size` (int, default `15`)
- **Build Windows app:** `powershell -ExecutionPolicy Bypass -File .\windows_app\build.ps1` (compiles `windows_app/src/Program.cs` with .NET 4.0 `csc.exe` into `windows_app/md2pdf.exe`)
- **Desktop self-test:** `.\windows_app\md2pdf.exe path\to\input.md --self-test path\to\out.pdf` (renders the file, exports via both `pdf_engine.py` and WebView2 `PrintToPdfAsync`, and writes `selftest_report.json` alongside `out.pdf`; exits `0` on pass)
- **Lint / unit tests:** None configured in-repo.

## Architecture

- `gemini-renderer.js` (`GeminiRenderer`): Single source of truth shared across all three entry points for invisible-character cleanup (`normalizeGeminiText`), flattened Gemini sub-list reconstruction (`reconstructGeminiLists`), Markdown + KaTeX + Mermaid SVG rendering (`renderGeminiMarkdown`), print/screen CSS (`getDocumentStyles`), overflow equation scaling (`fitWideEquations`), and standalone HTML generation (`buildStandaloneHtml`).
- `vendor/`: Bundled offline assets (`marked.min.js`, `katex.min.js`, `katex.min.css`, `beautiful-mermaid.min.js`, and `vendor/fonts/*.woff2`).
- `pdf_engine.py`: Shared backend for encoding-aware text reading (`read_text_auto`: UTF-16 LE/BE, UTF-8-BOM, UTF-8, cp1252 fallback) and headless Chrome/Edge `--print-to-pdf` generation (`html_to_pdf_bytes`).
- `server.py` + `index.html`: Local `ThreadingHTTPServer` web studio serving static files, `GET /api/health`, `GET /api/sample`, and `POST /api/export-pdf`.
- `md2pdf.py`: CLI wrapper that injects `vendor/` + `gemini-renderer.js` file URIs into a self-executing HTML runner (`build_cli_html`) and passes it to `pdf_engine.html_to_pdf_bytes`.
- `windows_app/`: WinForms + WebView2 host (`src/Program.cs`), UI (`desktop_ui.html`), and Python JSON-over-stdout bridge (`desktop_backend.py` implementing `read`, `sample`, `export-pdf`, `batch-export`). `Program.cs` maps `https://studio.local/` to the parent repo root so `desktop_ui.html` consumes the shared `vendor/` and `gemini-renderer.js` without duplication.

## Conventions & Gotchas

- **Shared rendering parity:** Keep Markdown/LaTeX/Mermaid/CSS rendering logic in `gemini-renderer.js` and `build_cli_html` (`md2pdf.py`) rather than duplicating it in `index.html` or `windows_app/desktop_ui.html`.
- **KaTeX link rewriting:** `pdf_engine.resolve_local_katex_href` rewrites `vendor/katex.min.css` to a local `file://` URI, and `windows_app/desktop_backend.py` first strips `https://studio.local/` back to `vendor/katex.min.css`. If you change the KaTeX stylesheet `href` in `index.html` or `desktop_ui.html`, update those rewrites to match.
- **Desktop build dependencies:** `windows_app/build.ps1` uses `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` and falls back to copying `Microsoft.Web.WebView2.*.dll` / `WebView2Loader.dll` from `C:\Program Files\Listary\` if missing locally. `windows_app/generate_icon.py` requires `Pillow` (`PIL`), though it only runs if `windows_app/app.ico` is absent.
- **Desktop runtime expectations:** `windows_app/md2pdf.exe` resolves the repo root as `..` from its executable directory and looks for `python` on `PATH` (or `C:\Python312`–`314` / `%LocalAppData%\Programs\Python\...`) to run `desktop_backend.py`.
- **Missing sample file:** Both `server.py` (`GET /api/sample`) and `desktop_backend.py` (`sample`) read `pdf_engine.SAMPLE_TXT_PATH` (`sample.md` in the repo root), which is not currently checked into the repository.
- **Ignored runtime/build outputs:** `windows_app/webview2_data/`, `windows_app/settings.json`, `dist/`, and `__pycache__/` are gitignored; avoid committing local WebView2 profiles or settings.
