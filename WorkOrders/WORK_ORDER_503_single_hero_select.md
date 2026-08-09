# WORK ORDER 503 — Single-Hero Select Screen + Remove Pet-Select Step

**Status:** DONE (reconciled 2026-08-09 from the tree - delivered by the WO-559 hero-select rebuild: `Assets/_Modules/Onboarding/HeroSelectController.cs` presents one playable hero with LOCKED / Coming Soon previews for the rest and routes straight to the hub, and the pet step is bypassed by `FeatureFlags.BypassPetSelect`. NOT felt-verified; no `.RESULT.md`)

Status: READY TO IMPLEMENT
Lane: L4 (UI / onboarding) — code-built UI only; no scene hand-edit
Author: Design/Creative agent (read-only RCA pass, 2026-06-24)
WO number: 503 verified next-free vs CLI_LANES_WO_NUMBERS.md (500/501/502 used; 503 free).

---

## 0. ONE-PARAGRAPH SUMMARY

Simplify the hero-select screen to feature the single V1 playable hero — the Knight,
display name "Grom" — as the hero. Layout: a LARGE hero image on the LEFT, his BACKSTORY
underneath it, his STATS in a panel BESIDE it (right of the image). Below/around that, show
the other three heroes (Thrain/Mage, Sylas/Ranger, Elara/Cleric) as a GREYED-OUT, locked,
non-selectable "coming later" carousel. A single SELECT/CONFIRM button persists Grom and
routes straight to the village home hub (MainCastle_Hall). The pet-select (echo) step is
ALREADY bypassed at runtime by `FeatureFlags.BypassPetSelect` (default ON) — this WO makes
that the only path, hard-routes HeroSelect -> Castle, and FLAGS the PetSelect scene/controller
for DELETION after the new flow is verified (do NOT delete in this WO).

GOOD NEWS FROM RCA: most of this is already half-built. The pet step is already a non-binding
teaser that persists nothing, the bypass flag already routes HeroSelect->Castle, the Grom
record + portrait + backstory copy already exist and are correctly wired. The bulk of this WO
is the LAYOUT redesign of HeroSelectController; the flow change is mostly a verification +
small cleanup.

---

## 1. SME FINDINGS (what already exists — read before touching anything)

### Files in play
- `Assets/_Modules/Onboarding/HeroSelectController.cs` — THE screen. Already 100% code-built
  (clears the UIDocument root, builds the whole tree in code; no UXML dependency — CLAUDE.md
  Section 8 honored). Currently renders FOUR even hero cards in a row + a shared detail card
  below. THIS is the file the layout redesign edits.
- `Assets/_Modules/Onboarding/HeroCatalog.cs` — static presentation catalog: 4 `HeroCardInfo`
  entries (Mage/Knight/Ranger/Cleric) with en.json keys + glyph + accent + 1-5 stat pips +
  signature ability name/desc. Knight entry: name key `hero.knight.name`, hp 5 / attack 3 /
  speed 2, ability "Bulwark Slam".
- `Assets/_Modules/Onboarding/PetSelectController.cs` — the pet/echo step. Already neutered:
  persists NOTHING (PET-ACQUISITION REWORK 2026-06-13 — the real bond happens at the Echo
  Hollow in town), and `OnEnable` already early-returns `SceneRouter.GoCastle()` when
  `BypassPetSelect` is ON. This is the file/scene to FLAG for deletion.
- `Assets/_Modules/Core/SceneRouter.cs` — scene routing. `HeroSelect` and `PetSelect` consts;
  `GoHeroSelect()`, `GoPetSelect()`, `GoCastle()` (-> `MainCastle_Hall`). `Village` const =
  "Village2" (NOT the home hub — the home hub is `Castle` = "MainCastle_Hall").
- `Assets/_Modules/Core/FeatureFlags.cs` — `SingleHero` (default ON), `KnightOnly` (default ON
  — `ChooseHero` forces class to Knight), `BypassPetSelect` (default ON).
- `Assets/_Modules/DialogueUI/IntroCommandBridge.cs` (line ~95) — the Title intro cinematic
  calls `SceneRouter.GoHeroSelect()` at its end. This is the Title -> HeroSelect entry. LEAVE
  AS-IS (HeroSelect is staying; only the step AFTER it changes).
