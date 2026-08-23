# Items & Resources

A grep-able glossary for when `./ai/uo summary` or `./ai/uo mobiles` shows an
item you don't recognize. Map glyphs (`p`, `r`, `s`, `b`, `$`, etc.) are
defined in `SKILL.md`'s summary legend — this file doesn't redefine them, only
explains what's behind each category.

## Spell reagents (`r` on map)

The eight reagents every Magery spell consumes some combination of. Sold by
herbalists/alchemists — `./ai/uo use <npc-serial>` then `./ai/uo say "buy"`
(see `SKILL.md`).

| Reagent | Common uses |
|---|---|
| Black Pearl | Offensive/illusion spells (Magic Arrow, Fireball, Invisibility, Blade Spirits) |
| Blood Moss | Location/summon spells (Teleport, Magic Lock, Recall, Summon creature) |
| Garlic | Protection/curing spells (Heal, Reactive Armor, Cure, Protection) |
| Ginseng | Healing/buff spells (Heal, Agility, Cunning, Great Heal) |
| Mandrake Root | Curse/utility spells (Curse, Poison, Paralyze, Incognito) |
| Nightshade | Poison/death-adjacent spells (Poison, Mind Blast, Energy Bolt) |
| Spider's Silk | Sight/detection spells (Night Sight, Reactive Armor, Detect Hidden) |
| Sulfurous Ash | Fire/energy spells (Fireball, Explosion, Magic Arrow, Energy Bolt) |

A given spell needs a specific combination — see the quick reference in
`SKILL.md`'s Skills and Spells section for common early spells, or check the
spell's tooltip in-game.

## Potions (`p` on map)

Colored/typed consumables, typically alchemy-crafted or looted. Common types:
Heal, Cure (poison), Refresh (stamina), Strength/Agility (temporary stat
buff), Explosion (thrown offensive), Poison (thrown offensive), Invisibility,
Night Sight, Deadly Poison. Use with `./ai/uo use <serial>`, or throw offensive
ones at a target — check the item's tooltip via `paperdoll`/inspection if the
type isn't obvious from context.

## Scrolls (`s` on map)

Four distinct things all render as "scroll" — don't confuse them:

| Type | What it does |
|---|---|
| **Spell scroll** | One-time cast of a spell without needing it memorized in a spellbook, or used to inscribe a new spellbook entry (Inscription skill) |
| **Power scroll** | Permanently raises a skill's cap above the normal 100 (to 105/110/115/120) for the character that uses it — valuable, typically from champion spawns/bosses |
| **Stat scroll** | Permanently raises the character's Str/Dex/Int cap similarly to a power scroll |
| **Skill scroll (Treasures of Tokuno)** | A cosmetic/craftable reward scroll, unrelated to skill points |

If unsure which kind an item is, don't consume it — inspect first.

## Currency

- **Gold** (`$` on map) — the base currency, stacks in inventory.
- **Bank checks** — large gold sums converted to a single stackable item for easier carrying/banking; deposit/withdraw at any bank (`N`-marked banker NPC or bank building).

## Crafting resources

Gathered via Mining/Lumberjacking/etc. (see `../guides/skills-reference.md`
and `../guides/professions.md` for how to actually raise these skills) and
consumed by crafting skills (Blacksmith, Tailoring, Carpentry, etc.).

| Resource | Source skill | Tiers (roughly weakest → strongest) |
|---|---|---|
| Ore / Ingots | Mining → smelt into ingots | Iron, Dull Copper, Shadow Iron, Copper, Bronze, Gold, Agapite, Verite, Valorite |
| Leather | Skinning creatures (via a blade) | Regular, Spined, Horned, Barbed |
| Logs / Boards | Lumberjacking | Regular, Oak, Ash, Yew, Heartwood, Bloodwood, Frostwood |
| Cloth | Tailoring (bought/woven, not gathered) | Regular cloth, various colored variants |
| Gems | Mining (bonus finds) | Used in jewelry crafting and some spell components |

Higher tiers are rarer and yield equipment with better bonuses — a character
gathering for a crafting profession should prioritize finding these higher
tiers once base skill is reliable.

## Containers

Anything marked `C` on the map (`SKILL.md` legend) is a container — backpacks,
chests, barrels, crates. `./ai/uo use <serial>` opens it. Some are locked
(needs Lockpicking or a key) or trapped (needs Remove Trap or caution).

## Bandages (`b` on map)

Cloth bandages used with the Healing skill to stop bleeding and restore HP
over time (~12 seconds to apply — see `../guides/combat.md`). Craftable via
Tailoring from cloth, or bought from healers.

## See also

- `../guides/skills-reference.md` — the skills that gather/use these resources
- `../guides/professions.md` — build guides organized around these resource chains
- `bestiary.md` — what creatures actually drop these items
