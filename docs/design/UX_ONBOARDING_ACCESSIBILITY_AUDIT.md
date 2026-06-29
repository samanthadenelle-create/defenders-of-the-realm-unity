# UX / Onboarding / Accessibility Audit — Echoes of Elarion

**Date:** 2026-06-28
**Scope:** First-time-user experience (title → hero-select → pet → hub → first battle),
tutorialization, core-loop & economy clarity, accessibility (text size, colorblind-safe
palette, input options, mobile touch targets).
**Method:** Source read of the onboarding, HUD, settings and UI systems (see
"Files reviewed"). This is a UX/design audit — findings only, no code changed.

> ⚠️ **Headline finding:** the first-run TEACHING flow (`TutorialDirector`,
> `OnboardingFlow`) still teaches the *retired* tower-defense game (build towers at
> gates, place pets, defend waves), not the current **single-Knight overworld →
> isolated BattleArena** combat north star. The FTUE is the single biggest UX debt and
> the highest-leverage fix. Everything else is polish on top of a tutorial that
> describes the wrong game.

---

## 1. Executive summary

| Area | State | Verdict |
|---|---|---|
| Title / boot | Works, heavily scar-tissued (watchdogs, re-asserts) | **At risk** — 3 entry buttons with jargon, destructive "Start New" w/ no confirm, title text baked into art |
| Hero-select | Solid carousel, Knight-only, locked previews | **Good** — but duplicated across two controllers |
| Pet-select | Bypassed in V1 (`BypassPetSelect` ON), dead screen retained | **Confusing** — vestigial path; copy still implies a "pick" |
| FTUE / tutorialization | Teaches TD loop (towers/gates/waves/pets) | **Broken vs. canon** — wrong game taught |
| Core-loop / economy clarity | No onboarding for echoes / life-force / wood-iron-grain / overworld battle | **Missing** |
| Settings | Audio, difficulty, quality, screen-shake — persisted, applied on launch | **Good foundation** |
| Accessibility — color | Gold-on-obsidian = high contrast; **red/green** is the only signal for affordable/HP | **At risk** — no colorblind-safe secondary cue, no CB mode |
| Accessibility — text | All font sizes hardcoded px; floor = 11px; no scaling | **At risk** on mobile |
| Accessibility — input | Virtual joystick + 9-zone battle HUD; keyboard fallbacks; no remap | **Adequate** |
| Accessibility — touch targets | `ElarionUi.TapTarget = 44` honored in shared buttons & battle HUD | **Good** (a few exceptions) |
| Accessibility — settings surface | WO-357 (text-size / CB / high-contrast / screen-reader) specced, **never built** | **Missing** |

---

## 2. First-run flow walkthrough (what a brand-new player actually hits)

**Title scene (`TitleController.cs`)**
1. Splash gate: a static art background (`Title/Title_L` landscape / `Title/Title_H`
   portrait — **title text is baked into the image**, not localized or scalable) with a
   bottom row of THREE buttons: **Play Intro · Start New · Continue**.
2. "Start New" → `ResetToNewGame()` (wipes the save) + fast-path onboarding flag → builds
   the in-title hero-select cards. "Play Intro" → 9-screen Yarn cinematic. "Continue" →
   straight to Castle.
3. The title screen *itself* renders a second hero-select (4 cards + detail/stat card).

**Hero-select** — two implementations exist:
- The **in-title** card grid (`TitleController.BuildCards`) — tap a hero, tap again to commit.
- The **standalone** `HeroSelectController` carousel (prev/next, pip dots, stat pips,
  LOCK scrim on the 3 non-Knight heroes). Reached via `SceneRouter.GoHeroSelect`.

**Pet-select (`PetSelectController.cs`)** — `FeatureFlags.BypassPetSelect` is ON, so
`OnEnable` immediately `GoCastle()`s. The screen, its card builder, its "you already have
a Warden" state, and its empty-catalog fallback all still exist but are unreachable in the
default flow.

**Hub (MainCastle_Hall) → FTUE (`TutorialDirector.cs`)** — self-bootstraps, gated on
`GameState.Onboarded == false`. Default ("Start New") = **fast path**: a 3-line companion
hook → `FinishTutorial()` → `WaveManager.BeginLoop()`. Learn-by-doing barks fire on first
**tower build** and first **breach**.

