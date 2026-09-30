using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace TrayTranslator;

// LM Studio 로컬 서버 (OpenAI 호환: {host}/v1/chat/completions). 요금·한도 없음.
sealed class LmStudioClient : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(120) };
    private readonly Dictionary<(string Text, string Lang), string> _cache = new();

    public static readonly Dictionary<string, string> LangMap = new()
    {
        // Hy-MT2 공식 권장: 전체 언어명(full names) 사용
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
            return (false, "[설정 오류] LM Studio 모델명을 입력하세요. (LM Studio에 로드된 모델 ID)");
        string target = LangMap.TryGetValue(targetLang, out var mapped) ? mapped : targetLang;

        var url = host.TrimEnd('/') + "/v1/chat/completions";
        var body = new ChatRequest(model,
            [new ChatMessage("user", $"Translate the following text into {target}. Note that you should only output the translated result without any additional explanation:\n\n{text}")],
            0.2f, 1024, false);

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
        catch (HttpRequestException)
        {
            return (false, "[연결 실패] LM Studio 서버에 연결할 수 없습니다. 서버 시작 여부와 주소를 확인하세요.");
        }

        UsageTracker.Add();
        using (res)
        {
            var json = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
                return (false, $"[오류 {(int)res.StatusCode}] {Trim(ExtractMessage(json), 200)}");
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var t = doc.RootElement.GetProperty("choices")[0]
                    .GetProperty("message").GetProperty("content").GetString()?.Trim();
                if (string.IsNullOrEmpty(t)) return (false, "[빈 응답] 모델이 빈 결과를 반환했습니다.");
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

    public async Task<(bool Ok, string Message)> TestAsync(string host, string model)
    {
        var ids = await ListModelsAsync(host);
        if (ids.Count == 0)
            return (false, "[연결 실패] LM Studio 서버에 연결할 수 없거나 로드된 모델이 없습니다.");
        string note = string.IsNullOrWhiteSpace(model) ? " (모델명 미입력)"
            : ids.Any(id => id.Contains(model.Trim(), StringComparison.OrdinalIgnoreCase))
                ? "" : $" (입력한 '{model.Trim()}'은 목록에 없음)";
        return (true, $"연결 성공 (로드: {string.Join(", ", ids.Take(5))}){note}");
    }

    public async Task<List<string>> ListModelsAsync(string host)
    {
        try
        {
            using var res = await _http.GetAsync(host.TrimEnd('/') + "/v1/models");
            if (!res.IsSuccessStatusCode) return [];
            var json = await res.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("data").EnumerateArray()
                .Select(e => e.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "")
                .Where(s => s != "").ToList();
        }
        catch { return []; }
    }

    private static string ExtractMessage(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var err))
            {
                if (err.ValueKind == System.Text.Json.JsonValueKind.String)
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
        float temperature,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        bool stream);
    private sealed record ChatMessage(string role, string content);
}
