#!/usr/bin/env bash
# install.sh — wire up ClassicUO AI sandbox tools
#
# Creates symlinks so that updates to this repo propagate automatically:
#   ai/uo     → ~/.local/bin/uo          (or /usr/local/bin/uo if requested)
#   ai/uo.md  → ~/.claude/commands/uo.md (Claude Code /uo skill)
#
# Usage:
#   ./ai/install.sh              # install to ~/.local/bin (default)
#   ./ai/install.sh --global     # install to /usr/local/bin (requires sudo)
#   ./ai/install.sh --uninstall  # remove all symlinks

set -euo pipefail

REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GLOBAL=false
UNINSTALL=false

for arg in "$@"; do
    case "$arg" in
        --global)    GLOBAL=true ;;
        --uninstall) UNINSTALL=true ;;
        --help|-h)
            echo "Usage: $0 [--global] [--uninstall]"
            echo "  (default) symlinks uo to ~/.local/bin and uo.md to ~/.claude/commands"
            echo "  --global    symlinks uo to /usr/local/bin instead (may need sudo)"
            echo "  --uninstall removes all symlinks installed by this script"
            exit 0
            ;;
    esac
done

# ── Determine bin target ──────────────────────────────────────────────────────

if $GLOBAL; then
    BIN_DIR="/usr/local/bin"
else
    BIN_DIR="$HOME/.local/bin"
    mkdir -p "$BIN_DIR"
    # Warn if not in PATH
    if [[ ":$PATH:" != *":$BIN_DIR:"* ]]; then
        echo "WARNING: $BIN_DIR is not in your PATH."
        echo "  Add this to your shell rc:  export PATH=\"\$HOME/.local/bin:\$PATH\""
    fi
fi

CLAUDE_COMMANDS="$HOME/.claude/commands"

# ── Uninstall ─────────────────────────────────────────────────────────────────

if $UNINSTALL; then
    for target in "$BIN_DIR/uo" "$CLAUDE_COMMANDS/uo.md"; do
        if [[ -L "$target" ]]; then
            rm "$target"
            echo "removed $target"
        elif [[ -e "$target" ]]; then
            echo "skipped $target (not a symlink — remove manually if needed)"
        fi
    done
    echo "Uninstall complete."
    exit 0
fi

# ── Install ───────────────────────────────────────────────────────────────────

# uo wrapper script
ln -sf "$REPO_DIR/uo" "$BIN_DIR/uo"
chmod +x "$REPO_DIR/uo"
echo "linked  $REPO_DIR/uo  →  $BIN_DIR/uo"

# Gemma skill for Claude Code
mkdir -p "$CLAUDE_COMMANDS"
ln -sf "$REPO_DIR/uo.md" "$CLAUDE_COMMANDS/uo.md"
echo "linked  $REPO_DIR/uo.md  →  $CLAUDE_COMMANDS/uo.md"

echo ""
echo "All done."
echo "  Test the wrapper:        uo help"
echo "  Start playing (Claude):  /uo"
echo "  Continuous play:         /loop 20s /uo"
