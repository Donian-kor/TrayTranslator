using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace TrayTranslator;

// 내장 로컬 번역: llama-server 자식 프로세스 + Hy-MT2 GGUF. 서버·키·요금 없음.
// 서버 파일: %AppData%\TrayTranslator\llama-server\ (자동 다운로드)
// 모델: %AppData%\TrayTranslator\models\hy-mt2.gguf (또는 사용자 지정 파일)
sealed class BuiltinClient : IDisposable
{
    public const string ServerVersion = "b11254-vulkan";
    public const string ServerZipUrl =
        "https://github.com/ggml-org/llama.cpp/releases/download/b11254/llama-b11254-bin-win-vulkan-x64.zip";
    public const string ModelDownloadUrl =
        "https://huggingface.co/tencent/Hy-MT2-1.8B-GGUF/resolve/main/Hy-MT2-1.8B-Q4_K_M.gguf";
    public const int Port = 18080;

    public static string BaseDir => AppPaths.Base;
    public static string ServerDir => AppPaths.ServerDir;
    public static string ServerExe => Path.Combine(ServerDir, "llama-server.exe");
    public static string ModelDir => AppPaths.ModelsDir;
    public static string DefaultModelPath => Path.Combine(ModelDir, "hy-mt2.gguf");

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern int GetShortPathName(string longPath, StringBuilder shortPath, int buf);

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(180) };
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<(string Text, string Lang), string> _cache = new();
    private Process? _server;
    private int _serverCtx;

    public static bool HasNonAscii(string s) => s.Any(c => c > 127);

    // llama.cpp는 비ASCII 경로를 못 열 수 있으므로 ASCII 경로 보장
    public static string ResolveLoadPath(string path)
    {
        if (!HasNonAscii(path) && File.Exists(path)) return path;
        try
        {
            var sb = new StringBuilder(512);
            if (GetShortPathName(path, sb, sb.Capacity) > 0)
            {
                string? shortPath = sb.ToString();
                if (!string.IsNullOrEmpty(shortPath) && !HasNonAscii(shortPath) && File.Exists(shortPath))
                    return shortPath;
            }
        }
        catch { }
        string dest = DefaultModelPath;
        if (!File.Exists(dest) || new FileInfo(dest).Length != new FileInfo(path).Length)
        {
            Directory.CreateDirectory(ModelDir);
            File.Copy(path, dest, overwrite: true);
        }
        return dest;
    }

    public static (bool Exists, long Size) ModelStatus(string? customPath)
    {
        string path = string.IsNullOrWhiteSpace(customPath) ? DefaultModelPath : customPath!;
        try
        {
            if (File.Exists(path)) return (true, new FileInfo(path).Length);
        }
        catch { }
        return (false, 0);
    }

    public static bool ServerFilesPresent
    {
        get
        {
            try
            {
                if (!File.Exists(ServerExe)) return false;
                string verFile = Path.Combine(ServerDir, "VERSION");
                return File.Exists(verFile) && File.ReadAllText(verFile).Trim() == ServerVersion;
            }
            catch { return false; }
        }
    }

    public async Task<(bool Ok, string Text)> TranslateAsync(
        string text, string targetLang, string? customPath, int ctxSize, Func<string, Task>? onProgress = null)
    {
        var cacheKey = (text, targetLang);
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            Logger.Log("캐시 적중 (API 호출 없음)");
            return (true, cached);
        }
        string target = LmStudioClient.LangMap.TryGetValue(targetLang, out var mapped) ? mapped : targetLang;

        await _gate.WaitAsync();
        try
        {
            if (!ServerFilesPresent)
            {
                if (onProgress != null) await onProgress("서버 파일 다운로드 중... (최초 1회)");
                var (okDl, msgDl) = await DownloadServerAsync(async pct =>
                {
                    if (onProgress != null) await onProgress($"서버 파일 다운로드 중... {pct}%");
                });
                if (!okDl) return (false, msgDl);
            }
            string modelArg = string.IsNullOrWhiteSpace(customPath) ? DefaultModelPath : customPath!;
            if (!File.Exists(modelArg))
                return (false, "[모델 없음] 설정에서 모델 파일을 등록하거나 Q4를 다운로드하세요.");

            var (okSrv, errSrv) = await EnsureServerAsync(modelArg, ctxSize, onProgress);
            if (!okSrv) return (false, errSrv);

            var body = new ChatRequest("hy",
                [new ChatMessage("user", $"Translate the following text into {target}. Note that you should only output the translated result without any additional explanation:\n\n{text}")],
                0.7f, 0.6f, 20, 1.05f, 1024);
            try
            {
                using var res = await Http.PostAsJsonAsync($"http://127.0.0.1:{Port}/v1/chat/completions", body);
                var json = await res.Content.ReadAsStringAsync();
                if (!res.IsSuccessStatusCode)
                    return (false, $"[서버 오류 {(int)res.StatusCode}] {Trim(json, 200)}");
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var t = doc.RootElement.GetProperty("choices")[0]
                    .GetProperty("message").GetProperty("content").GetString()?.Trim();
                if (string.IsNullOrEmpty(t)) return (false, "[빈 응답] 모델이 빈 결과를 반환했습니다.");
                if (_cache.Count > 100) _cache.Clear();
                _cache[cacheKey] = t;
                return (true, t);
            }
            catch (TaskCanceledException)
            {
                return (false, "[시간 초과] 180초 내 응답 없음.");
            }
            catch (HttpRequestException ex)
            {
                return (false, "[연결 오류] " + Trim(ex.Message, 150));
            }
        }
        catch (Exception ex)
        {
            return (false, "[실행 오류] " + ex.Message.Split('\n')[0]);
        }
        finally { _gate.Release(); }
    }

    public async Task<(bool Ok, string Message)> TestAsync(string? customPath, int ctxSize)
    {
        if (!ServerFilesPresent)
            return (false, "[서버 없음] 첫 번역 시 서버 파일을 자동 다운로드합니다.");
        string modelArg = string.IsNullOrWhiteSpace(customPath) ? DefaultModelPath : customPath!;
        if (!File.Exists(modelArg))
            return (false, "[모델 없음] 설정에서 모델 파일을 등록하거나 Q4를 다운로드하세요.");
        var (ok, err) = await EnsureServerAsync(modelArg, ctxSize, null);
        if (!ok) return (false, err);
        try
        {
            using var res = await Http.GetAsync($"http://127.0.0.1:{Port}/v1/models");
            var json = await res.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var ids = doc.RootElement.GetProperty("data").EnumerateArray()
                .Select(e => e.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "")
                .Where(s => s != "").Take(3).ToList();
            return (true, $"로컬 서버 실행 중 ({string.Join(", ", ids)})");
        }
        catch (Exception ex)
        {
            return (false, "[오류] " + Trim(ex.Message, 150));
        }
    }

    private async Task<(bool Ok, string Error)> EnsureServerAsync(string modelPath, int ctxSize, Func<string, Task>? onProgress)
    {
        ctxSize = Math.Clamp(ctxSize, 1024, 65536);
        // 실행 중 서버가 요청 ctx와 일치하면 재사용, 다르면 재시작
        if (_server is { HasExited: false } && _serverCtx == ctxSize && await IsHealthyAsync())
            return (true, "");
        KillOurs();
        if (await IsHealthyAsync())
            return (false, $"[포트 충돌] {Port}번 포트를 다른 프로그램이 사용 중입니다.");
        string loadPath;
        try { loadPath = ResolveLoadPath(modelPath); }
        catch (Exception ex) { return (false, "[모델 경로 오류] " + ex.Message.Split('\n')[0]); }
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ServerExe,
                WorkingDirectory = ServerDir,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("-m"); psi.ArgumentList.Add(loadPath);
            psi.ArgumentList.Add("--host"); psi.ArgumentList.Add("127.0.0.1");
            psi.ArgumentList.Add("--port"); psi.ArgumentList.Add(Port.ToString());
            psi.ArgumentList.Add("-c"); psi.ArgumentList.Add(ctxSize.ToString());
            psi.ArgumentList.Add("-ngl"); psi.ArgumentList.Add("99");
            psi.ArgumentList.Add("--jinja");
            psi.ArgumentList.Add("--log-disable");
            _server = Process.Start(psi);
            if (_server == null) return (false, "[서버 실행 실패] 프로세스를 시작할 수 없습니다.");
            _serverCtx = ctxSize;
        }
        catch (Exception ex)
        {
            return (false, "[서버 실행 실패] " + ex.Message.Split('\n')[0]);
        }

        if (onProgress != null) await onProgress("모델 로딩 중... (최초 1회, 수십 초)");
        for (int i = 0; i < 90; i++)
        {
            await Task.Delay(2000);
            if (await IsHealthyAsync())
            {
                Logger.Log("내장 로컬 서버 시작됨");
                return (true, "");
            }
            if (_server.HasExited)
                return (false, $"[서버 종료됨] 종료 코드 {_server.ExitCode}. 모델 파일이 손상됐을 수 있습니다.");
        }
        return (false, "[서버 시간 초과] 3분 내 시작 실패.");
    }

    private static async Task<bool> IsHealthyAsync()
    {
        try
        {
            using var res = await Http.GetAsync($"http://127.0.0.1:{Port}/health");
            return res.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // 비정상 종료 잔재(우리 폴더의 서버)만 정리. 사용자의 다른 서버는 건드리지 않음.
    private void KillOurs()
    {
        try
        {
            foreach (var p in Process.GetProcessesByName("llama-server"))
            {
                try
                {
                    string? file = p.MainModule?.FileName;
                    if (file != null && file.StartsWith(ServerDir, StringComparison.OrdinalIgnoreCase)
                        && (_server == null || p.Id != _server.Id))
                        p.Kill();
                }
                catch { }
            }
        }
        catch { }
        if (_server is { HasExited: false })
        {
            try { _server.Kill(); } catch { }
        }
        _server = null;
        _serverCtx = 0;
    }

    public async Task<(bool Ok, string Message)> DownloadServerAsync(Func<int, Task>? onPercent = null)
    {
        try
        {
            // 버전이 바뀌면旧 파일 정리 후 새로 받음
            string verFile = Path.Combine(ServerDir, "VERSION");
            string cur = "";
            try { if (File.Exists(verFile)) cur = (await File.ReadAllTextAsync(verFile)).Trim(); } catch { }
            if (cur != ServerVersion)
            {
                try { Directory.Delete(ServerDir, recursive: true); } catch { }
            }
            Directory.CreateDirectory(ServerDir);
            string zip = Path.Combine(ServerDir, "llama-server.zip");
            using var http = new HttpClient { Timeout = TimeSpan.FromHours(1) };
            using var res = await http.GetAsync(ServerZipUrl, HttpCompletionOption.ResponseHeadersRead);
            if (!res.IsSuccessStatusCode)
                return (false, $"[다운로드 오류] {(int)res.StatusCode}");
            long? total = res.Content.Headers.ContentLength;
            using var net = await res.Content.ReadAsStreamAsync();
            using var fs = new FileStream(zip, FileMode.Create, FileAccess.Write, FileShare.None);
            var buf = new byte[1 << 20];
            long done = 0;
            int read, lastPct = -1;
            while ((read = await net.ReadAsync(buf)) > 0)
            {
                await fs.WriteAsync(buf.AsMemory(0, read));
                done += read;
                if (total > 0 && onPercent != null)
                {
                    int pct = (int)(done * 100 / total.Value);
                    if (pct != lastPct) { lastPct = pct; await onPercent(pct); }
                }
            }
            fs.Close();
            ZipFile.ExtractToDirectory(zip, ServerDir, overwriteFiles: true);
            try { File.Delete(zip); } catch { }
            try { await File.WriteAllTextAsync(verFile, ServerVersion); } catch { }
            return File.Exists(ServerExe)
                ? (true, "서버 파일 준비 완료")
                : (false, "[압축 해제 오류] 서버 실행 파일을 찾을 수 없습니다.");
        }
        catch (Exception ex)
        {
            return (false, "[다운로드 오류] " + ex.Message.Split('\n')[0]);
        }
    }

    public async Task<(bool Ok, string Message)> DownloadModelAsync(Func<int, Task>? onPercent = null)
    {
        try
        {
            Directory.CreateDirectory(ModelDir);
            using var http = new HttpClient { Timeout = TimeSpan.FromHours(2) };
            using var res = await http.GetAsync(ModelDownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            if (!res.IsSuccessStatusCode)
                return (false, $"[다운로드 오류] {(int)res.StatusCode}");
            long? total = res.Content.Headers.ContentLength;
            using var net = await res.Content.ReadAsStreamAsync();
            using var fs = new FileStream(DefaultModelPath, FileMode.Create, FileAccess.Write, FileShare.None);
            var buf = new byte[1 << 20];
            long done = 0;
            int read, lastPct = -1;
            while ((read = await net.ReadAsync(buf)) > 0)
            {
                await fs.WriteAsync(buf.AsMemory(0, read));
                done += read;
                if (total > 0 && onPercent != null)
                {
                    int pct = (int)(done * 100 / total.Value);
                    if (pct != lastPct) { lastPct = pct; await onPercent(pct); }
                }
            }
            return (true, $"다운로드 완료 ({done / 1024 / 1024}MB)");
        }
        catch (Exception ex)
        {
            try { if (File.Exists(DefaultModelPath)) File.Delete(DefaultModelPath); } catch { }
            return (false, "[다운로드 오류] " + ex.Message.Split('\n')[0]);
        }
    }

    private static string Trim(string s, int n) => s.Length <= n ? s : s[..n] + "...";

    public void Dispose()
    {
        try
        {
            if (_server is { HasExited: false }) _server.Kill();
        }
        catch { }
        _server?.Dispose();
        _server = null;
        _gate.Dispose();
    }

    private sealed record ChatRequest(
        string model,
        ChatMessage[] messages,
        float temperature,
        [property: JsonPropertyName("top_p")] float TopP,
        [property: JsonPropertyName("top_k")] int TopK,
        [property: JsonPropertyName("repeat_penalty")] float RepeatPenalty,
        [property: JsonPropertyName("max_tokens")] int MaxTokens);
    private sealed record ChatMessage(string role, string content);
}