- `Assets/Editor/IntroFlowSceneBuilder.cs` — editor builder that generates BOTH HeroSelect.unity
  and PetSelect.unity and registers them in Build Settings between Title and Village. Relevant
  only for the deletion-flag step (it still creates PetSelect).

### Hero data / art / copy that EXISTS to reuse (no new art needed for Grom)
- Display name "Grom" -> `hero.knight.name` in `Assets/StreamingAssets/Data/Canonical/en.json`
  AND the mirror `Assets/Resources/Data/Canonical/en.json` (line ~148). NO MISMATCH — the
  Knight record's display name is already "Grom". (Owner's "Grom" == `HeroClass.Knight`.)
- Role: `hero.knight.role` = "Lightbearer".
- BACKSTORY (reuse verbatim): `hero.knight.blurb` = "Hammer in hand and the sun's emblem on
  his shield, Grom carries blessed steel into the breach. Where his light falls, the Hollowed
  falter and the walls hold."
- STATS (reuse): from HeroCatalog Knight entry — HP 5/5, Attack 3/5, Speed 2/5 pips; signature
  ability "Bulwark Slam" — "Cleaving slam — hits all foes in front."
- LARGE hero image: `Assets/_Modules/Onboarding/Resources/HeroPortraits/Grom.jpg` EXISTS
  (loaded via `Resources.Load<Sprite>("HeroPortraits/Grom")` / `SlugFor(HeroClass.Knight)`
  returns "Grom"). The locked carousel art also exists: Thrain.jpg, Sylas.jpg, Elara.jpg in the
  same folder.

### NO MISMATCH TO FLAG
The owner's "Grom" maps cleanly to `HeroClass.Knight` with the display name already "Grom" in
en.json and the portrait already named Grom.jpg. CLI wires the EXISTING Knight record — nothing
to reconcile. (Documented here so CLI does not go hunting.)

---

## 2. THE LAYOUT SPEC — single-hero screen (redesign of HeroSelectController.BuildScreen)

Keep the screen 100% CODE-BUILT (no UXML — CLAUDE.md Section 8). Keep the existing
ElarionUi palette (PanelStone / Gold / Parchment) and the responsive GeometryChangedEvent
re-flow pattern. Keep the post-build self-assert (adapt it to the new tree). Replace the
"four even cards + shared detail card" body with a HERO STAGE + LOCKED CAROUSEL.

### Zones (root is a full-screen column on ColBackground)
```
root (column, full screen, ColBackground)
  |- brand block      (title + subtitle, centered, top)  [keep existing]
  |- gold divider     [keep]
  |- eyebrow "— YOUR HERO —"   [reword from "CHOOSE YOUR HERO"; only one to choose]
  |- HERO STAGE (row, flexGrow 1)        <-- the new centerpiece
  |    |- LEFT COLUMN (flexGrow ~1.3, the larger half)
  |    |    |- HERO IMAGE   (large, gold-framed, Grom.jpg, ScaleAndCrop, ~3:4)
  |    |    |- BACKSTORY    (UNDER the image: name (gold, big) + role (amber) +
  |    |                     hero.knight.blurb (parchment, wrap))
  |    |- RIGHT COLUMN (flexGrow ~1, the stats panel BESIDE the image)
  |         |- STATS PANEL  (PanelStone card, gold rim):
  |              HP      *****  (PipString(5))
  |              ATTACK  ***oo  (PipString(3))
  |              SPEED   **ooo  (PipString(2))
  |              -- divider --
  |              "Bulwark Slam" (amber, bold)
  |              "Cleaving slam — hits all foes in front." (parchment dim)
  |- LOCKED CAROUSEL (row, flexShrink 0, bottom band)    <-- "coming later"
  |    |- locked card: Thrain   (Mage)   greyed + lock badge, NOT selectable
  |    |- locked card: Sylas    (Ranger) greyed + lock badge, NOT selectable
  |    |- locked card: Elara    (Cleric) greyed + lock badge, NOT selectable
  |- footer (row, center)
       |- SELECT/CONFIRM button "Enter Elarion" (or reuse "Dive into Village" copy)
          -> primary gold CTA, ENABLED by default (single hero is pre-selected)
```

### Responsive rule (BONES, must hold both orientations)
- LANDSCAPE: HERO STAGE is a ROW (image+backstory left, stats right). LOCKED CAROUSEL is a
  3-wide row across the bottom.
