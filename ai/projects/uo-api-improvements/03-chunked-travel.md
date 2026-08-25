# 03 — Server-side chunked travel with progress

## Problem

A* is capped at 10,000 closed nodes (`Pathfinder.cs:17,921`), so long routes fail: live testing
found ~25-tile hops working but 50-tile hops failing in cluttered terrain. The workaround —
chaining 25-tile `goto` hops from bash — is tedious and brittle (each hop re-derives context,
failures need manual detours). The fix: make the *client* own the multi-segment route. Each
segment is a small A* the node budget comfortably handles; when a segment completes, the client
re-pathfinds from the new position. Moving obstacles (monsters) simply get re-routed next
segment. This is the core navigation fix.

## Design

**Travel goal state** — new `TravelState` in `Api/` following the `WorldSnapshot` static
pattern (game thread writes, HTTP threads read):

```csharp
internal static class TravelState
{
    public static int GoalX, GoalY;
    public static sbyte GoalZ;
    public static bool Active;
    public static long LastPathfindTicks;   // cooldown bookkeeping
}
```

**Segment size**: 40 tiles. If the goal is >40 tiles away (Chebyshev), pathfind to a
*waypoint* 40 tiles along the straight line to the goal:
`wx = gx + Clamp(playerX - gx, -40, 40)`, same for y; waypoint z = player's current z.
If ≤40 tiles away, pathfind straight to the goal.

**Driver** — in `ApiGameController.Update()`, after the action-queue drain, each tick:
1. If `!TravelState.Active`, or world/player null, or `!world.InGame`, or player
   dead/paralyzed/is-targeting → skip.
2. If the player is within 1 tile (Chebyshev, x/y only) of the goal → `Active = false`
   (arrival). Also `Pathfinder.StopAutoWalk()` if still auto-walking.
3. Otherwise, if `Pathfinder.AutoWalking` is false (previous segment finished or never
   started) and `Time.Ticks - LastPathfindTicks >= 1000` (1s cooldown):
   - Compute waypoint (or goal). Call `Pathfinder.WalkTo(wx, wy, wz, 0)` via the player.
   - If it returns 0 (no path): try up to 5 alternative waypoints — same 40-tile step but
     offset perpendicular to the travel line by ±5 and ±10 tiles (clamp to ≥0).
   - If all fail: leave `Active` true, update `LastPathfindTicks`, and try again next
     cooldown (do NOT clear the goal — obstacles may move; do NOT spam pathfind).
   - On success: `LastPathfindTicks = Time.Ticks`.

**Endpoints** (ActionsController):
- `POST /api/actions/travel` body `{"x":int,"y":int,"z":sbyte?}` → enqueue setting
  `TravelState.{GoalX,GoalY,GoalZ,Active}` (z defaults to player's current z when omitted;
  read it inside the queued lambda). Respond via the awaited helper from item 02 with
  `{"active":true,"goalX":x,"goalY":y,"tilesRemaining":<chebyshev>}` computed in the lambda.
- `POST /api/actions/stopwalk` → existing behavior **plus** clear `TravelState.Active = false`
  in the same queued lambda.

**Player JSON** — add (PlayerController, read wherever other live fields are read):
`"travel": { "active":bool, "goalX":int, "goalY":int, "goalZ":int, "tilesRemaining":int }`
(tilesRemaining = Chebyshev distance player→goal; 0/active=false when not traveling).

**Wrapper** (`ai/uo`) — new subcommand:
```
travel <x> <y> [z]    Server-side travel: walk there in segments, report progress
```
Behavior: POST the request; then poll `./ai/uo player` every 1s:
- success: `travel.active == false` AND player within 1 tile of (x,y) → print
  `✓ Arrived at (x, y, z)`, exit 0
- stuck: position unchanged for 20 consecutive samples while active → print
  `✗ Travel stuck at (px, py, pz) — goal (x, y) is N tiles away` + hint
  (`try: ./ai/uo summary  (check for doors/monsters), ./ai/uo stopwalk`), exit 1
