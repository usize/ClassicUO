# HANDOFF — UO AI Sandbox (read this first)

_Last updated: 2026-08-24 23:02 PDT, branch `uo-api-improvements`._

This repo is an experiment: let an LLM **play** Ultima Online through a local REST API
(`http://127.0.0.1:9000`) exposed by a headless ClassicUO client, and drive it with the
`./ai/uo` bash CLI + the `uo` skill. Two workstreams are in flight:

- **A — REST API fixes** (`ai/projects/uo-api-improvements/`): 9 numbered specs fixing
  navigation/observability bugs found during live play. Items **01–03 done**, 04–09
  not started.
- **B — Playable harness** (this doc, section 4): the bigger goal — a harness where the LLM
  is the *brain* (chooses tasks, socializes, reacts) and a deterministic engine is the *hands*
  (executes long repetitive tasks), with compact observations and persistent play-state.

**Immediate next action for a fresh session:** start item 04 (pathfinder O(1) node
bookkeeping, node cap 20k, mobile-obstacle toggle) per
`ai/projects/uo-api-improvements/04-*.md`. Read the item-03 completion notes below first —
they record three deliberate deviations (25-tile segments, z-aware arrival, cooldown
backoff) that item 04 interacts with.

---

## 1. Repo & git state

- Branch: `uo-api-improvements` (based on `ai_sandbox`).
- Recent commits (newest first):
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
| 04 | Pathfinder O(1) node bookkeeping, cap 20k, mobile-obstacle toggle | ⬜ not started |
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
- **Character**: Gemma, serial `0x7AEB` (decimal 31467), Trammel. As of this doc: at
  `(1539,2000,0)` — **outdoors**, open country at the W edge of the big mountain mass,
  south of Britain (left the Warriors' Guild during item-03 testing), **gold 0**, weight
  23/134, HP 63/63, MP 58/58. Skills: Meditation 36.8, Wrestling 31.7, Eval Int 30.7,
  Magery 30.0, Focus 16.5, Anatomy 7.5. Young-player protection (PvP-safe) was active at
  session start.
- **Build + restart the headless client** (required to test any C# change):
  ```bash
  dotnet build src/ClassicUO.RestAPI/ClassicUO.RestAPI.csproj
  pkill -f "ClassicUO.RestAPI"
  # .NET shutdown takes a few seconds — wait for the old process to actually die,
  # otherwise two clients log in as the same character and race for port 9000:
  for i in $(seq 1 20); do pgrep -f "ClassicUO.RestAPI" >/dev/null || break; sleep 1; done
  nohup ./run_rest_client.sh > /tmp/cuo-api.log 2>&1 &
  for i in $(seq 1 45); do ./ai/uo status | grep -q '"inGame":true' && break; sleep 2; done
  ./ai/uo status   # then: ./ai/uo stopwalk
  ```
  Autologin is on; character reloads in place. If it never goes inGame, read `/tmp/cuo-api.log`.
- **Test safety**: no attacking, no `warmode on`, no buying/selling during dev tests.
  Movement/speech/emote are fine.
- **Known transient walk-freeze**: position stays put with ALL diagnostics normal
  (`isParalyzed:false`, `walker:{walkingFailed:false,stepsCount:0}`). `./ai/uo resync` clears
  it reliably (worked repeatedly). Do NOT chase it client-side; resync and move on.
- **Shell is zsh**: unquoted `$var` does NOT word-split — use explicit `&&` chains in test
  loops (a `for t in "x y"; do uo goto $t; done` loop silently passed "x y" as one arg).
- **Pathfinder facts**: `WalkTo` is synchronous A*, ≤10k nodes, ~0.5 s worst case; returns
  node count incl. start tile (0 = no path). Waypoint calls use `distance:0` (auto-bumps to 1
  if blocked). UO north = y−1.

## 5. World context (near spawn + the original task)

- Original user task (still open): **find a bank and open the bank box.** Nearest town with a
  bank is **Jhelom at (1788,2090)** (Trammel). Exact town/dungeon/moongate coords are in
  `.agents/skills/uo/knowledge/map/trammel.md` — use those, don't guess.
- **Warriors' Guild** (the building Gemma was stuck in, ~ (1340-1360, 1724-1757)): a maze —
  floors at z=32/22/20 (mezzanine)/2; the z=20 mezzanine is a dead-end pocket; the exit is
  a step DOWN on the south side (~y=1758) into open ground (z≈17, sloping to 0). Multi-floor
  stair auto-walks are unreliable (walk-freezes mid-stair); `travel` with an explicit goal Z
  eventually works, single-floor `goto` is more reliable inside.
- **Britain → old-spawn corridor** (x≈1330-1550, y≈1758-2000): open farmland, scattered
  ground reagents + trees (single-tile walls), a wall line at x≈1540 (gap to the north,
  y≈2002). The ESE mountain mass (target (1706,2010)) is confirmed impassable on foot —
  good permanent "unreachable goal" test target. Open ground to the north.
- Near the old spawn (~1338,1997): a dead moongate at (1336,1997,5) (does nothing).
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
- `src/ClassicUO.Client/Game/Pathfinder.cs` — `WalkTo` (now int), `PATHFINDER_MAX_NODES=10000`,
  mobile-obstacle condition (~line 65/142), item-04 target.
- `src/ClassicUO.Client/Game/GameObjects/PlayerMobile.cs` — `Walk()` guard (~line 525);
  `Game/Managers/WalkerManager.cs` — `WalkingFailed`/`StepsCount`; `Game/Constants.cs:16`
  `MAX_STEP_COUNT=5`.
- `ai/uo` — bash wrapper (subcommands incl. `resync`, `travel`).
- `.agents/skills/uo/SKILL.md` + `.agents/skills/uo/knowledge/` — skill docs + world knowledge.
- `ai/agent/` — the separate `uoagent` Python loop (DESIGN.md is a strong reference for L2).
- `run_rest_client.sh`, `.restapi.toml` (gitignored) — client launch/config.