**First battle** — the *current* combat loop is overworld encounter → isolated
`BattleArena` (`BattleArenaHud.cs`, `BattleHud9Zone.cs`): primary-target readout, ability
arc, Flee (tap-to-confirm), victory summary (crown tier, time, XP/Wisdom/Wood/Iron/Gear).

### The flow's structural problems
- **Two hero-select surfaces** (title cards + standalone carousel) = double maintenance and
  the historical "which one does the owner land on?" regressions documented in both files.
- **Three first-run gates** layered on one `Onboarded` flag: `StoryIntroController` (cold
  open), `OnboardingFlow` (6 coach-marks), `TutorialDirector` (7-scene). They actively
  suppress each other by reflection. Fragile; many watchdog/re-assert hacks in
  `TitleController` exist only because this stack stalls.
- **The teaching content is for the wrong game** (see §3).
- **Destructive "Start New" has no confirmation** — one tap wipes the save + all dialogue
  state (`ResetToNewGame` + `DialogueResetService.ResetForNewGame`).

---

## 3. Tutorialization — teaches the retired loop (P0)

`OnboardingFlow` beats: *Welcome → The Heart → Force-field → **Raise a Tower** → **Your
Wardens (place a pet)** → **Begin Wave 1**.* `TutorialDirector` scenes: village tour →
**free tower at a gate** → **defend a wave** → **3× tower-cost supply grant** → daily quests
→ **name your pet, Defend/Gather** → "fortify all four gates."

Per `combat-pivot-single-hero-northstar` + `overworld-encounter-isolated-battle` canon, V1
is: **control ONE Knight → walk the overworld → engage a wandering enemy → pop into a
real-time kite arena → win → return home.** Base-building (barracks/towers/waves) is
**V2, gated behind `ff.basebuilding` (OFF)**. So the FTUE teaches mechanics the player
cannot use in the shipped loop, and never teaches the loop they *will* use.

Consequences for a first-timer:
- They're told to "build a tower at the gate" and "place a Warden" — neither is the V1 core.
- They are never shown: move the Knight, find an encounter, what triggers a battle, how the
  arena works (kite/abilities/heal/flee), or what victory gives them.
- The post-battle economy grant (3× tower cost in crystals) seeds a currency the V1 loop
  doesn't spend.

**Fix (P0):** author a new FTUE for the V1 loop. Minimum viable teaching beats:
1. *Move* — "drag to move / use the stick" (introduce the joystick + camera).
2. *Find the fight* — point at the first wandering encounter; explain the chase/leash.
3. *The battle* — on first arena entry, coach-mark the basic-attack anchor, the ability
   arc, heal, and Flee (the 9-zone HUD already has the bones — overlay coach-marks on it).
4. *Reward* — read the victory summary; explain what XP/Wisdom/Wood/Iron *do*.
5. *The home loop* — back home, introduce the echo workforce / life-force tree (the actual
   economy), not towers.

