namespace CloudStorage.Shared;

/// <summary>
/// Base CLI options shared by the ASCII-viewer projects. Parses the flags the
/// two viewers have in common (--url/--token/--remote/-w/--width/-c/--color/
/// /-h/--help) plus the positional local path. Viewer-specific flags are
/// handled by overriding <see cref="Consume"/>.
/// </summary>
public class Options
{
    public bool Help;
    public int? Width;
    public bool Color;
    public string? Url;
    public string? Token;
    public string? RemotePath;
    public string? LocalPath;

    /// <summary>
    /// Parses <paramref name="args"/> applied to a new <typeparamref name="T"/>.
    /// Returns null if the argument list is malformed (caller prints help and
    /// returns a non-zero exit code).
    /// </summary>
    public static T? Parse<T>(string[] args) where T : Options, new()
    {
        var o = new T();
        for (int i = 0; i < args.Length; i++)
        {
            string flag = args[i];
            switch (flag)
            {
                case "-h":
                case "--help":
                    o.Help = true;
                    break;
                case "-w":
                case "--width":
                    if (++i >= args.Length || !int.TryParse(args[i], out int w)) return null;
                    o.Width = w;
                    break;
                case "-c":
                case "--color":
                    o.Color = true;
                    break;
                case "--url":
                    if (++i >= args.Length) return null;
                    o.Url = args[i];
                    break;
                case "--token":
                    if (++i >= args.Length) return null;
                    o.Token = args[i];
                    break;
                case "--remote":
                    if (++i >= args.Length) return null;
                    o.RemotePath = args[i];
                    break;
                default:
                    if (flag.StartsWith("-"))
                    {
                        // Viewer-specific flag: give the consumer a chance to
                        // read both it and any parameter it needs.
                        if (!o.Consume(flag, args, ref i)) return null;
                    }
                    else
                    {
                        o.LocalPath = flag;
                    }
                    break;
            }
        }
        return o;
    }

    /// <summary>
    /// Template hook for viewer-specific flags. <paramref name="i"/> points at
    /// the flag; advance it past any consumed parameters. Return false to mark
    /// the argument list invalid, true otherwise.
    /// </summary>
    protected virtual bool Consume(string flag, string[] args, ref int i) => false;
}
