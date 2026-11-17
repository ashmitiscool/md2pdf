(function (global) {
  'use strict';

  /**
   * Clean hidden padding characters Gemini web UI inserts on copy.
   */
  function normalizeGeminiText(raw, options) {
    let text = String(raw || '').replace(/\r\n?/g, '\n');
    if (!options || options.cleanInvisible !== false) {
      // Remove BOM, zero-width space/joiners, soft hyphens; normalize NBSP
      text = text
        .replace(/[\u200B-\u200D\uFEFF\u00AD]/g, '')
        .replace(/\u00A0/g, ' ');
    }
    return text;
  }

  /**
   * Reconstruct nested lists flattened when copying from Gemini.
   * Example:
   *   * **Problem Setup:** ...:
   *   * **Machine 1:** ...
   * or:
   *   * **Tree Diagram Approach:**
   *   1. **First stage:** ...
   */
  function reconstructGeminiLists(text) {
    const lines = text.split('\n');
    const out = [];
    const listItemRe = /^(\s*)([-*+]|\d+\.)\s+(.*)$/;
    const colonLeadRe = /:(?:\*\*|\*)?\s*$/;

    let inChildBlock = false;
    let parentIndentLen = 0;

    for (let i = 0; i < lines.length; i++) {
      const line = lines[i];
      const match = line.match(listItemRe);

      if (inChildBlock) {
        if (line.trim() === '') {
          inChildBlock = false;
          out.push(line);
          continue;
        }
        if (match && match[1].length <= parentIndentLen) {
          out.push('    ' + line);
          continue;
        }
        if (!match && line.trim() !== '') {
          // Continuation line inside child item
          out.push('    ' + line);
          continue;
        }
        inChildBlock = false;
      }

      out.push(line);

      if (match && colonLeadRe.test(line)) {
        // Check if the VERY NEXT line (no blank gap) is also a list item at same/lower indent
        const nextLine = i + 1 < lines.length ? lines[i + 1] : '';
        const nextMatch = nextLine.match(listItemRe);
        if (nextMatch && nextMatch[1].length <= match[1].length) {
          inChildBlock = true;
          parentIndentLen = match[1].length;
        }
      }
    }

    return out.join('\n');
  }

  function escapeHtml(str) {
    return String(str)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;');
  }

  /**
   * Detect whether a fenced code block body starts with a supported Mermaid header.
   */
  function isMermaidSource(code) {
    let cleaned = String(code || '').replace(/\r\n?/g, '\n');
    cleaned = cleaned.replace(/^\s*---\n[\s\S]*?\n---\s*(?:\n|$)/, '');
    cleaned = cleaned.replace(/%%\{[\s\S]*?\}%%/g, '');
    const lines = cleaned.split('\n');
    for (let i = 0; i < lines.length; i++) {
      const line = lines[i].trim();
      if (!line || line.startsWith('%%')) continue;
      return /^(?:(?:flowchart|graph)(?:\s+(?:TB|TD|BT|RL|LR))?\s*;?|(?:sequenceDiagram|classDiagram|stateDiagram(?:-v2)?|erDiagram|xychart(?:-beta)?)\s*;?)$/i.test(
        line
      );
    }
    return false;
  }

  /**
   * Normalize Mermaid diagram text before passing it to beautiful-mermaid.
   */
  function normalizeMermaidCode(code, fenceLang) {
    let text = String(code || '').replace(/\r\n?/g, '\n').trim();
    text = text.replace(/^---\n[\s\S]*?\n---\s*(?:\n|$)/, '');
    text = text.replace(/%%\{[\s\S]*?\}%%/g, '').trim();

    const info = String(fenceLang || '').trim();
    if (/^(?:flowchart|graph)(?:\s+(?:TB|TD|BT|RL|LR))?$/i.test(info) && !isMermaidSource(text)) {
      text = text ? info + '\n' + text : info;
    }

    const lines = text.split('\n');
    let headerIdx = -1;
    for (let i = 0; i < lines.length; i++) {
      const trimmed = lines[i].trim();
      if (!trimmed || trimmed.startsWith('%%')) continue;
      let header = trimmed.replace(/;\s*$/, '');
      if (/^(?:flowchart|graph)$/i.test(header)) {
        header = header + ' TD';
      }
      lines[i] = header;
      headerIdx = i;
      break;
    }

    if (headerIdx > 0) {
      return lines.slice(headerIdx).join('\n').trim();
    }
    return lines.join('\n').trim();
  }

  /**
   * Extract code blocks/spans, Mermaid diagrams, and LaTeX math BEFORE Markdown
   * parsing so underscores (_), asterisks (*), pipes (|), and backslashes (\)
   * are never corrupted by Markdown emphasis or table rules.
   */
  function renderGeminiMarkdown(rawText, options) {
    const opts = Object.assign(
      {
        cleanInvisible: true,
        reconstructLists: true,
        autoNumberSublists: true
      },
      options || {}
    );

    if (typeof global.marked === 'undefined' || typeof global.katex === 'undefined') {
      throw new Error('Required libraries (marked, katex) are not loaded.');
    }

    let text = normalizeGeminiText(rawText, opts);

    const codeBlocks = [];
    const mermaidBlocks = [];
    const inlineCodes = [];
    const blockMaths = [];
    const inlineMaths = [];

    // 1. Protect fenced code blocks (```...``` or ~~~...~~~) and extract Mermaid blocks
    text = text.replace(
      /((?:^|\n)[ \t]*)(```+|~~~+)([^\n]*)\n(?:([\s\S]*?)\n)?[ \t]*\2[ \t]*(?=\n|$)/g,
      function (m, prefix, fence, infoRaw, bodyRaw) {
        const info = (infoRaw || '').trim();
        const lang = info ? info.split(/\s+/)[0].toLowerCase() : '';
        const body = bodyRaw || '';
        const isHeaderLang = lang === 'flowchart' || lang === 'graph';
        const candidate = isHeaderLang && !isMermaidSource(body) ? info + '\n' + body : body;

        if (lang === 'mermaid' || ((lang === '' || isHeaderLang) && isMermaidSource(candidate))) {
          const token = '@@GMD_MERMAID_' + mermaidBlocks.length + '@@';
          mermaidBlocks.push({
            code: normalizeMermaidCode(body, info),
            rawFence: m,
            explicit: lang === 'mermaid'
          });
          return '\n\n' + token + '\n\n';
        }

        const token = '@@GMD_CODEBLOCK_' + codeBlocks.length + '@@';
        codeBlocks.push(m);
        return '\n' + token + '\n';
      }
    );

    // 2. Protect inline code spans (`...`)
    text = text.replace(/`([^`\n]+)`/g, function (m) {
      const token = '@@GMD_CODEINLINE_' + inlineCodes.length + '@@';
      inlineCodes.push(m);
      return token;
    });

    // 3. Extract display math: $$...$$ (single-line or multi-line) and \[...\]
    text = text.replace(/\$\$([\s\S]+?)\$\$/g, function (_, expr) {
      const token = '@@GMD_MATHBLOCK_' + blockMaths.length + '@@';
      blockMaths.push(expr.trim());
      return '\n\n' + token + '\n\n';
    });

    text = text.replace(/\\\[([\s\S]+?)\\\]/g, function (_, expr) {
      const token = '@@GMD_MATHBLOCK_' + blockMaths.length + '@@';
      blockMaths.push(expr.trim());
      return '\n\n' + token + '\n\n';
    });

    // 4. Extract inline math: \(...\) and $...$
    text = text.replace(/\\\((.+?)\\\)/g, function (_, expr) {
      const token = '@@GMD_MATHINLINE_' + inlineMaths.length + '@@';
      inlineMaths.push(expr.trim());
      return token;
    });

    text = text.replace(/(?<!\\)\$(?!\s)((?:\\.|[^$\n])+?)(?<!\s)\$(?!\d)/g, function (_, expr) {
      const token = '@@GMD_MATHINLINE_' + inlineMaths.length + '@@';
      inlineMaths.push(expr.trim());
      return token;
    });

    // 5. Reconstruct flattened Gemini sub-lists (before restoring code blocks)
    if (opts.reconstructLists) {
      text = reconstructGeminiLists(text);
    }

    // 6. Restore code blocks & inline code so marked renders them cleanly
    text = text.replace(/@@GMD_CODEBLOCK_(\d+)@@/g, function (_, idx) {
      return codeBlocks[Number(idx)];
    });
    text = text.replace(/@@GMD_CODEINLINE_(\d+)@@/g, function (_, idx) {
      return inlineCodes[Number(idx)];
    });

    // 7. Parse Markdown via marked
    global.marked.setOptions({
      gfm: true,
      breaks: false
    });
    let html = global.marked.parse(text);

    // 8. Render extracted math with KaTeX
    let equationCount = 0;
    const errors = [];

    function renderLatex(latex, displayMode) {
      equationCount++;
      try {
        return global.katex.renderToString(latex, {
          displayMode: displayMode,
          throwOnError: true,
          strict: false,
          trust: true
        });
      } catch (err) {
        errors.push({ latex: latex, message: err.message });
        return global.katex.renderToString(latex, {
          displayMode: displayMode,
          throwOnError: false,
          strict: false,
          trust: true
        });
      }
    }

    html = html.replace(/<p>\s*@@GMD_MATHBLOCK_(\d+)@@\s*<\/p>/g, function (_, idx) {
      return renderLatex(blockMaths[Number(idx)], true);
    });
    html = html.replace(/@@GMD_MATHBLOCK_(\d+)@@/g, function (_, idx) {
      return renderLatex(blockMaths[Number(idx)], true);
    });
    html = html.replace(/@@GMD_MATHINLINE_(\d+)@@/g, function (_, idx) {
      return renderLatex(inlineMaths[Number(idx)], false);
    });

    // 9. Render extracted Mermaid diagrams to vector SVG
    let diagramCount = 0;
    function renderMermaidBlock(entry) {
      if (global.beautifulMermaid && typeof global.beautifulMermaid.renderMermaidSVG === 'function') {
        try {
          let svg = global.beautifulMermaid.renderMermaidSVG(entry.code, {
            bg: '#18181B',
            fg: '#FAFAFA',
            font: 'Inter'
          });
          svg = svg.replace(/^(<svg\b[^>]*?\s)style="[^"]*"/, '$1style="background:var(--bg)"');
          diagramCount++;
          return '<div class="mermaid-block">' + svg + '</div>';
        } catch (err) {
          if (entry.explicit) {
            errors.push({ latex: 'mermaid', message: err && err.message ? err.message : String(err) });
          }
        }
      }
      const codeClass = entry.explicit ? ' class="language-mermaid"' : '';
      return '<pre><code' + codeClass + '>' + escapeHtml(entry.code) + '</code></pre>';
    }

    html = html.replace(/<p>\s*@@GMD_MERMAID_(\d+)@@\s*<\/p>/g, function (_, idx) {
      return renderMermaidBlock(mermaidBlocks[Number(idx)]);
    });
    html = html.replace(/@@GMD_MERMAID_(\d+)@@/g, function (_, idx) {
      return renderMermaidBlock(mermaidBlocks[Number(idx)]);
    });

    // Extract title from first heading for suggested PDF filename
    let title = 'md2pdf_Document';
    const headingMatch = rawText.match(/^#{1,4}\s+(.+)$/m);
    if (headingMatch) {
      title = headingMatch[1]
        .replace(/[*_`~]/g, '')
        .replace(/[^\w\s-]/g, '')
        .trim()
        .replace(/\s+/g, '_')
        .slice(0, 64) || 'md2pdf_Document';
    }

    return {
      html: html,
      title: title,
      stats: {
        equations: equationCount,
        blockEquations: blockMaths.length,
        inlineEquations: inlineMaths.length,
        diagrams: diagramCount,
        errors: errors
      }
    };
  }

  /**
   * Build the shared CSS used both inside the live preview container and in
   * standalone/PDF print documents so screen preview and PDF match identically.
   */
  function getDocumentStyles(config) {
    const cfg = Object.assign(
      {
        theme: 'dark',
        paper: 'A4',
        margins: 'normal',
        fontSize: 15
      },
      config || {}
    );

    const isDark = cfg.theme === 'dark';
    const marginMap = {
      compact: '12mm 14mm',
      normal: '18mm 20mm',
      academic: '22mm 24mm'
    };
    const pageMargin = marginMap[cfg.margins] || marginMap.normal;

    const bg = isDark ? '#131314' : '#FFFFFF';
    const fg = isDark ? '#E3E3E3' : '#18181B';
    const heading = isDark ? '#FFFFFF' : '#09090B';
    const muted = isDark ? '#A1A6B3' : '#52525B';
    const rule = isDark ? '#2B2D31' : '#E4E4E7';
    const codeBg = isDark ? '#1E1F22' : '#F4F4F5';
    const codeBorder = isDark ? '#2F3136' : '#E4E4E7';
    const quoteBorder = isDark ? '#4C8DF6' : '#2563EB';
    const quoteBg = isDark ? 'rgba(76, 141, 246, 0.08)' : '#F8FAFC';

    return `
      @page {
        size: ${cfg.paper};
        margin: ${pageMargin};
        background: ${bg};
      }
      @page :first {
        background: ${bg};
      }
      html, body {
        margin: 0;
        padding: 0;
        background: ${bg};
        color: ${fg};
        -webkit-print-color-adjust: exact;
        print-color-adjust: exact;
      }
      .gemini-doc {
        font-family: "Inter", "Google Sans", "Segoe UI", -apple-system, BlinkMacSystemFont, sans-serif;
        font-size: ${cfg.fontSize}px;
        line-height: 1.65;
        color: ${fg};
        background: ${bg};
        max-width: 780px;
        margin: 0 auto;
        word-wrap: break-word;
      }
      .gemini-doc > *:first-child { margin-top: 0; }
      .gemini-doc h1, .gemini-doc h2, .gemini-doc h3, .gemini-doc h4 {
        color: ${heading};
        font-weight: 600;
        line-height: 1.32;
        letter-spacing: -0.015em;
        break-after: avoid;
        page-break-after: avoid;
      }
      .gemini-doc h1 { font-size: 1.65em; margin: 0 0 0.7em; }
      .gemini-doc h2 { font-size: 1.38em; margin: 1.35em 0 0.6em; }
      .gemini-doc h3 { font-size: 1.16em; margin: 1.35em 0 0.55em; }
      .gemini-doc h4 { font-size: 1.04em; margin: 1.15em 0 0.45em; }
      .gemini-doc p { margin: 0.75em 0; }
      .gemini-doc strong { color: ${heading}; font-weight: 600; }
      .gemini-doc em { color: ${isDark ? '#D5D8DF' : '#3F3F46'}; }
      .gemini-doc hr {
        border: none;
        border-top: 1px solid ${rule};
        margin: 1.5em 0;
      }
      .gemini-doc ul, .gemini-doc ol {
        margin: 0.55em 0 0.8em;
        padding-left: 1.55em;
      }
      .gemini-doc ul {
        list-style-type: ${isDark ? 'circle' : 'disc'};
      }
      .gemini-doc ul ul {
        list-style-type: disc;
        margin-top: 0.35em;
        margin-bottom: 0.35em;
      }
      .gemini-doc ol {
        margin-top: 0.35em;
        margin-bottom: 0.35em;
      }
      .gemini-doc li {
        margin: 0.42em 0;
        break-inside: avoid;
        page-break-inside: avoid;
      }
      .gemini-doc li > p {
        margin: 0.25em 0;
      }
      .gemini-doc .katex-display {
        margin: 1.05em 0;
        padding: 0.25em 0;
        overflow-x: auto;
        overflow-y: hidden;
        text-align: center;
        color: ${heading};
        break-inside: avoid;
        page-break-inside: avoid;
      }
      .gemini-doc .katex {
        font-size: 1.05em;
        color: ${heading};
      }
      .gemini-doc code {
        font-family: "JetBrains Mono", "Cascadia Code", Consolas, monospace;
        font-size: 0.88em;
        background: ${codeBg};
        border: 1px solid ${codeBorder};
        border-radius: 5px;
        padding: 0.14em 0.38em;
      }
      .gemini-doc pre {
        background: ${codeBg};
        border: 1px solid ${codeBorder};
        border-radius: 8px;
        padding: 0.9em 1.05em;
        overflow-x: auto;
        line-height: 1.5;
        break-inside: avoid;
        page-break-inside: avoid;
      }
      .gemini-doc pre code {
        background: transparent;
        border: none;
        padding: 0;
      }
      .gemini-doc blockquote {
        margin: 1em 0;
        padding: 0.6em 1em;
        border-left: 3px solid ${quoteBorder};
        background: ${quoteBg};
        color: ${muted};
        border-radius: 0 6px 6px 0;
        break-inside: avoid;
        page-break-inside: avoid;
      }
      .gemini-doc table {
        width: 100%;
        border-collapse: collapse;
        margin: 1.1em 0;
        font-size: 0.94em;
        break-inside: avoid;
        page-break-inside: avoid;
      }
      .gemini-doc th, .gemini-doc td {
        border: 1px solid ${rule};
        padding: 0.52em 0.75em;
        text-align: left;
      }
      .gemini-doc th {
        background: ${codeBg};
        color: ${heading};
        font-weight: 600;
      }
      .gemini-doc .mermaid-block {
        margin: 1.15em 0;
        padding: 18px 20px;
        background: ${isDark ? '#18181B' : '#FAFAFA'};
        border: 1px solid ${codeBorder};
        border-radius: 8px;
        text-align: center;
        overflow-x: auto;
        break-inside: avoid;
        page-break-inside: avoid;
      }
      .gemini-doc .mermaid-block svg {
        --bg: ${isDark ? '#18181B' : '#FAFAFA'};
        --fg: ${isDark ? '#FAFAFA' : '#27272A'};
        display: block;
        max-width: 100%;
        height: auto;
        margin: 0 auto;
      }
      @media print {
        .gemini-doc {
          max-width: none;
          width: 100%;
        }
        .gemini-doc .katex-display {
          overflow-x: visible;
        }
        .gemini-doc .mermaid-block {
          overflow-x: visible;
        }
      }
    `;
  }

  /**
   * Scale down overly wide display equations only when they would clip past
   * the container width (e.g. long multi-term derivations on A4 portrait).
   */
  function fitWideEquations(rootEl) {
    if (!rootEl) return;
    const maxW = rootEl.clientWidth;
    if (!maxW) return;
    const blocks = rootEl.querySelectorAll('.katex-display');
    for (let i = 0; i < blocks.length; i++) {
      const block = blocks[i];
      const inner = block.querySelector('.katex');
      if (!inner) continue;
      inner.style.fontSize = '';
      const naturalW = inner.scrollWidth;
      if (naturalW > maxW) {
        const ratio = Math.max(0.68, (maxW - 4) / naturalW);
        inner.style.fontSize = ratio.toFixed(3) + 'em';
      }
    }
  }

  /**
   * Build a complete standalone HTML document ready for download or headless PDF rendering.
   */
  function buildStandaloneHtml(renderedHtml, options) {
    const opts = Object.assign(
      {
        title: 'md2pdf Document',
        theme: 'dark',
        paper: 'A4',
        margins: 'normal',
        fontSize: 15,
        katexCssHref: 'https://cdn.jsdelivr.net/npm/katex@0.16.11/dist/katex.min.css'
      },
      options || {}
    );

    const safeTitle = String(opts.title).replace(/[<>&"]/g, '');
    const css = getDocumentStyles(opts);

    return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>${safeTitle}</title>
  <link rel="stylesheet" href="${opts.katexCssHref}" />
  <style>${css}</style>
</head>
<body>
  <article class="gemini-doc" id="docRoot">${renderedHtml}</article>
  <script>
    (function() {
      function fit() {
        var root = document.getElementById('docRoot');
        if (!root) return;
        var maxW = root.clientWidth;
        if (!maxW) return;
        var blocks = root.querySelectorAll('.katex-display');
        for (var i = 0; i < blocks.length; i++) {
          var inner = blocks[i].querySelector('.katex');
          if (!inner) continue;
          inner.style.fontSize = '';
          if (inner.scrollWidth > maxW) {
            var r = Math.max(0.68, (maxW - 4) / inner.scrollWidth);
            inner.style.fontSize = r.toFixed(3) + 'em';
          }
        }
      }
      if (document.fonts && document.fonts.ready) {
        document.fonts.ready.then(fit);
      } else {
        window.addEventListener('load', fit);
      }
      window.addEventListener('beforeprint', fit);
    })();
  </script>
</body>
</html>`;
  }

  global.GeminiRenderer = {
    normalizeGeminiText: normalizeGeminiText,
    reconstructGeminiLists: reconstructGeminiLists,
    renderGeminiMarkdown: renderGeminiMarkdown,
    getDocumentStyles: getDocumentStyles,
    fitWideEquations: fitWideEquations,
    buildStandaloneHtml: buildStandaloneHtml
  };
})(typeof window !== 'undefined' ? window : globalThis);
