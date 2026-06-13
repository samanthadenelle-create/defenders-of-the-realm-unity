# RCA — Castle Settings gear opens nothing (HelpMenu panel=`<null>`)

**Date:** 2026-06-13
**Scene:** MainCastle_Hall (castle home hub)
**Symptom (owner):** "still cant get to settings" — the top-right gear fires, the
overlay logs as shown, but nothing renders. Knock-on: AdminOverlay/dev tools also dead.
**Type:** READ-ONLY RCA. No code changed.

---

## 1. Pinned root cause

**The gear works. The HelpMenu's borrowed PanelSettings gets killed out from under it
by `OnboardingPanelGuard`, so the panel has no live `IPanel` to render into.**

This is cause **(c)** in the brief — *the borrowed panel got disabled after borrow* —
with a precise mechanism, not (a)/(b)/(d). Chain:

### Step 1 — HelpMenu borrows a panel by `panelSettings != null` (NOT by "is it live")
`Assets/_Modules/HUD/HelpMenu.cs:57-66`
```
if (_document.panelSettings == null) {
    foreach (var existing in FindObjectsByType<UIDocument>(Include, None)) {
        if (existing == _document || existing.panelSettings == null) continue;
        _document.panelSettings = existing.panelSettings;   // <- borrows the ASSET
        break;
    }
}
```
The borrow only checks that some UIDocument has a non-null `panelSettings` **asset**.
It does **not** check whether that document is enabled, rendering, or that the asset is
something safe to share. In MainCastle_Hall the only UI-Toolkit documents carrying a
PanelSettings asset are the onboarding-lineage docs (Title/HeroSelect/PetSelect leak
in, plus intro fade) — all backed by the single **`OnboardingPanelSettings`** asset
(`Assets/_Modules/Onboarding/Generated/OnboardingPanelSettings.asset`). So HelpMenu
borrows `OnboardingPanelSettings`. The borrow *succeeds* — `ActivePanelSettings` is
non-null — which is why the `enabled=false` "Help button hidden" branch
(`HelpMenu.cs:67-72`) never fires and the gear/ToggleOverlay path runs fully and logs
"Settings shown — display=Flex".

### Step 2 — OnboardingPanelGuard disables EVERY UIDocument bound to that asset, in this scene
`Assets/_Modules/Onboarding/OnboardingPanelGuard.cs:104-203`

MainCastle_Hall is a **non-onboarding** scene (`IsOnboardingScene` is false —
`OnboardingPanelGuard.cs:92-97`). On every scene load the guard sweeps all UIDocuments
and, for each one whose `panelSettings.name == "OnboardingPanelSettings"`
(`OnboardingPanelGuard.cs:164`):
```
root.style.display = DisplayStyle.None;     // line 177 — renders nothing
root.pickingMode  = PickingMode.Ignore;     // line 178
...
if (doc.enabled) doc.enabled = false;       // line 191 — tears down the panel/raycaster
```
The guard matches **by asset name**, not by instance. HelpMenu's own UIDocument now
carries `panelSettings.name == "OnboardingPanelSettings"` (it borrowed it in Step 1), so
**HelpMenu's document is one of the docs the guard disables** — directly, or it shares
the asset with docs the guard kills and Unity tears down the shared panel. Disabling the
UIDocument detaches its `rootVisualElement` from any live `IPanel`. That is exactly the
pointer-dump line **`panel=<null> (not attached/visible)`**: the VisualElement tree
exists in memory, `_overlay.style.display` is flipped to `Flex` by `SetOpen`
(`HelpMenu.cs:234`) — which is why the log says "display=Flex picking=Position" — but
there is no attached panel, so nothing is drawn and no pixels reach the screen.

**The "Settings shown" log is built-but-invisible, not data-empty and not
threw-and-skipped.** The code ran to completion against a dead panel.

