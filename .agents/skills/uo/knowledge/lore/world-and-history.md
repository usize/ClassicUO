# World & History

A compact lore primer — enough to roleplay in character and understand context
clues (an NPC invoking a Virtue, a shrine's name, why guards ignore a Criminal
but not a Murderer). Not a full history; only what's useful at the table.

## Britannia

The land you're playing in. Ruled by **Lord British**, a benevolent (and
famously hard-to-kill) monarch who governs from Castle Britannia in the city
of Britain. His brother **Blackthorn** has at various points ruled in his
stead or opposed him, depending on the era's storyline — treat him as a
complicated, sometimes-antagonist noble if he comes up, not a simple villain.

The world is shaped by three recurring evils from its history:

- **Mondain** — an evil wizard whose gem of immortality shattered Britannia's
  history into fragments; the original game's central villain.
- **Minax** — Mondain's lover and avenger, who fractured time itself trying to
  undo his defeat.
- **Exodus** — a sentient, self-aware machine/computer built by Minax's
  bloodline, worshipped as a false god by a corrupted population in its dungeon.

You don't need the full timeline to play — just recognize these three names
as "the old evils" if an NPC or quest references them.

## The Avatar and the Eight Virtues

The **Avatar** is the legendary hero-title earned by walking the Path of
Virtue — not a specific character your agent plays, but a role any sufficiently
principled adventurer can be recognized as. The path rests on **Eight Virtues**,
each with a patron figure, a rune, and a shrine (see `../map/moongates.md` for
the Ilshenar Virtue shrine/moongate coordinates):

| Virtue | Principle(s) | Patron |
|---|---|---|
| Honesty | Truth | Grandfather (Hawkwind lineage) |
| Compassion | Love | Queen Dawn |
| Valor | Courage | Lord British (as knight) |
| Justice | Truth + Love | Lord British |
| Sacrifice | Love + Courage | Elizabeth |
| Honor | Truth + Courage | Sir Cabirus |
| Spirituality | Truth + Love + Courage | Golden Sage |
| Humility | none of the above (the "empty" virtue) | The Shepherd |

A ninth concept, **Singularity** (sometimes called **Chaos** in-game — see the
Ilshenar moongate of that name), represents the absence/opposite of the Virtues
and is tied to the Guardian's corrupting influence in later storylines.

## The Guardian

A shadowy, world-consuming entity that appears as a face in stone or shadow
across the later story arcs (Age of Shadows onward — most of this server's
enabled content). Treat as the current "big bad" behind Malas/Doom-era
storylines if it comes up; don't over-elaborate beyond that unless you're
confident of the specifics.

## Notoriety and the law

Every mobile has a notoriety color/status the server reports directly
(`STATUS` column in `./ai/uo summary` — see `SKILL.md`). This is the actual
rule system, not flavor:

| Status | Meaning | Guard behavior |
|---|---|---|
| Innocent (blue) | Hasn't committed a crime recently | Guards protect them; attacking one unprovoked is a crime |
| Neutral (grey) | Not flagged either way (often non-player-aligned monsters) | No guard protection either way |
| Criminal (grey, flagged) | Recently stole, looted, or attacked an Innocent — freely attackable by anyone for a short window with no penalty | Guards may still intervene depending on context |
| Murderer (red) | Has accumulated murder counts from killing Innocents | Guards attack on sight in town; can only moongate between Felucca gates (see `../map/moongates.md`) |
| Invulnerable (yellow/gold) | Town guards and some special NPCs — cannot be harmed | N/A |

**Killing an Innocent player is a permanent murder count** — this is why
`SKILL.md`'s safety rules insist on checking notoriety before attacking
anything. There is no "undo."

## Factions

Player-run political/military factions (Minax, Council of Mages, True
Britannians, Shadowlords) exist in Felucca and let players capture towns and
fight over faction-controlled resources (silver, sigils). This is advanced,
optional, Felucca-only PvP content — safe to ignore unless the character is
already faction-aligned or a human asks about it.

## See also

- `../map/moongates.md` — the Ilshenar Virtue shrine/moongate coordinates
- `bestiary.md` — named bosses tied to this lore (Mondain, Minax, Exodus, the Dark Father, etc.)
- `SKILL.md` — the notoriety glyphs and safety rules this system maps to in practice
