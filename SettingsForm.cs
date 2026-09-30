using System.Runtime.InteropServices;

namespace TrayTranslator;

sealed class SettingsForm : Form
{
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    private const int TestId = 9999;

    private readonly Label _lblKey;
    private readonly TextBox _txtKey;
    // Papago만 사용하는 두 번째 입력창 (Client Secret) / MS 리전 겸용
    private readonly Label _lblKey2;
    private readonly TextBox _txtKey2;
    // Papago만 사용하는 원본 언어 드롭다운 (source 필수, auto 미지원)
    private readonly Label _lblSource;
    private readonly ComboBox _cmbSource;
    private readonly Label _lblHost;
    private readonly TextBox _txtHost;
    private readonly Label _lblLang;
    private readonly ComboBox _lang;
    private readonly ComboBox _provider;
    private readonly Label _lblHotkey;
    private readonly CheckBox _chkCtrl, _chkAlt, _chkShift, _chkWin;
    private readonly ComboBox _cmbKey;
    private readonly Label _lblModel;
    private readonly ComboBox _cmbModel;
    private readonly Label _lblFile;
    private readonly Button _btnBrowse;
    private readonly Button _btnDownload;
    private readonly Label _lblCtx;
    private readonly ComboBox _cmbCtx;
    private readonly Label _lblTest;
    private readonly Button _btnTest;
    private readonly Label _lblHint;
    private readonly Label _lblUsage;
    private readonly Button _btnSave;
    private readonly Button _btnCancel;
    private readonly AppSettings _settings;
    private readonly uint _origMods;
    private readonly string _origKey;
    // 엔진 전환 시점의 ComboBox는 Provider가 이미 새 값을 가리키고 있으므로,
    // 직전 엔진을 따로 기억해 입력창 값을 올바른 변수에 보관한다.
    private AppSettings.ProviderKind _prevProvider;
    private string _builtinPath;
    private int _builtinCtx;
    private bool _downloading;
    // 엔진별 메모리 보관값
    private readonly Dictionary<AppSettings.ProviderKind, EngineFields> _fields = new();

    private readonly GeminiClient _geminiProbe = new();
    private readonly DeepLClient _deeplProbe = new();
    private readonly LmStudioClient _lmProbe = new();
    private readonly BuiltinClient _builtinProbe = new();
    private readonly OpenAiCompatClient _deepseekProbe = new("DeepSeek", supportsThinkingToggle: true);
    private readonly OpenAiCompatClient _groqProbe = new("Groq");
    private readonly OpenAiCompatClient _openaiProbe = new("OpenAI");
    private readonly GoogleTranslateClient _googleProbe = new();
    private readonly PapagoClient _papagoProbe = new();
    private readonly MsTranslatorClient _msProbe = new();

    /// <summary>엔진 하나에 필요한 설정 값을 묶어 보관한다.</summary>
    private sealed class EngineFields
    {
        public string Key = "";
        public string Second = "";   // Papago Client Secret 등 두 번째 값
        public string Region = "";   // MS Translator 리전 등
        public string Source = "";   // Papago 원본 언어 (표시 이름)
        public string Host = "";
        public string Model = "";
    }

    private EngineFields Fields(AppSettings.ProviderKind kind)
    {
        if (!_fields.TryGetValue(kind, out var f))
        {
            f = new EngineFields();
            _fields[kind] = f;
        }
        return f;
    }

    // settings.json → 메모리. 새 엔진을 추가할 때 여기만 갱신하면 된다.
    private void LoadFieldsFromSettings(AppSettings s)
    {
        Fields(AppSettings.ProviderKind.Gemini).Key = s.ApiKey;
        Fields(AppSettings.ProviderKind.Gemini).Model = s.Model;
        Fields(AppSettings.ProviderKind.DeepL).Key = s.DeepLApiKey;
        Fields(AppSettings.ProviderKind.LmStudio).Key = s.LmStudioKey;
        Fields(AppSettings.ProviderKind.LmStudio).Host = s.LmStudioHost;
        Fields(AppSettings.ProviderKind.LmStudio).Model = s.LmStudioModel;
        Fields(AppSettings.ProviderKind.DeepSeek).Key = s.DeepSeekKey;
        Fields(AppSettings.ProviderKind.DeepSeek).Host = s.DeepSeekHost;
        Fields(AppSettings.ProviderKind.DeepSeek).Model = s.DeepSeekModel;
        Fields(AppSettings.ProviderKind.Groq).Key = s.GroqKey;
        Fields(AppSettings.ProviderKind.Groq).Host = s.GroqHost;
        Fields(AppSettings.ProviderKind.Groq).Model = s.GroqModel;
        Fields(AppSettings.ProviderKind.OpenAi).Key = s.OpenAiKey;
        Fields(AppSettings.ProviderKind.OpenAi).Host = s.OpenAiHost;
        Fields(AppSettings.ProviderKind.OpenAi).Model = s.OpenAiModel;
        Fields(AppSettings.ProviderKind.GoogleTranslate).Key = s.GoogleKey;
        Fields(AppSettings.ProviderKind.Papago).Key = s.PapagoClientId;
        Fields(AppSettings.ProviderKind.Papago).Second = s.PapagoClientSecret;
        Fields(AppSettings.ProviderKind.Papago).Source = s.PapagoSource;
        Fields(AppSettings.ProviderKind.MsTranslator).Key = s.MsTranslatorKey;
        Fields(AppSettings.ProviderKind.MsTranslator).Region = s.MsTranslatorRegion;
    }

