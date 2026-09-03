using System.Diagnostics;
using CloudStorage.Shared;

namespace VideoViewer;

internal static class Program
{
    private static int Main(string[] args)
    {
        var opts = Options.Parse<Options>(args);
        if (opts == null || opts.Help) { PrintHelp(); return opts == null ? 1 : 0; }

        if (opts.RemotePath == null && opts.LocalPath == null)
        {
            Console.Error.WriteLine("error: provide a file path, --remote, or pipe");
            return 1;
        }

        string file;
        if (opts.RemotePath != null)
        {
            if (opts.Url == null || opts.Token == null)
            {
                Console.Error.WriteLine("error: --remote requires --url and --token");
                return 1;
            }
            try
            {
                var client = new StorageClient(opts.Url, opts.Token);
                Console.Error.WriteLine("downloading remote video...");
                file = client.DownloadToTempAsync(opts.RemotePath).GetAwaiter().GetResult();
            }
            catch (Exception ex) { Console.Error.WriteLine("error: " + ex.Message); return 1; }
        }
        else
        {
            file = opts.LocalPath!;
        }

        if (!File.Exists(file)) { Console.Error.WriteLine("error: file not found: " + file); return 1; }

        try
        {
            Play(file, opts).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("error: " + ex.Message);
            return 1;
        }
        return 0;
    }

    private static async Task Play(string file, Options o)
    {
        var probe = Probe.Run(file);
        int outW = o.Width ?? Math.Min(Console.BufferWidth - 1, 120);
        int outH = Math.Max(1, (int)Math.Round(outW * (double)probe.Height / probe.Width * 0.5));
        double fps = o.Fps ?? probe.Fps;

        int frameBytes = outW * outH * 3;

        var args = new List<string> { "-loglevel", "error", "-i", file };
        if (o.Seek.HasValue) { args.Add("-ss"); args.Add(o.Seek.Value.ToString("0.###")); }
        args.Add("-vf");
        args.Add($"scale={outW}:{outH}");
        args.Add("-f"); args.Add("rawvideo");
        args.Add("-pix_fmt"); args.Add("rgb24");
        args.Add("-r"); args.Add(fps.ToString("0.###"));
        args.Add("-");

        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var p = Process.Start(psi) ?? throw new Exception("ffmpeg not found");
        using var stdout = p.StandardOutput.BaseStream;

        var frame = new byte[frameBytes];
        var sw = new Stopwatch();
        int shown = 0;
        long maxFrames = o.MaxFrames.HasValue ? o.MaxFrames.Value : long.MaxValue;

        Console.Write("\u001b[2J");
        while (shown < maxFrames)
        {
            int total = 0;
            while (total < frameBytes)
            {
                int n = await stdout.ReadAsync(frame, total, frameBytes - total);
                if (n == 0) goto done;
                total += n;
            }

            sw.Restart();
            string art = AsciiRenderer.Render(frame, outW, outH, o.Color);
            Console.Write("\u001b[H" + art);
            shown++;

            double delay = 1000.0 / fps - sw.ElapsedMilliseconds;
            if (delay > 0) await Task.Delay((int)delay);
        }
    done:
        await p.WaitForExitAsync();
        Console.Write("\u001b[2J\u001b[H");
        Console.WriteLine($"played {shown} frames");
    }

    private static void PrintHelp()
    {
        Console.WriteLine(@"video-viewer - play video as ASCII/ANSI art in the terminal

Usage:
  video-viewer <file> [options]
  video-viewer --remote <path> --url <url> --token <token> [options]

Requires ffmpeg and ffprobe on PATH.

Options:
  -w, --width <n>     output width in characters (default: terminal width)
  -r, --fps <n>       override playback frame rate
  -c, --color         24-bit ANSI truecolor (enabled by default)
  -n, --frames <n>    play only the first N frames
  -s, --seek <sec>    start at offset (seconds)
  --remote <path>     play a video stored on cloud-storage-backend
  --url <url>         backend base URL
  --token <token>     backend bearer token
  -h, --help          show this help");
    }

    private sealed class Options : CloudStorage.Shared.Options
    {
        public double? Fps;
        public int? MaxFrames;
        public double? Seek;

        protected override bool Consume(string flag, string[] args, ref int i)
        {
            switch (flag)
            {
                case "-r":
                case "--fps":
                    if (i + 1 >= args.Length || !double.TryParse(args[++i], out double f)) return false;
                    Fps = f;
                    return true;
                case "-n":
                case "--frames":
                    if (i + 1 >= args.Length || !int.TryParse(args[++i], out int n)) return false;
                    MaxFrames = n;
                    return true;
                case "-s":
                case "--seek":
                    if (i + 1 >= args.Length || !double.TryParse(args[++i], out double t)) return false;
                    Seek = t;
                    return true;
                default:
                    return false;
            }
        }
    }
}
