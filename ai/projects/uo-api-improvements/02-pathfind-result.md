# 02 — Pathfind returns pathFound/pathLength (awaited-action helper)

## Problem

`POST /api/actions/pathfind` (`ActionsController.cs:166-205`) returns a bare `202 Accepted`
with an empty body. The queued lambda calls `world.Player.Pathfinder.WalkTo(...)` on the game
thread and discards its result — `WalkTo` returns `bool` (`_pathSize != 0`, `Pathfinder.cs:995`).
The caller cannot tell whether a path was found. The bash wrapper compensates with a fragile
"watch the position for 1 second" stuck-heuristic, which misfires (it reported "stuck" for
pathfinds that had simply not started yet, and "reached" for positions the character walked past).

Because actions execute asynchronously on the game thread (see README threading model), the
HTTP response cannot simply capture the lambda's return value. We need a small
enqueue-and-await primitive. **Items 03 and 06 build on this helper — design it to be general.**

## Changes

1. **`Pathfinder.WalkTo` returns the path length.**
   Change `public bool WalkTo(int x, int y, int z, int distance)` (Pathfinder.cs:930) to
   `public int WalkTo(...)` returning `_pathSize` (0 when no path found — the existing final
   `return _pathSize != 0;` becomes `return _pathSize;`). Update every caller
   (grep `WalkTo` — expected: ActionsController, possibly a couple of client call sites;
   update any bool-context usage, e.g. `if (WalkTo(...))` → `if (WalkTo(...) > 0)`).

2. **Awaited action helper** in `ActionQueue.cs`:
   ```csharp
   public Task<T> EnqueueAwait<T>(Func<T> action)
   ```
   enqueue a wrapper that runs `action()` on the game thread and completes a
   `TaskCompletionSource<T>` (create with `RunContinuationsAsynchronously`).
   In `ActionsController`, add a private helper that enqueues and awaits with a timeout:
   ```csharp
   private async Task<T?> EnqueueRun<T>(Func<T> action, int timeoutMs = 5000)
   ```
   using `await tcs.Task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs))`; on timeout return
   `default` (controller methods decide how to report it). Make the affected controller
   methods `async Task<IActionResult>`.

3. **Pathfind responses** (both branches — serial target and x/y target):
   capture `int pathLength = ...WalkTo(...)` inside the awaited action; respond
   `Ok(new { pathFound = pathLength > 0, pathLength })`.
   If the await timed out: `Ok(new { pathFound = false, pathLength = 0, error = "timeout" })`.

## Test

1. Build + restart per README; `./ai/uo stopwalk`.
2. Note position: `./ai/uo player | jq '{x,y,z}'`.
3. Nearby target (~5–10 tiles, pick from the summary map's open ground):
   `curl -s -X POST http://127.0.0.1:9000/api/actions/pathfind -d '{"x":<X>,"y":<Y>}'`
   → `{"pathFound":true,"pathLength":N}` with N ≥ distance. Character actually walks (poll
   position a few seconds — it may end 1 tile off target; that's normal).
4. Far target (>150 tiles straight into a wall/mountain — e.g. reuse a known-blocked spot or
   simply 300 tiles E): → `{"pathFound":false,"pathLength":0}` quickly (no 5s hang), and the
   character does not start walking.
5. Serial target still works: `curl ... -d '{"serial":<decimal-serial-of-nearby-mobile>}'`
   → JSON body (pathFound true or false is fine; shape is what matters).
6. `./ai/uo move east` still works afterward (regression: the awaited path must not wedge the queue).

## Acceptance

- Both pathfind branches return the JSON shape; a no-path target returns `pathFound:false`
  fast; the game loop stays responsive (a `say` during a pathfind still lands in the journal).
- All existing `WalkTo` callers compile and behave as before.

## Commit

`Return pathFound/pathLength from the pathfind endpoint`
