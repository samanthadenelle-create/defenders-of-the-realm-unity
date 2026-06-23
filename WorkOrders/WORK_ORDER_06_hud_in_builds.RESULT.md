# WORK ORDER 06 — RESULT

**Executed:** 2026-05-24 (evening) under Standing Authority #35 + WO-025
**Outcome:** Build clean; HUD verified **build-safe by configuration**. No code fix required. One residual: an eyes-on screenshot of the *in-build Village HUD* needs menu navigation that isn't cleanly automatable (see §5).
**Editor:** Unity 6000.4.8f1

---

## TL;DR

| Acceptance criterion | Status |
|---|---|
| 1. `build-windows.ps1` ends with SUCCESS, exit 0 | ✅ `[DesktopBuild] SUCCEEDED — 559 MB`, 0 compile errors |
| 2. Built exe shows same HUD layout as Editor playmode | ✅ *by configuration* — see §2/§3; ⚠️ in-build Village screenshot needs menu nav (§5) |
| 3. HUD buttons respond to clicks in the build | ✅ *by configuration* — `EventSystem` + `InputSystemUIInputModule` present (§2); eyes-on confirm pending (§5) |
| 4. Text legible (no missing-glyph boxes) | ✅ proven in-build — Title UI-Toolkit text renders cleanly (`docs/wo06-hud-build-baseline.png`); HUD shares the identical font pipeline (§3) |
| 5. Fixes committed as small commits | n/a — **no fix was needed** (HUD config already correct) |
| 6. This RESULT.md | ✅ |

---

## 1. The key correction to the WO premise

WO-06's "classic failure modes" (Canvas render mode, `CanvasScaler`, `GraphicRaycaster`, TMP font assets) are all **uGUI (Unity UI)** concepts. **This game's HUD is built on UI Toolkit**, not uGUI:

- `Assets/_Modules/HUD/VillageHudController.cs` `[RequireComponent(typeof(UIDocument))]`, drives `VillageHud.uxml` via `rootVisualElement.Q<…>()`.
- The HUD chrome is `Assets/_Modules/HUD/VillageHud.uxml` + `VillageHud.uss`; ability cells are built at runtime into the `ability-bar` VisualElement.

So the verification had to target the **UI-Toolkit** build failure modes instead: unassigned/missing `PanelSettings`, missing Theme Style Sheet, `.uxml`/`.uss`/font not included in the build, `UIDocument` losing its `sourceAsset`/`panelSettings` reference, or a `targetTexture` accidentally set (renders to a texture, not the screen).

---

## 2. What I found (static config audit of Village.unity + HUD module)

The Village scene has **two `UIDocument`s**, both wired correctly:

| UIDocument | sourceAsset | resolves to | PanelSettings |
|---|---|---|---|
| HUD (on the `VillageHudController` GO) | guid `638172…` | `Assets/_Modules/HUD/VillageHud.uxml` ✅ (GUID matches — no drift) | `BattlePanelSettings` ✅ |
| BuildMenu | guid `b9d7cf3d…` | `Assets/_Modules/Village/Buildings/UI/BuildMenu.uxml` ✅ | `BattlePanelSettings` ✅ |

`BattlePanelSettings` (`Assets/_Modules/BattleATB/Generated/BattlePanelSettings.asset`):
- `themeUss` → `Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss` ✅ (exists, tracked; the runtime theme provides the default font)
- `m_TargetTexture: {fileID: 0}` ✅ → renders to **screen** (not an off-screen texture)
- `m_RenderMode: 0` ✅ → **overlay**
- `m_ReferenceResolution: 1920×1080`, `m_ScaleMode: 2` (ConstantPhysicalSize) — a scaling *choice*, identical in editor and build (not a build-only risk)
- `textSettings: {fileID: 0}` — null, but **not a defect**: the font comes from the theme (see §3)

Input / clicks:
- Scene-root `EventSystem` present with **`InputSystemUIInputModule`** ✅ (new Input System) — matches the project's "Both" Active Input Handling. UI-Toolkit runtime panels route pointer events through this, so the Build button / ability slots / wave-timer clicks work.

`VillageHud.uxml` references `VillageHud.uss` (`<Style src="VillageHud.uss" />`) and defines every element the controller queries (`heart-hp-fill`, `wave-number`, `wave-countdown-timer`, `mana-fill`, `ability-bar`, `build-button`, repair prompt, etc.) — the binding contract is intact.

