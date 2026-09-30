namespace TrayTranslator;

// DeepL API Free (월 50만 자) 클라이언트. 엔드포인트: api-free.deepl.com
sealed class DeepLClient : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly Dictionary<(string Text, string Lang), string> _cache = new();
    private const string Base = "https://api-free.deepl.com";

    public static readonly Dictionary<string, string> LangMap = new()
    {
        ["한국어"] = "KO",
        ["English"] = "EN-US",
        ["日本語"] = "JA",
        ["中文"] = "ZH",
        ["Español"] = "ES",
        ["Français"] = "FR",
        ["Deutsch"] = "DE",
        ["Tiếng Việt"] = "VI",
    };

    public async Task<(bool Ok, string Text)> TranslateAsync(string text, string targetLang, string apiKey)
    {
        var cacheKey = (text, targetLang);
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            Logger.Log("캐시 적중 (API 호출 없음)");
            return (true, cached);
        }
        if (!LangMap.TryGetValue(targetLang, out var target))
            return (false, $"[설정 오류] DeepL 대상 언어 매핑 없음: {targetLang}");

        for (int attempt = 0; attempt < 2; attempt++)
        {
            if (attempt > 0) await Task.Delay(5000);
            var (ok, result, status, detected) = await GenerateAsync(text, target, apiKey);
            if (ok)
            {
                var clean = Logger.Sanitize(result, apiKey);
                // 원문 그대로 반환 + 원본 언어가 대상과 다르면 DeepL이 미번역한 것.
                // 하이픈/언더스코어 구분자를 공백으로 정리해 1회 재요청 (flower-power → flower power)
                if (clean.Trim().Equals(text.Trim(), StringComparison.OrdinalIgnoreCase)
                    && detected != null && !SameLang(detected, target))
                {
                    string normalized = NormalizeSeparators(text);
                    if (normalized != text.Trim())
                    {
                        Logger.Log("DeepL 미번역. 구분자 정리 후 재요청.");
                        var (ok2, result2, _, _) = await GenerateAsync(normalized, target, apiKey);
                        if (ok2) clean = Logger.Sanitize(result2, apiKey);
                    }
                }
                if (_cache.Count > 100) _cache.Clear();
                _cache[cacheKey] = clean;
                return (true, clean);
            }
            if (status != 429) return (false, Logger.Sanitize(result, apiKey));
            if (attempt == 1) return (false, Logger.Sanitize(result, apiKey));
        }
        return (false, "[요청 과다] 잠시 후 다시 시도하세요.");
    }

    public async Task<(bool Ok, string Message)> TestAsync(string apiKey, string targetLang)
    {
        string usage;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, Base + "/v2/usage");
            req.Headers.Add("Authorization", "DeepL-Auth-Key " + apiKey);
            using var res = await _http.SendAsync(req);
            var json = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
                return (false, InterpretError((int)res.StatusCode, json));
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            usage = $"{root.GetProperty("character_count").GetInt64():N0} / {root.GetProperty("character_limit").GetInt64():N0}자";
        }
        catch (Exception ex)
        {
            return (false, "[네트워크 오류] " + Trim(ex.Message, 150));
        }

        var (ok, result) = await TranslateAsync("Say OK", targetLang, apiKey);
        return ok ? (true, $"연결 성공 (이번 달 사용 {usage})") : (false, result);
    }

    private static bool SameLang(string detected, string target)
    {
        string d = detected.Length >= 2 ? detected[..2].ToUpperInvariant() : detected.ToUpperInvariant();
        string t = target.Length >= 2 ? target[..2].ToUpperInvariant() : target.ToUpperInvariant();
        return d == t;
    }

    // 식별자 구분자를 공백으로 정리 (flower-power → flower power)
    private static string NormalizeSeparators(string s) =>
        string.Join(" ", s.Replace('-', ' ').Replace('_', ' ')
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private async Task<(bool Ok, string Result, int? Status, string? Detected)> GenerateAsync(string text, string target, string apiKey)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, Base + "/v2/translate");
        req.Headers.Add("Authorization", "DeepL-Auth-Key " + apiKey);
        req.Content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("text", text),
            new KeyValuePair<string, string>("target_lang", target),
        ]);

        HttpResponseMessage res;
        try
        {
            res = await _http.SendAsync(req);
        }
        catch (TaskCanceledException)
        {
            return (false, "[네트워크 오류] 20초 내 응답 없음. 인터넷 연결을 확인하세요.", null, null);
        }
        catch (HttpRequestException ex)
        {
            return (false, "[네트워크 오류] " + Trim(ex.Message, 150), null, null);
        }

        UsageTracker.Add();
        using (res)
        {
            var json = await res.Content.ReadAsStringAsync();
            int status = (int)res.StatusCode;
            if (status == 200)
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    var first = doc.RootElement.GetProperty("translations")[0];
                    var t = first.GetProperty("text").GetString()?.Trim();
                    string? detected = first.TryGetProperty("detected_source_language", out var ds)
                        ? ds.GetString() : null;
                    if (!string.IsNullOrEmpty(t)) return (true, t, null, detected);
                    return (false, "[빈 응답] DeepL이 빈 결과를 반환했습니다.", null, null);
                }
                catch
                {
                    return (false, "[응답 파싱 실패] " + Trim(json, 150), null, null);
                }
            }
            return (false, InterpretError(status, json), status, null);
        }
    }

    private static string InterpretError(int status, string json)
    {
        string detail = ExtractMessage(json);
        return status switch
        {
            400 => $"[요청 오류] {detail} (대상 언어 미지원 가능)",
            403 => "[키 오류] DeepL 인증키가 잘못되었습니다. Free 키인지 확인하세요.",
            404 => "[요청 오류] 존재하지 않는 엔드포인트입니다.",
            429 => "[요청 과다] 너무 잦은 호출. 5초 후 자동 재시도합니다.",
            456 => "[월 한도 초과] 이번 달 50만 자 소진. 다음 달 1일 리셋됩니다.",
            >= 500 => $"[서버 오류] DeepL 서버 문제입니다. 잠시 후 다시 시도하세요. ({status})",
            _ => $"[오류 {status}] {detail}",
        };
    }

    private static string ExtractMessage(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("message", out var m)
                ? Trim(m.GetString() ?? "", 150) : Trim(json, 150);
        }
        catch { return Trim(json, 150); }
    }

    private static string Trim(string s, int n) => s.Length <= n ? s : s[..n] + "...";

    public void Dispose() => _http.Dispose();
}