    // 메모리 → settings.json
    private void SaveFieldsToSettings(AppSettings s)
    {
        s.ApiKey = Fields(AppSettings.ProviderKind.Gemini).Key;
        s.Model = Fields(AppSettings.ProviderKind.Gemini).Model;
        s.DeepLApiKey = Fields(AppSettings.ProviderKind.DeepL).Key;
        s.LmStudioKey = Fields(AppSettings.ProviderKind.LmStudio).Key;
        s.LmStudioHost = Fields(AppSettings.ProviderKind.LmStudio).Host;
        s.LmStudioModel = Fields(AppSettings.ProviderKind.LmStudio).Model;
        s.DeepSeekKey = Fields(AppSettings.ProviderKind.DeepSeek).Key;
        s.DeepSeekHost = Fields(AppSettings.ProviderKind.DeepSeek).Host;
        s.DeepSeekModel = Fields(AppSettings.ProviderKind.DeepSeek).Model;
        s.GroqKey = Fields(AppSettings.ProviderKind.Groq).Key;
        s.GroqHost = Fields(AppSettings.ProviderKind.Groq).Host;
        s.GroqModel = Fields(AppSettings.ProviderKind.Groq).Model;
        s.OpenAiKey = Fields(AppSettings.ProviderKind.OpenAi).Key;
        s.OpenAiHost = Fields(AppSettings.ProviderKind.OpenAi).Host;
        s.OpenAiModel = Fields(AppSettings.ProviderKind.OpenAi).Model;
        s.GoogleKey = Fields(AppSettings.ProviderKind.GoogleTranslate).Key;
        s.PapagoClientId = Fields(AppSettings.ProviderKind.Papago).Key;
        s.PapagoClientSecret = Fields(AppSettings.ProviderKind.Papago).Second;
        s.PapagoSource = Fields(AppSettings.ProviderKind.Papago).Source;
        s.MsTranslatorKey = Fields(AppSettings.ProviderKind.MsTranslator).Key;
        s.MsTranslatorRegion = Fields(AppSettings.ProviderKind.MsTranslator).Region;
    }

    public SettingsForm(AppSettings settings)
    {
        _settings = settings;
        _origMods = settings.HotkeyMods;
        _origKey = settings.HotkeyKey;
        _prevProvider = settings.Engine;
        _builtinPath = settings.BuiltinModelPath;
        _builtinCtx = settings.BuiltinContextSize >= 1024 ? settings.BuiltinContextSize : 4096;
        LoadFieldsFromSettings(settings);

        Text = "TrayTranslator 설정";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 9f);

