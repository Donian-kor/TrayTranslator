namespace TrayTranslator;

// 트레이·설정창 공용 아이콘 (pro08.png 임베드 리소스)
static class AppIcon
{
    private static Icon? _instance;

    public static Icon Instance => _instance ??= Load();

    private static Icon Load()
    {
        try
        {
            var asm = typeof(AppIcon).Assembly;
            using var s = asm.GetManifestResourceStream("TrayTranslator.assets.icons.tray.png");
            if (s != null)
            {
                using var bmp = new Bitmap(s);
                return Icon.FromHandle(bmp.GetHicon());
            }
        }
        catch { }
        return SystemIcons.Information;
    }
}
