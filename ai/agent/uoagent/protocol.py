"""The turn protocol: spec (embedded in the system prompt), parser, and dispatch.

Single source of truth for the model-facing command language. The manual must not
duplicate this text (DESIGN.md section 2).
"""

from __future__ import annotations

import json
import time
from dataclasses import dataclass

from . import memory
from .restapi import RestApi, to_serial
from .state import AgentState

PROTOCOL_SPEC = """\
## How to respond (the protocol)

Reply in plain text. Each line starting with a verb below is executed; anything else
is treated as your private thoughts. Issue at most 3 DO lines per turn, then WAIT.

THINK: <reasoning>              private; use freely
SAY: <text>                     speak in-game (also EMOTE:, YELL:, WHISPER:)
DO: <command> <args>            one game action (commands listed in the manual)
NOTE: <text>                    pin a numbered note to your screen (working memory)
NOTE DEL <n>                    delete pinned note n
TODO: <text>                    add a todo item
TODO DONE <n>                   complete todo item n
MEM SAVE <slug>: <text>         write long-term memory file (maps, people, prices, lessons)
MEM READ <slug>                 show a memory on your next screen
MEM DEL <slug>                  delete a memory
WAIT: <seconds>                 how long to pause before your next turn
                                (2-5 in conversation, 30-120 when grinding alone)

Example turn:
THINK: Beau sells reagents and I am low on black pearl.
DO: follow 0x00000F2A
SAY: Good day, Beau. I would like to buy reagents.
NOTE: Beau the herbalist, reagent vendor, near the west bank
WAIT: 4
"""

DIRECTIONS = {"north", "south", "east", "west", "ne", "nw", "se", "sw"}


@dataclass
class Command:
    verb: str  # normalized: say/emote/yell/whisper/do/note/note_del/todo/todo_done/mem_save/mem_read/mem_del/wait/think
    arg: str


def _strip_decorations(line: str) -> str:
    s = line.strip()
    for prefix in ("- ", "* ", "• "):
        if s.startswith(prefix):
            s = s[len(prefix):]
    if len(s) > 2 and s[0].isdigit() and s[1:3] in (". ", ") "):
        s = s[3:]
    return s.strip()


def parse(reply: str) -> list[Command]:
    cmds: list[Command] = []
    for raw in reply.splitlines():
        line = _strip_decorations(raw)
        if not line or line.startswith("```"):
            continue
        upper = line.upper()

        def rest_after(token: str) -> str:
            return line[len(token):].lstrip(":- \t")

        if upper.startswith("THINK"):
            cmds.append(Command("think", rest_after("THINK")))
        elif upper.startswith("NOTE DEL"):
            cmds.append(Command("note_del", rest_after("NOTE DEL")))
        elif upper.startswith("NOTE"):
            cmds.append(Command("note", rest_after("NOTE")))
        elif upper.startswith("TODO DONE"):
            cmds.append(Command("todo_done", rest_after("TODO DONE")))
        elif upper.startswith("TODO"):
            cmds.append(Command("todo", rest_after("TODO")))
        elif upper.startswith("MEM SAVE"):
            cmds.append(Command("mem_save", rest_after("MEM SAVE")))
        elif upper.startswith("MEM READ"):
            cmds.append(Command("mem_read", rest_after("MEM READ")))
        elif upper.startswith("MEM DEL"):
            cmds.append(Command("mem_del", rest_after("MEM DEL")))
        elif upper.startswith("WAIT"):
            cmds.append(Command("wait", rest_after("WAIT")))
        elif upper.startswith("SAY"):
            cmds.append(Command("say", rest_after("SAY")))
        elif upper.startswith("EMOTE"):
            cmds.append(Command("emote", rest_after("EMOTE")))
        elif upper.startswith("YELL"):
            cmds.append(Command("yell", rest_after("YELL")))
        elif upper.startswith("WHISPER"):
            cmds.append(Command("whisper", rest_after("WHISPER")))
        elif upper.startswith("DO"):
            cmds.append(Command("do", rest_after("DO")))
        else:
            cmds.append(Command("think", line))
    return cmds


# ── DO dispatch ──────────────────────────────────────────────────────────────

