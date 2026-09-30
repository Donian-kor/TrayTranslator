using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;

namespace TrayTranslator;

sealed class AppSettings
{
    // 메모리에는 평문으로 보관. 디스크 저장 시 SecretStore(DPAPI)로 암호화된다.
    [JsonIgnore] public string ApiKey { get; set; } = "";
    [JsonIgnore] public string DeepLApiKey { get; set; } = "";
    [JsonIgnore] public string LmStudioKey { get; set; } = "";

    // settings.json에 실제로 기록되는 필드 (암호문)
    public string ApiKeyEnc
    {
        get => SecretStore.Protect(ApiKey);
        set => ApiKey = SecretStore.Unprotect(value);
    }

    public string DeepLApiKeyEnc
    {
        get => SecretStore.Protect(DeepLApiKey);
        set => DeepLApiKey = SecretStore.Unprotect(value);
    }

    public string LmStudioKeyEnc
    {
        get => SecretStore.Protect(LmStudioKey);
        set => LmStudioKey = SecretStore.Unprotect(value);
    }

    public string LmStudioHost { get; set; } = "http://localhost:1234";
    public string LmStudioModel { get; set; } = "";
    public string BuiltinModelPath { get; set; } = "";
    public int BuiltinContextSize { get; set; } = 4096;
    public string Provider { get; set; } = "Gemini";
    public string TargetLang { get; set; } = "한국어";

    public static readonly string[] Providers = ["Gemini", "DeepL", "LM Studio", "로컬 (Hy-MT2)"];

    public bool HotkeyCtrl { get; set; } = true;
    public bool HotkeyAlt { get; set; } = false;
    public bool HotkeyShift { get; set; } = true;
    public bool HotkeyWin { get; set; } = false;
    public string HotkeyKey { get; set; } = "T";

    public string Model { get; set; } = "gemini-2.5-flash-lite";

    public static readonly string[] Models =
    [
        "gemini-2.5-flash-lite",
        "gemini-2.0-flash-lite",
        "gemini-2.5-flash",
    ];

    public static readonly string[] Languages =
        ["한국어", "English", "日本語", "中文", "Español", "Français", "Deutsch", "Tiếng Việt"];

    public static string TargetCode(string lang) => lang switch
    {
        "한국어" => "ko",
        "English" => "en",
        "日本語" => "ja",
        "中文" => "zh",
        "Español" => "es",
        "Français" => "fr",
        "Deutsch" => "de",
        "Tiếng Việt" => "vi",
        _ => "out",
    };

    public static readonly string[] HotkeyKeys =
    [
        "A","B","C","D","E","F","G","H","I","J","K","L","M",
        "N","O","P","Q","R","S","T","U","V","W","X","Y","Z",
        "0","1","2","3","4","5","6","7","8","9",
        "F1","F2","F3","F4","F5","F6","F7","F8","F9","F10","F11","F12",
    ];

    public uint HotkeyMods =>
        (HotkeyCtrl ? 0x0002u : 0) | (HotkeyAlt ? 0x0001u : 0) |
        (HotkeyShift ? 0x0004u : 0) | (HotkeyWin ? 0x0008u : 0);

    public uint HotkeyVk
    {
        get
        {
            if (HotkeyKey.Length == 1)
            {
                char c = char.ToUpperInvariant(HotkeyKey[0]);
                if (c >= 'A' && c <= 'Z') return (uint)(Keys.A + (c - 'A'));
                if (c >= '0' && c <= '9') return (uint)(Keys.D0 + (c - '0'));
            }
            if (Enum.TryParse<Keys>(HotkeyKey, true, out var k)) return (uint)k;
            return (uint)Keys.T;
        }
    }

    public string HotkeyDisplay =>
        $"{(HotkeyCtrl ? "Ctrl+" : "")}{(HotkeyAlt ? "Alt+" : "")}" +
        $"{(HotkeyShift ? "Shift+" : "")}{(HotkeyWin ? "Win+" : "")}{HotkeyKey}";

    private static string Path => AppPaths.SettingsFile;

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(Path)) return new AppSettings();

            string json = File.ReadAllText(Path);
            var s = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();

            // 구버전 호환: 암호화 전에는 "ApiKey"에 평문으로 저장돼 있었다.
            // 마이그레이션 뒤에는 필드명이 ApiKeyEnc로 바뀌므로 평문 값을 복원한다.
            using (var doc = JsonDocument.Parse(json))
            {
                if (doc.RootElement.TryGetProperty("ApiKey", out var legacyGemini)
                    && !string.IsNullOrEmpty(legacyGemini.GetString()))
                {
                    s.ApiKey = SecretStore.Unprotect(legacyGemini.GetString());
                }
                if (doc.RootElement.TryGetProperty("DeepLApiKey", out var legacyDeepl)
                    && !string.IsNullOrEmpty(legacyDeepl.GetString()))
                {
                    s.DeepLApiKey = SecretStore.Unprotect(legacyDeepl.GetString());
                }
                if (doc.RootElement.TryGetProperty("LmStudioKey", out var legacyLm)
                    && !string.IsNullOrEmpty(legacyLm.GetString()))
                {
                    s.LmStudioKey = SecretStore.Unprotect(legacyLm.GetString());
                }
            }
            return s;
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllText(Path, JsonSerializer.Serialize(this,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
