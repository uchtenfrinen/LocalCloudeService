using System.Net.Http.Headers;
using System.Text.Json;

namespace CloudStorage.Shared;

/// <summary>
/// HTTP client for cloud-storage-backend. A single concrete class that
/// consolidates what were previously three separate (and mostly duplicated)
/// client implementations in the client, image-viewer and video-viewer
/// projects.
/// </summary>
public sealed class StorageClient
{
    private static readonly SocketsHttpHandler SharedHandler = new()
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
    };

    private readonly HttpClient _http;

    public StorageClient(string baseUrl, string token)
        : this(baseUrl, token, CancellationToken.None)
    {
    }

    public StorageClient(string baseUrl, string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("Base URL must not be empty", nameof(baseUrl));

        _http = new HttpClient(SharedHandler, disposeHandler: false)
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
        };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public record FileItem(string Name, string Path, bool IsDir, long Size, DateTime ModTime);

    private static string BuildUrl(string endpoint, string path) =>
        endpoint + "?path=" + Uri.EscapeDataString(path);

    public async Task<List<FileItem>> ListAsync(string path, CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync(BuildUrl("api/files", path), ct);
        await EnsureAsync(resp, ct);
        string body = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var items = new List<FileItem>();
        if (doc.RootElement.TryGetProperty("items", out var arr))
        {
            foreach (var e in arr.EnumerateArray())
            {
                items.Add(new FileItem(
                    e.GetProperty("name").GetString() ?? "",
                    e.GetProperty("path").GetString() ?? "",
                    e.GetProperty("is_dir").GetBoolean(),
                    e.GetProperty("size").GetInt64(),
                    e.GetProperty("mod_time").GetDateTime()));
            }
        }
        return items;
    }

    public async Task MkdirAsync(string path, CancellationToken ct = default)
    {
        var content = new StringContent(
            "{\"path\":\"" + path.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}",
            System.Text.Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync("api/mkdir", content, ct);
        await EnsureAsync(resp, ct);
    }

    public async Task UploadAsync(string localPath, string remoteDir, CancellationToken ct = default)
    {
        using var fileStream = File.OpenRead(localPath);
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(fileStream), "file", Path.GetFileName(localPath));
        using var resp = await _http.PostAsync(BuildUrl("api/upload", remoteDir), content, ct);
        await EnsureAsync(resp, ct);
    }

    public async Task DownloadAsync(string remotePath, string localPath, CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync(BuildUrl("api/download", remotePath), ct);
        await EnsureAsync(resp, ct);
        await using var fs = File.Create(localPath);
        await resp.Content.CopyToAsync(fs, ct);
    }

    /// <summary>
    /// Returns a <see cref="Stream"/> to the remote file's content. The caller
    /// owns the returned stream and is responsible for disposing it.
    /// </summary>
    public async Task<Stream> DownloadAsync(string remotePath, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync(BuildUrl("api/download", remotePath), ct);
        if (!resp.IsSuccessStatusCode)
        {
            string body = await resp.Content.ReadAsStringAsync(ct);
            resp.Dispose();
            throw new Exception($"download failed ({(int)resp.StatusCode} {resp.ReasonPhrase}): {body}");
        }
        return await resp.Content.ReadAsStreamAsync(ct);
    }

    /// <summary>
    /// Downloads a remote file into a temp file (deleted by the caller after use)
    /// and returns its path. This is used for streaming remote videos to ffmpeg.
    /// </summary>
    public async Task<string> DownloadToTempAsync(string remotePath, CancellationToken ct = default)
    {
        string url = BuildUrl("api/download", remotePath);
        using var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode)
            throw new Exception($"download failed ({(int)resp.StatusCode} {resp.ReasonPhrase})");
        string ext = Path.GetExtension(remotePath);
        string tmp = Path.Combine(Path.GetTempPath(), "video-viewer-" + Guid.NewGuid().ToString("N") + ext);
        await using var fs = File.Create(tmp);
        await resp.Content.CopyToAsync(fs, ct);
        return tmp;
    }

    public async Task DeleteAsync(string remotePath, CancellationToken ct = default)
    {
        using var resp = await _http.DeleteAsync(BuildUrl("api/files", remotePath), ct);
        await EnsureAsync(resp, ct);
    }

    /// <summary>
    /// Returns true if <paramref name="remotePath"/> exists on the server AND is a
    /// directory (ListAsync succeeds). Used to validate before entering a folder,
    /// so the user cannot "cd" into a non-existent path and get a bad request.
    /// </summary>
    public async Task<bool> DirExistsAsync(string remotePath, CancellationToken ct = default)
    {
        try
        {
            await ListAsync(remotePath, ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> RemoteExistsAsync(string remotePath, CancellationToken ct = default)
    {
        try
        {
            using var resp = await _http.GetAsync(BuildUrl("api/files", remotePath), ct);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static async Task EnsureAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        if (resp.IsSuccessStatusCode)
            return;

        string body = "";
        try { body = await resp.Content.ReadAsStringAsync(ct); }
        catch { }
        throw new Exception($"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}: {body}");
    }
}
