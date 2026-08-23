# UO Knowledge Base

Reference material for the `uo` skill (`.agents/skills/uo/SKILL.md`). SKILL.md
covers *how to act* (the `./ai/uo` command interface, the game loop, safety
rules). This folder covers *what to know* — world geography, lore, and how
the game's systems work — so an agent can look things up mid-session instead
of guessing.

**When to read from here:** whenever you hit a fact you're not sure of —
"where is X", "what is a moongate", "how do I train Y", "what does this
creature/item do". Grep for the term first (`grep -ril "term" knowledge/`),
or jump straight to the relevant file below. Don't read the whole knowledge
base up front — it's a reference, not required reading.

All map coordinates in this knowledge base come from this server's own data
(`servers/ModernUO/Distribution/Data/Locations/*.json` and `PublicMoongate.cs`)
— they are exact, not guessed. Lore, bestiary, and mechanics content draws on
general Ultima Online knowledge and is flagged where a specific number should
be verified in-game (`./ai/uo player`, `./ai/uo summary`) rather than trusted
blindly.

## map/ — where things are

| File | Covers |
|---|---|
| [`map/overview.md`](map/overview.md) | Facets on this server, the coordinate system, how travel actually works — **read this first** |
| [`map/moongates.md`](map/moongates.md) | The public moongate network: exact gate locations and destinations on every facet |
| [`map/felucca.md`](map/felucca.md) | Felucca: towns, dungeons, shrines |
| [`map/trammel.md`](map/trammel.md) | Trammel: towns, dungeons, shrines |
| [`map/ilshenar.md`](map/ilshenar.md) | Ilshenar: wilderness, dungeons, Virtue moongates |
| [`map/malas.md`](map/malas.md) | Malas: Luna, Umbra, Doom Gauntlet |
| [`map/tokuno.md`](map/tokuno.md) | Tokuno: Zento and the three islands |

## lore/ — what things are

| File | Covers |
|---|---|
| [`lore/world-and-history.md`](lore/world-and-history.md) | Britannia lore: the Virtues, the Guardian/Mondain/Minax/Exodus arc, notoriety system |
| [`lore/bestiary.md`](lore/bestiary.md) | Creatures by threat tier: what they are, how they fight, what they drop |
| [`lore/items-and-resources.md`](lore/items-and-resources.md) | Reagents, potions, scrolls, crafting resources, currency — the item glossary |

## guides/ — how to play

| File | Covers |
|---|---|
| [`guides/getting-started.md`](guides/getting-started.md) | Orientation for a new session: who am I, what archetype, where to look next |
| [`guides/skills-reference.md`](guides/skills-reference.md) | Every skill, its index number, how it's trained and gained |
| [`guides/professions.md`](guides/professions.md) | Build guides: warrior, mage, tamer, crafter, bard, thief, necromancer, paladin, samurai, ninja |
| [`guides/combat.md`](guides/combat.md) | Combat mechanics, healing, fleeing, notoriety in a fight |
| [`guides/training-guide.md`](guides/training-guide.md) | Practical routes for raising skills from 0 to GM |
