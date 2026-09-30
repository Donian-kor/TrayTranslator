using System.Text;
using FlaUI.UIA3;

namespace TrayTranslator;

// 접근성(UIA) 트리에서 선택 텍스트를 직접 읽음.
// 클립보드를 건드리지 않고, 포커스를 옮기지도 않음.
// 크롬·엣지·일렉트론(OpenCode 등)·메모장·오피스 등 UIA를 노출하는 모든 앱에서 동작.
public static class TextGrabber
{
    public static string? GetSelectedText()
    {
        try
        {
            using var automation = new UIA3Automation();
            var roots = new List<FlaUI.Core.AutomationElements.AutomationElement>();
            try { roots.Add(automation.FocusedElement()); } catch { }
            try
            {
                var p = Cursor.Position;
                roots.Add(automation.FromPoint(new System.Drawing.Point(p.X, p.Y)));
            }
            catch { }

            foreach (var root in roots)
            {
                var t = FromSubtree(root);
                if (!string.IsNullOrWhiteSpace(t)) return t.Trim();
            }
        }
        catch { }
        return null;
    }

    // 요소 자체→부모 방향으로 올라가며 선택 영역을 찾음 (최대 8단계)
    private static string? FromSubtree(FlaUI.Core.AutomationElements.AutomationElement root)
    {
        var el = root;
        for (int depth = 0; depth < 8 && el != null; depth++)
        {
            var text = TryGetSelection(el);
            if (!string.IsNullOrWhiteSpace(text)) return text;
            try { el = el.Parent; } catch { break; }
        }
        return null;
    }

    private static string? TryGetSelection(FlaUI.Core.AutomationElements.AutomationElement el)
    {
        try
        {
            var tp = el.Patterns.Text;
            if (!tp.IsSupported) return null;
            var sb = new StringBuilder();
            foreach (var range in tp.Pattern.GetSelection())
            {
                try { sb.Append(range.GetText(-1)); } catch { }
            }
            var s = sb.ToString().Trim();
            return string.IsNullOrEmpty(s) ? null : s;
        }
        catch { return null; }
    }
}
