# DOTween — known dictionary (2026-07-30)

> **Status:** CANONICAL REFERENCE. Every row is a code- or doc-verified fact with its source, so any
> single claim can be re-checked at a glance rather than re-derived. Refresh on any DOTween version
> change (SUNDAY_HOUSEKEEPING.md §2).
> **Sources:** owner's SME brief (2026-07-30) + a research pass over https://dotween.demigiant.com/
> (support / documentation / license / pro / getstarted) + **verification against this tree**.

---

## 0. State in THIS project

| Fact | Value | Verified |
|---|---|---|
| Installed | **YES** — `Assets/Plugins/Demigiant/DOTween/` | on disk |
| Version | **v1.3.030** (DLL/XML dated 2026-06-23) | ⚠ `DOTween.dll.meta` records Asset-Store `packageVersion: 1.2.825` — conflicting. Read the Utility Panel header to settle. |
| Tracked in git | **YES, since 2026-07-30** (`96df1dd7`) — was untracked before | `git ls-files` |
| Settings asset | `Assets/Resources/DOTweenSettings.asset` — **must stay tracked** (DOTween FAQ: ASMDEFs are auto-removed if it is not in source control) | tracked |
| Utility Panel setup | **RUN** (owner, 2026-07-30 12:33) | settings asset exists |
| `createASMDEF` | **0 — keep it 0.** Generating ASMDEFs pulls the module `.cs` out of `Assembly-CSharp` and **forfeits the `link.xml` coverage they currently inherit** | settings asset |
| Safe Mode | **ON — keep it on.** Treat as a net, not a policy; `SetLink` + explicit kills are the real fix | settings asset |
| `defaultRecyclable` | **0 — keep it off.** Recycling trades GC for stale-reference bugs | settings asset |

## 1. ⛔ OpenUPM does NOT serve DOTween

**`com.demigiant.dotween` is not on OpenUPM.** The registry endpoint
`https://package.openupm.com/com.demigiant.dotween` returns **HTTP 404** (verified 2026-07-30). UPM
support has been an open request since 2018. **There is no `manifest.json` diff.** DOTween ships as a
classic `Assets/Plugins` drop.

> A CLI asserted the opposite earlier the same day and was wrong. The OpenUPM scoped registry in
> `Packages/manifest.json` stays scoped to `com.cysharp.unitask` only. Any community UPM mirror is an
> unofficial fork — do not adopt one.

## 2. Licence — no blockers

- **Free covers the ENTIRE WO-786 spec**: all eases (incl. `Ease.OutBack`), `Sequence`, `DOShake*`,
  `DOPunchScale`, every callback, `SetUpdate`. None of it is Pro.
- **Pro adds only**: visual editors (`DOTweenAnimation`/`DOTweenPath`), `DOSpiral`, 2D Toolkit,
  TMP per-character.
- **Commercial use permitted. No fee, no revenue share, no in-game attribution.**
- **The one prohibition: never distribute a MODIFIED version.** → **Never hand-edit anything under
  `Assets/Plugins/Demigiant/`.** (This is also why the 2026-07-30 `DOTWEEN_EPO` break was fixed at the
  define, not by deleting the module file.)

## 3. ⚠ THE `DOTWEEN_EPO` TRAP — it broke this project once

**Symptom:** the whole project stops compiling with `CS0246` on `EPOOutline`, `Outlinable`, `Outliner`,
`SerializedPass` from `Modules/DOTweenModuleEPOOutline.cs`. Every gate, build and deploy blocks.

**Cause:** the Utility Panel setup wrote **`DOTWEEN_EPO` into `scriptingDefineSymbols` for EVERY
platform** (18 occurrences) although *Easy Performant Outline* is not in the project. The module is
guarded `#if DOTWEEN_EPO || EPO_DOTWEEN`, so the define **opened the guard** and it compiled against
types that do not exist.

**Fix (at the cause):** remove `DOTWEEN_EPO` from `ProjectSettings/ProjectSettings.asset`. Keep
`DOTWEEN` and `DOTWEEN_UITOOLKIT`. Do **not** delete the module file.

**Module guard map** (read from our own copy — note which are negative):

| Module | Guard | Meaning |
|---|---|---|
| Audio / Physics / Physics2D / Sprite / UI | `#if !DOTWEEN_NOxxx` | **ON by default**; add the `NO` define to disable |
| EPOOutline | `#if DOTWEEN_EPO \|\| EPO_DOTWEEN` | OFF unless a define opens it — **the trap** |
| UIToolkit | `#if DOTWEEN_UITOOLKIT` | OFF unless defined |
| UnityVersion / Utils | *(no guard)* | always compiled |

