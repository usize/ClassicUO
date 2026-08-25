# HANDOFF — UO AI Sandbox (read this first)

_Last updated: 2026-08-25 12:51 PDT, branch `uo-api-improvements`._

This repo is an experiment: let an LLM **play** Ultima Online through a local REST API
(`http://127.0.0.1:9000`) exposed by a headless ClassicUO client, and drive it with the
`./ai/uo` bash CLI + the `uo` skill. Two workstreams are in flight:

- **A — REST API fixes** (`ai/projects/uo-api-improvements/`): 9 numbered specs fixing
  navigation/observability bugs found during live play. Items **01–04 done**, 05–09
  not started.
- **B — Playable harness** (this doc, section 4): the bigger goal — a harness where the LLM
  is the *brain* (chooses tasks, socializes, reacts) and a deterministic engine is the *hands*
  (executes long repetitive tasks), with compact observations and persistent play-state.

**Immediate next action for a fresh session:** start item 05 (`journal?limit` returns most
recent N) per `ai/projects/uo-api-improvements/05-*.md`. First: check whether Gemma has
been resurrected (she was left **dead** at (1453,2072,0) — see section 4; a background
poller was watching for the healer gump).

---

## 1. Repo & git state

- Branch: `uo-api-improvements` (based on `ai_sandbox`).
- Recent commits (newest first):
  - `25973dff5` — item 04: pathfinder O(1) bookkeeping, 20k cap, mobile-obstacle toggle
  - `4c4bea701` — item 03: server-side chunked travel (`actions/travel`) + progress
  - `3f7168fe2` — item 02: pathfind returns `pathFound`/`pathLength` (+ awaited-action helper)
  - `bbc2f6eaa` — item 01: `resync` endpoint + walker diagnostics in `player`
  - `d3cc78b35` — the 9-item project plan (`ai/projects/uo-api-improvements/`)
  - `82d64d957` — Gemma profile (mage goal, model bump, memory notes)
  - `ba108afa1` — UO knowledge base added to the `uo` skill
- **Uncommitted right now:** only this file (`ai/projects/HANDOFF.md`, untracked).
- Rules (from `ai/projects/uo-api-improvements/README.md`): one commit per work item, exact
  commit messages given in each spec, never commit `.restapi.toml`/`bin/`/`obj/`/logs,
  sequential subagents only (single branch, git-index safety).

## 2. Workstream A — REST API fix status

Specs live in `ai/projects/uo-api-improvements/0N-*.md`; the README there has the shared
context (build/restart, threading model, safety). Status:

| # | Fix | Status |
|---|-----|--------|
| 01 | `resync` endpoint + `isParalyzed`/`walker` in `player` | ✅ committed `bbc2f6e` |
| 02 | `pathfind` returns `{pathFound,pathLength}`; `EnqueueAwait<T>`/`EnqueueRun<T>` helper | ✅ committed `3f7168f` |
| 03 | **Server-side chunked travel** (`actions/travel`, 25-tile segments, progress) | ✅ committed `4c4bea7` (see notes below) |
| 04 | Pathfinder O(1) node bookkeeping, cap 20k, mobile-obstacle toggle | ✅ committed `25973d` (see notes below) |
| 05 | `journal?limit` returns most recent N | ⬜ not started |
| 06 | Action endpoints return JSON acks (move/use/attack/say…) | ⬜ not started |
| 07 | Rewrite `ai/uo goto` around travel, honest exit codes | ⬜ not started |
| 08 | Configurable summary map radius (`?radius=`) | ⬜ not started |
| 09 | Update `SKILL.md` to match new behavior | ⬜ not started (last) |

### Item 03 — completion record (2026-08-24)

Implemented, tested live, committed as `4c4bea7`. All spec tests passed:

- **Short travel** (12 tiles, open ground): `✓ Arrived at (1341,1769,15)`, exit 0,
  `player.travel.active:false`.
- **Long travel** (151 tiles S, unattended): `✓ Arrived at (1349,1919,0)` in <30 s,
  exit 0. Terrain z followed 17→0 across segments.
