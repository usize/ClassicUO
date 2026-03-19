"""Async wrappers over the ClassicUO REST API endpoints."""

from __future__ import annotations

import os

import aiohttp

BASE = f"http://{os.environ.get('UO_HOST', 'host.docker.internal')}:{os.environ.get('UO_PORT', '9000')}/api"

# aiohttp does not auto-read http_proxy env vars; pass it explicitly.
_PROXY: str | None = os.environ.get("http_proxy") or os.environ.get("HTTP_PROXY") or None


async def _post(path: str, body: dict) -> dict:
    async with aiohttp.ClientSession() as session:
        async with session.post(f"{BASE}/{path}", json=body, proxy=_PROXY) as resp:
            try:
                return await resp.json()
            except Exception:
                return {}


async def _get(path: str) -> dict:
    async with aiohttp.ClientSession() as session:
        async with session.get(f"{BASE}/{path}", proxy=_PROXY) as resp:
            try:
                return await resp.json()
            except Exception:
                return {}


async def move(direction: str, run: bool = False) -> None:
    await _post("actions/move", {"direction": direction, "run": run})


async def goto(x: int, y: int, z: int = 0) -> None:
    await _post("actions/pathfind", {"x": x, "y": y, "z": z})


async def follow(serial: int) -> None:
    await _post("actions/pathfind", {"serial": serial})


async def stopwalk() -> None:
    await _post("actions/stopwalk", {})


async def say(text: str) -> None:
    await _post("actions/say", {"text": text})


async def emote(text: str) -> None:
    await _post("actions/say", {"text": text, "type": "emote"})


async def use(serial: int) -> None:
    await _post("actions/use", {"serial": serial})


async def cast(spell: int) -> None:
    await _post("actions/spell", {"index": spell})


async def heal() -> None:
    await cast(4)


async def nightsight() -> None:
    await cast(6)


async def meditate() -> None:
    await _post("actions/skill", {"index": 46})


async def attack(serial: int) -> None:
    await _post("actions/attack", {"serial": serial})


async def warmode(on: bool) -> None:
    await _post("actions/warmode", {"enabled": on})


async def grab(serial: int, amount: int = 1) -> None:
    await _post("actions/grab", {"serial": serial, "amount": amount})


async def buy(serial: int, quantity: int = 1) -> None:
    await _post("shop/buy", {"items": [{"serial": serial, "quantity": quantity}]})


async def buy_from_vendor(
    vendor_serial: int,
    items: list[tuple[int, int]],
    *,
    approach_xy: tuple[int, int] | None = None,
    open_timeout: float = 15.0,
) -> bool:
    """
    Full buy flow in one call:
      1. If approach_xy given, smart_goto there first (outside the building).
      2. smart_follow the vendor to within 2 tiles.
      3. Spam "vendor buy" up to 6 times (1s apart) while re-following if the
         vendor wanders — do NOT double-click (that opens the paperdoll, not shop).
      4. Wait for shop gump to open between each attempt.
      5. Send ONE batch buy request with all (serial, quantity) pairs.

    Returns True if items were purchased, False if shop never opened.
    Example:
        await buy_from_vendor(476, [(pearl_serial, 20), (moss_serial, 20)],
                              approach_xy=(4421, 1113))
    """
    import asyncio
    import time
    from lib.nav import smart_follow, smart_goto

    if approach_xy:
        await smart_goto(approach_xy[0], approach_xy[1], timeout=60.0)

    # Follow vendor close enough to interact
    await smart_follow(vendor_serial, stop_distance=2, timeout=30.0)

    # Spam "vendor buy" while re-following if vendor wanders
    deadline = time.time() + open_timeout
    attempts = 0
    while time.time() < deadline:
        # Re-follow if we've drifted or vendor moved
        await smart_follow(vendor_serial, stop_distance=2, timeout=10.0)

        await say("vendor buy")
        attempts += 1

        # Give server 1.5s to respond with gump
        for _ in range(3):
            await asyncio.sleep(0.5)
            shop = await _get("shop")
            if shop.get("isOpen"):
                # Single batch purchase
                payload = {"items": [{"serial": s, "quantity": q} for s, q in items]}
                await _post("shop/buy", payload)
                return True

        if attempts >= 6:
            break

    return False