## 4. ⚠ THE LIFETIME FOOTGUN — highest-likelihood bug

> **`SetLink` has NO EFFECT on a tween added to a Sequence.** — official docs, confirmed verbatim in
> our shipped `DOTween.XML`.

This is the one that will bite: putting `.SetLink(star)` on each member tween is the intuitive thing to
write, it **compiles**, and it fails only when a player closes the panel mid-animation.
**Link the SEQUENCE.**

```csharp
private Sequence _reveal;

_reveal = DOTween.Sequence()
    .SetLink(gameObject)     // BELT: KillOnDestroy -- on the SEQUENCE, not its members
    .SetUpdate(true)         // see s5
    .SetId("StarReveal");

private void OnDestroy()     // BRACES: explicit, before base teardown
{
    _reveal?.Kill(complete: false);
    _reveal = null;
    transform.DOKill();
}

public void Skip() => _reveal?.Complete(withCallbacks: true);  // snap, never freeze mid-scale
```

- **Teardown goes in `OnKill`, never `OnComplete`.** `OnKill` is documented to fire on every exit path;
  whether `OnComplete` fires on an early kill is ambiguous in the docs and has an open bug.
- `LinkBehaviour` (11 values, from our `DOTween.XML`): `KillOnDestroy` (default, always also applied),
  `KillOnDisable`, `PauseOnDisable`, `PauseOnDisablePlayOnEnable`, `PauseOnDisableRestartOnEnable`,
  `PlayOnEnable`, `RestartOnEnable`, `CompleteOnDisable`, `CompleteAndKillOnDisable`, `RewindOnDisable`,
  `RewindAndKillOnDisable`.

## 5. `SetUpdate(true)` is REQUIRED here

`SetUpdate(bool isIndependentUpdate)` ignores `Time.timeScale`. Our death/hitstop path modifies
timeScale, **and** `EndStateView` already drives its animations on `unscaledDeltaTime` — a scaled tween
would be the odd one out and would freeze outright at `timeScale = 0`. Put it on the Sequence; it
propagates. (Separate knob: the global `DOTween.timeScale`.)

## 6. Init + capacity

```csharp
DOTween.Init(recycleAllByDefault: false, useSafeMode: true, LogBehaviour.ErrorsOnly);
DOTween.SetTweensCapacity(200, 50);   // defaults; raise only with evidence
```
Auto-init fires on the first tween, and **`Init` after that is a silent no-op** — so it must run at
boot. Capacity auto-grows when exceeded, but that resize is a **mid-animation allocation** — the 60fps
risk. `DOTween.Clear()` resets capacity, so re-call `SetTweensCapacity` after any `Clear`.

## 7. Choreography — prefer `Insert`

`Append` places at current total duration, `Join` at the last element's start — both order-dependent and
fragile. **`Insert(atPosition, tween)` is an absolute offset from t=0**, which makes a "0.30s between
stars" spec exactly expressible and independently tweakable. Full worked star-reveal sequence lives in
`WorkOrders/WORK_ORDER_786_raid_star_reveal.md`.

## 8. uGUI coverage

| Target | Methods | Module |
|---|---|---|
| `Transform` / `RectTransform` scale | `DOScale`, `DOPunchScale`, `DOShakeScale` | **core** (no module) |
| `RectTransform` position/size | `DOAnchorPos`, `DOSizeDelta`, `DOShakeAnchorPos` | UI |
| `Image`/`Graphic`, `CanvasGroup`, legacy `Text` | `DOColor`, `DOFade`, `DOFillAmount` | UI |
| `TextMeshProUGUI` | `DOText`, `DOColor`, `DOFade` | **TMP — currently OFF, module file absent** |

**UI module is active** (`DOTWEEN_NOUI` is not defined), so everything WO-786 needs compiles today.
**TMP is off and 87 files use TMPro** — design around it: animate the `FLAWLESS` stamp by scaling its
`Transform` (core) and fading a `CanvasGroup` (UI). This also dodges an in-repo hazard: `EndStateView`
records that *"the TMP star glyphs tofu'd on the build font"*, which is why its star row is procedural
`Image` diamonds, not text.
⚠ **UNVERIFIED:** `pro.php` lists TMP shortcuts as Pro while our `DOTweenModuleUtils.cs` documents
`DOTWEEN_TMP` as a standard module define. Settle via the Utility Panel toggle; do not assume.

## 9. IL2CPP / stripping / WebGL

