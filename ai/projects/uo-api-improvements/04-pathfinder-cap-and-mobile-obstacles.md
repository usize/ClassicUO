# 04 — Pathfinder: O(1) node bookkeeping, higher cap, mobile-obstacle toggle

## Problem

Two things make the built-in A* unreliable for bot navigation (both in
`src/ClassicUO.Client/Game/Pathfinder.cs`):

1. **Node budget is small and bookkeeping is O(n) per node.** `PATHFINDER_MAX_NODES = 10000`
   (line 17). `FindPath` gives up with *no path* when 10,000 nodes are closed (line ~921).
   The duplicate/slot scans are linear over the whole pool: `DoesNotExistOnOpenList`
   (line ~642), `DoesNotExistOnClosedList` (line ~656), the "find existing open node" scan in
   `AddNodeToList` (line ~720), and the free-slot scan for closed nodes (line ~752). In open
   terrain 10k nodes covers ~50-tile Chebyshev radius; with walls/monsters it covers far less
   — live testing saw 25-tile hops succeed and 50-tile hops fail.

2. **Living mobiles are impassable obstacles** (lines 65, 140–160): every monster in a tile
   column blocks the search unless `ignoreGameCharacters` is set — which for players depends on
   `ProfileManager.CurrentProfile.IgnoreStaminaCheck` or the server-set `IgnoreCharacters` flag
   (`Mobile.cs:123`, getter-only — derived from the server's IgnoreMobiles flag, not settable
   client-side). A monster-dense field burns the node budget and can block routes entirely.
   For an AI player this is the wrong default; it should be opt-in per session.

## Changes

