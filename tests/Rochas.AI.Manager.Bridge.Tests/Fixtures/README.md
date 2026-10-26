# Fixtures do `Rochas.AI.Manager.Bridge.Tests`

Estes arquivos são **dados de teste** e **artefatos de smoke manual**. Não são
runtime de produção e não entram no pacote NuGet.

## `fake_llama.py`

Upstream falso no formato OpenAI `chat/completions`, usado quando não há GPU ou
quando o teste precisa de resposta determinística.

O que ele imita:

| Requisição | Resposta |
|---|---|
| `POST /v1/chat/completions` com `stream=false` | `chat.completion` com `content="ok"` |
| `POST /v1/chat/completions` com `stream=true` | SSE `data: {...}` + `data: [DONE]` |
| `POST /v1/chat/completions` com `grammar` no corpo (ToolShim) | `content` = envelope `{"name":"read","arguments":{"path":"a.txt"}}` |
| `GET /health` | `200 ok` |

O comportamento com `grammar` é proposital: ele prova o caminho
`ToolShim.Apply` → resposta JSON → `ToolShim.TransformResponse` → `tool_calls`.

### Smoke manual

```bash
# 1) upstream falso
python3 tests/Rochas.AI.Manager.Bridge.Tests/Fixtures/fake_llama.py --port 4199

# 2) host console (transporte completo)
dotnet run --project src/Rochas.AI.Manager.Bridge.Runner -c Release -- \
  serve --port 4152 --upstream http://127.0.0.1:4199 --model smoke

# 3) caminho Responses não-stream
curl -sS http://127.0.0.1:4152/v1/responses \
  -H 'Content-Type: application/json' \
  -d '{"model":"m","input":"oi"}'

# 4) caminho Responses stream
curl -sS -N http://127.0.0.1:4152/v1/responses \
  -H 'Content-Type: application/json' \
  -d '{"model":"m","input":"oi","stream":true}'

# 5) shim de tool calling (host com --tool-shim json)
dotnet run --project src/Rochas.AI.Manager.Bridge.Runner -c Release -- \
  serve --port 4153 --upstream http://127.0.0.1:4199 --model smoke --tool-shim json
curl -sS http://127.0.0.1:4153/v1/responses \
  -H 'Content-Type: application/json' \
  -d '{"model":"m","input":"leia a.txt","tools":[{"type":"function","name":"read","parameters":{"type":"object","properties":{"path":{"type":"string"}},"required":["path"]}}]}'
```

O teste automatizado `FixtureDataTests.FakeLlamaServeChatStreamEShim` sobe o
script em uma porta livre, espera `/health`, executa `CompleteAsync`,
`StreamAsync` e o caminho com shim, e encerra o processo no `finally`. Se
`python3` não existir na máquina, o teste retorna sem falhar (ambiente sem
Python não é falha do transporte .NET).

## `smoke.manifest.json`

Manifesto de exemplo compatível com `BridgeManifestValidator` e
`ManifestCommandResolver`. Ele alimenta dois testes:

* validação + dry-run (hash calculado, preflight, dependências);
* documentação viva do formato que o AI.Manager vai gerar.

```bash
dotnet run --project src/Rochas.AI.Manager.Bridge.Runner -c Release -- \
  manifest --manifest tests/Rochas.AI.Manager.Bridge.Tests/Fixtures/smoke.manifest.json \
  --workspace /tmp/opencode --model smoke
```

## Por que estes dados ficam no projeto de teste

O plano de absorção trata **teste como dado do workflow**: o cenário, o
manifesto e o alvo (Q4 gate / Q3 espelho) são entradas versionadas. Manter o
`fake_llama.py` e o manifesto ao lado dos testes garante que o mesmo dado usado
no smoke manual também é exercitado no CI, sem depender de `/tmp` ou de GPU.
