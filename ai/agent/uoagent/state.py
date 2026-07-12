"""AgentState — everything that persists between turns (and restarts)."""

from __future__ import annotations

import json
import time
from dataclasses import dataclass, field
from pathlib import Path

MAX_RING = 200
MAX_SNAPSHOTS = 4  # current + 3 history


@dataclass
class Snapshot:
    ts: float
    x: int = 0
    y: int = 0
    z: int = 0
    hits: int = 0
    hits_max: int = 0
    mana: int = 0
    mana_max: int = 0
    entities: dict[str, dict] = field(default_factory=dict)  # serial-hex -> {name, dist}


@dataclass
class AgentState:
    state_dir: Path
    chat: list[dict] = field(default_factory=list)     # {ts, name, text}
    events: list[dict] = field(default_factory=list)   # {ts, name, text}
    notes: list[str] = field(default_factory=list)
    todo: list[str] = field(default_factory=list)
    snapshots: list[Snapshot] = field(default_factory=list)
    last_turn: list[str] = field(default_factory=list)  # rendered result lines
    recalled: list[tuple[str, str]] = field(default_factory=list)  # (slug, body), one turn
    woken_by: str = ""            # one-turn banner text
    engine_notice: str = ""       # one-turn nudge, e.g. "no valid commands"
    last_journal_ts: str = ""     # ISO timestamp high-water mark

    # ── rings ────────────────────────────────────────────────────────────

    def push_chat(self, entry: dict) -> None:
        self.chat.append(entry)
        del self.chat[:-MAX_RING]

    def push_event(self, entry: dict) -> None:
        self.events.append(entry)
        del self.events[:-MAX_RING]

    def push_snapshot(self, snap: Snapshot) -> None:
        self.snapshots.append(snap)
        del self.snapshots[:-MAX_SNAPSHOTS]

    # ── persistence ──────────────────────────────────────────────────────

    @property
    def _file(self) -> Path:
        return self.state_dir / "rings.json"

    def persist(self) -> None:
        data = {
            "chat": self.chat,
            "events": self.events,
            "notes": self.notes,
            "todo": self.todo,
            "last_turn": self.last_turn,
            "last_journal_ts": self.last_journal_ts,
            "snapshots": [vars(s) for s in self.snapshots],
        }
        self._file.write_text(json.dumps(data, ensure_ascii=False), encoding="utf-8")

    @classmethod
    def load(cls, state_dir: Path) -> "AgentState":
        st = cls(state_dir=state_dir)
        f = st._file
        if f.exists():
            try:
                data = json.loads(f.read_text(encoding="utf-8"))
                st.chat = data.get("chat", [])
                st.events = data.get("events", [])
                st.notes = data.get("notes", [])
                st.todo = data.get("todo", [])
                st.last_turn = data.get("last_turn", [])
                st.last_journal_ts = data.get("last_journal_ts", "")
                st.snapshots = [Snapshot(**s) for s in data.get("snapshots", [])]
            except (json.JSONDecodeError, OSError):
                pass
        return st

    def log_transcript(self, screen: str, reply: str, results: list[str]) -> None:
        line = json.dumps(
            {"ts": time.time(), "screen_chars": len(screen), "reply": reply, "results": results},
            ensure_ascii=False,
        )
        with (self.state_dir / "transcript.log").open("a", encoding="utf-8") as f:
            f.write(line + "\n")
