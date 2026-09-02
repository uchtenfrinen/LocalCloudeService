#!/usr/bin/env bash
# Launch the cloud-storage client TUI.
# Credentials come from ~/.config/csc/.env (created once by setup.sh),
# so no args are needed for daily use.
set -euo pipefail
SOURCE="${BASH_SOURCE[0]}"
while [ -L "$SOURCE" ]; do SOURCE="$(readlink "$SOURCE")"; done
HERE="$(cd "$(dirname "$SOURCE")" && pwd)"
exec dotnet run -c Release --project "$HERE/cloud-storage-client" -- "$@"
