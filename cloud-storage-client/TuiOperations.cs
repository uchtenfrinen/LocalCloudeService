using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CloudStorageClient;

internal sealed partial class TuiApp
{
    private async Task Upload(CancellationToken ct = default)
    {
        if (!_leftFocused) { SetStatus("switch to Local panel (Tab) to pick a file"); return; }
        var e = At(_local, _localSel);
        if (e == null || e.IsDir) { SetStatus("pick a local file first"); return; }
        if (!File.Exists(e.Path)) { SetStatus("file not found"); return; }

        SetStatus("uploading " + e.Name + " ..."); Render();
        try
        {
            await _client.UploadAsync(e.Path, _cfg.RemoteCwd, ct);
            await RefreshRemote(ct);
            SetStatus("uploaded " + e.Name);
        }
        catch (Exception ex) { SetStatus("upload failed: " + ex.Message); }
    }

    private async Task Download(CancellationToken ct = default)
    {
        if (_leftFocused) { SetStatus("switch to Remote panel (Tab) to pick a file"); return; }
        var e = At(_remote, _remoteSel);
        if (e == null || e.IsDir) { SetStatus("pick a remote file first"); return; }
        string dest = Path.Combine(_cfg.LocalCwd, e.Name);

        SetStatus("downloading " + e.Name + " ..."); Render();
        try
        {
            await _client.DownloadAsync(e.Path, dest, ct);
            RefreshLocal();
            SetStatus("downloaded -> " + dest);
        }
        catch (Exception ex) { SetStatus("download failed: " + ex.Message); }
    }

    private async Task Mkdir(CancellationToken ct = default)
    {
        string? name = Prompt(_leftFocused ? "New local folder name" : "New remote folder name");
        if (string.IsNullOrWhiteSpace(name) || name == ".." || name.Contains('/')) { SetStatus("cancelled / invalid name"); return; }

        if (_leftFocused)
        {
            try { Directory.CreateDirectory(Path.Combine(_cfg.LocalCwd, name)); RefreshLocal(); SetStatus("created local " + name); }
            catch (Exception ex) { SetStatus("mkdir error: " + ex.Message); }
        }
        else
        {
            string path = string.IsNullOrEmpty(_cfg.RemoteCwd) ? name : _cfg.RemoteCwd + "/" + name;
            SetStatus("creating /" + path + " ..."); Render();
            try { await _client.MkdirAsync(path, ct); await RefreshRemote(ct); SetStatus("created remote " + name); }
            catch (Exception ex) { SetStatus("mkdir failed: " + ex.Message); }
        }
    }

    private async Task Delete(CancellationToken ct = default)
    {
        var e = _leftFocused ? At(_local, _localSel) : At(_remote, _remoteSel);
        if (e == null || e.Name == "..") return;
        string? ans = Prompt($"Delete {(e.IsDir ? "folder" : "file")} '{e.Name}'? (y/N)");
        if (ans == null || !ans.Trim().ToLowerInvariant().StartsWith("y")) { SetStatus("cancelled"); return; }

        if (_leftFocused)
        {
            try
            {
                if (e.IsDir) Directory.Delete(e.Path, true); else File.Delete(e.Path);
                RefreshLocal(); SetStatus("deleted " + e.Name);
            }
            catch (Exception ex) { SetStatus("delete error: " + ex.Message); }
        }
        else
        {
            SetStatus("deleting " + e.Name + " ..."); Render();
            try { await _client.DeleteAsync(e.Path, ct); await RefreshRemote(ct); SetStatus("deleted " + e.Name); }
            catch (Exception ex) { SetStatus("delete failed: " + ex.Message); }
        }
    }

    private void View()
    {
        if (_leftFocused)
        {
            var e = At(_local, _localSel);
            if (e == null || e.IsDir) { SetStatus("pick a file"); return; }
            if (IsImage(e.Name)) ViewLocal(e, "image-viewer");
            else if (IsVideo(e.Name)) ViewLocal(e, "video-viewer");
            else SetStatus("unsupported file type");
        }
        else
        {
            var e = At(_remote, _remoteSel);
            if (e == null || e.IsDir) { SetStatus("pick a file"); return; }
            if (IsImage(e.Name)) ViewRemote(e, "image-viewer");
            else if (IsVideo(e.Name)) ViewRemote(e, "video-viewer");
            else SetStatus("unsupported file type");
        }
    }

    private void ViewLocal(Entry e, string kind) => LaunchViewer(kind, new List<string> { e.Path });

    private void ViewRemote(Entry e, string kind) =>
        LaunchViewer(kind, new List<string> { "--remote", e.Path, "--url", _cfg.Url, "--token", _cfg.Token });

    private static string ResolveViewer(string kind)
    {
        string configured = kind == "video-viewer" ? AppConfig.VideoViewerPath : AppConfig.ViewerPath;
        var candidates = new List<string> { configured };
        if (configured == kind)
        {
            var dir = AppContext.BaseDirectory;
            while (dir != null)
            {
                candidates.Add(Path.Combine(dir, kind, "bin", "Release", "net10.0", kind));
                candidates.Add(Path.Combine(dir, kind, "bin", "Debug", "net10.0", kind));
                candidates.Add(Path.Combine(dir, kind));
                dir = Path.GetDirectoryName(dir);
            }
        }
        foreach (var c in candidates)
            if (File.Exists(c)) return c;
        return configured; // let the OS resolve via PATH
    }

    private void LaunchViewer(string kind, List<string> args)
    {
        var viewer = ResolveViewer(kind);
        var psi = new ProcessStartInfo { FileName = viewer, UseShellExecute = false };
        foreach (var a in args) psi.ArgumentList.Add(a);
        Console.Clear();
        try
        {
            var p = Process.Start(psi);
            if (p == null) throw new Exception("process did not start");
            p.WaitForExit();
            Console.WriteLine($"\n[{kind} finished] Press any key to return to the file manager...");
            Console.ReadKey(true);
        }
        catch (Exception ex)
        {
            Console.WriteLine("FAILED to launch " + kind + " (" + viewer + "): " + ex.Message);
            Console.WriteLine("Make sure " + kind + " is built and on PATH, or start the client with --viewer /path/to/"
                              + (kind == "video-viewer" ? "video-viewer" : "image-viewer"));
            Console.WriteLine("Press any key to return...");
            Console.ReadKey(true);
        }
    }
}
