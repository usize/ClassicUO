# uoagent — quickstart

A fixed-context LLM agent that plays UO through the ClassicUO REST API. Full design:
[DESIGN.md](DESIGN.md). No pip dependencies; Python ≥ 3.11.

```bash
# 1. Start the headless client (repo root)
./run_rest_client.sh

# 2. Preview the rendered screen (no LLM call)
cd ai/agent
python3 -m uoagent --profile profiles/gemma --dry-run

# 3. Play
export ANTHROPIC_API_KEY=sk-ant-…
python3 -m uoagent --profile profiles/gemma          # endless loop
python3 -m uoagent --profile profiles/gemma --once   # single turn (debug)
```

Open-weight / other providers — edit `profiles/<name>/profile.toml`:

```toml
[llm]
provider = "openai"                       # any OpenAI-compatible server
model = "deepseek-chat"
base_url = "https://api.deepseek.com"
api_key_env = "DEEPSEEK_API_KEY"
# Ollama: base_url = "http://localhost:11434/v1", api_key_env can point at an unset var
```

New character = copy `profiles/gemma`, rewrite `PERSONALITY.md` and `GOALS.md`.
Each extra concurrent character needs its own headless client on its own port
(see DESIGN.md M3 — `-apiport` flag is not implemented yet).

Debugging: `state/transcript.log` records every turn (reply + results, JSONL).
`--dry-run` shows exactly what the model sees.
