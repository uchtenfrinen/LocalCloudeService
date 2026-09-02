#!/usr/bin/env bash
# Builds the client + viewers, creates a SINGLE shared token (once), and installs
# the `csc` / `vview` launchers into ~/.local/bin.
#
# The token is written to two places so both sides use the EXACT same secret:
#   - ./.env                (repo root)  -> consumed by cloud-storage-backend/deploy.sh
#   - ~/.config/csc/.env    (client)     -> consumed by `csc`
# You never type the token twice and it can never mismatch.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "== Building projects (this may take a minute) =="
dotnet build -c Release "$HERE/cloud-storage-client"
dotnet build -c Release "$HERE/image-viewer"
dotnet build -c Release "$HERE/video-viewer"

# --- ONE shared token + URL (created once, reused by client AND backend) ---
ROOT_ENV="$HERE/.env"
[ -f "$ROOT_ENV" ] && { set -a; . "$ROOT_ENV"; set +a; }

URL="${STORAGE_URL:-}"
TOKEN="${STORAGE_TOKEN:-}"

if [ -z "$URL" ]; then
  read -r -p "Storage URL (e.g. http://100.91.32.58:8080): " URL
fi
if [ -z "$TOKEN" ]; then
  TOKEN="$(openssl rand -hex 32)"
  echo "Generated a new token (this same token will be deployed to the backend)."
fi

# Persist the shared secret at the repo root so deploy.sh reuses it.
printf 'STORAGE_URL=%s\nSTORAGE_TOKEN=%s\n' "$URL" "$TOKEN" > "$ROOT_ENV"
chmod 600 "$ROOT_ENV"

# Client credentials (git-ignored, outside the repo).
CONFIG_DIR="$HOME/.config/csc"
mkdir -p "$CONFIG_DIR"
printf 'STORAGE_URL=%s\nSTORAGE_TOKEN=%s\n' "$URL" "$TOKEN" > "$CONFIG_DIR/.env"
chmod 600 "$CONFIG_DIR/.env"

# --- install launchers ---
BIN_DIR="$HOME/.local/bin"
mkdir -p "$BIN_DIR"
chmod +x "$HERE/csc.sh" "$HERE/vview.sh"
ln -sf "$HERE/csc.sh" "$BIN_DIR/csc"
ln -sf "$HERE/vview.sh" "$BIN_DIR/vview"

# --- make sure ~/.local/bin is on PATH ---
case ":$PATH:" in
  *":$BIN_DIR:"*) echo "~/.local/bin already on PATH." ;;
  *)
    for rc in "$HOME/.bashrc" "$HOME/.zshrc"; do
      if [ -f "$rc" ]; then
        printf '\nexport PATH="$HOME/.local/bin:$PATH"\n' >> "$rc"
        echo "Added ~/.local/bin to PATH in $rc"
      fi
    done
    ;;
esac

echo
echo "== Done! =="
echo "Restart your shell (or run: source ~/.zshrc   # or ~/.bashrc), then:"
echo "    csc               # launch the file-manager TUI"
echo
echo "Now deploy the backend (it will use the SAME token from ./.env):"
echo "    cd cloud-storage-backend && ./deploy.sh <server-ip>"
echo
echo "Token is stored in $ROOT_ENV (also copied to $CONFIG_DIR/.env)."
