"""Navigation helpers: A*-based pathfinding with tile-grid wall awareness and landmark lookup."""

from __future__ import annotations

import asyncio
import heapq
import json
import time
from pathlib import Path

import aiohttp

from lib.actions import BASE, _get, _post, use

LANDMARKS_PATH = Path(__file__).parent.parent / "map" / "landmarks.json"

# UO direction index → (dx, dy)
# 0=N, 1=NE, 2=E, 3=SE, 4=S, 5=SW, 6=W, 7=NW
_DIR_DELTA = {
    0: (0, -1),
    1: (1, -1),
    2: (1, 0),
    3: (1, 1),
    4: (0, 1),
    5: (-1, 1),
    6: (-1, 0),
    7: (-1, -1),
}

# Reverse lookup: (dx, dy) → direction index
_DELTA_DIR = {v: k for k, v in _DIR_DELTA.items()}

MOVE_DELAY = 0.5    # seconds between move steps (generous to let server process each move)
DOOR_RANGE = 2      # tiles within which we open doors

# A* search limits
ASTAR_MAX_NODES = 4000   # abort A* if grid is too sparse/large


# ── Landmark DB ───────────────────────────────────────────────────────────────

def load_landmarks() -> dict:
    """Return the full landmarks dict (live read, no cache)."""
    try:
        return json.loads(LANDMARKS_PATH.read_text())
    except Exception:
        return {"landmarks": {}}


def find_landmark(query: str) -> tuple[str, dict] | None:
    """
    Fuzzy-search landmarks by key or name/tag fragment.
    Returns (key, landmark_dict) or None.
    """
    db = load_landmarks()["landmarks"]
    q = query.lower().replace("-", "_").replace(" ", "_")

    if q in db:
        return q, db[q]

    matches = [(k, v) for k, v in db.items() if q in k]
    if len(matches) == 1:
        return matches[0]
    if len(matches) > 1:
        return min(matches, key=lambda kv: len(kv[0]))

    matches = [(k, v) for k, v in db.items() if q in v.get("name", "").lower()]
    if matches:
        return min(matches, key=lambda kv: len(kv[1].get("name", "")))

    matches = [(k, v) for k, v in db.items() if q in v.get("tags", [])]
    if len(matches) == 1:
        return matches[0]

    return None


def add_landmark(key: str, name: str, x: int, y: int, z: int = 0, map_idx: int = 1,
                 tags: list[str] | None = None, notes: str = "", **kwargs) -> None:
    """Write a new or updated landmark to landmarks.json."""
    data = json.loads(LANDMARKS_PATH.read_text())
    data["landmarks"][key] = {
        "name": name, "x": x, "y": y, "z": z, "map": map_idx,
        "tags": tags or [], "notes": notes, **kwargs
    }
    LANDMARKS_PATH.write_text(json.dumps(data, indent=2))


# ── Player position ────────────────────────────────────────────────────────────

async def player_pos() -> tuple[int, int, int]:
    """Return (x, y, z) from the player endpoint."""
    data = await _get("player")
    return data.get("x", 0), data.get("y", 0), data.get("z", 0)


# ── Tile grid fetch ────────────────────────────────────────────────────────────

async def fetch_tile_grid() -> dict | None:
    """
    Fetch the current tile grid from GET /api/world/tiles.
    Returns dict with keys: playerX, playerY, radiusX, radiusY, width, height, rows.
    Returns None on failure.
    """
    try:
        data = await _get("world/tiles")
        if "rows" in data:
            return data
    except Exception:
        pass
    return None


def _grid_passable(rows: list[str], gx: int, gy: int) -> bool:
    """True if grid cell (gx, gy) can be walked through (open or door)."""
    if gy < 0 or gy >= len(rows):
        return False
    row = rows[gy]
    if gx < 0 or gx >= len(row):
        return False
    c = row[gx]
    return c in ('.', '+', '^', 'v', 'X', ' ')  # ' '=unloaded, treat as passable


def _grid_is_door(rows: list[str], gx: int, gy: int) -> bool:
    """True if grid cell is a door tile."""
    if gy < 0 or gy >= len(rows):
        return False
    row = rows[gy]
    if gx < 0 or gx >= len(row):
        return False
    return row[gx] == '+'


# ── A* pathfinding on tile grid ───────────────────────────────────────────────

