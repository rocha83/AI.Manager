#!/usr/bin/env python3
"""Gerador deterministico do C4 (draw.io XML) do plano de absorcao.

Arquitetura representada (v2, absorcao):
  - AI.Manager e o sistema concentrador (Web/API, workflow existente,
    TestBench, SQLite/evidencia);
  - AI.Manager.Bridge e um projeto DENTRO do AI.Manager
    (Core/Contracts/Client/Hosting/Api/Runner);
  - TestBench/Flow, Seed do WAR e a integracao in-process sao fase 2;
  - Rochas.OpenCodeBridge aparece como DOADOR/TRANSITORIO, fora do sistema;
  - Q4 :4124 e o gate; Q3 :4125 e espelho pos-green; llama.cpp fica externo.

Uso:
  python3 gen-c4.py            # grava ai-manager-next-steps-c4.drawio ao lado
  python3 gen-c4.py --stdout   # imprime o XML

O PNG e derivado do .drawio pela CLI oficial (ver LAB-SETUP §4.6):
  drawio -x -f png --size page -s 2.5 -o ai-manager-next-steps-c4.png ai-manager-next-steps-c4.drawio
"""
from __future__ import annotations

import html
import sys
from pathlib import Path

PAGE_W, PAGE_H = 1600, 960
DRAWIO = "app.diagrams.net"

# --- estilo base -------------------------------------------------------------
BOX = (
    "rounded=1;arcSize=6;whiteSpace=wrap;html=1;fontSize=12;"
    "fillColor={fill};strokeColor={stroke};strokeWidth=1;fontColor=#18202E;"
    "align=center;verticalAlign=middle;spacing=6;"
)
BOX_LEFT = BOX.replace("align=center", "align=left;spacingLeft=10")
TITLE = "text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;fontSize=24;fontStyle=1;fontColor=#18202E;"
SUB = "text;html=1;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;fontSize=11;fontColor=#4E5A6C;"
BOUNDARY = (
    "rounded=1;arcSize=2;whiteSpace=wrap;html=1;dashed={dashed};dashPattern=6 4;"
    "fillColor={fill};strokeColor={stroke};strokeWidth=1;verticalAlign=top;"
    "align=left;spacingLeft=14;spacingTop=8;fontSize=14;fontStyle=1;fontColor=#18202E;"
)
EDGE = (
    "edgeStyle=orthogonalEdgeStyle;rounded=0;html=1;endArrow=block;endSize=6;"
    "strokeColor={stroke};strokeWidth=1.2;fontSize=9;fontColor=#4E5A6C;"
    "labelBackgroundColor=#FFFFFF;"
)

C_AI = "#EAF2FD"        # AI.Manager (sistema)
C_BRIDGE = "#E6EEFB"    # AI.Manager.Bridge (projeto interno)
C_DONOR = "#F4F4F5"     # OpenCodeBridge (doador)
C_MODEL = "#EAF6EE"     # llama.cpp
C_GATE = "#FDF0E6"      # bridge Q4 (gate)
C_MIRROR = "#F3F0FB"    # bridge Q3 (espelho)
C_STORE = "#FFF7E0"     # storage
C_EXT = "#F7F8FA"       # externo neutro
S_AI = "#788496"
S_BRIDGE = "#3E6FB0"
S_DONOR = "#8A8A8A"
S_MODEL = "#4E8A63"
S_GATE = "#B06A2C"
S_MIRROR = "#6A4FB0"
S_STORE = "#B09029"
S_EXT = "#788496"

cells: list[str] = []


def cell(
    cid: str,
    value: str,
    x: int,
    y: int,
    w: int,
    h: int,
    style: str = BOX,
    fill: str = "#FFFFFF",
    stroke: str = "#D2D8E2",
    dashed: str = "0",
    boundary: bool = False,
) -> None:
    if boundary:
        st = BOUNDARY.format(fill=fill, stroke=stroke, dashed=dashed)
    else:
        st = style.format(fill=fill, stroke=stroke)
    v = html.escape(value, quote=True).replace("&#x27;", "'")
    cells.append(
        f'        <mxCell id="{cid}" value="{v}" style="{st}" vertex="1" parent="1">\n'
        f'          <mxGeometry x="{x}" y="{y}" width="{w}" height="{h}" as="geometry" />\n'
        f"        </mxCell>"
    )


