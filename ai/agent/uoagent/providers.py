"""LLM providers via plain HTTP (no SDK dependencies). DESIGN.md section 6."""

from __future__ import annotations

import json
import time
import urllib.error
import urllib.request

from .config import LlmConfig

DEFAULT_BASES = {
    "anthropic": "https://api.anthropic.com",
    "openai": "https://api.openai.com/v1",
}


def _http_json(url: str, headers: dict, body: dict, timeout: float = 300.0) -> dict:
    req = urllib.request.Request(url, data=json.dumps(body).encode(), method="POST")
    req.add_header("Content-Type", "application/json")
    for k, v in headers.items():
        req.add_header(k, v)
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        return json.loads(resp.read().decode("utf-8", "replace"))


def _complete_once(cfg: LlmConfig, api_key: str, system: str, user: str) -> tuple[str, dict]:
    """Returns (text, usage) where usage = {input_tokens, output_tokens}."""
    base = (cfg.base_url or DEFAULT_BASES[cfg.provider]).rstrip("/")

    if cfg.provider == "anthropic":
        data = _http_json(
            f"{base}/v1/messages",
            {"x-api-key": api_key, "anthropic-version": "2023-06-01"},
            {
                "model": cfg.model,
                "max_tokens": cfg.max_tokens,
                "temperature": cfg.temperature,
                "system": [{"type": "text", "text": system, "cache_control": {"type": "ephemeral"}}],
                "messages": [{"role": "user", "content": user}],
            },
        )
        u = data.get("usage") or {}
        return (
            "".join(b.get("text", "") for b in data.get("content", [])),
            {"input_tokens": u.get("input_tokens", 0), "output_tokens": u.get("output_tokens", 0)},
        )

    # openai-compatible: OpenAI, DeepSeek, OpenRouter, Ollama, vLLM, LM Studio
    headers = {"Authorization": f"Bearer {api_key}"} if api_key else {}
    data = _http_json(
        f"{base}/chat/completions",
        headers,
        {
            "model": cfg.model,
            "max_completion_tokens" if cfg.provider == "openai" else "max_tokens": cfg.max_tokens,
            "temperature": cfg.temperature,
            "messages": [
                {"role": "system", "content": system},
                {"role": "user", "content": user},
            ],
        },
    )
    choices = data.get("choices") or [{}]
    u = data.get("usage") or {}
    return (
        (choices[0].get("message") or {}).get("content") or "",
        {"input_tokens": u.get("prompt_tokens", 0), "output_tokens": u.get("completion_tokens", 0)},
    )


def complete(cfg: LlmConfig, api_key: str, system: str, user: str) -> tuple[str, dict]:
    """Call the LLM with exponential backoff. Raises only KeyboardInterrupt."""
    delay = 5.0
    while True:
        try:
            return _complete_once(cfg, api_key, system, user)
        except urllib.error.HTTPError as e:
            detail = e.read().decode("utf-8", "replace")[:300]
            print(f"[llm] HTTP {e.code}: {detail} — retrying in {delay:.0f}s")
        except (urllib.error.URLError, TimeoutError, OSError, json.JSONDecodeError) as e:
            print(f"[llm] {e} — retrying in {delay:.0f}s")
        time.sleep(delay)
        delay = min(delay * 2, 300.0)
