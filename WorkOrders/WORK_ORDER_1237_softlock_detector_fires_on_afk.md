# WORK ORDER 1237 - The softlock detector fires on AFK, and that noise will bury a real softlock

**Status:** READY TO IMPLEMENT
**Silo:** Tooling / F8 harness
**Severity:** P2 by symptom, but it degrades the signal the whole section-14 pipeline depends on.
**Origin:** CLI triage of device capture seq 3609, 2026-08-26 - one of the first two captures the
WO-1227 bridge ever delivered.

---

## PROOF

```json
{"kind":"possible_softlock","message":"No movement or progress for 180s in 'Main_Castle_Overworld'",
 "scene":"Main_Castle_Overworld","t":2199.36,"utc":"2026-08-26T17:49:16.975Z"}
```

The matching screenshot (`logs/f8-inbox/device/SM02G4061955851/break_02_possible_softlock.png`) shows
a COMPLETELY HEALTHY frame: Thrain Lv 5 at full HP and mana, the five-face calm bar present
(Build / Bag / Raids 3-of-10 / Quests / Manage 3 idle), `Wave 6 - Next wave in 107s` counting down
normally, no modal, no scrim, town responsive.

**Nothing was stuck.** The owner was idle - reading and typing to the CLI while the game ran.

## Why this matters more than one false alarm

`possible_softlock` is one of the four kinds the section-14 daemon pages on. The device backfill digest
(`logs/f8-inbox/DEVICE_BACKFILL_2026-08-26.md`, `F8_DIGEST_OK entries=736 ... softlock=8`) holds
**8** of them. If an unknown fraction are AFK, the seat learns to discount the kind - and the ONE
real softlock arrives already discredited. That is the WO-965 lesson in a different costume: a queue
whose entries cannot be trusted is a queue nobody reads.

It also lands the same day the WO-1227 bridge went live, so **every future device AFK now pages a
seat**. The bridge multiplied the cost of this false positive; it did not create it.

## Required

Give the detector a way to tell IDLE from STUCK. The current rule is "no movement or progress for
180 s", which a player reading their phone satisfies perfectly.

Candidate discriminators - INSTRUMENT AND MEASURE before choosing (section 12), do not assume:
- **Input presence.** A stuck player TAPS and nothing happens; an AFK player does not tap at all.
  Zero input across the window is idle; input-without-state-change is the real signal.
- **App focus.** `Application.isFocused` false, or a backgrounded app, is idle by definition.
- **World liveness.** The wave clock was ticking normally in this capture. A world still advancing
  while the PLAYER does not move is idle; a world frozen while input arrives is stuck.

DO NOT simply raise the 180 s threshold. That trades a false positive for a slower true positive and
leaves the classifier just as blind - the same "raise the cap" reflex WO-1229 forbids for the VFX pool.

DO NOT silence the kind. A softlock reaching nobody is the WO-1227 failure this repo just spent a
ticket closing. The fix is DISCRIMINATION, not suppression. An idle-classified capture should still
be RECORDED and simply not paged, so the distinction stays auditable.

## Acceptance

1. A regression over the classifier: an idle-with-no-input window classifies IDLE; a
   no-input-but-frozen-world window and an input-without-progress window both classify SOFTLOCK.
   Prove RED first (WO-1138) - today all three are `possible_softlock`.
2. Re-run the classifier over the 736-entry device backfill and REPORT how many of the 8
   `possible_softlock` entries reclassify as idle. That number is this ticket's value, stated.
3. The daemon does not page on an idle-classified capture, and the capture is still written.
4. Any `.ps1` touched is PURE ASCII (the encoding oracle fails otherwise - it caught exactly this
   on 2026-08-26, on the orchestration hook).

## What NOT to touch

- The other three kinds (`flagged` / `error` / `exception`). Unaffected.
- `f8-inbox-lib.ps1`'s queue semantics or the ack watermark (WO-965).
- The WO-1227 device bridge's transport or its `device-state.json` watermark. It works and is proven
  idempotent three ways; this ticket is CLASSIFICATION, not transport.