- **Cancel**: `stopwalk` 5 s into a 149-tile travel → `active:false`, character stopped
  within 1 step; wrapper reported "Travel stopped", exit 1.
- **Unreachable goal** (into the (1706,2010) mountain at (1549,2000)): character stayed
  put, no CPU spin (~15% with travel stuck vs ~14% idle, measured with `top -pid`),
  wrapper "✗ Travel stuck … is 25 tiles away" + hint, exit 1.
- **Regression**: `move`/`goto`/`say` all work (east step at the mountain foot is
  legitimately blocked by terrain).

**Deviations from the spec (all documented in `03-chunked-travel.md`):**
1. **25-tile segments, not 40** — A* only sees *loaded* 8×8 map chunks
   (`Map.GetTile(x,y,load:false)`); 40 can land outside the loaded area. 25 is the
   validated working hop size. Item 04 may allow larger segments.
2. **Z-aware arrival** — arrival requires `|playerZ − goalZ| ≤ 2` *when Z was requested*
   (omitted Z = "the x/y spot on whatever floor"). Fixed a live bug where travel
   "arrived" 20 Z above an explicit ground-floor goal.
3. **Cooldown backoff 1 s→2 s→4 s→8 s→15 s cap** on consecutive fully-failed cycles
   (reset on any success) — a flat 1 s retry of up to 5 A* searches is a hot loop in
   principle; backoff makes a permanently blocked goal settle to near-zero CPU.

**Ops note found while testing:** `pkill -f "ClassicUO.RestAPI"` takes several seconds to
actually take the client down (graceful .NET shutdown). The old runbook's `sleep 1` raced
it, leaving two clients logged in as Gemma (one zombie spun ~99% CPU until killed). The
runbook (project README + this doc) now waits for the process to be gone before relaunching.
Also: measure client CPU with `top -l 2 -s 5 -pid <pid> -stats pid,cpu`, not
`ps pcpu`/`ps -o time` (both gave bogus readings on this box).

### Item 04 — completion record (2026-08-25)

Implemented, tested live, committed as `25973d`. All spec tests passed:

- **Perf (failing search):** ~235-tile unreachable target (the mountain mass) from open
  ground → `{"pathFound":false}` in **0.16–0.18 s** (bar: <1 s). Caveat: the searchable
  region is bounded by *loaded* map chunks, so a ground pathfind can't exhaust the 20k
  budget — the bar is met with margin and the add path is scan-free by construction.
- **Perf (successful search):** 22-tile open-ground path found and walked to completion.
- **Toggle in a monster field** (countryside N of the swamp — wolves/bears/ettins/pigs):
  same 30-tile target → **OFF: pathLength 32 (detour around the wolf), ON: pathLength 27
  (straight through)**. `player.ignoreMobiles` flips with `./ai/uo ignoremobiles on|off`;
  process restart resets to default off.
- **Regressions:** short `goto` hop OK; `say` during an active pathfind lands in the journal.

**Deviations (documented in `04-pathfinder-cap-and-mobile-obstacles.md`):** free-slot
allocation for *both* pools uses `Stack<int>` index stacks (the acceptance bar forbids any
linear pool scan in the add path); the open→closed move was extracted to
`MoveToClosedList(openIndex)` (it needs the open index to refund `_freeOpen`); the start
node is now allocated from `_freeClosed` (no hardcoded slot-0). `FindCheapestNode` stays
linear per spec. The pre-existing double-cost quirk in the open-node update was preserved
(behavior parity).

**Ops findings from the field trip to the monster field (all hit live):**
1. **Long idle → stuck walk state.** After ~35 min of no movement, *all* walks fail with
   normal diagnostics; `./ai/uo resync` cleared it (worked again here).