- PORTRAIT (height >= width): HERO STAGE re-flows to a COLUMN (image on top, stats below,
  backstory under image) so nothing crowds; carousel stays a 3-wide row (it is small). Use the
  existing `ReflowForSize(width,height)` + `GeometryChangedEvent` hook — NO fixed pixel x/width
  on the stage columns (flex/percent only). This is the same regression-proofing the current
  screen already has; preserve it.

### Single-hero selection behavior
- Grom (Knight) is PRE-SELECTED on build: set `_selectedHero = HeroClass.Knight`,
  `_hasSelection = true`. The big stage IS the selection — there is no "tap to choose" among
  the playable set (only one). The confirm CTA is enabled immediately.
- KnightOnly is already ON and `ChooseHero` already forces Knight, so even if selection logic
  is simplified, persistence is correct.

### Locked carousel cards (NON-SELECTABLE — "coming later")
For each of Mage / Ranger / Cleric build a small card:
- Portrait from `HeroPortraits/<slug>` (Thrain/Sylas/Elara) but desaturated/dimmed: overlay a
  semi-opaque dark scrim (e.g. `new Color(0,0,0,0.55f)`) over the portrait, OR tint the card
  background to `ElarionUi.Disabled` and set the portrait element `opacity = 0.35f`.
- A LOCK affordance: a lock glyph (use a Unicode padlock "🔒" or the text "LOCKED")
  centered on the card + a small "Coming later" caption in ParchmentDim.
- `pickingMode = PickingMode.Ignore` on the card (and DO NOT register a PointerDownEvent) so it
  is provably non-interactive — a tap does nothing, never selects.
- Name + class label shown dim (so the player sees who is coming) but visually "off".

### Copy
- Reuse all Grom copy from en.json (no new strings required). Eyebrow reword "YOUR HERO" is an
  inline literal (or add a new key `heroSelect.yourHero` if CLI prefers — optional, not blocking).
- Confirm CTA: reuse existing `heroSelect.diveVillage` ("Dive into Village") OR a new literal
  "Enter Elarion". Owner-tunable copy (FINESSE) — pick one, note it in the RESULT.
- PLACEHOLDER FLAG: none needed for Grom — all art + copy + stats exist. The locked carousel
  reuses existing portraits. If any locked portrait fails to load, fall back to the existing
  glyph path (already in `BuildCard`). No missing-asset risk.

---

## 3. THE FLOW CHANGE — remove PetSelect from the boot sequence

### Current chain (verified)
`Title (intro cinematic)` --`IntroCommandBridge.GoHeroSelect()`--> `HeroSelect`
--`HeroSelectController.OnDiveVillageClicked()`--> [if `BypassPetSelect` ON] `GoCastle()`
else `GoPetSelect()` --> `PetSelect` --`PetSelectController`--> `GoCastle()`.

### Exact edit points
1. `Assets/_Modules/Onboarding/HeroSelectController.cs`, `OnDiveVillageClicked()` (line ~833):
   it currently branches on `FeatureFlags.BypassPetSelect`. Since V1 is single-hero permanently,
   make the confirm route DIRECTLY to the home hub:
   - Call `PersistHero()` then `SceneRouter.GoCastle()` unconditionally.
   - You MAY keep the flag branch as a reversibility hatch (flag OFF -> old `GoPetSelect()`),
     but the DEFAULT/normal path must be HeroSelect -> Castle with NO pet step. Owner decision
     in Section 7 on whether to keep the hatch or hard-wire.
2. `Assets/_Modules/Onboarding/HeroSelectController.cs`, returning-player gate
   `IsIntroComplete()` (line ~167): **DOWNSTREAM DEPENDENCY — MUST FIX.** It currently returns
   true only when BOTH `HeroClass != None` AND `StarterPetId` is non-empty. Because the pet
   step no longer sets `StarterPetId`, this gate will NEVER be satisfied -> a returning player
   who already picked Grom would be RE-SHOWN the hero-select every launch. **Change the gate to
   `HeroClass != None` alone** (drop the `StarterPetId` requirement) so a returning player skips
   straight to the castle. This is the single most important non-obvious fix in this WO.
   - (Same dependency note: `PetSelectController.HasStarterPet()` / its returning gate become
     dead once PetSelect is removed — fine, they go with the file in the deletion step.)
