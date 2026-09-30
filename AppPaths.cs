namespace TrayTranslator;

// 데이터 기준 폴더 결정:
// 1) exe 옆 data\settings.json 존재 → portable 모드
// 2) 기존 %AppData% settings.json 존재 → 기존 유지
// 3) 신규 → exe 옆 쓰기 가능하면 portable, 아니면 %AppData%
static class AppPaths
{
    private static string? _base;

    public static string Base
    {
        get
        {
            if (_base != null) return _base;
            string exeData = Path.Combine(AppContext.BaseDirectory, "data");
            string appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TrayTranslator");
            if (File.Exists(Path.Combine(exeData, "settings.json")))
                _base = exeData;
            else if (File.Exists(Path.Combine(appData, "settings.json")))
                _base = appData;
            else if (IsWritable(AppContext.BaseDirectory))
                _base = exeData;
            else
                _base = appData;
            try { Directory.CreateDirectory(_base); } catch { }
            return _base;
        }
    }

    public static string SettingsFile => Path.Combine(Base, "settings.json");
    public static string LogFile => Path.Combine(Base, "app.log");
    public static string UsageFile => Path.Combine(Base, "usage.json");
    public static string ModelsDir => Path.Combine(Base, "models");
    public static string ServerDir => Path.Combine(Base, "llama-server");

    private static bool IsWritable(string dir)
    {
        try
        {
            string probe = Path.Combine(dir, ".writetest");
            File.WriteAllText(probe, "1");
            File.Delete(probe);
            return true;
        }
        catch { return false; }
    }
}
