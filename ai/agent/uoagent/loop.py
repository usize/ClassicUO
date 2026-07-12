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
from .telemetry import Telemetry


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
    telemetry = Telemetry(
        profile.telemetry.otlp_endpoint,
        profile.telemetry.service_name,
        profile.telemetry.capture_content,
    )
    if telemetry.enabled:
        print(f"[uoagent] telemetry → {profile.telemetry.otlp_endpoint}/v1/traces")

    player_name = ""
    turn_number = 0

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
        turn_number += 1
        turn_start = time.time()
        rings.poll_journal(api, state)
        snapshots.capture(api, state)
        rendered = screen.render(api, profile, state, player_name)
        state.woken_by = ""
        state.engine_notice = ""
        state.recalled = []

        if dry_run:
            print(rendered)
            print(f"\n[uoagent] screen ~{screen.est_tokens(rendered)} tokens")
            return

        # ── decide ───────────────────────────────────────────────────────
        llm_start = time.time()
        reply, usage = providers.complete(profile.llm, api_key, system, rendered)
        llm_end = time.time()
        cmds = protocol.parse(reply)
        actionable = [c for c in cmds if c.verb != "think"]
        if not actionable:
            state.engine_notice = (
                "Your last reply contained no commands. Use the protocol (SAY:, DO:, WAIT:)."
            )

        # ── act ──────────────────────────────────────────────────────────
        pre_ts = state.last_journal_ts
        results, wait = protocol.execute(cmds, api, state, profile.memory_dir)

        # aftermath: give the actions a moment to land, then surface what they
        # caused (spell fizzles, skill gains, NPC replies) right in LAST TURN
        if any(c.verb in ("do", "say", "emote", "yell", "whisper") for c in cmds):
            time.sleep(1.5)
            rings.poll_journal(api, state)
            fresh = sorted(
                rings.fmt_entry(e) for e in (state.chat + state.events)
                if e.get("ts", "") > pre_ts
            )
            if fresh:
                results += ["— what happened next:"] + fresh

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
        alone, _ = screen.company_status(profile, state, player_name)
        if alone:
            # no one to talk to → don't let short waits burn tokens into the void
            delay = max(delay, p.alone_min_wait)
            snaps = state.snapshots
            if len(snaps) >= 4 and len({(s.x, s.y) for s in snaps[-4:]}) == 1:
                state.engine_notice = (
                    "You have been standing in the same spot for several turns. "
                    "Pick a TODO and act on it, or explore somewhere new — do not idle."
                )

        telemetry.emit_turn({
            "turn_start": turn_start, "llm_start": llm_start,
            "llm_end": llm_end, "turn_end": time.time(),
            "provider": profile.llm.provider, "model": profile.llm.model,
            "system": system, "screen": rendered, "reply": reply,
            "usage": usage, "results": results, "wait": delay,
            "profile": profile.root.name, "turn_number": turn_number,
        })

        print(f"[uoagent] waiting {delay:.0f}s{' (alone)' if alone else ''}")
        _sleep_interruptible(delay, waker, state)


def _sleep_interruptible(delay: float, waker: SpeechWaker | None, state: AgentState) -> None:
    if waker is None:
        time.sleep(delay)
        return
    waker.event.clear()
    if waker.event.wait(timeout=delay):
        state.woken_by = waker.last_speech or "someone spoke nearby"
        print(f"[uoagent] woken: {state.woken_by}")
