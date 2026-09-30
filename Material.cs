namespace TrayTranslator;

// Google Material 느낌의 WinForms 테마.
// UseVisualStyleBackColor=false 가 핵심 (켜져 있으면 BackColor가 무시된다).
static class Material
{
    public static readonly Color Primary = Color.FromArgb(0x3F, 0x51, 0xB5);
    public static readonly Color TonalBg = Color.FromArgb(0xE8, 0xEA, 0xF6);
    public static readonly Color FieldBg = Color.FromArgb(0xF5, 0xF5, 0xF5);
    public static readonly Color SectionFg = Color.FromArgb(0x61, 0x61, 0x61);

    public static readonly Font SectionFont = new("Segoe UI", 8.5f);
    public static readonly Font FieldFont = new("Segoe UI", 9.5f);
    public static readonly Font ButtonFont = new("Segoe UI", 9f, FontStyle.Bold);

    public static void SectionLabel(Label l)
    {
        l.Font = SectionFont;
        l.ForeColor = SectionFg;
    }

    public static void Field(TextBox t)
    {
        t.BorderStyle = BorderStyle.FixedSingle;
        t.BackColor = FieldBg;
        t.Font = FieldFont;
    }

    public static void FieldCombo(ComboBox c)
    {
        c.FlatStyle = FlatStyle.Flat;
        c.BackColor = FieldBg;
        c.Font = FieldFont;
    }

    private static void BaseButton(Button b)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 0;
        b.UseVisualStyleBackColor = false;
        b.Font = ButtonFont;
    }

    // 저장 같은 주 액션
    public static void Contained(Button b)
    {
        BaseButton(b);
        b.BackColor = Primary;
        b.ForeColor = Color.White;
    }

    // 테스트·다운로드 같은 보조 액션
    public static void Tonal(Button b)
    {
        BaseButton(b);
        b.BackColor = TonalBg;
        b.ForeColor = Primary;
    }

    // 취소 같은 텍스트 액션
    public static void TextButton(Button b)
    {
        BaseButton(b);
        b.BackColor = Color.White;
        b.ForeColor = SectionFg;
    }

    // 테두리만 있는 보조 액션 (파일 찾기)
    public static void Outlined(Button b)
    {
        BaseButton(b);
        b.BackColor = Color.White;
        b.ForeColor = Primary;
        b.FlatAppearance.BorderSize = 1;
        b.FlatAppearance.BorderColor = Color.FromArgb(0x9E, 0x9E, 0x9E);
    }
}
