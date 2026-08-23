# Skills Reference

All skill actions go through `./ai/uo skill <index>` (see `SKILL.md`). The six
indices below are confirmed directly from `SKILL.md`'s own quick reference:
`0=Alchemy 16=Eval Int 17=Healing 23=Inscription 25=Magery 46=Meditation`. The
rest of this table follows the standard modern-UO/ModernUO `SkillName` enum
ordering, which is internally consistent with those six anchors — but if any
index below ever produces an unexpected result in-game, trust `./ai/uo player`'s
own skill list over this document and treat it as the correction.

## How skill gain works

- **Use-based.** Skills rise only by attempting the action they govern — casting
  a spell, swinging a weapon, mining ore, talking to a locked chest, etc.
  Reading about a skill does nothing; the server rolls a gain check each time
  you use it.
- **Gain chance shrinks as you approach your cap** relative to your stats — the
  closer current skill is to (roughly) your relevant stat total, the harder
  gains get. Very low skill gains fast; near-GM (100.0) gains are slow and
  need many repetitions.
- **Failure can still gain the skill** (and success can still fail to gain it)
  — gain is a separate roll from success/failure of the action itself. Don't
  stop training because an action "failed"; check `./ai/uo journal` for a skill
  gain message regardless of outcome.
- **GM = 100.0.** Standard total skill cap across all skills combined is ~700.0
  (unverified for this specific server — confirm via `player` if it matters).
  Legendary/beyond-GM skills (100.1–120) exist on some rulesets via scrolls of
  power/transcendence; don't assume they're available here without evidence.
- **Three ways to raise a skill:**
  1. **Self-train** — repeat the action yourself. Free, slow, always available.
  2. **NPC vendor training** — `./ai/uo use <npc-serial>` then `./ai/uo say "train"`
     raises the skill instantly for gold, but **only up to 40.0** (per `SKILL.md`).
     Fastest way to get a new skill off the ground.
  3. **Trainer/guild NPCs and skill-specific mechanics** — some skills
     (e.g. Lockpicking, Magery) benefit from specific setups covered in
     `training-guide.md` and `professions.md`.

## Combat skills

| Skill | Index | Governs | Train via |
|---|---|---|---|
| Anatomy | 1 | Bonus melee damage, healing effectiveness | Fight things; study while healing |
| Parrying | 5 | Block chance with weapon or shield | Fight while equipped with a weapon/shield |
| Tactics | 27 | Melee damage bonus | Fight things |
| Archery | 31 | Bow/crossbow combat | Fight with a ranged weapon |
| Swordsmanship | 40 | Sword-class weapons | Fight with a sword weapon |
| Macing | 41 | Blunt weapons | Fight with a mace/hammer/staff |
| Fencing | 42 | Piercing weapons | Fight with a spear/dagger/kryss |
| Wrestling | 43 | Unarmed combat | Fight bare-handed |
| Throwing | 57 | Thrown weapons | Fight with a throwable |

See `combat.md` for how these combine into an effective fighter.

## Magic skills

| Skill | Index | Governs | Train via |
|---|---|---|---|
| Evaluate Intelligence | 16 | Spell damage scaling | Cast spells |
| Meditation | 46 | Mana regeneration | `./ai/uo meditate` while safe and out of combat |
| Magery | 25 | Cast spells (`./ai/uo cast <n>`) | Cast spells; buy reagents from herbalists/alchemists |
| Magic Resistance | 26 | Resist being spell-targeted | Get hit by/resist spells |
| Inscription | 23 | Write spells to scrolls, some scroll bonuses | Use with blank scrolls + reagents at a scribe's table |
| Spirit Speak | 32 | Necromancer utility, ghost communication | Use near spirits, or as a necromancer |
| Necromancy | 49 | Necromancer spells | Cast necromancer spells |
| Focus | 50 | Necromancer/summon stat support | Passive use while playing necromancer |
| Chivalry | 51 | Paladin prayers | Invoke paladin abilities |
| Bushido | 52 | Samurai combat forms | Fight as a samurai |
| Ninjitsu | 53 | Ninja abilities (smoke bombs, animal form) | Use ninja abilities |
| Spellweaving | 54 | Arcane focus spells | Cast spellweaving spells at an arcane focus |
| Mysticism | 55 | Mystic spells | Cast mysticism spells |
| Imbuing | 56 | Craft magic properties onto items | Imbue items at an imbuing station |

