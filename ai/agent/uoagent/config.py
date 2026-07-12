"""Profile loading. A profile is a directory; see DESIGN.md section 7."""

from __future__ import annotations

import os
import tomllib
from dataclasses import dataclass, field
from pathlib import Path

DEFAULT_BUDGETS = {
    "goals": 300,
    "notes": 600,
    "todo": 200,
    "memory_index": 300,
    "recalled": 800,
    "history": 600,
    "now": 2200,
    "chat": 1500,
    "events": 500,
    "last_turn": 600,
}


@dataclass
class LlmConfig:
    provider: str = "anthropic"  # "anthropic" | "openai"
    model: str = "claude-haiku-4-5-20251001"
    base_url: str = ""  # default chosen per provider
    api_key_env: str = "ANTHROPIC_API_KEY"
    max_tokens: int = 800
    temperature: float = 0.7


@dataclass
class Pacing:
    min_wait: float = 2.0
    max_wait: float = 120.0
    default_wait: float = 20.0
    alone_min_wait: float = 20.0   # floor while no one is speaking to the agent
    company_window: float = 180.0  # seconds since another's speech = "in conversation"
    wake_on_speech: bool = True


@dataclass
class TelemetryConfig:
    otlp_endpoint: str = ""        # e.g. "http://127.0.0.1:4318"; empty = disabled
    capture_content: bool = True   # include full prompt/completion in span events
    service_name: str = ""         # defaults to "uoagent-<profile dir name>"


@dataclass
class Profile:
    root: Path
    api_base: str = "http://127.0.0.1:9000"
    llm: LlmConfig = field(default_factory=LlmConfig)
    pacing: Pacing = field(default_factory=Pacing)
    telemetry: TelemetryConfig = field(default_factory=TelemetryConfig)
    budgets: dict[str, int] = field(default_factory=lambda: dict(DEFAULT_BUDGETS))

    @property
    def state_dir(self) -> Path:
        return self.root / "state"

    @property
    def memory_dir(self) -> Path:
        return self.root / "memory"

    def read_doc(self, name: str) -> str:
        p = self.root / name
        return p.read_text(encoding="utf-8").strip() if p.exists() else ""

    def api_key(self) -> str:
        key = os.environ.get(self.llm.api_key_env, "")
        if not key and self.llm.provider != "openai":
            raise SystemExit(f"missing API key: set ${self.llm.api_key_env}")
        return key


def manual_text(profile_root: Path) -> str:
    """Shared manual lives beside the profiles dir: ai/agent/manual/MANUAL.md."""
    p = profile_root.parent.parent / "manual" / "MANUAL.md"
    return p.read_text(encoding="utf-8").strip() if p.exists() else ""


def load_profile(path: str | Path) -> Profile:
    root = Path(path).resolve()
    cfg_path = root / "profile.toml"
    if not cfg_path.exists():
        raise SystemExit(f"no profile.toml in {root}")
    raw = tomllib.loads(cfg_path.read_text(encoding="utf-8"))

    llm = LlmConfig(**{k: v for k, v in raw.get("llm", {}).items()})
    pacing = Pacing(**{k: v for k, v in raw.get("pacing", {}).items()})
    telemetry = TelemetryConfig(**{k: v for k, v in raw.get("telemetry", {}).items()})
    if not telemetry.service_name:
        telemetry.service_name = f"uoagent-{root.name}"
    budgets = dict(DEFAULT_BUDGETS)
    budgets.update(raw.get("budget", {}))

    prof = Profile(
        root=root,
        api_base=raw.get("api", {}).get("base", "http://127.0.0.1:9000").rstrip("/"),
        llm=llm,
        pacing=pacing,
        telemetry=telemetry,
        budgets=budgets,
    )
    prof.state_dir.mkdir(exist_ok=True)
    prof.memory_dir.mkdir(exist_ok=True)
    return prof
