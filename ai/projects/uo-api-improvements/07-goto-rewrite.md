# 07 — Rewrite `ai/uo goto` around travel; honest failure modes

## Problem

`goto` in `ai/uo` (lines ~79–211) is a 130-line bash loop: bc-based float distance math,
0.5 s position polling, a 1 s "stuck" threshold, a 30 s max timeout, and a door-retry
subroutine. In live play it:
- declared **stuck** for pathfinds that simply hadn't started (1 s is shorter than pathfind
  compute + first step on the game thread);
- declared **reached** at ≤1 tile while the client kept walking the rest of the path
  (observed: success reported at (1337,1996), character ended at (1336,1996));
- capped at 30 s total — meaningless for multi-hundred-tile routes, which it can't do anyway;
- couldn't distinguish "no path exists" from "blocked by a door" from "server slow".

Items 02 (pathfind returns `pathFound`) and 03 (`travel` with server-side segments +
`player.travel` progress) make all of that guesswork unnecessary. This item is **bash only** —
no C#.

## Design (rewrite the `goto)` case in `ai/uo`)

```
goto <x> <y> [z]
  read player (px, py, pz)
  tiles = max(|x-px|, |y-py|)          # integer math — drop the bc float code
  if z given: tiles = max(tiles, |z-pz|)

  if tiles <= 40:
      resp = POST actions/pathfind {x, y[, z]}
      if resp.pathFound != true:
          print "✗ No path to (x, y) — ${tiles} tiles away (terrain or obstacles)"
          exit 1
      wait for arrival (poll player every 0.5 s):
          arrived  = max(|x-px|, |y-py|) <= 1   (and z ok if given)
          stuck    = position unchanged for 15 s  → door retry (below), then fail
          timeout  = max(30, tiles * 1.5) s
  else:
      run the shared travel wait (same logic as the `travel` subcommand from item 03:
      POST actions/travel, poll player.travel every 1 s, stuck = 20 s no movement,
      timeout = max(45, tiles * 1.2 + 20) s)
```

Factor the travel wait into a bash function (e.g. `_wait_travel <x> <y> [z]`) shared by the
`travel` subcommand (item 03) and long-range `goto` — don't duplicate the polling loop.

**Door retry (keep the old intent, fix the parse)** on a stuck detection:
- Doors appear in the `summary` entity list as lines starting with two spaces + `+`:
  `  +  0xSERIAL  Door  item  3t  N  0 -3` (see SummaryController entity format:
  2-space indent, glyph, 2 spaces, `0xSERIAL`).
- `door=$(./ai/uo summary | grep -E '^  \+  0x' | head -1 | awk '{print $2}')`
- If found: `./ai/uo use "$door"`, wait 2 s, resume the stuck clock (max 2 door attempts).
- If movement resumes after a door: continue waiting normally.

**Output** (keep the established shapes — the LLM and humans read them):
```
Pathfinding to (x, y)...        # short branch
Traveling to (x, y) — N tiles   # long branch
✓ Reached destination (px, py, pz)
✗ No path to (x, y) — N tiles away (terrain or obstacles)
✗ Pathfinding stuck at (px, py, pz) — destination (x, y) is N tiles away
  Hint: ./ai/uo summary  (doors show as +; try opening one), or ./ai/uo stopwalk
```
Exit codes: 0 arrived, 1 no-path, 2 stuck, 3 timeout.

**Delete**: the bc distance/stuck machinery, `get_position`/`find_nearest_door`/`open_door`
as separate functions (fold into the new code), and the "retry pathfinding" re-POST on door
open (the server-side travel driver re-pathfinds automatically; for the short branch a single
re-POST is fine if you keep it).

**Unchanged**: `move`, `walk`, `follow`, `stopwalk` subcommands.

## Test (in-game; character is Gemma, Trammel)

1. Build not needed (bash only) — but confirm the API is the new build from items 02/03.
2. **Short success**: `./ai/uo goto` a ~15-tile open point from the map → `✓ Reached`, exit 0,
   character within 1 tile.
3. **Long success**: `./ai/uo goto` a ~120–150-tile point on flat ground (verify with
   `summary` before choosing) → `Traveling...` line, arrives unattended, exit 0.
4. **No path**: `./ai/uo goto` a point ≤40 tiles away that is clearly inside a wall/mountain
   mass on the map → `✗ No path`, exit 1, **fast** (<3 s), character unmoved.
5. **Timeout**: `./ai/uo goto` an unreachable point >40 tiles away (e.g. across the mountain)
   → exits 3 within `max(45, tiles*1.2+20)` s with a clean message; verify cuo-api is not
   spinning (CPU sane); `./ai/uo stopwalk` afterward.
6. **Stuck/door**: only testable if a closed door is within reach — if none, verify by code
   review and note it.
7. **Regression**: `./ai/uo move east`, `./ai/uo walk north 3`, `./ai/uo travel` still work.

## Acceptance

- All five failure/success modes produce distinct messages and exit codes; no bc dependency;
  the polling loop is shared between `goto` and `travel`.
- `goto` on an unreachable ≤40-tile target fails in <3 s (previously it burned 1–30 s).

## Commit

`Rewrite goto around travel with honest failure modes`