def _astar(
    rows: list[str],
    player_x: int,
    player_y: int,
    radius_x: int,
    radius_y: int,
    tx: int,
    ty: int,
) -> list[tuple[int, int]] | None:
    """
    A* from player world position to (tx, ty) using the tile grid.
    Returns list of world-coordinate waypoints (excluding start), or None if no path found.
    The player's grid position is (radius_x, radius_y).
    """
    # Convert world coords to grid coords
    def w2g(wx: int, wy: int) -> tuple[int, int]:
        return wx - player_x + radius_x, wy - player_y + radius_y

    def g2w(gx: int, gy: int) -> tuple[int, int]:
        return gx - radius_x + player_x, gy - radius_y + player_y

    width = len(rows[0]) if rows else 0
    height = len(rows)

    start_g = w2g(player_x, player_y)
    goal_wx, goal_wy = tx, ty
    goal_g = w2g(tx, ty)

    # If goal is outside grid, clamp to grid boundary in the right direction
    clamped = False
    cgx = max(0, min(goal_g[0], width - 1))
    cgy = max(0, min(goal_g[1], height - 1))
    if (cgx, cgy) != goal_g:
        goal_g = (cgx, cgy)
        clamped = True

    def heuristic(gx: int, gy: int) -> float:
        gwx, gwy = g2w(gx, gy)
        return max(abs(gwx - goal_wx), abs(gwy - goal_wy))  # Chebyshev distance

    open_heap: list[tuple[float, int, int]] = []
    heapq.heappush(open_heap, (0.0, start_g[0], start_g[1]))
    came_from: dict[tuple[int, int], tuple[int, int] | None] = {start_g: None}
    g_score: dict[tuple[int, int], float] = {start_g: 0.0}

    nodes_expanded = 0
    found_goal: tuple[int, int] | None = None

    while open_heap:
        _, cx, cy = heapq.heappop(open_heap)
        cur = (cx, cy)

        if cur == goal_g:
            found_goal = cur
            break

        if nodes_expanded > ASTAR_MAX_NODES:
            break
        nodes_expanded += 1

        for dir_idx, (ddx, ddy) in _DIR_DELTA.items():
            nx, ny = cx + ddx, cy + ddy
            nxt = (nx, ny)

            if not _grid_passable(rows, nx, ny):
                continue

            # Diagonal movement cost: √2 ≈ 1.414; cardinal: 1.0
            move_cost = 1.414 if (ddx != 0 and ddy != 0) else 1.0
            # Small penalty for door tiles to prefer open paths when available
            if _grid_is_door(rows, nx, ny):
                move_cost += 0.5

            tentative_g = g_score[cur] + move_cost

            if nxt not in g_score or tentative_g < g_score[nxt]:
                g_score[nxt] = tentative_g
                f = tentative_g + heuristic(nx, ny)
                heapq.heappush(open_heap, (f, nx, ny))
                came_from[nxt] = cur

    if found_goal is None:
        return None

    # Reconstruct path in world coords (excluding start)
    path_g: list[tuple[int, int]] = []
    node = found_goal
    while node is not None:
        path_g.append(node)
        node = came_from.get(node)
    path_g.reverse()
    path_g = path_g[1:]  # skip start

    return [g2w(gx, gy) for gx, gy in path_g]


# ── Direction calculation ─────────────────────────────────────────────────────

def _best_direction(px: int, py: int, tx: int, ty: int) -> int:
    """Return direction index (0-7) pointing most directly from (px,py) toward (tx,ty)."""
    dx = tx - px
    dy = ty - py
    if dx == 0 and dy == 0:
        return 0

    # Normalize to 8 compass directions
    if abs(dx) >= abs(dy) * 2:
        return 2 if dx > 0 else 6          # E / W
    elif abs(dy) >= abs(dx) * 2:
        return 4 if dy > 0 else 0          # S / N
    elif dx > 0 and dy < 0:
        return 1                            # NE
    elif dx > 0 and dy > 0:
        return 3                            # SE
    elif dx < 0 and dy > 0:
        return 5                            # SW
    else:
        return 7                            # NW


def _dir_to_waypoint(px: int, py: int, wx: int, wy: int) -> int:
    """Direction index from current position toward waypoint."""
    dx = wx - px
    dy = wy - py
    # Prefer exact diagonal/cardinal match
    sdx = 0 if dx == 0 else (1 if dx > 0 else -1)
    sdy = 0 if dy == 0 else (1 if dy > 0 else -1)
    return _DELTA_DIR.get((sdx, sdy), _best_direction(px, py, wx, wy))


