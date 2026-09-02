using System;
using CloudStorage.Shared;

namespace CloudStorageClient;

internal static class Program
{
    private static int Main(string[] args)
    {
        var cfg = AppConfig.Load(args);
        if (cfg.Url.Length == 0 || cfg.Token.Length == 0)
        {
            Console.Error.WriteLine("Storage URL or token missing.");
            Console.Error.WriteLine("Set STORAGE_URL/STORAGE_TOKEN in .env, or pass --url/--token.");
            return 1;
        }

        var client = new StorageClient(cfg.Url, cfg.Token);
        var app = new TuiApp(cfg, client);
        app.Run();
        cfg.Save();
        return 0;
    }
}