### Why "duplicate HelpMenu suppressed" is a RED HERRING (cause (d) ruled out)
`Assets/_Modules/HUD/HelpMenuBootstrap.cs:35-43` does a **global** dedupe across all
loaded scenes and `return`s before creating a second instance when one exists. The
"duplicate suppressed" line is the additive **OuterWorld** load firing `sceneLoaded`
and correctly finding the existing MainCastle_Hall HelpMenu — it does **not** spawn a
stale second instance. The gear toggles `HelpMenu.Instance`, which is the one live
instance. The dead panel is that same live instance — not a phantom. So (d) is not it.

### Why (a) and (b) are not the root cause
- **(a) "no live panel to borrow":** there ARE borrowable docs (the onboarding-lineage
  ones), so the borrow succeeds — the issue is *what* it borrows, not *whether* it can.
- **(b) "borrow runs before any panel is live" (timing):** HelpMenu spawns at
  `AfterSceneLoad` (`HelpMenuBootstrap.cs:15`) and borrows in `Awake`, by which time the
  onboarding docs exist (the borrow succeeds, confirmed by the non-null
  `ActivePanelSettings`). The failure is the guard running *after* and tearing the panel
  down — an ordering between guard and HelpMenu, but the defect is the shared-asset
  coupling, not a missing-at-borrow-time panel.

### Knock-on: AdminOverlay / dev tools die for the same reason
`HelpMenu.OnOpenDevTools` (`HelpMenu.cs:409`) hands `ActivePanelSettings` to
`AdminOverlay.TryBuild` (`AdminOverlay.cs:75-108`). Since `ActivePanelSettings` is the
guard-killed `OnboardingPanelSettings`, AdminOverlay builds against the same dead/being-
killed panel (its own doc, sortingOrder 2710, is *also* matched and disabled by the guard
if it adopted that asset). Restoring HelpMenu's panel to a clean asset the guard never
touches fixes dev tools in the same stroke.

---

## 2. PanelSettings assets that exist in the project

| Asset | Path | In a `Resources/` folder? | Runtime-loadable? |
|---|---|---|---|
| OnboardingPanelSettings | `Assets/_Modules/Onboarding/Generated/OnboardingPanelSettings.asset` | No | No — and it's the **poisoned** one (guard target) |
| BattlePanelSettings | `Assets/_Modules/BattleATB/Generated/BattlePanelSettings.asset` | No | No |
| DungeonPanelSettings | `Assets/_Modules/Dungeons/Generated/DungeonPanelSettings.asset` | No | No |
| (DevPanelSettings) | referenced by `DevBootstrap.ResolvePanelSettings` via `Resources.Load<PanelSettings>("DevPanelSettings")` | — | **Does not exist** (optional override; Glob found none) |

**There is NO canonical PanelSettings asset in any Resources folder** — so HelpMenu
cannot today `Resources.Load<PanelSettings>(...)` its way to a reliable panel. All three
existing PanelSettings assets live outside Resources and are module-scoped.

**The shared theme DOES exist and is the only theme any of them use:**
- `Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss`
  (guid `0f935660100b1b54bb1efb7fe6c811e7`; content: `@import url("unity-theme://default")`).
  Verified: `OnboardingPanelSettings.asset:15` `themeUss` references exactly this guid.
  **It is NOT in a Resources folder either**, so it cannot be `Resources.Load`-ed as-is.

---

## 3. Recommended fix — ranked, with file:line

### PREFERRED — HelpMenu owns a self-built runtime PanelSettings; never borrow
**File:** `Assets/_Modules/HUD/HelpMenu.cs`, replace the borrow block at **lines 53-72**.

Stop borrowing a foreign (poisonable) asset. Build a private PanelSettings at runtime,
exactly the proven `DevBootstrap.ResolvePanelSettings` / `ArenaDefensePaletteUI` pattern
(`DevBootstrap.cs:117-130`, `ArenaDefensePaletteUI.cs:83-94`). Approach:

```
// (1) optional override if one is ever dropped in Resources
_document.panelSettings = Resources.Load<PanelSettings>("HelpPanelSettings");
// (2) build our OWN so nothing else can disable/share it
if (_document.panelSettings == null) {
    var ps = ScriptableObject.CreateInstance<PanelSettings>();
    ps.name = "HelpRuntimePanelSettings";       // NOT "OnboardingPanelSettings" -> guard ignores it
    ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
    ps.referenceResolution = new Vector2Int(1080, 1920);
    // borrow only the THEME (themeStyleSheet) from any live doc; never the whole asset
    foreach (var d in FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        if (d != null && d.panelSettings != null && d.panelSettings.themeStyleSheet != null)
            { ps.themeStyleSheet = d.panelSettings.themeStyleSheet; break; }
    _document.panelSettings = ps;
}
```

