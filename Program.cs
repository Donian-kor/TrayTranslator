namespace TrayTranslator;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, "TrayTranslator_SingleInstance", out bool created);
        if (!created)
        {
            MessageBox.Show("이미 실행 중입니다.", "TrayTranslator",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayAppContext());
    }
}
