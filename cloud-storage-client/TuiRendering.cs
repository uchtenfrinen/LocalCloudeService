using System;
using System.Collections.Generic;

namespace CloudStorageClient;

internal record Entry(string Name, bool IsDir, string Path);

internal sealed partial class TuiApp
{
    private const int DefaultWidth = 100;
    private const int DefaultHeight = 30;
    private const int MinWidth = 40;
    private const int MinHeight = 10;

    private void Render()
    {
        int w = SafeWidth();
        int h = SafeHeight();
        int panelW = (w - 1) / 2;
        int listH = Math.Max(3, h - 3); // 1 title + 1 status + 1 footer

        AdjustScroll(_local, _localSel, listH, ref _localScroll);
        AdjustScroll(_remote, _remoteSel, listH, ref _remoteScroll);

        Console.Clear();
        Console.WriteLine($" CLOUD STORAGE   Local: {_cfg.LocalCwd}");
        Console.WriteLine(Trunc(_status, w));
        Console.WriteLine(new string('─', w));

        for (int row = 0; row < listH; row++)
        {
            string left = FormatItem(At(_local, _localScroll + row), _leftFocused && _localScroll + row == _localSel, panelW);
            string right = FormatItem(At(_remote, _remoteScroll + row), !_leftFocused && _remoteScroll + row == _remoteSel, panelW);
            Console.WriteLine(left + "│" + right);
        }

        Console.WriteLine(new string('─', w));
        Console.WriteLine("F2 Ref │ F5 Up │ F6 Dn │ F7 Mk │ F8 Del │ F3 View │ Tab Sw │ Q Quit");
    }

    private void SetStatus(string s) => _status = s;

    private static Entry? At(List<Entry> items, int i) => (i >= 0 && i < items.Count) ? items[i] : null;

    private static void AdjustScroll(List<Entry> items, int sel, int listH, ref int scroll)
    {
        if (items.Count == 0) { scroll = 0; return; }
        if (sel < scroll) scroll = sel;
        else if (sel >= scroll + listH) scroll = sel - listH + 1;
        if (scroll < 0) scroll = 0;
    }

    private static string FormatItem(Entry? e, bool selected, int width)
    {
        string text = e == null ? "" : (e.IsDir ? "[D] " : "    ") + e.Name;
        if (text.Length > width) text = text.Substring(0, Math.Max(1, width - 1)) + "…";
        text = text.PadRight(width);
        return selected ? "\u001b[7m" + text + "\u001b[0m" : text;
    }

    private static string Trunc(string s, int w)
    {
        if (s.Length > w) return s.Substring(0, Math.Max(1, w - 1)) + "…";
        return s.PadRight(w);
    }

    private static int SafeWidth()
    {
        try { return Console.BufferWidth > MinWidth ? Console.BufferWidth : DefaultWidth; }
        catch { return DefaultWidth; }
    }

    private static int SafeHeight()
    {
        try { return Console.BufferHeight > MinHeight ? Console.BufferHeight : DefaultHeight; }
        catch { return DefaultHeight; }
    }
}