Why this is correct:
- **Guard can't touch it.** `OnboardingPanelGuard` matches `panelSettings.name ==
  "OnboardingPanelSettings"` (`OnboardingPanelGuard.cs:164`). A privately-named instance
  is invisible to the guard — the panel stays attached, `panel != null`, and the overlay
  renders.
- **No shared-asset coupling.** HelpMenu's panel lifetime is its own; disabling
  onboarding docs no longer collaterally kills Settings.
- **Text still renders.** WO-417 already forces an explicit `LegacyRuntime.ttf` on every
  Label/Button (`HelpMenu.cs:200-205, 217`), so glyphs draw even if the borrowed
  `themeStyleSheet` is null. Borrowing the theme when available is belt-and-braces.
- `sortingOrder = 2700` (`HelpMenu.cs:78`) is unchanged and still tops the uGUI town HUD.

**Even more robust (optional, recommended belt):** instead of borrowing the theme from a
sibling, make the canonical theme `Resources`-loadable so it never depends on a sibling
existing. Either move/copy `Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss`
into a `Resources/` folder and `Resources.Load<ThemeStyleSheet>(...)` it, or author a
committed `Assets/.../Resources/HelpPanelSettings.asset` (a real PanelSettings with the
theme wired) and let path (1) above load it. This removes the last "no sibling theme"
edge case. (This is editor/asset work for the owner, not required for the core fix —
the runtime-create path renders fine with WO-417's explicit font even if
`themeStyleSheet` ends up null.)

### "Duplicate HelpMenu suppressed"
**No fix needed — not the bug.** It is the correct global-dedupe message from the
additive OuterWorld load (`HelpMenuBootstrap.cs:35-43`). The gear targets the single live
`HelpMenu.Instance`, which is the same instance whose panel was killed. There is no stale
second instance to re-target. (If desired, downgrade the `FlowTrace.Warn` at
`HelpMenuBootstrap.cs:40` to `Step` so it stops reading like an error — cosmetic only.)

### Confirm AdminOverlay / dev tools restored
**Yes, same fix restores them.** `OnOpenDevTools` lends `ActivePanelSettings` to
`AdminOverlay.TryBuild` (`HelpMenu.cs:409`, `AdminOverlay.cs:75-108`). Once
`ActivePanelSettings` is HelpMenu's own un-poisoned runtime PanelSettings (named
`HelpRuntimePanelSettings`), AdminOverlay builds against a live panel the guard ignores,
its `sortingOrder = 2710` (`AdminOverlay.cs:104`) sits above HelpMenu's 2700, and dev
tools render. No separate AdminOverlay change required.

### SECONDARY (do NOT prefer) — fix the guard instead of HelpMenu
The guard *could* be taught to skip docs owned by HelpMenu/AdminOverlay. This is worse:
it hard-codes cross-module knowledge into `DeNelle.Onboarding`, and HelpMenu would still
be sharing a foreign asset whose other holders the guard disables (Unity may still tear
down the shared panel). The clean invariant is **HelpMenu must not depend on a borrowed,
externally-managed PanelSettings** — fix it at the source (PREFERRED).

---

## 4. One-line root cause for the board
HelpMenu borrows the scene's only PanelSettings asset (`OnboardingPanelSettings`) in
`Awake`; `OnboardingPanelGuard` then disables every UIDocument bound to that asset in
non-onboarding scenes (MainCastle_Hall) — including HelpMenu's own — leaving the overlay
attached to a dead panel (`panel=<null>`), so the gear "opens" an invisible Settings menu
and dev tools die with it. Fix: HelpMenu builds its **own** privately-named runtime
PanelSettings (guard ignores it), per the DevBootstrap/Arena pattern.
