# Map & Coordinates: Overview

This server runs a modern-era ModernUO ruleset (T2A through Time of Legends /
Endless Journey — see `servers/ModernUO/Distribution/Configuration/expansion.json`).
Five facets (maps) are enabled. TerMur (Valley of Eodon) is **disabled**
(`MapSelectionFlags.TerMur: false`) — do not path there.

| Facet | What it is | Landmass |
|---|---|---|
| **Felucca** | The original world. Full looting, non-consensual PvP, guards only in town. Same geography as Trammel. | Britannia mainland |
| **Trammel** | A "safe" mirror of Felucca — no non-consensual PvP outside special areas. Same coordinates as Felucca for shared landmarks (cities, dungeon entrances). | Britannia mainland (copy) |
| **Ilshenar** | A separate landmass, no cities of its own in the classic sense — mostly wilderness, the eight Virtue moongates, and dungeons/shrines. | Own map |
| **Malas** | Added in Age of Shadows. Home to Luna (mage/vendor hub) and Umbra (near Doom Gauntlet). | Own map |
| **Tokuno** | Added in Samurai Empire. Three small islands: Isamu-Jima, Makoto-Jima, Homare-Jima (city: Zento). | Own map |

Each `map/<facet>.md` file lists that facet's landmarks with coordinates **local
to that facet's map** — a coordinate on Felucca is a different physical place
than the same coordinate typed while standing on Malas. You can't `goto` a
location on a facet you aren't currently on; travel there first (see
`map/moongates.md`), then `goto` within it.

## Coordinate system

- **X** increases **East**, decreases West.
- **Y** increases **South**, decreases North.
- **Z** is elevation/altitude (dungeon levels, upper floors, mountains use it heavily). It's usually safe to omit and let the client resolve ground level, but `./ai/uo goto <x> <y> <z>` accepts it explicitly when the destination is on a specific floor (e.g. a dungeon level or a building's upper story).
- Coordinates in these files come straight from this server's own data —
  `servers/ModernUO/Distribution/Data/Locations/*.json` (the same data ModernUO uses
  to power its in-game `[locations` browser) and `PublicMoongate.cs` (the hardcoded
  public moongate list). They are not guesses.

## How to actually get somewhere

1. **Check what facet you're on** — `./ai/uo player` or `./ai/uo summary` shows your location; ask "which map am I on" if unclear (Felucca and Trammel share coordinates, so this matters for safety, not pathing).
2. **Same facet, known coordinates** — `./ai/uo goto <x> <y> [z]`. This is an in-world walk; it can be a long trip across the map. A* pathfinding handles the route, but it's real travel time.
3. **Different facet, or long distance** — use a **moongate** (`map/moongates.md`) to jump near a city instantly, then `goto` from there. Player characters can also gain the Magery **Recall**/**Gate Travel** spells or Chivalry's equivalent once trained — see `guides/skills-reference.md`.
4. **Dungeons and multi-level sites** — the coordinate given is usually the entrance/exit tile. Levels below/above it are separate coordinate+Z combinations; see the specific facet file's dungeon sub-table.

## Files in this folder

- `felucca.md` — Felucca towns, dungeons, shrines, points of interest
- `trammel.md` — Trammel towns, dungeons, shrines, points of interest
- `ilshenar.md` — Ilshenar wilderness, dungeons, Virtue moongates
- `malas.md` — Malas: Luna, Umbra, Doom Gauntlet
- `tokuno.md` — Tokuno islands: Zento and the three islands
- `moongates.md` — the public moongate network: exact locations and what each one connects to
