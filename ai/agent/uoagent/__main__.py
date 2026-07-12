"""uoagent entry point.

    python3 -m uoagent --profile ai/agent/profiles/gemma [--once | --dry-run]
"""

from __future__ import annotations

import argparse

from .config import load_profile
from .loop import run


def main() -> None:
    ap = argparse.ArgumentParser(prog="uoagent", description="LLM agent for Ultima Online")
    ap.add_argument("--profile", required=True, help="path to a profile directory")
    ap.add_argument("--once", action="store_true", help="run a single turn and exit")
    ap.add_argument("--dry-run", action="store_true", help="print the rendered screen and exit (no LLM call)")
    args = ap.parse_args()

    profile = load_profile(args.profile)
    try:
        run(profile, once=args.once, dry_run=args.dry_run)
    except KeyboardInterrupt:
        print("\n[uoagent] stopped")


if __name__ == "__main__":
    main()
