# WORK ORDER 1013 — "Castle Defense Plans": the wave-2 drop that unlocks the Arcane Spire

**Status:** DONE (mechanics) — implemented + `[castle-plans]` green 2026-08-10; the guide beat +
parchment sheet are held for post-WO-1012 integration and the "Arcane Tower vs Spire" naming is an
open owner ruling. See the RESULT file.
**Minted:** 2026-08-09 (UI seat) — provenance stack bumped 1013 → 1014 in the same edit
**Lane:** Early-game progression + one contextual beat. Small, sharply-scoped: ONE unlock, ONE drop, ONE
contextual step. **No new systems, no new UI, no new verbs.**
**Provenance:** owner idea 2026-08-09 (verbatim): *"after they've survived the second wave, in their drops
they receive a set of castle defense plans which add the arcane tower or maybe give them a free arcane
tower. Something to build to — so you start with one, but then, wow, these ones are better."* Owner's own
stress-test in the same session: *"are we adding too much depth in the beginning?"* — resolved KEEP, with
the guardrails in §3 (the design adds CONTENT on known verbs, never a new verb; it lands AFTER the
tutorial in the D20 "afterwards" window).
**Canon:** *Echoes of a Forgotten Civilization* — the plans are **recovered knowledge of the fallen
people**; the Arcane Spire is THEIR tech, which is why it outclasses the starter tower. The drop is lore,
not just loot.
**Depends on:** WO-1012 (the contextual one-shot kit delivers the beat), WO-1010 (the build grammar this
reinforces; the Defense card row shows the locked card), the wave system (wave-index signal), the
walk-over pickup pattern (`ComposedKeyPickup` class of trigger), the card lock-with-reason state
(WO-1010 spec'd "locked: reason in words" — CLI verifies the mechanism at source before building on it).

---

## 1. The design (player experience, in order)

1. **From minute one:** the Arcane Spire card is VISIBLE in the Defense category but LOCKED, reason in
   words: `Recover the plans`. (Aspiration is on-screen before the player can ask; the lock line IS the
   foreshadowing — no tutorial text spent on it.)
2. **Survive wave 2** (the second real wave after onboarding — completion signal off the existing wave
   counter): a **physical drop lands at the gate** — a small chest/satchel prop ("the plans"), same
   walk-over pickup grammar as keys/ingredients. It glints; no banner announces it.
3. **Walk over it** → pickup fires the ONE contextual beat (WO-1012 kit: guide line + FocusMask on the
   Defense quick-tab): guide hero — one line, e.g. *"The old builders' plans. A Spire. Better than
   anything we raise today."* (final copy = owner's voice pass; ASCII).
4. **The unlock + the funding:** the Arcane Spire card unlocks AND the drop granted the resources for
   the FIRST one (grant sized to its catalog cost — the owner-refined middle path: **plans + funded
   build, NOT a free pre-placed tower**). The player still opens Build, picks the card, places and
   builds it THEMSELVES — the WO-1010 loop, reinforced with a visibly better reward.
5. Per D20: **no "FREE" labeling anywhere** — the card shows its normal cost; the player simply finds
   they can afford it. They will see it did not cost them.

## 2. Implementation shape (all existing rails — verified pattern-level, CLI verifies at source)

- **Lock:** the structures-catalog/build-palette lock-with-reason state carries `arcane_spire` as
  locked-by-flag until the unlock grant flips it. Persisted in save (same class of flag as
  `everBuiltStructureIds` / SeenTutorials — pick the idiomatic home, do NOT invent a new store).
- **Trigger:** a `tutorial-steps.json` contextual step (`oneShot:true`) with a wave-2-survived
  completion signal (add ONE signal id if the wave counter does not already publish one).
- **Drop:** spawn the plans prop at the gate on the signal; walk-over trigger grants: unlock flag +
  the funding basket + fires the guide beat. Cost-basket rule applies (WO-947: regular vs arcane
  baskets — the Spire is ARCANE, so the funding is crystal-inclusive per its real catalog cost).
- **Beat delivery:** the WO-1012 presentation kit (GuideLine + FocusMask). No bespoke UI.

## 3. Guardrails (the "too much depth?" resolution — binding)

- **ONE authored drop, ever.** This is not a drop system; wave 3+ drops nothing scripted. Normal
  progression owns everything after this beat (the CoC pattern: early gift, then systems take over).
- **No new verbs:** pickup + build only — both already taught. If any part of the implementation wants
  a new player-facing mechanic, it is out of scope; stop and flag.
- **No tutorial lengthening:** the WO-1012 mandatory arc is untouched; this is a post-tutorial
  contextual beat.
- **No announcement chrome:** no banner, no modal, no "NEW!" badge storm — the drop glints, the guide
  speaks once, the card unlocks. Less is more (D20).

## 4. Acceptance criteria

- [ ] Arcane Spire card: visible + locked ("Recover the plans") from first build-mode open; normal cost
      displayed; never buildable before the unlock.
- [ ] Surviving wave 2 spawns the plans drop at the gate; walk-over collects it (works even if the
      player ignores it for N waves — it persists until collected).
- [ ] Collection: unlock flips (persisted across restart), funding granted (sized to catalog cost,
      arcane basket), guide beat fires once, ever (oneShot persisted).
- [ ] Player builds the first Spire through the normal WO-1010 flow; no FREE labeling anywhere.
- [ ] Wave 3+ produce NO scripted drops (guardrail regression).
- [ ] Skip-tutorial players still get the beat (it is post-tutorial, gated on waves, not on tutorial
      completion).
- [ ] `[Flow:Tutorial]`/`[Flow:Progression]` lines: drop-spawned, collected, unlocked, first-spire-built
      — funnel readable headless.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` + `UI_CAPTURE_OK` (locked card, drop at gate,
      guide beat, unlocked card).

## 5. What NOT to touch

- Wave balance/composition, the tutorial arc (WO-1012), the build UI (WO-1010), catalog costs.
- No new currencies, no new inventory item type (the "plans" are a trigger prop + grants, not an
  inventory object the player manages).
- Do not generalize into a drop/reward framework — that is a future pillar, not this WO.
