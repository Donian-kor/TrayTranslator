using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrayTranslator;

// OpenAI 호환 API 클라이언트 (DeepSeek / Groq / OpenAI / LM Studio 공용).
// 이 프로그램의 번역은 "추론 없는 즉시 응답"이 요구사항이므로 thinking을 끄는 파라미터를 보내고,
// 응답에 섞여오는 추론 흔적은 제거한다.
sealed class OpenAiCompatClient : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(120) };
    private readonly Dictionary<(string Text, string Lang), string> _cache = new();
    private readonly string _serviceName;
    // DeepSeek만 thinking 파라미터를 지원한다. 다른 서비스에 보내면 오류가 난다.
    private readonly bool _supportsThinkingToggle;

    public OpenAiCompatClient(string serviceName, bool supportsThinkingToggle = false)
    {
        _serviceName = serviceName;
        _supportsThinkingToggle = supportsThinkingToggle;
    }

    public static readonly Dictionary<string, string> LangMap = new()
    {
        ["한국어"] = "Korean",
        ["English"] = "English",
        ["日本語"] = "Japanese",
        ["中文"] = "Chinese",
        ["Español"] = "Spanish",
        ["Français"] = "French",
        ["Deutsch"] = "German",
        ["Tiếng Việt"] = "Vietnamese",
    };

    public async Task<(bool Ok, string Text)> TranslateAsync(
        string text, string targetLang, string host, string model, string apiKey)
    {
        var cacheKey = (text, targetLang);
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            Logger.Log("캐시 적중 (API 호출 없음)");
            return (true, cached);
        }
        if (string.IsNullOrWhiteSpace(model))
            return (false, $"[설정 오류] {_serviceName} 모델명을 입력하세요.");
        string target = LangMap.TryGetValue(targetLang, out var mapped) ? mapped : targetLang;

        var url = host.TrimEnd('/') + "/v1/chat/completions";
        // DeepSeek는 thinking이 기본 ON이라, 번역이 지연되지 않도록 명시적으로 끈다.
        // thinking 파라미터는 DeepSeek에만 보내고, 다른 서비스에는 아예 넣지 않는다
        // (null을 보내면 지원하지 않는 서비스가 오류를 낸다).
        var body = _supportsThinkingToggle
            ? new ChatRequest(model, [new ChatMessage("user", BuildPrompt(target, text))],
                1024, false, new ThinkingOff())
            : new ChatRequest(model, [new ChatMessage("user", BuildPrompt(target, text))],
                1024, false);

        HttpResponseMessage res;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(body),
            };
            if (!string.IsNullOrWhiteSpace(apiKey))
                req.Headers.Add("Authorization", "Bearer " + apiKey.Trim());
            res = await _http.SendAsync(req);
        }
        catch (TaskCanceledException)
        {
            return (false, "[시간 초과] 120초 내 응답 없음. 모델이 너무 무겁거나 서버가 멈췄을 수 있습니다.");
        }
        catch (HttpRequestException ex)
        {
            return (false, $"[연결 실패] {_serviceName} 서버에 연결할 수 없습니다. ({Trim(ex.Message, 120)})");
        }

        UsageTracker.Add();
        using (res)
        {
            var json = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
                return (false, $"[오류 {(int)res.StatusCode}] {Trim(ExtractMessage(json), 200)}");
            try
            {
                using var doc = JsonDocument.Parse(json);
                var msg = doc.RootElement.GetProperty("choices")[0].GetProperty("message");
                var t = (msg.GetProperty("content").GetString() ?? "").Trim();
                if (t.Length == 0) return (false, "[빈 응답] 모델이 빈 결과를 반환했습니다.");
                t = StripThinking(t);
                if (_cache.Count > 100) _cache.Clear();
                _cache[cacheKey] = t;
                return (true, t);
            }
            catch
            {
                return (false, "[응답 파싱 실패] " + Trim(json, 200));
            }
        }
    }

    // 즉시 번역이 목적이라 모델이 앞뒤에 붙인 think 태그를 제거한다.
    private static string StripThinking(string s)
    {
        int open = s.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
        if (open >= 0)
        {
            int close = s.IndexOf("</think>", open, StringComparison.OrdinalIgnoreCase);
            s = close >= 0 ? s[(close + "</think>".Length)..] : s[open..];
        }
        s = s.Trim();
        if (s.StartsWith("<think>", StringComparison.OrdinalIgnoreCase))
            s = s["<think>".Length..].Trim();
        if (s.EndsWith("</think>", StringComparison.OrdinalIgnoreCase))
            s = s[..^"</think>".Length].Trim();
        return s;
    }

    private static string BuildPrompt(string target, string text) =>
        $"Translate the following text into {target}. " +
        "Output ONLY the translation. No explanation, no reasoning, no notes. " +
        "Do not repeat the original text.\n\n" + text;

    public async Task<(bool Ok, string Message)> TestAsync(
        string host, string model, string apiKey, string targetLang)
    {
        var (ok, result) = await TranslateAsync("Say OK", targetLang, host, model, apiKey);
        return ok ? (true, $"연결 성공 ({model})") : (false, result);
    }

    // Groq/OpenAI 등은 키가 있어야 목록 조회가 된다. 실패하면 빈 목록을 돌려
    // 호출부가 고정 모델 목록이나 수동 입력으로 대체한다.
    public async Task<List<string>> ListModelsAsync(string host, string apiKey)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, host.TrimEnd('/') + "/v1/models");
            if (!string.IsNullOrWhiteSpace(apiKey))
                req.Headers.Add("Authorization", "Bearer " + apiKey.Trim());
            using var res = await _http.SendAsync(req);
            if (!res.IsSuccessStatusCode) return [];
            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data)) return [];
            return data.EnumerateArray()
                .Select(e => e.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "")
                .Where(s => s != "")
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch { return []; }
    }

    private static string ExtractMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var err))
            {
                if (err.ValueKind == JsonValueKind.String)
                    return Trim(err.GetString() ?? "", 200);
                if (err.TryGetProperty("message", out var m))
                    return Trim(m.GetString() ?? "", 200);
            }
            return Trim(json, 200);
        }
        catch { return Trim(json, 200); }
    }

    private static string Trim(string s, int n) => s.Length <= n ? s : s[..n] + "...";

    public void Dispose() => _http.Dispose();

    private sealed record ChatRequest(
        string model,
        ChatMessage[] messages,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        bool stream,
        // DeepSeek: {"thinking":{"type":"disabled"}} 로 추론을 끈다.
        // WhenWritingNull 이므로 지원하지 않는 서비스에는 필드 자체가 빠진다.
        [property: JsonPropertyName("thinking")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        ThinkingOff? Thinking = null);
    private sealed record ChatMessage(string role, string content);
    private sealed record ThinkingOff
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = "disabled";
    }
}
