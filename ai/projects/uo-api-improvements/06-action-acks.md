# 06 — Action endpoints return JSON acks

## Problem

Every action endpoint in `ActionsController.cs` returns `202 Accepted` with an **empty body**
at enqueue time. The action runs later on the game thread; if it throws, it is swallowed into
`Log.Error` and the caller never knows (see `ActionsController.Enqueue` and
`ApiGameController.Update`). Consequences seen in live play: `use` on a dead moongate, `move`
during a walker freeze, a mis-targeted `attack` — all looked identical to success, and the only
signal was "did the world change?".

Item 02 added the `EnqueueRun<T>` awaited helper — use it here.

## Changes

Convert these endpoints from `Enqueue(...); return Accepted();` to the awaited pattern, each
returning a small JSON ack computed **inside the queued lambda** (game thread):

| Endpoint | Ack shape | Notes |
|---|---|---|
| `move` | `{"accepted": bool}` | `player?.Walk(direction, run)` already returns bool |
| `say` / `yell` / `whisper` / `emote` (all four call `actions/say`) | `{"ok": true}` | confirms execution, not just enqueue |
| `use` | `{"found": bool}` | inside the lambda: `found` = serial exists in `world.Mobiles` or `world.Items` and not destroyed; then run `GameActions.DoubleClick` as today. If `GameActions.DoubleClick` returns a bool, add `"accepted": <it>` |
| `attack` | `{"found": bool}` | same existence check; run `GameActions.Attack` as today |
| `warmode` | `{"ok": true}` | the server flag may lag; don't try to report it |
| `skill` | `{"ok": true}` | results surface in the journal |
| `spell` | `{"ok": true}` | same |

Leave the other endpoints (`pathfind`, `travel`, `grab`, `pickup`, `equip`, `drop`, `target`,
`opendoor`, `stopwalk`) as item 02/03 defined them or as-is — don't expand scope.

**Exception surfacing**: in the `EnqueueRun` helper (or a small wrapper used by these
endpoints), if the action throws, fail the task with the exception; the controller catches it
and returns `500` with `{"error": "<message>"}`. Do not change the fire-and-forget
`ActionQueue.Enqueue` path itself — other callers depend on it.

Timeout: 3 s is fine for all of these (they are single-tick operations). On timeout return
`504 {"error":"timeout"}`.

## Test

1. Build + restart per README.
2. `curl -s -X POST .../api/actions/move -d '{"direction":"east"}'` → `{"accepted":true}`
   (or false if blocked — either is a valid ack; character should move when true).
3. `curl -s -X POST .../api/actions/say -d '{"text":"ack test"}'` → `{"ok":true}`; journal
   shows the line.
4. `curl -s -X POST .../api/actions/use -d '{"serial":999999}'` (bogus serial) →
   `{"found":false}`; with a real nearby serial from `summary` → `{"found":true,...}`.
5. `curl -s -X POST .../api/actions/attack -d '{"serial":999999}'` → `{"found":false}`.
   **Do not** attack a real serial in the test.
6. `curl -s -X POST .../api/actions/warmode -d '{"enabled":false}'` → `{"ok":true}`
   (use `false` — do not enable war mode in testing).
7. Regression: `./ai/uo summary` still renders; `goto`/`travel` still work.

## Acceptance

- The listed endpoints return the ack shapes; bogus `use`/`attack` targets report
  `found:false` instead of a silent 202.
- A throwing action returns 500 + error (test by temporarily... no — verify by code review of
  the wrapper plus the bogus-serial paths; don't force a crash in the live client).
- Empty-202 endpoints listed as out of scope still behave exactly as before.

## Commit

`Return JSON acks from move/say/use/attack/warmode/skill/spell`
