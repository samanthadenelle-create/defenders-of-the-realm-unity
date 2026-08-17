<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 446 — Front Door: scored press-gate → "The Heart-Tree" cold open

**Status: READY TO IMPLEMENT.** Owner-directed (2026-06-17), storyboarded live + locked (v2).
Lane: Onboarding (Title/StoryIntro) + narrative copy + art (Grok plates). Single-pass build.

**Platform: MOBILE-FIRST, WEB-DELIVERED (prove value).** Target = phone browser (WebGL), **portrait 2:3
(9:16-class)**. Design the whole front door for a thumb on a phone. No landscape/desktop cut until
mobile-web validates the loop. Portrait assets (`opener.mp4`, `Intro2.mp4` @ 448×672) are on-target.
This is **Echoes of Elarion · Chapter One** — the web-forward opening of the *Legends of the Realm* saga.

## Goal
Replace the bloated 14-beat / ~73s cold open with the locked **6-plate / 26s** sequence
"The Heart-Tree," fronted by the existing **press-any-button gate** (which is also the WebGL
audio-unlock gesture). Forced 10s hold, then skippable.

**Canon spine (give → take → tell):**
1. **GIVE** — load to the **living Heart-Tree of Elarion in glory** (`opener.mp4`, scored). The beauty IS
   the hook — instant, free, shown BEFORE any ask.
2. **ENTER** — on the glory screen: **`Connect Wallet` / `Continue` (Guest)**. EITHER is the entry: it
   unlocks WebGL audio AND triggers the destruction. **GUEST PATH IS MANDATORY** — connecting must NEVER
   be required to witness the dust/lore/loop (beauty-first softens the ask; the guest path keeps the
   funnel open). One tap = audio-unlock gesture + wallet/continue choice + destruction trigger.
3. **DUST / THE SACRIFICE (the take-away)** — the instant they enter, the **Withering** descends and the
   land turns to dust. **The people give themselves to the Heart-Tree — it takes them in to escape sure
   destruction.** To preserve them, the Tree **spends all its strength**: its light goes out and it falls
   **dark and dormant — but STILL STANDING**, every soul of Elarion held within its heartwood. The
   dormancy is a **sacrifice, not decay.** Near-wordless; the player FEELS it before being told it.
