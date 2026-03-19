#!/usr/bin/env python3
"""UO Python Reflex Runtime — entry point.

Usage:
    python ai/runtime.py

Starts three concurrent async tasks:
  - poller: fetches world state every 500ms, writes ai/state/world.json
  - hot_reloader: watches ai/run.py for changes, reimports on mtime change
  - rule_evaluator: evaluates rules every 100ms, fires first matching rule

Logs to ai/log/runtime.log and stderr.
Graceful shutdown on SIGINT (Ctrl-C) or SIGTERM.
"""

from __future__ import annotations

import asyncio
import logging
import signal
import sys
from pathlib import Path

# Ensure ai/ is on the path so `from lib.xxx import ...` works
AI_DIR = Path(__file__).parent
sys.path.insert(0, str(AI_DIR))

from lib.engine import hot_reloader_task, poller_task, rule_evaluator_task

# ── Logging ───────────────────────────────────────────────────────────────────

LOG_PATH = AI_DIR / "log" / "runtime.log"
LOG_PATH.parent.mkdir(parents=True, exist_ok=True)

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s %(levelname)-8s %(name)s: %(message)s",
    handlers=[
        logging.FileHandler(LOG_PATH),
        logging.StreamHandler(sys.stderr),
    ],
)
logger = logging.getLogger("runtime")


# ── Main ──────────────────────────────────────────────────────────────────────

async def main() -> None:
    logger.info("UO runtime starting — AI_DIR=%s", AI_DIR)

    stop_event = asyncio.Event()

    def _shutdown(sig: int, _frame: object) -> None:
        logger.info("received signal %d, shutting down", sig)
        stop_event.set()

    signal.signal(signal.SIGINT, _shutdown)
    signal.signal(signal.SIGTERM, _shutdown)

    tasks = [
        asyncio.create_task(poller_task(), name="poller"),
        asyncio.create_task(hot_reloader_task(), name="hot_reloader"),
        asyncio.create_task(rule_evaluator_task(), name="rule_evaluator"),
    ]

    logger.info("all tasks started")

    # Wait until shutdown signal
    await stop_event.wait()

    logger.info("cancelling tasks...")
    for task in tasks:
        task.cancel()

    await asyncio.gather(*tasks, return_exceptions=True)
    logger.info("runtime stopped")


if __name__ == "__main__":
    asyncio.run(main())
