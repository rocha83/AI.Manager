#!/usr/bin/env python3
"""Converte Markdown do AI.Manager em PDF usando Chrome headless.

Uso:
    python3 scripts/docs/md2pdf.py docs/ai-manager/GUIA-INICIANTE.md
    python3 scripts/docs/md2pdf.py docs/ai-manager/*.md --out-dir docs/ai-manager/pdf

Regras:
- Markdown -> HTML com `markdown` (tables, fenced_code, sane_lists, attr_list, toc).
- PDF gerado por `google-chrome --headless=new --print-to-pdf` (sem depender de pandoc).
- Nome do PDF = nome do .md, mesma pasta de destino.
- Falha rapida: markdown ou chrome ausentes = exit != 0 com a causa.
"""

from __future__ import annotations

import argparse
import html
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

CSS = """
@page { size: A4; margin: 16mm 14mm 18mm 14mm; }
* { box-sizing: border-box; }
body {
  font-family: "DejaVu Sans", "Segoe UI", Arial, sans-serif;
  font-size: 10.2pt; line-height: 1.5; color: #1c1e21; margin: 0;
}
h1 { font-size: 21pt; color: #8a1c1c; border-bottom: 3px solid #8a1c1c;
     padding-bottom: 6px; margin: 0 0 14px; page-break-after: avoid; }
h2 { font-size: 15pt; color: #202a44; margin: 22px 0 8px;
     border-bottom: 1px solid #cfd4de; padding-bottom: 4px; page-break-after: avoid; }
h3 { font-size: 12.4pt; color: #33415c; margin: 16px 0 6px; page-break-after: avoid; }
h4 { font-size: 11pt; color: #3d2b1f; margin: 12px 0 4px; page-break-after: avoid; }
p, li { orphans: 3; widows: 3; }
p { margin: 6px 0; }
ul, ol { margin: 6px 0 6px 20px; padding: 0; }
li { margin: 2px 0; }
code { font-family: "DejaVu Sans Mono", Consolas, monospace; font-size: 9pt;
       background: #f2f4f8; padding: 1px 3px; border-radius: 3px; }
pre { background: #f6f8fa; border: 1px solid #d8dee9; border-left: 4px solid #8a1c1c;
      border-radius: 4px; padding: 8px 10px; overflow-x: auto; page-break-inside: avoid;
      font-size: 8.6pt; line-height: 1.35; }
pre code { background: none; padding: 0; font-size: inherit; }
table { border-collapse: collapse; width: 100%; margin: 10px 0; font-size: 8.6pt;
        page-break-inside: auto; }
th, td { border: 1px solid #c9d0da; padding: 4px 6px; vertical-align: top; text-align: left;
         overflow-wrap: break-word; }
table.wide { table-layout: fixed; font-size: 8.2pt; }
table.wide th, table.wide td { overflow-wrap: anywhere; }
th { background: #e8ecf4; font-weight: 600; }
tr:nth-child(even) td { background: #fafbfd; }
thead { display: table-header-group; }
blockquote { border-left: 4px solid #b0b8c6; margin: 10px 0; padding: 4px 12px;
             color: #404654; background: #f7f8fb; }
hr { border: none; border-top: 1px solid #d0d5de; margin: 16px 0; }
a { color: #1a4f8a; text-decoration: none; }
.doc-meta { color: #5a6272; font-size: 8.8pt; margin: -8px 0 14px; }
"""


def require_markdown():
    try:
        import markdown  # noqa: F401
    except ImportError:
        sys.exit("ERRO: modulo python 'markdown' ausente. Instale com: pip install markdown")
    return __import__("markdown")


def chrome_binary() -> str:
    for name in ("google-chrome", "google-chrome-stable", "chromium", "chromium-browser"):
        path = shutil.which(name)
        if path:
            return path
    sys.exit("ERRO: Chrome/Chromium nao encontrado no PATH (necessario para gerar PDF).")


def mark_wide_tables(body: str) -> str:
    """Marca tabelas com mais de 4 colunas para layout fixo (evita overflow lateral)."""
    parts = body.split("<table>")
    out = [parts[0]]
    for chunk in parts[1:]:
        head, sep, tail = chunk.partition("</table>")
        cols = head.count("<th")
        cls = "wide" if cols > 4 else "std"
        out.append(f'<table class="{cls}">{head}{sep}{tail}')
    return "".join(out)


def render(md_path: Path, out_dir: Path, chrome: str) -> Path:
    md = require_markdown()
    text = md_path.read_text(encoding="utf-8")
    body = md.markdown(
        text,
        extensions=["tables", "fenced_code", "sane_lists", "attr_list", "toc", "nl2br"],
        extension_configs={"toc": {"permalink": False}},
    )
    body = mark_wide_tables(body)
    title_match = re.search(r"^#\s+(.+)$", text, re.MULTILINE)
    title = html.escape(title_match.group(1).strip()) if title_match else md_path.stem
    doc = (
        "<!doctype html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\">"
        f"<title>{title}</title><style>{CSS}</style></head><body>"
        f"<div class=\"doc-meta\">Fonte: {html.escape(md_path.as_posix())} "
        f"&middot; gerado por scripts/docs/md2pdf.py</div>{body}</body></html>"
    )

    out_dir.mkdir(parents=True, exist_ok=True)
    pdf_path = out_dir / (md_path.stem + ".pdf")
    with tempfile.TemporaryDirectory(prefix="md2pdf-") as tmp:
        html_path = Path(tmp) / (md_path.stem + ".html")
        html_path.write_text(doc, encoding="utf-8")
        cmd = [
            chrome,
            "--headless=new",
            "--no-sandbox",
            "--disable-gpu",
            "--disable-dev-shm-usage",
            "--no-pdf-header-footer",
            f"--print-to-pdf={pdf_path}",
            html_path.as_uri(),
        ]
        proc = subprocess.run(cmd, capture_output=True, text=True)
        if proc.returncode != 0 or not pdf_path.exists():
            sys.exit(
                f"ERRO: chrome falhou (rc={proc.returncode}) para {md_path}\n"
                f"{proc.stderr[-2000:]}"
            )
    return pdf_path


def main() -> int:
    parser = argparse.ArgumentParser(description="Markdown -> PDF (Chrome headless).")
    parser.add_argument("inputs", nargs="+", help="arquivos .md")
    parser.add_argument("--out-dir", default="docs/ai-manager/pdf", help="pasta de saida")
    args = parser.parse_args()

    chrome = chrome_binary()
    out_dir = Path(args.out_dir)
    for raw in args.inputs:
        md_path = Path(raw)
        if not md_path.is_file():
            sys.exit(f"ERRO: arquivo nao encontrado: {md_path}")
        pdf = render(md_path, out_dir, chrome)
        size = pdf.stat().st_size
        print(f"OK {md_path} -> {pdf} ({size} bytes)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
