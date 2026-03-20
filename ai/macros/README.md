# UO Macro Library

Reusable Python asyncio scripts for playing Ultima Online via the ClassicUO REST API.

Run from the **repo root**:
```bash
python3 ai/macros/<name>.py
```
Interrupt at any time with **Ctrl+C** — the game action stops instantly.

## Current Macros

| File | Description |
|------|-------------|
| `buy_reagents.py` | Navigate to Sancia in Haven, open shop, buy standard reagent stock |
| `meditate_full.py` | Meditate until mana is full |

**Note:** Long-running loops (training, grinding, patrolling) belong in `run.py` as
rules evaluated by the engine — not here. Macros are for one-shot tasks that finish.

## Heartbeat Protocol

All macros should call `escalate()` at key moments so Claude can check in via
`./ai/uo queue drain` without the macro stopping:

```python
from lib.engine import escalate

await escalate("macro_start",    macro="my_macro", pos=(x, y))
await escalate("heartbeat",      macro="my_macro", progress="halfway", ...)
await escalate("macro_complete", macro="my_macro", result="ok")
await escalate("macro_blocked",  macro="my_macro", reason="why_it_stopped")
```

Long loops (training, grinding) should emit a `heartbeat` every N iterations with
relevant stats (skill level, cast count, HP, MP).

---

## Adding a New Macro

Copy the template, fill it in, and add a row to this table:

```python
#!/usr/bin/env python3
"""One-line description."""
import asyncio, sys
sys.path.insert(0, "ai")

from lib.actions import say, emote
from lib.nav import smart_goto, player_pos

async def main() -> None:
    x, y, _ = await player_pos()
    print(f"Starting at ({x}, {y})")
    # your logic here

if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        print("\nInterrupted.")
```
