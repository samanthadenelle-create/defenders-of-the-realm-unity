# Yarn Spinner 3 (Unity) — Dialogue Advancing & Customization Notes

Distilled from the official docs (docs.yarnspinner.dev) + the installed package source
(`Library/PackageCache/dev.yarnspinner.unity@f59b843921af`) + the paid ClassicRPG add-on.
Authored 2026-06-05 to "understand the product before implementing" (owner directive).

## Architecture (v3)
- **DialogueRunner** drives content; advance API: `RequestNextLine()`, `RequestHurryUpLine()`,
  `RequestHurryUpOption()` (`DialogueRunner.cs:1115/1145/1162`).
- **DialoguePresenters** (`DialoguePresenterBase` subclasses) draw the line; `RunLineAsync`
  blocks until the player advances. ClassicRPG's `RPGDialoguePresenter` is our presenter.
- **LineAdvancer** is itself a presenter (`LineAdvancer.cs:153`, `sealed`) that only *listens for
  input* and calls the runner's request methods. It draws nothing. The continue indicator is
  drawn by `RPGDialoguePresenter`, not the advancer.

## Advancing on TAP/CLICK + MOBILE (the intended way)
- LineAdvancer `InputMode` enum: `InputActions` / `KeyCodes` / `LegacyInputAxes` / `None`.
- **Intended path:** `InputMode = InputActions`, bind `hurryUpLineAction` to `<Pointer>/press`
  (one binding covers mouse-click + touch + pen), `separateHurryUpAndAdvanceControls = false`,
  and assign BOTH `runner` AND `presenter` on the LineAdvancer (`Start()` early-returns if either
  is null → silently leaves hurry-up-only, no advance). No code needed.
- `separateHurryUpAndAdvanceControls = false` → ONE control both hurries a typing line and
  advances a completed one (`nextLineAction`/`nextLineKeyCode` are nulled out in this mode).
- **GOTCHA:** `InputMode = None` + the PUBLIC `RequestLineHurryUp()` does NOT auto-advance a
  completed line (it only calls `RequestHurryUpLine`; the hurry-then-advance logic is in the
  PRIVATE `RequestLineHurryUpInternal()`). So for true "tap hurries, tap-again advances", use
  InputActions/KeyCodes mode — do NOT roll your own with the public method.
- **WebGL/mobile:** Active Input Handling must include the Input System (ours = "Both" ✓) for
  `<Pointer>/press` to fire. `<Pointer>/press` already unifies mouse + touch — no separate touch
  binding needed.

## The "blue Next" indicator (continue sprite)
- It's `RPGDialoguePresenter.lineCompleteImage` (a `UnityEngine.UI.Image`) showing `continueSprite`
  mid-dialogue / `endDialogueSprite` on the last line (`RPGDialoguePresenter.cs:118-134/541/565`).
  All `[SerializeField]` + `[MustNotBeNull]`.
- **Restyle/resize/reposition WITHOUT forking:** swap the `continueSprite`/`endDialogueSprite`
  for our own art (or a tiny/transparent sprite) and resize/move the `lineCompleteImage`
  RectTransform in the prefab.
- **Do NOT delete** `lineCompleteImage` (it's `[MustNotBeNull]`, toggled via `SetActive` →
  null = NRE). To hide: zero-alpha sprite, disabled `Image` component, or ~0 RectTransform.
- No "hide indicator" bool exists; feature flags that DO exist: `useAudio/useOptions/useIcons/
  useActionButton/useLetterbox/useSkipping/useBackgroundStyles` (`:233-240`).

## Extension points (least invasive first)
1. **LineAdvancer config** — for advance behavior (above). The intended place.
2. **RPGDialoguePresenter serialized fields** — for look (sprites, sizes, feature bools). Zero code.
3. **Subclass `RPGDialoguePresenter`** — `public` (not sealed) but `RunLineAsync`/`RunOptionsAsync`
   are wholesale overrides (fields private) — clean only for `OnDialogueStartedAsync` /
   `OnDialogueCompleteAsync` additions, not surgical indicator tweaks.
4. **Subclass `DialoguePresenterBase`** — full custom presenter (docs: custom-dialogue-views).
   Note `LinePresenter` and `LineAdvancer` are `sealed` (can't subclass those).

## Command / variable API (confirmed, already used by DialogueCommandBridge)
- `AddCommandHandler(string, Delegate)` via `IActionRegistration` — the real API; typed
  `Action<...>` / `Func<...>` extension overloads exist. Handler returning `IEnumerator`/`Coroutine`
  blocks the dialogue until it finishes.
- `VariableStorage.SetValue("$var", value)` — typed string/float/bool. Must match the Yarn-declared
  type (no cross-type coercion — cf. `$tutorialStep` is a String).

## Options vs lines
- Option selection is separate (each `OptionItem.onSubmit` via EventSystem). Hurry-up during
  options goes through `RequestOptionHurryUp()` (reveals option text only; never selects). Built-in
  same-frame guards stop a line-advance tap from leaking into the option list.

## Doc sources
- Line Advancer: https://docs.yarnspinner.dev/components/dialogue-view/dialogue-advance-input
- LineAdvancer API: https://docs.yarnspinner.dev/api/csharp/yarn.unity/yarn.unity.lineadvancer
- Custom Presenters: https://docs.yarnspinner.dev/components/dialogue-view/custom-dialogue-views