        var lblProvider = new Label { Text = "번역 엔진:", Location = new Point(12, 12), AutoSize = true };
        _provider = new ComboBox
        {
            Location = new Point(12, 32), Width = 174,
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        _provider.Items.AddRange(AppSettings.Providers);
        _provider.SelectedItem = AppSettings.Providers.Contains(settings.Provider)
            ? settings.Provider : AppSettings.DisplayName(AppSettings.ParseProvider(settings.Provider));
        _provider.SelectedIndexChanged += (_, _) => SwitchProvider();

        _lblLang = new Label { Text = "번역 대상 언어:", Location = new Point(196, 12), AutoSize = true };
        _lang = new ComboBox
        {
            Location = new Point(196, 32), Width = 160,
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        _lang.Items.AddRange(AppSettings.Languages);
        _lang.SelectedItem = AppSettings.Languages.Contains(settings.TargetLang)
            ? settings.TargetLang : AppSettings.Languages[0];

        _lblKey = new Label { Location = new Point(12, 60), AutoSize = true };
        _txtKey = new TextBox { Location = new Point(12, 80), Width = 356, UseSystemPasswordChar = true };

        _lblKey2 = new Label { Location = new Point(12, 108), AutoSize = true };
        _txtKey2 = new TextBox { Location = new Point(12, 128), Width = 356, UseSystemPasswordChar = true };

        _lblSource = new Label { Text = "원본 언어 (Papago는 자동 감지 없음):", AutoSize = true };
        _cmbSource = new ComboBox { Width = 356, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbSource.Items.AddRange(AppSettings.Languages);

        _lblHost = new Label { Text = "서버 주소:", AutoSize = true };
        _txtHost = new TextBox { Width = 356 };

        _lblHotkey = new Label { Text = "번역 핫키 (수식키 1개 이상 필수):", AutoSize = true };
        _chkCtrl = new CheckBox { Text = "Ctrl", AutoSize = true, Checked = settings.HotkeyCtrl };
        _chkAlt = new CheckBox { Text = "Alt", AutoSize = true, Checked = settings.HotkeyAlt };
        _chkShift = new CheckBox { Text = "Shift", AutoSize = true, Checked = settings.HotkeyShift };
        _chkWin = new CheckBox { Text = "Win", AutoSize = true, Checked = settings.HotkeyWin };
        _cmbKey = new ComboBox { Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbKey.Items.AddRange(AppSettings.HotkeyKeys);
        _cmbKey.SelectedItem = AppSettings.HotkeyKeys.Contains(settings.HotkeyKey)
            ? settings.HotkeyKey : "T";

        _lblModel = new Label { AutoSize = true };
        _cmbModel = new ComboBox { Width = 356, DropDownStyle = ComboBoxStyle.DropDown };
        _cmbModel.Items.AddRange(AppSettings.Models);

        _lblFile = new Label { Size = new Size(356, 40), ForeColor = Color.Gray };
        _btnBrowse = new Button { Text = "내장 모델 찾기...", AutoSize = false };
        _btnBrowse.Click += OnBrowse;
        _btnDownload = new Button { Text = "내장 모델 다운로드 (1.1GB)", AutoSize = false };
        _btnDownload.Click += OnDownload;
        _lblCtx = new Label { Text = "컨텍스트 크기 (변경 시 서버 재시작):", AutoSize = true };
        _cmbCtx = new ComboBox { Width = 200, DropDownStyle = ComboBoxStyle.DropDown };
        _cmbCtx.Items.AddRange(["2048", "4096", "8192", "16384", "32768"]);

        _btnTest = new Button { Text = "연결 테스트", Size = new Size(356, 30) };
        _btnTest.Click += OnTest;
        _lblTest = new Label
        {
            Size = new Size(356, 32),
            ForeColor = Color.Gray, Text = "테스트를 눌러 연결이 유효한지 확인하세요.",
        };

        _lblHint = new Label { AutoSize = true, ForeColor = Color.Gray };
        _lblUsage = new Label
        {
            Text = $"오늘 API 호출: {UsageTracker.Today}회 (태평양 기준)",
            AutoSize = true, ForeColor = Color.Gray,
        };
        void RefreshHint() => _lblHint.Text =
            $"사용법: 텍스트 드래그 → {PreviewDisplay()}";
        _chkCtrl.CheckedChanged += (_, _) => RefreshHint();
        _chkAlt.CheckedChanged += (_, _) => RefreshHint();
        _chkShift.CheckedChanged += (_, _) => RefreshHint();
        _chkWin.CheckedChanged += (_, _) => RefreshHint();
        _cmbKey.SelectedIndexChanged += (_, _) => RefreshHint();
        RefreshHint();

        _btnSave = new Button { Text = "저장", Width = 75, DialogResult = DialogResult.OK };
        _btnCancel = new Button { Text = "취소", Width = 75, DialogResult = DialogResult.Cancel };
        _btnSave.Click += OnSave;

        Controls.AddRange([lblProvider, _provider, _lblKey, _txtKey, _lblKey2, _txtKey2, _lblHost, _txtHost,
            _lblSource, _cmbSource,
            _lblFile, _btnBrowse, _btnDownload, _lblCtx, _cmbCtx,
            _lblLang, _lang, _lblHotkey, _chkCtrl, _chkAlt, _chkShift, _chkWin, _cmbKey,
            _lblModel, _cmbModel, _btnTest, _lblTest, _lblHint, _lblUsage, _btnSave, _btnCancel]);
        AcceptButton = _btnSave;
        CancelButton = _btnCancel;

        ApplyMaterialTheme();

        LoadProviderFields();
        LayoutForProvider();
        RefreshFileStatus();
        if (IsLM) _ = RefreshLmModelsAsync();
    }

    // 머티리얼 테마 일괄 적용. 레이아웃(위치)은 건드리지 않고 색·폰트·플랫 스타일만 바꾼다.
    private void ApplyMaterialTheme()
    {
        foreach (Control c in Controls)
        {
            switch (c)
            {
                case Button b when b == _btnSave:
                    Material.Contained(b);
                    b.Size = new Size(110, 34);
                    break;
                case Button b when b == _btnCancel:
                    Material.TextButton(b);
                    b.Size = new Size(104, 34);
                    break;
                case Button b when b == _btnBrowse:
                    Material.Outlined(b);
                    break;
                case Button b:
                    Material.Tonal(b);
                    break;
                case TextBox t:
                    Material.Field(t);
                    break;
                case ComboBox cb:
                    Material.FieldCombo(cb);
                    break;
                case Label l when l == _lblTest || l == _lblHint || l == _lblUsage || l == _lblFile:
                    break; // 상태 문구는 기존 회색 유지
                case Label l:
                    Material.SectionLabel(l);
                    break;
                case CheckBox ch:
                    ch.Font = new Font("Segoe UI", 9f);
                    break;
            }
        }
        _btnTest.Size = new Size(356, 36);
    }

    private string Provider => _provider.SelectedItem?.ToString()
        ?? AppSettings.DisplayName(AppSettings.ParseProvider(_settings.Provider));
    private AppSettings.ProviderKind Engine => AppSettings.ParseProvider(Provider);
    private AppSettings.ProviderKind PrevEngine => _prevProvider;
    private bool IsGemini => Engine == AppSettings.ProviderKind.Gemini;
    private bool IsDeepL => Engine == AppSettings.ProviderKind.DeepL;
    private bool IsLM => Engine == AppSettings.ProviderKind.LmStudio;
    private bool IsBuiltin => Engine == AppSettings.ProviderKind.Builtin;

    // 엔진 전환: 입력 중이던 값을 메모리에 보관, 선택된 엔진의 값을 표시
    private void SwitchProvider()
    {
        // ComboBox는 드롭다운 선택 즉시 SelectedIndexChanged가 발생해
        // Provider가 이미 새 엔진으로 바뀐 상태다. 따라서 이전 엔진의 값을
        // 보관하려면 Provider를 기준으로 판단하는 StashCurrentFields를
        // 먼저 돌릴 수 없다. 전환 직전 엔진 기준으로 임시 저장한 뒤 불러온다.
        StashPreviousProviderFields();
        LoadProviderFields();
        LayoutForProvider();
        if (IsLM) _ = RefreshLmModelsAsync();
    }

    // ComboBox 이벤트 특성상 Provider가 이미 새 값을 가리키고 있으므로,
    // 직전 엔진(PrevEngine)을 기준으로 입력창을 해당 엔진의 메모리 슬롯에 보관한다.
    private void StashPreviousProviderFields()
    {
        StashFields(PrevEngine);
        _prevProvider = Engine;
    }

    // 화면(입력창) → 해당 엔진의 메모리 슬롯.
    private void StashFields(AppSettings.ProviderKind kind)
    {
        if (kind == AppSettings.ProviderKind.Builtin)
        {
            _builtinCtx = ParseCtx(_cmbCtx.Text);
            return;
        }

        var f = Fields(kind);
        f.Key = _txtKey.Text.Trim();
        if (kind == AppSettings.ProviderKind.Gemini)
            f.Model = AppSettings.ModelId(AppSettings.GeminiModelOptions, _cmbModel.Text.Trim());
        if (kind == AppSettings.ProviderKind.Papago)
        {
            f.Second = _txtKey2.Text.Trim();
            f.Source = _cmbSource.SelectedItem?.ToString() ?? "한국어";
        }
        if (kind == AppSettings.ProviderKind.MsTranslator)
            f.Region = _txtKey2.Text.Trim();
        if (AppSettings.IsOpenAiCompat(kind))
        {
            f.Host = _txtHost.Text.Trim();
            f.Model = AppSettings.ModelId(AppSettings.ServiceOf(kind).Models, _cmbModel.Text.Trim());
        }
    }

    private bool _refreshingModels;

    // LM Studio / Groq / OpenAI: 서버의 모델 목록을 가져와 드롭다운에 채운다.
    // 키가 없거나 조회가 실패하면 목록을 비워 수동 입력만 허용한다.
    private async Task RefreshLmModelsAsync()
    {
        if (_refreshingModels) return;
        _refreshingModels = true;
        try
        {
            var kind = Engine;
            if (!AppSettings.IsOpenAiCompat(kind)) return;

            // DeepSeek처럼 고정 목록이 있는 서비스는 목록 조회가 필요 없다.
            if (AppSettings.ServiceOf(kind).Models.Length > 0) return;

            string host = _txtHost.Text.Trim();
            if (string.IsNullOrWhiteSpace(host))
                host = AppSettings.ServiceOf(kind).BaseUrl;
            string apiKey = _txtKey.Text.Trim();

            List<string> ids;
            if (kind == AppSettings.ProviderKind.LmStudio)
                ids = await _lmProbe.ListModelsAsync(host);
            else
                ids = await ProbeOf(kind).ListModelsAsync(host, apiKey);

            if (ids.Count > 0)
            {
                string keep = _cmbModel.Text.Trim();
                _cmbModel.Items.Clear();
                _cmbModel.Items.AddRange([.. ids]);
                _cmbModel.Text = ids.Contains(keep) ? keep : ids[0];
            }
            else if (_cmbModel.Items.Count == 0)
            {
                _cmbModel.Text = "";
            }
        }
        finally { _refreshingModels = false; }
    }

    private OpenAiCompatClient ProbeOf(AppSettings.ProviderKind kind) => kind switch
    {
        AppSettings.ProviderKind.DeepSeek => _deepseekProbe,
        AppSettings.ProviderKind.Groq => _groqProbe,
        AppSettings.ProviderKind.OpenAi => _openaiProbe,
        AppSettings.ProviderKind.LmStudio => throw new InvalidOperationException("LM Studio는 별도 클라이언트 사용"),
        _ => throw new InvalidOperationException($"OpenAI 호환 엔진 아님: {kind}"),
    };

    // 저장/테스트 시점에는 ComboBox의 선택이 이미 확정돼 있으므로 현재 엔진(Engine)을 기준으로 보관한다.
    private void StashCurrentFields() => StashFields(Engine);

    private static int ParseCtx(string s)
    {
        if (int.TryParse(s.Trim(), out int v)) return Math.Clamp(v, 1024, 65536);
        return 4096;
    }

    // 메모리 → 화면. Papago만 두 번째 입력창을 쓰고, OpenAI 호환만 주소/모델을 쓴다.
    private void LoadProviderFields()
    {
        var kind = Engine;

        if (kind == AppSettings.ProviderKind.Builtin)
        {
            // 로컬 엔진은 API 키가 필요 없다. 이전 엔진의 키가 입력창에 남으면
            // 다른 분기에서 잘못 읽혀 엉뚱한 키로 요청할 수 있어 비운다.
            _txtKey.Text = "";
            _txtKey2.Text = "";
            _txtHost.Text = "";
            _cmbCtx.Text = _builtinCtx.ToString();
            RefreshFileStatus();
            return;
        }

        var f = Fields(kind);
        string name = AppSettings.DisplayName(kind);

        // 라벨 문구
        if (AppSettings.IsOpenAiCompat(kind))
        {
            _lblKey.Text = kind == AppSettings.ProviderKind.LmStudio
                ? "LM Studio API 키 (선택, 보통 불필요):"
                : $"{name} API 키:";
            _lblHost.Text = "서버 주소:";
            _lblModel.Text = $"{name} 모델:";
        }
        else if (kind == AppSettings.ProviderKind.Papago)
        {
            _lblKey.Text = "Papago Client ID:";
            _lblKey2.Text = "Papago Client Secret:";
        }
        else if (kind == AppSettings.ProviderKind.MsTranslator)
        {
            _lblKey2.Text = "리전 (지역 리소스만, 예: koreacentral):";
        }
        else if (kind == AppSettings.ProviderKind.DeepL)
        {
            _lblKey.Text = "DeepL API 키 (Free):";
        }
        else
        {
            _lblKey.Text = $"{name} API 키:";
        }

        // 값 채우기
        _txtKey.Text = f.Key;
        if (kind == AppSettings.ProviderKind.Papago)
        {
            _txtKey2.Text = f.Second;
            _cmbSource.SelectedItem = AppSettings.Languages.Contains(f.Source)
                ? f.Source : AppSettings.Languages[0];
        }
        else if (kind == AppSettings.ProviderKind.MsTranslator)
        {
            _txtKey2.Text = f.Region;
        }
        else _txtKey2.Text = "";

        // Gemini는 OpenAI 호환은 아니지만 고정 모델 목록이 있다. 표시 이름으로 보여주고 ID로 저장한다.
        if (kind == AppSettings.ProviderKind.Gemini)
        {
            _cmbModel.Items.Clear();
            _cmbModel.Items.AddRange(AppSettings.GeminiModelOptions.Select(o => o.Name).ToArray());
            _cmbModel.Text = AppSettings.ModelName(AppSettings.GeminiModelOptions,
                string.IsNullOrWhiteSpace(f.Model) ? AppSettings.Models[0] : f.Model);
        }
        else if (AppSettings.IsOpenAiCompat(kind))
        {
            var svc = AppSettings.ServiceOf(kind);
            _txtHost.Text = string.IsNullOrWhiteSpace(f.Host) ? svc.BaseUrl : f.Host;

            // 고정 목록이 있는 서비스(DeepSeek)는 그 목록을, 없으면 서버 조회를 쓴다.
            if (svc.Models.Length > 0)
            {
                _cmbModel.Items.Clear();
                _cmbModel.Items.AddRange(svc.Models.Select(o => o.Name).ToArray());
                string id = string.IsNullOrWhiteSpace(f.Model) ? svc.Models[0].Id : f.Model;
                _cmbModel.Text = AppSettings.ModelName(svc.Models, id);
            }
            else
            {
                // LM Studio / Groq / OpenAI: 서버에서 목록을 가져온다(키가 있을 때만).
                _cmbModel.Items.Clear();
                _cmbModel.Text = f.Model;
            }
        }
    }

    private void LayoutForProvider()
    {
        var kind = Engine;
        bool builtin = kind == AppSettings.ProviderKind.Builtin;
        bool compat = AppSettings.IsOpenAiCompat(kind);
        bool showModel = AppSettings.HasModel(kind);
        bool papago = kind == AppSettings.ProviderKind.Papago;
        bool ms = kind == AppSettings.ProviderKind.MsTranslator;

        // 두 번째 입력창: Papago(Client Secret) / MS(리전)만 사용
        _lblKey2.Visible = _txtKey2.Visible = papago || ms;
        _lblSource.Visible = _cmbSource.Visible = papago;
        _lblKey.Visible = _txtKey.Visible = !builtin;
        _lblHost.Visible = _txtHost.Visible = compat && !builtin;
        _lblModel.Visible = showModel;
        _cmbModel.Visible = showModel;
        _lblFile.Visible = builtin;
        _btnBrowse.Visible = builtin;
        _btnDownload.Visible = builtin;
        _lblCtx.Visible = builtin;
        _cmbCtx.Visible = builtin;

        int y = 112;
        if (builtin)
        {
            y = 60;
            _lblFile.Location = new Point(12, y); y += 44;
            // 버튼 너비는 텍스트 실측 + 여유분. 한 행에 안 들어가면 세로로 쌓음.
            int bw = TextRenderer.MeasureText(_btnBrowse.Text, _btnBrowse.Font).Width + 30;
            int dw = TextRenderer.MeasureText(_btnDownload.Text, _btnDownload.Font).Width + 30;
            if (bw + dw + 12 <= 356)
            {
                _btnBrowse.Location = new Point(12, y);
                _btnBrowse.Size = new Size(bw, 30);
                _btnDownload.Location = new Point(368 - dw, y);
                _btnDownload.Size = new Size(dw, 30);
                y += 34;
            }
            else
            {
                _btnBrowse.Location = new Point(12, y);
                _btnBrowse.Size = new Size(356, 30);
                y += 34;
                _btnDownload.Location = new Point(12, y);
                _btnDownload.Size = new Size(356, 30);
                y += 34;
            }
            _lblCtx.Location = new Point(12, y); y += 20;
            _cmbCtx.Location = new Point(12, y); y += 28;
        }
        else
        {
            if (papago)
            {
                _lblKey.Location = new Point(12, y); y += 20;
                _txtKey.Location = new Point(12, y); y += 28;
                _lblKey2.Location = new Point(12, y); y += 20;
                _txtKey2.Location = new Point(12, y); y += 28;
                _lblSource.Location = new Point(12, y); y += 20;
                _cmbSource.Location = new Point(12, y); y += 28;
            }
            else if (ms)
            {
                _lblKey2.Location = new Point(12, y); y += 20;
                _txtKey2.Location = new Point(12, y); y += 28;
            }
            if (compat)
            {
                _lblHost.Location = new Point(12, y); y += 20;
                _txtHost.Location = new Point(12, y); y += 28;
            }
        }
        _lblHotkey.Location = new Point(12, y); y += 20;
        _chkCtrl.Location = new Point(14, y);
        _chkAlt.Location = new Point(74, y);
        _chkShift.Location = new Point(128, y);
        _chkWin.Location = new Point(192, y);
        _cmbKey.Location = new Point(258, y - 2); y += 26;
        if (showModel)
        {
            _lblModel.Location = new Point(12, y); y += 20;
            _cmbModel.Location = new Point(12, y); y += 28;
        }
        _btnTest.Location = new Point(12, y); y += 40;
        _lblTest.Location = new Point(12, y); y += 36;
        _lblHint.Location = new Point(12, y); y += 18;
        _lblUsage.Location = new Point(12, y); y += 18;
        _btnSave.Location = new Point(146, y);
        _btnCancel.Location = new Point(264, y);
        ClientSize = new Size(380, y + 40);
    }

    private void RefreshFileStatus()
    {
        string effective = string.IsNullOrWhiteSpace(_builtinPath)
            ? BuiltinClient.DefaultModelPath : _builtinPath;
        var (exists, size) = BuiltinClient.ModelStatus(effective);
        if (exists)
        {
            string name;
            try { name = Path.GetFileName(effective); }
            catch { name = effective; }
            if (string.IsNullOrEmpty(name)) name = "hy-mt2.gguf";
            _lblFile.ForeColor = Color.Green;
            _lblFile.Text = $"모델: {name} ({size / 1024 / 1024}MB)";
        }
        else
        {
            _lblFile.ForeColor = Color.Gray;
            _lblFile.Text = "모델 없음. [내장 모델 찾기] 또는 [내장 모델 다운로드]를 사용하세요.";
        }
        // 등록된 모델이 있으면 다운로드 불필요
        if (!_downloading) _btnDownload.Enabled = !exists;
    }

    private void OnBrowse(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "GGUF 모델 (*.gguf)|*.gguf",
            Title = "Hy-MT2 GGUF 모델 파일 선택",
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _builtinPath = dlg.FileName;
            RefreshFileStatus();
        }
    }

    private async void OnDownload(object? sender, EventArgs e)
    {
        if (_downloading) return;
        _downloading = true;
        _btnBrowse.Enabled = false;
        _btnDownload.Enabled = false;
        try
        {
            _lblFile.ForeColor = Color.Gray;
            _lblFile.Text = "다운로드 준비 중...";
            var (ok, msg) = await _builtinProbe.DownloadModelAsync(pct =>
            {
                try { BeginInvoke(() => _lblFile.Text = $"다운로드 중... {pct}%"); } catch { }
                return Task.CompletedTask;
            });
            _builtinPath = ok ? BuiltinClient.DefaultModelPath : _builtinPath;
            RefreshFileStatus();
            _lblTest.ForeColor = ok ? Color.Green : Color.Red;
            _lblTest.Text = msg;
        }
        finally
        {
            _downloading = false;
            _btnBrowse.Enabled = true;
            RefreshFileStatus();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _geminiProbe.Dispose(); _deeplProbe.Dispose(); _lmProbe.Dispose(); _builtinProbe.Dispose();
            _deepseekProbe.Dispose(); _groqProbe.Dispose(); _openaiProbe.Dispose();
            _googleProbe.Dispose(); _papagoProbe.Dispose(); _msProbe.Dispose();
        }
        base.Dispose(disposing);
    }

    private string PreviewDisplay() =>
        $"{(_chkCtrl.Checked ? "Ctrl+" : "")}{(_chkAlt.Checked ? "Alt+" : "")}" +
        $"{(_chkShift.Checked ? "Shift+" : "")}{(_chkWin.Checked ? "Win+" : "")}{_cmbKey.SelectedItem}";

    private async void OnTest(object? sender, EventArgs e)
    {
        string target = _lang.SelectedItem?.ToString() ?? "한국어";
        StashCurrentFields();
        _btnTest.Enabled = false;
        _lblTest.ForeColor = Color.Gray;
        _lblTest.Text = "테스트 중...";
        try
        {
            var kind = Engine;
            var f = Fields(kind);
            (bool ok, string msg) = kind switch
            {
                AppSettings.ProviderKind.Gemini => await _geminiProbe.TestAsync(f.Key, f.Model, target),
                AppSettings.ProviderKind.DeepL => await _deeplProbe.TestAsync(f.Key, target),
                AppSettings.ProviderKind.Builtin => await _builtinProbe.TestAsync(_builtinPath, _builtinCtx),
                AppSettings.ProviderKind.DeepSeek => await _deepseekProbe.TestAsync(f.Host, f.Model, f.Key, target),
                AppSettings.ProviderKind.Groq => await _groqProbe.TestAsync(f.Host, f.Model, f.Key, target),
                AppSettings.ProviderKind.OpenAi => await _openaiProbe.TestAsync(f.Host, f.Model, f.Key, target),
                AppSettings.ProviderKind.GoogleTranslate => await _googleProbe.TestAsync(f.Key, target),
                AppSettings.ProviderKind.Papago => await _papagoProbe.TestAsync(
                    f.Key, f.Second, f.Source, target),
                AppSettings.ProviderKind.MsTranslator => await _msProbe.TestAsync(f.Key, f.Region, target),
                _ => throw new InvalidOperationException($"새 엔진 추가 시 연결 테스트 분기 갱신 필요: {kind}"),
            };
            _lblTest.ForeColor = ok ? Color.Green : Color.Red;
            _lblTest.Text = msg;
        }
        finally { _btnTest.Enabled = true; }
    }

    private void OnSave(object? sender, EventArgs e)
    {
        if (!_chkCtrl.Checked && !_chkAlt.Checked && !_chkShift.Checked && !_chkWin.Checked)
        {
            MessageBox.Show("수식키(Ctrl/Alt/Shift/Win) 중 1개 이상 선택하세요. 일반 키만 쓰면 타이핑이 가로채집니다.",
                "TrayTranslator", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        // 충돌 검사: 바뀐 경우에만 임시 등록 테스트 (기존 등록과 겹치면 실패하므로)
        var probe = new AppSettings
        {
            HotkeyCtrl = _chkCtrl.Checked, HotkeyAlt = _chkAlt.Checked,
            HotkeyShift = _chkShift.Checked, HotkeyWin = _chkWin.Checked,
            HotkeyKey = _cmbKey.SelectedItem?.ToString() ?? "T",
        };
        if (probe.HotkeyMods != _origMods || probe.HotkeyKey != _origKey)
        {
            if (RegisterHotKey(Handle, TestId, probe.HotkeyMods, probe.HotkeyVk))
                UnregisterHotKey(Handle, TestId);
            else
            {
                MessageBox.Show($"핫키 등록 실패 ({probe.HotkeyDisplay}). 다른 프로그램이 사용 중입니다. 다른 조합을 선택하세요.",
                    "TrayTranslator", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }
        }

        StashCurrentFields();
        SaveFieldsToSettings(_settings);

        // 비어 있으면 기본값으로 채운다 (다음 실행 때 주소가 비어 보이지 않도록)
        var lmF = Fields(AppSettings.ProviderKind.LmStudio);
        if (string.IsNullOrWhiteSpace(lmF.Host)) lmF.Host = "http://localhost:1234";
        _settings.LmStudioHost = lmF.Host;
        var dsF = Fields(AppSettings.ProviderKind.DeepSeek);
        if (string.IsNullOrWhiteSpace(dsF.Host)) dsF.Host = "https://api.deepseek.com";
        if (string.IsNullOrWhiteSpace(dsF.Model)) dsF.Model = "deepseek-flash";
        _settings.DeepSeekHost = dsF.Host;
        _settings.DeepSeekModel = dsF.Model;
        var gqF = Fields(AppSettings.ProviderKind.Groq);
        if (string.IsNullOrWhiteSpace(gqF.Host)) gqF.Host = "https://api.groq.com/openai";
        _settings.GroqHost = gqF.Host;
        var oaF = Fields(AppSettings.ProviderKind.OpenAi);
        if (string.IsNullOrWhiteSpace(oaF.Host)) oaF.Host = "https://api.openai.com/v1";
        _settings.OpenAiHost = oaF.Host;

        var gemF = Fields(AppSettings.ProviderKind.Gemini);
        if (string.IsNullOrWhiteSpace(gemF.Model)) gemF.Model = AppSettings.Models[0];
        _settings.Model = gemF.Model;

        _settings.BuiltinModelPath = _builtinPath;
        _settings.BuiltinContextSize = _builtinCtx;
        _settings.Provider = Provider;
        _settings.TargetLang = _lang.SelectedItem?.ToString() ?? "한국어";
        _settings.HotkeyCtrl = _chkCtrl.Checked;
        _settings.HotkeyAlt = _chkAlt.Checked;
        _settings.HotkeyShift = _chkShift.Checked;
        _settings.HotkeyWin = _chkWin.Checked;
        _settings.HotkeyKey = _cmbKey.SelectedItem?.ToString() ?? "T";
        _settings.Save();
    }
}
