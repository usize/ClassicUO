"""Pydantic models mirroring the ClassicUO REST API DTOs."""

from __future__ import annotations

from datetime import datetime, timezone

from pydantic import BaseModel


class JournalEntry(BaseModel):
    timestamp: str
    speaker: str
    message: str


class Mobile(BaseModel):
    serial: int
    name: str
    type: str          # "creature" | "NPC" | "player"
    distance: int
    dx: int
    dy: int
    notoriety: str     # "Innocent" | "Gray" | "Criminal" | "Murderer" | "Invulnerable"
    hp_pct: float


class Item(BaseModel):
    serial: int
    name: str
    type: str          # "weapon" | "reagent" | "container" | "door" | "item" etc.
    distance: int
    dx: int
    dy: int


class Player(BaseModel):
    serial: int
    name: str
    x: int
    y: int
    z: int
    hp: int
    hp_max: int
    mp: int
    mp_max: int
    stamina: int
    stamina_max: int
    gold: int
    weight: int
    weight_max: int
    skills: dict[str, float]

    @property
    def hp_pct(self) -> float:
        return self.hp / self.hp_max if self.hp_max else 0.0


class WorldState(BaseModel):
    player: Player
    mobiles: list[Mobile]
    items: list[Item]
    journal: list[JournalEntry]
    timestamp: datetime

    def nearest_hostile(self, within: int = 999) -> Mobile | None:
        hostile = [
            m for m in self.mobiles
            if m.notoriety in ("Criminal", "Murderer", "Gray")
            and m.distance <= within
        ]
        return min(hostile, key=lambda m: m.distance) if hostile else None

    def nearest_npc(self, within: int = 999) -> Mobile | None:
        npcs = [
            m for m in self.mobiles
            if m.type == "NPC" and m.distance <= within
        ]
        return min(npcs, key=lambda m: m.distance) if npcs else None

    def nearby_player(self, within: int = 10) -> Mobile | None:
        players = [
            m for m in self.mobiles
            if m.type == "player" and m.distance <= within
        ]
        return min(players, key=lambda m: m.distance) if players else None

    def near(self, x: int, y: int, radius: int = 2) -> bool:
        return (
            abs(self.player.x - x) <= radius
            and abs(self.player.y - y) <= radius
        )

    def journal_contains(self, text: str, within_seconds: int = 10) -> bool:
        now = datetime.now(timezone.utc)
        for entry in self.journal:
            try:
                ts = datetime.fromisoformat(entry.timestamp.replace("Z", "+00:00"))
                if ts.tzinfo is None:
                    ts = ts.replace(tzinfo=timezone.utc)
                age = (now - ts).total_seconds()
                if age <= within_seconds and text.lower() in entry.message.lower():
                    return True
            except Exception:
                continue
        return False
