using System.Diagnostics;
using System.Text.Json;

namespace VideoViewer;

internal static class Probe
{
    public static (int Width, int Height, double Fps) Run(string file)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "ffprobe",
            ArgumentList =
            {
                "-v", "error",
                "-select_streams", "v:0",
                "-show_entries", "stream=width,height,r_frame_rate",
                "-of", "json",
                file
            },
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        using var p = Process.Start(psi) ?? throw new Exception("ffprobe not found");
        string json = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0) throw new Exception("ffprobe failed");

        using var doc = JsonDocument.Parse(json);
        var stream = doc.RootElement.GetProperty("streams")[0];
        int w = stream.GetProperty("width").GetInt32();
        int h = stream.GetProperty("height").GetInt32();

        string fr = stream.GetProperty("r_frame_rate").GetString() ?? "25/1";
        var parts = fr.Split('/');
        double fps = parts.Length == 2
            ? double.Parse(parts[0]) / double.Parse(parts[1])
            : double.Parse(fr);

        if (fps <= 0 || fps > 120) fps = 25;
        return (w, h, fps);
    }
}
