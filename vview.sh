#!/usr/bin/env bash
# Launch the video-to-ASCII viewer.
# Usage: vview <local-file> | vview --remote <path> [--url URL] [--token TOKEN]
set -euo pipefail
SOURCE="${BASH_SOURCE[0]}"
while [ -L "$SOURCE" ]; do SOURCE="$(readlink "$SOURCE")"; done
HERE="$(cd "$(dirname "$SOURCE")" && pwd)"
exec dotnet run -c Release --project "$HERE/video-viewer" -- "$@"
