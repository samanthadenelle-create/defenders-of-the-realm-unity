# WORK ORDER 1018 — F8 captures are STILL being buried: the QUEUE producer half of WO-965 never landed

**Status:** READY TO IMPLEMENT — scope reduced 2026-08-10: **Evidence A is RESOLVED** (a stale daemon
process, not missing code — see the CORRECTION block below). **Evidence B + C remain OPEN** (recovery
loses captures; the empty `acked` set lets the watermark bury un-triaged seqs) and are the work.
**Minted:** 2026-08-10 (UI seat) — provenance stack bumped 1018 → 1019 in the same edit
**Lane:** Tooling / F8 triage harness (`.claude/skills/run-defenders/`). No game code.
**Provenance:** observed live this session, 2026-08-10, while triaging the owner's dungeon flags.
Owner instruction to file it: *"yes."*
**Relationship to WO-965:** WO-965 fixed the CONSUMER (`f8-ack.ps1`). **The PRODUCER side never landed**
— the harness's own WARN says so out loud, three times tonight. This WO closes that half.

---

## 1. RCA — the harness announces its own defect (verbatim, tonight)

**Evidence A — the producer is not queueing.** Every capture tonight arrived with:
```
[f8-queue] WARN: seq=2312 had no QUEUE.jsonl entry; recovered from
  D:\EoA\logs\f8-inbox\capture-20260810-183326.md (producer running pre-WO-965 code?)
[f8-queue] WARN: seq=2314 had no QUEUE.jsonl entry; recovered from
  D:\EoA\logs\f8-inbox\capture-20260810-183545.md (producer running pre-WO-965 code?)
```
The capture producer writes the `capture-*.md` file and bumps `PING.json`, but **never appends the
`QUEUE.jsonl` entry** WO-965's design depends on. The consumer is running on a *reconstructed* queue.

**Evidence B — the reconstruction loses captures, and the watermark then buries them.** Observed
sequence, in order, from this session's console:
```
[f8-ack] Acknowledged seq=2312
[f8-ack] STILL PENDING: 1 capture(s). NEXT = seq=2313        <- 2313 correctly known here
   ... (2313 triaged by me, filed into WO-1016)
[f8-queue] WARN: seq=2314 had no QUEUE.jsonl entry; recovered from capture-20260810-183545.md
[f8-ack] Acknowledged seq=2314                                <- acked 2314, NOT the older 2313
[f8-ack] Inbox clean - no captures pending.                   <- 2313 declared closed, never acked
```
`f8-ack.ps1` is **not** at fault in its selection logic — line 38 is explicit and correct:
`$targets = @([int]$pending[0].seq)   # OLDEST first - never the newest`. The bug is that **`$pending`
was rebuilt from a single recovered capture file**, so 2313 was not IN the pending list; "oldest
pending" was therefore 2314, and advancing the ack **watermark** to 2314 swept 2313 under it.

**⇒ Net effect: the exact WO-965 failure, through a different door.** WO-965's header records the
original: *"ack of seq 2309 silently closed seq 2307 + 2308, two of the owner's flags, which no seat
ever saw."* Tonight, 2314 closed 2313. **The owner's bug reports are still being silently deleted.**
(2313 survived only because I had already read it by hand before acking.)

**Evidence C — MEASURED STATE ON DISK, 2026-08-10 (this is the mechanism, confirmed):**
```
logs/f8-inbox/ACK.json  ->  { "lastAckSeq": 2315, "acked": [], "ackedAtUtc": ... }
                                                   ^^^^^^^^^^^ EMPTY
logs/f8-inbox/capture-*.md  ->  1641 files on disk
```
**The `acked` set is EMPTY. The watermark is the only authority in practice.** Every capture with
seq ≤ `lastAckSeq` is implicitly closed, whether or not any seat ever read it — so a single ack of a
high seq buries every lower one at once. That is not a race or an edge case; it is the steady state.

Note also `lastAckSeq: 2315` — higher than anything acked in this session (2312, then 2314). **2313 and
2315 are both swept under the watermark**, and 2315 was never surfaced to any seat at all. The
`f8-check-inbox.ps1` run minutes earlier reported `NO_CAPTURE ack=2314 ping=2314`, so the watermark
advanced again with no triage in between.

⚠ The 1641 capture files are NOT all un-triaged flags (most predate this and many are startup noise the
filter drops), but with an empty `acked` set **there is no record of which were ever read** — so the
backfill sweep (§2.5) cannot assume, and must classify by kind (`flagged`/`error`/`exception` vs noise)
and report what it finds.

## 2. What to do

1. **Fix the PRODUCER (the actual hole).** Whatever writes `capture-*.md` + `PING.json` must append its
   `QUEUE.jsonl` entry **in the same operation**, atomically — write the queue entry BEFORE (or
   transactionally with) the ping, so a capture can never exist without a queue row. Find why it is
   "running pre-WO-965 code": stale in-editor harness assembly, a second producer path, or an unshipped
   edit. Name the cause in the RESULT.
2. **Make recovery COMPLETE, not single-file.** When `QUEUE.jsonl` is missing entries, rebuild the
   pending set by scanning **every `capture-*.md` in `logs/f8-inbox/` whose seq > lastAckSeq** — not just
   the one named by `PING.json`. A partial recovery that reports "clean" is worse than no recovery.
