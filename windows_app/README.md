# md2pdf — Windows Desktop App

A native 64-bit Windows desktop application (`md2pdf.exe`) built with WinForms and Microsoft Edge WebView2 inside `windows_app/`, leaving the root web-based studio (`../index.html`, `../server.py`, `../Start-md2pdf.bat`) completely intact while sharing the core rendering and PDF export stack:

- `../gemini-renderer.js` — Markdown, Gemini sub-list reconstruction, KaTeX math rendering, Mermaid SVG rendering, and shared print/preview CSS
- `../vendor/` — Local `marked.min.js`, `katex.min.js`, `katex.min.css`, `beautiful-mermaid.min.js`, and KaTeX `.woff2` fonts
- `../pdf_engine.py` & `../md2pdf.py` — Shared Chromium headless PDF engine and auto-encoding text reader (via `desktop_backend.py`)

## Launch

- Double-click `md2pdf.exe` or `Launch-md2pdf.bat`
- Or pass a file directly: `md2pdf.exe path\to\notes.md`

## Desktop Features

- **Native Windows Window**: Windows 10/11 dark title bar, Per-Monitor DPI awareness, custom `.ico`, persisted window bounds & settings (`settings.json`).
- **Native Menus & File Dialogs**: Open/Save `.md`/`.txt`, Recent Files (10 items), Save Standalone HTML, native Print dialog, and Explorer reveal for exported PDFs.
- **Dual PDF Engines** (`Document -> PDF Engine`):
  - **Shared Python Chromium Engine (`../pdf_engine.py`)** — Matches the web studio & CLI byte-for-byte (with automatic fallback to WebView2 if needed).
  - **Native WebView2 Direct PrintToPdf** — Instant in-process PDF generation.
- **Batch PDF Export** (`Ctrl+B`): Convert multiple `.txt`/`.md` files into vector PDFs in one folder using `../md2pdf.py` + `../pdf_engine.py`.
- **Live File Watcher** (`View -> Watch Open File for Changes`): Auto-reloads the open file when edited externally.
- **Editor Productivity Tools**: Split / Editor-Only / Preview-Only layouts (`Ctrl+1` / `Ctrl+2` / `Ctrl+3`), Find & Replace (`Ctrl+F`), Markdown & LaTeX formatting toolbar, and Document Outline navigator.

## Keyboard Shortcuts

| Shortcut | Action |
| :--- | :--- |
| `Ctrl+N` | New document |
| `Ctrl+O` | Open `.txt` / `.md` file |
| `Ctrl+S` / `Ctrl+Shift+S` | Save / Save As |
| `Ctrl+E` | Export vector PDF |
| `Ctrl+Shift+H` | Export standalone HTML |
| `Ctrl+B` | Batch convert files to PDF |
| `Ctrl+F` | Find & Replace in editor |
| `Ctrl+1` / `Ctrl+2` / `Ctrl+3` | Split / Editor Only / Preview Only |
| `Ctrl++` / `Ctrl+-` / `Ctrl+0` | Zoom In / Out / Reset |
| `F11` | Toggle Full Screen |
| `F12` | Open WebView2 DevTools |

## Rebuild

Run from the repository root or `windows_app/`:

```powershell
powershell -ExecutionPolicy Bypass -File .\windows_app\build.ps1
```
