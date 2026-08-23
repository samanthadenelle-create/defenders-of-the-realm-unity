# WO-1145 RESULT - f8-ack: no pre-acks, no silent out-of-order acks

**Completed:** 2026-08-23. **Lane:** F8 triage harness (PowerShell only - no C#, no compile gate).
**Marker:** `F8_SELFTEST_OK 39/39` (was 24/24; 15 new cases).

## What the ticket assumed vs. what HEAD actually did (MEASURED, not read)

The ticket was written against the pre-WO-1018 harness. Three of its four scope items were
**already landed** at HEAD by WO-965 + WO-1018, and that was proven by driving the real scripts
against a throwaway inbox rather than by reading them:

- ack is already per-capture (`ACK.json.acked` holds out-of-order acks above a contiguous watermark)
- a bare `f8-ack.ps1` already takes `$pending[0]`, the OLDEST
- `pending=N` already counts un-acked RECORDS (`Get-F8Pending`), not `newest - ack`

Acking the newer 3583 with 3582 queued already left 3582 pending. **The live 2026-08-22 repro in the
ticket predates the WO-1018 commit that landed the same day.**

## The two holes that were still open, both measured live

**1. THE PRE-ACK (the ticket did not name this one; it is the worse of the two).**
`f8-ack.ps1:59` synthesised a target for a seq that was **not pending** and wrote it into `ACK.json`:

```powershell
else { $targets = @([pscustomobject]@{ seq = $Seq; kind = 'manual'; capturePath = ''; ackKey = "seq:$Seq" }) }
```

Measured against HEAD: with only seq 2306 pending, `f8-ack.ps1 -Seq 2308` succeeded. Captures 2307
and 2308 then arrived, and `f8-check-inbox.ps1` surfaced **2306 + 2307 only**. 2308 was born acked and
was never surfaced to any seat. That is the exact 2026-08-10 loss recorded in CLAUDE.md s14, reachable
by one typo, in code whose header claims that loss was fixed.

**2. THE QUIET OUT-OF-ORDER ACK** (the ticket's scope item 3). Acking the newest while an older
capture waited printed the identical `[f8-ack] Acknowledged seq=N` a correct oldest-first ack prints.
The "STILL PENDING" line that followed is also printed after a correct ack, so nothing in the output
distinguished "I triaged the oldest" from "I just skipped the owner's older flag".

## The fix

- `.claude/skills/run-defenders/f8-ack.ps1:43` - `Write-F8AckPending`, so every refusal names what is
  still waiting and can never read as "done".
- `.claude/skills/run-defenders/f8-ack.ps1:66` - `$ackedSeqs` / `$ackedLeaves`: the watermark and the
  acked sets are the only proof a capture was already closed.
- `.claude/skills/run-defenders/f8-ack.ps1:81` - `-File` with no pending record is REFUSED unless that
  capture is really on disk (preserving the WO-1018 buried-capture recovery path).
- `.claude/skills/run-defenders/f8-ack.ps1:107` - `-Seq` with no pending record is REFUSED (or reported
  as already-acked). `ACK.json` is not touched; an `error` event is written to `queue-events.log`.
- `.claude/skills/run-defenders/f8-ack.ps1:127` - an ack whose key is not `$pending[0]`'s prints
  `OUT OF ORDER`, names the capture that should have been triaged first, and logs a `warn` event.
  Deliberate out-of-order acks still work - they just cannot be quiet.

Semantics are unchanged: one capture per ack, oldest by default, same exit codes, same
`[f8-ack] Acknowledged seq=N` line the hooks parse.

## Regression - `f8-inbox-selftest.ps1`, cases F/G/H (15 new asserts)

Each case builds its OWN throwaway inbox under `$env:TEMP` and drives the REAL scripts in child
processes (`Write-Host` does not reach the output stream in PS 5.1, so an in-process assertion would
silently pass). No arithmetic is re-implemented in the test.

- **F** - the live 3582/3583 case: ack the newer, assert the older is STILL pending and that the ack
  announced itself as out of order.
- **G** - the 2026-08-10 shape: 2306/2307/2308 queued; ack 2306 and assert both survive; a burst of 3
  needs 3 acks and only the third reads `NO_CAPTURE`.
- **H** - the pre-ack guard: an unarrived seq is refused, `ACK.json` is byte-identical afterwards, and
  the capture that later takes that number still reaches a seat.

Both new guards were proven against the PRE-fix code first (F's `OUT OF ORDER` line did not exist;
H's capture was measurably lost), so neither is a tautology.

## Verification

```
F8_SELFTEST_OK 39/39
```

Nothing was run against the live `logs/f8-inbox` - every run used `-InboxOverride`. Both edited files
were tokenizer-parse-checked under PowerShell 5.1 (`PARSE_OK`); the check-in gate's own failure mode
was a script that never parsed.
