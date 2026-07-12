"""World snapshots and HISTORY digests (DESIGN.md section 4)."""

from __future__ import annotations

import time

from .restapi import RestApi
from .state import AgentState, Snapshot


def capture(api: RestApi, state: AgentState) -> None:
    player = api.get_json("player")
    if not player:
        return
    stats = player.get("stats") or {}
    snap = Snapshot(
        ts=time.time(),
        x=player.get("x", 0),
        y=player.get("y", 0),
        z=player.get("z", 0),
        hits=stats.get("hits", 0),
        hits_max=stats.get("hitsMax", 0),
        mana=stats.get("mana", 0),
        mana_max=stats.get("manaMax", 0),
    )
    for mob in api.get_json("world/mobiles") or []:
        serial = f"0x{mob.get('serial', 0):08X}"
        snap.entities[serial] = {
            "name": mob.get("name") or serial,
            "dist": mob.get("distance", 0),
        }
    state.push_snapshot(snap)


def _ago(seconds: float) -> str:
    s = max(0, int(seconds))
    return f"-{s // 60}m{s % 60:02d}s" if s >= 60 else f"-{s}s"


def digest_line(prev: Snapshot, cur: Snapshot, now: float) -> str:
    parts = [
        f"{_ago(now - cur.ts)}  ({cur.x},{cur.y}) HP{cur.hits}/{cur.hits_max} MP{cur.mana}/{cur.mana_max}"
    ]
    changes = []
    appeared = [v["name"] for k, v in cur.entities.items() if k not in prev.entities]
    gone = [v["name"] for k, v in prev.entities.items() if k not in cur.entities]
    moved = [
        f"{v['name']} {prev.entities[k]['dist']}t→{v['dist']}t"
        for k, v in cur.entities.items()
        if k in prev.entities and abs(v["dist"] - prev.entities[k]["dist"]) >= 3
    ]
    if appeared:
        changes.append("appeared: " + ", ".join(appeared[:4]))
    if gone:
        changes.append("gone: " + ", ".join(gone[:4]))
    if moved:
        changes.append("moved: " + ", ".join(moved[:4]))
    if (cur.x, cur.y) == (prev.x, prev.y) and not changes:
        changes.append("no change")
    if changes:
        parts.append(" | ".join(changes))
    return " | ".join(parts)


def render_history(state: AgentState) -> str:
    """Digest lines for all but the newest snapshot (newest is rendered in NOW)."""
    snaps = state.snapshots
    if len(snaps) < 2:
        return "(no history yet)"
    now = time.time()
    lines = []
    for prev, cur in zip(snaps[:-1], snaps[1:]):
        lines.append(digest_line(prev, cur, now))
    return "\n".join(lines)
