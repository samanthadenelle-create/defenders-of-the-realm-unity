**Status:** FIXED 2026-08-23 (57b2c4595) — the PRE-ACK hole is SEALED: a non-pending seq is now REFUSED, out-of-order acks print OUT OF ORDER. F8_SELFTEST_OK 39/39. FELT-TEST: press F8 twice quickly — BOTH must reach the seat. AWAITING OWNER CLOSE.

# WORK ORDER 1145 - f8-ack is a high-water pointer, so acking the newest swallows older captures

**Minted:** 2026-08-22 (CLI, banner bumped 1145 -> 1146 in the SAME edit)
**Lane:** F8 triage harness. **Class:** THE GUARD ITSELF.

## REPRODUCED LIVE, 2026-08-22

The UserPromptSubmit hook named **seq=3582** as the oldest un-acked capture. One
`f8-ack.ps1` call reported:

```
[f8-ack] Acknowledged seq=3583
[f8-ack] Inbox clean - no captures pending.
f8-check-inbox: NO_CAPTURE ack=3583 ping=3583
```

It acked the **NEWEST** (3583), not the oldest (3582), and closed the queue. `QUEUE.jsonl` proves
both existed as distinct records. **seq=3583 was never read by any seat before being marked acked** -
it was recovered only because the CLI seat distrusted the mismatch and opened the file by hand. Its
content was a real owner flag: *"random vfx stuck around"*.

## WHY THIS IS THE MOST EXPENSIVE CLASS OF DEFECT WE HAVE

CLAUDE.md s14 exists so **the owner is NEVER the bug detector**. This is the mechanism that carries
her flags to a seat - and it can drop them silently. A lost capture is indistinguishable from a
capture that was never made.

⛔ **THIS ALREADY HAPPENED ONCE AND COST REAL REPORTS.** s14 records it verbatim: *"on 2026-08-10 the
seat acked seq 2306, next saw 2309, and the owner's 2307 + 2308 never reached any seat."* WO-965
introduced `QUEUE.jsonl` + per-capture files precisely so the inbox would stop being a single slot.
**The queue landed; the ACK did not follow it.** The record is now per-item and the pointer is still
a watermark, so the fix is half-applied - which is worse than not applied, because the queue's
existence makes the inbox LOOK safe.

s14's own words: *"Never ack 'the latest'; never assume the newest capture is the only one."* The
script does exactly what the doc forbids the seat from doing.

## SCOPE

1. **Ack must be PER-CAPTURE, not a watermark.** Mark the specific seq acked - ideally by stamping
   its `QUEUE.jsonl` record or maintaining an acked-set - so an out-of-order ack cannot close
   anything else.
2. **`f8-ack.ps1` with no argument must ack the OLDEST un-acked**, matching what
   `f8-check-inbox.ps1` surfaces and what s14 tells every seat to expect. Accept an explicit seq
   argument for the deliberate case.
3. **A mismatch must be LOUD.** If the seq being acked is not the oldest un-acked, say so and name
   what else is still pending, rather than silently closing them.
4. **`pending=N` must count un-acked RECORDS**, not `newest - ack`.

## ACCEPTANCE

- [x] Acking a NEWER seq leaves older un-acked captures pending, and `f8-check-inbox` still surfaces
      the oldest
- [x] A no-arg ack takes the oldest
- [x] Regression drives the exact 3582/3583 case: two queued captures, ack the newer, assert the
      older is STILL pending. ⚠ MEASURE, do not restate - drive the real scripts against a temp
      inbox; do not re-implement their arithmetic in the test
- [x] The 2026-08-10 shape is covered too: ack 2306 with 2307/2308 queued, assert both survive