4. **LORE (the tell, click-paced cards)** — the dark Tree has held its people a hundred winters, spending
   the last of itself, **waiting in hope that a hero would rise to drive the evil from the land.** (**No
   spire, no "song" mechanic** — the Tree holds its *people*; that's the surviving Heart you defend.)
5. **YOU** — *"You are that hope. Wake them. Reclaim the Realm — the Heart of Elarion is yours now."* →
   into the loop. (Future hook: as the dark is driven back, the Tree's light returns and its people wake —
   the town grows. Freed, not built.)

**Gameplay continuity (the whole payoff):** the dormant Heart-Tree the cold open leaves you with **IS the
in-game town center** — `HeartController` / "Elarion the world-tree" / the HUD's "HEART OF ELARION" tree.
You watched it nearly die; the endless defend-loop is keeping its last song lit. The tree that turns to dust
MUST be the same tree (reference = `opener.mp4` / `reference_heart-tree_GIVE.jpg`) AND must visually match
the in-game `TreeOfLifeMaterialFixer` centerpiece (violet-emissive world-tree). Cinematic → gameplay, one entity.
The embers become the **ambient backdrop loop** (canon-sourced to the Tree, NOT a sci-fi starfield).

## The locked sequence (storyboard → spec)

### Press-gate (`TitleController` splash gate, already exists)
- **Copy change only:** the generic *"press any button to start"* → **`⟡  Enter the Realm  ⟡`**
  with a quieter sub-line *"(press anything)"*. Keep it pulsing softly over the **living Heart-Tree
  art** (gold canopy, embers drifting up) — the gate IS the hook image.
- **Unchanged behavior (load-bearing):** the press unlocks the AudioContext (`WebGLAudioUnlock`)
  AND fires `PlayMusic(MusicTrack.Title)` the same frame, THEN starts the cold open **with score**.
  This press is the ONLY legal audio-start moment on WebGL — do not auto-start the intro without it.
- **Continuity:** the cold open opens on the SAME Tree the gate shows (frame 0 → frame 1), then
  burns it. The first thing the player loves is the first thing the game takes away.

### Cold open — replace `StoryIntroController.ReactOpeningCinematic` with these 6 beats
Crossfade between plates (full-bleed, NOT the old corner thumbnails). Times are the on-screen
read window per plate; ~0.6s crossfades between. Total ≈ 26.0s if untouched.

**Delivery model — FORCE the feeling, let them pace the telling.** Two parts:
- **CINEMATIC (forced, auto, ~10-12s, UNSKIPPABLE):** the GIVE + the DUST. Emotion shouldn't be self-paced —
  it lands or it doesn't, so the player can't click past it.
- **LORE (self-paced, CLICK-TO-ADVANCE story cards):** the few screens that explain what they just saw.
  Tap to advance, read at their own speed. Exposition *should* be self-paced. Tap-to-advance is the mobile
  gesture (replaces the old rigid auto-timer). A small "Skip ▸" is available on the lore cards only.

**Part A — CINEMATIC (forced):**
1. **GIVE** — `opener.mp4`: living Heart-Tree in glory, scored. The beauty, before any ask.
   **Video behavior:** play ~10s once, then **FREEZE the last frame via code** (`VideoPlayer.isLooping=false`;
   `Pause()` on reaching the end / 10s mark) so it **holds the glory frame** as the gate backdrop — NO loop
   (would re-burn), NO black (dead). The Connect/Continue UI sits over the frozen frame; entry triggers the dust.
   **Pick the hold-frame deliberately** — must be a CLEAN glory frame, NOT the garbled "Legends of tif Realm"
   title card (fix the title, or freeze a beat earlier before that card resolves).
2. **ENTER** — on the frozen glory frame, TWO ways in: **`Connect Wallet`** (web3) and a **themed guest
   button** — *"Venture into the Realm" / "Enter the Realm" / "Step into Elarion"* (final copy = creative's
   call, NOT a generic "Continue"). Guest path **MANDATORY**; either button = audio-unlock gesture + dust trigger.
3. **DUST** — the instant they enter, the glory burns to dust; the SAME tree is stripped dark & dormant but
   STILL STANDS. Near-wordless; music carries. *(soft line, late/optional:* `…and the Heart of Elarion went dark.`*)*

**Part B — LORE (click-to-advance cards).** Replace `StoryIntroController.ReactOpeningCinematic` with a
tap-paced card deck (no fixed durations — player taps "ready/continue" to advance):

| Card | ImageId | Line | Emphasis |
|---|---|---|---|
| 1 | intro-2 | `A hundred winters have passed. The light never returned.` | — |
| 2 | intro-3 | `But deep in the dark wood, one ember of the old song still holds.` | — |
| 3 | intro-4 | `While that song holds, the valley holds. They call the dark the Withering.` | — |
| 4 | intro-5 | `The song is fraying — and it has waited for the one it would answer to.` | — |
| 5 | intro-6 | `That one is you. Come home — the song is yours now.` | **yes** |

- Each card: tap-anywhere (or a "Continue ▸" affordance) advances to the next. Last card → **Begin** → the loop.
- **No spire** — the song lives in the dormant Heart-Tree itself (matches the in-game `HeartController`
  world-tree you defend). Old "spire over its ashes" line is retired.
- Forced applies to Part A only (give + dust); Part B is fully player-paced.

### The forced-wait gate (the core mechanic)
- `_pointerSkipGraceSeconds`: **`1.25f → 10.0f`** — this already gates tap-anywhere; bumping it
  forces the first 10s. Premise is fully delivered by 10.0s (plates 1+2+reveal of 3).
- **Gate the Skip button** (currently added unconditionally + "never gated"): build it
  `display:None`, reveal it (+ a `tap to continue ▸` hint, top-right) only once
  `Time.unscaledTime - _overlayStartTime >= 10f`.
- After 10s: tap / Skip → jump straight to hero-select (existing skip target).

### Ember backdrop — RETHEME `TitleStarfield` (stars/comets → Heart-Tree embers)
The existing particle system is already warm-gold (ember-colored, not cold-star) and already
`loop = true`. Retheme it from a cosmic starfield to **embers + ash rising from the Heart-Tree**:
- **Drift flip:** embers RISE — change the comet/particle vertical velocity from down/sideways to
  gently UP (and slow it). Reads as sparks lifting off the ashes, not stars/comets crossing.
- **Naming:** rename the `Comets`/`Stars` GameObjects + the class' intent comments to embers/ash
  (no behavior change beyond the drift). Keep the soft additive glow + the violet-fade tail.
- **Guttering-ember sync:** on beat 3 (~9s) a single ember gutters out and falls (one downward
  streak) — "one ember at a time." (Was the "comet" cue.)
- **Brighten pulse:** on beat 6 (~20.8s) briefly raise ember emission/brightness + a gold bloom.
- The ember loop keeps running on the Title after the story ends once (ambient cycle —
  do NOT re-loop the narration). It loops natively; no rendered video required (a soft 10s loop
  video is optional, not needed — the runtime system already loops seamlessly + stays WebGL-light).

### Routing (first-launch anticlimax fix)
- **First launch** (cold open played): route the dissolve **straight into hero-select**, NOT back
  to the Title menu — don't deflate "the song is yours now" with a 3-button menu.
- **Returning** player (cold open skipped via `!Onboarded`): land on the Title menu as today
  (Continue / Start New / Play Intro).

## Art — 6 Grok plates → `Assets/_Modules/Onboarding/Resources/Intro/intro-1..6.jpg`
Soft hand-sketched, charcoal+ink under muted watercolor wash; cold violet-blue/ash palette with
one recurring warm **gold** light (the Tree's light → the embers → the spire's song); paper grain,
soft vignette, 16:9, quiet lower third for the text. **The Heart-Tree is the through-image** — the
same tree alive, burning, charred, then a stump under the spire.

**The GIVE (glory) is `opener.mp4`** — already made. The reference still for the tree's design is
`reference_heart-tree_GIVE.jpg` (clean frame, no title). **Feed it to Grok as the image reference**
so every plate below is unmistakably THIS tree.

**Generation order (consistency — all reference the opener tree):**
1. **`intro-1`** — the DESTRUCTION: *"this exact tree consumed by fire, collapsing, turning to ash and
   embers, the dark closing in"* (a brief 1–3s clip would be even better than a still — the take-away moment).
2. `intro-2` — *"the same place, a hundred winters later: only the charred trunk/stump remains, embers + ash
   still rising into the night, grey cold ground"*.
3. `intro-3` — pale stone **spire raised over the same charred stump**, gold glow within (lock the spire here).
4. `intro-4` — the spire holding its glow against the encroaching dark (the Withering at the frame edges).
5. `intro-5` — the spire's glow **fraying/flickering**, a lone snow path approaching, no figure yet.
6. `intro-6` — a **faceless/hooded** figure at the spire's foot, gold reaching out (pre-HeroSelect — must NOT lock to a class).

(Full per-plate prompts + style preamble: see chat storyboard v2, 2026-06-17 / paste-pack. The opener
tree's warm-gold + cosmic-violet palette is the reference for all plates.)

## Acceptance
- [ ] Press-gate reads "Enter the Realm"; press unlocks audio + starts the cold open WITH music.
- [ ] 6 plates play full-bleed with crossfades; total ≈26s untouched.
- [ ] First 10s cannot be skipped (no tap-through, Skip hidden); at 10.0s Skip + "tap to continue ▸" appear.
- [ ] Post-10s tap/Skip → hero-select.
- [ ] Embers RISE (drift flipped up); a single ember gutters/falls on beat 3 (~9s); brighten on beat 6.
- [ ] Backdrop reads as Heart-Tree embers, NOT a cosmic starfield (theme association holds).
- [ ] First launch → hero-select directly; returning → Title menu.
- [ ] Star field/comets persist + loop on the Title; narration does NOT re-loop.
- [ ] Compile gate green; brace + NUL guards pass.

## What NOT to touch
- Don't auto-start the intro without the press (breaks WebGL audio). Don't restore corner-thumbnail
  layout. Don't re-loop the narration. Don't disable the shared OnboardingPanelSettings doc on teardown
  (the "Skip wipes every screen" regression — keep the emptied display:None root pattern).
- §0: CLI edits the `.cs` on the Windows path; UI does not touch code. §2: copy/art are owner+UI lane.

*Cross-ref:* `Onboarding/StoryIntroController.cs`, `Onboarding/TitleController.cs` (splash gate
:142–176), `Onboarding/TitleStarfield.cs`, `docs/STORYLINE.md` (canon), `docs/TOWN_LOOP_CANON.md`
(this is the beat BEFORE onboard→town). WO number to reconcile against master backlog in the hygiene pass.
