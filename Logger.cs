namespace TrayTranslator;

// %AppData%\TrayTranslator\app.log 에 동작 기록. 진단용, 실패해도 무시.
static class Logger
{
    private static readonly string Path = AppPaths.LogFile;
    private static readonly object Gate = new();

    public static string LogPath => Path;

    public static void Log(string msg)
    {
        try
        {
            lock (Gate)
            {
                var f = new FileInfo(Path);
                if (f.Exists && f.Length > 500_000) f.Delete();
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
                File.AppendAllText(Path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}\n");
            }
        }
        catch { }
    }

    public static string Preview(string? s, int n = 120)
    {
        if (string.IsNullOrEmpty(s)) return "(없음)";
        s = s.Replace("\r", " ").Replace("\n", " ");
        return s.Length <= n ? s : s[..n] + $"...({s.Length}자)";
    }

    // 로그·팝업에 API 키가 노출되지 않도록 마스킹
    public static string Sanitize(string s, string secret) =>
        string.IsNullOrEmpty(secret) ? s : s.Replace(secret, "***");

    // 여러 키를 한 번에 마스킹. 로그 호출부에서 매번 키 목록을 넘기지 않도록 사용.
    public static string SanitizeAll(string s, params string?[] secrets)
    {
        if (string.IsNullOrEmpty(s) || secrets is null) return s;
        foreach (string? secret in secrets)
        {
            if (!string.IsNullOrEmpty(secret))
                s = s.Replace(secret, "***");
        }
        return s;
    }
}
