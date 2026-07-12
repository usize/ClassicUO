"""Optional trace export: OTLP/HTTP JSON with GenAI semantic conventions.

Zero dependencies — the OTLP JSON payload is built by hand and POSTed to
`<endpoint>/v1/traces` (default collector port 4318: Jaeger, Tempo, Alloy,
Phoenix, Langfuse all accept this). Each turn emits one `uoagent.turn` span
with a `chat <model>` child span carrying gen_ai.* attributes; the full
rendered screen (what the agent sees) and the completion ride along as span
events. Export is fire-and-forget on a daemon thread — it never blocks a turn.

`otlp_endpoint = "stdout"` skips the network entirely and pretty-prints each
turn's spans to stdout instead — use this for local inspection. A bare `nc`
listener will NOT work as a collector: nc speaks raw TCP, not HTTP, so it
never sends back a response and every export will read as a connection
failure (or hang until timeout).
"""

from __future__ import annotations

import json
import secrets
import sys
import threading
import urllib.request


def _attr(key: str, value) -> dict:
    if isinstance(value, bool):
        v = {"boolValue": value}
    elif isinstance(value, int):
        v = {"intValue": str(value)}
    elif isinstance(value, float):
        v = {"doubleValue": value}
    else:
        v = {"stringValue": str(value)}
    return {"key": key, "value": v}


def _ns(t: float) -> str:
    return str(int(t * 1e9))


class Telemetry:
    def __init__(self, endpoint: str, service_name: str, capture_content: bool = True):
        self.endpoint = endpoint.rstrip("/") if endpoint else ""
        self.service_name = service_name
        self.capture_content = capture_content
        self._warned = False

    @property
    def enabled(self) -> bool:
        return bool(self.endpoint)

    def emit_turn(self, t: dict) -> None:
        """t keys: turn_start, llm_start, llm_end, turn_end (unix seconds),
        provider, model, system, screen, reply, usage {input_tokens, output_tokens},
        results (list[str]), wait, profile, turn_number."""
        if not self.enabled:
            return
        threading.Thread(target=self._send, args=(t,), daemon=True).start()

    def _send(self, t: dict) -> None:
        trace_id = secrets.token_hex(16)
        turn_span_id = secrets.token_hex(8)
        llm_span_id = secrets.token_hex(8)
        usage = t.get("usage") or {}

        llm_events = []
        if self.capture_content:
            llm_events = [
                {
                    "timeUnixNano": _ns(t["llm_start"]),
                    "name": "gen_ai.content.prompt",
                    "attributes": [
                        _attr("gen_ai.system_instructions", t["system"]),
                        _attr("gen_ai.prompt", t["screen"]),
                    ],
                },
                {
                    "timeUnixNano": _ns(t["llm_end"]),
                    "name": "gen_ai.content.completion",
                    "attributes": [_attr("gen_ai.completion", t["reply"])],
                },
            ]

        llm_span = {
            "traceId": trace_id,
            "spanId": llm_span_id,
            "parentSpanId": turn_span_id,
            "name": f"chat {t['model']}",
            "kind": 3,  # CLIENT
            "startTimeUnixNano": _ns(t["llm_start"]),
            "endTimeUnixNano": _ns(t["llm_end"]),
            "attributes": [
                _attr("gen_ai.operation.name", "chat"),
                _attr("gen_ai.system", t["provider"]),
                _attr("gen_ai.request.model", t["model"]),
                _attr("gen_ai.usage.input_tokens", int(usage.get("input_tokens", 0))),
                _attr("gen_ai.usage.output_tokens", int(usage.get("output_tokens", 0))),
            ],
            "events": llm_events,
        }

        turn_span = {
            "traceId": trace_id,
            "spanId": turn_span_id,
            "name": "uoagent.turn",
            "kind": 1,  # INTERNAL
            "startTimeUnixNano": _ns(t["turn_start"]),
            "endTimeUnixNano": _ns(t["turn_end"]),
            "attributes": [
                _attr("uoagent.profile", t["profile"]),
                _attr("uoagent.turn_number", int(t.get("turn_number", 0))),
                _attr("uoagent.results", "\n".join(t.get("results", []))[:4000]),
                _attr("uoagent.wait_seconds", float(t.get("wait", 0.0))),
                _attr("uoagent.screen_tokens_est", len(t["screen"]) // 4),
            ],
        }

        payload = {
            "resourceSpans": [
                {
                    "resource": {"attributes": [_attr("service.name", self.service_name)]},
                    "scopeSpans": [{"scope": {"name": "uoagent"}, "spans": [turn_span, llm_span]}],
                }
            ]
        }

        if self.endpoint == "stdout":
            self._print(t, usage)
            return

        try:
            req = urllib.request.Request(
                f"{self.endpoint}/v1/traces",
                data=json.dumps(payload).encode(),
                headers={"Content-Type": "application/json"},
                method="POST",
            )
            urllib.request.urlopen(req, timeout=5).close()
        except OSError as e:
            if not self._warned:
                print(f"[telemetry] export to {self.endpoint} failed ({e}). "
                      f"If you pointed this at `nc`, that won't work — nc doesn't speak "
                      f"HTTP, so it can never acknowledge the POST. Use otlp_endpoint = "
                      f"\"stdout\" for a dependency-free local view, or point at a real "
                      f"OTLP collector (Jaeger: `docker run -p 4318:4318 -p 16686:16686 "
                      f"jaegertracing/all-in-one`).")
                self._warned = True

    def _print(self, t: dict, usage: dict) -> None:
        dur_ms = (t["llm_end"] - t["llm_start"]) * 1000
        line = (
            f"[otel] turn={t.get('turn_number')} model={t['provider']}/{t['model']} "
            f"latency={dur_ms:.0f}ms in={usage.get('input_tokens', 0)}tok "
            f"out={usage.get('output_tokens', 0)}tok wait={t.get('wait', 0):.0f}s"
        )
        print(line, file=sys.stderr)
        if self.capture_content:
            print(f"  prompt: {t['screen'][:300].replace(chr(10), ' ')}", file=sys.stderr)
            print(f"  reply:  {t['reply'][:300].replace(chr(10), ' ')}", file=sys.stderr)
