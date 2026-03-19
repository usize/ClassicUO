"""Built-in always-on survival rules Claude can import into run.py.

Each rule is an async function with signature:
    async def rule_name(world: WorldState) -> bool

Return True if the rule fired (suppresses lower-priority rules).
Return False to pass control to the next rule.
"""

from __future__ import annotations

from lib.actions import cast, cast_at, heal, meditate, move, target, warmode
from lib.models import WorldState

# Directions to flee south (cycle through to escape)
_FLEE_DIRS = ["south", "southeast", "southwest"]
_flee_idx = 0


async def survive(world: WorldState) -> bool:
    """
    PRIORITY: HIGHEST. Cast Heal (self-targeted) if HP below 40%.
    Flee south AND heal if HP below 20%.
    Returns True if this rule fired (suppresses lower rules).
    """
    global _flee_idx
    hp = world.player.hp_pct

    if hp < 0.20:
        # Critical — heal AND flee
        await cast_at(4, world.player.serial)  # Heal, targeted at self
        dir_ = _FLEE_DIRS[_flee_idx % len(_FLEE_DIRS)]
        _flee_idx += 1
        await move(dir_, run=True)
        return True

    if hp < 0.40:
        await cast_at(4, world.player.serial)  # Heal, targeted at self
        return True

    return False


async def defend(world: WorldState) -> bool:
    """
    Cast Magic Arrow (spell 5) at nearest hostile within 8 tiles if HP above 60%.
    Targets the hostile via the targeting cursor after casting.
    """
    if world.player.hp_pct < 0.60:
        return False

    hostile = world.nearest_hostile(within=8)
    if not hostile:
        return False

    await warmode(True)
    await cast_at(5, hostile.serial)  # Magic Arrow at hostile
    return True


async def recover_mana(world: WorldState) -> bool:
    """
    Meditate if MP below 30% and no hostile nearby.
    """
    mp_pct = world.player.mp / world.player.mp_max if world.player.mp_max else 1.0
    if mp_pct >= 0.30:
        return False

    if world.nearest_hostile(within=10):
        return False

    await meditate()
    return True
