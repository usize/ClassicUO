#!/usr/bin/env python3
"""Meditate until mana is full (or Ctrl+C).

Run from repo root:  python3 ai/macros/meditate_full.py
"""

import asyncio
import sys

sys.path.insert(0, "ai")

from lib.actions import _get, meditate
from lib.engine import escalate


async def get_mp() -> tuple[int, int]:
    data = await _get("player")
    return data.get("mana", 0), data.get("manaMax", 1)


async def main() -> None:
    print("[meditate_full] Meditating until full mana. Ctrl+C to stop.")
    await escalate("macro_start", macro="meditate_full")

    while True:
        mp, mp_max = await get_mp()
        if mp >= mp_max:
            print(f"[meditate_full] Mana full ({mp}/{mp_max}). Done.")
            await escalate("macro_complete", macro="meditate_full", mp=mp, mp_max=mp_max)
            break
        print(f"[meditate_full] MP {mp}/{mp_max} — meditating...")
        await meditate()
        await asyncio.sleep(2.5)


if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        print("\n[meditate_full] Interrupted.")