## Trade / crafting skills

| Skill | Index | Governs | Train via |
|---|---|---|---|
| Blacksmithing | 7 | Forge weapons/armor from ingots | Smith at a forge/anvil |
| Fletching | 8 | Craft bows/arrows from wood | Craft at a fletcher's bench |
| Carpentry | 11 | Craft wood furniture/items | Craft at a carpenter's bench |
| Cartography | 12 | Craft/decode maps | Use blank maps, decode treasure maps |
| Cooking | 13 | Prepare food | Cook at a fire/oven with ingredients |
| Tailoring | 34 | Craft cloth/leather armor and clothes | Craft at a sewing station |
| Tinkering | 37 | Craft tools, traps, mechanisms | Craft with a tinker's tools |
| Alchemy | 0 | Brew potions | Brew at an alchemy station with reagents |

## Gathering / wilderness skills

| Skill | Index | Governs | Train via |
|---|---|---|---|
| Camping | 10 | Set up a campfire anywhere | Use a campfire item |
| Detect Hidden | 14 | Reveal hidden creatures/players/traps | Use actively near suspected hidden targets |
| Fishing | 18 | Catch fish, fish up treasure | Fish with a fishing pole at water |
| Herding | 20 | Direct herd animals | Use near herdable animals |
| Lumberjacking | 44 | Chop logs from trees | Chop trees with an axe |
| Mining | 45 | Mine ore/stone from rock | Mine at a rock/vein with a shovel/pick |
| Tracking | 38 | Locate creatures/players on the map | Use the tracking ability |
| Veterinary | 39 | Heal/resurrect animals | Bandage injured tamed animals |

## Lore / identification skills

| Skill | Index | Governs | Train via |
|---|---|---|---|
| Animal Lore | 2 | Identify animal stats/loyalty | Use on an animal |
| Item Identification | 3 | Identify magic item properties | Use on items |
| Arms Lore | 4 | Identify weapon/armor durability details | Use on equipment |
| Forensics | 19 | Investigate corpses for cause of death | Use on a corpse |
| Taste Identification | 36 | Detect poison in food/drink | Use on consumables |

## Social / bard skills

| Skill | Index | Governs | Train via |
|---|---|---|---|
| Begging | 6 | Beg gold/items from NPCs | Use on NPCs |
| Peacemaking | 9 | Calm hostile creatures | Use in combat situations |
| Discordance | 15 | Debuff a target's combat stats | Use on a target, usually with an instrument |
| Provocation | 22 | Incite two targets to fight each other | Use on two targets, usually with an instrument |
| Musicianship | 29 | Underlies all bard skills | Play an instrument |
| Poisoning | 30 | Apply poison to weapons | Use poison on a weapon |

## Thief / rogue skills

| Skill | Index | Governs | Train via |
|---|---|---|---|
| Hiding | 21 | Become hidden | Use while not being watched |
| Lockpicking | 24 | Pick locked containers/doors | Use on locked things (own locked chest is safest) |
| Snooping | 28 | Peek into others' containers unnoticed | Use on a container |
| Stealing | 33 | Take items from mobiles/containers unnoticed | Use on a target — **risky, can flag you criminal** |
| Stealth | 47 | Move while hidden without revealing | Use while hidden and moving |
| Remove Trap | 48 | Disarm trapped containers | Use on a trapped container |

## Taming

| Skill | Index | Governs | Train via |
|---|---|---|---|
| Animal Taming | 35 | Tame wild creatures | Use on a tameable creature |

Reference `professions.md` for how these skills combine into a coherent build,
and `training-guide.md` for concrete step-by-step routes to raise each one.
