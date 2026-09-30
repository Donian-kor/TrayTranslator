namespace TrayTranslator;

// 커서 옆에 뜨는 경량 결과 팝업. 포커스를 가로채지 않으므로(ShowWithoutActivation)
// 핫키를 연속으로 눌러도 원본 앱의 선택 영역이 유지됨. 클릭/ESC/3초 후 닫힘.
// actions 지정 시 [번역]/[닫기] 등 확인 버튼 표시.
// 크기: MeasureText로 직접 계산 (Label/FlowLayout 자동측정 버그 회피). 내용 길이에 반응형.
sealed class PopupForm : Form
{
    private readonly System.Windows.Forms.Timer _timer;

    protected override bool ShowWithoutActivation => true;

    public PopupForm(string text, int timeoutMs = 3000, string? header = null, params (string Caption, Action OnClick)[] actions)
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        DoubleBuffered = true;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(25, 25, 25);
        ForeColor = Color.White;
        AutoSize = false;

        using var font = new Font("맑은 고딕", 14f);
        using var g = CreateGraphics();
        var measured = TextRenderer.MeasureText(g, text, font,
            new Size(600, 0),
            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
        int labelW = Math.Max(300, Math.Min(600, measured.Width));
        int labelH = Math.Max(64, Math.Min(500, measured.Height + 4));

        const int Pad = 20;
        int y = Pad;
        if (header != null)
        {
            var timeLabel = new Label
            {
                Text = header,
                AutoSize = false,
                Location = new Point(Pad, y),
                Size = new Size(labelW, 22),
                ForeColor = Color.FromArgb(150, 150, 150),
                BackColor = Color.FromArgb(25, 25, 25),
                Font = new Font("맑은 고딕", 9f),
            };
            timeLabel.Click += (_, _) => Close();
            Controls.Add(timeLabel);
            y += 24;
        }
        var label = new Label
        {
            Text = text,
            AutoSize = false,
            Location = new Point(Pad, y),
            Size = new Size(labelW, labelH),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(25, 25, 25),
            Font = new Font("맑은 고딕", 14f),
        };
        label.Click += (_, _) => Close();
        Controls.Add(label);

        int labelBottom = label.Location.Y + labelH;
        int bottom = labelBottom + Pad;
        if (actions.Length > 0)
        {
            // 버튼 줄 (우측 정렬, 너비는 텍스트 실측으로 확정 배치)
            using var btnFont = new Font("맑은 고딕", 12f);
            using var g2 = CreateGraphics();
            int totalW = 0;
            var widths = new int[actions.Length];
            for (int i = 0; i < actions.Length; i++)
            {
                widths[i] = TextRenderer.MeasureText(g2, actions[i].Caption, btnFont).Width + 34;
                totalW += widths[i] + 8;
            }
            int bx = Pad + labelW - totalW + 8;
            int btnH = 0;
            for (int i = 0; i < actions.Length; i++)
            {
                var (caption, onClick) = actions[i];
                var b = new Button
                {
                    Text = caption, AutoSize = false,
                    Size = new Size(widths[i], 34),
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("맑은 고딕", 12f),
                    Location = new Point(bx, labelBottom + 8),
                };
                b.Click += (_, _) =>
                {
                    try { onClick(); } catch { }
                };
                Controls.Add(b);
                bx += widths[i] + 8;
                btnH = 34;
            }
            bottom = labelBottom + 8 + btnH + Pad;
        }

        ClientSize = new Size(Pad + labelW + Pad, bottom);
        Click += (_, _) => Close();

        _timer = new System.Windows.Forms.Timer();
        _timer.Tick += (_, _) => Close();
        if (timeoutMs != Timeout.Infinite)
        {
            _timer.Interval = timeoutMs;
            _timer.Start();
        }

        // 커서 오른쪽 아래에 임시 배치. 최종 위치는 OnShown에서 실제 크기로 보정.
        var p = Cursor.Position;
        Location = new Point(p.X + 12, p.Y + 16);
    }

    // 창 가장자리에 균일한 흰색 테두리 직접 그리기
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var r = ClientRectangle;
        using var pen = new Pen(Color.White, 4);
        e.Graphics.DrawRectangle(pen, r.X + 2, r.Y + 2, r.Width - 5, r.Height - 5);
    }

    // 실제 렌더링 크기로 화면 경계 안에 들어오게 보정
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ClampToScreen();
        Invalidate(); // 레이아웃 확정 후 테두리 전체 다시 그리기
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        Logger.Log($"팝업: 위치={Location} 크기={Size} 화면={area} 커서={Cursor.Position}");
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (IsHandleCreated && Visible) ClampToScreen();
    }

    private void ClampToScreen()
    {
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        int x = Math.Min(Left, area.Right - Width - 8);
        int y = Math.Min(Top, area.Bottom - Height - 8);
        var next = new Point(Math.Max(area.Left + 8, x), Math.Max(area.Top + 8, y));
        if (next != Location) Location = next;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape) { Close(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}
