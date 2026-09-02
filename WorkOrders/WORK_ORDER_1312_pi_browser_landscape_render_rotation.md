# WORK ORDER 1312 — Pi Browser will not rotate; render the game rotated instead of asking the player to

**Status:** READY TO IMPLEMENT
**Silo:** Web / Pi
**Minted:** 2026-09-02 (CLI) from a direct owner instruction.
**Severity:** P1 — the game is unplayable in portrait, and this likely also fails Pi's app validator.

## Owner instruction, verbatim

> **"for Pi i really need to get pi browser to allow horizzontal"**

## Current behaviour, read at source

`Assets/WebGLTemplates/Pi/index.html:268`
```js
if (piBrowser && !isLandscape()) showLandscapeGate(); else startUnityOnce();
```
In Pi Browser, a portrait viewport shows a **blocking modal** that asks the player to rotate. Unity
does not start. `requestLandscape()` (`:231`) tries `requestFullscreen` then
`screen.orientation.lock('landscape')`, and already has a `catch` that writes
*"Pi Browser refused rotation"* — i.e. **we knew the lock gets rejected.** An escape hatch appears
after `PI_GATE_ESCAPE_MS = 9000`.

## The root the owner is pointing at

`screen.orientation.lock()` can only succeed if the **host native app** permits that orientation. Pi
Browser is a WebView whose activity is portrait-locked, so **no web API we call can ever force
landscape.** The current design asks the *player* to solve a problem the *page* must solve.

**The fix is to stop asking. Render the game rotated ourselves** — when the viewport is portrait,
present a landscape-shaped surface rotated 90 degrees, so the game is horizontal on a portrait device.

## ⚠ THE TRAP — do not ship the naive CSS rotation

A bare `transform: rotate(90deg)` on the canvas **breaks all touch input**, and it breaks it
*silently*: the game renders correctly and taps land in the wrong place.

Unity WebGL maps a pointer to canvas space using `canvas.getBoundingClientRect()` and a linear
`(clientX - rect.left) * (canvas.width / rect.width)`. `getBoundingClientRect()` returns the
**axis-aligned bounding box of the transformed element**, so after a 90-degree rotation Unity's mapping
is rotated relative to what the player sees. A tap at the top-left registers somewhere else entirely.

**You must therefore ship the rotation AND a coordinate shim together.** Shipping the rotation alone
is worse than shipping nothing, because the failure is invisible to a smoke test that only looks at
the screen.

## Required approach

1. **Rotate a wrapper, not the canvas alone.** In portrait: size the wrapper `width = innerHeight`,
   `height = innerWidth`, `transform-origin: top left`, `transform: rotate(90deg) translateY(-100%)`,
   `position: fixed; top: 0; left: 0`. Set the Unity canvas to 100%/100% of that wrapper.
2. **Shim the input coordinates.** Intercept `touchstart/move/end/cancel` and `mousedown/move/up` on
   `window` in the CAPTURE phase, `stopImmediatePropagation()`, and re-dispatch synthetic events on the
   canvas with swapped/mapped coordinates. For a 90-degree clockwise visual rotation the mapping is
   `canvasX = clientY`, `canvasY = (innerWidth - clientX)` — **derive it, do not copy this line on
   faith, and prove it.**
3. **Keep it inert when the device is genuinely landscape.** No rotation, no shim, no synthetic events.
   A real landscape viewport must take exactly today's path.
4. **Re-evaluate on `orientationchange` / `resize` / `visibilitychange`,** matching the existing listeners.

## Prove it — CLAUDE.md sec.12 applies to web too

Add a throwaway harness page under `tools/webtest/` that draws a crosshair where the shim thinks the
pointer landed. **Screenshot it in both orientations** and attach to the RESULT. A visual/spatial
defect is judged by a screenshot, not by reasoning (memory `screenshots-are-primary-evidence`).
Do not close on "the code looks right".

## The second, related question this may answer

Pi's validator appears to load the app in a Pi-Browser-like view (owner: *"looks lie the pi web browser
i think"*). If that view is portrait, **today's blocking modal means the validator never boots the
game** — it would sit on a rotate-your-device prompt whose escape hatch is a button no automated
validator will click. Removing the blocking gate may fix validation as a side effect.
**State this as a hypothesis in the RESULT unless you can capture evidence** — it is untested.

## Acceptance criteria

1. In a portrait viewport with a `PiBrowser` user agent, the game **boots directly into a landscape-
   shaped surface**. No modal, no player action.
2. Touch input lands where the player sees it, **proven by the harness screenshots in both orientations**.
3. A genuinely landscape viewport is byte-for-byte unchanged in behaviour.
4. Non-Pi browsers are unchanged.
5. Multi-touch still works. `touch-action: none` on html/body/canvas is **load-bearing** for build-mode
   pinch/drag (RCA 2026-07-16 recorded in-file) — do not regress it, and make sure the shim forwards
   ALL touch points, not just the first.

## What NOT to touch

- Do NOT remove `touch-action: none` / `overscroll-behavior: none` (`index.html:16-24`).
- Do NOT touch the `pi-sdk.js` benign-timeout suppression (WO-678, `:54+`) — unrelated, and it owns the
  global JS error surface.
- Do NOT change `validation-key.txt`. It was just corrected under WO-1313 and is now correct in all
  four copies.
- Do NOT touch Addressables, `ServerData/`, or anything under `Assets/AddressableAssetsData/**` — a
  change there re-hashes every bundle and mandates a fresh `tools\r2-ship.ps1` push (CLAUDE.md sec.16).
- Do NOT delete the escape hatch until criterion 1 is proven; it is the only thing standing between a
  misreported viewport and a permanently unbootable game.
