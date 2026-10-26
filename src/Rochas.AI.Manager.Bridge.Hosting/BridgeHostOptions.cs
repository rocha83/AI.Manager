using System.Globalization;

namespace Rochas.AI.Manager.Bridge.Hosting;

/// <summary>
/// CLI/configuracao do host console. Mesma superficie que o host legado expunha
/// por flag, com fallback por ambiente para systemd/container.
/// </summary>
public sealed class BridgeHostOptions
{
    public const int DefaultPort = 4140;

    public int Port { get; set; } = DefaultPort;
    public string Listen { get; set; } = "127.0.0.1";
    public string UpstreamUrl { get; set; } = "http://127.0.0.1:4100";
    public string ModelId { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public bool IsGate { get; set; } = true;
    public string? ArtifactRoot { get; set; }
    public double Temperature { get; set; } = 0.5;
    public string Thinking { get; set; } = "events";
    public int MaxTokens { get; set; }
    public int ContextWindow { get; set; }
    public bool Harness { get; set; } = true;
    public bool MainHarness { get; set; } = true;
    public string TailScope { get; set; } = "managed";
    public bool LegacyHarness { get; set; }
    public bool QwenStandard { get; set; }
    public bool ToolStrategy { get; set; } = true;
    public int BreakerSoft { get; set; } = 3;
    public int BreakerHard { get; set; } = 6;
    public bool SamplingPassthrough { get; set; }
    public string ToolShimMode { get; set; } = string.Empty;
    public int ToolShimMaxString { get; set; } = 2000;
    public int ToolShimMinCalls { get; set; }
    public bool ShowHelp { get; set; }

    public string BaseUrl => $"http://{Listen}:{Port}";

    public static string Usage => """
        Uso: Rochas.AI.Manager.Bridge.Runner [serve] [opcoes]

          --port <int>          porta HTTP (default 4140; env BRIDGE_PORT)
          --listen <addr>       interface de escuta (default 127.0.0.1; env BRIDGE_LISTEN)
          --upstream <url>      llama.cpp deste perfil (env BRIDGE_UPSTREAM)
          --model <id>          model id servido pelo upstream (env BRIDGE_MODEL)
          --api-key <token>     bearer exigido nas rotas /v1 e /api (env BRIDGE_API_KEY)
          --mirror              marca o perfil como espelho (Q3), nunca gate
          --artifacts <dir>     raiz de artefatos/evidencias (env BRIDGE_ARTIFACT_ROOT)
          --temperature <double> temperatura do motor (default 0.5)
          --thinking <mode>     events|off (default events)
          --max-tokens <int>    teto de saida; 0 = sem teto
          --context <int>       janela de contexto do modelo (0 = desconhecida)
          --no-harness          desliga as caudas de comportamento/tools
          --main-harness        substitui o system do cliente pelo MainHarness (default)
          --no-main-harness     preserva o system do cliente
          --tail-scope <scope>  managed (default) | all | off
          --legacy-harness      strip de call narrada + guardrail textual
          --no-legacy-harness   caminho moderno, sem guardrail (default)
          --qwen-standard       ModelPrompt + traducao OpenAI/Alibaba
          --no-tool-strategy    nao remove execute/Code Mode do catalogo
          --sampling-passthrough copia top_p/top_k/repeat_penalty/... do request
          --breaker-soft <int>  falhas para o nudge (default 3; 0 desliga)
          --breaker-hard <int>  falhas para o trip (default 6; 0 desliga)
          --tool-shim <mode>    "" (default) | json = shim de tool calling
          --tool-shim-max-string <int>  cap de chars por string no grammar
          --tool-shim-min-calls <int>   calls minimas antes de liberar {"final"}
          --help                mostra esta ajuda
        """;

    public static BridgeHostOptions Parse(string[] args)
    {
        var options = new BridgeHostOptions
        {
            Port = ReadInt("BRIDGE_PORT", DefaultPort),
            Listen = ReadString("BRIDGE_LISTEN", "127.0.0.1"),
            UpstreamUrl = ReadString("BRIDGE_UPSTREAM", "http://127.0.0.1:4100"),
            ModelId = ReadString("BRIDGE_MODEL", string.Empty),
            ApiKey = ReadString("BRIDGE_API_KEY", null),
            ArtifactRoot = ReadString("BRIDGE_ARTIFACT_ROOT", null),
            MainHarness = ReadBool("BRIDGE_MAIN_HARNESS", true),
            TailScope = ReadString("BRIDGE_TAIL_SCOPE", "managed") ?? "managed",
            LegacyHarness = ReadBool("BRIDGE_LEGACY_HARNESS", false),
            QwenStandard = ReadBool("BRIDGE_QWEN_STANDARD", false),
            ToolStrategy = ReadBool("BRIDGE_TOOL_STRATEGY", true),
            BreakerSoft = ReadInt("BRIDGE_BREAKER_SOFT", 3),
            BreakerHard = ReadInt("BRIDGE_BREAKER_HARD", 6),
            SamplingPassthrough = ReadBool("BRIDGE_SAMPLING_PASSTHROUGH", false)
        };

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--help":
                case "-h":
                    options.ShowHelp = true;
                    break;
                case "--port":
                    options.Port = ParseInt(NextValue(args, ref i, arg), arg);
                    break;
                case "--listen":
                    options.Listen = NextValue(args, ref i, arg);
                    break;
                case "--upstream":
                    options.UpstreamUrl = NextValue(args, ref i, arg);
                    break;
                case "--model":
                    options.ModelId = NextValue(args, ref i, arg);
                    break;
                case "--api-key":
                    options.ApiKey = NextValue(args, ref i, arg);
                    break;
                case "--mirror":
                    options.IsGate = false;
                    break;
                case "--gate":
                    options.IsGate = true;
                    break;
                case "--artifacts":
                    options.ArtifactRoot = NextValue(args, ref i, arg);
                    break;
                case "--temperature":
                    options.Temperature = ParseDouble(NextValue(args, ref i, arg), arg);
                    break;
                case "--thinking":
                    options.Thinking = NextValue(args, ref i, arg);
                    break;
                case "--max-tokens":
                    options.MaxTokens = ParseInt(NextValue(args, ref i, arg), arg);
                    break;
                case "--context":
                    options.ContextWindow = ParseInt(NextValue(args, ref i, arg), arg);
                    break;
                case "--no-harness":
                    options.Harness = false;
                    break;
                case "--main-harness":
                    options.MainHarness = true;
                    break;
                case "--no-main-harness":
                    options.MainHarness = false;
                    break;
                case "--tail-scope":
                    options.TailScope = NextValue(args, ref i, arg).Trim().ToLowerInvariant();
                    break;
                case "--legacy-harness":
                    options.LegacyHarness = true;
                    break;
                case "--no-legacy-harness":
                    options.LegacyHarness = false;
                    break;
                case "--qwen-standard":
                    options.QwenStandard = true;
                    break;
                case "--no-tool-strategy":
                    options.ToolStrategy = false;
                    break;
                case "--sampling-passthrough":
                    options.SamplingPassthrough = true;
                    break;
                case "--breaker-soft":
                    options.BreakerSoft = ParseInt(NextValue(args, ref i, arg), arg);
                    break;
                case "--breaker-hard":
                    options.BreakerHard = ParseInt(NextValue(args, ref i, arg), arg);
                    break;
                case "--tool-shim":
                    options.ToolShimMode = NextValue(args, ref i, arg).Trim().ToLowerInvariant();
                    break;
                case "--tool-shim-max-string":
                    options.ToolShimMaxString = ParseInt(NextValue(args, ref i, arg), arg);
                    break;
                case "--tool-shim-min-calls":
                    options.ToolShimMinCalls = ParseInt(NextValue(args, ref i, arg), arg);
                    break;
                default:
                    throw new ArgumentException($"argumento desconhecido: '{arg}'. Use --help.");
            }
        }

