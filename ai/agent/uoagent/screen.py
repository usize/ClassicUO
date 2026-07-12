"""Render the fixed-budget screen (the single user message). DESIGN.md section 1."""

from __future__ import annotations

import json
import time

from . import memory, rings, snapshots
from .config import Profile
from .restapi import RestApi
from .state import AgentState

DIVIDER = "─" * 76  # matches /api/summary's section divider


def est_tokens(text: str) -> int:
    return len(text) // 4


def _fit_lines_newest(lines: list[str], budget: int) -> list[str]:
    """Keep the newest lines that fit whole; never truncate a line mid-way."""
    kept: list[str] = []
    used = 0
    for line in reversed(lines):
        t = est_tokens(line) + 1
        if used + t > budget:
            break
        kept.append(line)
        used += t
    dropped = len(lines) - len(kept)
    kept.reverse()
    if dropped > 0:
        kept.insert(0, f"(… {dropped} older lines not shown)")
    return kept


def _numbered(items: list[str]) -> str:
    return "\n".join(f"{i + 1}. {t}" for i, t in enumerate(items)) if items else "(empty)"


def render_now(api: RestApi, budget: int) -> str:
    """Current world from /api/summary, journal section stripped, plus open gumps."""
    text = api.get_text("summary")
    if not text.strip():
        return "(world unavailable — REST API not responding)"
    sections = [s for s in text.split(DIVIDER) if s.strip()]
    # summary layout: status | player | map(+key) [| stairs] | entities | journal
    if len(sections) >= 2:
        sections = sections[:-1]  # drop journal — CHAT/EVENTS cover it, untruncated
    body = "\n".join(s.strip("\n") for s in sections)

    gumps = api.get_json("gumps") or []
    if gumps:
        body += "\n\nOPEN GUMPS (respond with: DO: gump <id> <button>):\n"
        body += json.dumps(gumps, ensure_ascii=False)[: budget * 2]

    # Over budget → drop entity-list rows from the end (they're distance-sorted)
    lines = body.splitlines()
    while est_tokens("\n".join(lines)) > budget and len(lines) > 25:
        lines.pop(len(lines) - 1 - (1 if gumps else 0))
    return "\n".join(lines)


def company_status(profile: Profile, state: AgentState, self_name: str) -> tuple[bool, str]:
    """(alone, human-readable line). Alone = no one else has spoken recently."""
    other = rings.last_other_speech(state, self_name)
    age = rings.entry_age_seconds(other) if other else None
    if other and age is not None and age <= profile.pacing.company_window:
        return False, (f"{other['name']} spoke {int(age)}s ago — you are in conversation; "
                       "keep them company.")
    if other and age is not None:
        return True, (f"no one has spoken to you in {int(age // 60)}m (last: {other['name']}). "
                      "You are ALONE. Do not greet or wait for anyone — pursue your GOALS.")
    return True, ("you are ALONE — no one has spoken to you. "
                  "Do not greet empty air or ask questions to nobody; pursue your GOALS.")


def render(api: RestApi, profile: Profile, state: AgentState, self_name: str = "") -> str:
    b = profile.budgets
    parts: list[str] = []

    def section(title: str, body: str) -> None:
        parts.append(f"== {title} ==\n{body.strip()}")

    if state.woken_by:
        section("WOKEN", state.woken_by)
    if state.engine_notice:
        section("NOTICE", state.engine_notice)

    _, company = company_status(profile, state, self_name)
    section("TIME / COMPANY", f"Now: {time.strftime('%H:%M:%S')}. {company}")

    goals = profile.read_doc("GOALS.md")
    if goals:
        section("GOALS", goals[: b["goals"] * 4])

    notes = _numbered(state.notes)
    if est_tokens(notes) > b["notes"]:
        notes += "\nNOTES FULL — delete stale notes with NOTE DEL <n>"
    section("NOTES", notes)
    section("TODO", _numbered(state.todo))
    section("MEMORY INDEX (MEM READ <slug> to recall)", memory.index_text(profile.memory_dir)[: b["memory_index"] * 4])

    if state.recalled:
        blocks = [f"### {slug}\n{body}" for slug, body in state.recalled]
        section("RECALLED (shown this turn only)", "\n\n".join(blocks)[: b["recalled"] * 4])

    section("HISTORY (recent world changes)", snapshots.render_history(state)[: b["history"] * 4])
    section("NOW", render_now(api, b["now"]))
    section("CHAT (newest last; '(you)' marks YOUR OWN past words — never answer them)",
            "\n".join(_fit_lines_newest([rings.fmt_entry(e, self_name) for e in state.chat], b["chat"])) or "(quiet)")
    section("EVENTS",
            "\n".join(_fit_lines_newest([rings.fmt_entry(e) for e in state.events], b["events"])) or "(none)")
    section("LAST TURN",
            "\n".join(_fit_lines_newest(state.last_turn, b["last_turn"])) or "(this is your first turn)")

    parts.append("What do you do? Respond using the protocol.")
    return "\n\n".join(parts)


def system_prompt(profile: Profile, protocol_spec: str, manual: str) -> str:
    personality = profile.read_doc("PERSONALITY.md") or "You are an adventurer in Britannia."
    return "\n\n".join([personality, protocol_spec, manual])
