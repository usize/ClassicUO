#!/usr/bin/env bash
# install.sh — OPTIONAL global PATH installation for the `uo` wrapper
#
# The ClassicUO AI sandbox works out of the box without running this script:
#   - /uo is automatically available in Claude Code (project-local command)
#   - ./ai/uo works from the repo root without any PATH entry
#
# Run this only if you want `uo` available globally (without the ./ai/ prefix)
# in any terminal window or other LLM harness.
#
# Usage:
#   ./ai/install.sh              # symlink to ~/.local/bin/uo
#   ./ai/install.sh --global     # symlink to /usr/local/bin/uo (may need sudo)
#   ./ai/install.sh --uninstall  # remove the symlink

set -euo pipefail

REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GLOBAL=false
UNINSTALL=false

for arg in "$@"; do
    case "$arg" in
        --global)    GLOBAL=true ;;
        --uninstall) UNINSTALL=true ;;
        --help|-h)
            cat <<'HELP'
install.sh — optional global PATH install for the `uo` wrapper

Without this script:
  Claude Code: type /uo (project-local command, works automatically)
  Terminal:    ./ai/uo summary  (from repo root)

With this script:
  Terminal:    uo summary  (from anywhere)

Options:
  (none)       symlink ai/uo to ~/.local/bin/uo
  --global     symlink ai/uo to /usr/local/bin/uo (may need sudo)
  --uninstall  remove the symlink created by a previous run
HELP
                exit 0
            ;;
    esac
done

if $GLOBAL; then
    BIN_DIR="/usr/local/bin"
else
    BIN_DIR="$HOME/.local/bin"
    mkdir -p "$BIN_DIR"
    if [[ ":$PATH:" != *":$BIN_DIR:"* ]]; then
        echo "WARNING: $BIN_DIR is not in your PATH."
        echo "  Add this to your shell rc:  export PATH=\"\$HOME/.local/bin:\$PATH\""
    fi
fi

TARGET="$BIN_DIR/uo"

if $UNINSTALL; then
    if [[ -L "$TARGET" ]]; then
        rm "$TARGET"
        echo "Removed $TARGET"
    elif [[ -e "$TARGET" ]]; then
        echo "Skipped $TARGET (not a symlink — remove manually if needed)"
    else
        echo "Nothing to remove at $TARGET"
    fi
    exit 0
fi

ln -sf "$REPO_DIR/uo" "$TARGET"
chmod +x "$REPO_DIR/uo"
echo "Linked  $REPO_DIR/uo  →  $TARGET"
echo ""
echo "You can now run: uo help"