2. **Hilly terrain breaks 25-tile travel hops.** The swamp/ridge country north of the old
   spawn has z=5 ridges and cliffs: a 25-tile segment hop fails when the only route is a
   long detour *outside the loaded-chunk window* (72-tile detours work, longer don't).
   `travel` then backs off and sits. Workaround that worked: 15-tile `pathfind` hops in a
   loop (see `/tmp/hop.sh` pattern — 15t hops with 8t/5t fallback + resync on no-progress).
3. **The countryside N of the swamp has real spawns**: grey/timber wolves, grizzly bears,
   ettins, pigs, horses, sheep (z=0 and z=5). Gemma **died to a grey wolf twice** testing
   the ON route (HP 10 → 0).
4. **Resurrection gump = healer-offered, not automatic.** First death: "resurrected here
   by this healer" gump appeared and CONTINUE (buttonID 1) worked (full-ish HP, spot of
   death). Second death: no gump after 10+ min — no healer nearby. Login while dead does
   NOT auto-resurrect on this server. Left a 30-min background poller
   (`/tmp/resurrect.sh` → `/tmp/resurrect.log`) watching for the gump.

## 3. Workstream B — Playable harness (the goal)

### The core problem (why current play is bad)

**The LLM is in the hot path of every game action.** UO is a stream of small, fast decisions;
the current harness makes each one a full LLM round-trip (observe 2–10k tokens → think → ONE
action → verify). That causes: (1) slowness — the trip toward Jhelom took 30+ min of 25-tile
hops; (2) cost — `player` alone is ~8k tokens, ~90% unused; (3) fragility — context
compaction wipes *intent* (after compaction the agent doesn't know its goal/plan).

Observed symptom of no coherent strategy: Gemma was played and is now at gold **0**, skills
scattered (Meditation 36.8, Wrestling 31.7, Anatomy 7.5, **Magery still 30.0**) — no focus.

### The plan (three layers)

- **L0 — API/client primitives (Workstream A).** Reliable `travel`, `resync`, action acks,
  honest state. Mostly done after items 03–09. This is the substrate.
- **L1 — deterministic task engine (NEW, build in the C# API).** Long-running tasks the LLM
  starts/monitors/cancels; the engine executes them in the game loop (same place travel's
  driver lives). Proposed endpoints: `POST /api/tasks` (type + params), progress exposed in
  `player` JSON, `POST /api/tasks/stop`. Task types to build first:
  - `goto-town <name>` — coords already in `.agents/skills/uo/knowledge/map/*.md` (exact)
  - `gather <item> <count>` — find, approach, grab, repeat
  - `train <skill>` — walk to trainer, say "train", pay, repeat until gain/unaffordable
  - `sell <filter>` — walk to vendor, sell matching backpack items
  - `meditate` / `idle` — regenerate mana when low
  Design: each task is a state machine on the game thread; re-path via travel; self-heal on
  stuck (resync); report progress + terminal result to the journal so the LLM sees outcomes.
- **L2 — compact screen + persistent state (NEW).** One `./ai/uo screen` command emitting a
  fixed-budget view (~1k tokens, not 8k): position, active task + progress, vitals, gold,
  top skills, nearby entities, open gumps, recent journal. Plus a durable `state/` file the
  LLM reads/writes each turn: current goal → subgoal → next waypoint → learned facts
  (bank location, trainer cost, etc.). This is the `NOTES/TODO/MEM` idea from
  `ai/agent/DESIGN.md` (which is well-designed and worth reusing) adapted to *my* play loop.

**Model economics:** good model for strategy + conversation (rare); deterministic code for
execution (always); optionally a cheap model for roleplay chat. Don't pay frontier price for
every micro-step.

### Open decision (ask the user)

The `uoagent` (Python loop + its own LLM, `ai/agent/`) already exists and its design matches
this shape. Decide: wire the L1 task engine into **(a) me through the `uo` skill** (my session
+ `./ai/uo screen` + state file), **(b) the uoagent run unattended**, or **(c) shared task
engine with both**. Recommendation: build L1 in C# either way (shared), then wire into the
skill first (that's what the user is watching), keep uoagent as a second consumer.

## 4. Operational runbook

- **API**: `http://127.0.0.1:9000/api`. **CLI**: `./ai/uo` from repo root (`./ai/uo help`).
- **Character**: Gemma, serial `0x7AEB` (decimal 31467), Trammel. As of this doc: **DEAD**
  at `(1453,2072,0)` (killed by a grey wolf while item-04 testing) — ghost standing in the
  countryside monster field N of the swamp; a 30-min poller was watching for the healer
  resurrection gump (`/tmp/resurrect.log`). If still dead next session: either poll for the
  gump (healer-offered, see item-04 notes), or restart the client and wait — or accept the
  walk-to-a-shrine slog (Spirituality shrine (1589,2485,5) is nearest, ~430t). **Gold 0**,
  weight ~27, Wrestling now 32.0. A second player character, **Shanley**, was sighted
  roaming this same area (1464,2107,0) — possibly the user's alt.
- **Build + restart the headless client** (required to test any C# change):
   ```bash
   dotnet build src/ClassicUO.RestAPI/ClassicUO.RestAPI.csproj
   pkill -f "ClassicUO.RestAPI"
   # .NET shutdown often HANGS past 40 s ("Application is shutting down..." with the game
   # thread stuck) — wait for the real client binary, then KILL. Two gotchas:
   #   * pgrep -f "ClassicUO.RestAPI" matches YOUR OWN shell (its cmdline contains the
   #     pattern) — use the binary path instead.
   #   * TERM alone is often not enough; KILL after ~40 s.
   for i in $(seq 1 40); do sleep 1
     pgrep -f "bin/Debug/net10.0/cuo-api" >/dev/null || break; done
   pgrep -f "bin/Debug/net10.0/cuo-api" >/dev/null && pkill -9 -f "bin/Debug/net10.0/cuo-api"
   pgrep -f "dotnet run --project.*ClassicUO.RestAPI" >/dev/null && pkill -9 -f "dotnet run --project.*ClassicUO.RestAPI"
   sleep 2
   nohup ./run_rest_client.sh > /tmp/cuo-api.log 2>&1 &
   for i in $(seq 1 45); do ./ai/uo status | grep -q '"inGame":true' && break; sleep 2; done
   ./ai/uo status   # then: ./ai/uo stopwalk
   ```
   Autologin is on; character reloads in place. If it never goes inGame, read `/tmp/cuo-api.log`.
- **Test safety**: no attacking, no `warmode on`, no buying/selling during dev tests.
  Movement/speech/emote are fine.
- **Known transient walk-freeze**: position stays put with ALL diagnostics normal
  (`isParalyzed:false`, `walker:{walkingFailed:false,stepsCount:0}`). Happens after long
  idle stretches. `./ai/uo resync` clears it reliably (worked repeatedly). Do NOT chase it
  client-side; resync and move on.
- **Shell is zsh**: unquoted `$var` does NOT word-split — use explicit `&&` chains in test
  loops (a `for t in "x y"; do uo goto $t; done` loop silently passed "x y" as one arg).
- **Pathfinder facts**: `WalkTo` is synchronous A*, ≤**20k** nodes (item 04), O(1) node
  bookkeeping (sets/dicts + free-index stacks), ~0.2 s worst case observed for failing
  searches; returns node count incl. start tile (0 = no path). Waypoint calls use
  `distance:0` (auto-bumps to 1 if blocked). UO north = y−1. `Pathfinder.IgnoreMobileObstacles`
  (REST: `POST /api/actions/ignoremobiles`, CLI: `uo ignoremobiles on|off`, player field
  `ignoreMobiles`) lets A* route through standing mobiles; default off.

## 5. World context (near spawn + the original task)

- Original user task (still open): **find a bank and open the bank box.** Nearest town with a
  bank is **Britain (1475,1645,20)** (capital, biggest bank) or **New Haven (3506,2570,14)**
  (new-player hub with a bank) — the earlier "Jhelom at (1788,2090)" note was WRONG; Jhelom
  is at (1414,3816,0) per the server's location data. Exact town/dungeon/moongate coords are
  in `.agents/skills/uo/knowledge/map/*.md` — use those, don't guess.
- **Warriors' Guild** (the building Gemma was stuck in, ~ (1340-1360, 1724-1757)): a maze —
  floors at z=32/22/20 (mezzanine)/2; the z=20 mezzanine is a dead-end pocket; the exit is
  a step DOWN on the south side (~y=1758) into open ground (z≈17, sloping to 0). Multi-floor
  stair auto-walks are unreliable (walk-freezes mid-stair); `travel` with an explicit goal Z
  eventually works, single-floor `goto` is more reliable inside.
- **Britain → old-spawn corridor** (x≈1330-1550, y≈1758-2000): open farmland, scattered
  ground reagents + trees (single-tile walls), a wall line at x≈1540 (gap to the north,
  y≈2002). The ESE mountain mass (target (1706,2010)) is confirmed impassable on foot —
  good permanent "unreachable goal" test target. Open ground to the north.
- **Swamp/ridge country** (x≈1440-1500, y≈2070-2170, between the farmland and the old spawn):
  hilly — z=0 valleys, z=5 ridges with cliffs, a z=-1 cave room (stairs at (1472,2151));
  ground reagents everywhere (Nightshade, Ginseng, Blood Moss, Black Pearl); **live monster
  spawns** (grey/timber wolves, grizzly bears, ettins, pigs, horses, sheep — aggressive when
  approached). 25-tile travel hops fail here (detours exceed the loaded-chunk window); use
  15-tile `pathfind` hops instead (item-04 notes, finding 2).
- **Britain moongate (1336,1997,5)** — the Trammel public moongate (per
  `knowledge/map/moongates.md`; gates to Jhelom/Moonglow/New Haven/etc.). The old "dead
  moongate" note from a prior session is UNVERIFIED — retry properly: `./ai/uo use <serial>`
  (or walk onto it) → `./ai/uo gumps` → `gump-respond` with the destination button.
- The ground is littered with sellable reagents (Black Pearl, Sulfurous Ash, Spider Silk,
  Nightshade, Blood Moss, Mandrake) — the natural first gold loop: gather → sell in town →
  train Magery (vendors take to 40) → repeat. Long-term goal (in `ai/agent/profiles/gemma`):
  save ~100,000 gp for a mage tower and reach Magery 100.0 (grandmaster).

## 6. Key files

- `ai/projects/HANDOFF.md` — this file.
- `ai/projects/uo-api-improvements/README.md` + `01..09-*.md` — API fix plan + specs.
- `src/ClassicUO.RestAPI/` — the C# API:
  - `Api/Controllers/ActionsController.cs` — action endpoints (`resync`, `travel`, pathfind acks, `EnqueueRun<T>`)
  - `ApiGameController.cs` — game-loop glue; item-03 travel driver lives here
  - `Api/ActionQueue.cs` — `Enqueue` + `EnqueueAwait<T>`
  - `Api/TravelState.cs`, `Api/Models/TravelDto.cs`, `Api/Models/PlayerDto.cs` — travel + player fields
  - `Api/WorldSnapshot.cs` — `TileGrid` (radius consts 20/10, item-08 target), snapshot pattern
- `src/ClassicUO.Client/Game/Pathfinder.cs` — `WalkTo` (returns int),
  `PATHFINDER_MAX_NODES=20000`, O(1) bookkeeping (`_openKeys`/`_openIndex`/`_closedKeys` +
  `_freeOpen`/`_freeClosed` stacks, `MoveToClosedList`), `IgnoreMobileObstacles` static
  toggle, mobile-obstacle condition (~line 73/150).
- `src/ClassicUO.Client/Game/GameObjects/PlayerMobile.cs` — `Walk()` guard (~line 525);
  `Game/Managers/WalkerManager.cs` — `WalkingFailed`/`StepsCount`; `Game/Constants.cs:16`
  `MAX_STEP_COUNT=5`.
- `ai/uo` — bash wrapper (subcommands incl. `resync`, `travel`).
- `.agents/skills/uo/SKILL.md` + `.agents/skills/uo/knowledge/` — skill docs + world knowledge.
- `ai/agent/` — the separate `uoagent` Python loop (DESIGN.md is a strong reference for L2).
- `run_rest_client.sh`, `.restapi.toml` (gitignored) — client launch/config.