1. **O(1) duplicate/slot bookkeeping** in `Pathfinder`:
   - Add `private readonly HashSet<(int,int,int)> _openKeys = new();` and
     `private readonly Dictionary<(int,int,int), int> _openIndex = new();` (key→pool index),
     plus `private readonly HashSet<(int,int,int)> _closedKeys = new();`.
   - Clear all three in `WalkTo` where the pools are Reset (line ~947).
   - `AddNodeToList(0, ...)`: replace `DoesNotExistOnClosedList` → `_closedKeys.Contains(key)`;
     replace `DoesNotExistOnOpenList` → `_openKeys.Contains(key)`; replace the O(n)
     "find existing open node" scan (line ~720) → `_openIndex.TryGetValue(key, out i)`;
     maintain the set/dictionary when a node is added or updated.
   - `AddNodeToList(1, ...)` (move open→closed): remove from `_openKeys`/`_openIndex`, add to
     `_closedKeys`. (The free-slot scan for the closed pool can stay linear — it's amortized —
     or use a `Stack<int>` of free closed indices if you like.)
   - Delete `DoesNotExistOnOpenList`/`DoesNotExistOnClosedList` if unused after the change.
   - `FindCheapestNode` stays a linear scan (it's ~20k cheap comparisons per call; acceptable).

2. **Raise the cap**: `PATHFINDER_MAX_NODES` 10000 → 20000. (Three `PathNode[20000]` pools ≈
   3–4 MB; fine. Do NOT go to 40k — `FindCheapestNode`'s linear scan would start to hurt on
   failing searches.)

3. **Mobile-obstacle toggle** (client-side, since the server flag isn't settable):
   - `public static bool IgnoreMobileObstacles { get; set; }` on `Pathfinder` (default false).
   - Fold into the condition at line 65:
     `bool ignoreGameCharacters = ... || Pathfinder.IgnoreMobileObstacles || ...`
   - REST endpoint `POST /api/actions/ignoremobiles` body `{"enabled":bool}` → enqueue
     `() => Pathfinder.IgnoreMobileObstacles = enabled`; respond (awaited helper from item 02)
     `{"ignoreMobiles":<new value>}`.
   - Player JSON: add top-level `"ignoreMobiles":bool` (read from the static).
   - Wrapper: `ignoremobiles <on|off>` subcommand + help line:
     `uo ignoremobiles <on|off>  Let pathfinding route through standing monsters (default: off)`

## Test

1. Build + restart per README.
2. **Perf sanity (failing search)**: pick an unreachable target ~300 tiles away (behind the
   known mountain). `time curl -s -X POST .../api/actions/pathfind -d '{"x":...,"y":...}'`
   → `{"pathFound":false,...}` and wall time **under ~1 s** (compare against the pre-change
   build if you want — build the old commit in a temp dir; not required, the <1s bar is enough).
3. **Perf sanity (successful search)**: 20-tile pathfind in open ground → fast as before,
   character walks.
4. **Toggle off (default)**: in the current monster-dense area, attempt a 40–60 tile pathfind
   in the direction of dense spawns; record `pathFound` (likely false or a long detour — this
   is the baseline).
5. **Toggle on**: `./ai/uo ignoremobiles on`; repeat the same pathfind → expect
   `pathFound:true` (or a route that completes). Verify `./ai/uo player | jq .ignoreMobiles`
   flips with the command. Then `./ai/uo ignoremobiles off` to restore.
6. **Regression**: `./ai/uo goto` (old endpoint) short hop works; `say` during a pathfind still
   lands in the journal.

## Acceptance

- Failing 300-tile search returns in <1 s; successful searches unchanged.
- Node bookkeeping is set/dictionary-backed (no linear pool scans in the add path).
- Toggle works end-to-end and changes pathfinding behavior in monster fields; default off.

## Implementation notes (deviations from the letter of the spec)

- **Free-slot allocation via index stacks** (`_freeOpen`/`_freeClosed`): the acceptance
  bar says "no linear pool scans in the add path", which includes the free-slot scans.
  Both pools now allocate from a `Stack<int>` refilled in `WalkTo` (40k pushes ≈ 40µs,
  negligible). Closed slots are never freed during a search, so the stack never regrows.
- **`MoveToClosedList(openIndex)` extracted** from `AddNodeToList`: the open→closed branch
  needs the open-list *index* (to push it back onto `_freeOpen`), but `AddNodeToList(1,…)`
  only received the `PathNode`. `FindCheapestNode` now calls `MoveToClosedList(cheapestNode)`
  directly; `AddNodeToList` handles open-list adds only. `OpenNodes` call site updated.
- **Start node allocated from `_freeClosed`** in `FindPath` (no more hardcoded `_closedList[0]`
  magic — the old slot-0 assumption would desync from the stack).
- The pre-existing double-cost quirk in the open-node update
  (`node.DistFromStartCost = startCost + cost`) was preserved verbatim — behavior parity,
  not a fix for this item.
- `FindCheapestNode` stays linear, per spec.

## Test results (live, Trammel)

- Failing search (~235t to the impassable mountain mass, open-ground start):
  `{"pathFound":false}` in **0.16–0.18 s** wall (bar: <1 s). Note: the searchable region
  is bounded by *loaded* map chunks (a few dozen tiles around the player), so a ground-level
  pathfind can't actually exhaust the 20k budget; the <1 s bar is met with wide margin and
  the add path is scan-free by construction.
- Successful search: 22-tile path in open ground, walked to completion.
- Toggle in a monster field (countryside north of the swamp, wolves/bears/ettins):
  same 30-tile target, **OFF → pathLength 32 (detour around the wolf), ON → pathLength 27
  (straight through)**. Player JSON `ignoreMobiles` flips with the command; process restart
  resets to default off.
- Regressions: `goto`-style short hop OK; `say` during an active pathfind lands in journal.
- Collateral finding: the character died to a grey wolf twice in this field while testing
  the ON route — expected (that's what the toggle does). First death handled via the
  healer resurrection gump; second death the gump did not appear (no healer nearby), so
  she was left waiting for one — resurrection mechanics: the gump is offered by a *healer
  present at the corpse*, not sent automatically.

## Commit

`Speed up pathfinder node bookkeeping; add mobile-obstacle toggle`