Keep it **learn-by-doing** (the existing bark pattern) and **skippable**. Retire (flag-gate
off, don't delete) the tower/gate/wave/pet beats in `OnboardingFlow` + `TutorialDirector`
until `ff.basebuilding` returns.

---

## 4. Core-loop & economy clarity (P1)

- **No first-run explanation of the real economy.** The victory summary lists
  `+Wisdom`, `+Wood`, `+Iron`, `+XP`, `Gear` (`BattleArenaHud.ShowVictorySummary`) with no
  glossary. "Wisdom" especially is unexplained anywhere in the FTUE.
- **The echo / life-force / wood-iron-grain harvest loop** (the documented value engine,
  `echo-workforce-drag-drop`) has no onboarding surface at all. A new player has no idea
  that driving enemies back grows the tree, or that they drag-assign echoes to harvest.
- **Currency split is opaque:** crystals (build/spend pool, used by the deprecated tower
  flow) vs. wood/iron/grain (harvest) vs. Wisdom (talent?) — the player is never told which
  matters. Recommend a single one-line "what is this" tooltip on each resource pip in the
  HUD, plus a one-time coach-mark the first time each resource is earned.
- **Help → "Controls" is a 5-second toast** (`HelpMenu.OnShowControls`) — transient, can't be
  re-read, and lists **desktop** keys (WASD / 1-2-3-4 / Build hotkeys) that don't match the
  mobile virtual-joystick + 9-zone touch reality. Make Controls a persistent panel with a
  mobile column.

---

## 5. Accessibility

### 5.1 Color / colorblind (P1 — real risk)
- The panel language is **runic gold `#d4af37` on obsidian black** (`ElarionUi`). Gold-vs-black
  is a strong *luminance* contrast and is broadly colorblind-safe — good.
- **The risk is the state palette:** `Affordable` green `#76bc6b` and `Danger` red `#db5752`
  are the canonical affordable/unaffordable, valid/invalid and HP signals — the exact
  **red↔green** pair protanopes/deuteranopes confuse. In several surfaces color is the *only*
  signal (e.g. button `ButtonKind.Confirm` vs `Danger` differ only by hue; HP red bar).
  WO-357 §"Color not the only indicator" was specced but is **unbuilt**.
- **No colorblind mode exists.** Grep confirms zero `colorBlind` runtime code; the only
  `reducedMotion` flags are per-dungeon-prop serialized booleans (`Lantern`, `Checkpoint`,
  `CraftingPedestal`, `IngredientPickup`) **not wired to any setting**.
- **Fixes:**
  - **P1:** pair every red/green state with a non-color cue — a check/✕ glyph, an icon, or a
    text label ("Can't afford", "Valid"). Cheapest high-impact a11y win.
  - **P2:** add a Colorblind mode (None/Protan/Deutan/Tritan) to Settings that swaps the
    `Affordable`/`Danger`/vitals tokens for a CB-safe set (e.g. blue/orange). All state
    colors already funnel through `ElarionUi` tokens, so this is a single indirection point.

### 5.2 Text size / legibility (P1)
- **Every font size is a hardcoded px literal.** The scale ladder
  (`FontTitle 24 / Head 18 / Body 15 / Label 13 / Micro 11`) is sensible, but lots of
  player-facing copy sits at **11–13px** (hero blurbs, stat labels, pet archetype, tutorial
  progress, "Coming Soon"). On a phone at native DPI that is below comfortable reading size.
- **No text-scale option.** WO-357's Small/Normal/Large was specced, never built.
- Several low-contrast pairings: `ParchmentDim` (`#c7bca8`) italic body at 11–13px on stone —
  fails comfortable AA at that size.
- **Fixes:**
  - **P1:** add a global text-scale multiplier (Small/Normal/Large) to Settings + a static
    `UiScale` the layout helpers multiply font sizes by. Route through `ElarionUi` so it's one
    seam. Raise the on-screen floor to 13px for body copy.
  - **P2:** bump `ParchmentDim` body copy to `Parchment` at small sizes, or never go below 13px
    for anything the player must read.

### 5.3 Input options (adequate, minor gaps)
- **Touch:** `VirtualJoystick` (mobile-gated, bottom-left, radius `max(60, min(w,h)*0.16)`) +
  the 9-zone battle HUD. Good coverage.
- **Desktop:** WASD/gamepad in `HeroLocomotion`; battle abilities Q/W/E/R.
- **Gaps:** no key/button remapping; no left-handed (joystick-side) flip; the Help "Controls"
  text is desktop-only (§4). `safeArea` insets (notch/rounded corners) — WO-357 specced
  `ApplySafeArea`, not applied to the code-built UI roots. **P2:** apply safe-area padding to
  the title / select / HUD roots; add a left-handed toggle.

### 5.4 Touch-target sizes (good)
- `ElarionUi.TapTarget = 44` (`StyleButton` sets `minHeight = 44`) — honored by every shared
  themed button. Battle HUD targets exceed it (basic-attack 146px, abilities 84px,
  directional 56px, utilities 60px, lock 44px, gear 48px). Carousel arrows 44×44 (at the
  floor — fine). Joystick scales with screen.
- **Exceptions to fix (P2):** the title **Connect Wallet** button is **36px** tall (below
  44); the title splash menu buttons are custom-sized (~46px w/ padding — borderline). Bring
  both to ≥44px.

### 5.5 Audio / comfort (good foundation)
- Settings has Master/Music/SFX + Mute + a **screen-shake (reduce-motion)** toggle wired to
  `ScreenShakeSetting.Enabled`. Fresh saves start **muted** (browser-autoplay a11y) — good.
- **Gap (P2):** the `reducedMotion` prop flags (§5.1) and any UI/VFX pulsing aren't bound to a
  global "Reduce motion" setting — only screen-shake is. Promote screen-shake to a broader
  "Reduce motion" master that also stills idle UI animations and dungeon-prop spin.

---

## 6. Returning-player & flow robustness (P2)
- The `Onboarded` flag is the single gate for cold-open + both tutorials; its historical
  failure (replay-every-launch) is why `TitleController` carries the DEF-253 watchdog,
  WebGL orphan re-assert, and overlay-neutralize hacks. Once the FTUE is rebuilt (§3),
  collapse the three first-run surfaces to **one** sequencer behind one flag and delete the
  reflection-based mutual suppression — most of the scar tissue can then go.
- Hero-select and pet-select both self-skip for returning players (good). Pet-select's
  vestigial reachable states (`BindAlreadyChosen`, empty-catalog fallback) are dead under
  `BypassPetSelect` — **retire the screen** or gate its code off to stop the confusion.

---

## 7. Prioritized fix list

### P0 — first impression / teaches the wrong game
1. **Rebuild the FTUE for the V1 single-Knight overworld→arena loop** (§3). Retire the
   tower/gate/wave/pet beats behind `ff.basebuilding`. New beats: move → find encounter →
   battle (coach-mark the 9-zone HUD) → read reward → home economy.
2. **Add a confirm step to "Start New"** before `ResetToNewGame()` wipes the save (§2).

### P1 — clarity & core accessibility
3. **Color is never the only signal** — add glyph/text cues to every red/green
   affordable/valid/HP state (§5.1).
4. **Global text-scale option** (Small/Normal/Large) + raise body-copy floor to 13px (§5.2).
5. **Economy onboarding** — one-time coach-mark + a persistent resource glossary the first
   time each of Wisdom/Wood/Iron/XP is earned; teach the echo/life-force loop (§4).
6. **Make Help → Controls a persistent panel** with a mobile column, not a 5s toast (§4).

### P2 — polish & deeper a11y
7. **Colorblind mode** (None/Protan/Deutan/Tritan) swapping the `ElarionUi` state tokens (§5.1).
8. **"Reduce motion" master** that also stills UI/prop animation (promote screen-shake) (§5.5).
9. **Safe-area insets** on UI roots; **left-handed** joystick toggle; **key remapping** (§5.3).
10. **Touch-target cleanup** — Connect Wallet (36→44px), title menu buttons ≥44px (§5.4).
11. **Collapse the 3 first-run surfaces to one sequencer**; retire vestigial pet-select (§6).
12. **De-duplicate hero-select** — one source of truth (title cards OR carousel, not both) (§2).
13. **Move title text out of baked art** into localizable, scalable labels (§2/§5.2).

---

## 8. Files reviewed
- Onboarding: `Assets/_Modules/Onboarding/{TitleController, HeroSelectController,
  PetSelectController, OnboardingFlow}.cs`
- FTUE: `Assets/_Modules/Village/Tutorial/TutorialDirector.cs`
- HUD / battle: `Assets/_Modules/HUD/HelpMenu.cs`,
  `Assets/_Modules/Village/Arena/{BattleArenaHud, BattleHud9Zone}.cs`,
  `Assets/_Modules/Village/Hero/VirtualJoystick.cs`
- Theme / a11y: `Assets/_Modules/Core/UI/ElarionUi.cs`,
  `Assets/_Modules/Settings/{SettingsController, SettingsModel}.cs`
- Spec (unbuilt): `WorkOrders/WORK_ORDER_357_mobile_touch_accessibility.md`
- Canon checked: `combat-pivot-single-hero-northstar`, `overworld-encounter-isolated-battle`,
  `echo-workforce-drag-drop` (auto-memory).

> **Note for the PO:** items 1, 3, 4, 5 are NEW-FEATURE work (build, not RCA) per the
> TICKET_PIPELINE classification — route as specs/WOs, not bug fixes. Items 2, 6, 10, 12 are
> small EXISTING-surface edits.
