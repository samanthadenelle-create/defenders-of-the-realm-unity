# WO-786 — Raid End: punchy star reveal + audio

**Status:** READY TO IMPLEMENT
**Minted:** 2026-07-30 (OWNER-authored spec, transcribed verbatim below)
**Lane:** Village / Camps / Raid **presentation** — file-disjoint from the army-settlement logic
**Priority:** High
**Depends on:** the final star calculation rules (troop survival %) — **SATISFIED**, see Dependency below

---

## Goal

Make the moment the player receives stars feel **premium, weighty, and clearly differentiated** —
especially 3-star — while remaining extremely cheap to build and maintain.

## Copy

- **3-star stamp text: `FLAWLESS`**

## Exact sequence

1. Raid victory condition met
2. Brief dramatic hold (**0.45s**) — mild vignette / desaturation
3. **Stars slam in one by one (left -> right)**
   - Scale: `0 -> 1.3 -> 1.0`
   - Strong ease-out
   - Small screen shake (noticeably heavier on the 3rd star)
   - Delay between stars: **0.30s**
4. **3-star only (premium layer):**
   - Final star receives a bigger punch + short gold flash
   - Quick radial gold pulse behind the stars (~**0.3s**)
   - **`FLAWLESS`** stamp slams in under the stars
5. Short appreciation beat (**0.4s**)
6. Normal victory panel continues as usual

## Audio cues (required)

| Moment | Sound description | Notes |
|---|---|---|
| Each star impact | Solid, weighty "star hit" | Same sound, slight volume/pitch increase on later stars |
| 3rd star (3-star only) | Brighter, more triumphant version of the star hit | Clear step up in intensity |
| `FLAWLESS` stamp | Sharp, satisfying lock-in / stamp sound | Short and punchy |
| Optional bed | Very low, short riser or swell under the whole reveal | Keep extremely subtle |

All sounds should be short. **Reuse or lightly edit existing UI/combat impact sounds where possible.**

## Technical rules (keep it cheap)

- Tween library only — **no new AnimationClips** *(see the BLOCKING NOTE below — the named library is
  not in this project)*
- Simple filled / empty stars
- Reuse existing UI particles / VFX for impact bursts if available
- **Total added time before the victory panel <= 2.1 seconds**
- Must stay stable at **60fps on Seeker and mid-range Android**

## Acceptance criteria

- 1-star / 2-star / 3-star are clearly distinguishable **by feel and audio intensity**
- 3-star + `FLAWLESS` feels distinctly more rewarding
- Audio is synchronized with the visual hits
- **No layout jumps or overlapping elements**
- Verified by **screenshot + short screen recording** (headless + device preferred)
- Works correctly **regardless of current veterancy rank**

---

## CLI notes (added at transcription — verify before implementing)

### Tween library — OWNER RULED 2026-07-30: ADD DOTween

DOTween was **not** in the project when this WO was transcribed (no `Assets/Demigiant`, zero
`using DG.Tweening` hits). The owner ruled to **add it**, and supplied the target idiom verbatim:

```csharp
star.transform.localScale = Vector3.zero;
star.transform.DOScale(1.3f, 0.25f).SetEase(Ease.OutBack)
    .OnComplete(() => star.transform.DOScale(1f, 0.15f));
```

**Import path (headless-doable — no Asset Store download needed):** `Packages/manifest.json` already
declares an **OpenUPM scoped registry** (currently scoped to `com.cysharp.unitask`). DOTween ships
there as **`com.demigiant.dotween`** — add that scope + the dependency and Unity resolves it. The UPM
build ships pre-made asmdefs, so it does **not** need the DOTween Utility Panel step that the legacy
`.unitypackage` requires.

**Landed as its own isolated change** (WO-786 P0), NOT folded into an unrelated batch — a package
resolution that fails must be attributable to itself.

**Verify at import (do not skip — this is the AOT-risk half):**
1. `COMPILE_GATE_OK` with DOTween resolved.
2. **IL2CPP/AOT + managed stripping.** DOTween's shortcut extensions are reflection-adjacent; the
   project already ships `Assets/link.xml`, so add DOTween preservation there if stripping removes
   anything. `managedStrippingLevel` is set in `ProjectSettings.asset` — check it before assuming.
3. **A real Android/Seeker build**, not just the editor. Editor-green proves nothing about IL2CPP.
4. `DOTween.Init` / capacity: set an explicit tween capacity rather than relying on the default
   auto-grow, so a reveal never allocates mid-animation on a 60fps budget.
5. `SetUpdate(isIndependentUpdate: true)` on the reveal tweens if the victory beat can run while
   `Time.timeScale` is modified (the death/hitstop path does modify it).

**Fallback if DOTween ever has to come out:** `PanelOpenCloseFx`
(`Assets/_Modules/Core/UI/ElarionUiKitConformance.cs:468-546`) is the project's existing coroutine-driven
eased scale/fade and can express the same `0 -> 1.3 -> 1.0` punch. Recorded so the reveal is not
permanently welded to a third-party dependency.

