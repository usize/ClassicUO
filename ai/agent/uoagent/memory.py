"""Long-term memory: one markdown file per slug + always-visible MEMORY.md index."""

from __future__ import annotations

import re
from pathlib import Path

_SLUG_RE = re.compile(r"[^a-z0-9-]+")


def slugify(raw: str) -> str:
    return _SLUG_RE.sub("-", raw.strip().lower()).strip("-") or "untitled"


def _files(memory_dir: Path) -> list[Path]:
    return sorted(p for p in memory_dir.glob("*.md") if p.name != "MEMORY.md")


def _rebuild_index(memory_dir: Path) -> None:
    lines = ["# Memory Index", ""]
    for p in _files(memory_dir):
        first = p.read_text(encoding="utf-8").strip().splitlines()
        desc = first[0].lstrip("# ").strip() if first else ""
        lines.append(f"- {p.stem} — {desc}")
    (memory_dir / "MEMORY.md").write_text("\n".join(lines) + "\n", encoding="utf-8")


def save(memory_dir: Path, slug: str, text: str) -> str:
    slug = slugify(slug)
    (memory_dir / f"{slug}.md").write_text(text.strip() + "\n", encoding="utf-8")
    _rebuild_index(memory_dir)
    return f"saved memory '{slug}'"


def read(memory_dir: Path, slug: str) -> tuple[str, str] | None:
    p = memory_dir / f"{slugify(slug)}.md"
    return (p.stem, p.read_text(encoding="utf-8").strip()) if p.exists() else None


def delete(memory_dir: Path, slug: str) -> str:
    p = memory_dir / f"{slugify(slug)}.md"
    if p.exists():
        p.unlink()
        _rebuild_index(memory_dir)
        return f"deleted memory '{p.stem}'"
    return f"no memory named '{slugify(slug)}'"


def index_text(memory_dir: Path) -> str:
    files = _files(memory_dir)
    if not files:
        return "(no memories yet — use MEM SAVE <slug>: <text>)"
    idx = memory_dir / "MEMORY.md"
    if not idx.exists():
        _rebuild_index(memory_dir)
    body = idx.read_text(encoding="utf-8").strip().splitlines()
    return "\n".join(l for l in body if l.startswith("- "))
