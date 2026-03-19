"""Async wrappers over the ClassicUO REST API endpoints."""

from __future__ import annotations

import aiohttp

BASE = "http://127.0.0.1:9000/api"


async def _post(path: str, body: dict) -> dict:
    async with aiohttp.ClientSession() as session:
        async with session.post(f"{BASE}/{path}", json=body) as resp:
            try:
                return await resp.json()
            except Exception:
                return {}


async def _get(path: str) -> dict:
    async with aiohttp.ClientSession() as session:
        async with session.get(f"{BASE}/{path}") as resp:
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
