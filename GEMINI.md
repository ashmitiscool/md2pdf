# md2pdf

Offline-first 64-bit Windows desktop application turning Markdown, LaTeX math, Mermaid diagrams, and copied Gemini responses into vector PDFs. Built with WinForms + Edge WebView2 (`csc.exe`), vanilla HTML/JS (`marked`, `KaTeX`, `beautiful-mermaid`), and a Python 3 (stdlib) headless Chrome/Edge PDF backend.

## Commands

- **Requirements:** Windows 10/11 (x64) + Edge WebView2 Runtime, Python 3 (stdlib only for runtime), and Google Chrome or Microsoft Edge (for `pdf_engine.py`). No `package.json` or `pyproject.toml`.
- **Run Desktop App:** `.\md2pdf.exe` (or `.\md2pdf.exe path\to\notes.md`, or `.\Launch-md2pdf.bat`)
- **Build Windows App:** `powershell -ExecutionPolicy Bypass -File .\build.ps1` (compiles `src/Program.cs` with .NET 4.0 `csc.exe` into `md2pdf.exe`)
- **Desktop Self-Test:** `.\md2pdf.exe --self-test "$env:TEMP\md2pdf_test\out.pdf"` (or pass a `.md` file with equations; renders the document, exports via both `pdf_engine.py` and WebView2 `PrintToPdfAsync`, and writes `selftest_report.json` alongside `out.pdf`; exits `0` on pass)
- **Capture Screenshots:** `.\md2pdf.exe --capture-screenshots .\screenshots` (captures `app-dark.png` and `app-light.png` in the target directory)
- **Lint / Unit Tests:** None configured in-repo.

## Architecture

- `src/Program.cs`: WinForms + Edge WebView2 host (`Md2PdfDesktop`), mapping `https://studio.local/` to the executable directory (`_appDir`), managing native menus/dialogs, settings (`settings.json`), file watcher, dual PDF export dispatch, `--self-test`, and `--capture-screenshots`.
- `desktop_ui.html`: Desktop UI shell loaded at `https://studio.local/desktop_ui.html` (split/editor/preview panes, toolbar, Find & Replace, Document Outline, drag-and-drop, and `window.DesktopHostApp` bridge).
- `gemini-renderer.js` (`GeminiRenderer`): Single source of truth for invisible-character cleanup (`normalizeGeminiText`), flattened Gemini sub-list reconstruction (`reconstructGeminiLists`), Markdown + KaTeX + Mermaid SVG rendering (`renderGeminiMarkdown`), print/screen CSS (`getDocumentStyles`), overflow equation scaling (`fitWideEquations`), and standalone HTML generation (`buildStandaloneHtml`).
- `vendor/`: Bundled offline assets (`marked.min.js`, `katex.min.js`, `katex.min.css`, `beautiful-mermaid.min.js`, and `vendor/fonts/*.woff2`).
- `desktop_backend.py`: Python JSON-over-stdout bridge invoked by `md2pdf.exe` implementing `read`, `sample`, `export-pdf`, and `batch-export`.
- `pdf_engine.py`: Backend for encoding-aware text reading (`read_text_auto`: UTF-16 LE/BE, UTF-8-BOM, UTF-8, cp1252 fallback), self-executing HTML batch runner generation (`build_cli_html`), and headless Chrome/Edge `--print-to-pdf` generation (`html_to_pdf_bytes`).
- `build.ps1`, `generate_icon.py`, `app.ico`, `Launch-md2pdf.bat`: Build script, icon generator/asset, and launcher batch file.

## Conventions & Gotchas

- **Shared rendering parity:** Keep Markdown/LaTeX/Mermaid/CSS rendering logic in `gemini-renderer.js` and `build_cli_html` (`pdf_engine.py`) rather than duplicating it in `desktop_ui.html`.
- **KaTeX link rewriting:** `desktop_backend.py` first strips `https://studio.local/` from `https://studio.local/vendor/katex.min.css` back to `vendor/katex.min.css`, and `pdf_engine.resolve_local_katex_href` rewrites `vendor/katex.min.css` to a local `file://` URI. If you change the KaTeX stylesheet `href` in `desktop_ui.html`, update those rewrites to match.
- **Desktop build dependencies:** `build.ps1` uses `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` (C# 5 syntax only) and falls back to copying `Microsoft.Web.WebView2.*.dll` / `WebView2Loader.dll` from `C:\Program Files\Listary\` if missing locally. `generate_icon.py` requires `Pillow` (`PIL`), though it only runs if `app.ico` is absent.
- **Desktop runtime expectations:** `md2pdf.exe` resolves `desktop_backend.py`, `desktop_ui.html`, `gemini-renderer.js`, `pdf_engine.py`, and `vendor/` directly in its executable directory (`_appDir`), and looks for `python` on `PATH` (or `C:\Python312`–`314` / `%LocalAppData%\Programs\Python\...`) to run `desktop_backend.py`.
- **Missing sample file:** `desktop_backend.py` (`sample`) reads `pdf_engine.SAMPLE_TXT_PATH` (`sample.md` in the repo root), which is not currently checked into the repository; when absent, `desktop_ui.html` falls back to `FALLBACK_SAMPLE`.
- **Ignored runtime/build outputs:** `webview2_data/`, `settings.json`, `dist/`, and `__pycache__/` are gitignored; avoid committing local WebView2 profiles or settings.
