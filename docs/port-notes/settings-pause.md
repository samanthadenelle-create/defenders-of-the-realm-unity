# Settings menu + pause overlay — port notes (audit P0-8 / P0-10)

**Date:** 2026-05-19
**Source audit:** `docs/audit/missing-components.md` §2.1 (no settings menu, P0-8)
and §2.3 (no pause system, P0-10).
**Scope:** source only — Unity was not run. The integrator wires the two
overlay GameObjects into the gameplay scenes.

This pass builds the two missing-component P0s the audit flags: a settings /
options screen and a pause overlay. Both are UI Toolkit, styled to match the
existing `VillageHud` / `BuildMenu` / `BattleHUD` visual language.

---

## Module choice — a new `DeNelle.Settings` module

Created a **new module** rather than folding into `DeNelle.HUD`. Reasoning:

- The audit (per-module table) says **Core is specced to own "settings"** but
  Core has no UI. Putting a UI-Toolkit screen *in* `DeNelle.Core` would give
  the core assembly a UI dependency it deliberately does not have.
- `DeNelle.HUD` is a *passive, in-game* display (its own header: "owns no
  gameplay state", "passive display"). A modal options menu + a pause overlay
  that freezes `Time.timeScale` and routes scenes is not a passive HUD widget —
  bundling it would muddy that module's contract.
- Pause and Settings are coupled (the pause overlay opens the settings screen),
  so they belong **together**. A separate module lets them reference each other
  without a cross-module hop.

`DeNelle.Settings` references **`DeNelle.Core` + `UniTask` only** — no gameplay
module. Module isolation (port-spec Part 2) is respected: pausing is done with
the engine-global `Time.timeScale`, and quit-to-title uses the Core
`SceneRouter`, so no reference to Village / BattleATB / Dungeons is needed.

---

## Files created

| File | Purpose |
|------|---------|
| `Assets/_Modules/Settings/DeNelle.Settings.asmdef` | New module — references `DeNelle.Core`, `UniTask`. |
| `Assets/_Modules/Settings/SettingsModel.cs` | Persisted options store + apply layer. `QualityTier` enum, `ScreenShakeSetting` static flag. |
| `Assets/_Modules/Settings/AudioMixerBridge.cs` | The slider→`AudioMixer` seam — resolves the mixer by name, no-ops if absent. |
| `Assets/_Modules/Settings/SettingsBootstrap.cs` | `[RuntimeInitializeOnLoadMethod]` — re-applies persisted settings at launch. |
| `Assets/_Modules/Settings/SettingsScreen.uxml` / `.uss` | Options screen layout + styling. |
| `Assets/_Modules/Settings/SettingsController.cs` | `MonoBehaviour` driving the options screen. |
| `Assets/_Modules/Settings/PauseOverlay.uxml` / `.uss` | Pause menu layout + styling. |
| `Assets/_Modules/Settings/PauseController.cs` | `MonoBehaviour` — Esc-to-pause, `Time.timeScale` freeze, Resume / Settings / Quit. |

No `.meta` files were hand-authored for the `.cs` / `.uxml` / `.uss` files
(Unity generates them). The `.asmdef` was authored by hand, as instructed.

---

## The settings screen

`SettingsController` + `SettingsScreen.uxml`. A **modal overlay** (full-screen
scrim, captures input) — `Open()` / `Close()` show and hide it; it owns no
scene. Offers:

- **Audio** — Master / Music / SFX volume sliders (range **0..1.5**, per
  `audio-mix-spec.md` §2 — a player can push past unity gain) + a global mute
  toggle.
- **Graphics** — a three-tier quality selector: **Low (30 FPS) / High (60 FPS) /
  Desktop (60 FPS)**, mapping to the `Seeker_Low` / `Seeker_High` / `Desktop`
  `QualitySettings` tiers `SeekerBootstrap` manages. Buttons are built at
  runtime from `QualityTier` (the established pattern — `VillageHud`'s ability
  cells, `BuildMenu`'s cards).
- **Accessibility** — a **screen-shake** on/off toggle (audit §2.7 reduce-motion
  family). Gameplay camera code reads `ScreenShakeSetting.Enabled`.
- **Reset to defaults** and **Back** buttons.

Every control persists + applies the instant it changes — there is no separate
"save" step.

---

## How settings persist

A deliberate **two-store split**, both durable across launches:

1. **Music + SFX volume, and the global Mute** already have first-class fields
   in the Core `GameState` SO (`#21` `MusicVolume`, `#22` `SfxVolume`, `#20`
   `Muted`) and typed setters on `GameStateService` (`SetMusicVolume` /
   `SetSfxVolume` / `SetMuted`). `SettingsModel` routes these **through
   `GameStateService`** so they land in the canonical `dotr-save` PlayerPrefs
   blob with the rest of the save and stay schema-consistent.
   - Note the scale conversion: `GameState` stores volume **0..100**; the UI
     works **0..1.5**. `SettingsModel` converts both ways.
   - Note also `GameState.Muted` fresh-default is `true` (an a11y choice — a new
     visitor starts muted). `ResetToDefaults` deliberately sets it `false` (a
     player who is *on* the settings screen is past the first-visit mute).

2. **Master volume, the Quality tier, and the Screen-shake toggle** have **no
   `GameState` field**. Adding fields to the 41-field persisted save schema is a
   Core / `SaveSchema` / `SaveMigrator` change, out of this task's scope. These
   three persist to **`PlayerPrefs`** under their own keys
   (`dotr-settings-master-volume`, `dotr-settings-quality-tier`,
   `dotr-settings-screen-shake`). PlayerPrefs is the *same* backing store
   `GameStateService` itself uses — this is a lighter-weight slot, not a
   parallel persistence system.

**Reapply on launch:** `SettingsBootstrap` runs via
`[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` — no scene wiring — and calls
`SettingsModel.ApplyAll()`, pushing every stored value at its live target. It
runs at `AfterSceneLoad` (not `BeforeSceneLoad`) deliberately: it must run
*after* `SeekerBootstrap` (which auto-picks a tier from the hardware) so a
player's explicit tier choice wins, and the audio apply needs
`GameStateService` to exist.

---

## The pause overlay

`PauseController` + `PauseOverlay.uxml`. A modal overlay with **Resume /
Settings / Quit to Title**.

- **Pause input:** the **Esc key** (new Input System — `Keyboard.current`) and a
  public `TogglePause()` a HUD pause button can call. On keyboard-less hardware
  `Keyboard.current` is `null`, so a **HUD pause button is required for mobile**
  (integrator wiring — see below).
- **`Time.timeScale`:** `Pause()` captures the current `Time.timeScale` and sets
  it to `0` (freezing wave timers, the ATB tick, enemy/hero movement — all read
  `Time.deltaTime`); `Resume()` restores the captured value. Capturing rather
  than assuming `1.0` means pausing during a slow-mo / fast-forward effect
  restores *that*, not a hard 1.0. `OnDestroy` un-freezes as a safety net.
- **Quit to Title** restores `Time.timeScale` **first** (the next scene must
  never load frozen) then `SceneRouter.GoTitle()`. This also closes the audit's
  §2.4 P1 gap "no quit-to-title path from gameplay."
- **Settings from pause:** the Settings button opens `SettingsController` over
  the top; the pause panel hides while settings are up (the game stays frozen)
  and re-shows when `SettingsController` raises `SettingsClosed`.
- **Platform compliance:** `OnApplicationPause(true)` — Android backgrounding /
  an incoming call — auto-pauses, the behaviour audit §2.3 explicitly asks for.

---

## Audio-mixer seam — FLAGGED

An **Audio-system agent is building the `AudioMixer` in parallel** with this
work. At the time these files were written **no `.mixer` asset exists in the
project** (`Glob **/*.mixer` → none).

`AudioMixerBridge` is written **seam-safe** so this is not a blocker:

- It resolves the mixer **lazily** — first from a directly-assigned
  `AudioMixer` (`SettingsController` has a serialized field), else from
  **`Resources.Load<AudioMixer>("Audio/GameAudioMixer")`**.
- If the mixer is **absent, every call is a quiet no-op** — one warning logged,
  then silence. No exception, no hard dependency. The settings screen still
  shows a seam notice ("Audio mixer not yet wired — sliders are saved and will
  take effect once audio is integrated") that **hides itself automatically**
  once the mixer resolves.
- Slider→mixer conversion: a `0..1.5` linear slider → decibels via
  `Mathf.Log10(v) * 20` (`0`→full mute, `1.0`→0 dB, `1.5`→~+3.5 dB), then
  `AudioMixer.SetFloat`.

**Contract with the Audio agent — the single point of coupling:**

| What | Expected value |
|------|----------------|
| Exposed parameter — Master | `MasterVol` |
| Exposed parameter — Music | `MusicVol` |
| Exposed parameter — SFX | `SfxVol` |
| Mixer asset location (for the Resources fallback) | `Assets/.../Resources/Audio/GameAudioMixer.mixer` |

If the Audio agent's mixer uses **different exposed-parameter names**, update
the three `*Param` constants at the top of `AudioMixerBridge.cs`. If the mixer
lives elsewhere, either assign it to `SettingsController._audioMixer` in the
inspector or update `AudioMixerBridge.MixerResourcePath`. When a parameter name
mismatches, `SetGroup` logs a targeted warning rather than failing silently.

---

## What the integrator must wire

Neither overlay self-installs — both are UIDocument GameObjects the scene
builder / `VillageController` adds. `DeNelle.HUD` cannot see `DeNelle.Settings`,
so the HUD pause-button hook is the integrator's job.

1. **Settings overlay GameObject** — `UIDocument` + `SettingsScreen.uxml` +
   `SettingsController`. Sort-order **above** the HUD and the pause overlay.
2. **Pause overlay GameObject** — `UIDocument` + `PauseOverlay.uxml` +
   `PauseController`. Sort-order **above the HUD, below the settings overlay**.
   One per gameplay scene (Village, Dungeon, ATBBattle), or a shared
   `DontDestroyOnLoad` object — `SettingsModel` is static, no state is lost
   across scene loads.
3. **Pause → Settings:** assign the `SettingsController` to
   `PauseController._settings`. If left empty the pause "Settings" button hides
   itself.
4. **HUD pause button (REQUIRED for mobile):** add a pause `Button` to
   `VillageHud.uxml` (and the battle / dungeon HUDs) and, in the scene builder,
   hook `pauseButton.clicked += pauseController.TogglePause`. Esc already works
   with no wiring on desktop, but touch-only hardware has no keyboard.
5. **AudioMixer:** once the Audio agent ships the mixer, assign it to
   `SettingsController._audioMixer` (or place it at
   `Resources/Audio/GameAudioMixer.mixer`). Verify the exposed parameters are
   `MasterVol` / `MusicVol` / `SfxVol`.
6. **Quality tiers:** the selector depends on the `Seeker_Low` / `Seeker_High` /
   `Desktop` `QualitySettings` tiers existing — run **Defenders > Setup > Apply
   Mobile Settings** (`MobileSettings.cs`) if they are absent, or the selector
   applies a frame cap only and logs that the named tier is missing.
7. **Screen-shake:** gameplay camera-shake code should gate on
   `DeNelle.Settings.ScreenShakeSetting.Enabled` before applying shake.

---

## Notes / known follow-ups (deliberately not done here)

- **Master volume / quality / screen-shake live in PlayerPrefs, not the save
  schema.** The cleaner long-term home is three new `GameState` fields + a
  `SaveSchema` / `SaveMigrator` bump — a Core change out of scope here. Recorded
  so a future Core pass can fold them in.
- **No controls / input-remapping, language selector, or credits screen.** The
  audit §2.1 lists these as part of a full options menu; this pass delivers the
  audio + graphics + comfort core the audit names as the *minimum*. The screen
  is sectioned so adding a "Controls" / "Language" section later is additive.
- **`Time.timeScale = 0` freezes only `Time.deltaTime` consumers.** Any system
  that must keep ticking while paused must use `Time.unscaledDeltaTime`. UI
  Toolkit transitions are unscaled already.
- **Settings is reachable from pause but not yet from the title screen.** The
  audit §2.4 notes the title has only Start + Connect Wallet. Adding an
  "Options" button to `TitleScreen.uxml` that calls `SettingsController.Open()`
  is a small additive follow-up (an Onboarding-module change).

---

_Tend the Heart. Hold the dark. Let the player pause._
