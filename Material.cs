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

    // 트레이 우클릭 메뉴용 머티리얼 렌더러 (흰 바탕·인디고 하이라이트·얇은 테두리)
    public sealed class MaterialMenuColors : ProfessionalColorTable
    {
        private static readonly Color Selected = Color.FromArgb(0xE8, 0xEA, 0xF6);
        private static readonly Color Pressed = Color.FromArgb(0xC5, 0xCA, 0xE9);
        private static readonly Color Line = Color.FromArgb(0xE0, 0xE0, 0xE0);

        public override Color MenuItemSelected => Selected;
        public override Color MenuItemSelectedGradientBegin => Selected;
        public override Color MenuItemSelectedGradientEnd => Selected;
        public override Color MenuItemPressedGradientBegin => Pressed;
        public override Color MenuItemPressedGradientMiddle => Pressed;
        public override Color MenuItemPressedGradientEnd => Pressed;
        public override Color MenuItemBorder => Pressed;
        public override Color MenuBorder => Line;
        public override Color ToolStripBorder => Line;
        public override Color ToolStripDropDownBackground => Color.White;
        public override Color ImageMarginGradientBegin => Color.White;
        public override Color ImageMarginGradientMiddle => Color.White;
        public override Color ImageMarginGradientEnd => Color.White;
        public override Color SeparatorDark => Line;
        public override Color SeparatorLight => Color.White;
    }

    public sealed class MaterialMenuRenderer : ToolStripProfessionalRenderer
    {
        public MaterialMenuRenderer() : base(new MaterialMenuColors()) { RoundedEdges = false; }

        public static void ThemeMenu(ToolStripDropDownMenu menu)
        {
            menu.Renderer = new MaterialMenuRenderer();
            menu.Font = new Font("Segoe UI", 9f);
            menu.ShowImageMargin = false;
            menu.BackColor = Color.White;
            foreach (ToolStripItem i in menu.Items)
            {
                i.ForeColor = Color.FromArgb(0x21, 0x21, 0x21);
                if (i is ToolStripMenuItem mi && mi.HasDropDownItems && mi.DropDown is ToolStripDropDownMenu sub)
                    ThemeMenu(sub);
            }
        }
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
