"""Thin client for the ClassicUO REST API. stdlib only."""

from __future__ import annotations

import json
import urllib.error
import urllib.parse
import urllib.request


def to_serial(value: str) -> int:
    """Accept 0x… hex or decimal, return unsigned int."""
    s = value.strip()
    return int(s, 16) if s.lower().startswith("0x") else int(s)


class RestApi:
    def __init__(self, base: str, timeout: float = 10.0):
        self.base = base.rstrip("/")
        self.timeout = timeout

    def _request(self, method: str, path: str, body: dict | None = None) -> tuple[int, str]:
        url = f"{self.base}/api/{path.lstrip('/')}"
        data = json.dumps(body).encode() if body is not None else None
        req = urllib.request.Request(url, data=data, method=method)
        if data is not None:
            req.add_header("Content-Type", "application/json")
        try:
            with urllib.request.urlopen(req, timeout=self.timeout) as resp:
                return resp.status, resp.read().decode("utf-8", "replace")
        except urllib.error.HTTPError as e:
            return e.code, e.read().decode("utf-8", "replace")
        except (urllib.error.URLError, TimeoutError, OSError) as e:
            return 0, f"connection error: {e}"

    def get_json(self, path: str):
        status, text = self._request("GET", path)
        if status != 200:
            return None
        try:
            return json.loads(text)
        except json.JSONDecodeError:
            return None

    def get_text(self, path: str) -> str:
        status, text = self._request("GET", path)
        return text if status == 200 else ""

    def post(self, path: str, body: dict) -> tuple[bool, str]:
        status, text = self._request("POST", path, body)
        return status in (200, 202, 204), text

    def delete(self, path: str) -> tuple[bool, str]:
        status, text = self._request("DELETE", path)
        return status in (200, 202, 204), text

    # ── convenience ──────────────────────────────────────────────────────

    def in_game(self) -> bool:
        st = self.get_json("status")
        return bool(st and st.get("inGame"))

    def journal_since(self, since_iso: str | None, limit: int = 200) -> list[dict]:
        path = f"journal?limit={limit}"
        if since_iso:
            path += f"&since={urllib.parse.quote(since_iso)}"
        return self.get_json(path) or []
