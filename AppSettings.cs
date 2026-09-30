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
    [JsonIgnore] public string DeepSeekKey { get; set; } = "";
    [JsonIgnore] public string GroqKey { get; set; } = "";
    [JsonIgnore] public string OpenAiKey { get; set; } = "";
    [JsonIgnore] public string GoogleKey { get; set; } = "";
    [JsonIgnore] public string PapagoClientId { get; set; } = "";
    [JsonIgnore] public string PapagoClientSecret { get; set; } = "";
    [JsonIgnore] public string MsTranslatorKey { get; set; } = "";

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

    public string DeepSeekKeyEnc
    {
        get => SecretStore.Protect(DeepSeekKey);
        set => DeepSeekKey = SecretStore.Unprotect(value);
    }

    public string GroqKeyEnc
    {
        get => SecretStore.Protect(GroqKey);
        set => GroqKey = SecretStore.Unprotect(value);
    }

    public string OpenAiKeyEnc
    {
        get => SecretStore.Protect(OpenAiKey);
        set => OpenAiKey = SecretStore.Unprotect(value);
    }

    public string GoogleKeyEnc
    {
        get => SecretStore.Protect(GoogleKey);
        set => GoogleKey = SecretStore.Unprotect(value);
    }

    public string PapagoClientIdEnc
    {
        get => SecretStore.Protect(PapagoClientId);
        set => PapagoClientId = SecretStore.Unprotect(value);
    }

    public string PapagoClientSecretEnc
    {
        get => SecretStore.Protect(PapagoClientSecret);
        set => PapagoClientSecret = SecretStore.Unprotect(value);
    }

    public string MsTranslatorKeyEnc
    {
        get => SecretStore.Protect(MsTranslatorKey);
        set => MsTranslatorKey = SecretStore.Unprotect(value);
    }

    public string LmStudioHost { get; set; } = "http://localhost:1234";
    public string LmStudioModel { get; set; } = "";
    public string DeepSeekHost { get; set; } = "https://api.deepseek.com";
    public string DeepSeekModel { get; set; } = "deepseek-flash";
    public string GroqHost { get; set; } = "https://api.groq.com/openai";
    public string GroqModel { get; set; } = "";
    public string OpenAiHost { get; set; } = "https://api.openai.com/v1";
    public string OpenAiModel { get; set; } = "";
    public string BuiltinModelPath { get; set; } = "";
    public int BuiltinContextSize { get; set; } = 4096;
    public string Provider { get; set; } = "Gemini";

    /// <summary>Provider 문자열을 enum으로 해석. 분기에서는 이 값을 사용할 것.</summary>
    public ProviderKind Engine => ParseProvider(Provider);
    public string TargetLang { get; set; } = "한국어";

    public static readonly string[] Providers =
    [
        "Gemini", "DeepL", "LM Studio", "로컬 (Hy-MT2)",
        "DeepSeek", "Groq", "OpenAI",
        "Google 번역", "Papago", "Microsoft 번역",
    ];

    /// <summary>
    /// 번역 엔진 식별자. 문자열 비교 대신 이 enum을 사용해 분기 누락을 컴파일/런타임에 드러낸다.
    /// 새 엔진을 추가할 때 이 enum에 값을 추가하고 아래 DisplayName/ParseProvider를 갱신한다.
    /// </summary>
    public enum ProviderKind
    {
        Gemini,
        DeepL,
        LmStudio,
        Builtin,
        // OpenAI 호환 LLM (thinking/추론 없이 즉시 응답 요구)
        DeepSeek,
        Groq,
        OpenAi,
        // 번역 전용 API (모델 선택 개념 없음)
        GoogleTranslate,
        Papago,
        MsTranslator,
    }

    /// <summary>드롭다운에 표시할 이름. 표시 문자열의 단일 진실 공급원.</summary>
    public static string DisplayName(ProviderKind kind) => kind switch
    {
        ProviderKind.Gemini => "Gemini",
        ProviderKind.DeepL => "DeepL",
        ProviderKind.LmStudio => "LM Studio",
        ProviderKind.Builtin => "로컬 (Hy-MT2)",
        ProviderKind.DeepSeek => "DeepSeek",
        ProviderKind.Groq => "Groq",
        ProviderKind.OpenAi => "OpenAI",
        ProviderKind.GoogleTranslate => "Google 번역",
        ProviderKind.Papago => "Papago",
        ProviderKind.MsTranslator => "Microsoft 번역",
        _ => throw new InvalidOperationException($"새 엔진 추가 시 DisplayName 갱신 필요: {kind}"),
    };

    /// <summary>OpenAI 호환 엔진의 기본 서버 주소와 고정 모델 목록.</summary>
    public sealed record OpenAiService(string Name, string BaseUrl, string[] Models, string KeyUrl);

    public static readonly OpenAiService[] OpenAiServices =
    [
        new("LM Studio", "http://localhost:1234", [], "설치: https://lmstudio.ai"),
        new("DeepSeek", "https://api.deepseek.com",
            ["deepseek-flash", "deepseek-v4-pro"],
            "https://platform.deepseek.com"),
        new("Groq", "https://api.groq.com/openai", [],
            "https://console.groq.com/keys"),
        new("OpenAI", "https://api.openai.com/v1", [],
            "https://platform.openai.com/api-keys"),
    ];

    public static OpenAiService ServiceOf(ProviderKind kind) => kind switch
    {
        ProviderKind.LmStudio => OpenAiServices[0],
        ProviderKind.DeepSeek => OpenAiServices[1],
        ProviderKind.Groq => OpenAiServices[2],
        ProviderKind.OpenAi => OpenAiServices[3],
        _ => throw new InvalidOperationException($"OpenAI 호환 엔진 아님: {kind}"),
    };

    /// <summary>API 키 입력창이 필요한 엔진인지. 로컬 엔진은 키가 필요 없다.</summary>
    public static bool NeedsKey(ProviderKind kind) => kind is not (ProviderKind.LmStudio or ProviderKind.Builtin);

    /// <summary>모델 선택 드롭다운을 보여줘야 하는 엔진인지. 번역 전용 API는 모델 개념이 없다.</summary>
    public static bool HasModel(ProviderKind kind) => kind is not (
        ProviderKind.DeepL or ProviderKind.Builtin
        or ProviderKind.GoogleTranslate or ProviderKind.Papago or ProviderKind.MsTranslator);

    /// <summary>OpenAI 호환 LLM 엔진인지 (서버에서 모델 목록 조회 가능).</summary>
    public static bool IsOpenAiCompat(ProviderKind kind) => kind is (
        ProviderKind.LmStudio or ProviderKind.DeepSeek or ProviderKind.Groq or ProviderKind.OpenAi);

    /// <summary>번역 전용 API 엔진인지 (추론 개념 없이 항상 즉시 응답).</summary>
    public static bool IsTranslateApi(ProviderKind kind) => kind is (
        ProviderKind.DeepL or ProviderKind.GoogleTranslate
        or ProviderKind.Papago or ProviderKind.MsTranslator);

    /// <summary>표시 이름 → enum. 알 수 없는 값은 Gemini로 대체(구버전/손상 파일 대비).</summary>
    public static ProviderKind ParseProvider(string? name)
    {
        foreach (var kind in Enum.GetValues<ProviderKind>())
        {
            if (DisplayName(kind) == name) return kind;
        }
        return ProviderKind.Gemini;
    }

    /// <summary>enum → settings.json에 기록할 문자열.</summary>
    public static string ProviderName(ProviderKind kind) => DisplayName(kind);

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
            bool migrated = false;
            using (var doc = JsonDocument.Parse(json))
            {
                if (doc.RootElement.TryGetProperty("ApiKey", out var legacyGemini)
                    && !string.IsNullOrEmpty(legacyGemini.GetString()))
                {
                    s.ApiKey = SecretStore.Unprotect(legacyGemini.GetString());
                    migrated = true;
                }
                if (doc.RootElement.TryGetProperty("DeepLApiKey", out var legacyDeepl)
                    && !string.IsNullOrEmpty(legacyDeepl.GetString()))
                {
                    s.DeepLApiKey = SecretStore.Unprotect(legacyDeepl.GetString());
                    migrated = true;
                }
                if (doc.RootElement.TryGetProperty("LmStudioKey", out var legacyLm)
                    && !string.IsNullOrEmpty(legacyLm.GetString()))
                {
                    s.LmStudioKey = SecretStore.Unprotect(legacyLm.GetString());
                    migrated = true;
                }
            }

            // 평문 키가 남아있었다면 즉시 암호화해 덮어쓴다.
            // 사용자가 설정 창을 열기 전에 이미 보호된다.
            if (migrated) s.Save();
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
