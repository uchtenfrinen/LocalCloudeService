using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace CloudStorage.Shared;

/// <summary>
/// Converts pixel data into ASCII art with 24-bit ANSI truecolor (each symbol
/// is drawn in the color of the pixel it represents). The span-based core is
/// shared by all viewers; the image overload adds ImageSharp loading/resizing
/// on top of it.
/// </summary>
public static class AsciiRenderer
{
    private const string Ramp = " .'`^\",:;Il!i><~+_-?][}{1)(|\\/tfjrxnuvczXYUJCLQ0OZmwqpdbkhao*#MW&8%B@$";
    private const float CellAspect = 0.5f;

    /// <summary>
    /// Renders raw RGB24 bytes (three bytes per pixel, row-major) to ASCII.
    /// </summary>
    public static string Render(ReadOnlySpan<byte> rgb, int w, int h, bool color)
    {
        var sb = new System.Text.StringBuilder(w * (h + 1));
        string reset = "\u001b[0m";

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = (y * w + x) * 3;
                byte r = rgb[i], g = rgb[i + 1], b = rgb[i + 2];
                sb.Append(Char(r, g, b));
            }
            sb.Append(reset).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>
    /// Loads an image from <paramref name="imageStream"/>, scales it to fit
    /// <paramref name="maxWidth"/> x <paramref name="maxHeight"/> preserving
    /// aspect ratio, and renders it as ASCII.
    /// </summary>
    public static string RenderImage(Stream imageStream, int maxWidth, int maxHeight, bool color)
    {
        using var image = Image.Load<Rgb24>(imageStream);

        float aspect = (float)image.Height / image.Width;

        int outWidth = Math.Clamp(maxWidth, 10, 600);
        if (outWidth * aspect * CellAspect > maxHeight)
            outWidth = Math.Max(10, (int)Math.Floor(maxHeight / (aspect * CellAspect)));
        outWidth = Math.Clamp(outWidth, 10, 600);

        int outHeight = Math.Max(1, (int)Math.Round(aspect * outWidth * CellAspect));
        if (outHeight > maxHeight)
        {
            outWidth = Math.Max(10, (int)Math.Floor(maxHeight / (aspect * CellAspect)));
            outHeight = Math.Max(1, (int)Math.Round(aspect * outWidth * CellAspect));
        }

        image.Mutate(x => x.Resize(outWidth, outHeight));

        var rgb = new byte[outWidth * outHeight * 3];
        int n = 0;
        for (int y = 0; y < outHeight; y++)
        {
            for (int x = 0; x < outWidth; x++)
            {
                Rgb24 p = image[x, y];
                rgb[n++] = p.R;
                rgb[n++] = p.G;
                rgb[n++] = p.B;
            }
        }
        return Render(rgb, outWidth, outHeight, color);
    }

    private static string Char(byte r, byte g, byte b)
    {
        float lum = (0.299f * r + 0.587f * g + 0.114f * b) / 255f;
        int idx = (int)Math.Round((1f - lum) * (Ramp.Length - 1));
        char c = Ramp[idx];
        return $"\u001b[38;2;{r};{g};{b}m{c}";
    }
}
