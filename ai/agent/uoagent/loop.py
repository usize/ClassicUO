"""The main loop + wake-on-speech (DESIGN.md section 3)."""

from __future__ import annotations

import json
import threading
import time
import urllib.request
from typing import Callable

from . import protocol, providers, rings, screen, snapshots
from .config import Profile, manual_text
from .restapi import RestApi
from .state import AgentState


class SpeechWaker(threading.Thread):
    """Holds open GET /api/events (SSE); sets `event` when someone else speaks."""

    def __init__(self, api_base: str, self_name: Callable[[], str]):
        super().__init__(daemon=True)
        self.api_base = api_base
        self.self_name = self_name
        self.event = threading.Event()
        self.last_speech = ""

    def run(self) -> None:
        url = f"{self.api_base}/api/events"
        while True:
            try:
                req = urllib.request.Request(url, headers={"Accept": "text/event-stream"})
                with urllib.request.urlopen(req, timeout=3600) as resp:
                    for raw in resp:
                        line = raw.decode("utf-8", "replace").strip()
                        if not line.startswith("data:"):
                            continue
                        self._consider(line[5:].strip())
            except OSError:
                time.sleep(5)

    def _consider(self, payload: str) -> None:
        try:
            data = json.loads(payload)
        except json.JSONDecodeError:
            return
        etype = str(data.get("type") or data.get("eventType") or "").lower()
        name = str(data.get("name") or data.get("data", {}).get("name") or "")
        text = str(data.get("text") or data.get("data", {}).get("text") or "")
        me = (self.self_name() or "").lower()
        if ("speech" in etype or "journal" in etype or "message" in etype) and text:
            if name and name.lower() not in ("system", me):
                self.last_speech = f'{name} said: "{text}"'
                self.event.set()


def run(profile: Profile, once: bool = False, dry_run: bool = False) -> None:
    api = RestApi(profile.api_base)
    state = AgentState.load(profile.state_dir)
    system = screen.system_prompt(profile, protocol.PROTOCOL_SPEC, manual_text(profile.root))
    api_key = profile.api_key() if not dry_run else ""

    player_name = ""

    def self_name() -> str:
        return player_name

    waker = None
    if profile.pacing.wake_on_speech and not (once or dry_run):
        waker = SpeechWaker(profile.api_base, self_name)
        waker.start()

    print(f"[uoagent] profile={profile.root.name} api={profile.api_base} "
          f"llm={profile.llm.provider}/{profile.llm.model}")
    print(f"[uoagent] system prompt ~{screen.est_tokens(system)} tokens")

    while True:
        # ── wait for the world ───────────────────────────────────────────
        while not api.in_game():
            print("[uoagent] not in game — retrying in 10s")
            if once or dry_run:
                return
            time.sleep(10)

        player = api.get_json("player") or {}
        player_name = player.get("name") or player_name

        # ── observe ──────────────────────────────────────────────────────
        rings.poll_journal(api, state)
        snapshots.capture(api, state)
        rendered = screen.render(api, profile, state)
        state.woken_by = ""
        state.engine_notice = ""
        state.recalled = []

        if dry_run:
            print(rendered)
            print(f"\n[uoagent] screen ~{screen.est_tokens(rendered)} tokens")
            return

        # ── decide ───────────────────────────────────────────────────────
        reply = providers.complete(profile.llm, api_key, system, rendered)
        cmds = protocol.parse(reply)
        actionable = [c for c in cmds if c.verb != "think"]
        if not actionable:
            state.engine_notice = (
                "Your last reply contained no commands. Use the protocol (SAY:, DO:, WAIT:)."
            )

        # ── act ──────────────────────────────────────────────────────────
        results, wait = protocol.execute(cmds, api, state, profile.memory_dir)
        state.last_turn = results or ["(no actions taken)"]
        state.persist()
        state.log_transcript(rendered, reply, results)

        for line in reply.splitlines():
            if line.strip():
                print(f"  {line.strip()[:110]}")
        for r in results:
            print(f"    → {r[:110]}")

        if once:
            return

        # ── pace ─────────────────────────────────────────────────────────
        p = profile.pacing
        delay = min(max(wait if wait is not None else p.default_wait, p.min_wait), p.max_wait)
        print(f"[uoagent] waiting {delay:.0f}s")
        _sleep_interruptible(delay, waker, state)


def _sleep_interruptible(delay: float, waker: SpeechWaker | None, state: AgentState) -> None:
    if waker is None:
        time.sleep(delay)
        return
    waker.event.clear()
    if waker.event.wait(timeout=delay):
        state.woken_by = waker.last_speech or "someone spoke nearby"
        print(f"[uoagent] woken: {state.woken_by}")
