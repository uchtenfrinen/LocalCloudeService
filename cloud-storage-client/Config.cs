using System.Text.Json;
using CloudStorage.Shared;

namespace CloudStorageClient;

internal sealed class AppConfig
{
    public string Url { get; set; } = "";
    public string Token { get; set; } = "";
    public string LocalCwd { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public string RemoteCwd { get; set; } = "";

    private static string ConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "csc");

    private static string ConfigPath => Path.Combine(ConfigDir, "config.json");

    /// <summary>
    /// Resolution order: built-in defaults < config file < .env < command-line args.
    /// The token is taken from args or .env only — it is NEVER written to the config file.
    /// </summary>
    public static AppConfig Load(string[] args)
    {
        var cfg = new AppConfig();

        // 1. config file (no token inside)
        try
        {
            if (File.Exists(ConfigPath))
            {
                var saved = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath));
                if (saved != null)
                {
                    cfg.Url = saved.Url;
                    cfg.LocalCwd = saved.LocalCwd;
                    cfg.RemoteCwd = saved.RemoteCwd;
                }
            }
        }
        catch { }

        // 2. .env — first the config dir (works from any CWD), then the current dir
        DotEnv.Load(Path.Combine(ConfigDir, ".env"));
        DotEnv.Load(".env");

        // 3. command-line args win over everything except they are optional
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--url" && i + 1 < args.Length) cfg.Url = args[++i];
            else if (args[i] == "--token" && i + 1 < args.Length) cfg.Token = args[++i];
            else if (args[i] == "--local" && i + 1 < args.Length) cfg.LocalCwd = args[++i];
            else if (args[i] == "--remote" && i + 1 < args.Length) cfg.RemoteCwd = args[++i];
            else if (args[i] == "--viewer" && i + 1 < args.Length) ViewerPath = args[++i];
        }

        // env vars as a fallback for token/url (already loaded via .env or real env)
        cfg.Url = cfg.Url.Length == 0 ? Environment.GetEnvironmentVariable("STORAGE_URL") ?? "" : cfg.Url;
        cfg.Token = cfg.Token.Length == 0 ? Environment.GetEnvironmentVariable("STORAGE_TOKEN") ?? "" : cfg.Token;

        if (!Directory.Exists(cfg.LocalCwd))
            cfg.LocalCwd = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return cfg;
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath)!;
            Directory.CreateDirectory(dir);
            // never persist the token
            var toSave = new AppConfig { Url = Url, LocalCwd = LocalCwd, RemoteCwd = RemoteCwd };
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(toSave, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    public static string ViewerPath { get; set; } = "image-viewer";
}
