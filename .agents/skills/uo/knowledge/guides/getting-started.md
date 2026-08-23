# Getting Started

Read this after `SKILL.md` and before doing anything else with a new or unfamiliar
character. It's an orientation index, not a full manual — it points into the
deeper guides below.

## 1. Find out who you are

```
./ai/uo player
```

Read, in order:
- **Name, race, sex.** Race changes what's possible: Elves get bonus resist and
  see hidden creatures more easily; Gargoyles cannot ride normal animal mounts
  (only gargoyle-specific ones — see `SKILL.md` Key Learnings) and fly instead
  of walking in some contexts.
- **Skills list.** Whatever is already trained tells you the *intended*
  archetype far more reliably than guessing from gear. A character with
  Magery/EvalInt/Meditation trained is a mage even if wearing armor.
- **Stats (Str/Dex/Int) and HP/Mana/Stamina.** High Str + weapon skill = melee
  viable now. High Int + Magery = spellcasting viable now. Low everything =
  brand new character, treat this guide as a build plan, not a status report.
- **Equipment** (`./ai/uo paperdoll <own serial>` if unclear from `player`) —
  weapon, armor, spellbook all signal role.

Standard-era stat/skill caps (verify against `./ai/uo player` if precision
matters — this server may differ): 100.0 per skill at GM, ~700.0 skill points
total cap, 125 max per stat / ~225 total stat cap.

## 2. Pick (or confirm) an archetype

| If skills lean toward... | You're playing a... | Primary guide |
|---|---|---|
| Swordsmanship/Macing/Fencing + Tactics + Anatomy | **Warrior** | `professions.md` §Warrior |
| Magery + EvalInt + Meditation + Inscription | **Mage** | `professions.md` §Mage |
| AnimalTaming + AnimalLore + Veterinary | **Tamer** | `professions.md` §Tamer |
| Blacksmith/Tailoring/Carpentry/Tinkering + a gathering skill | **Crafter** | `professions.md` §Crafter |
| Musicianship + Provocation/Peacemaking/Discordance | **Bard** | `professions.md` §Bard |
| Stealing/Hiding/Snooping/Lockpicking | **Thief** | `professions.md` §Thief |
| Necromancy + SpiritSpeak | **Necromancer** | `professions.md` §Necromancer |
| Chivalry + a weapon skill | **Paladin** | `professions.md` §Paladin |
| Bushido + a weapon skill | **Samurai** | `professions.md` §Samurai |
| Ninjitsu + Hiding/Stealth | **Ninja** | `professions.md` §Ninja |
| Everything near 0 | **Fresh character** — go to §3 below | `training-guide.md` |

Roleplay accordingly per `SKILL.md`'s Roleplay section — a mage talks and
moves differently than a warrior.

## 3. Fresh character checklist

1. Skim `training-guide.md` for an order-of-operations.
2. Check starting inventory/gold (`player`/`summary`) — you may already be
   equipped for your archetype.
3. Find the nearest bank and relevant trainer/vendor — see the facet file
   under `../map/` matching your current location, or ask "what facet am I
   on and where's the nearest city" via `./ai/uo summary`.
4. Start training per `training-guide.md` and the skill-specific notes in
   `skills-reference.md`.

## 4. Where things are

- Currently lost or need coordinates for a city, dungeon, shrine, or
  moongate? → `../map/overview.md` and the per-facet files.
- Need to know what a creature or item actually is before you fight it or
  pick it up? → `../lore/bestiary.md`, `../lore/items-and-resources.md`.
- Need world/faction/virtue context for roleplay or an NPC quest? →
  `../lore/world-and-history.md`.

## 5. Deeper guides

- `skills-reference.md` — every skill, its index number, how it's trained.
- `professions.md` — full build guides per archetype.
- `combat.md` — fighting safely (notoriety, healing, fleeing, reagents).
- `training-guide.md` — concrete 0→GM training routes.