# ── Door handling ──────────────────────────────────────────────────────────────

async def _open_nearby_doors() -> int:
    """Open all doors within DOOR_RANGE tiles. Returns count opened."""
    try:
        items = await _get("world/items")
        if not isinstance(items, list):
            return 0
        doors = [
            i for i in items
            if "door" in i.get("name", "").lower() and i.get("distance", 99) <= DOOR_RANGE
        ]
        for door in doors:
            await use(door["serial"])
            await asyncio.sleep(0.4)
        return len(doors)
    except Exception:
        return 0


# ── Smart move-based navigation ───────────────────────────────────────────────

async def smart_goto(
    x: int,
    y: int,
    z: int = 0,
    timeout: float = 60.0,
    tolerance: int = 3,
) -> bool:
    """
    Navigate to (x, y) using A* pathfinding on the tile grid when available,
    falling back to greedy directional movement when the grid cannot be fetched
    or the target is out of range.

    Algorithm:
    - On first step and every REPLAN_EVERY steps: fetch tile grid once, run A*.
    - Follow planned waypoints. Before stepping onto a door tile (known from the
      grid fetched at plan time), open nearby doors first.
    - On stuck ≥2: scan all 8 directions; commit to escape dir for a few steps.
    - Timeout after `timeout` seconds. Returns True on arrival, False on timeout.
    """
    REPLAN_EVERY = 12  # replan A* every N successful steps

    deadline = time.time() + timeout
    last_pos: tuple[int, int] | None = None
    stuck_count = 0
    committed_dir: int | None = None
    committed_steps = 0
    path: list[tuple[int, int]] = []        # remaining world-coord waypoints
    door_tiles: set[tuple[int, int]] = set()  # waypoints that are doors
    steps_since_replan = 0

    while time.time() < deadline:
        px, py, _ = await player_pos()

        if max(abs(px - x), abs(py - y)) <= tolerance:
            return True

        moved = (last_pos is not None and (px, py) != last_pos)

        if moved:
            stuck_count = 0
            if committed_dir is not None:
                committed_steps -= 1
                if committed_steps <= 0:
                    committed_dir = None
            steps_since_replan += 1
        else:
            if last_pos is not None:
                stuck_count += 1

            # Committed direction stopped working — abandon it so we can scan again
            if committed_dir is not None and stuck_count >= 3:
                committed_dir = None
                path = []
                door_tiles = set()

            # Force replan if stuck without a path for too long
            if stuck_count >= 5 and not path:
                stuck_count = 0  # reset so we don't spam replans

            # Open doors every 3 stuck steps
            if stuck_count > 0 and stuck_count % 3 == 0:
                await _open_nearby_doors()

            # After 2 stuck steps, scan for a clear direction
            if stuck_count >= 2 and committed_dir is None:
                ideal_dir = _best_direction(px, py, x, y)
                scan_order = [0, 1, -1, 2, -2, 3, -3, 4]
                for offset in scan_order:
                    candidate = (ideal_dir + offset) % 8
                    await _post("actions/move", {"directionIndex": candidate})
                    await asyncio.sleep(MOVE_DELAY)
                    nx, ny, _ = await player_pos()
                    if (nx, ny) != (px, py):
                        committed_dir = candidate
                        committed_steps = 4
                        last_pos = (nx, ny)
                        path = []
                        door_tiles = set()
                        steps_since_replan = 0
                        stuck_count = 0
                        break
                continue

        last_pos = (px, py)

        # Use committed escape direction if set
        if committed_dir is not None:
            await _post("actions/move", {"directionIndex": committed_dir})
            await asyncio.sleep(MOVE_DELAY)
            continue

        # Replan A* when path is empty or stale (one grid fetch per replan)
        if not path or steps_since_replan >= REPLAN_EVERY:
            grid = await fetch_tile_grid()
            if grid and grid.get("rows"):
                rows = grid["rows"]
                rx, ry = grid["radiusX"], grid["radiusY"]
                gpx, gpy = grid["playerX"], grid["playerY"]
                new_path = _astar(rows, gpx, gpy, rx, ry, x, y)
                if new_path:
                    path = new_path
                    # Mark door waypoints from the freshly fetched grid
                    door_tiles = set()
                    for wx, wy in path:
                        gx = wx - gpx + rx
                        gy = wy - gpy + ry
                        if _grid_is_door(rows, gx, gy):
                            door_tiles.add((wx, wy))
                else:
                    path = []
                    door_tiles = set()
            else:
                path = []
                door_tiles = set()
            steps_since_replan = 0

        # Advance along path or fall back to greedy
        if path:
            # Skip waypoints already reached (within 1 tile)
            while path and max(abs(px - path[0][0]), abs(py - path[0][1])) <= 1:
                path.pop(0)

            if path:
                wx, wy = path[0]
                # Open door before stepping onto a door tile
                if (wx, wy) in door_tiles:
                    await _open_nearby_doors()
                    await asyncio.sleep(0.3)
                dir_idx = _dir_to_waypoint(px, py, wx, wy)
            else:
                dir_idx = _best_direction(px, py, x, y)
        else:
            dir_idx = _best_direction(px, py, x, y)

        await _post("actions/move", {"directionIndex": dir_idx})
        await asyncio.sleep(MOVE_DELAY)

    return False


