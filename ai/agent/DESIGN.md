# uoagent — Design Document

A standalone, endless-loop agent that plays Ultima Online through the ClassicUO REST API
(`src/ClassicUO.RestAPI`, default `http://127.0.0.1:9000`). It acts as a companion when a
human is present and pursues its own goals (skills, resources, exploration) when alone.

This document is the source of truth. It is written so that a smaller model (or a human)
can implement, extend, or debug the system without re-deriving the design.

---

## 1. The core idea: a rendered context, not a conversation

Every existing LLM harness (Claude Code, chat UIs) accumulates messages until the context
overflows, then compacts. That model is wrong for a game agent: the world state goes stale
the moment it's emitted, chat logs get truncated, and token use grows without bound.

**uoagent instead re-renders the entire prompt from persistent state every turn.**

```
turn N:   prompt = render(state)  →  LLM  →  parse reply  →  apply(state, actions)
turn N+1: prompt = render(state)  →  ...
```

- The prompt has a **fixed token budget**, section by section. It never grows.
- There is **no message history**. What persists between turns is *state*:
  ring buffers (chat, journal, world snapshots), the agent's own notes/todo,
  and long-term memory files.
- **Compaction never happens** because nothing accumulates. The agent manages its own
  continuity through the NOTE/TODO/MEM commands (section 5).

Think of it as a terminal UI for an LLM: the screen redraws every frame; the model's job
each frame is to look at the screen and press keys.

### Prompt structure (order matters, for provider prefix-caching)

```
[system]  PERSONALITY.md  +  PROTOCOL SPEC (from code)  +  manual/MANUAL.md
          — static per profile → cacheable prefix (Anthropic cache_control /
            OpenAI-compat automatic prefix cache)

[user]    THE SCREEN — rendered fresh each turn, fixed budget:
          == TIME / COMPANY ==   computed presence line: current clock + either
                                 "<name> spoke Ns ago — you are in conversation" or
                                 "you are ALONE — pursue your GOALS". Small models
                                 cannot infer this from timestamps; say it outright.
          == GOALS ==            profile GOALS.md (short, standing directives)
          == NOTES ==            agent-maintained pinned notes (numbered)
          == TODO ==             agent-maintained todo list (numbered)
          == MEMORY INDEX ==     one line per long-term memory file
          == RECALLED ==         body of MEM-read files (shown for one turn only)
          == HISTORY ==          digests of the previous 3 world snapshots (diffs)
          == NOW ==              full current world state: status, player, map, entities, gumps
          == CHAT ==             ring buffer of speech, FULL TEXT, never truncated mid-line
          == EVENTS ==           ring buffer of system/journal lines
          == LAST TURN ==        the commands you issued last turn + their results
          == INSTRUCTION ==      one fixed closing line: respond using the protocol
```

### Token budgeting

- Estimator: `tokens ≈ len(text) / 4` (chars/4). Good enough; do not add a tokenizer dep.
- Each section has a budget in `profile.toml` (`[budget]`, values in tokens).
- Enforcement is per-section, oldest-entries-dropped-first (for rings) or tail-truncated
  (for NOW, which should instead reduce entity list rows beyond budget).
- Sections do **not** borrow from each other — predictability beats packing efficiency.
- Chat lines are never truncated mid-line: a line either fits whole or the *oldest* lines
  are dropped to make room. (This fixes the 68-char truncation in `/api/summary`.)

Suggested defaults (a ~10–14k dynamic screen + ~4–6k static system):

```toml
[budget]
goals        = 300
notes        = 600
todo         = 200
memory_index = 300
recalled     = 800
history      = 600
now          = 2200
chat         = 1500
events       = 500
last_turn    = 600
```

---

## 2. The turn protocol (model output format)

**Decision: a line protocol, not native tool calling.** Native tool-call formats differ
per provider, small open-weight models emit malformed JSON, and schemas cost tokens.
A line protocol works on *any* instruct model, including 7B-class.

