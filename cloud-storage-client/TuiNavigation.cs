using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CloudStorageClient;

internal sealed partial class TuiApp
{
    private async Task Activate(CancellationToken ct = default)
    {
        if (_leftFocused) ActivateLocal(); else await ActivateRemote(ct);
    }

    private void ActivateLocal()
    {
        var e = At(_local, _localSel);
        if (e == null) return;
        if (e.Name == "..") { _cfg.LocalCwd = e.Path; _localSel = 0; RefreshLocal(); return; }
        if (e.IsDir) { _cfg.LocalCwd = e.Path; _localSel = 0; RefreshLocal(); return; }
        if (IsVideo(e.Name)) ViewLocal(e, "video-viewer");
        else ViewLocal(e, "image-viewer");
    }

    private async Task ActivateRemote(CancellationToken ct = default)
    {
        var e = At(_remote, _remoteSel);
        if (e == null) return;
        if (e.Name == "..") { _cfg.RemoteCwd = Parent(_cfg.RemoteCwd); _remoteSel = 0; await RefreshRemote(ct); return; }
        if (e.IsDir)
        {
            if (await _client.DirExistsAsync(e.Path, ct))
            {
                _cfg.RemoteCwd = e.Path; _remoteSel = 0; await RefreshRemote(ct); SetStatus("entered /" + e.Path);
            }
            else SetStatus("folder does not exist: /" + e.Path);
        }
        else if (IsVideo(e.Name)) ViewRemote(e, "video-viewer");
        else ViewRemote(e, "image-viewer");
    }

    private void RefreshLocal()
    {
        _local = new List<Entry>();
        string cur = _cfg.LocalCwd;
        string? parent = Path.GetDirectoryName(cur);
        if (!string.IsNullOrEmpty(parent) && parent != cur)
            _local.Add(new Entry("..", true, parent));
        try
        {
            foreach (var d in Directory.EnumerateDirectories(cur).OrderBy(Path.GetFileName))
                _local.Add(new Entry(Path.GetFileName(d), true, d));
            foreach (var f in Directory.EnumerateFiles(cur).OrderBy(Path.GetFileName))
                _local.Add(new Entry(Path.GetFileName(f), false, f));
        }
        catch (Exception ex) { SetStatus("local error: " + ex.Message); }
    }

    private async Task RefreshRemote(CancellationToken ct = default)
    {
        _remote = new List<Entry>();
        SetStatus("loading remote..."); Render();
        try
        {
            var items = await _client.ListAsync(_cfg.RemoteCwd, ct);
            if (_cfg.RemoteCwd != "")
                _remote.Add(new Entry("..", true, Parent(_cfg.RemoteCwd)));
            foreach (var it in items.OrderBy(x => !x.IsDir).ThenBy(x => x.Name))
                _remote.Add(new Entry(it.Name, it.IsDir, it.Path));
            SetStatus("remote: /" + _cfg.RemoteCwd);
        }
        catch (Exception ex)
        {
            _cfg.RemoteCwd = "";
            _remote = new List<Entry>();
            SetStatus("remote error, reset to root: " + ex.Message);
        }
    }
}