async def smart_follow(
    serial: int,
    stop_distance: int = 2,
    timeout: float = 30.0,
    fallback_xy: tuple[int, int] | None = None,
) -> bool:
    """
    Navigate toward mobile/item `serial` using move commands until within stop_distance.
    Polls mobile list each step for the target's current position.

    If target is not visible (out of render range), uses fallback_xy as the
    intermediate destination until the target comes into view.
    """
    import os as _os
    proxy = _os.environ.get("http_proxy") or _os.environ.get("HTTP_PROXY")

    deadline = time.time() + timeout
    last_pos: tuple[int, int] | None = None
    stuck_count = 0
    committed_dir: int | None = None
    committed_steps = 0

    while time.time() < deadline:
        # Refresh target position from mobiles list
        tx = ty = None
        try:
            async with aiohttp.ClientSession() as session:
                async with session.get(f"{BASE}/world/mobiles", proxy=proxy) as resp:
                    mobiles = await resp.json()
            target = next((m for m in mobiles if m.get("serial") == serial), None)
            if target is not None:
                if target.get("distance", 999) <= stop_distance:
                    return True
                tx, ty = target["x"], target["y"]
        except Exception:
            pass

        # Fall back to known coords if target not visible
        if tx is None:
            if fallback_xy:
                tx, ty = fallback_xy
            else:
                # Target gone and no fallback
                break

        px, py, _ = await player_pos()
        ideal_dir = _best_direction(px, py, tx, ty)
        moved = (last_pos is not None and (px, py) != last_pos)

        if moved:
            stuck_count = 0
            if committed_dir is not None:
                committed_steps -= 1
                if committed_steps <= 0:
                    committed_dir = None
        else:
            stuck_count += 1
            if stuck_count % 3 == 0:
                await _open_nearby_doors()
            if stuck_count >= 2 and committed_dir is None:
                scan_order = [0, 1, -1, 2, -2, 3, -3, 4]
                for offset in scan_order:
                    candidate = (ideal_dir + offset) % 8
                    await _post("actions/move", {"directionIndex": candidate})
                    await asyncio.sleep(MOVE_DELAY)
                    nx, ny, _ = await player_pos()
                    if (nx, ny) != (px, py):
                        committed_dir = candidate
                        committed_steps = 3
                        last_pos = (nx, ny)
                        break
                continue

        last_pos = (px, py)
        dir_idx = committed_dir if committed_dir is not None else ideal_dir
        await _post("actions/move", {"directionIndex": dir_idx})
        await asyncio.sleep(MOVE_DELAY)

    return False


async def goto_landmark(query: str, timeout: float = 60.0) -> bool:
    """
    Look up landmark by name/tag/key and navigate there.
    Uses 'approach' coords if present (outside building doors), otherwise main coords.
    Returns True on arrival.
    """
    result = find_landmark(query)
    if result is None:
        raise ValueError(f"Unknown landmark: {query!r}")

    key, lm = result
    approach = lm.get("approach")
    if approach:
        tx, ty = approach["x"], approach["y"]
        tz = approach.get("z", lm.get("z", 0))
    else:
        tx, ty, tz = lm["x"], lm["y"], lm.get("z", 0)

    return await smart_goto(tx, ty, tz, timeout=timeout)