3. **Make the watermark non-burying.** An ack must close **only the seq(s) it names**. If a monotonic
   watermark is kept for convenience, it may never imply-ack a seq that was never in the pending list;
   prefer the explicit `acked` set as the authority (the script already maintains one — make it the
   sole truth).
4. **Fail loud, never "clean".** If any `capture-*.md` exists with seq > lastAckSeq and is not acked,
   `f8-check-inbox.ps1` must NOT print `NO_CAPTURE`. Add a reconciliation check: files-on-disk vs
   acked-set; any discrepancy prints a LOUD warning naming each orphaned seq.
5. **Backfill sweep:** scan `logs/f8-inbox/` for any capture whose seq is at/below the current watermark
   but was never in the `acked` set — those are previously buried owner flags. **List them for triage**
   (do not auto-close). Tonight's data suggests this has been happening since before WO-965.

## 3. Acceptance criteria

- [ ] A fresh capture writes its `QUEUE.jsonl` entry every time; zero `had no QUEUE.jsonl entry` WARNs
      across a multi-capture play session.
- [ ] With 3 captures pending, three bare `f8-ack.ps1` calls close them **oldest-first, one each**, and
      the inbox reports clean only after the third.
- [ ] Deleting/withholding `QUEUE.jsonl` entirely still yields a COMPLETE pending list rebuilt from the
      capture files (regression-tested by simulating the missing queue).
- [ ] Acking an out-of-order seq never implies-acks an older un-triaged seq.
- [ ] `f8-check-inbox.ps1` cannot print `NO_CAPTURE` while an un-acked `capture-*.md` exists on disk.
- [ ] The backfill sweep is run once and its findings reported to the owner as a list of orphaned seqs.
- [ ] WO-965's header comment is updated in the same commit to record that the producer half landed here
      (canon §15 — a fix that leaves its own doc claiming completeness is how this recurred).

## 4. Why this is HIGH, not cosmetic

The F8 listener exists because of the standing rule that **the owner is never the bug detector**
(CLAUDE.md §14). A harness that silently closes her flags inverts that rule: she reports a defect, the
tooling deletes it, and no seat ever sees it. Every hour this stays broken, felt-test findings are being
lost — and the loss is invisible by construction.

## 5. What NOT to touch

- Game code. The capture CONTENT/format and the `[Flow:*]` harvest are working well — do not "improve"
  them here.
- `f8-ack.ps1`'s oldest-first selection (already correct — WO-965).

---

## ⚠ CORRECTION 2026-08-10 (CLI seat, verified at source and by ops) - EVIDENCE A IS WRONG

**"The PRODUCER side never landed" is FALSE.** The producer code DID land and is committed:
`.claude/skills/run-defenders/f8-watch-daemon.ps1:14` sources `f8-inbox-lib.ps1` and `:4` documents
"every capture is now APPENDED to logs/f8-inbox/QUEUE.jsonl". The publish path goes through
`Publish-F8Capture` under the inbox lock.

**What was actually wrong: the RUNNING PROCESS was stale.** The daemon had been started BEFORE WO-965
landed, so the live process was executing the old script from memory. That is precisely what the
harness's own WARN guessed - *"producer running pre-WO-965 code?"* - and it was right.

**Resolution:** `f8-watch-stop.ps1` then `f8-watch-start.ps1`. Old pid 48412 stopped, new pid 24116
started on the queue-aware code, 2026-08-10 ~19:0x, with the owner's player closed so no session was
disturbed. No code change was required for Evidence A.

**Transferable lesson, and the reason this is written down rather than quietly fixed:** a shipped fix to
a DAEMON is not a live fix until the daemon is restarted. The same shape has bitten this project before
from the other direction - a FeatureFlags default flip that changed nothing because PlayerPrefs already
held a value (KEY_FACTS 2026-08-08). **Code landing and behaviour changing are two different events.**
Any future harness fix must name the restart as part of the deliverable.

## STILL OPEN - Evidence B, do not close this WO on the correction above

The claim that the RECONSTRUCTION path loses captures and that the ack watermark then buries them is
**not** addressed by the restart, because it lives in the CONSUMER. It needs verifying on real data
rather than reasoning: the reconstruction ran all evening (every capture arrived via `WARN_UNQUEUED`),
and at least one capture - **seq 2314** - was auto-acked while a newer seq was pending, which is how
WO-1017 came to be filed by hand "to prevent loss".

State at the time of writing, for whoever picks this up: `QUEUE.jsonl` does not exist yet (no capture
since the restart, so the producer half is UNPROVEN in practice), `ACK.json` reads
`lastAckSeq 2315` with an empty `acked` list, and there are 1641 `capture-*.md` files on disk.
**The first capture after the restart is the proof** - it must appear in `QUEUE.jsonl` with no
`WARN_UNQUEUED`. If it does, Evidence A is closed for good; if it does not, the producer has a real
code gap and this WO becomes a code ticket.

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `f8-inbox-lib.ps1 untouched since 96100bc2d` — recovery-path burial unchanged. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.