3. `SceneRouter` `GoPetSelect()` + `PetSelect` const: LEAVE for now (the deletion-flag step
   removes them after verification). Routing no longer calls `GoPetSelect()` from the live path.

### Does anything REQUIRE a pet to proceed? (answer: NO)
- `GameState.StarterPetId` is the only thing PetSelect ever wrote, and the rework already made
  PetSelect write NOTHING. So nothing downstream blocks on a pet being chosen at boot. The
  Echo/pet bond happens later at the Echo Hollow (`PetAcquisitionService.Acquire` via the
  PetHouse). No default pet needs to be set. CONFIRMED no boot-blocking pet dependency.
- The ONLY code that read `StarterPetId` as a boot gate is `HeroSelectController.IsIntroComplete`
  (fixed in edit point 2 above) and `PetSelectController`'s own returning gate (deleted with the
  file). Save/Load round-trip tests reference `StarterPetId` as a field — leave the FIELD on
  `GameState`/`SaveSchema` intact (do NOT remove the field; the Echo Hollow may still use it and
  save migration depends on it).

---

## 4. DELETION FLAG — pet-select files/scene to remove AFTER verification

GATE: **Do NOT delete anything in this WO. Flag only.** Deletion is a SEPARATE follow-up WO
(call it WO-504-pet-select-purge) that runs ONLY after the owner felt-verifies the new flow
(Title -> HeroSelect(Grom) -> Castle, returning player skips correctly, no blank/dead screens).

### Checklist to delete later (once flow verified working):
- [ ] `Assets/_Modules/Onboarding/PetSelectController.cs` (+ `.meta`)
- [ ] `Assets/Scenes/PetSelect.unity` (+ `.meta`) — and DE-REGISTER it from Build Settings
      (it is inserted by `IntroFlowSceneBuilder` between HeroSelect and Village).
- [ ] `Assets/_Modules/Onboarding/UI/PetSelectScreen.uxml` (+ `.meta`) — the stale UXML the
      builder attaches (unused at runtime since the screen is code-built, but tied to PetSelect).
- [ ] `SceneRouter.PetSelect` const + `SceneRouter.GoPetSelect()` method (in `SceneRouter.cs`).
- [ ] `IntroFlowSceneBuilder.cs` — remove the PetSelect scene generation + Build-Settings
      insert (keep HeroSelect generation). This is editor-only.
- [ ] `FeatureFlags.BypassPetSelect` — retire ONLY after the hard-wire in Section 3 lands and
      no code references it (it becomes dead once HeroSelect routes straight to Castle).
- [ ] Reword the `SceneRouter` header comment + `IntroFlowSceneBuilder` header that still
      describe the "Title -> HeroSelect -> PetSelect -> Village" chain.

### DO NOT DELETE — the Echo WORKFORCE / pet systems are SEPARATE (boundary flag)
The pet-SELECT screen is NOT the pet/echo gameplay system. Keep ALL of these — they are the
real, live pet/echo features and have nothing to do with the boot screen:
- `Assets/_Modules/Pets/**` (PetDeployer, PetAcquisitionService, PetProgression, PetClipPlayer,
  PetIdleRoutines, PetEmoteController, PetBillboard, MineNodeBridge, etc.) — the in-town echo
  bonding + harvest workforce.
- `Assets/_Modules/Onboarding/IntroPetCatalog.cs` — reads pets.json; still used by the Echo
  Hollow path / catalog. (It is referenced by PetSelectController too, but it is NOT pet-select-
  only — verify no other references break before removing in the purge WO; default = KEEP.)
- `GameState.StarterPetId` / `SaveSchema` pet fields — KEEP (save migration + Echo Hollow).
- The echo WORKFORCE / harvest system (drag-drop assign, wood/iron/grain) — entirely separate,
  untouched. (Memory: echo-workforce-drag-drop — that is the value loop, not this boot screen.)

CONFLATION GUARD: if a purge agent later greps "pet" or "echo" and finds these, they STAY.
Only the four PetSelect-specific assets + the two SceneRouter members + the IntroFlowSceneBuilder
PetSelect block + the dead flag are in scope for deletion.

---

## 5. REUSE MAP

