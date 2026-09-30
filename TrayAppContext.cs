namespace TrayTranslator;

using System.Text;

sealed class TrayAppContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly HotkeyWindow _hotkey;
    private readonly AppSettings _settings;
    private readonly GeminiClient _gemini = new();
    private readonly DeepLClient _deepl = new();
    private readonly LmStudioClient _lmstudio = new();
    private readonly BuiltinClient _builtin = new();
    private PopupForm? _popup;
    private bool _busy;
    private bool _disposed;

    public TrayAppContext()
    {
        _settings = AppSettings.Load();
        Application.ThreadExit += (_, _) => Dispose();

        _tray = new NotifyIcon
        {
            Text = "TrayTranslator",
            Visible = true,
            Icon = AppIcon.Instance,
            ContextMenuStrip = BuildMenu(),
        };
        UpdateTrayText();
        _tray.DoubleClick += (_, _) => OpenSettings();

        _hotkey = new HotkeyWindow(_settings.HotkeyMods, _settings.HotkeyVk, _settings.HotkeyDisplay);
        _hotkey.Pressed += OnHotkey;

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            _tray.ShowBalloonTip(5000, "TrayTranslator",
                "API 키를 먼저 설정하세요. 트레이 아이콘 우클릭 → 설정", ToolTipIcon.Warning);
        }
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        var langMenu = new ToolStripMenuItem("번역 대상 언어");
        foreach (var lang in AppSettings.Languages)
        {
            var item = new ToolStripMenuItem(lang)
            {
                Checked = lang == _settings.TargetLang,
            };
            item.Click += (_, _) =>
            {
                _settings.TargetLang = lang;
                _settings.Save();
                foreach (ToolStripMenuItem i in langMenu.DropDownItems)
                    i.Checked = i.Text == lang;
                UpdateTrayText();
            };
            langMenu.DropDownItems.Add(item);
        }
        var settings = new ToolStripMenuItem("설정...", null, (_, _) => OpenSettings());
        var fileTr = new ToolStripMenuItem("파일 번역...", null, (_, _) => TranslateFile());
        var exit = new ToolStripMenuItem("종료", null, (_, _) => ExitThread());
        menu.Items.AddRange([langMenu, new ToolStripSeparator(), settings, fileTr, exit]);
        return menu;
    }

    private void OpenSettings()
    {
        using var f = new SettingsForm(_settings);
        if (f.ShowDialog() != DialogResult.OK) return;
        UpdateTrayText();
        if (!_hotkey.Update(_settings.HotkeyMods, _settings.HotkeyVk, _settings.HotkeyDisplay))
            MessageBox.Show($"핫키 등록 실패 ({_settings.HotkeyDisplay}). 다른 프로그램이 사용 중입니다.",
                "TrayTranslator", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void UpdateTrayText() =>
        _tray.Text = $"TrayTranslator [{_settings.Provider}] ({_settings.HotkeyDisplay})";

    // txt 파일 전체 번역: 문장 단위로 조각내 순차 번역 후 {원본}.{언어코드}.txt 저장
    private async void TranslateFile()
    {
        if (_busy) return;
        using var dlg = new OpenFileDialog
        {
            Filter = "텍스트 파일 (*.txt)|*.txt",
            Title = "번역할 텍스트 파일 선택",
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        string src;
        try
        {
            using var sr = new StreamReader(dlg.FileName, detectEncodingFromByteOrderMarks: true);
            src = await sr.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            ShowPopup("[파일 오류] " + ex.Message.Split('\n')[0]);
            return;
        }
        if (string.IsNullOrWhiteSpace(src))
        {
            ShowPopup("[파일 오류] 빈 파일입니다.");
            return;
        }

        int chunkSize = _settings.Provider switch
        {
            "DeepL" => 30000,
            "Gemini" => 8000,
            "LM Studio" => 2000,
            _ => Math.Clamp(_settings.BuiltinContextSize / 3, 500, 8000), // 로컬: ctx 비례
        };
        var chunks = ChunkText(src, chunkSize);
        string outPath = Path.Combine(
            Path.GetDirectoryName(dlg.FileName)!,
            $"{Path.GetFileNameWithoutExtension(dlg.FileName)}.{AppSettings.TargetCode(_settings.TargetLang)}.txt");

        _busy = true;
        try
        {
            var results = new List<string>();
            for (int i = 0; i < chunks.Count; i++)
            {
                ShowPopup($"파일 번역 중... {i + 1}/{chunks.Count}", Timeout.Infinite);
                (bool ok, string result) = _settings.Provider switch
                {
                    "DeepL" => await _deepl.TranslateAsync(chunks[i], _settings.TargetLang, _settings.DeepLApiKey),
                    "LM Studio" => await _lmstudio.TranslateAsync(chunks[i], _settings.TargetLang,
                        _settings.LmStudioHost, _settings.LmStudioModel, _settings.LmStudioKey),
                    "로컬 (Hy-MT2)" => await _builtin.TranslateAsync(chunks[i], _settings.TargetLang,
                        _settings.BuiltinModelPath, _settings.BuiltinContextSize, null),
                    _ => await _gemini.TranslateAsync(
                        chunks[i], _settings.TargetLang, _settings.ApiKey, _settings.Model,
                        msg => { ShowPopup($"파일 번역 중... {i + 1}/{chunks.Count}\n{msg}", Timeout.Infinite); return Task.CompletedTask; }),
                };
                if (!ok)
                {
                    ShowPopup($"파일 번역 중단 ({i + 1}/{chunks.Count}):\n{result}", 10000);
                    Logger.Log($"파일 번역 중단: {i + 1}/{chunks.Count} {Logger.Preview(result)}");
                    return;
                }
                results.Add(result);
            }
            await File.WriteAllTextAsync(outPath, string.Join("\n\n", results), new UTF8Encoding(true));
            Logger.Log($"파일 번역 완료: {chunks.Count}조각, {src.Length}자 → {outPath}");
            ShowPopup($"번역 완료: {chunks.Count}조각, {src.Length:N0}자\n저장됨:\n{outPath}", 10000);
        }
        catch (Exception ex)
        {
            ShowPopup($"[실행 오류] {ex.Message.Split('\n')[0]}");
        }
        finally { _busy = false; }
    }

    // 문장 경계에서 max 이하로 나누기 (경계 없는 장문은 강제 분할)
    private static List<string> ChunkText(string src, int max)
    {
        var sentences = System.Text.RegularExpressions.Regex
            .Split(src, @"(?<=[.!?。！？\n])")
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();
        var chunks = new List<string>();
        var cur = new StringBuilder();
        void Flush()
        {
            if (cur.Length > 0) { chunks.Add(cur.ToString()); cur.Clear(); }
        }
        foreach (var s in sentences)
        {
            if (s.Length > max)
            {
                Flush();
                for (int i = 0; i < s.Length; i += max)
                    chunks.Add(s.Substring(i, Math.Min(max, s.Length - i)));
                continue;
            }
            if (cur.Length + s.Length + 1 > max) Flush();
            if (cur.Length > 0) cur.Append(' ');
            cur.Append(s);
        }
        Flush();
        return chunks;
    }

    private async void OnHotkey()
    {
        if (_busy) return;
        string activeKey = _settings.Provider switch
        {
            "DeepL" => _settings.DeepLApiKey,
            "LM Studio" => _settings.LmStudioKey,
            _ => _settings.ApiKey,
        };
        bool needKey = _settings.Provider is "Gemini" or "DeepL";
        if (needKey && string.IsNullOrWhiteSpace(activeKey))
        {
            ShowPopup($"{_settings.Provider} API 키가 없습니다. 트레이 우클릭 → 설정에서 입력하세요.");
            return;
        }
        _busy = true;
        try
        {
            // 이전 팝업이 포커스를 가로챘으면 복사가 안 되므로 먼저 닫고 포커스 복귀 대기
            ClosePopup();
            await Task.Delay(150);

            // 1순위: UIA로 선택 텍스트 직접 읽기 (클립보드·포커스 무관)
            string? text = TextGrabber.GetSelectedText();
            bool viaClipboard = false;
            if (string.IsNullOrWhiteSpace(text))
            {
                // 2순위: 구방식 Ctrl+C 폴백 (UIA 미지원 앱용)
                var (clip, changed) = GrabSelectedText();
                viaClipboard = true;
                if (!changed && !string.IsNullOrWhiteSpace(clip) &&
                    clip != _settings.ApiKey && !GeminiClient.LooksLikeApiKey(clip))
                {
                    // 복사가 안 돼서 예전 클립보드 내용이 그대로인 경우: 멋대로 번역하지 않고 확인받음
                    PopupForm? confirm = null;
                    confirm = new PopupForm(
                        $"선택 영역을 읽지 못했습니다.\n클립보드 내용: \"{Logger.Preview(clip, 80)}\"\n\n이 내용을 번역하려면 [번역]을 누르세요.",
                        10000, null,
                        ("번역", () => { _ = TranslateAndShowAsync(clip); }),
                        ("닫기", () => confirm?.Close()));
                    ClosePopup();
                    _popup = confirm;
                    confirm.Show();
                    return;
                }
                text = clip;
            }
            Logger.Log($"핫키: 방식={(viaClipboard ? "클립보드" : "UIA")} 내용={Logger.Preview(Logger.SanitizeAll(text ?? "", _settings.ApiKey, _settings.DeepLApiKey, _settings.LmStudioKey))}");
            if (string.IsNullOrWhiteSpace(text))
            {
                ShowPopup($"텍스트를 드래그한 뒤 {_settings.HotkeyDisplay}를 누르세요.");
                return;
            }
            // 클립보드에 복사해둔 API 키를 번역 대상으로 잡는 실수 방지
            if (text == _settings.ApiKey || text == _settings.DeepLApiKey || GeminiClient.LooksLikeApiKey(text))
            {
                ShowPopup("클립보드에 API 키가 들어있습니다. 번역할 텍스트를 드래그한 뒤 핫키를 누르세요.");
                return;
            }
            _busy = false; // 이후부터는 TranslateAndShowAsync가 _busy 관리
            await TranslateAndShowAsync(text);
        }
        catch (Exception ex)
        {
            Logger.Log("실행 오류: " + ex.Message);
            ShowPopup($"[실행 오류] {ex.Message}");
        }
        finally { _busy = false; }
    }

    private async Task TranslateAndShowAsync(string text)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            if (text.Length > 2000) text = text[..2000];
            ShowPopup("번역 중...", Timeout.Infinite);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Logger.Log($"번역 요청: 엔진={_settings.Provider} 대상={_settings.TargetLang} 원문={Logger.Preview(Logger.SanitizeAll(text, _settings.ApiKey, _settings.DeepLApiKey, _settings.LmStudioKey))}");
            (bool ok, string result) = _settings.Provider switch
            {
                "DeepL" => await _deepl.TranslateAsync(text, _settings.TargetLang, _settings.DeepLApiKey),
                "LM Studio" => await _lmstudio.TranslateAsync(text, _settings.TargetLang,
                    _settings.LmStudioHost, _settings.LmStudioModel, _settings.LmStudioKey),
                "로컬 (Hy-MT2)" => await _builtin.TranslateAsync(text, _settings.TargetLang,
                    _settings.BuiltinModelPath, _settings.BuiltinContextSize,
                    msg => { ShowPopup(msg, Timeout.Infinite); return Task.CompletedTask; }),
                _ => await _gemini.TranslateAsync(
                    text, _settings.TargetLang, _settings.ApiKey, _settings.Model,
                    msg => { ShowPopup(msg, Timeout.Infinite); return Task.CompletedTask; }),
            };
            Logger.Log($"번역 완료: {sw.ElapsedMilliseconds}ms 성공={ok} 결과={Logger.Preview(Logger.SanitizeAll(result, _settings.ApiKey, _settings.DeepLApiKey, _settings.LmStudioKey))}");
            if (ok)
            {
                try { Clipboard.SetText(result); } catch { }
            }
            ShowPopup(result, 3000, FormatElapsed(sw.ElapsedMilliseconds));
        }
        finally { _busy = false; }
    }

    // 현재 선택 영역을 Ctrl+C로 클립보드에 복사해 읽어옴.
    // Changed=false면 복사 전후 클립보드가 동일 = 복사가 안 된 것.
    private static (string? Text, bool Changed) GrabSelectedText()
    {
        string? before = null;
        try { if (Clipboard.ContainsText()) before = Clipboard.GetText(); } catch { }

        try
        {
            SendKeys.SendWait("^c");
        }
        catch { return (null, false); }

        for (int i = 0; i < 30; i++)
        {
            Thread.Sleep(30);
            try
            {
                if (Clipboard.ContainsText())
                {
                    var after = Clipboard.GetText();
                    if (after != before) return (after.Trim(), true);
                }
            }
            catch { Thread.Sleep(50); }
        }
        return (before?.Trim(), false);
    }

    private void ShowPopup(string text, int timeoutMs = 3000, string? header = null)
    {
        ClosePopup();
        _popup = new PopupForm(text, timeoutMs, header);
        _popup.Show();
    }

    private static string FormatElapsed(long ms) =>
        ms < 10000 ? $"{ms / 1000.0:0.##}초" : $"{ms / 1000.0:0.#}초";

    private void ClosePopup()
    {
        _popup?.Close();
        _popup?.Dispose();
        _popup = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;
        if (disposing)
        {
            try
            {
                try { _hotkey.Dispose(); } catch { }
                _tray.Visible = false;
                _tray.Dispose();
                _gemini.Dispose();
                _deepl.Dispose();
                _lmstudio.Dispose();
                _builtin.Dispose();
                _popup?.Dispose();
            }
            catch { }
        }
        base.Dispose(disposing);
    }
}
