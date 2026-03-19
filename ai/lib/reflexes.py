"""Built-in always-on survival rules Claude can import into run.py.

Each rule is an async function with signature:
    async def rule_name(world: WorldState) -> bool

Return True if the rule fired (suppresses lower-priority rules).
Return False to pass control to the next rule.
"""

from __future__ import annotations

from lib.actions import cast, heal, meditate, move, warmode
from lib.models import WorldState

# Directions to flee south (cycle through to escape)
_FLEE_DIRS = ["south", "southeast", "southwest"]
_flee_idx = 0


async def survive(world: WorldState) -> bool:
    """
    PRIORITY: HIGHEST. Cast Heal if HP below 40%. Flee south if HP below 20%.
    Returns True if this rule fired (suppresses lower rules).
    """
    global _flee_idx
    hp = world.player.hp_pct

    if hp < 0.20:
        # Critical — heal AND flee
        await heal()
        dir_ = _FLEE_DIRS[_flee_idx % len(_FLEE_DIRS)]
        _flee_idx += 1
        await move(dir_, run=True)
        return True

    if hp < 0.40:
        await heal()
        return True

    return False


async def defend(world: WorldState) -> bool:
    """
    Cast Magic Arrow (spell 5) at nearest hostile within 3 tiles if HP above 60%.
    Does NOT fire if war mode is off and no target is in range.
    """
    if world.player.hp_pct < 0.60:
        return False

    hostile = world.nearest_hostile(within=3)
    if not hostile:
        return False

    await warmode(True)
    await cast(5)  # Magic Arrow
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
