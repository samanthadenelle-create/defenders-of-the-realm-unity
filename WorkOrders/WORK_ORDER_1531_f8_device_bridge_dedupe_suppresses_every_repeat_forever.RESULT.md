# WO-1531 RESULT - the F8 device bridge dedupe is time-scoped, and a FLAG is never deduped

**Status:** IMPLEMENTED - 2026-09-07, uncommitted, awaiting gate. Edit-only lane: no git, daemon not restarted.

## What changed
`.claude/skills/run-defenders/f8-device-bridge.ps1`
- `Get-EntryKey` (`:211` before -> `:248` after): was `kind + message(200)` and NOTHING else, so the first
  occurrence of a message suppressed every later one across sessions and days. Now
  `kind + message(200) + sessionId + 10-min bucket`, returning `''` for `kind=flagged` - an owner FLAG is an
  EVENT and is NEVER deduped.
- New `Get-EntryBucket` (`:235`, knob `$Script:DedupeBucketMinutes`), `Test-F8Duplicate` (`:261`, empty key =
  never seen), session-per-line in the eligibility pass (`:379`), 4-arg key in the publish loop (`:443`), a
  flagged publish stores nothing in `seen` (`:513`), new `-SelfTest` switch.
- Queue semantics untouched - still `Publish-F8Capture` into the append-only queue (WO-965).

`.claude/skills/run-defenders/f8-watch-daemon.ps1` - same carve-out: `:198` / `:243` let a `flagged` line
bypass `$seenKeys`. **Parse-checked only, NOT behavior-tested.** Side effect: on the log-shrink replay path
(`$breakBase = 0`) a flagged line seen this process lifetime now re-emits where it was suppressed.

## Proof

`powershell -File .claude\skills\run-defenders\f8-device-bridge.ps1 -SelfTest` - 9/9 PASS:

```
CASE 1  319 entries, 1 session, 1 repeated message, 1 FLAG -> published 3 (flag 1, error 1 per bucket)
CASE 2  two FLAG presses ten minutes apart                 -> 2   (RED before this change)
CASE 3  300 identical errors inside ONE bucket             -> 1   (the refusal path)
CASE 3c the same error a day later                         -> 2   (the bug itself)
F8_DEVICE_BRIDGE_SELFTEST_OK
```
End-to-end on the REAL staged device log (fixture mode, temp inbox, live inbox/state untouched, temp deleted):
`-LogOverride <copy of SM02G4061955851 break-log.jsonl> -ReplayLast 400` -> `F8_DEVICE_BRIDGE_OK published=28
dupSuppressed=372 offset=11379/11379` = **flagged 1, possible_softlock 2, error 25**. The owner's FLAG publishes.
`Parser::ParseFile` 0 errors on both files; 0 non-ASCII bytes; bridge pure LF, daemon uniformly CRLF (as found).

## Open
- Acceptance 4 (backfill = 1 flagged + 2 softlocks) NOT run: the live watermark has already passed them
  (`device-state.json` lastUtc `2026-09-07T05:44:05Z`, lineOffset 11379), so it is a scoped replay for the lead.
- Acceptance 5 (cross-reference in WO-1460): outside this lane's file list.
- Old `seen` hashes in `device-state.json` are orphaned by the new key format; harmless, `lastUtc` still guards replay.
