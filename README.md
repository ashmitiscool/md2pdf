# md2pdf

Convert Markdown, LaTeX math, Mermaid diagrams, and copied Gemini responses into crisp vector PDFs—via a **Windows desktop app**, a **local web studio**, or a **CLI**.

## Features

- **Vector Math & Markdown:** Local offline rendering using `marked`, `KaTeX` (with bundled `.woff2` fonts), and `beautiful-mermaid` SVG diagrams.
- **Gemini Copy Helpers:** Optional cleanup of hidden padding/zero-width characters and automatic reconstruction of flattened nested sub-lists when pasting copied Gemini text.
- **Identical Screen & PDF Output:** Shared rendering pipeline (`gemini-renderer.js`) and headless Chrome/Edge vector PDF generator (`pdf_engine.py`).
- **Themes & Page Layout:** Switch between `Gemini Dark` and `Academic Light`, `A4` or `Letter` paper, `Compact` / `Normal` / `Academic` margins, and `13–16 px` type sizes.
- **Three Ways to Use:**
  1. **Windows Desktop App** (`windows_app/md2pdf.exe`) — WinForms + WebView2 editor/preview with native file dialogs, recent files, file watcher, Find & Replace, Document Outline, and Batch PDF export.
  2. **Local Web Studio** (`Start-md2pdf.bat` / `server.py`) — Split live editor and preview in your browser with drag-and-drop, clipboard paste, standalone HTML export, and one-click vector PDF export.
  3. **CLI Converter** (`md2pdf.py`) — Convert `.md` or `.txt` files directly from the terminal.

## Requirements

- **Python 3** (standard library only for the server, CLI, and PDF engine)
- **Google Chrome or Microsoft Edge** (used by `pdf_engine.py` for headless vector PDF export)
- **Windows 10/11 + Edge WebView2 Runtime** (for the desktop app `windows_app/md2pdf.exe`)

## Quick Start

### 1. Windows Desktop App

Run `windows_app\md2pdf.exe` or `windows_app\Launch-md2pdf.bat` (or pass a file: `windows_app\md2pdf.exe path\to\notes.md`).  
See [windows_app/README.md](windows_app/README.md) for shortcuts and rebuild instructions.

### 2. Local Web Studio

Double-click `Start-md2pdf.bat`, or run:

```bash
python server.py --port 8765
```

Then open `http://127.0.0.1:8765`.

### 3. CLI (`md2pdf.py`)

```bash
python md2pdf.py input.md output.pdf --theme dark --paper A4 --margins normal --font-size 15
```

Options:
- `--theme`: `dark` (default) or `light`
- `--paper`: `A4` (default) or `Letter`
- `--margins`: `compact`, `normal` (default), or `academic`
- `--font-size`: base font size in px (default `15`)

## Sample Outputs

Example PDFs rendered from the same source in both themes are available in [`examples/`](examples/).

## Rebuilding the Windows Executable

```powershell
powershell -ExecutionPolicy Bypass -File .\windows_app\build.ps1
```
