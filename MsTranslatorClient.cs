using System.Net.Http.Json;
using System.Text.Json;

namespace TrayTranslator;

// Microsoft Azure Translator v3 (번역 전용 — 추론 개념 없음).
// 구독 키 발급: https://azure.microsoft.com → Cognitive Services → Translator
sealed class MsTranslatorClient : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly Dictionary<(string Text, string Lang), string> _cache = new();
    private const string Base = "https://api.cognitive.microsofttranslator.com/translate?api-version=3.0";

    public static readonly Dictionary<string, string> LangMap = new()
    {
        ["한국어"] = "ko", ["English"] = "en", ["日本語"] = "ja", ["中文"] = "zh-Hans",
        ["Español"] = "es", ["Français"] = "fr", ["Deutsch"] = "de", ["Tiếng Việt"] = "vi",
    };

    public async Task<(bool Ok, string Text)> TranslateAsync(string text, string targetLang, string apiKey)
    {
        var cacheKey = (text, targetLang);
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            Logger.Log("캐시 적중 (API 호출 없음)");
            return (true, cached);
        }
        if (string.IsNullOrWhiteSpace(apiKey))
            return (false, "[설정 오류] Microsoft 구독 키를 입력하세요.");
        string target = LangMap.TryGetValue(targetLang, out var m) ? m : "en";

        HttpResponseMessage res;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{Base}&to={target}")
            {
                Content = JsonContent.Create(new[] { new { Text = text } }),
            };
            req.Headers.Add("Ocp-Apim-Subscription-Key", apiKey.Trim());
            res = await _http.SendAsync(req);
        }
        catch (TaskCanceledException)
        {
            return (false, "[네트워크 오류] 20초 내 응답 없음.");
        }
        catch (HttpRequestException ex)
        {
            return (false, "[연결 실패] Microsoft 번역 서버에 연결할 수 없습니다. (" + Trim(ex.Message, 120) + ")");
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
                var t = doc.RootElement[0].GetProperty("text").GetString()?.Trim();
                if (string.IsNullOrEmpty(t)) return (false, "[빈 응답] 번역 결과가 비어 있습니다.");
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

    public async Task<(bool Ok, string Message)> TestAsync(string apiKey, string targetLang)
    {
        var (ok, result) = await TranslateAsync("Say OK", targetLang, apiKey);
        return ok ? (true, "연결 성공 (Microsoft 번역)") : (false, result);
    }

    private static string ExtractMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var err))
            {
                if (err.TryGetProperty("message", out var m)) return Trim(m.GetString() ?? "", 200);
            }
            return Trim(json, 200);
        }
        catch { return Trim(json, 200); }
    }

    private static string Trim(string s, int n) => s.Length <= n ? s : s[..n] + "...";

    public void Dispose() => _http.Dispose();
}
