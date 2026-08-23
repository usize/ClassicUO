# Training Guide

Concrete routes for raising skills from 0 toward GM (100.0). These are general,
reliable patterns, not a min-maxed speedrun — exact optimal routes shift with
server-specific gain-rate tuning, so treat timing claims as rough and watch
`./ai/uo journal` for actual gain messages as ground truth. See
`skills-reference.md` for what each skill index does and `professions.md` for
which skills to combine.

## Universal pattern

1. **Vendor-train to 40 first, for any skill an NPC offers it for.**
   `./ai/uo use <npc-serial>` → `./ai/uo say "train"`. Instant, costs gold,
   caps at 40.0 (per `SKILL.md`). This is the fastest possible start for any
   trainable skill and should almost always be step one before self-training.
2. **Self-train from 40 to GM** via repeated use of the skill. Gain chance is
   good in this range and drops as you approach your personal cap — expect to
   need many more repetitions in the 80–100 range than in the 40–70 range.
3. **Check `./ai/uo journal` after batches of actions**, not after every
   single one — skill-gain messages appear there and confirm progress without
   needing to poll `player` constantly.

## Order of operations for a fresh character

1. Decide archetype (`getting-started.md` §2, `professions.md`).
2. Buy/equip whatever tool or weapon your core skill needs.
3. Vendor-train every skill in your build that an NPC will raise to 40.
4. Self-train the cheapest/safest skill in your build first to build
   confidence in the loop (e.g. a crafting or gathering skill has no combat
   risk), then move to riskier skills (combat, taming, stealing) once you
   have some HP/gold buffer.
5. Revisit `combat.md` before your first real fight — don't self-train combat
   skills against anything you haven't sized up first.

## Combat skills (weapon skill, Tactics, Anatomy, Parrying)

Fight live targets — there's no substitute. Start against weak, low-threat
creatures (check `../lore/bestiary.md` for a target's relative danger before
engaging) rather than anything that can plausibly kill you at low skill.
Tactics and Anatomy gain passively alongside your weapon skill just by being
in combat, so a single fight trains multiple skills at once. Parrying gains
from taking hits while a weapon or shield is equipped.

## Magery / Evaluate Intelligence / Meditation

- Buy a spellbook and a stock of basic reagents (Sulfurous Ash, Black Pearl,
  Garlic, Ginseng, Spider Silk, Mandrake Root, Nightshade, Blood Moss —
  see `../lore/items-and-resources.md`) from a mage-shop NPC.
- Cast the lowest-circle spell you have reagents for, repeatedly — Magic
  Arrow and Heal are common early choices since they're cheap and useful.
  Every cast attempt (success or fail) rolls for both Magery and EvalInt
  gain.
- `./ai/uo meditate` while safe and stationary between casting sessions to
  recover mana without waiting on passive regen — this also trains
  Meditation itself.
- Inscription (23) trains separately at a scribe's table using blank scrolls
  and the same reagents as the spell being inscribed — worth training once
  Magery is established, since self-made scrolls save reagent cost long-term.

## Crafting skills (Blacksmith, Tailoring, Carpentry, Tinkering, Fletching,
## Cooking, Alchemy)

1. Buy the relevant tool (see `../lore/items-and-resources.md` for tool
   names per skill) from a tool vendor.
2. Gather or buy raw material — ore for Blacksmithing (via Mining), cloth/
   leather for Tailoring (buyable or hunted), boards for Carpentry/Fletching
   (via Lumberjacking), reagents for Alchemy.
3. Craft the cheapest item repeatedly at the appropriate station. Failed
   crafts still consume some material and still roll for skill gain.
4. As skill rises, more complex/valuable recipes unlock — check what's
   craftable periodically rather than grinding the same low-tier item the
   whole way to GM.

## Gathering skills (Mining, Lumberjacking, Fishing)

- Mining: find a rock/vein (check the current facet's `../map/` file for
  known mining areas near mountains), use Mining repeatedly with a shovel or
  pickaxe. Ore quality/veins can require the higher skill to access
  successfully.
- Lumberjacking: use an axe on trees repeatedly.
- Fishing: use a fishing pole at any open water tile.
- All three double as a resource supply for a matching crafting skill —
  train them together with `professions.md`'s Crafter build for full
  self-sufficiency.

## Taming (Animal Taming, Animal Lore, Veterinary)

- Start on the weakest tameable creatures (rabbits, cats, small dogs — check
  `../lore/bestiary.md` for taming-difficulty notes). Attempting to tame
  something well above your current skill is a common way to provoke an
  attack from the creature.
- Animal Lore trains passively by using it to inspect creatures before
  taming attempts — worth doing every time since it also informs whether the
  attempt is safe.
- Veterinary trains by bandaging an injured tamed pet — keep a pet slightly
  hurt (from mild combat) rather than always at full health if you need
  reps.

## Bard skills (Musicianship, Provocation, Peacemaking, Discordance)

- Buy an instrument. Train Musicianship first and alone for a while — the
  other three all require a successful Musicianship check underneath them,
  so a weak Musicianship suppresses their gain too.
- Provocation/Peacemaking/Discordance train by using them on live targets in
  or near combat — safest done alongside a warrior/tamer who can handle the
  actual fighting while you support.

## Thief skills (Hiding, Stealth, Snooping, Lockpicking, Stealing)

- Train Hiding and Lockpicking first on **your own belongings** — pick your
  own locked chest, hide repeatedly in a safe spot. Zero risk.
- Snooping trains on any container, including your own or an NPC shop's
  display — no criminal flag from snooping an NPC vendor's own stock in most
  rulesets, but treat player-owned containers as off-limits until you
  understand local norms.
- Stealing is the highest-risk skill in the game to train — it can flag you
  Criminal or trigger a guard response on a failed attempt against a live
  target. Get Hiding and Stealth solid first so you can escape after a
  successful (or failed) attempt.

## Necromancy / Spirit Speak / Focus

Learn spells from a necromancer trainer, then cast low-tier necromancer
spells repeatedly near safe targets — same use-based gain pattern as Magery.
Spirit Speak also gains from interacting with spirits/ghosts directly.

## Chivalry / Bushido / Ninjitsu / Spellweaving / Mysticism

These late-era skill lines generally require learning the ability set from a
trainer or book specific to that skill line, then using the abilities
repeatedly — same use-based gain pattern as everything else. Check with the
relevant trainer NPC for that skill's specific unlock requirements before
assuming it behaves exactly like Magery.
