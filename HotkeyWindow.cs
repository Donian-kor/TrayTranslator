using System.Runtime.InteropServices;

namespace TrayTranslator;

// WM_HOTKEY를 받기 위한 숨은 네이티브 윈도우. 핫키는 설정에서 변경 가능.
sealed class HotkeyWindow : NativeWindow, IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    public event Action? Pressed;

    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int ID = 1;
    private uint _mods;
    private uint _vk;

    public string Display { get; private set; }

    public HotkeyWindow(uint mods, uint vk, string display)
    {
        _mods = mods;
        _vk = vk;
        Display = display;
        CreateHandle(new CreateParams());
        if (!RegisterHotKey(Handle, ID, _mods, _vk))
            NotifyConflict();
    }

    public bool Update(uint mods, uint vk, string display)
    {
        UnregisterHotKey(Handle, ID);
        if (RegisterHotKey(Handle, ID, mods, vk))
        {
            _mods = mods;
            _vk = vk;
            Display = display;
            return true;
        }
        // 실패 시 기존 핫키 복구
        RegisterHotKey(Handle, ID, _mods, _vk);
        return false;
    }

    private void NotifyConflict() =>
        MessageBox.Show($"전역 핫키 등록 실패 ({Display}). 다른 프로그램과 충돌할 수 있습니다. 설정에서 변경하세요.",
            "TrayTranslator", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == ID)
            Pressed?.Invoke();
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        UnregisterHotKey(Handle, ID);
        DestroyHandle();
    }
}