        if (options.Port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(options.Port), options.Port, "porta fora da faixa 1..65535");

        if (options.BreakerSoft < 0 || options.BreakerHard < 0)
            throw new ArgumentOutOfRangeException(nameof(options.BreakerSoft), "breaker-soft/breaker-hard nao podem ser negativos");

        if (options.TailScope is not ("managed" or "all" or "off"))
            throw new ArgumentException(
                $"--tail-scope '{options.TailScope}' invalido; use managed, all ou off");

        if (options.BreakerHard > 0 && options.BreakerSoft > options.BreakerHard)
            throw new ArgumentException(
                $"--breaker-soft {options.BreakerSoft} maior que --breaker-hard {options.BreakerHard}: o aviso nunca apareceria");

        if (!Uri.TryCreate(options.UpstreamUrl, UriKind.Absolute, out var upstream)
            || (upstream.Scheme != Uri.UriSchemeHttp && upstream.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException($"--upstream invalido: '{options.UpstreamUrl}'");
        }

        return options;
    }

    private static string NextValue(string[] args, ref int index, string flag)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException($"faltou valor para {flag}");
        return args[++index];
    }

    private static int ParseInt(string text, string flag) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new ArgumentException($"{flag} exige inteiro, veio '{text}'");

    private static double ParseDouble(string text, string flag) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new ArgumentException($"{flag} exige numero, veio '{text}'");

    private static string ReadString(string key, string? fallback) =>
        Environment.GetEnvironmentVariable(key) is { Length: > 0 } value ? value : fallback ?? string.Empty;

    private static int ReadInt(string key, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    private static bool ReadBool(string key, bool fallback) =>
        Environment.GetEnvironmentVariable(key)?.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "yes" or "on" => true,
            "0" or "false" or "no" or "off" => false,
            _ => fallback
        };
}
