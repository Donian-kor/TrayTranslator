using System.Text.Json;

namespace TrayTranslator;

// 미국 태평양 기준 오늘 날짜의 실제 API 호출 횟수 기록. %AppData%\TrayTranslator\usage.json
static class UsageTracker
{
    private static readonly string Path = AppPaths.UsageFile;
    private static readonly object Gate = new();
    private static Data _d = Load();

    public static int Today { get { lock (Gate) { ResetIfNewDay(); return _d.Count; } } }

    public static void Add()
    {
        lock (Gate)
        {
            ResetIfNewDay();
            _d.Count++;
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
                File.WriteAllText(Path, JsonSerializer.Serialize(_d));
            }
            catch { }
        }
    }

    private static void ResetIfNewDay()
    {
        if (_d.Date != PtDate()) _d = new Data { Date = PtDate() };
    }

    private static string PtDate()
    {
        try
        {
            var pt = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, pt).ToString("yyyy-MM-dd");
        }
        catch { return DateTime.UtcNow.ToString("yyyy-MM-dd"); }
    }

    private static Data Load()
    {
        try
        {
            if (File.Exists(Path))
                return JsonSerializer.Deserialize<Data>(File.ReadAllText(Path)) ?? new Data();
        }
        catch { }
        return new Data { Date = PtDate() };
    }

    private sealed class Data
    {
        public string Date { get; set; } = "";
        public int Count { get; set; }
    }
}