| Need | Reuse (already exists) |
|---|---|
| Large hero image | `Resources/HeroPortraits/Grom.jpg` (via `SlugFor(Knight)`="Grom") |
| Backstory text | `hero.knight.blurb` in en.json (both copies) |
| Name / role | `hero.knight.name`="Grom", `hero.knight.role`="Lightbearer" |
| Stats + ability | `HeroCatalog` Knight entry (hp5/atk3/spd2, "Bulwark Slam") + `PipString()` |
| Locked carousel art | `HeroPortraits/Thrain.jpg`, `Sylas.jpg`, `Elara.jpg` |
| Palette / chrome | `ElarionUi.*` (PanelStone/Gold/Parchment/Gilt/Disabled) |
| Responsive re-flow | existing `GeometryChangedEvent` + `ReflowForSize` in HeroSelectController |
| Code-built idiom | existing `BuildScreen()` (clears root, builds in code) — adapt, don't rewrite |
| Persist hero | `GameStateService.ChooseHero(Knight)` (already KnightOnly-forced) |
| Route to home hub | `SceneRouter.GoCastle()` -> `MainCastle_Hall` |

---

## 6. ACCEPTANCE CRITERIA

Gate-provable (BONES — verify headless / by code path):
1. HeroSelect screen builds with Grom pre-selected; confirm CTA enabled on load.
2. Confirm routes to `MainCastle_Hall` (Castle) directly — NO PetSelect scene ever loads on the
   normal path (assert PetSelect.OnEnable is not reached, or that the next active scene after
   HeroSelect confirm is `MainCastle_Hall`).
3. `GameState.HeroClass == Knight` persisted after confirm (ChooseHero called).
4. Returning player (save has HeroClass=Knight, StarterPetId empty) SKIPS HeroSelect straight to
   Castle — `IsIntroComplete()` returns true on HeroClass alone (the dependency fix).
5. Self-assert: hero stage present (image + stats + backstory) AND exactly 3 locked carousel
   cards exist AND none of the 3 has a click handler / all are PickingMode.Ignore.
6. C# brace-balance gate passes on every edited file (CLAUDE.md Section 1). No NUL bytes.
7. No UXML dependency introduced — screen is fully code-built (CLAUDE.md Section 8).

Felt-tune (FINESSE — owner verifies):
8. Large image reads as the centerpiece; backstory legible under it; stats panel beside it
   (landscape) / below it (portrait).
9. Locked carousel clearly reads as "coming later / locked," visibly greyed, obviously
   non-tappable.
10. Confirm CTA copy + colors feel right; nothing crowds in either orientation.

---

## 7. BONES vs FINESSE + OWNER DECISIONS

BONES (gate-provable, do these solidly): the flow change (HeroSelect->Castle, no PetSelect),
the `IsIntroComplete` dependency fix, the pre-selected single hero + persistence, the locked
carousel being provably non-selectable, code-built + brace gate. These must be correct.

FINESSE (owner felt-tune after): exact image size/ratio, stat panel styling, locked-card
desaturation strength, confirm CTA label, portrait vs landscape spacing. Ship the bones; the
owner tunes the feel.

OWNER DECISIONS NEEDED:
- D1. Keep `BypassPetSelect` as a reversibility hatch (flag OFF restores the old pet step) OR
  hard-wire HeroSelect->Castle and retire the flag now? (Recommend: hard-wire the route in this
  WO, retire the flag in the WO-504 purge — so the live path is unconditional but cleanup is
  staged.)
- D2. Confirm CTA copy: reuse "Dive into Village" or new "Enter Elarion"? (Recommend: "Enter
  Elarion" — the destination is the home hub, not a TD raid.)
- D3. Eyebrow text "— YOUR HERO —" inline literal vs new en.json key `heroSelect.yourHero`?
  (Either fine; literal is lower-friction.)

## 8. WHAT NOT TO TOUCH
- Do NOT hand-edit any `.unity` scene (HeroSelect.unity / PetSelect.unity stay as the builder
  generated them; the screen is code-built so no scene edit is needed).
- Do NOT delete anything (deletion is the separate post-verification WO-504).
- Do NOT touch `Assets/_Modules/Pets/**`, the echo workforce/harvest system, or
  `GameState.StarterPetId`'s field definition.
- Do NOT touch the Title intro cinematic / `IntroCommandBridge` entry (Title->HeroSelect stays).
- Do NOT reintroduce UXML binding (CLAUDE.md Section 8).
- Single-writer note: HeroSelectController is the only file the layout edit touches; the flow +
  dependency fix is in the same file plus (optionally) SceneRouter. File-disjoint from other lanes.