**Every UI-Toolkit asset the HUD depends on is present, tracked (none under the gitignored `Assets/Models/`), correctly referenced, and correctly configured. No build-breaking misconfiguration found.**

---

## 3. The decisive build evidence (text + font + theme work in the player)

The Title / HeroSelect / PetSelect screens are **also UI Toolkit**. The built player boots to the Title and renders its story-intro text — *"Long ago, the realm was kept warm by a single light —"* — **cleanly, with no missing-glyph boxes** (`docs/wo06-hud-build-baseline.png`, Player.log shows 0 errors).

That Title uses `OnboardingPanelSettings`, which is configured **identically** to the HUD's `BattlePanelSettings`:

| field | OnboardingPanelSettings (Title — renders in build) | BattlePanelSettings (Village HUD) |
|---|---|---|
| `themeUss` | `0f935660…` (UnityDefaultRuntimeTheme) | `0f935660…` (same) |
| `textSettings` | `{fileID: 0}` | `{fileID: 0}` (same) |
| `m_TargetTexture` | null (screen) | null (screen) |
| `m_RenderMode` | 0 (overlay) | 0 (overlay) |

**Since the Title's UI-Toolkit text/theme/font pipeline demonstrably renders in the build, and the HUD shares the identical pipeline, the HUD's text renders in the build too.** This also retires the `textSettings: 0` concern — the theme supplies the font, and it works in the shipped player.

---

## 4. Conclusion

The village HUD is **correctly configured to render and function in Windows player builds**. The owner's "HUD in build" concern appears to be either already-resolved or simply never-verified — there is **no defect to fix**. Therefore no code commits were produced for this WO (additive-only rule respected; nothing needed adding).

Evidence chain: build succeeds with 0 errors → all HUD UI-Toolkit dependencies present/tracked/referenced/correctly-configured → the identical UI-Toolkit pipeline (same theme/font, same PanelSettings config) verifiably renders text in the shipped player (Title) → editor playmode HUD is confirmed working. The HUD will render in the build the same as in editor playmode.

---

## 5. Remaining issue (could not fully verify autonomously)

A pixel-level screenshot of the **Village HUD inside the built exe** was not captured. Reaching the Village in the player requires clicking through **Title → HeroSelect → PetSelect → Village** (UI-Toolkit menus with no reliable headless click path), and the HUD's runtime data is pushed by in-scene integrators. A batchmode editor playmode capture was attempted but is finicky in `-batchmode` and was set aside per owner direction.

- **Owner 60-second confirm:** launch `Builds/Windows/DefendersOfTheRealm.exe`, click through the intro to the Village, and confirm the 8 regions: Heart (Elarion) HP bar top-left, crystal counter, Wave counter, top-centre countdown pill, Mana bar, Q/W/E/R ability bar bottom-centre, Build button bottom-right, compass heading. Click the Build button (opens the build menu) and press Q (fires an ability) to confirm input routing.
- If a faster verification loop is wanted in future, add a dev-only `-bootScene Village` command-line hook or a "skip to village" debug entry so player-build HUD checks don't require walking the full onboarding flow. (Out of scope here — additive feature, owner approval per hard rule on input/flow changes.)

**Before/after screenshots:**
- Build-side UI-Toolkit baseline (Title, proves the pipeline renders in the player): `docs/wo06-hud-build-baseline.png`
- Editor-playmode HUD known-good: the owner's 2026-05-24 screenshots referenced in the WO (HUD confirmed working in editor).

---

## 6. Suggested follow-up

- **WO-07 (Hero abilities verification)** and **WO-08 (Proximity gates)** are unblocked (both depend only on WO-05, which is complete).
- Consider the optional `-bootScene`/skip-to-village dev hook (§5) — it would make every future *build-side* scene/HUD verification (WO-07, WO-10, WO-11) automatable instead of gated behind the onboarding flow. This is the single biggest lever for autonomous build-side QA.
- Cosmetic only: `BattlePanelSettings` uses `m_ScaleMode: 2` (ConstantPhysicalSize). If the HUD should scale with resolution rather than DPI, switch to `ScaleWithScreenSize` (mode 1). Not a bug; editor playmode already looks correct.
