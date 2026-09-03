# cloud-storage-client

Interactive **two-panel console TUI** (Midnight-Commander / Total-Commander style) for
[cloud-storage-backend](../cloud-storage-backend), written in C# (.NET 10).
Cross-platform (Arch, Ubuntu, macOS, Windows), **no external dependencies** — the UI
is rendered directly in the terminal.

Left panel = your local filesystem. Right panel = the remote storage on your server.
You navigate both, and move files between them with single keystrokes. It also
integrates with [image-viewer](../image-viewer): the `F3` key renders a remote (or
local) photo as ASCII art directly in the terminal.

## One-time setup

From the repo root, run the installer (builds everything, stores credentials once,
installs the `csc` / `vview` launchers into `~/.local/bin`):

```bash
./setup.sh
```

It will ask for the backend URL + token once and save them to
`~/.config/csc/.env` (git-ignored, outside the repo). After that you never type a
token or URL again.

## Build

```bash
dotnet build -c Release
```

The binary lands in `bin/Release/net10.0/`. This project depends on
[`CloudStorage.Shared`](../CloudStorage.Shared) (HTTP client, `.env` loader); it is
built automatically as part of this project via a `ProjectReference`.

## Run

Daily use — just:

```bash
csc          # launches the TUI, reads URL/token from ~/.config/csc/.env
```

Manual alternative (same result, no setup needed):

```bash
dotnet run -c Release -- \
  --url http://100.91.32.58:8080 \
  --token your_long_token \
  --viewer /path/to/image-viewer \
  --video-viewer /path/to/video-viewer
```

`csc` is a thin wrapper around the command above, so any extra args
(`--local`, `--remote`, `--viewer`) can still be passed.

### Options

| Flag | Description |
|------|-------------|
| `--url <url>` | Backend base URL, e.g. `http://100.64.0.5:8080` |
| `--token <token>` | API bearer token |
| `--viewer <path>` | Path to the `image-viewer` binary (used by `F3` / Enter on images) |
| `--video-viewer <path>` | Path to the `video-viewer` binary (used by `F3` / Enter on videos) |
| `--local <path>` | Start the local panel in this directory |
| `--remote <path>` | Start the remote panel in this folder |

## Session & secrets

- The token and URL can also come from environment variables `STORAGE_URL` /
  `STORAGE_TOKEN` or a local `.env` file (same format as the backend). The client
  auto-loads `.env` if present.
- **Current local and remote folders are persisted** between runs in
  `~/.config/csc/config.json` so you don't re-navigate every launch.
- The token itself is **never** written to that config file — only `url` and the
  two folder paths. Keep the token in `.env` / env var / `--token`.

Example `.env` (git-ignored):

```
STORAGE_URL=http://100.91.32.58:8080
STORAGE_TOKEN=your_long_token
```

## Controls

| Key | Action |
|-----|--------|
| ↑ / ↓ | Move selection |
| Tab / ← / → | Switch active panel |
| Enter | Open folder / preview image (ASCII) |
| F2 or `r` | Refresh both panels |
| F5 or `u` | Upload selected local file → remote (current folder) |
| F6 or `o` | Download selected remote file → local (current folder) |
| F7 or `n` | Create folder (in active panel) |
| F8 or `x` | Delete selected (with confirmation) |
| F3 or `v` | View image as ASCII (launches `image-viewer`) |
| Q / Esc | Quit |

> Note: some terminals don't deliver function keys to `Console.ReadKey`, so every
> action also has a letter shortcut (`r/u/o/n/x/v`). Use those if `F1`–`F8` seem dead.

## Validation

The client refuses to do nonsensical things instead of throwing raw errors:

- Entering a remote folder checks it actually exists first (`DirExistsAsync`);
  if not, it shows `folder does not exist` and keeps you where you were.
- Upload / download / delete verify the source exists before acting.
- `mkdir` rejects empty or invalid names (`..`, names containing `/`).

## How `F3` (view) works

`F3` (and `Enter` on a file) launches the matching viewer: the `image-viewer`
executable for photos (path from `--viewer`, default `image-viewer` on `PATH`) and
the `video-viewer` executable for videos (path from `--video-viewer`, default
`video-viewer` on `PATH`). For a remote file it passes `--remote <path> --url <url>
--token <token>`; for a local file it passes the local path. The TUI is suspended
while the viewer runs, then redrawn when the viewer exits.

## Notes

- The client keeps a virtual working directory on the remote storage, so you don't
  type full paths repeatedly.
- Remote errors (e.g. server unreachable) reset the remote panel to root and show a
  message — the local panel keeps working.
