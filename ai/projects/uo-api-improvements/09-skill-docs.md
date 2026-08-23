# 09 — Update SKILL.md to match the new behavior

## Problem

`.agents/skills/uo/SKILL.md` documents the pre-improvement interface: `goto` as "A* routes
around obstacles / 30 s timeout / stuck-door retry", `journal [N]` as "last N journal entries"
(which was a lie before item 05), no `travel`, `resync`, or `ignoremobiles` commands, no
explanation of how to diagnose a frozen character, and no mention of the map radius option.
The knowledge base (`knowledge/`) was already added; SKILL.md references it.

## Changes (docs only — `.agents/skills/uo/SKILL.md` and the `ai/uo` help text if it drifts)

Rewrite the MOVE / Navigation / Key-Learnings sections to match what items 01–08 shipped:

1. **`goto`** — now: ≤40 tiles uses a single pathfind with an immediate `pathFound` answer;
   >40 tiles uses server-side segmented travel. Distinct failure modes:
   `✗ No path` (exit 1, fast), `✗ stuck` (exit 2, with door hint), timeout (exit 3).
   Remove the "Times out after 30s" and "Improved goto automatically opens stuck doors"
   mythology — replace with the real behavior (door retry happens on a 15/20 s stall;
   travel re-pathfinds automatically around moving obstacles).
2. **`travel <x> <y> [z]`** — new command; the recommended way to cross long distances.
   Progress is visible in `./ai/uo player | jq .travel` (`active`, `tilesRemaining`).
   `stopwalk` cancels travel.
3. **`resync`** — new command + a "When the character won't move" troubleshooting block:
   - check `./ai/uo player | jq '{isParalyzed, walker, travel}'`
   - `walker.walkingFailed` true or `stepsCount` stuck at 5 → `./ai/uo resync`, wait a few
     seconds, retry movement
   - `isParalyzed` true → you are frozen server-side; wait it out or resync
   - still frozen → restart the client (README instructions)
4. **`ignoremobiles <on|off>`** — pathfinding through standing monsters; default off; when to
   use (dense monster fields) and the tradeoff (server may deny a step through a moving
   monster; travel re-routes on the next segment).
5. **`journal [N]`** — now genuinely returns the *last* N entries (item 05).
6. **`summary [radius]`** — map radius option, default 20, max 50; recommend 30 when
   planning a route.
7. **Action acks** — `move`/`use`/`attack`/`say` now return JSON (`accepted`/`found`/`ok`);
   `use` on something out of reach or nonexistent says so instead of failing silently.
8. **Safety**: keep the existing safety rules; add "check `pathFound` before committing to a
   route — a `No path` result means the terrain blocks you, not a bug."
9. Keep the knowledge-base section and everything else (roleplay, game loop, shops, gumps,
   combat) as-is unless it contradicts the above.

Also: verify the `ai/uo help` text matches (items 01–08 added lines incrementally; fix any
inconsistencies — e.g. the help's "Pathfind to world coordinates (A* around obstacles)" line).

## Test

- No build needed.
- Run each command the docs describe once and confirm the doc's wording matches observed
  output/exit codes: `goto` (short + no-path case), `travel` (short), `resync`,
  `ignoremobiles on/off` (and leave it **off**), `journal 3`, `summary 30`, `move`/`use` acks.
- Read SKILL.md top to bottom once; it must contain no claim the implementation contradicts.

## Acceptance

- Every command documented exists and behaves as described; no stale claims remain
  (grep the file for "30s", "A* around", "last N" and sanity-check each hit).

## Commit

`docs(skill): document travel, resync, ignoremobiles, map radius, and action acks`
