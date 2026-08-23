# Public Moongates

Moongates are free, instant travel between the public moongate on your current
facet and any other public moongate you choose from a menu — no spell, no
reagents, just walk into the swirling gate and double-click it (or step onto
it) to open the destination list.

**How to use one:** `./ai/uo use <moongate-serial>` (or simply walk onto it —
`OnMoveOver` triggers the menu too), then pick a destination in the gump that
opens. Coordinates below tell you where to *find* each gate, and where you'll
land at the other end.

Rules enforced by the server (`PublicMoongate.cs`):
- Criminals and anyone in combat cannot gate out.
- Murderers (red) can only travel between **Felucca** gates.
- The destination list shown depends on your current facet and expansion flags — from a Felucca or Trammel gate you'll see all Trammel + Felucca gates (plus Ilshenar/Malas/Tokuno once you've visited/unlocked them client-side).

This data is hardcoded server-side in `PublicMoongate.cs` (`PMList`), not
guessed — coordinates are exact.

## Felucca gates

| City | X | Y | Z |
|---|---|---|---|
| Moonglow | 4467 | 1283 | 5 |
| Britain | 1336 | 1997 | 5 |
| Jhelom | 1499 | 3771 | 5 |
| Yew | 771 | 752 | 5 |
| Minoc | 2701 | 692 | 5 |
| Trinsic | 1828 | 2948 | -20 |
| Skara Brae | 643 | 2067 | 5 |
| (New) Magincia | 3563 | 2139 | ground level |
| Buccaneer's Den | 2711 | 2234 | 0 |

## Trammel gates

Same footprint as Felucca for the shared cities, plus New Haven instead of
Buccaneer's Den:

| City | X | Y | Z |
|---|---|---|---|
| Moonglow | 4467 | 1283 | 5 |
| Britain | 1336 | 1997 | 5 |
| Jhelom | 1499 | 3771 | 5 |
| Yew | 771 | 752 | 5 |
| Minoc | 2701 | 692 | 5 |
| Trinsic | 1828 | 2948 | -20 |
| Skara Brae | 643 | 2067 | 5 |
| (New) Magincia | 3563 | 2139 | ground level |
| New Haven | 3450 | 2677 | 25 |

## Ilshenar gates — the Virtue moongates

Ilshenar's public gates are named for the eight Virtues (plus Chaos). These
double as the Virtue shrine sites referenced in quests and the Avatar's path.

| Virtue | X | Y | Z |
|---|---|---|---|
| Compassion | 1215 | 467 | -13 |
| Honesty | 722 | 1366 | -60 |
| Honor | 744 | 724 | -28 |
| Humility | 281 | 1016 | 0 |
| Justice | 987 | 1011 | -32 |
| Sacrifice | 1174 | 1286 | -30 |
| Spirituality | 1532 | 1340 | -3 |
| Valor | 528 | 216 | -45 |
| Chaos | 1721 | 218 | 96 |

## Malas gates

| City | X | Y | Z |
|---|---|---|---|
| Luna | 1015 | 527 | -65 |
| Umbra | 1997 | 1386 | -85 |

## Tokuno gates

| Island | X | Y | Z |
|---|---|---|---|
| Isamu-Jima | 1169 | 998 | 41 |
| Makoto-Jima | 802 | 1204 | 25 |
| Homare-Jima (Zento) | 270 | 628 | 15 |

## Notes

- Gate colors: the Umbra gate is tinted purple (hue `0x497`) in-game — a useful visual tell that you've reached the Malas gate menu vs. Felucca/Trammel's default gates.
- Getting *back* from Ilshenar/Malas/Tokuno works the same way: find the local public gate, open it, pick a Britannia city.
- Trained mages/priests get faster point-to-point travel via **Recall**, **Gate Travel**, and **Sacred Journey** (see `guides/skills-reference.md`) — moongates are the free fallback that requires no skill.
