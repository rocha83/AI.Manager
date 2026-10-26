#!/usr/bin/env python3
"""Fake llama.cpp upstream para smoke/E2E do AI.Manager.Bridge.

Este arquivo e DADO DE TESTE, nao runtime de producao. Ele substitui o
llama.cpp real nos testes de transporte quando nao ha GPU disponivel ou quando
o teste precisa de uma resposta deterministica.

Contrato que ele imita (somente o que o Bridge usa):

* ``POST /v1/chat/completions``
  - ``stream=false`` -> um objeto ``chat.completion`` com ``content="ok"``;
  - ``stream=true``  -> SSE ``data: {...}\\n\\n`` + ``data: [DONE]``;
  - quando o corpo tem ``grammar`` (ToolShim ligado), o ``content`` vira o
    envelope JSON ``{"name":"read","arguments":{"path":"a.txt"}}`` que o
    ``ToolShim.TransformResponse`` converte em ``tool_calls``.
* ``GET /health`` -> 200 ``ok``.

Uso:
    python3 fake_llama.py --port 4199

No teste automatizado ele e iniciado por ``FakeLlamaTests``; no smoke manual,
suba-o e aponte o Runner/Api com ``--upstream http://127.0.0.1:4199``.
"""

from __future__ import annotations

import argparse
import json
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer


def _chunk(delta: dict, finish: str | None = None, usage: dict | None = None) -> str:
    payload = {
        "id": "chatcmpl-fake",
        "object": "chat.completion.chunk",
        "choices": [{"index": 0, "delta": delta, "finish_reason": finish}],
    }
    if usage is not None:
        payload["usage"] = usage
    return "data: " + json.dumps(payload) + "\n\n"


class FakeLlamaHandler(BaseHTTPRequestHandler):
    server_version = "FakeLlama/1.0"

    def _read_body(self) -> dict:
        length = int(self.headers.get("Content-Length", "0"))
        raw = self.rfile.read(length) if length else b"{}"
        try:
            return json.loads(raw or b"{}")
        except json.JSONDecodeError:
            return {}

    def _send(self, status: int, content_type: str, raw: bytes) -> None:
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(raw)))
        self.end_headers()
        self.wfile.write(raw)

    def do_GET(self) -> None:  # noqa: N802 (API do BaseHTTPRequestHandler)
        if self.path == "/health":
            self._send(200, "text/plain; charset=utf-8", b"ok")
            return
        self._send(404, "application/json", b'{"error":"not found"}')

    def do_POST(self) -> None:  # noqa: N802 (API do BaseHTTPRequestHandler)
        if self.path != "/v1/chat/completions":
            self._send(404, "application/json", b'{"error":"not found"}')
            return

        body = self._read_body()
        shimmed = "grammar" in body
        content = (
            json.dumps({"name": "read", "arguments": {"path": "a.txt"}})
            if shimmed
            else "ok"
        )

        if body.get("stream"):
            raw = (
                _chunk({"role": "assistant", "content": content})
                + _chunk({}, "stop", {"prompt_tokens": 10, "completion_tokens": 1})
                + "data: [DONE]\n\n"
            ).encode("utf-8")
            self._send(200, "text/event-stream", raw)
            return

        payload = {
            "id": "chatcmpl-fake",
            "object": "chat.completion",
            "choices": [
                {
                    "index": 0,
                    "message": {"role": "assistant", "content": content},
                    "finish_reason": "stop",
                }
            ],
            "usage": {"prompt_tokens": 10, "completion_tokens": 1},
        }
        self._send(200, "application/json", json.dumps(payload).encode("utf-8"))

    def log_message(self, *args: object) -> None:
        # Teste nao precisa do log linha a linha do HTTPServer.
        pass


def main() -> None:
    parser = argparse.ArgumentParser(description="Fake llama.cpp upstream do Bridge")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=4199)
    args = parser.parse_args()

    server = ThreadingHTTPServer((args.host, args.port), FakeLlamaHandler)
    print(f"fake llama em http://{args.host}:{args.port}", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
