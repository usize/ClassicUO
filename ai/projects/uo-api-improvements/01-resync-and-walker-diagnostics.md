# 01 — Resync endpoint + walker diagnostics

## Problem

When the server denies a walk step (position desync) or the character is frozen/paralyzed,
`PlayerMobile.Walk()` (`src/ClassicUO.Client/Game/GameObjects/PlayerMobile.cs`, guard at line ~525)
silently stops working:

```csharp
if (Walker.WalkingFailed || Walker.LastStepRequestTime > Time.Ticks ||
    Walker.StepsCount >= Constants.MAX_STEP_COUNT ||
    Client.Game.UO.Version >= ClientVersion.CV_60142 && IsParalyzed)
    return false;
```

`Walker.WalkingFailed` latches true on a bad step confirm; `StepsCount` (max 5,
`Constants.cs:16`) fills with unconfirmed steps. Both are only cleared when the server sends a
player position update (`PacketHandlers.cs` `UpdatePlayer`, ~line 6427) — which may not arrive.
The API exposes none of this, and the only client-side recovery primitive,
`NetClient.Socket.Send_Resync()` (`src/ClassicUO.Client/Network/OutgoingPackets.cs:3738`),
is not wired to any endpoint. Result: an undiagnosable, unfixable "character won't move" state.

## Changes

1. **`POST /api/actions/resync`** in `ActionsController.cs`:
   enqueue `() => NetClient.Socket.Send_Resync();` (check the exact static path used in
   `WalkerManager.cs` — it calls `NetClient.Socket.Send_Resync()`). Return a small JSON ack
   (`{"sent":true}`) — use a plain `Ok(new { sent = true })`.

2. **Player diagnostics** in the `player` response (`PlayerController.cs` + its DTO):
   add two fields, read on the game thread (follow how existing fields are populated —
   if the controller reads the world on the HTTP thread, move these reads into the same
   place other live player fields are read):
   - `"isParalyzed": <bool>` — `world.Player.IsParalyzed` (`Mobile.cs:117`, the Frozen flag)
   - `"walker": { "walkingFailed": <bool>, "stepsCount": <int> }` —
     `world.Player.Walker.WalkingFailed` / `.StepsCount` (`WalkerManager.cs:81,83`)

3. **Wrapper**: add a `resync` subcommand to `ai/uo` (POST `actions/resync`, print the body)
   and a line in the help text under a new "RECOVERY" section:
   `uo resync   Request a full client/server state resync (clears stuck walk state)`.

## Test

1. Build + restart per README.
2. `curl -si -X POST http://127.0.0.1:9000/api/actions/resync` → 200 with JSON body.
3. `./ai/uo player | jq '{isParalyzed, walker}'` → e.g. `{"isParalyzed":false,"walker":{"walkingFailed":false,"stepsCount":0}}`.
4. `./ai/uo move east` once, then re-check the two fields (should still be sane; stepsCount may
   transiently be 1 right after a step — that's expected, not a bug).
5. `./ai/uo resync` works from the wrapper.

## Acceptance

- Endpoint exists and returns JSON; wrapper subcommand works.
- `player` JSON contains `isParalyzed` and `walker` with correct types in the normal state.
- Nothing else in the player JSON changed.

## Commit

`Add resync action and walker diagnostics to player output`