- timeout: `max(30, tiles * 1.2 + 15)` seconds → same stuck-style message, exit 1
Add to help under MOVE.

## Test

1. Build + restart per README; `./ai/uo stopwalk`.
2. **Short travel** (~15 tiles to open ground): `./ai/uo travel <x> <y>` → arrives,
   `✓ Arrived`, exit 0. `./ai/uo player | jq .travel` → `active:false`.
3. **Long travel** (~150+ tiles ESE through the known hill/monster country — pick a point
   ~150 tiles E of the character on flat ground, verify with the map): runs unattended,
   no bash hops needed, arrives (exit 0). Watch `travel.tilesRemaining` decrease monotonically
   (polling from another shell is fine).
4. **Cancel mid-travel**: start a long travel, `./ai/uo stopwalk` after ~5s → travel stops
   (active false; character stops within a step or two).
5. **Unreachable goal**: travel to a point clearly inside a wall (read the map) → character
   does not spin CPU (watch `top` on cuo-api for a minute), stays put, wrapper reports
   stuck/timeout with a clean message, exit 1.
6. **Regression**: `./ai/uo move`, `./ai/uo goto` (old pathfind endpoint), `say` all still work.

## Acceptance

- A 150+ tile route completes unattended on flat-ish terrain.
- Progress is observable in `player.travel`; stopwalk cancels; unreachable goals fail
  cleanly without a hot loop.
- No changes to `Pathfinder.cs` (that's item 04).

## Deviation (implemented)

- **Segment size is 25, not 40.** `Pathfinder.CanWalk` reads tiles via
  `Map.GetTile(x, y, load: false)` — A* can only search *already-loaded* 8×8 map
  chunks (time-LRU, evicted by `Map.ClearUnusedBlocks`), so a 40-tile waypoint can
  lie outside the loaded area and the search can never see it. Live testing (item 02)
  found 25-tile hops work and 50-tile hops fail in cluttered terrain; 25 stays inside
  both the loaded-chunk radius and the 10k node budget. Revisit after item 04
  (pathfinder node cap / bookkeeping).
- **Waypoint formula follows the spec's prose** ("waypoint N tiles along the straight
  line to the goal", measured from the player). The spec's literal formula
  `wx = gx + Clamp(playerX - gx, -N, N)` measures from the *goal*, which would make
  the first segment ~110 tiles on a 150-tile trip — contradicting the spec's own
  "small A*" premise. Implemented as `wx = playerX + Clamp(goalX - playerX, -N, N)`.
- **Arrival requires the goal's floor when a Z was requested.** The spec's "x/y only"
  arrival check let travel report "Arrived" while the character was still 20 Z units
  above an explicit ground-floor goal (observed live: goal (1338,1740,2), "arrived" at
  z=22 with no stair ever attempted). Implemented as
  `Chebyshev(x,y) <= 1 && (!zExplicit || |playerZ - goalZ| <= 2)` — the floor check
  applies only when the request included a Z (floors are ~20 Z apart, so 2 never spans
  floors; ground terrain can slope ±3 between distant tiles, so an omitted Z must not
  gate arrival). For off-floor explicit-Z goals the driver pathfinds the stair route in
  the final segment (WalkTo handles z changes); if no stair is within segment reach it
  retries on cooldown and the wrapper reports stuck/timeout honestly.
- **Cooldown backs off on consecutive failures (1 s → 2 s → 4 s → 8 s → 15 s cap).**
  The spec's flat 1 s retry loop measured 45–49% sustained CPU on the game thread when
  stuck against the (1706,2010) mountain (up to 5 full A* searches per second), which
  fails the spec's own "does not spin CPU" test. `TravelState.ConsecutiveFailures` grows
  on each fully-failed cycle and resets on any successful pathfind (so a goal blocked by
  a *moving* obstacle is still retried within ~1 s at first). A permanently blocked goal
  settles to one 5-A* burst per 15 s ≈ a few % CPU.

## Commit

`Add server-side chunked travel (actions/travel) with progress reporting`
