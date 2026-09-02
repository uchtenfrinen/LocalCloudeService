#!/usr/bin/env bash
# Usage:
#   ./deploy.sh 100.x.x.x                      # user taken from ~/.ssh/config (or default)
#   SERVER_USER=admin ./deploy.sh 100.x.x.x     # explicit user
#   SERVER_HOST=100.x.x.x SERVER_USER=admin ./deploy.sh
#
# Requirements on the client: go, ssh, scp.
# Requirements on the server: systemd, a user with sudo.
# The SSH user must have this client's key in authorized_keys (ssh-copy-id).
#
# The token is written to /etc/cloud-storage.env on the server (chmod 600),
# NOT into the unit file, so secrets never land in the repo or in logs.

set -euo pipefail

SERVER_HOST="${1:-${SERVER_HOST:?set SERVER_HOST (tailscale IP/hostname)}}"
SERVER_USER="${SERVER_USER:-}"
TARGET="$SERVER_HOST"
[ -n "$SERVER_USER" ] && TARGET="$SERVER_USER@$SERVER_HOST"

# Load local secrets from a gitignored .env (never committed).
# Reads the repo-root .env first (shared with the client), then the backend .env.
# Only fills variables that are not already set in the environment.
HERE_DEPLOY="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
load_env() {
  local f="$1"
  [ -f "$f" ] || return 0
  set -a
  while IFS='=' read -r k v; do
    k="$(echo "$k" | tr -d '[:space:]')"
    v="$(echo "$v" | tr -d '[:space:]' | sed 's/^"//; s/"$//')"
    [ -z "$k" ] && continue
    [ -n "${!k:-}" ] && continue
    export "$k=$v"
  done < "$f"
  set +a
}
load_env "$HERE_DEPLOY/../.env"
load_env "$HERE_DEPLOY/.env"

if [ -z "${STORAGE_TOKEN:-}" ]; then
  echo "ERROR: STORAGE_TOKEN is not set. Run ../setup.sh first (it creates ./.env with the token)." >&2
  exit 1
fi

# One-time: enable passwordless sudo on the server so deploys are non-interactive.
if ! ssh "$TARGET" 'sudo -n true' 2>/dev/null; then
  echo "The server needs a sudo password (one-time) to enable passwordless sudo for deploys."
  read -r -s -p "sudo password for $TARGET: " SUDO_PASS
  echo
  # Append a NOPASSWD rule as the LAST line of /etc/sudoers (wins over group rules),
  # then validate the whole file with visudo -c so we never leave sudo broken.
  ssh "$TARGET" "sudo -S -p '' bash -c 'set -e; R=\"\$(id -un) ALL=(ALL) NOPASSWD:ALL\"; grep -qxF \"\$R\" /etc/sudoers || echo \"\$R\" >> /etc/sudoers; visudo -c'" <<< "$SUDO_PASS"
  if ssh "$TARGET" 'sudo -n true' 2>/dev/null; then
    echo "Passwordless sudo enabled."
  else
    echo "ERROR: could not enable passwordless sudo automatically." >&2
    echo "Enable it manually (replace 'admin' with the real server user if different), then re-run deploy.sh:" >&2
    echo "  ssh $TARGET" >&2
    echo "  echo 'admin ALL=(ALL) NOPASSWD:ALL' | sudo tee -a /etc/sudoers && sudo visudo -c" >&2
    exit 1
  fi
fi

INSTALL_DIR="/opt/cloud-storage"
STORAGE_DIR="/srv/storage"
LOCAL_BIN="./server-linux"
SERVICE_SRC="./cloud-storage.service"
ENV_FILE="/etc/cloud-storage.env"

echo "== build linux binary =="
GOOS=linux GOARCH=amd64 go build -o "$LOCAL_BIN" .

echo "== prepare dirs on server =="
ssh "$TARGET" \
  "sudo mkdir -p '$INSTALL_DIR' '$STORAGE_DIR' && sudo chown -R \$(id -u):\$(id -g) '$STORAGE_DIR'"

echo "== copy binary =="
scp "$LOCAL_BIN" "$TARGET:/tmp/server-linux"
ssh "$TARGET" \
  "sudo mv /tmp/server-linux '$INSTALL_DIR/server' && sudo chmod +x '$INSTALL_DIR/server'"

echo "== install systemd unit =="
scp "$SERVICE_SRC" "$TARGET:/tmp/cloud-storage.service"
ssh "$TARGET" \
  "sudo mv /tmp/cloud-storage.service /etc/systemd/system/cloud-storage.service && sudo systemctl daemon-reload"

echo "== write env (token) =="
# Token is piped via stdin (never echoed, never in the remote command line)
printf 'STORAGE_TOKEN=%s\nSTORAGE_ROOT=%s\n' "$STORAGE_TOKEN" "$STORAGE_DIR" \
  | ssh "$TARGET" "sudo tee '$ENV_FILE' >/dev/null && sudo chmod 600 '$ENV_FILE'"

echo "== enable & restart =="
ssh "$TARGET" \
  "sudo systemctl enable --now cloud-storage && sleep 1 && systemctl is-active cloud-storage"

echo "== done =="
echo "Client: csc   (uses the same token from ~/.config/csc/.env)"