The model replies with plain text; each line starting with a known verb is a command.
Verbs are case-insensitive. Markdown fences are stripped before parsing. Lines that match
no verb are treated as THINK (ignored but logged). The spec below is embedded verbatim in
the system prompt (single source of truth: the `PROTOCOL_SPEC` constant in `protocol.py`;
the manual must NOT duplicate it).

```
THINK: <reasoning>                     ignored by the engine; use freely
SAY: <text>                            speak in-game (also: EMOTE:, YELL:, WHISPER:)
DO: <command> <args>                   perform one game action (max 3 DO per turn)
NOTE: <text>                           pin a numbered note to your screen
NOTE DEL <n>                           delete pinned note n
TODO: <text>                           add a numbered todo item
TODO DONE <n>                          complete todo item n
MEM SAVE <slug>: <text>                write long-term memory file <slug>
MEM READ <slug>                        show memory <slug> on next screen
MEM DEL <slug>                         delete memory <slug>
WAIT: <seconds>                        sleep before your next turn (clamped to profile min/max)
```

`DO:` commands mirror the `./ai/uo` CLI verbs and map 1:1 to REST endpoints:

| DO command                      | REST call                                          |
|---------------------------------|----------------------------------------------------|
| `move <dir> [run]`              | POST /api/actions/move                             |
| `goto <x> <y> [z]`              | POST /api/actions/pathfind (coords)                |
| `follow <serial>`               | POST /api/actions/pathfind (serial)                |
| `stopwalk`                      | POST /api/actions/stopwalk                         |
| `use <serial>`                  | POST /api/actions/use                              |
| `attack <serial>`               | POST /api/actions/attack                           |
| `warmode on|off`                | POST /api/actions/warmode                          |
| `target <serial>` / `target <x> <y> <z>` / `target cancel` | POST /api/actions/target |
| `opendoor`                      | POST /api/actions/opendoor                         |
| `grab <serial> [amount]`        | POST /api/actions/grab                             |
| `pickup <serial> [amount]`      | POST /api/actions/pickup                           |
| `equip [container]`             | POST /api/actions/equip                            |
| `drop <serial> [container]` / `drop <serial> <x> <y> <z>` | POST /api/actions/drop |
| `skill <index>`                 | POST /api/actions/skill                            |
| `cast <index>`                  | POST /api/actions/spell                            |
| `backpack` / `container <serial>` | GET /api/containers/... → result shown in LAST TURN |
| `player`                        | GET /api/player → result shown in LAST TURN        |
| `paperdoll <serial>`            | GET /api/world/mobiles/{serial}/paperdoll → LAST TURN |
| `gumps`                         | GET /api/gumps → LAST TURN                         |
| `gump <id> <button> [switches]` | POST /api/gumps/{id}/respond                       |
| `gumpclose <id>`                | DELETE /api/gumps/{id}                             |
| `shop` / `buy <serial> [qty]` / `sell <serial> [qty]` | /api/shop endpoints          |

Serials accept `0x…` hex or decimal (convert hex → decimal before JSON, unsigned).

**Parser rules (be forgiving — this is what makes small models viable):**
1. Strip ``` fences and leading list markers (`- `, `* `, `1. `).
2. Match `^\s*(VERB)\s*[:\-]?\s*(.*)$` case-insensitively per line.
3. Unknown verb → THINK. Empty reply → engine appends a one-turn system note:
   *"Your last reply contained no commands. Use the protocol (SAY:, DO:, WAIT:)."*
4. Execute DO commands sequentially with ~0.6 s spacing; stop the sequence early if one
   returns an error, and report all results in LAST TURN.
5. Query-type DOs (backpack, player, gumps, …) print their JSON (truncated to the
   `last_turn` budget) in the next screen's LAST TURN section.

---

## 3. The main loop and pacing

```python
while True:
    poll_journal()                # GET /api/journal?since=<last_ts> → route lines to chat/events rings
    poll_world()                  # GET /api/summary (+/api/gumps) → push snapshot, compute digest
    screen = render(state)        # section 1
    reply  = llm(system, screen)  # providers, section 6
    cmds   = parse(reply)
    results = execute(cmds)       # REST calls; SAY/NOTE/TODO/MEM applied to state
    state.last_turn = (cmds, results)
    persist(state)                # state/rings.json, notes.md, todo.md — restart-safe
    sleep_until(wait_seconds)     # interruptible, see below
