# UO REST API Improvements

Fixes for the navigation and observability shortcomings found during live play
testing of the `uo` skill (see session notes in git history of `ai_sandbox`).
Each numbered file is one self-contained work item: one subagent, one test pass,
one commit. Work them in order — later items build on earlier ones.

## Status

- [x] 01 — resync endpoint + walker diagnostics in player output (`bbc2f6e`)
- [x] 02 — pathfind returns pathFound/pathLength (introduces the awaited-action helper) (`3f7168f`)
- [ ] 03 — server-side chunked travel (`actions/travel`) with progress
- [ ] 04 — pathfinder: O(1) duplicate checks, higher node cap, mobile-obstacle toggle
- [ ] 05 — journal `limit` returns the most recent entries
- [ ] 06 — action endpoints return JSON acks (move/use/attack/say)
- [ ] 07 — rewrite `ai/uo goto` around travel + honest failure modes
- [ ] 08 — configurable summary map radius (`?radius=`)
- [ ] 09 — update SKILL.md to match the new behavior

## Shared context (read before starting any item)

**Layout**
- REST API (C#, .NET 10): `src/ClassicUO.RestAPI/`
  - `Api/Controllers/ActionsController.cs` — action endpoints (enqueue into ActionQueue)
  - `ApiGameController.cs` — game-loop glue: dequeues actions in `Update()`
  - `Api/ActionQueue.cs` — `ConcurrentQueue<Action>`
  - `Api/WorldSnapshot.cs` — static world snapshot + `TileGrid` (sampled on game thread)
  - `Api/Controllers/{Summary,Player,Journal,World,Container,Shop,Gump,Status}Controller.cs`
  - `Api/Models/*Dto.cs` — response shapes
- Client pathfinder: `src/ClassicUO.Client/Game/Pathfinder.cs`
- Client walk state: `src/ClassicUO.Client/Game/GameObjects/PlayerMobile.cs` (`Walk()` guard at line ~525),
  `src/ClassicUO.Client/Game/Managers/WalkerManager.cs`, `src/ClassicUO.Client/Game/Constants.cs` (`MAX_STEP_COUNT = 5`)
- Bash wrapper: `ai/uo` (721 lines, `case` statement of subcommands)
- Skill docs: `.agents/skills/uo/SKILL.md` (+ `knowledge/` reference folder)

**Threading model (important)**
The game runs a single XNA thread; all game-state mutation must happen there.
REST controllers run on HTTP threads and may only *enqueue* work
(`ActionQueue.Enqueue`), which executes in `ApiGameController.Update()`.
`WorldSnapshot` is the established pattern for game-thread-written state that
HTTP threads read (a static class, refreshed each tick).

**Build & restart (required to test any C# change)**
```bash
dotnet build src/ClassicUO.RestAPI/ClassicUO.RestAPI.csproj
pkill -f "ClassicUO.RestAPI"                      # stops dotnet run + cuo-api
# .NET shutdown takes a few seconds — wait for the old process to actually die,
# otherwise two clients log in as the same character and race for port 9000:
for i in $(seq 1 20); do pgrep -f "ClassicUO.RestAPI" >/dev/null || break; sleep 1; done
nohup ./run_rest_client.sh > /tmp/cuo-api.log 2>&1 &
# wait for relogin (autologin is on; character reloads in place):
for i in $(seq 1 45); do ./ai/uo status | grep -q '"inGame":true' && break; sleep 2; done
./ai/uo status
```
The character is Gemma (Trammel). After relogin, run `./ai/uo stopwalk` before
movement tests. If `inGame` never becomes true, check `/tmp/cuo-api.log`.

**In-game test safety**
- No attacking, no `warmode on`, no buying/selling (don't move gold).
- Movement, speech, emotes, and short-distance pathfinding are fine.
- Keep test movement within ~30 tiles of where the character stands.
- If the character appears frozen (position unchanged after a move), that is a
  known pre-existing condition — note it, don't chase it in items 01–02
  (item 01 adds the tools to diagnose/clear it).

**Git**
- One commit per work item, message given in the item file.
- Commit only files in the item's scope. Never commit `.restapi.toml`, `bin/`,
  `obj/`, or logs. `git status` must be clean (of your item's files) after commit.
