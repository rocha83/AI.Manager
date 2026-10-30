# PDFs da documentação

PDFs gerados a partir dos `.md` de `docs/ai-manager/`. **Não editar o PDF**: a fonte é o Markdown.

| PDF | Fonte |
|---|---|
| `Rochas.AI.Manager-Arquitetura v2.pdf` | `../Rochas.AI.Manager-Arquitetura v2.md` (rev. 2.2) |
| `GUIA-INICIANTE.pdf` | `../GUIA-INICIANTE.md` (rev. 2.2) |

## Regenerar

```bash
# na raiz do repositório
python3 scripts/docs/md2pdf.py \
  "docs/ai-manager/GUIA-INICIANTE.md" \
  "docs/ai-manager/Rochas.AI.Manager-Arquitetura v2.md" \
  --out-dir docs/ai-manager/pdf
```

Requisitos: `python3` com o módulo `markdown` (`pip install markdown`) e Chrome/Chromium no `PATH`
(`google-chrome`, `chromium` ou `chromium-browser`). O script falha rápido se faltar qualquer um.

Regra do repositório: **PDF só é válido se gerado do `.md` no mesmo commit** — nunca editar à mão.