def edge(src: str, dst: str, label: str = "", stroke: str = "#788496",
         dashed: str = "0", exit: str = "", entry: str = "") -> None:
    st = EDGE.format(stroke=stroke)
    if dashed == "1":
        st += "dashed=1;dashPattern=4 4;"
    if exit:
        st += f"exitX={exit[0]};exitY={exit[1]};exitDx=0;exitDy=0;"
    if entry:
        st += f"entryX={entry[0]};entryY={entry[1]};entryDx=0;entryDy=0;"
    eid = f"e_{src}_{dst}"
    v = html.escape(label, quote=True)
    cells.append(
        f'        <mxCell id="{eid}" value="{v}" style="{st}" edge="1" parent="1" source="{src}" target="{dst}">\n'
        f'          <mxGeometry relative="1" as="geometry" />\n'
        f"        </mxCell>"
    )


def build() -> str:
    # ---- cabecalho ----------------------------------------------------------
    cell("title", "AI.Manager — C4 Nível 2 (Contêineres): porte do OpenCodeBridge como AI.Manager.Bridge",
         30, 6, 1180, 34, style=TITLE, fill="none", stroke="none")
    cell("subtitle",
         "AI.Manager é o concentrador da solução de treinamento e testes de modelos locais de baixa instrução  •  "
         "porte 2026-10-25: Core/Contracts/Client/Hosting/Api/Runner  •  Q4 :4124 = gate  •  Q3 :4125 = espelho pós-green",
         30, 42, 1200, 22, style=SUB, fill="none", stroke="none")

    cell("legend",
         "<b>Legenda</b><br><font style='font-size:9px'>"
         "—— chamada síncrona&nbsp;&nbsp;&nbsp;-- evidência / fase 2 / migração<br>"
         "■ AI.Manager&nbsp;&nbsp; ■ AI.Manager.Bridge&nbsp;&nbsp; ■ doador&nbsp;&nbsp; "
         "■ modelo&nbsp;&nbsp; ■ storage</font>",
         1240, 10, 330, 62, style=BOX_LEFT, fill="#FFFFFF", stroke="#D2D8E2")

    # ---- sistema AI.Manager -------------------------------------------------
    cell("boundary_ai",
         "<b>AI.Manager</b><br><font style='font-size:9px;color:#4E5A6C'>"
         "sistema concentrador — experimento, workflow, circuito, enunciado, evidência</font>",
         90, 96, 790, 720, fill=C_AI, stroke=S_AI, dashed="1", boundary=True)

    cell("web", "<b>Web UI</b><br><font style='font-size:9px'>catálogo • wizard • mapa do circuito<br>console • evidência</font>",
         120, 146, 330, 72, fill="#FFFFFF")
    cell("api", "<b>API + Application Services</b><br><font style='font-size:9px'>CRUD • ManifestBuilder • Validator<br>Acceptance • Runner Registry</font>",
         490, 146, 350, 72, fill="#FFFFFF")
    cell("workflow", "<b>Workflow existente</b><br><font style='font-size:9px'>Experiment (Kind=Training|Test) • TrainingWorkflowCard<br>aprovação/status</font>",
         120, 246, 330, 82, fill="#FFFFFF")
    cell("testbench", "<b>TestBench / Domain</b> <font color='#B06A2C'>(fase 2)</font><br><font style='font-size:9px'>ExperimentPlan • CircuitStage/Step • TestScenario<br>TestRun • MirrorRun • Evidence</font>",
         490, 246, 350, 82, fill="#FFFFFF")

    # ---- AI.Manager.Bridge (projeto interno) --------------------------------
    cell("boundary_bridge",
         "<b>AI.Manager.Bridge</b> — projeto interno (extraível após paridade)<br>"
         "<font style='font-size:9px;color:#4E5A6C'>núcleo protocol-only • hosts Api/Runner • contratos/client/hosting</font>",
         110, 356, 750, 322, fill=C_BRIDGE, stroke=S_BRIDGE, dashed="0", boundary=True)

    cell("br_contracts", "<b>Contracts</b><br><font style='font-size:9px'>DTOs • manifest • opções (sem ASP.NET)</font>",
         125, 404, 140, 70, fill="#FFFFFF", stroke=S_BRIDGE)
    cell("br_client", "<b>Client</b><br><font style='font-size:9px'>HTTP/SSE tipado • host legado</font>",
         275, 404, 140, 70, fill="#FFFFFF", stroke=S_BRIDGE)
    cell("br_hosting", "<b>Hosting</b><br><font style='font-size:9px'>flags • DI • bootstrap</font>",
         425, 404, 140, 70, fill="#FFFFFF", stroke=S_BRIDGE)
    cell("br_runner", "<b>Runner</b><br><font style='font-size:9px'>serve • manifest</font>",
         575, 404, 140, 70, fill="#FFFFFF", stroke=S_BRIDGE)
    cell("br_api", "<b>Api</b><br><font style='font-size:9px'>REST + Swagger • /v1</font>",
         725, 404, 125, 70, fill="#FFFFFF", stroke=S_BRIDGE)

    cell("br_core", "<b>Core (Rochas.AI.Manager.Bridge)</b><br><font style='font-size:9px'>Responses↔chat • tools • MainHarness base + HarnessTails<br>anti-repeat • breaker • QwenStandard • ContextGuard</font>",
         250, 510, 460, 76, fill="#FFFFFF", stroke=S_BRIDGE)

    cell("br_note",
         "<b>Harness (core):</b> <font style='font-size:9px'>"
         "MainHarness.Apply(messages, enabled) • gate por cliente (User-Agent contém \"opencode\"; nunca sufixo -build) • "
         "base e tails coexistem • TailScope managed|all|off • ForClient isola a decisão por requisição</font>",
         125, 606, 725, 48, style=BOX_LEFT, fill="#F8FAFF", stroke=S_BRIDGE)

    cell("store", "<b>SQLite</b><br><font style='font-size:9px'>definições • versões • runs • metadados de evidência • auditoria</font>",
         120, 700, 330, 72, fill=C_STORE, stroke=S_STORE)
    cell("seed", "<b>Seed do WAR</b> <font color='#B06A2C'>(fase 2)</font><br><font style='font-size:9px'>war-baseline.json (importação, nunca runtime) • WarBaselineSeeder idempotente</font>",
         490, 700, 350, 72, fill=C_STORE, stroke=S_STORE)

    # ---- doador -------------------------------------------------------------
    cell("boundary_donor",
         "<b>Rochas.OpenCodeBridge</b> — doador / transitório (Git separado até o cutover)<br>"
         "<font style='font-size:9px;color:#4E5A6C'>congelado para features; consome AI.Manager.Bridge durante a transição; arquivado após paridade + 3 gates Q4 verdes</font>",
         90, 826, 790, 108, fill=C_DONOR, stroke=S_DONOR, dashed="1", boundary=True)
    cell("donor", "<b>Host legado + Test + WAR</b><br><font style='font-size:9px'>Program.cs (~245 KB) • TestCircuitDriver • WarTemplate • E2E belts • Web</font>",
         120, 870, 730, 50, fill="#FFFFFF", stroke=S_DONOR)

    # ---- fora do AI.Manager -------------------------------------------------
    cell("user", "<b>Autor / operador</b><br><font style='font-size:9px'>pessoa — guia o circuito e aprova o experimento</font>",
         960, 146, 600, 64, fill=C_EXT, stroke=S_EXT)
    cell("bridge_q4", "<b>Bridge Q4 :4124</b> — GATE<br><font style='font-size:9px'>instância estável; único critério de aceite</font>",
         960, 238, 600, 64, fill=C_GATE, stroke=S_GATE)
    cell("llama_q4", "<b>llama.cpp Q4 :4100</b><br><font style='font-size:9px'>Gemma4 E4B Q4_K_M • ctx 131072 (1 slot) • saída 16384</font>",
         960, 330, 600, 56, fill=C_MODEL, stroke=S_MODEL)
    cell("bridge_q3", "<b>Bridge Q3 :4125</b> — espelho pós-green<br><font style='font-size:9px'>roda o mesmo manifestHash só depois do Q4 verde; nunca é gate</font>",
         960, 414, 600, 64, fill=C_MIRROR, stroke=S_MIRROR)
    cell("llama_q3", "<b>llama.cpp Q3 :4110</b><br><font style='font-size:9px'>Gemma4 E4B UD-Q3 • ctx 114688 (2×57344) • saída 16384</font>",
         960, 506, 600, 56, fill=C_MODEL, stroke=S_MODEL)
    cell("github", "<b>GitHub</b><br><font style='font-size:9px'>versiona AI.Manager; OpenCodeBridge até o arquivamento</font>",
         960, 598, 600, 56, fill=C_EXT, stroke=S_EXT)
    cell("cloudflare", "<b>Cloudflare (acesso externo)</b><br><font style='font-size:9px'>publica a UI; opcional nesta fase</font>",
         960, 684, 600, 56, fill=C_EXT, stroke=S_EXT)

    # ---- relacionamentos ----------------------------------------------------
    edge("user", "web", "HTTPS", exit=("0.5", "1"), entry=("0.5", "0"))
    edge("web", "api", "", exit=("1", "0.5"), entry=("0", "0.5"))
    edge("api", "workflow", "serviços", stroke=S_BRIDGE, exit=("0.15", "1"), entry=("0.85", "0"))
    edge("workflow", "testbench", "", stroke=S_BRIDGE, exit=("1", "0.5"), entry=("0", "0.5"))
    edge("api", "br_api", "HTTP", stroke=S_BRIDGE, exit=("1", "0.2"), entry=("1", "0.5"))
    edge("br_client", "br_api", "HTTP/SSE", stroke=S_BRIDGE, exit=("0.5", "1"), entry=("0.5", "1"))
    edge("br_contracts", "br_core", "DTOs", stroke=S_BRIDGE, exit=("0.5", "1"), entry=("0.12", "0"))
    edge("br_hosting", "br_core", "flags/DI", stroke=S_BRIDGE, exit=("0.5", "1"), entry=("0.38", "0"))
    edge("br_runner", "br_core", "serve/manifest", stroke=S_BRIDGE, exit=("0.5", "1"), entry=("0.62", "0"))
    edge("br_api", "br_core", "tradução/stream", stroke=S_BRIDGE, exit=("0.5", "1"), entry=("0.88", "0"))
    edge("seed", "store", "importação", stroke=S_STORE, dashed="1", exit=("0", "0.5"), entry=("1", "0.5"))
    edge("donor", "br_client", "transição (Bridge.Client)", stroke=S_DONOR, dashed="1", exit=("0", "0.5"), entry=("0.5", "1"))
    edge("testbench", "bridge_q4", "gate Q4", stroke=S_GATE, exit=("1", "0.5"), entry=("0", "0.35"))
    edge("testbench", "bridge_q3", "espelho pós-green", stroke=S_MIRROR, dashed="1", exit=("1", "0.8"), entry=("0", "0.65"))
    edge("bridge_q4", "llama_q4", "inferência", stroke=S_GATE, exit=("0.7", "1"), entry=("0.7", "0"))
    edge("bridge_q3", "llama_q3", "inferência", stroke=S_MIRROR, exit=("0.7", "1"), entry=("0.7", "0"))
    edge("store", "github", "", stroke=S_EXT, dashed="1", exit=("0.5", "1"), entry=("0.1", "0.5"))

    body = "\n".join(cells)
    return (
        f'<mxfile host="{DRAWIO}" modified="2026-10-25T00:00:00.000Z" agent="AI.Manager" '
        f'version="24.7.17" type="device">\n'
        f'  <diagram id="ai-manager-c4" name="C4 Nível 2 — Contêineres (absorção)">\n'
        f'    <mxGraphModel dx="1600" dy="960" grid="1" gridSize="10" guides="1" tooltips="1" '
        f'connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="{PAGE_W}" '
        f'pageHeight="{PAGE_H}" math="0" shadow="0">\n'
        f"      <root>\n"
        f'        <mxCell id="0" />\n'
        f'        <mxCell id="1" parent="0" />\n'
        f"{body}\n"
        f"      </root>\n"
        f"    </mxGraphModel>\n"
        f"  </diagram>\n"
        f"</mxfile>\n"
    )


def main() -> int:
    xml = build()
    if "--stdout" in sys.argv:
        sys.stdout.write(xml)
        return 0
    out = Path(__file__).with_name("ai-manager-next-steps-c4.drawio")
    out.write_text(xml, encoding="utf-8")
    print(f"escrito: {out} ({len(xml)} bytes)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
