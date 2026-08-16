# WORK ORDER 1030 — Echo task dialogue: the choice list is clipped, and the portrait is a placeholder

**Status:** IMPLEMENTED 2026-08-16 - pending PO felt-verify (commit `323f3c97f`); see RESULT
**Minted:** 2026-08-16 (UI seat) — provenance stack bumped 1030 → 1031 in the same edit
**Lane:** HUD `DialogueView` presentation + Echo portrait art. Disjoint from every gameplay lane.
**Provenance:** owner screenshot 2026-08-16 — the "Frost" Echo task prompt in `Main_Castle_Overworld`,
with the instruction *"find what is causing these screens"*.

---

## 1. What the screen IS (the chain, traced to source)

| step | file |
|---|---|
| Player engages a deployed Echo | `PetTaskController.Engage()` :156 |
| Builds a code-built 2-choice dialogue (no catalog id, no UXML, no Yarn) | `PetTaskController.BuildEngageDef()` :168-212 |
| Speaker name from species | `SpeakerName()` :215-224 — `ice-wolf → "Frost"`, `flame-pup → "Ember"`, `aether-sprite → "Aether"`, else `"Your Echo"` |
| Played | `DialogueService.PlayDef(...)` :160 |
| Rendered | `DeNelle.HUD.DialogueView` — the §8 canon reference implementation |

The content is correct and intentional: one line + `Gather resources` / `Repair structures`, routed by
the `pet_task` verb.

⚠ **Naming note, NOT a defect to fix here:** the system is internally **Pets** (`PetTaskController`,
`pet_engage`, `pet_task`) while canon §7 renames the player-facing surface **"Pets" → "Echoes"**. The
*displayed* strings are already correct ("Frost", "Keeper…"). Do **not** rename the module in this WO —
that is a separate, wide-blast-radius change.

## 2. DEFECT A — the option list is clipped. Cause is arithmetic, not layout drift

`DialogueView` sizes the panel from measured content (`:734-752`):

```csharp
float textPx    = _body.GetPreferredValues(_body.text ?? "", w, 0f).y;
float textWellPx= textPx > 0f ? textPx + BodyWellPadPx : 0f;
float optionsPx = 0f;
if (_vm != null && _vm.ShowingOptions && _optionsCol != null)
    optionsPx = LayoutUtility.GetPreferredHeight(_optionsCol);
float contentPx = textWellPx + (optionsPx > 0f ? optionsPx + 12f : 0f);
float bodyPx    = Mathf.Clamp(contentPx, MinBodyPx, _maxBodyPx);   // ← THE CLAMP IS THE BUG
```

**The options ARE measured** — `optionsPx` is summed into `contentPx`. The failure is the **ceiling**.
`_maxBodyPx` is derived once (`:664-678`) from a HUD-safe vertical band:

```csharp
float halfSafe  = Mathf.Min(0.655f - cyFrac, cyFrac - 0.155f);   // under TargetInfo, above action bar
float maxFrac   = Mathf.Max(_box.anchorMax.y - _box.anchorMin.y, 2f * halfSafe);
float maxPanelH = maxFrac * CanvasLocalHeight();
_maxBodyPx      = Mathf.Max(180f, maxPanelH - (TopPad + HeaderPx + Gap + BottomBandPx));
```

`_maxBodyPx` is therefore **proportional to `CanvasLocalHeight()`**. In **landscape on a phone** that
height is small, the HUD-safe band takes roughly half of it, the chrome bands take a fixed bite, and
what remains cannot hold `text + 2 options`. `Mathf.Clamp` caps `bodyPx`, the content overflows the
viewport, and **the overflow is the bottom of the option list** — exactly what the screenshot shows,
with `Repair structures` sliced by the panel edge.

