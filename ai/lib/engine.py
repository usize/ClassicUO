"""Async engine: poller, rule evaluator, hot-reloader, escalation queue."""

from __future__ import annotations

import asyncio
import fcntl
import importlib
import importlib.util
import json
import logging
import os
import sys
import time
from datetime import datetime, timezone
from pathlib import Path
from typing import Callable

import aiofiles
import aiohttp

from lib.models import Item, JournalEntry, Mobile, Player, WorldState

logger = logging.getLogger(__name__)

# Paths relative to ai/ directory (the cwd when runtime.py runs)
AI_DIR = Path(__file__).parent.parent
QUEUE_PATH = AI_DIR / "escalate.queue"
WORLD_PATH = AI_DIR / "state" / "world.json"
RUN_PY_PATH = AI_DIR / "run.py"

import os as _os
BASE_URL = f"http://{_os.environ.get('UO_HOST', 'host.docker.internal')}:{_os.environ.get('UO_PORT', '9000')}/api"

# Tick intervals
POLL_INTERVAL = 0.5       # seconds — world state refresh
RULE_INTERVAL = 0.1       # seconds — rule evaluator
HEARTBEAT_INTERVAL = 60   # seconds — unconditional escalation

# Escalation triggers
HP_CRITICAL_THRESHOLD = 0.25
PLAYER_SPEAK_LOOKBACK = 5  # seconds


# ── Escalation queue ──────────────────────────────────────────────────────────

LOCK_PATH = QUEUE_PATH.parent / "escalate.queue.lock"


async def escalate(reason: str, **context) -> None:
    """Append one JSONL event to the escalation queue (file-locked via .lock file)."""
    event = {"reason": reason, "ts": int(time.time()), **context}
    line = json.dumps(event) + "\n"
    QUEUE_PATH.parent.mkdir(parents=True, exist_ok=True)

    def _write():
        with open(LOCK_PATH, "a") as lock_f:
            fcntl.flock(lock_f, fcntl.LOCK_EX)
            try:
                with open(QUEUE_PATH, "a") as q_f:
                    q_f.write(line)
            finally:
                fcntl.flock(lock_f, fcntl.LOCK_UN)

    await asyncio.get_event_loop().run_in_executor(None, _write)
    logger.info("escalate: %s", event)


# ── World state poller ────────────────────────────────────────────────────────

_current_world: WorldState | None = None


async def _fetch_world() -> WorldState | None:
    try:
        async with aiohttp.ClientSession() as session:
            async with asyncio.timeout(3):
                player_resp, mobiles_resp, journal_resp = await asyncio.gather(
                    session.get(f"{BASE_URL}/player"),
                    session.get(f"{BASE_URL}/world/mobiles"),
                    session.get(f"{BASE_URL}/journal"),
                )
                player_data = await player_resp.json()
                mobiles_data = await mobiles_resp.json()
                journal_data = await journal_resp.json()

        player = Player.model_validate(player_data)
        mobiles = [Mobile.model_validate(m) for m in (mobiles_data if isinstance(mobiles_data, list) else [])]
        items_raw = mobiles_data if isinstance(mobiles_data, list) else []
        # Items come from a separate field if present; otherwise empty
        items: list[Item] = []
        journal = [JournalEntry.model_validate(j) for j in (journal_data if isinstance(journal_data, list) else [])]

        return WorldState(
            player=player,
            mobiles=mobiles,
            items=items,
            journal=journal,
            timestamp=datetime.now(timezone.utc),
        )
    except Exception as exc:
        logger.warning("poll failed: %s", exc)
        return None


async def poller_task() -> None:
    """Fetch world state every POLL_INTERVAL seconds, write to world.json."""
    global _current_world
    WORLD_PATH.parent.mkdir(parents=True, exist_ok=True)

    while True:
        world = await _fetch_world()
        if world is not None:
            _current_world = world
            try:
                async with aiofiles.open(WORLD_PATH, "w") as f:
                    await f.write(world.model_dump_json(indent=2))
            except Exception as exc:
                logger.warning("world.json write failed: %s", exc)
        await asyncio.sleep(POLL_INTERVAL)


# ── Rule hot-reloader ─────────────────────────────────────────────────────────

_run_py_mtime: float = 0.0
_rules: list[Callable] = []
_fallback_rules: list[Callable] | None = None