```

**Pacing.** The model controls its own cadence with `WAIT: <seconds>`, clamped to
`[pacing.min_wait, pacing.max_wait]` (defaults 2/120 s); no WAIT → `pacing.default_wait`
(20 s). In conversation it should wait 2–5 s; grinding skills alone, 30–120 s. This is
prompt-taught, not code-enforced.

**Wake-on-speech (companion mode).** A background thread holds open
`GET /api/events` (SSE). When an event arrives whose type is speech and whose speaker is
not the agent itself, it sets a wake event; `sleep_until` returns early and the next
screen gets a one-turn banner: `== WOKEN == <name> said: "<text>"`. This is what makes it
feel like a companion — instant response to a human, slow heartbeat otherwise.
If SSE is unavailable, degrade to polling the journal at `min_wait` while sleeping.

**Failure handling.**
- REST error / timeout → recorded in LAST TURN as `ERROR <endpoint>: <msg>`; loop continues.
- LLM API error → exponential backoff 5 s → 10 → 20 → … capped at 5 min; never crash.
- `/api/status` shows disconnected → pause, poll every 10 s, note reconnection on screen.
- Every turn appended (prompt hash, reply, commands, results) to `state/transcript.log`
  for offline debugging. The transcript is NEVER fed back into context.

---

## 4. World state: NOW, HISTORY, CHAT, EVENTS

**NOW** — the current world. v1 sources it from `GET /api/summary` (text) because that is
the only place the ASCII tile map exists. Split on the 76-char `─` divider lines; keep the
status/player/map/entities sections; DROP the journal section (we manage chat ourselves,
untruncated). Append open gumps from `GET /api/gumps` (id, buttons, text) — a visible gump
is almost always the most important thing on screen. If the entity list exceeds the `now`
budget, drop the most distant entities first, then note `(+N more beyond Xt)`.
*(Later, M3: a `GET /api/map` JSON endpoint so rendering is fully client-side.)*

**HISTORY** — the previous 3 snapshots as *digests*, not full maps (a full map is ~300
tokens; diffs carry more signal for fewer tokens). A snapshot record keeps: timestamp,
(x,y,z), hp/mp/stam, and `{serial: (name, x, y, dist)}`. A digest of consecutive snapshots:

```
-3m12s  (1336,1997) HP62/62 MP46/46 | appeared: Beau the herbalist NPC 8t NE | gone: a rabbit
-1m40s  (1340,1995) HP62/62 MP41/46 | moved: Beau 8t→3t | you cast Night Sight
```

**CHAT** — journal entries that are *speech*: `name` ≠ "System" and `messageType` in
{Regular, Yell, Whisper, Emote} (tune against live data; entries carry `textType` of
SYSTEM/OBJECT/CLIENT…). Ring of ~200 entries, rendered newest-last, full text,
`HH:MM:SS [Name] text`. Own lines included but marked `[Name (you)]` — without the
marker, small models answer their own greetings in an endless loop (observed with
gpt-5.4-nano). The section header spells it out: "'(you)' marks YOUR OWN past words —
never answer them."

**Presence & pacing guards** (small-model lessons, all engine-side):
- `company_status()` — someone else's chat within `pacing.company_window` (180 s) =
  in conversation; otherwise ALONE. Rendered in TIME / COMPANY and used by the loop.
- While ALONE, the wait floor is `pacing.alone_min_wait` (20 s) regardless of what the
  model asked for — prevents 3-second polling of an empty room.
- Standing on the same tile for 4+ snapshots while ALONE → one-turn NOTICE nudging the
  agent to work a TODO or explore.
- **Aftermath feedback**: after executing game actions, the loop sleeps ~1.5 s, re-polls
  the journal, and appends everything that just happened to LAST TURN under
  "— what happened next:". Without this the model only ever sees "ok cast: done" and
  cannot tell success from fizzle.
- Every successful `cast` result carries a reminder that spells do nothing until
  targeted (`DO: target <serial>`). *(M2: expose `TargetManager.IsTargeting` — one line
  in WorldSnapshot + StatusController — and render "TARGET CURSOR ACTIVE" in NOW.)*

**EVENTS** — everything else (skill gains, spell results, system messages). Same ring
mechanics, smaller budget. Noise filter: collapse consecutive "world save" pairs, etc. —
keep a small `NOISE_PATTERNS` list in code.

Rings persist to `state/rings.json` each turn so a restart loses nothing.

---

## 5. Memory, notes, todo — continuity without history

Three tiers, all agent-driven (mirrors Claude Code's own memory design):

1. **NOTES** — working memory. Numbered, always on screen, budget ~600 tokens. When full,
   the renderer shows `NOTES FULL — delete stale notes with NOTE DEL <n>`; oldest notes
   are marked but never silently deleted (the agent must manage them — that's the skill).
2. **TODO** — same mechanics, for intentions. `TODO:` add, `TODO DONE <n>`.
3. **MEM** — long-term. Files at `profile/memory/<slug>.md`, one fact/topic per file,
   first line is the description. `memory/MEMORY.md` is the always-visible index
   (`- slug — description`), regenerated on every MEM SAVE/DEL. `MEM READ slug` shows the
   body in RECALLED for exactly one turn. The manual teaches: *save maps, prices, people,
   and lessons learned; read before deciding; your notes are working memory, MEM is your
   diary.*

---

## 6. Providers

Two thin adapters, plain `urllib.request` (zero pip dependencies):

- **anthropic** — POST `{base}/v1/messages`, headers `x-api-key`, `anthropic-version:
  2023-06-01`. System as top-level `system: [{type:"text", text:…, cache_control:
  {type:"ephemeral"}}]` for prefix caching. Single user message = the screen.
- **openai** — POST `{base}/chat/completions`, `Authorization: Bearer …`. Works for
  OpenAI, DeepSeek (`https://api.deepseek.com`), OpenRouter, Ollama
  (`http://localhost:11434/v1`), vLLM, LM Studio. Messages: `[{role:system},{role:user}]`.

