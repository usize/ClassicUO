# Notes on Drop/Place Skill Improvements

## Goals
- Make `drop` intuitive: pick up an item and drop it on the ground or into a container in one step.
- Add a `place` skill for explicit ground coordinates or container targets.
- Auto-`goto` when the target item is > 3 tiles away on the ground.
- Test by removing deathrobes (equipped robes) and dropping them.

## What Was Changed

### 1. Bash script (`ai/uo`)
- **`drop`**: Added a ground-item distance check via `api/world/items/{serial}`. If distance > 3, it calls `goto` before dropping.
- **`place`**: New command with syntax `uo place <serial> [container | x y z]`. Same auto-goto logic.
- Help text updated.

### 2. C# RestAPI (`ActionsController.cs`)
- Modified the `Drop` endpoint to **compose pickup + drop**:
  - If the item is not already held (`ItemHold`), call `GameActions.PickUp()` first.
  - Then call `GameActions.DropItem()` with the requested container/coordinates.
  - For ground drops with unspecified coordinates (`0xFFFF`), substitute the player's current X/Y/Z so the server receives valid world coordinates instead of sentinel values.

## Why It Didn't Work

### The Core Problem
The `GameActions.PickUp()` + `GameActions.DropItem()` sequence works when dropping **into a container** (see `GrabItem`, which uses the exact same pattern). However, when dropping **onto the ground** (`container = 0xFFFFFFFF`), the item consistently ends up back in the backpack rather than on the ground.

### Observed Behavior
1. **`grab` works perfectly**: `grab 1075945082` (equipped robe) successfully moved the robe into the backpack.
2. **Separate `pickup` + `drop` works for backpack→ground?** No. After moving the robe to the backpack, calling `drop 1075945082` or `place 1075944214` (another backpack robe) left the item in the backpack.
3. **The item never appeared on the ground** in the `summary` entity list.
4. **The API returned 202 Accepted** with no errors, suggesting the server accepted the packets but chose to put the item in the backpack.

### Hypotheses Tested

#### 1. Coordinate sentinels (`0xFFFF`)
- **Theory**: Dropping to ground with `x = 0xFFFF, y = 0xFFFF` is invalid; the server defaults to backpack.
- **Fix tried**: Substituted player coordinates (`world.Player.X/Y/Z`) when `container == 0xFFFFFFFF && x == 0xFFFF`.
- **Result**: Still ended up in the backpack.

#### 2. Two-step vs. composed action
- **Theory**: Calling `PickUp` and `DropItem` in the same Enqueue lambda is too fast; the server needs a frame between them.
- **Fix tried**: Called `pickup` and `drop` as separate REST requests with `sleep 1` between them.
- **Result**: Still ended up in the backpack.

#### 3. Equipped items can't be dropped directly
- **Theory**: The server (ModernUO) might reject dropping an equipped item to ground without an explicit "unequip" step.
- **Fix tried**: Used `grab` to move the robe to the backpack first (this worked), then tried to drop from backpack to ground.
- **Result**: Still ended up in the backpack.

#### 4. The `DropItem` conditional silently failing
- **Theory**: `GameActions.DropItem` checks `ItemHold.Enabled` and might skip sending the packet if something is off.
- **Investigation**: `ItemHold.Set()` does set `Enabled = true`. `DropItem` should proceed. But because the REST API process hides stdout, we couldn't see the `Console.WriteLine("PACKET - ITEM DROP OK!")` from `PacketHandlers.DropItemAccepted`.
- **Result**: Unconfirmed.

## What Would Likely Fix It

1. **Use the existing `grab` logic as the model**: `GrabItem` works because it drops into a container (the backpack). A ground drop might need a different packet structure or timing.
   - Compare `Send_DropRequest` vs. what the native client sends when you drag an item from your backpack to the ground with the mouse.

2. **Add a ground-drop-specific endpoint**: Instead of reusing the generic `Drop` action, create an endpoint that calls a new helper method mirroring the client's native ground-drop behavior (possibly using `Send_DropRequest_Old` or setting `slot` differently).

3. **Expose `ItemHold` state via REST**: Add `GET api/cursor` so we can inspect `ItemHold.Enabled`, `.Serial`, and `.Container` to verify whether pickup actually succeeded before attempting the drop.

4. **Add synchronous feedback to actions**: Return a result from `Drop` (using `TaskCompletionSource` like the paperdoll endpoint) so we know whether `PickUp` returned `false` or `DropItem` was skipped.

## Current Status
- The bash `drop`/`place` commands are ready and include auto-goto logic.
- The C# `Drop` endpoint auto-composes pickup when the item isn't held.
- **Ground drops are still non-functional**; items end up in the backpack.
- The best immediate workaround for the user is: `grab <serial>` to move an item into the backpack, then manually use the in-game client to drag it to the ground if needed.
