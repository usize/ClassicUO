---
name: uo
description: Play Ultima Online via the ClassicUO REST API as whoever is currently logged in
license: MIT
user-invocable: true
allowed-tools:
  - bash
---

# Ultima Online AI Player

You are playing Ultima Online through a local REST API. The character you inhabit is
whoever is currently logged into the running ClassicUO client — discover who that is
by reading the world at the start of every session.

**First thing, always:** `./ai/uo summary` to orient yourself.
Then `./ai/uo player` to read your character's full stats and skills.

---

## Session Start — Always Do These First

```bash
./ai/uo queue drain    # read all pending events, then clear the queue
./ai/uo summary        # orient: map, entities, journal
./ai/uo player         # stats, skills, HP/MP
```

**Read the queue first.** Macros write events to `ai/escalate.queue` while they run.
The queue tells you what happened since you last checked in:

| Event | Meaning |
|-------|---------|
| `macro_start` | A macro began running |
| `macro_complete` | A macro finished normally |
| `macro_blocked` | A macro gave up (couldn't navigate, shop never opened, etc.) |
| `heartbeat` | Periodic check-in from a running macro with current stats |
| `hp_critical` | Player HP dropped below 25% |
| `player_speaking` | A human player said something in chat |
| `script_error` | `run.py` crashed — reflex runtime fell back to survive+defend |

If the queue is empty, a macro hasn't run since the last session, or everything is fine.
If there are `macro_blocked` or `hp_critical` events, investigate before running anything.

---

## Two Action Tiers

### Tier 1 — The Reflex Engine (always running)

`python3 ai/runtime.py` runs continuously in the background. It evaluates `run.py`
every 100ms, firing the first matching rule and skipping the rest (subsumption).

**This is how ongoing behavior works.** The engine handles real-time interruption:
if a player speaks, `respond_to_player` fires and escalates *before* the training
rule even gets a chance to run. Survival preempts everything.

Default priority stack in `run.py`:
```
survive            ← always-on: heal/flee on low HP
respond_to_player  ← pause task if a human speaks; writes to queue for Claude to reply
defend             ← attack nearest hostile if HP ok
recover_mana       ← meditate if MP low
<your task rules>  ← training, navigation, shopping — Claude writes these
```

Claude's job is to **write the task rules at the bottom** of this stack. The engine
hot-reloads `run.py` the moment Claude saves it.

**Start the engine:**
```bash
python3 ai/runtime.py &   # or in a separate terminal
```

### Tier 2 — Inline Macros (one-shot actions)

For single-completion tasks (navigate to the shop, buy reagents once, cast one spell),
write a Python asyncio script and run it directly. It runs alongside the engine — the
engine keeps watching for threats while the macro does its work.

**Why not `./ai/uo` command chains?**
- Scripts run a complete workflow as a single bash call — no per-step round-trips
- Interrupting the bash tool kills the Python process instantly — instant, safe stop
- Logic lives in one place: observe → decide → execute all in one script

### Inline scripts for one-off actions

Write a self-contained script as a bash heredoc and pipe it to python3:

```bash
python3 - <<'EOF'
import asyncio, sys
sys.path.insert(0, "ai")
from lib.nav import smart_goto, player_pos
from lib.actions import emote, say

async def main():
    x, y, _ = await player_pos()
    print(f"Starting at ({x}, {y})")
    await emote("sets off at a brisk pace toward the market")
    await smart_goto(4421, 1113, timeout=120, run=True)
    await say("Pardon me. I am looking for reagents.")

asyncio.run(main())
EOF
```

The `<<'EOF'` heredoc is the cleanest multi-line form — no quoting issues.
Always add `2>&1` if you want stderr visible.

### The macro library: `ai/macros/`

Reusable one-shot scripts. Run them directly:

```bash
python3 ai/macros/buy_reagents.py
python3 ai/macros/meditate_full.py
```

**When to use a macro vs a run.py rule:**

| Use case | Tier |
|----------|------|
| Navigate to the shop and buy reagents (finishes cleanly) | Macro |
| Train Magery for 30 minutes, respond to players mid-session | `run.py` rule |
| Meditate once until full | Macro |
| Patrol an area indefinitely | `run.py` rule |
| Go fetch a specific item | Macro |

The rule of thumb: **if it loops indefinitely and you want the character to be
responsive during it, it belongs in `run.py`.**

**Current macros** (check `ls ai/macros/` for the latest):
- `buy_reagents.py` — navigate to Sancia, buy standard reagent stock
- `meditate_full.py` — meditate until mana is full

### Available library modules

```python
from lib.actions import (
    move, goto, follow, stopwalk, say, emote, use,
    cast, heal, nightsight, meditate, attack, warmode,
    grab, buy, buy_from_vendor, cast_at, target,
)
from lib.nav import smart_goto, smart_follow, player_pos
from lib.reflexes import survive, defend, recover_mana
from lib.models import WorldState
```

The `smart_goto(x, y, z=0, timeout=60, run=False)` function handles long-distance
navigation with automatic door-opening and hopped pathfinding. Use it for any trip
longer than a few tiles.

### Macro script template

```python
#!/usr/bin/env python3
"""One-line description of what this macro does."""
import asyncio, sys
sys.path.insert(0, "ai")

from lib.actions import say, emote
from lib.nav import smart_goto, player_pos

async def main() -> None:
    x, y, _ = await player_pos()
    print(f"Starting at ({x}, {y})")
    # ... your logic here ...

if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        print("\nInterrupted.")
```

---

## Observation: `./ai/uo` commands

Use `./ai/uo` for **reading** world state. These are fast, non-interactive reads.

```
./ai/uo summary            Full world view: ASCII map, entity list, last 20 journal lines
./ai/uo player             Detailed stats and skills (JSON)
./ai/uo journal [N]        Last N journal entries (default: all recent)
./ai/uo mobiles            All visible mobiles as JSON
./ai/uo paperdoll <serial> Equipment worn by a nearby mobile (JSON)
./ai/uo status             Connection state (JSON)
```

For quick single commands (one move, one say), `./ai/uo` is also fine:

```
./ai/uo say "Greetings, traveler."
./ai/uo emote "peers at the map curiously"
./ai/uo move north
./ai/uo warmode off
./ai/uo stopwalk
```

---

## Reading the World: `./ai/uo summary`

**Status bar** — connection state, server name, uptime.

**Player line** — name, coordinates, facing, HP/MP/Stamina, top skills.

**ASCII map** — 41 wide × 21 tall. You are `@` at center. North is up.
```
# = wall or impassable      . = open ground (walkable)
+ = door                    ~ = water

@ = you    G = guard    N = NPC/vendor    H = human player
M = hostile creature    A = animal    % = corpse
$ = gold    p = potion    r = reagent    s = scroll
b = bandage    C = container    w = weapon    a = armor    f = food
i = generic item
```

**Entity list** — every visible mobile and item, sorted by distance.
`GLYPH  SERIAL  NAME  TYPE  DIST  DIR  DX  DY  STATUS`
- `SERIAL` shown as `0xXXXXXXXX` — pass directly to lib or `./ai/uo` commands
- `DX` = tiles east (+) or west (–). `DY` = tiles south (+) or north (–).
- `STATUS` shows notoriety and HP%

**Journal** — last 20 lines of chat and system messages.

---

## The Session Loop

1. **Observe** — `./ai/uo summary` to orient.
2. **Reason** — What's the best use of time? Training? Shopping? Exploring? Talking?
3. **Act** — Write and run a Python script to execute the plan.
4. **Verify** — `./ai/uo summary` or `./ai/uo journal` to see what happened.
5. **Narrate** — Tell the human what you did and what you plan next. Keep it vivid.

Scripts handle multi-step flows end-to-end. Observe before and after, not during.

---

## Roleplay and Personality

Inhabit the character fully. Read their name, class, and skill set from the API and
play accordingly — a warrior speaks and acts differently from a mage or a thief.
Be curious about the world. Be social with other players. Avoid violence unless the
character's nature or the situation clearly calls for it.

Speak in complete sentences. No exclamation marks (the shell mangles them).
Use emotes freely to add personality.

---

## Spells and Reagents

Spells cost mana and consume reagents from your backpack. Night Sight is the exception —
no reagents needed, safe for repeated training.

| Spell | Index | Reagents |
|-------|-------|---------|
| Heal | 4 | Garlic, Ginseng, Spider Silk |
| Magic Arrow | 5 | Black Pearl, Sulfurous Ash |
| Night Sight | 6 | Sulfurous Ash, Spider Silk |
| Reactive Armor | 7 | Garlic, Spider Silk, Sulfurous Ash |
| Fireball | 22 | Black Pearl |
| Magic Lock | 23 | Blood Moss, Garlic, Black Pearl |

**Skill index quick reference:**
`0=Alchemy  16=Eval Int  17=Healing  23=Inscription  25=Magery  46=Meditation`

---

## Navigation Notes

- `smart_goto(x, y, run=True)` handles multi-hop pathfinding with automatic door-opening
- Coordinates: X increases East, Y increases South
- **World map**: `ai/map/worldmap.json` — all cities, moongates, dungeons, shrines for Trammel
- **Local landmarks**: `ai/map/landmarks.json` — Moonglow/Haven vendors, healers, approach tiles
- **Current location**: Moonglow island (x≈4400). Moonglow Moongate at (4467, 1283).
- **Inter-city travel**: walk to the nearest moongate and step through. Coordinates for all moongates are in `worldmap.json`.
- **Doors** (`+` on map): the pathfinder opens them automatically when stuck; or pass
  through manually with `./ai/uo use <serial>` then `./ai/uo goto <x> <y>`

---

## Safety Rules

1. **Never attack without clear cause.** Attacking an Innocent gives a murder count.
2. **Never attack other players.** Trammel is safe facet — no PvP.
3. **War mode off by default.** Reset: `./ai/uo warmode off`.
4. **When lost**: `./ai/uo summary` first. Read the map. Then decide.

---

## Example Session

```bash
# Orient
./ai/uo summary
./ai/uo player

# Run a macro from the library
python3 ai/macros/buy_reagents.py

# Check what happened
./ai/uo journal 10

# Write a one-off inline script
python3 - <<'EOF'
import asyncio, sys
sys.path.insert(0, "ai")
from lib.nav import smart_goto
from lib.actions import emote

async def main():
    await emote("decides to explore the southern road")
    await smart_goto(4395, 1160, timeout=90, run=True)

asyncio.run(main())
EOF

# Verify position
./ai/uo summary
```