Config in `profile.toml`; API keys only via environment variables (`api_key_env`).
`max_tokens` ~800 (a turn is a few lines), temperature ~0.7.

Because every turn is `1 system + 1 user`, weak-model quirks (lost-in-the-middle, poor
multi-turn coherence) are largely bypassed — the model only ever answers one question:
*"here is your screen; what do you do?"*

---

## 6b. Telemetry — GenAI traces over OTLP/HTTP

`telemetry.py` exports one trace per turn to `<otlp_endpoint>/v1/traces` (standard
collector port 4318 — Jaeger, Tempo, Alloy, Phoenix, Langfuse). Hand-rolled OTLP JSON,
zero dependencies, fire-and-forget on a daemon thread (never blocks a turn; warns once
if the collector is down).

Span layout per turn, following the OpenTelemetry GenAI semantic conventions:
- `uoagent.turn` (INTERNAL) — attributes: `uoagent.profile`, `uoagent.turn_number`,
  `uoagent.results`, `uoagent.wait_seconds`, `uoagent.screen_tokens_est`.
- └ `chat <model>` (CLIENT) — `gen_ai.operation.name=chat`, `gen_ai.system`,
  `gen_ai.request.model`, `gen_ai.usage.input_tokens/output_tokens` (from the provider's
  usage block); span events `gen_ai.content.prompt` (full system + rendered screen —
  exactly what the agent sees) and `gen_ai.content.completion` (the raw reply).

Config: `[telemetry] otlp_endpoint` ("" disables), `capture_content` (set false to keep
prompts out of the collector), `service_name` (default `uoagent-<profile>`), so each
character shows up as its own service in the trace UI.

## 7. Profiles — one directory per character

