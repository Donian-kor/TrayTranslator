using System.Runtime.InteropServices;

namespace TrayTranslator;

sealed class SettingsForm : Form
{
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    private const int TestId = 9999;

    private readonly Label _lblKey;
    private readonly TextBox _txtKey;
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
    private string _geminiKey;
    private string _deeplKey;
    private string _lmKey;
    private string _lmHost;
    private string _lmModel;
    private string _geminiModel;
    private string _builtinPath;
    private int _builtinCtx;
    private bool _downloading;
    private readonly GeminiClient _geminiProbe = new();
    private readonly DeepLClient _deeplProbe = new();
    private readonly LmStudioClient _lmProbe = new();
    private readonly BuiltinClient _builtinProbe = new();

    public SettingsForm(AppSettings settings)
    {
        _settings = settings;
        _origMods = settings.HotkeyMods;
        _origKey = settings.HotkeyKey;
        _geminiKey = settings.ApiKey;
        _deeplKey = settings.DeepLApiKey;
        _lmKey = settings.LmStudioKey;
        _lmHost = settings.LmStudioHost;
        _lmModel = settings.LmStudioModel;
        _geminiModel = settings.Model;
        _builtinPath = settings.BuiltinModelPath;
        _builtinCtx = settings.BuiltinContextSize >= 1024 ? settings.BuiltinContextSize : 4096;

        Text = "TrayTranslator 설정";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;

        var lblProvider = new Label { Text = "번역 엔진:", Location = new Point(12, 12), AutoSize = true };
        _provider = new ComboBox
        {
            Location = new Point(12, 32), Width = 174,
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        _provider.Items.AddRange(AppSettings.Providers);
        _provider.SelectedItem = AppSettings.Providers.Contains(settings.Provider)
            ? settings.Provider : AppSettings.Providers[0];
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

        Controls.AddRange([lblProvider, _provider, _lblKey, _txtKey, _lblHost, _txtHost,
            _lblFile, _btnBrowse, _btnDownload, _lblCtx, _cmbCtx,
            _lblLang, _lang, _lblHotkey, _chkCtrl, _chkAlt, _chkShift, _chkWin, _cmbKey,
            _lblModel, _cmbModel, _btnTest, _lblTest, _lblHint, _lblUsage, _btnSave, _btnCancel]);
        AcceptButton = _btnSave;
        CancelButton = _btnCancel;

        LoadProviderFields();
        LayoutForProvider();
        RefreshFileStatus();
        if (IsLM) _ = RefreshLmModelsAsync();
    }

    private string Provider => _provider.SelectedItem?.ToString() ?? "Gemini";
    private bool IsDeepL => Provider == "DeepL";
    private bool IsLM => Provider == "LM Studio";
    private bool IsBuiltin => Provider == "로컬 (Hy-MT2)";

    // 엔진 전환: 입력 중이던 값을 메모리에 보관, 선택된 엔진의 값을 표시
    private void SwitchProvider()
    {
        StashCurrentFields();
        LoadProviderFields();
        LayoutForProvider();
        if (IsLM) _ = RefreshLmModelsAsync();
    }

    private bool _refreshingModels;

    // LM Studio: 서버의 로드된 모델 목록을 가져와 모델 드롭다운에 채움 (브랜드별 목록)
    private async Task RefreshLmModelsAsync()
    {
        if (_refreshingModels) return;
        _refreshingModels = true;
        try
        {
            string keep = _cmbModel.Text.Trim();
            var ids = await _lmProbe.ListModelsAsync(
                string.IsNullOrWhiteSpace(_txtHost.Text) ? "http://localhost:1234" : _txtHost.Text.Trim());
            if (ids.Count > 0)
            {
                _cmbModel.Items.Clear();
                _cmbModel.Items.AddRange([.. ids]);
                _cmbModel.Text = ids.Contains(keep) ? keep : ids[0];
            }
        }
        finally { _refreshingModels = false; }
    }

    private void StashCurrentFields()
    {
        if (IsDeepL) _deeplKey = _txtKey.Text.Trim();
        else if (IsLM) { _lmKey = _txtKey.Text.Trim(); _lmHost = _txtHost.Text.Trim(); _lmModel = _cmbModel.Text.Trim(); }
        else if (IsBuiltin) { _builtinCtx = ParseCtx(_cmbCtx.Text); }
        else { _geminiKey = _txtKey.Text.Trim(); _geminiModel = _cmbModel.Text.Trim(); }
    }

    private static int ParseCtx(string s)
    {
        if (int.TryParse(s.Trim(), out int v)) return Math.Clamp(v, 1024, 65536);
        return 4096;
    }

    private void LoadProviderFields()
    {
        if (IsBuiltin)
        {
            _cmbCtx.Text = _builtinCtx.ToString();
            RefreshFileStatus();
            return;
        }
        if (IsDeepL) { _lblKey.Text = "DeepL API 키 (Free):"; _txtKey.Text = _deeplKey; }
        else if (IsLM)
        {
            _lblKey.Text = "LM Studio API 키 (선택, 보통 불필요):";
            _txtKey.Text = _lmKey;
            _txtHost.Text = _lmHost;
            _lblModel.Text = "LM Studio 모델 ID (로드된 모델과 일치):";
            _cmbModel.Text = _lmModel;
        }
        else
        {
            _lblKey.Text = "Gemini API 키:";
            _txtKey.Text = _geminiKey;
            _lblModel.Text = "Gemini 모델 (404 시 자동 폴백):";
            _cmbModel.Items.Clear();
            _cmbModel.Items.AddRange(AppSettings.Models);
            _cmbModel.Text = string.IsNullOrWhiteSpace(_geminiModel) ? AppSettings.Models[0] : _geminiModel;
        }
    }

    private void LayoutForProvider()
    {
        bool lm = IsLM;
        bool builtin = IsBuiltin;
        bool showModel = !IsDeepL && !builtin;
        _lblKey.Visible = _txtKey.Visible = !builtin;
        _lblHost.Visible = _txtHost.Visible = lm && !builtin;
        _lblModel.Visible = showModel;
        _cmbModel.Visible = showModel;
        _lblFile.Visible = builtin;
        _btnBrowse.Visible = builtin;
        _btnDownload.Visible = builtin;
        _lblCtx.Visible = builtin;
        _cmbCtx.Visible = builtin;

        int y = 108;
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
        else if (lm)
        {
            _lblHost.Location = new Point(12, y); y += 20;
            _txtHost.Location = new Point(12, y); y += 28;
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
        _btnTest.Location = new Point(12, y); y += 34;
        _lblTest.Location = new Point(12, y); y += 36;
        _lblHint.Location = new Point(12, y); y += 18;
        _lblUsage.Location = new Point(12, y); y += 18;
        _btnSave.Location = new Point(212, y);
        _btnCancel.Location = new Point(293, y);
        ClientSize = new Size(380, y + 36);
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
        if (disposing) { _geminiProbe.Dispose(); _deeplProbe.Dispose(); _lmProbe.Dispose(); _builtinProbe.Dispose(); }
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
            (bool ok, string msg) = Provider switch
            {
                "DeepL" => await _deeplProbe.TestAsync(_deeplKey, target),
                "LM Studio" => await _lmProbe.TestAsync(_lmHost, _lmModel),
                "로컬 (Hy-MT2)" => await _builtinProbe.TestAsync(_builtinPath, _builtinCtx),
                _ => await _geminiProbe.TestAsync(_geminiKey, _cmbModel.Text.Trim(), target),
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
        _settings.ApiKey = _geminiKey;
        _settings.DeepLApiKey = _deeplKey;
        _settings.LmStudioKey = _lmKey;
        _settings.LmStudioHost = string.IsNullOrWhiteSpace(_lmHost) ? "http://localhost:1234" : _lmHost;
        _settings.LmStudioModel = _lmModel;
        _settings.BuiltinModelPath = _builtinPath;
        _settings.BuiltinContextSize = _builtinCtx;
        _settings.Provider = Provider;
        _settings.TargetLang = _lang.SelectedItem?.ToString() ?? "한국어";
        _settings.HotkeyCtrl = _chkCtrl.Checked;
        _settings.HotkeyAlt = _chkAlt.Checked;
        _settings.HotkeyShift = _chkShift.Checked;
        _settings.HotkeyWin = _chkWin.Checked;
        _settings.HotkeyKey = _cmbKey.SelectedItem?.ToString() ?? "T";
        if (Provider == "Gemini")
        {
            _settings.Model = _cmbModel.Text.Trim();
            if (string.IsNullOrEmpty(_settings.Model)) _settings.Model = AppSettings.Models[0];
        }
        _settings.Save();
    }
}
