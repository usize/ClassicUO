"""Journal polling: route entries into chat vs events rings, drop noise."""

from __future__ import annotations

import re

from .restapi import RestApi
from .state import AgentState

# Speech-like message types (see JournalEntryDto.messageType from the REST API).
CHAT_TYPES = {"regular", "yell", "whisper", "emote", "party", "guild", "alliance"}

NOISE_PATTERNS = [
    re.compile(r"world is saving", re.I),
    re.compile(r"world save completed", re.I),
]


def is_noise(text: str) -> bool:
    return any(p.search(text) for p in NOISE_PATTERNS)


def poll_journal(api: RestApi, state: AgentState) -> None:
    entries = api.journal_since(state.last_journal_ts or None)
    for e in entries:
        ts = e.get("timestamp", "")
        # High-water mark; skip exact duplicates of the boundary timestamp
        if state.last_journal_ts and ts <= state.last_journal_ts:
            continue
        text = (e.get("text") or "").strip()
        if not text or is_noise(text):
            if ts > state.last_journal_ts:
                state.last_journal_ts = ts
            continue
        name = (e.get("name") or "").strip()
        mtype = (e.get("messageType") or "").lower()
        entry = {"ts": ts, "name": name, "text": text}
        if name and name.lower() != "system" and mtype in CHAT_TYPES:
            state.push_chat(entry)
        else:
            state.push_event(entry)
        if ts > state.last_journal_ts:
            state.last_journal_ts = ts


def fmt_entry(e: dict, self_name: str = "") -> str:
    hhmmss = e["ts"][11:19] if len(e.get("ts", "")) >= 19 else "--:--:--"
    name = e.get("name") or ""
    if self_name and name.lower() == self_name.lower():
        name += " (you)"
    who = f"[{name}] " if name else ""
    return f"{hhmmss} {who}{e['text']}"


def last_other_speech(state: AgentState, self_name: str) -> dict | None:
    """Newest chat entry spoken by someone other than the agent, or None."""
    me = self_name.lower()
    for e in reversed(state.chat):
        if (e.get("name") or "").lower() not in ("", "system", me):
            return e
    return None


def entry_age_seconds(e: dict) -> float | None:
    from datetime import datetime

    try:
        ts = datetime.fromisoformat(e["ts"])
        return (datetime.now(ts.tzinfo) - ts).total_seconds()
    except (ValueError, KeyError):
        return None
