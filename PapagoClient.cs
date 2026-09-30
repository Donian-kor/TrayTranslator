using System.Text.Json;

namespace TrayTranslator;

// Naver Papago NMT API (번역 전용 — 추론 개념 없음).
// Client ID/Secret 발급: https://developers.naver.com → 프로젝트 → Papago API
// 정식 스펙: openapi.naver.com/v1/papago/n2mt + application/x-www-form-urlencoded.
// source는 필수(자동 감지 미지원)이므로 호출부가 원본 언어를 지정해야 한다.
// 조합 제한: ko<->en, ko<->zh-CN, ko<->zh-TW, ko<->es, ko<->fr, ko<->vi,
//            ko<->th, ko<->id, en<->ja, en<->fr 만 가능. 그 외는 N2MT06.
// 1회 최대 5,000자(N2MT08).
sealed class PapagoClient : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly Dictionary<(string Text, string Lang), string> _cache = new();
    private const string Base = "https://openapi.naver.com/v1/papago/n2mt";
    public const int MaxCharsPerRequest = 5000;

    public static readonly Dictionary<string, string> LangMap = new()
    {
        ["한국어"] = "ko", ["English"] = "en", ["日本語"] = "ja", ["中文"] = "zh-CN",
        ["Español"] = "es", ["Français"] = "fr", ["Deutsch"] = "de", ["Tiếng Việt"] = "vi",
    };

    public async Task<(bool Ok, string Text)> TranslateAsync(
        string text, string sourceLang, string targetLang, string clientId, string clientSecret)
    {
        var cacheKey = (text, sourceLang + ">" + targetLang);
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            Logger.Log("캐시 적중 (API 호출 없음)");
            return (true, cached);
        }
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            return (false, "[설정 오류] Papago Client ID와 Client Secret을 모두 입력하세요.");
        if (!LangMap.TryGetValue(sourceLang, out var source))
            return (false, $"[설정 오류] Papago가 원본 언어 '{sourceLang}'를 지원하지 않습니다.");
        if (!LangMap.TryGetValue(targetLang, out var target))
            return (false, $"[설정 오류] Papago가 대상 언어 '{targetLang}'를 지원하지 않습니다.");
        if (source == target)
            return (false, "[설정 오류] 원본과 대상 언어가 같습니다. (N2MT05)");
        if (text.Length > MaxCharsPerRequest)
            return (false, $"[설정 오류] 1회 {MaxCharsPerRequest}자 초과. 파일 번역 조각 크기를 확인하세요. (N2MT08)");

        HttpResponseMessage res;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, Base)
            {
                Content = new FormUrlEncodedContent(
                [
                    new KeyValuePair<string, string>("source", source),
                    new KeyValuePair<string, string>("target", target),
                    new KeyValuePair<string, string>("text", text),
                ]),
            };
            req.Headers.Add("X-Naver-Client-Id", clientId.Trim());
            req.Headers.Add("X-Naver-Client-Secret", clientSecret.Trim());
            res = await _http.SendAsync(req);
        }
        catch (TaskCanceledException)
        {
            return (false, "[네트워크 오류] 20초 내 응답 없음.");
        }
        catch (HttpRequestException ex)
        {
            return (false, "[연결 실패] Papago 서버에 연결할 수 없습니다. (" + Trim(ex.Message, 120) + ")");
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
                var t = doc.RootElement.GetProperty("message")
                    .GetProperty("result").GetProperty("translatedText").GetString();
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

    public async Task<(bool Ok, string Message)> TestAsync(
        string clientId, string clientSecret, string sourceLang, string targetLang)
    {
        var (ok, result) = await TranslateAsync("Say OK", sourceLang, targetLang, clientId, clientSecret);
        return ok ? (true, "연결 성공 (Papago)") : (false, result);
    }

    private static string ExtractMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            // NMT 에러 형태: {"errorMessage": "...", "errorCode": "N2MT06"}
            if (root.TryGetProperty("errorMessage", out var em))
                return Trim(em.GetString() ?? "", 200);
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
