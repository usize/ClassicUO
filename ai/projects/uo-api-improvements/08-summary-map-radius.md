# 08 — Configurable summary map radius

## Problem

The summary map is hard-coded to 41×21 (20-tile radius E/W, 10 N/S) — `TileGrid.RadiusX/RadiusY`
are consts (`Api/WorldSnapshot.cs:25-28`), sampled once per tick into
`WorldSnapshot.CurrentTileGrid`. For navigation decisions (spotting the next wall line,
mountain, or road 30–50 tiles out) the window is too small; the LLM ends up navigating by
repeated 25-tile hops and re-reading the map, which is exactly the tedium this branch removes.

## Changes

1. **Parameterize `TileGrid`** (`Api/WorldSnapshot.cs`):
   - Replace `const RadiusX/RadiusY/Width/Height` with instance fields set from the sampling
     radius; keep the default (20/10) for the per-tick cached grid.
   - `Sample(World world, int radiusX, int radiusY)` (default args 20/10). The per-tick
     sampling in `WorldSnapshot.Update` keeps calling the default.
2. **`GET /api/summary?radius=N`** (`SummaryController.cs`):
   - New `[FromQuery] int radius = 20`, clamped to 5..50.
   - radius == 20 (default) → use `WorldSnapshot.CurrentTileGrid` exactly as today (no
     extra game-thread work).
   - radius != 20 → fetch via the awaited helper from item 02:
     `EnqueueRun(() => TileGrid.Sample(world, r, r))` with a 3 s timeout; on timeout fall back
     to the default grid and note nothing (don't fail the request).
     (Tile sampling must run on the game thread — `world.Map.GetTile` + `world.Items` — so it
     cannot be called directly from the HTTP thread.)
   - The map header line already prints `{RadiusX}t E/W {RadiusY}t N/S` — with instance
     fields it will print the actual radius. Everything else in the summary (entity list,
     stairs, journal) is radius-independent.
3. **Wrapper** (`ai/uo`): `summary [radius]` — if the optional arg is numeric, append
   `?radius=N` to the GET. Help line:
   `uo summary [radius]   Full world view (map radius in tiles, default 20, max 50)`

## Test

1. Build + restart per README.
2. `./ai/uo summary` → identical shape to before: header `MAP 20t E/W 10t N/S`, 41 cols.
3. `./ai/uo summary 30` → header `MAP 30t E/W 30t N/S`, 61 columns × 61 rows of map, `@`
   centered, more terrain visible than the default.
4. `curl -s 'http://127.0.0.1:9000/api/summary?radius=25'` → 51-wide map.
5. `./ai/uo summary 99` → clamped to 50 (101-wide), responds in <2 s (time it).
6. `time ./ai/uo summary` (default) is as fast as before the change (the cached-grid path is
   untouched).
7. Regression: entity list, stairs section, and journal render correctly at both radii.

## Acceptance

- `?radius=` (5..50) changes the map window; default request is byte-compatible in structure
  with today's output; non-default radii are sampled on the game thread and don't block the
  loop for more than a few ms.

## Commit

`Add configurable map radius to the summary endpoint`