⚠ **The clamp is NOT wrong to exist** — it is the WO/F8 fix from 2026-07-16 (*"utilize the area not a
tiny area and scrollbar"*, comment at `:665-671`) and it keeps the panel inside the HUD-safe band. **Do
not delete it and do not widen the HUD-safe band** — that band is what stops the panel colliding with
TargetInfo above and the action bar below.

### The actual fix: options are not optional

Body **text** may scroll. **Options must not** — an unreachable choice is a dead end, not a
readability nit. Required behaviour:

- **Reserve the options' full measured height FIRST**, then give the remainder to the text well. The
  text is the scrollable element; the choice list is fixed.
- i.e. clamp the **text** contribution, not the sum:
  `textBudget = Mathf.Max(MinTextPx, _maxBodyPx - optionsPx - 12f)` and build `bodyPx` from that.
- If options alone exceed `_maxBodyPx` (many-option nodes), **the option column itself scrolls** with a
  visible affordance — never a silent clip.

**Every option must be tappable at every aspect ratio, with zero scrolling, for a 2-option node.**
Two options is the common case across the whole game; if that clips, the dialogue system is broken in
its default configuration.

⚠ **Instrumentation already exists and should have caught this** — `:755-759` logs
`resize contentH={0} (text={1} well={2} opts={3}) -> panelH={4} ... (min {6}/max {7})`. **Capture that
line on the failing device and put it in the RESULT**; it names `opts` and `max` side by side, which is
the proof. Do not fix this from the screenshot alone (§12).

## 3. DEFECT B — the portrait is a generic silhouette

The medallion renders a default silhouette rather than Frost. `DialogueView` resolves it per speaker
and refreshes every Repaint (`:357-364`, `ResolveSpeakerPortrait`), so the plumbing is live — the
resolve is returning nothing for Echo speakers and falling back.

**Determine which, and record it:**

1. Is there **no portrait art** for the three Echo species (`ice-wolf` / `flame-pup` / `aether-sprite`)?
2. Or does art exist but the **speaker key doesn't match** — `SpeakerName()` returns a *display name*
   ("Frost"), and the resolver may key on an id, not a display string?

⚠ **(2) is the cheaper and more likely fault, and it is invisible from the screenshot** — a name→key
mismatch looks identical to missing art. Check the resolver's key before commissioning any art.

If art is genuinely missing: `Assets/Resources/RpgUi/emblem` holds **25 class emblems, committed and
unused** (WO-1023 §3). An emblem is a legitimate stand-in for an Echo medallion and costs nothing to
wire. ⚠ Final pick is an **owner/UI tag**, not a CLI substitution
(memory `vfx-map-owner-tags-no-creative-pick`).

## 4. Do NOT

- Do not delete the `_maxBodyPx` clamp or widen the HUD-safe band (§2)
- Do not rename the Pets module to Echoes here (§1)
- Do not restyle the dialogue chrome — `DialogueView` is the **canon reference implementation** of the
  Obsidian formula (`UI_BLINK_TEMPLATE_CANON.md` §8). Every other screen is told to copy it, so a
  regression here propagates by example
- Do not remove the `[Flow:Dialogue] resize …` trace — §12, and it is the oracle for this defect

## 5. Acceptance criteria

- [ ] Both options fully visible and tappable on the owner's device in **landscape**, no scrolling
- [ ] Verified at 2670x1200 (the Seeker's real surface) — ⚠ the UI capture harness was **geometry-blind
      until `7e05e6d3`**; a PNG filename resolution was a *label*, not a layout (anchor 2026-08-09)
- [ ] A 4-option node either fits or scrolls **with a visible affordance** — never silently clipped
- [ ] Panel still respects the HUD-safe band: clear of TargetInfo above and the action bar below
- [ ] The portrait shows Echo-specific art, or a deliberately-chosen stand-in with the choice recorded
- [ ] The `[Flow:Dialogue] resize` line from the failing case is pasted in the RESULT
- [ ] No visual change to non-option dialogue passages (regression guard on the reference impl)

## 6. Verify

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites`
2. `UI_CAPTURE_OK` — **open the PNGs** (memory `headless-screenshot-verify-ui-before-build`)
3. Device screenshot in landscape at the real surface — memory
   `screenshots-are-primary-evidence-for-visual-defects`
4. Owner felt-verifies + closes (§13)
