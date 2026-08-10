# WORK ORDER 1012 — Tutorial/FTUE redesign: premium presentation, guide-hero rotation, the dynamic arc

**Status:** DONE (P1+P2+P3) — implemented 2026-08-10 by the sequential pipeline (kit → pet-guide →
8-beat arc), conformance review CONFORMS TRUE; P4 (captures vs wireframes + tester re-test) and the
owner felt-pass remain. See §7 below for the review's open findings.
**Minted:** 2026-08-09 (UI seat) — provenance stack bumped 1012 → 1013 in the same edit (header-is-sole-source rule, 08-09 restructure)

## §7 REVIEW FINDINGS (2026-08-10 pipeline review — open items, none blocking the lane commit)

- **IMPORTANT — the ARRIVE 4s no-input camera moment is implemented NOWHERE** (circular deferral:
  the step json delegates to OnboardingFlow presentation; OnboardingFlow's comment delegates to P3).
  P4 scope.
- **IMPORTANT — headless verification gap:** `AutoPilotDriver.AssertFoundingArc` (:2502-2530) still
  drives the RETIRED chain (places pet-house, checks `founding_hollow`); no driver exercises
  WALK / band-DEFEND / WIN, so the arc's `[Flow:Tutorial]` funnel is unproven headlessly. Update the
  driver in the next AutoPilot pass.
- Minors: 16 dead legacy `"speaker": "Sylas"` records (unreferenced, json-only) · the 2a utility
  line duplicated into an unreferenced record · stale TutorialFlow comments (defend order/final) ·
  the `{guide}` fallback portrait path is `Portraits/pet-house` (a building) · 12 pre-existing
  em-dash bytes in ctx objectives · `GuideLineUi` is a new presenter where §2b said "existing
  dialogue kit" (deviation documented in-file; mandatory beats still ride DialogueService).
- **NOT in this lane (deferred integrations):** §2c-ter (the Echo stacking teach — needs the WO-953
  gated-assignment seam to exist first) · WO-1013's `ctx_plans_recovered` step + `plans_collected`
  signal wiring (proposed JSON in the WO-1013 RESULT) — both ride the next tutorial-data touch.
**Lane:** Tutorial/Onboarding presentation + pacing. **The V2 bones STAY** — this is a skin + flow pass.
**Provenance:** owner directive 2026-08-08/09 (verbatim fragments): the tutorial *"feels very amateurish...
the verbiage is fine... it's the way the flow goes... the childish buttons they're using to highlight what
you should do next"*; the guide is *"a placeholder KayKit person... always scripted as from Silas"*; wants
*"heroes as an enum... whatever we commit is add one... greater than three, set it to zero"* (never your own
hero as guide); pacing: *"a little more natural... 'let's walk over here. Click that button down there.
Grab this, move it over here. Okay. Great. Let's move on.' Don't have them build the whole village. Have
them build one piece, then one cannon, maybe talk about the timers... then show enemies coming at the
gate."* Benchmark: 2026 mobile-RPG FTUE best practice.
**Wireframes (visual benchmark):** `UI_REVIEW/tutorial_flow_redesign_wireframes.html` — spotlight/chevron/
ghost-finger frames, the beat flow, the retire-vs-keep table.
**Research anchors:** progressive disclosure (one mechanic at a time, at the moment of need); teach by
DOING, never by text walls; a single clear objective at all times; visible progress (beads/checklist);
short beats with immediate payoff; skippable + resumable. (Sources logged in the session: gamedeveloper.com
FTUE best-practices, AC&A mobile onboarding, Udonis FTUE, CoC tutorial anatomy teardowns.)
**Depends on / anchors (all verified in-tree):** `TutorialFlow` + `tutorial-steps.json` +
`TutorialSignals` + `TutorialStepModel` (data-driven steps, contextual one-shots, grants),
`TutorialHighlightRegistry` + `UiSpotlight`, `OnboardingFlow` (the six-beat card sequence — converted
here), the dialogue system + portraits, `HeroClass` enum + `HeroCanonNames` (Grom/Sylas/Thrain/Elara),
the Onboarded gate + peace window, WO-1010 (the build UI this teaches — coordinate; D16 there defers the
tutorial rewrite HERE).

---

## 1. What is WRONG today (the owner's felt-test, mapped to code)

| Symptom | Source |
|---|---|
| "Childish buttons" highlighting next action | boxed yellow icon markers over targets; bounce-style cues |
| Fat top objective banner + "(0/1)" + inline "Skip >" + separate big "Skip Tutorial" | tutorial banner UI (collides with F8 box — WO-1010 D2) |
| Click-click-click-done pacing | steps chain with no acknowledgment beats, no movement, no narrative presence |
| Guide is a KayKit stand-in always scripted as "Sylas" | `tutorial-steps.json` ("Talk with Sylas beneath the tree") + placeholder rig |
| Wall-of-text welcome cards (Next / Next) | `OnboardingFlow` beats 1–3 |

## 2. The redesign

### 2a. The GUIDE — a rotating real hero, never yourself

> **⚠ OWNER CLARIFICATION (2026-08-09, verbatim intent, supersedes any formula-first reading):**
> **THE RULING IS THE INVARIANT, NOT THE MECHANISM** — *"making sure that person A is not guiding
> person A."* Whatever character guides the tutorial, it must NEVER be the hero the player selected.
> The rotation formula below is ONE sanctioned mechanism that satisfies it ("I don't really care what
> method you use"); any mechanism that satisfies the invariant is acceptable.
> **⚠ RE-RULED (owner, 2026-08-09, later the same day — via the pivot seam, exactly as designed):
> THE GUIDE IS THE PLAYER'S FIRST PET — AN ECHO OF ELARION.** Owner, verbatim: *"maybe even better
> to start with our pet. And our pet, which is an echo of Elarion, is the one that is our guide. By
> doing that, it introduces the pet first, allows us to always have an answer for who's gonna be the
> guide, and they can explain, hey, listen, I'll go farm for you, or I can repair your towers."*
> Confirmed over the alternatives (keep-rotation / hybrid) on the explicit pick list.
> **Why it wins:** canon-perfect (an Echo is the awakened essence of one of the people the Heart
> guarded — THIS WAS THEIR VILLAGE; the story does the teaching); the game's namesake system appears
> in minute one; the guide teaches its own utility (the canon claim-loop onboarding + the
> teaching-conversation-per-Echo ruling); the guide-identity problem dissolves permanently (a pet is
> never the player's hero — the "never yourself" invariant holds by construction); companion
> attachment from minute one. The starter-pet grant already exists in the step schema
> (`founding_hollow`) — it MOVES to the ARRIVE beat.
> **ROTATION is now the parked alternative** (formula preserved below for history/pivot). The seam
> stands: guide identity remains a data/config swap.
> **Flow deltas from the pet-guide:** ARRIVE grants + introduces the pet-Echo (it wakes near the
> Heart — its essence returning is the cold open's payoff); the pet leads the WALK beat (pets already
> follow/path — CLI verifies the rig can lead + emote); all GuideLine speech re-attributes to the
> pet-Echo (copy unchanged); ONE added utility line at the ACK beat — the pet offers its work in its
> own words ("I can farm. I can mend. Put me to work, Keeper.") — which doubles as the Echo-system
> hook. The rotation-hero cameo is NOT in scope (rejected hybrid).

- `guideClass = (HeroClass)(((int)playerHeroClass + 1) % 4)` — the owner's formula over the EXISTING
  `HeroClass` enum (Mage/Knight/Ranger/Cleric). Names resolve via `HeroCanonNames` (Grom/Sylas/Thrain/
  Elara). Pick Grom → the next hero guides; pick the last → wraps to the first. **No one is ever their
  own guide.**
- The guide's REAL rig (the same hero prefab/portrait the roster uses) spawns near the Heart, WALKS in,
  and remains a physical presence through the flow — it leads the walk beat, stands by the gate during
  the defense. Retire the KayKit stand-in.
- ALL tutorial dialogue re-attributes from hard-coded "Sylas" to the resolved guide (dialogue speaker id
  becomes data: `{guide}` token resolved at runtime; copy itself is unchanged — "the verbiage is fine").

### 2b. The PRESENTATION KIT (replaces the childish layer — one visual language for every step, forever)
1. **FocusMask** — full-screen dim (~65%) with ONE soft-edged cutout over the current target
   (upgrade `UiSpotlight`); everything outside is raycast-blocked. Gesture beats use a lighter dim
   (~35%) so the world stays readable.
2. **GuidePointer** — ONE slim gold chevron sprite that eases onto tap-targets (slow 6px settle loop —
   one moving element, never five). For gesture beats: a **ghost-finger** replaying the drag arc on a
   2s loop, fading permanently after the player's first successful gesture. Retires all boxed markers.
3. **ObjectiveStrip** — a THIN bottom-center strip: one objective sentence + progress beads (● ● ○ ○ ○).
   Replaces the fat top banner and its "(0/1)" counter; kills the F8 collision class by moving off the
   top edge entirely.
4. **GuideLine** — the guide hero's portrait + ONE line via the existing dialogue kit, lower-left,
   auto-dismissing on beat completion. No modal cards, no Next button. `OnboardingFlow`'s three welcome
   cards become guide one-liners over a single 4s camera moment.
5. **One Skip** — a single small corner control, one confirm sheet ("Skip the walkthrough? Your progress
   is saved."), checkpointed resume (the step registry already persists). Retires the big "Skip
   Tutorial" button + the banner's inline "Skip >".
6. **Acknowledgment beats** — after every action beat, the guide gives a 0.8s "Good. You got it." line
   (rotating micro-copy) BEFORE the next objective arms. The acknowledgment is the pacing — this is the
   owner's "Okay. Great. Let's move on." made structural.

### 2c. The FLOW — the owner's dynamic arc (authored in tutorial-steps.json, same schema)
0. Cold open cinematic (exists, unchanged).
1. **ARRIVE** — guide walks into frame by the Heart; one welcome line. (4s, no input.)
2. **WALK** — "Come with me." Follow the guide toward the gate — teaches joystick/camera BY DOING
   (completion signal: proximity to the guide at the gate; no text about controls).
3. **BUILD ONE** — one economy piece: FocusMask on Build, ghost-finger the drag, place it.
   (The prepaid-grant mechanism already exists in the step schema — reuse.)
4. **ACK** — "Good. You got it."
5. **ONE CANNON** — Defense tab, place ONE tower near the gate. (Teaches the WO-1010 lean-rail grammar.)
6. **TIMERS** — FocusMask on the build timer/queue chip: ONE line — "Work takes time, Keeper. Watch the
   ledger." No lecture; the Manage deep-dive stays a contextual one-shot for later.
7. **ENEMIES AT THE GATE** — a small scripted band (3–4) hits the gate; the player + the new tower repel
   it. The payoff beat — the tutorial's systems all fire at once. (Coordinate with the peace-window
   gating so ONLY this scripted band spawns.)
8. **WIN + HANDOFF** — quiet celebration (existing VFX facade, ~1s, no confetti wall); guide: "The rest
   is yours, Keeper." `FinishOnboarding()` → Onboarded=true; HUD elements fade in as unlocked
   (progressive disclosure).
- **NOT in the flow:** building the whole village, multi-structure quotas, any second fight, any screen
  tour. Contextual one-shots (first Manage open, first raid, first Echo awakening — the canon teaching
  conversation, first dungeon) ride the SAME kit via the existing `contextual`/`oneShot` schema.

### 2c-bis. ⚠ OWNER DIRECTIVE 2026-08-10 — the post-handoff "now what?" gap (ADDED to scope)

> Verbatim intent (owner, felt-test 2026-08-10): after placing the two towers and repelling the wave,
> *"it doesn't give you any more guidance... The bottom line was after I placed the two towers, I was
> like, okay. Now what?"* She wants the tutorial to hand off into a **gentle progression chain**:
> *"hey, we should set up a spot to buy some proper weapons... then after that, maybe let's get armor...
> it should follow the natural progression, not completely locked down that path... a gentle nudge or
> something... just some guidance on how to start."*

- The WIN + HANDOFF beat (8) must not end in silence. After `FinishOnboarding()`, a **nudge chain** of
  contextual one-shots continues the thread: suggest the weapons building first (canon ids per QR-5.7:
  the WEAPONS shop is id `workshop`, display "Forge"), then armor (id `forge`, display "Armorer"), then
  onward through the natural progression — each a GuideLine one-liner + optional FocusMask on the Build
  entry, **dismissible and never blocking** (nudges, not locks; free play stays free).
- Corroborating capture (F8 harvest 2026-08-10): `[Flow:Vendor] Forge: Lever-1 baked/station fallback
  withheld for 'collector_forge' — never player-built on this save (blank-town gate, WO-834)` — on a
  blank-town founding there IS no weapons vendor until the player builds one, and nothing tells them.
  The nudge chain is what closes that loop.
- Implementation shape: same `contextual`/`oneShot` schema + the §2b kit — this is P3/P4 data authoring,
  not new systems. The chain's ORDER (weapons → armor → what next) is a creative call: propose the full
  sequence to the owner before authoring past the first two beats she named.

### 2c-ter. ⚠ OWNER RULING 2026-08-10 — the Echo teaches the STACKING dependency (ADDED to scope)

> Verbatim: *"we should add to the tutorial, from the echo that it lets you know it cant do anything
> if it cant stack it somewhere."*

- The pet-Echo guide, in ITS OWN VOICE, teaches that its gathering is useless without somewhere to
  stack the goods — the works/storehouse dependency (the phantom-income existence gate,
  `ResourceBuildingHarvester.cs:129-143`, + the WO-837 storehouse capacity model). Example shape
  (copy = owner voice pass): "I can gather all day, Keeper — but I need somewhere to stack it. Raise
  the works and I will fill them."
- Delivery: a contextual one-shot on the FIRST gathering assignment whose resource's existence gate
  is CLOSED (the exact moment the owner herself hit on 2026-08-10 — assigned iron, no iron works,
  silent zero). Same kit, one line, dismissible. Pairs with WO-953's picker `NEEDS: <building>` cue —
  the Echo line is the teach, the picker cue is the persistent honest state.
- Also echoes into the utility line at ACK ("I can farm. I can mend.") — the guide may foreshadow it
  there in one clause, but the real teach fires contextually at the moment of need (progressive
  disclosure law).

## 3. Constraints (binding)

- **Bones untouched:** `tutorial-steps.json` schema, `TutorialFlow` interpreter, `TutorialSignals`,
  grants, the Onboarded gate, checkpoint persistence. New beats are DATA (new step defs + two new
  completion signals at most: follow-proximity, scripted-band-repelled).
- **Module isolation stands:** Onboarding/Core must not reference Village directly — the walk beat's
  proximity check and the scripted band ride the existing signal-bus/integrator seams.
- Code-built uGUI on `ElarionUiKit`; NO UXML. ASCII strings. MinTouch on Skip + confirm. Colorblind law:
  the cutout + chevron carry shape/position, never color alone; beads pair with the objective sentence.
- **Instrument the funnel (§12):** `[Flow:Tutorial]` per beat — armed/completed/elapsed-ms/skip-point —
  so the drop-off funnel is readable from a headless run and tuning is data-driven.
- Fixed-px bands for the strip/portrait line (the fraction-band lesson). F8/dev overlays get a disjoint
  band from ALL tutorial chrome (closes WO-1010 D2 for good).

## 4. Acceptance criteria

- [ ] Guide = rotating hero per the formula; four-for-four correct across all picks; never self; real rig
      walks in; zero "Sylas" hard-codings remain (grep-clean outside canon names).
- [ ] Boxed markers, fat banner, "(0/1)" counter, big Skip button, welcome cards: GONE (grep + capture).
- [ ] FocusMask/GuidePointer/ObjectiveStrip/GuideLine render per the wireframes; ONE moving cue at a time.
- [ ] The 8-beat arc runs end-to-end: walk → build one → ack → one cannon → timers → gate defense → win;
      under ~3 minutes at a relaxed pace; every action beat ends with an acknowledgment line.
- [ ] Skip works at any beat, confirms once, resumes from checkpoint on next launch if declined mid-flow.
- [ ] Contextual one-shots render with the same kit (verify one: first Manage open).
- [ ] Onboarded gate + peace window behave exactly as today (regression-pinned); the scripted band cannot
      leak into normal spawning.
- [ ] `[Flow:Tutorial]` funnel lines present for every beat in a headless run.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` + `UI_CAPTURE_OK` (PNGs: each beat's frame,
      judged against the wireframes) — then **external-tester re-test**, same panel as WO-1010: target
      verdict "it felt guided, not scripted."

## 5. What NOT to touch

- Tutorial COPY (owner: verbiage is fine) — re-attribute speakers only.
- The cold-open cinematic, hero-select, save schema, HeroClass enum values.
- WO-1010's build-mode surfaces beyond consuming its final layout (D16 there points here; do not fork it).
- No new dialogue system, no new tween library, no UXML.

## 6. Phasing

1. **P1 — kit:** FocusMask + GuidePointer + ObjectiveStrip + GuideLine + one-skip; re-skin the EXISTING
   step chain with it (old flow, new skin). Ships alone = the "childish" layer is dead.
2. **P2 — guide rotation:** the formula, the real rig, re-attributed lines, the walk-in.
3. **P3 — the arc:** re-author tutorial-steps.json to the 8 beats + the two new signals + the scripted
   gate band + acknowledgment beats.
4. **P4 — polish + funnel:** captures vs wireframes, funnel instrumentation review, tester re-test.
