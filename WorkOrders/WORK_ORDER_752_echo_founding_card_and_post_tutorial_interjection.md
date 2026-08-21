<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-19
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-19) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 752 — Echo founding card overhaul + post-tutorial Echo interjection

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
**Classification:** narrative + onboarding flow + UI layout. Player-felt.
**PO:** Sam. Memory: [[echo-is-essence-of-guarded-person]], [[echo-lane-design-rulings]]. WO-738 Echo model.

## Part A — Founding-Echo card overhaul (the card is failing on 4 fronts)
The founding-Echo unlock card (`EchoUnlockDialogue`, fired by `EchoService.AnnounceFoundingEcho`) is broken:
1. **Header** "Echo Leveled Up to 1!" is nonsense on a first-unlock (awakening, not a level-up; you START at 1).
   -> Founding/first-unlock header = **"An Echo Awakens"** (or "<Echo> Awakens"). Keep "Leveled Up to N" ONLY
   for real level-ups (2+). The founding path must pass an AWAKEN header, not the level-up template.
2. **Layout** — the Show less / Dismiss / Close buttons OVERLAP each other and COVER the description text.
   -> Lay buttons in a clean, non-overlapping row; give the description its own bounded scroll area.
3. **Story** — copy never conveys the core concept: **an Echo is the awakened ESSENCE of one of the PEOPLE
   the Heart of Elarion guards/remembers** (not a generic elemental). Rewrite the copy in `EchoRosterCatalog.cs`.
4. **Subtitle** — "Ice Elemental" reads as a monster. -> a person's-essence subtitle (owner wording).

**Approved reframe (owner 2026-07-19), Frosthowl — apply the same shape to all 6 echoes:**
> **An Echo Awakens** / **Frosthowl** - *Essence of a fallen keeper*
> "The Heart of Elarion remembers every soul it has guarded. I was one - a keeper of the old light, kept
> safe in the tree until a new defender rose. Now I wake as Frosthowl, the Ice Echo. My essence is yours to
> call. Together, we hold the last light."
(Owner open q: keep name "Frosthowl", or give the person a name w/ Frosthowl as their ice-form.)

## Part B — Post-tutorial Echo interjection -> pet tutorial (NEW onboarding beat)
**When the tutorial is OVER — whether it ENDED naturally OR was INTERRUPTED/skipped — the founding Echo
(Frosthowl) interjects.** Flow:
1. **Interject** — Frosthowl speaks up (portrait/dialogue via DialogueService), in-character.
2. **Ask how it can best help** — presents the lane offer (Harvest / Crafting / Defense / Exploration) — this
   is the DEFERRED lane-assignment moment (canon: tutorial teaches the claim-loop first, then the echo asks).
3. **Advise on actions** — short guidance on what to do next (claim resources / build / etc.), in Frosthowl's voice.
4. **Tag the pet tutorial** — hand off / trigger the pet tutorial as the next onboarding beat.

**Hooks:** `TutorialFlow` must fire this on BOTH completion AND interrupt/skip (one shared exit point ->
`EchoService`/DialogueService interjection -> on-close -> start the pet tutorial). Idempotent (once per save,
persisted flag, like `echo_founding_taught`). ASCII-only; code-built UI; mobile-input ruling (no key letters).

## Acceptance
- Card: awaken header, non-overlapping buttons, essence copy + subtitle, all 6 echoes.
- Flow: after tutorial end OR interrupt, Frosthowl interjects, offers lane help, advises, then the pet
  tutorial starts. Fires once per save on both exit paths.
- Gate green; owner felt-verify the beat + copy on the Seeker.

## Do NOT
- Don't reuse the level-up header for the founding awaken. Don't block the pet tutorial if the echo beat is
  dismissed (still hand off). ASCII-only; no UXML; dual-copy any dialogue JSON.

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `EchoUnlockDialogue.cs:245 done; no interjection hits` — Part B unbuilt. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict. ⚠ NOTE FOR ANYONE REOPENING: the 2026-08-21 read-only audit had classified this one OPEN - STILL VALID, with the evidence cited above. The owner's review supersedes that call (owner statements are ground truth). The audit line is left in place deliberately, so if this work turns out to be needed, the evidence for it is still here rather than erased.