Our config: Android **IL2CPP, ARM64, managed stripping = Medium** (`Assets/Editor/MobileSettings.cs:232`);
WebGL stripping = **Minimal** (`Assets/Editor/DesktopBuild.cs:56`).

- **DOTween publishes NO link.xml guidance** — absent from all doc pages.
- It **self-defends**: `DOTweenModuleUtils.Preserver()` carries `[UnityEngine.Scripting.Preserve]`, the
  fix for the one historical stripping crash, and it **is present in our copy**.
- The modules ship as loose `.cs` (no `DOTween.Modules.dll`), so they land in `Assembly-CSharp`, which
  our `Assets/link.xml` already preserves wholesale.
- **Ship without a DOTween link.xml entry; verify on device.** Keep ready but commented — it is
  project-wide and would inflate WebGL:
  ```xml
  <!-- <assembly fullname="DOTween" preserve="all" /> -->
  ```
- **AOT generics:** no documented DOTween AOT problem. Open IL2CPP issues are transpile-time on older
  versions. Note `link.xml` does not fix AOT generics anyway — different mechanism.
- **WebGL cost: ~2.7 MB compressed**, attributed to `DOTweenModuleUI` + `DOTweenModuleUtils`; Unity's
  tracker notes DOTween prevents managed stripping from working fully (the irony: `Preserver()`, the
  thing that makes stripping safe, is what defeats the stripper). **Mitigate by disabling unused
  modules — we need only UI; audio/physics/physics2D/sprite are all enabled and unused.**
- The Unity-6 WebGL compile defect #681 (`RectOffset`) **does not affect us** — grep for `RectOffset`
  in our 1.3.030 copy returns nothing.
- **Unity 6000.4 is undocumented territory** — DOTween's stated high-water mark is 6000.3. UNVERIFIED,
  one minor ahead. Smoke-test both an Android and a WebGL build.

## 10. Gotchas the docs call out

- **`From()` snaps immediately** — at the line where it executes, not when the Sequence plays. Building
  a Sequence of `From()` tweens makes every target snap at construction. **Set `localScale` explicitly
  instead.**
- `SetRelative()` = `startValue + endValue`; no effect on `From` tweens.
- `SetAutoKill` has no effect once started, and none inside a Sequence.
- `Ease.OutBack` **overshoots past target** — a `0 -> 1.3` OutBack leg peaks ~1.45. Not clipped by a
  `LayoutGroup` but **is** cropped by a parent `Mask`/`RectMask2D`, and can trigger rebuilds under a
  `ContentSizeFitter`. Keep the star row free of Fitters (it currently uses manual anchors — do not
  regress that).
- **Tweens survive scene loads** — the `[DOTween]` object is `DontDestroyOnLoad`, so every cross-scene
  tween is a destroyed-target case. `DOTween.KillAll()` on scene unload is common practice. ⚠ sourced
  from the component source + issue threads, **not** official docs (which are silent). Live for us: the
  raid flow is scene-transition heavy.
- **Do not use DOTween's `AsyncWaitForCompletion` on WebGL.** We already have UniTask — use
  `OnKill`/coroutines/UniTask.
- If UI text vanishes after adding DOTween on an older Unity, the FAQ blames the shipped `.mdb` files.
  Not expected on Unity 6, but it is the first thing to try.

## 11. Ship checklist

- [x] Commit `Assets/Plugins/Demigiant` + `DOTweenSettings.asset` by explicit path
- [x] Remove the stale `DOTWEEN_EPO` define
- [x] `COMPILE_GATE_OK` + `REGRESSION_OK`
- [ ] Utility Panel → disable **audio, physics, physics2D, sprite**; keep **UI**. Re-commit the settings asset.
- [ ] **Android IL2CPP build + on-device smoke test** — stripping failures NEVER surface in the editor
- [ ] **WebGL build; MEASURE the size delta** against the pre-DOTween baseline (343 MB uncompressed on
      2026-07-30) rather than assuming the ~2.7 MB
- [ ] `RunCaptureHeadless` and **open the PNGs** before any DOTween-driven UI reaches the owner
- [ ] Instrument the reveal with `FlowTrace` + `Guard.Try` from the first line (§12), not after it breaks

**Integration point:** `Assets/_Modules/Village/UI/EndState/EndStateView.cs` — `BuildStarRow` already
builds procedural gold diamonds and receives the star count; `RaidVictoryController` already computes
`RaidResult.Stars` (two-axis ladder as of 2026-07-30). Only the reveal animation is the stub.
