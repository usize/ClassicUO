#!/usr/bin/env bash
# run.sh — start the ClassicUO REST API client
#
# Reads connection settings from .restapi.toml in the repo root.
# Any value can be overridden with a CLI flag.
#
# Usage:
#   ./run.sh
#   ./run.sh --username foo --password bar --char "Alt Char"
#   ./run.sh --help

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIG="$SCRIPT_DIR/.restapi.toml"

# ── Parse .restapi.toml ───────────────────────────────────────────────────────

toml_get() {
    local key="$1"
    grep -E "^[[:space:]]*${key}[[:space:]]*=" "$CONFIG" 2>/dev/null \
        | sed 's/.*=[[:space:]]*"\?\([^"#]*\)"\?.*/\1/' \
        | sed 's/[[:space:]]*$//' \
        | head -1
}

if [[ -f "$CONFIG" ]]; then
    cfg_uopath=$(toml_get uopath)
    cfg_server=$(toml_get server)
    cfg_port=$(toml_get port)
    cfg_username=$(toml_get username)
    cfg_password=$(toml_get password)
    cfg_character=$(toml_get character)
else
    cfg_uopath="" cfg_server="" cfg_port="" cfg_username="" cfg_password="" cfg_character=""
fi

# ── Parse CLI flags (override config) ────────────────────────────────────────

cli_uopath="" cli_server="" cli_port="" cli_username="" cli_password="" cli_character=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --uopath)    cli_uopath="$2";    shift 2 ;;
        --server)    cli_server="$2";    shift 2 ;;
        --port)      cli_port="$2";      shift 2 ;;
        --username)  cli_username="$2";  shift 2 ;;
        --password)  cli_password="$2";  shift 2 ;;
        --char)      cli_character="$2"; shift 2 ;;
        --help|-h)
            cat <<'HELP'
run.sh — start the ClassicUO REST API client

Reads from .restapi.toml in the repo root. CLI flags override config values.

Options:
  --uopath   <path>    Path to UO client data files
  --server   <host>    ModernUO server host  (default: 127.0.0.1)
  --port     <port>    ModernUO server port  (default: 2593)
  --username <name>    Account username
  --password <pass>    Account password
  --char     <name>    Character name to log in as

Config file: .restapi.toml (copy from ai/restapi.toml.example)
HELP
            exit 0
            ;;
        *) echo "Unknown argument: $1. Try --help." >&2; exit 1 ;;
    esac
done

# ── Resolve final values (CLI > config > defaults) ────────────────────────────

uopath="${cli_uopath:-${cfg_uopath:-}}"
server="${cli_server:-${cfg_server:-127.0.0.1}}"
port="${cli_port:-${cfg_port:-2593}}"
username="${cli_username:-${cfg_username:-}}"
password="${cli_password:-${cfg_password:-}}"
character="${cli_character:-${cfg_character:-}}"

# ── Validate required values ──────────────────────────────────────────────────

missing=()
[[ -z "$uopath"    ]] && missing+=(uopath)
[[ -z "$username"  ]] && missing+=(username)
[[ -z "$password"  ]] && missing+=(password)
[[ -z "$character" ]] && missing+=(character)

if [[ ${#missing[@]} -gt 0 ]]; then
    echo "Error: missing required values: ${missing[*]}" >&2
    echo "Set them in .restapi.toml or pass as flags. See --help." >&2
    exit 1
fi

# ── Launch ────────────────────────────────────────────────────────────────────

echo "Starting ClassicUO REST API client..."
echo "  server:    $server:$port"
echo "  account:   $username"
echo "  character: $character"
echo "  uopath:    $uopath"
echo "  api:       http://127.0.0.1:9000"
echo ""

dotnet run --project "$SCRIPT_DIR/src/ClassicUO.RestAPI" -- \
    -uopath        "$uopath" \
    -clientversion 7.0.95.0 \
    -ip            "$server" \
    -port          "$port" \
    -username      "$username" \
    -password      "$password" \
    -autologin     true \
    -skiploginscreen \
    -lastcharname  "$character"
