# Combat

Read `SKILL.md`'s Safety Rules and Key Learnings sections first — this file
expands on them, it doesn't replace them, and every number here matches what's
already stated there.

## Before you engage anything

1. **Check notoriety.** `./ai/uo summary`'s entity list STATUS column shows
   Innocent / Neutral / Criminal / Murderer / Invulnerable. Attacking an
   Innocent gives you a **permanent** murder count. Guards (`G` on the map)
   are invulnerable and will attack Criminals/Murderers on your behalf in
   town — don't try to fight them.
2. **Check your own condition.** HP, armor, and relevant skill levels via
   `./ai/uo player`. Don't engage something you can't currently read the
   threat level of — check `../lore/bestiary.md` if the target is a creature
   you don't recognize.
3. **Equip appropriately.** A weapon matching your trained weapon skill (see
   `professions.md`), war mode on: `./ai/uo warmode on`.

## The combat loop

1. `./ai/uo attack <serial>` to engage (melee) or `./ai/uo cast <n>` targeting
   the hostile (magic) — combat in this client is largely automatic once
   engaged for melee; keep issuing attacks/spells as they come off cooldown.
2. Watch HP via `./ai/uo summary` or `./ai/uo journal` between actions —
   don't chain blind actions, per `SKILL.md`'s Game Loop.
3. **Flee at 40–50% HP** — not lower. This is `SKILL.md`'s stated threshold;
   treat it as the hard rule, not a suggestion. Fleeing means **physically
   moving away** (`./ai/uo move`/`goto` away from the threat), not just
   toggling warmode off — an adjacent hostile keeps attacking regardless of
   your mode.
4. Heal in safety once disengaged, then decide whether to re-engage.
5. `./ai/uo warmode off` once combat is fully resolved — war mode left on
   idle is a magnet for unwanted attention and slows natural regen.

## Melee vs. ranged vs. magic

- **Melee** (Swordsmanship/Macing/Fencing/Wrestling/Throwing): weapon skill +
  Tactics + Anatomy scale your damage; Parrying adds block chance. Requires
  being adjacent to the target — closing distance is itself a risk.
- **Ranged** (Archery/Throwing): same damage-scaling skills apply, fight from
  distance, but ammunition (arrows/bolts/throwables) is a consumable —
  check inventory before committing to a fight.
- **Magic** (Magery/Necromancy/Chivalry/Bushido/Ninjitsu/Spellweaving/
  Mysticism): `./ai/uo cast <spell-number>`, needs reagents (see
  `../lore/items-and-resources.md`) and mana. EvalInt scales damage,
  Meditation recovers mana between casts. Interruptible by taking damage
  mid-cast on many rulesets — casting while actively being hit is riskier
  than casting from range.

## Healing

- **Bandages take ~12 seconds to apply** (`SKILL.md`). Healing effectiveness
  drops if you're hit while bandaging — bandage from safety when possible,
  not mid-melee-exchange.
- Healing skill (17) + Anatomy (1) improve bandage effectiveness and success
  chance.
- The mage `Heal` spell (`./ai/uo heal`, spell 4) is faster than a bandage
  but costs mana and reagents (Garlic, Ginseng, Spider Silk per `SKILL.md`).
- Veterinary (39) is the pet/animal equivalent of Healing — use it to bandage
  a tamed creature, not yourself.

## Poison

- Getting poisoned drains HP over time until cured. Cure via a Cure potion,
  the mage Cure spell, or (if trained) resisting/curing naturally through
  Magic Resistance.
- Taste Identification (36) detects poison in food/drink before you consume
  it — useful if looting consumables from corpses.
- Poisoning (30) is the offensive skill for coating your own weapon —
  relevant if you're building a poison-focused rogue/assassin style warrior.

## Reagents quick-reference

See `SKILL.md`'s own spell table for the most common combat spells (Heal,
Magic Arrow, Night Sight, Reactive Armor, Fireball, Magic Lock) and their
reagent costs, and `../lore/items-and-resources.md` for what each reagent
is and where it comes from. Reagents are sold by herbalist/alchemist NPCs —
`./ai/uo use <serial>` then `./ai/uo say "buy"`.

## After the fight

- Loot is fair game on a defeated hostile creature — corpses show as `%` on
  the map. `./ai/uo use <corpse-serial>` to open it.
- Looting a player-killed Innocent's corpse in front of witnesses can itself
  carry consequences on some rulesets — when in doubt, don't loot corpses
  that aren't clearly hostile-creature kills.
- Reset `./ai/uo warmode off` and consider a Peacemaking/rest period before
  moving on, especially if HP or mana are still low.