```
ai/agent/
  DESIGN.md                 this file
  README.md                 quickstart
  uoagent/                  python package (section 8)
  manual/MANUAL.md          shared game manual (world knowledge, safety rules, skill/spell
                            tables — adapted from .claude/commands/uo.md, with all shell
                            syntax replaced by protocol syntax)
  profiles/
    gemma/
      profile.toml          api base, llm provider/model, budgets, pacing
      PERSONALITY.md        who this character is, voice, values, companion behavior
      GOALS.md              standing directives when alone (train magery, gather regs, …)
      memory/MEMORY.md      long-term memory index + files (agent-written, committed ok)
      state/                runtime state — gitignored (notes.md, todo.md, rings.json,
                            transcript.log)
```

Run N characters = N profile dirs + N headless clients on distinct ports:

```
python3 -m uoagent --profile ai/agent/profiles/gemma
```

**Prerequisite for N>1 (M3):** the API port is hardcoded to 9000 in
`src/ClassicUO.RestAPI/Program.cs` (`ApiServer.Start(actionQueue, eventBus, 9000)`).
Add a `-apiport` CLI flag / `UO_API_PORT` env var, thread it through, and add a
`--port` passthrough in `run_rest_client.sh`. Each profile.toml then points `[api] base`
at its own port. A `scripts/launch-party.sh` can tmux-spawn client+agent pairs.

---

## 8. Package layout (implementation map)

```
uoagent/
  __main__.py    argparse (--profile, --once for a single debug turn, --dry-run to print
                 the rendered screen and exit), wire modules, run loop
  config.py      load profile.toml + defaults; dataclasses Profile/Budgets/Pacing/Llm
  restapi.py     thin client: get/post JSON, get_text, hex/dec serial helper, SSE reader
  state.py       AgentState: rings, notes, todo, snapshots, last_turn; load/persist
  rings.py       Ring (deque with max entries) + journal routing (chat vs events vs noise)
  snapshots.py   snapshot capture from /api/summary + /api/player, digest(diff) rendering
  screen.py      render(state, budgets) → the user message; per-section budget enforcement
  protocol.py    PROTOCOL_SPEC constant; parse(reply) → [Command]; DISPATCH table
                 Command → restapi call; execute() with spacing + result capture
  memory.py      MEM save/read/del, MEMORY.md index regeneration; notes.md/todo.md io
  providers.py   complete(provider_cfg, system, user) → str; anthropic + openai adapters;
                 backoff; both via urllib (no pip deps)
  loop.py        the main loop (section 3), SSE waker thread, sleep_until
```

Target: each file < ~200 lines, stdlib only, Python ≥ 3.11 (tomllib).

---

## 9. Milestones

- **M1 — playable core** (this session): package skeleton, config, REST client, rings,
  renderer, protocol parser + dispatch, both providers, main loop with polling wake,
  gemma profile, manual. *Accept: `--once` prints a sane screen; a live turn executes
  SAY/DO/WAIT against the running client.*
- **M2 — companion polish**: SSE wake-on-speech, snapshot digests (M1 may ship
  position-only digests), noise filters, NOTES-full pressure UX, restart-safe rings.
  *Accept: speaking to the agent in-game gets a response within ~5 s while it idles at
  60 s waits.*
- **M3 — multi-instance**: `-apiport` in C#, run_rest_client.sh passthrough,
  launch-party script, second profile. *Accept: two characters online, chatting with
  each other.*
- **M4 — evaluation**: replay transcripts through candidate models scoring protocol
  adherence (% turns with ≥1 valid command) and task probes; tune manual/budgets for
  DeepSeek-class and Haiku-class models. *Accept: a 20-turn unattended Haiku run with
  zero engine errors and >90% valid-command turns.*

## 10. Deliberate non-goals (v1)

- No native tool-calling mode (adapter could be added behind `protocol.py` later).
- No hard safety rails in code (attack rules etc. live in MANUAL.md; revisit if agents
  misbehave on shared servers).
- No image/tile rendering; ASCII map only.
- No inter-agent bus; agents coordinate in-game (say/whisper), which is the point.
