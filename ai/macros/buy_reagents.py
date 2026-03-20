#!/usr/bin/env python3
"""Buy a standard stock of reagents from Sancia at the Haven reagent shop.

Navigates to the approach tile, follows Sancia, says 'vendor buy', and purchases
the reagent list defined in KNOWN_SERIALS below.

Update KNOWN_SERIALS after a successful manual buy (serials visible in ./ai/uo shop).

Run from repo root:  python3 ai/macros/buy_reagents.py
"""

import asyncio
import sys

sys.path.insert(0, "ai")

from lib.actions import buy_from_vendor, emote, say
from lib.engine import escalate
from lib.nav import player_pos, smart_goto

# Sancia the alchemist — Haven reagent shop
SANCIA_SERIAL = 476
APPROACH_XY = (4421, 1113)  # safe tile just outside the shop entrance

# (item_serial, quantity) — update after first successful buy
# Leave empty to open the shop manually and buy by hand
KNOWN_SERIALS: list[tuple[int, int]] = []


async def main() -> None:
    x, y, _ = await player_pos()
    print(f"[buy_reagents] Starting at ({x}, {y})")
    await escalate("macro_start", macro="buy_reagents", pos=(x, y))

    print("[buy_reagents] Navigating to reagent shop approach tile...")
    ok = await smart_goto(APPROACH_XY[0], APPROACH_XY[1], timeout=120.0, run=True)
    if not ok:
        await escalate("macro_blocked", macro="buy_reagents", reason="could_not_reach_approach")
        print("[buy_reagents] Could not reach approach tile — aborting")
        return

    await emote("steps into the reagent shop, scanning the shelves")

    print(f"[buy_reagents] Opening shop from Sancia (serial {SANCIA_SERIAL})...")
    ok = await buy_from_vendor(
        SANCIA_SERIAL,
        KNOWN_SERIALS,
        approach_xy=None,  # already there
        open_timeout=20.0,
    )

    if ok:
        await escalate("macro_complete", macro="buy_reagents", result="purchased")
        print("[buy_reagents] Purchase complete.")
        await say("Thank you, Sancia.")
    else:
        await escalate("macro_blocked", macro="buy_reagents", reason="shop_never_opened")
        print("[buy_reagents] Shop did not open — may need to update serial or approach.")


if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        print("\n[buy_reagents] Interrupted.")
