using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CloudStorage.Shared;

namespace CloudStorageClient;

internal sealed partial class TuiApp
{
    private readonly AppConfig _cfg;
    private readonly StorageClient _client;

    private List<Entry> _local = new();
    private List<Entry> _remote = new();
    private int _localSel, _remoteSel, _localScroll, _remoteScroll;
    private bool _leftFocused = true;
    private string _status = "Press F2 refresh, Tab switch panel, Q quit";

    private static readonly HashSet<string> ImageExt =
        new() { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".avif" };

    public TuiApp(AppConfig cfg, StorageClient client)
    {
        _cfg = cfg;
        _client = client;
    }

    public void Run() => RunAsync().GetAwaiter().GetResult();

    private async Task RunAsync(CancellationToken ct = default)
    {
        bool running = true;
        Console.TreatControlCAsInput = true;

        RefreshLocal();
        // validate initial remote folder, so the user cannot start in a folder
        // that no longer exists on the server
        if (_cfg.RemoteCwd.Length != 0 && !await _client.DirExistsAsync(_cfg.RemoteCwd, ct))
            _cfg.RemoteCwd = "";
        await RefreshRemote(ct);

        while (running)
        {
            Render();
            var key = Console.ReadKey(true);
            switch (key.Key)
            {
                case ConsoleKey.UpArrow:    MoveSel(-1); break;
                case ConsoleKey.DownArrow:  MoveSel(1); break;
                case ConsoleKey.LeftArrow:
                case ConsoleKey.RightArrow:
                case ConsoleKey.Tab:        _leftFocused = !_leftFocused; break;
                case ConsoleKey.Enter:      await Activate(); break;
                case ConsoleKey.F2:
                case ConsoleKey.R:          RefreshLocal(); await RefreshRemote(ct); SetStatus("refreshed"); break;
                case ConsoleKey.F5:
                case ConsoleKey.U:          await Upload(); break;
                case ConsoleKey.F6:
                case ConsoleKey.O:          await Download(); break;
                case ConsoleKey.F7:
                case ConsoleKey.N:          await Mkdir(); break;
                case ConsoleKey.F8:
                case ConsoleKey.X:          await Delete(); break;
                case ConsoleKey.F3:
                case ConsoleKey.V:          View(); break;
                case ConsoleKey.Q:
                case ConsoleKey.Escape:     running = false; break;
            }
        }
        Console.Clear();
        _cfg.Save();
    }

    private void MoveSel(int step)
    {
        if (_leftFocused)
            _localSel = Clamp(_localSel + step, _local.Count);
        else
            _remoteSel = Clamp(_remoteSel + step, _remote.Count);
    }

    private static int Clamp(int size, int count)
    {
        if (count == 0) return 0;
        if (size < 0) return 0;
        if (size > count - 1) return count - 1;
        return size;
    }

    private static string Parent(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";
        int i = path.LastIndexOf('/');
        return i < 0 ? "" : path[..i];
    }

    private static bool IsImage(string name) =>
        ImageExt.Contains(Path.GetExtension(name).ToLowerInvariant());

    private static string? Prompt(string label)
    {
        Console.WriteLine();
        Console.Write(label + ": ");
        return Console.ReadLine();
    }
}
