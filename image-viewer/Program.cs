using CloudStorage.Shared;

namespace ImageViewer;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var opts = Options.Parse<Options>(args);
        if (opts == null || opts.Help)
        {
            PrintHelp();
            return opts == null ? 1 : 0;
        }

        int width = opts.Width ?? (Console.IsOutputRedirected ? 120 : Math.Max(10, Console.WindowWidth - 1));
        int height = opts.Height ?? (Console.IsOutputRedirected ? 50 : Math.Max(5, Console.WindowHeight - 2));

        try
        {
            Stream stream;
            if (opts.RemotePath != null)
            {
                if (opts.Url == null || opts.Token == null)
                {
                    Console.Error.WriteLine("error: --remote requires --url and --token");
                    return 1;
                }
                var client = new StorageClient(opts.Url, opts.Token);
                if (opts.List)
                {
                    var items = await client.ListAsync(opts.RemotePath);
                    foreach (var it in items) Console.WriteLine(it.Name + (it.IsDir ? "/" : ""));
                    return 0;
                }
                stream = await client.DownloadAsync(opts.RemotePath);
            }
            else if (opts.LocalPath != null)
            {
                stream = File.OpenRead(opts.LocalPath);
            }
            else
            {
                if (Console.IsInputRedirected)
                {
                    stream = Console.OpenStandardInput();
                }
                else
                {
                    Console.Error.WriteLine("error: provide a file path, --remote, or pipe image data");
                    return 1;
                }
            }

            using (stream)
            {
                string art = AsciiRenderer.RenderImage(stream, width, height, opts.Color);
                Console.Write(art);
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("error: " + ex.Message);
            return 1;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine(@"image-viewer - render images as ASCII art in the terminal

Usage:
  image-viewer <file> [options]          render a local image
  image-viewer --remote <path> [opts]   render an image from cloud-storage-backend
  image-viewer --remote <path> --list   list files in a remote folder
  cat img.png | image-viewer            render piped image data

Options:
  -w, --width <n>    max output width in chars (default: terminal width)
  -H, --height <n>   max output height in rows (default: terminal height)
  -c, --color        use 24-bit ANSI truecolor
  -l, --list         list remote folder instead of rendering
  --url <url>        cloud-storage-backend base URL (e.g. http://100.x.y.z:8080)
  --token <token>    API bearer token
  -h, --help         show this help

By default the image is scaled to fill the terminal window, preserving aspect ratio.");
    }

    private sealed class Options : CloudStorage.Shared.Options
    {
        public int? Height;
        public bool List;

        protected override bool Consume(string flag, string[] args, ref int i)
        {
            switch (flag)
            {
                case "-H":
                case "--height":
                    if (i + 1 >= args.Length || !int.TryParse(args[++i], out int h)) return false;
                    Height = h;
                    return true;
                case "-l":
                case "--list":
                    List = true;
                    return true;
                default:
                    return false;
            }
        }
    }
}
