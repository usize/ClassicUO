# 05 — Journal `limit` returns the most recent entries

## Problem

`GET /api/journal?limit=N` (`Api/Controllers/JournalController.cs`) does `entries.Take(limit)`
on the ring buffer — returning the **oldest** N entries. The skill docs and wrapper help both
promise "last N journal entries" (`./ai/uo journal [N]` — Last N journal entries). In practice
`./ai/uo journal 5` returned the session's first five lines (from 45 minutes earlier), which is
useless for the core "verify my last action" loop. (The `summary` endpoint's embedded journal —
last 20 — is the only reliable view today.)

## Changes

- `JournalController.GetJournal`: apply `limit` as the **tail** of the buffer.
  `entries` is an `AsEnumerable()` over the journal list (oldest→newest). Change the
  `if (limit > 0)` block to `entries = entries.TakeLast(limit).ToList();`
  (keep the `since` filter applied **before** the tail, as it is now; keep order oldest→newest
  in the response — that's how `summary` prints it).
- No wrapper change needed (`./ai/uo journal [N]` already passes `?limit=N`).

## Test

1. Build + restart per README.
2. Say something: `./ai/uo say "journal limit test"`.
3. `curl -s 'http://127.0.0.1:9000/api/journal?limit=3' | jq -r '.[].text'` → the 3 **newest**
   lines, and the last one is (or contains) `journal limit test`.
4. `./ai/uo journal 3` → same three lines via the wrapper.
5. `curl -s 'http://127.0.0.1:9000/api/journal?limit=0'` → full buffer (limit=0 means no
   limit, per the existing `limit > 0` guard).
6. `since` still filters: `curl -s "http://127.0.0.1:9000/api/journal?since=2026-08-23T00:00:00&limit=3"`
   → newest 3 entries after that timestamp (or empty if none).

## Acceptance

- `?limit=N` returns the N most recent entries, oldest→newest, `limit=0`/omitted returns all.
- `since` behavior unchanged.

## Test results (live, Trammel)

All passed against a 100-entry buffer:

- `say "journal limit test"` → `?limit=3` returned exactly the 3 newest lines, last one the
  said text. `./ai/uo journal 3` (wrapper) identical.
- `?limit=3` byte-identical to the tail of `?limit=0` (full buffer).
- `?since=2026-08-25T00:00:00&limit=3` → newest 3 after the timestamp (since still filters
  before the tail). `?since=<future>` → `[]`.
- Bonus: buffer capacity observed at 100 entries.

## Commit

`Fix journal limit to return the most recent entries`
