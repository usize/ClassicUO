# ClassicUO REST API — AI Sandbox

A headless ClassicUO client that exposes a REST API on `localhost:9000`, letting an LLM
play Ultima Online by reading world state and issuing actions through simple shell commands.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A running [ModernUO](https://github.com/modernuo/ModernUO) server
- UO client data files (any OSI-compatible 7.x install)
- An account on the server created manually via the ModernUO console

## Build

```bash
dotnet build src/ClassicUO.RestAPI/ClassicUO.RestAPI.csproj
```

## Configure

Copy and edit the credentials file (it is gitignored):

```bash
cp ai/restapi.toml.example .restapi.toml
$EDITOR .restapi.toml
```

`.restapi.toml` fields:

```toml
uopath    = "/path/to/uo/data"   # folder containing UO client files
server    = "127.0.0.1"
port      = "2593"
username  = "myaccount"
password  = "mypassword"
character = "My Character"
```

## Run

```bash
./run_rest_client.sh
```

CLI flags override the config file for any field:

```bash
./run_rest_client.sh --username foo --password bar --char "Alt Char"
```

The REST API starts at `http://127.0.0.1:9000` once the character is in the world.
Check `./ai/uo status` to confirm.

## Play

**Claude Code** — open this repo in Claude Code, then:
```
/uo              # one turn: read world, decide, act, narrate
/loop 20s /uo   # autonomous play on a 20-second heartbeat
```

**Vibe** — from the repo root:
```bash
vibe             # picks up .vibe/config.toml automatically
/uo              # activates the UO skill
```

The `./ai/uo` script is the command interface — `./ai/uo help` for the full reference.
