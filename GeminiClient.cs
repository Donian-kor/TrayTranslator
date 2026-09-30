using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace TrayTranslator;

sealed partial class GeminiClient : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly Dictionary<(string Text, string Lang), string> _cache = new();

    // Gemini API 키 형태. 클립보드에 키가 남아있을 때 번역 대상으로 잡는 실수 방지용.
    [GeneratedRegex(@"^AIza[0-9A-Za-z_\-]{35}$")]
    private static partial Regex ApiKeyPattern();

    [GeneratedRegex(@"limit:\s*(\d+)")]
    private static partial Regex LimitPattern();

    [GeneratedRegex(@"^([\d.]+)s$")]
    private static partial Regex SecondsPattern();

    public static bool LooksLikeApiKey(string s) => ApiKeyPattern().IsMatch(s.Trim());

    private enum QuotaKind { None, PerMinute, PerDay }
    private sealed record QuotaInfo(QuotaKind Kind, int? Limit, int? RetrySeconds);

    public async Task<(bool Ok, string Text)> TranslateAsync(string text, string targetLang, string apiKey,
        string model, Func<string, Task>? onProgress = null)
    {
        var cacheKey = (text, targetLang);
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            Logger.Log("캐시 적중 (API 호출 없음)");
            return (true, cached);
        }

        // 일일 한도는 모델별 버킷이므로 429-per-day면 다음 모델로 폴백
        var chain = new List<string> { model };
        chain.AddRange(AppSettings.Models.Where(m => m != model));

        string lastError = "";
        foreach (var m in chain)
        {
            var (ok, result, status, quota) = await GenerateAsync(text, targetLang, apiKey, m);
            if (ok)
            {
                var clean = Sanitize(result, apiKey);
                if (_cache.Count > 100) _cache.Clear();
                _cache[cacheKey] = clean;
                return (true, clean);
            }
            if (status == 404) continue; // 다음 모델
            if (status == 429 && quota.Kind == QuotaKind.PerMinute)
            {
                // 분당 한도: RetryInfo만큼 기다렸다가 같은 모델로 1회 재시도
                int wait = Math.Min(quota.RetrySeconds ?? 30, 60);
                if (onProgress != null)
                    await onProgress($"분당 호출 한도 초과 (분당 {quota.Limit?.ToString() ?? "?"}회). {wait}초 후 자동 재시도 중...");
                await Task.Delay(wait * 1000);
                var (ok2, result2, status2, quota2) = await GenerateAsync(text, targetLang, apiKey, m);
                if (ok2)
                {
                    var clean = Sanitize(result2, apiKey);
                    if (_cache.Count > 100) _cache.Clear();
                    _cache[cacheKey] = clean;
                    return (true, clean);
                }
                if (status2 == 429 && quota2.Kind == QuotaKind.PerDay) continue; // 다음 모델
                lastError = result2;
                break;
            }
            if (status == 429 && quota.Kind == QuotaKind.PerDay) { lastError = result; continue; } // 다음 모델
            lastError = result;
            break;
        }
        return (false, Sanitize(lastError, apiKey));
    }

    public async Task<(bool Ok, string Message)> TestAsync(string apiKey, string model, string targetLang)
    {
        var (ok, result, _, _) = await GenerateAsync(
            $"Say OK (translate this to {targetLang} if it is not already {targetLang})",
            targetLang, apiKey, model);
        return ok ? (true, $"연결 성공 ({model})") : (false, Sanitize(result, apiKey));
    }

    private async Task<(bool Ok, string Result, int? Status, QuotaInfo Quota)> GenerateAsync(
        string text, string targetLang, string apiKey, string model)
    {
        var none = new QuotaInfo(QuotaKind.None, null, null);
        // API 키를 URL 쿼리에 붙이지 않고 x-goog-api-key 헤더로 보낸다.
        // URL은 접속 기록·프록시·에러 메시지에 남기 쉬운 자리라 헤더가 안전하다.
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";
        var body = new GenerateRequest(
            [new Content([new Part($"Translate the following text to {targetLang}. Output ONLY the translation, no explanation:\n\n{text}")])],
            new GenConfig(0.2f, 1024));

        HttpResponseMessage res;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Add("x-goog-api-key", apiKey);
            req.Content = JsonContent.Create(body);
            res = await _http.SendAsync(req);
        }
        catch (TaskCanceledException)
        {
            return (false, "[네트워크 오류] 20초 내 응답 없음. 인터넷 연결을 확인하세요.", null, none);
        }
        catch (HttpRequestException ex)
        {
            return (false, $"[네트워크 오류] {Trim(ex.Message, 200)}", null, none);
        }

        UsageTracker.Add();
        using (res)
        {
            var json = await res.Content.ReadAsStringAsync();
            int status = (int)res.StatusCode;
            if (status != 200 && status != 201)
                return (false, FriendlyError(status, json), status, ParseQuota(json));

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("candidates", out var cands) && cands.GetArrayLength() > 0)
                {
                    // 긴 출력은 여러 parts로 나뉠 수 있으므로 텍스트 parts를 전부 이어붙임
                    var sb = new System.Text.StringBuilder();
                    foreach (var part in cands[0].GetProperty("content").GetProperty("parts").EnumerateArray())
                        if (part.TryGetProperty("text", out var t))
                            sb.Append(t.GetString());
                    var s = sb.ToString().Trim();
                    if (!string.IsNullOrEmpty(s)) return (true, s, null, none);
                }
                string block = "";
                if (root.TryGetProperty("promptFeedback", out var fb) &&
                    fb.TryGetProperty("blockReason", out var br))
                    block = $" (차단 사유: {br.GetString()})";
                return (false, "[빈 응답] 모델이 빈 결과를 반환했습니다." + block, null, none);
            }
            catch
            {
                return (false, "[응답 파싱 실패] " + Trim(json, 200), null, none);
            }
        }
    }

    private static QuotaInfo ParseQuota(string json)
    {
        string quotaId = "";
        int? retry = null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var err = doc.RootElement.GetProperty("error");
            if (err.TryGetProperty("details", out var details))
                foreach (var d in details.EnumerateArray())
                {
                    if (!d.TryGetProperty("@type", out var t)) continue;
                    var ts = t.GetString() ?? "";
                    if (ts.EndsWith("QuotaFailure") && d.TryGetProperty("violations", out var v))
                        foreach (var viol in v.EnumerateArray())
                            if (viol.TryGetProperty("quotaId", out var q))
                                quotaId = q.GetString() ?? "";
                    if (ts.EndsWith("RetryInfo") && d.TryGetProperty("retryDelay", out var rd))
                    {
                        var m = SecondsPattern().Match(rd.GetString() ?? "");
                        if (m.Success && double.TryParse(m.Groups[1].Value,
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var sec))
                            retry = (int)Math.Ceiling(sec);
                    }
                }
        }
        catch { }

        int? limit = null;
        try
        {
            var m = LimitPattern().Match(ExtractMessage(json));
            if (m.Success) limit = int.Parse(m.Groups[1].Value);
        }
        catch { }

        var kind = quotaId.Contains("PerDay") ? QuotaKind.PerDay
            : quotaId.Contains("PerMinute") ? QuotaKind.PerMinute
            : QuotaKind.None;
        return new QuotaInfo(kind, limit, retry);
    }

    private static string FriendlyError(int status, string json)
    {
        string detail = ExtractMessage(json);
        var q = status == 429 ? ParseQuota(json) : new QuotaInfo(QuotaKind.None, null, null);
        return status switch
        {
            400 => $"[요청 오류] API 키가 잘못되었거나 요청 형식이 틀립니다. {detail}",
            403 => $"[권한 오류] API 키가 무효/차단되었거나 지역 제한입니다. {detail}",
            404 => $"[모델 오류] 모델을 찾을 수 없습니다. 설정에서 모델을 바꾸세요. {detail}",
            429 when q.Kind == QuotaKind.PerMinute =>
                $"[분당 한도 초과] 분당 {q.Limit?.ToString() ?? "?"}회 제한. {q.RetrySeconds?.ToString() ?? "?"}초 후 다시 시도하세요.",
            429 when q.Kind == QuotaKind.PerDay =>
                $"[일일 한도 초과] 이 모델의 오늘 무료 {q.Limit?.ToString() ?? "?"}회 소진. 태평양 자정(한국 오후 4~5시)에 리셋됩니다.",
            429 => "[한도 초과] 무료 할당량 소진. 잠시 후 다시 시도하세요.",
            >= 500 => $"[서버 오류] 구글 서버 문제입니다. 잠시 후 다시 시도하세요. ({status})",
            _ => $"[오류 {status}] {detail}",
        };
    }

    private static string ExtractMessage(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("error").GetProperty("message").GetString()
                is string m ? Trim(m, 200) : "";
        }
        catch { return Trim(json, 200); }
    }

    // 팝업/상태 메시지에 API 키가 절대 노출되지 않도록 마스킹
    private static string Sanitize(string s, string apiKey) => Logger.Sanitize(s, apiKey);

    private static string Trim(string s, int n) => s.Length <= n ? s : s[..n] + "...";

    public void Dispose() => _http.Dispose();

    private sealed record GenerateRequest(Content[] contents, GenConfig generationConfig);
    private sealed record Content(Part[] parts);
    private sealed record Part(string text);
    private sealed record GenConfig(
        [property: JsonPropertyName("temperature")] float Temperature,
        [property: JsonPropertyName("maxOutputTokens")] int MaxOutputTokens);
}