def _do(api: RestApi, arg: str) -> str:
    """Execute one DO command; return a short result line."""
    parts = arg.split()
    if not parts:
        return "ERROR do: empty command"
    cmd, args = parts[0].lower(), parts[1:]

    def post(path: str, body: dict) -> str:
        ok, text = api.post(path, body)
        text = text.strip()[:200]
        return f"{'ok' if ok else 'ERROR'} {cmd}: {text or 'done'}"

    def query(path: str) -> str:
        data = api.get_json(path)
        if data is None:
            return f"ERROR {cmd}: no response"
        return f"{cmd} → {json.dumps(data, ensure_ascii=False)}"

    try:
        if cmd == "move" and args:
            run = len(args) > 1 and args[1].lower() in ("run", "true")
            return post("actions/move", {"direction": args[0].lower(), "run": run})
        if cmd == "goto" and len(args) >= 2:
            body = {"x": int(args[0]), "y": int(args[1])}
            if len(args) >= 3:
                body["z"] = int(args[2])
            return post("actions/pathfind", body)
        if cmd == "follow" and args:
            return post("actions/pathfind", {"serial": to_serial(args[0])})
        if cmd == "stopwalk":
            return post("actions/stopwalk", {})
        if cmd == "use" and args:
            return post("actions/use", {"serial": to_serial(args[0])})
        if cmd == "attack" and args:
            return post("actions/attack", {"serial": to_serial(args[0])})
        if cmd == "warmode":
            enabled = not (args and args[0].lower() in ("off", "false"))
            return post("actions/warmode", {"enabled": enabled})
        if cmd == "target" and args:
            if args[0].lower() == "cancel":
                return post("actions/target", {"cancel": True})
            if len(args) >= 3:
                return post("actions/target", {"x": int(args[0]), "y": int(args[1]), "z": int(args[2])})
            return post("actions/target", {"serial": to_serial(args[0])})
        if cmd == "opendoor":
            return post("actions/opendoor", {})
        if cmd == "grab" and args:
            amount = int(args[1]) if len(args) > 1 else 0
            return post("actions/grab", {"serial": to_serial(args[0]), "amount": amount})
        if cmd == "pickup" and args:
            amount = int(args[1]) if len(args) > 1 else -1
            return post("actions/pickup", {"serial": to_serial(args[0]), "amount": amount})
        if cmd == "equip":
            body = {"container": to_serial(args[0])} if args else {}
            return post("actions/equip", body)
        if cmd == "drop" and args:
            body: dict = {"serial": to_serial(args[0])}
            if len(args) == 2:
                body["container"] = to_serial(args[1])
            elif len(args) >= 4:
                body.update(x=int(args[1]), y=int(args[2]), z=int(args[3]))
            return post("actions/drop", body)
        if cmd == "skill" and args:
            return post("actions/skill", {"index": int(args[0])})
        if cmd in ("cast", "spell") and args:
            r = post("actions/spell", {"index": int(args[0])})
            if not r.startswith("ERROR"):
                r += " — spells do NOTHING until targeted: follow with 'DO: target <serial>' (yourself for beneficial spells)"
            return r
        if cmd == "backpack":
            return query("containers/backpack")
        if cmd == "container" and args:
            return query(f"containers/{to_serial(args[0])}")
        if cmd == "player":
            return query("player")
        if cmd == "paperdoll" and args:
            return query(f"world/mobiles/{to_serial(args[0])}/paperdoll")
        if cmd == "gumps":
            return query("gumps")
        if cmd == "gump" and len(args) >= 2:
            switches = [int(s) for s in args[2].split(",")] if len(args) > 2 else []
            return post(
                f"gumps/{to_serial(args[0])}/respond",
                {"buttonID": int(args[1]), "switches": switches, "textEntries": []},
            )
        if cmd == "gumpclose" and args:
            ok, text = api.delete(f"gumps/{to_serial(args[0])}")
            return f"{'ok' if ok else 'ERROR'} gumpclose: {text.strip()[:120] or 'done'}"
        if cmd == "shop":
            return query("shop")
        if cmd in ("buy", "sell") and args:
            qty = int(args[1]) if len(args) > 1 else 1
            return post(f"shop/{cmd}", {"items": [{"serial": to_serial(args[0]), "quantity": qty}]})
        return f"ERROR do: unknown command '{cmd}' — see the manual's command list"
    except (ValueError, IndexError) as e:
        return f"ERROR {cmd}: bad arguments ({e})"


def execute(cmds: list[Command], api: RestApi, state: AgentState, memory_dir) -> tuple[list[str], float | None]:
    """Apply all commands. Returns (result lines for LAST TURN, requested wait seconds)."""
    results: list[str] = []
    wait: float | None = None
    do_count = 0

    for c in cmds:
        if c.verb == "think":
            continue
        if c.verb in ("say", "emote", "yell", "whisper"):
            if c.arg:
                body = {"text": c.arg}
                if c.verb != "say":
                    body["type"] = c.verb
                ok, _ = api.post("actions/say", body)
                results.append(f"{'ok' if ok else 'ERROR'} {c.verb}: {c.arg[:80]}")
        elif c.verb == "do":
            do_count += 1
            if do_count > 3:
                results.append(f"skipped (max 3 DO per turn): {c.arg[:60]}")
                continue
            results.append(_do(api, c.arg))
            if results[-1].startswith("ERROR"):
                pass  # keep going on later commands; the error is visible next turn
            time.sleep(0.6)
        elif c.verb == "note":
            if c.arg:
                state.notes.append(c.arg)
                results.append(f"noted ({len(state.notes)})")
        elif c.verb == "note_del":
            results.append(_del_numbered(state.notes, c.arg, "note"))
        elif c.verb == "todo":
            if c.arg:
                state.todo.append(c.arg)
                results.append(f"todo added ({len(state.todo)})")
        elif c.verb == "todo_done":
            results.append(_del_numbered(state.todo, c.arg, "todo"))
        elif c.verb == "mem_save":
            slug, _, text = c.arg.partition(":")
            if text.strip():
                results.append(memory.save(memory_dir, slug, text))
            else:
                results.append("ERROR mem save: format is MEM SAVE <slug>: <text>")
        elif c.verb == "mem_read":
            found = memory.read(memory_dir, c.arg)
            if found:
                state.recalled.append(found)
                results.append(f"memory '{found[0]}' will appear on your next screen")
            else:
                results.append(f"ERROR mem read: no memory '{c.arg.strip()}'")
        elif c.verb == "mem_del":
            results.append(memory.delete(memory_dir, c.arg))
        elif c.verb == "wait":
            try:
                wait = float(c.arg.split()[0])
            except (ValueError, IndexError):
                pass
    return results, wait


def _del_numbered(items: list[str], arg: str, kind: str) -> str:
    try:
        n = int(arg.split()[0])
        if 1 <= n <= len(items):
            removed = items.pop(n - 1)
            return f"{kind} {n} removed: {removed[:50]}"
        return f"ERROR {kind} del: no item {n}"
    except (ValueError, IndexError):
        return f"ERROR {kind} del: give a number"