### Dependency is satisfied

The star rules this depends on landed 2026-07-30 (WO-783 D3, owner ruling): `RaidScoring.ComputeStars`
is now the two-axis ladder — **1** = just cleared, **2** = cleared with high survival **OR** under the
clock, **3** = cleared with high survival **AND** under the clock. Survival is
`RaidScoring.SurvivalPct` (survivors / deployed, 1f when nothing was deployed) against the
`RaidScoring.HighSurvivalPct` threshold (0.70). **Read that const, never re-derive the threshold.**

This matters for the reveal: **3 stars is now genuinely rare.** Under the old formula every victory
scored 3, so a `FLAWLESS` stamp would have fired on every single win and meant nothing.

### Where it hooks

- The victory sequence is `RaidVictoryController.HandleCleared` (`Village/World/Camps/`), which calls
  `ShowVictoryScreen(configId, joined, result, loot)` — `result.Stars` is the tier to reveal.
- The screen itself is the shared Obsidian `EndStateView` / `EndStateVM`
  (`Village/UI/EndState/`), reached via `EndStateVM.FromRaidVictory`.
- **Insert the reveal BEFORE the victory panel**, per step 6. Note `ReconcileArmy(result)` already runs
  before `ShowVictoryScreen` and must STAY there — the army settlement must never sit behind a
  presentation beat that can throw or be skipped.

### Project law this must respect

- **Presentation is a separate layer that never touches the objects** (`ARCHITECTURE_PRINCIPLES` §2).
  The reveal reads `RaidResult`; it must not mutate army, loot or scoring state.
- **Code-built uGUI via ElarionUiKit only — no UXML/UIDocument** (it does not render in player builds).
- **ASCII-only TMP strings.** `FLAWLESS` is safe; keep any added copy ASCII.
- **Never convey meaning by colour alone** (owner is red/green colourblind). The gold flash and gold
  pulse are *accents* — the tier must remain readable from the star COUNT and the `FLAWLESS` word, which
  it is. Do not add a colour-only tier cue.
- **Touch/​layout:** `MinTouchPx = 112` floor and the WO-779 spacing rule (margins are deliberate
  multiples of `PadCard` 12 / `PadPanel` 18, never raw literals). "No layout jumps" is an explicit
  acceptance criterion — the stars must occupy their final footprint from frame one and scale *within*
  it, not reflow the panel as each lands.
- **`PanelManager`**: the reveal is a top-band surface — register it, or explicitly document why not.
  The `[modal-registration]` oracle lints this.

### Audio — what already exists to reuse

Per the spec's "reuse existing sounds where possible", these are on disk in
`Assets/_Modules/Audio/Resources/Sfx/`: `BuildingUpgrade.wav`, `Sfx_ArcaneExplosion.wav`,
`Sfx_Shockwave.wav`, `Sfx_LevelUp.wav`, `UiClick.wav`, `Swords_Clash.wav`, `Spell_Impact.wav`.
`Sfx_LevelUp` / `BuildingUpgrade` are the closest existing "achievement lock-in" candidates for the
stamp; `Spell_Impact` / `Swords_Clash` for the star hits with the pitch/volume ramp.
⚠ **`SfxClipLibrary.asset` does not exist**, so the `SfxId` path is a dead no-op — route audio through
the Village-side `GameSfx` (`Resources.Load("Sfx/<name>")` with a procedural fallback), which is what
actually plays sound today. Any genuinely new clip is an OWNER asset decision, not a CLI pick.

### Screen shake

There is no shared screen-shake helper; the existing shake is the `camera_shake` dialogue verb
(`DialogueCommandSink`). Either reuse that seam or add a small self-contained UI-space shake on the
reveal canvas — prefer UI-space, since shaking the gameplay camera during a settled raid can fight the
return-home fade.

### Verification (owner standing rule)

Screenshot proof is required before this reaches the owner. The headless capture
(`UICaptureLaunch.RunCaptureHeadless`) is **edit-mode and synchronous**, so it can shoot the reveal's
FINAL FRAME per tier (1/2/3-star + `FLAWLESS`) but **cannot** prove timing, easing, shake or audio sync
— those need the device recording the spec already asks for. Build the reveal so each tier's end state
is reachable from a pure builder method with no live services, the way `CaptureFoundingEchoCard` and
`CapturePauseMenu` are, and add all three tiers to the capture set.

## Do NOT touch

- `RaidScoring.ComputeStars` / `HighSurvivalPct` — read them, never re-derive or re-tune here.
- `ReconcileArmy` / `ReconcileRaidEnd` ordering — settlement stays ahead of presentation.
- Loot maths (`ComputeLoot`) — the reveal displays, it does not compute.