def _load_fallback_rules() -> list[Callable]:
    global _fallback_rules
    if _fallback_rules is None:
        from lib.reflexes import defend, survive
        _fallback_rules = [survive, defend]
    return _fallback_rules


def _reload_run_py() -> list[Callable] | None:
    """Import/reimport run.py, return RULES list or None on error."""
    spec = importlib.util.spec_from_file_location("run", RUN_PY_PATH)
    if spec is None or spec.loader is None:
        return None
    module = importlib.util.module_from_spec(spec)
    # Ensure lib/ is importable from run.py
    ai_dir_str = str(AI_DIR)
    if ai_dir_str not in sys.path:
        sys.path.insert(0, ai_dir_str)
    try:
        spec.loader.exec_module(module)  # type: ignore[union-attr]
        rules = getattr(module, "RULES", None)
        if not isinstance(rules, list):
            raise ValueError("run.py must define RULES as a list")
        return rules
    except Exception as exc:
        return exc  # type: ignore[return-value]  — caller checks type


async def hot_reloader_task() -> None:
    """Watch run.py mtime and reload when it changes."""
    global _run_py_mtime, _rules

    while True:
        try:
            mtime = RUN_PY_PATH.stat().st_mtime
            if mtime != _run_py_mtime:
                result = _reload_run_py()
                if isinstance(result, list):
                    _rules = result
                    _run_py_mtime = mtime
                    logger.info("run.py reloaded (%d rules)", len(_rules))
                elif isinstance(result, Exception):
                    err_str = f"{type(result).__name__}: {result}"
                    logger.error("run.py load error: %s", err_str)
                    await escalate("script_error", error=err_str)
                    _rules = _load_fallback_rules()
                    _run_py_mtime = mtime  # don't spam on same bad file
        except FileNotFoundError:
            pass
        except Exception as exc:
            logger.warning("hot_reloader error: %s", exc)

        await asyncio.sleep(0.5)


# ── Rule evaluator ────────────────────────────────────────────────────────────

_last_heartbeat: float = 0.0
_last_hp_pct: float = 1.0
_last_player_speak_check: float = 0.0


async def rule_evaluator_task() -> None:
    """Every RULE_INTERVAL: evaluate rules top-to-bottom, fire first match."""
    global _last_heartbeat, _last_hp_pct, _last_player_speak_check

    while True:
        await asyncio.sleep(RULE_INTERVAL)

        world = _current_world
        if world is None:
            continue

        # ── Escalation triggers ──────────────────────────────────────────────

        now = time.time()

        # Heartbeat
        if now - _last_heartbeat >= HEARTBEAT_INTERVAL:
            _last_heartbeat = now
            await escalate("heartbeat", tick=int(now))

        # HP critical
        hp = world.player.hp_pct
        if hp < HP_CRITICAL_THRESHOLD and _last_hp_pct >= HP_CRITICAL_THRESHOLD:
            await escalate("hp_critical", hp_pct=round(hp, 3))
        _last_hp_pct = hp

        # Player speaking in journal
        if now - _last_player_speak_check >= 1.0:
            _last_player_speak_check = now
            for entry in world.journal:
                # Heuristic: journal entries from human players have type "H" or non-NPC speakers
                if entry.speaker and entry.speaker != world.player.name:
                    try:
                        from datetime import datetime, timezone
                        ts = datetime.fromisoformat(entry.timestamp.replace("Z", "+00:00"))
                        if ts.tzinfo is None:
                            ts = ts.replace(tzinfo=timezone.utc)
                        age = (datetime.now(timezone.utc) - ts).total_seconds()
                        if age <= PLAYER_SPEAK_LOOKBACK:
                            await escalate(
                                "player_speaking",
                                speaker=entry.speaker,
                                message=entry.message,
                            )
                            break
                    except Exception:
                        continue

        # ── Run rules ────────────────────────────────────────────────────────

        rules = _rules if _rules else _load_fallback_rules()

        for rule in rules:
            try:
                fired = await rule(world)
                if fired:
                    break
            except Exception as exc:
                err_str = f"{type(exc).__name__} in {getattr(rule, '__name__', '?')}: {exc}"
                logger.error("rule error: %s", err_str)
                await escalate("script_error", error=err_str)
                _rules.clear()  # fall back next tick
                break
