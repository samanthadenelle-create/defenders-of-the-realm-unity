# WORK ORDER 1083 — Class-Select slot badge: honour the letterless ruling (mobile)

- **Status:** DESIGN SPEC — READY (opens with an OWNER DECISION GATE; see §5)
- **Lane:** 4 (UI legibility / conformance)
- **Author:** UI seat, on `claude/ui-spacing-layout-review-bqas0h` (2026-08-25)
- **WO number:** **1083 — minted from the UI-seat reserved block** (`CLI_LANES_WO_NUMBERS.md` wip:525, UI next-free 1083). This is a UI-originated ticket, so it takes a UI-block number, NOT a CLI-main-line number. (It was briefly mis-numbered 1196; that COLLIDES with the CLI's real WO-1196 on `wip`, hence the move to 1083. CLI: bump the UI-seat row 1083→1084.) No prior ticket file exists — adopt this as the WO-1083 ticket.
- **Branch of record for all citations:** `origin/wip/village2-and-f8-tickets` (the tree the CLI implements on). My local branch is ~a month stale; every `file:line` below was read from `wip`, not the working copy.
- **Deliverable type:** design spec + image-generation briefs (§6), handed to CLI for implementation. The UI seat does not write `.cs`.

---

## 1. The ask (owner, 2026-08-25)

> "we're calling them q, w, e, and r, but being it's a mobile game that really isn't applicable, but I understand you're doing that for anyone that would build the EXE."

The class-select screen paints keyboard key-letters (Q/W/E/R) on each hero's primary-skill rows. On a touch device there is no keyboard, so the letter names an input the player does not have.

## 2. This is an EXISTING rule being broken, not a new rule

Canon, owner ruling **2026-07-19** (verbatim, `SESSION_CANON_LOADER.md` — recorded in `CLI_LANES_WO_NUMBERS.md:8`):

> "**750** = Right ActionBar naming + Warden's Grace redesign … Attack + Q/W/E/R named skills (Sword Wielding/Sword Heroic/Shield Charge/Warden's Grace/Radiant Strike), **mobile HUD shows NO key-letters**."

And in code, the WO-750 rationale (verbatim, `Assets/_Modules/HUD/Kit/HudKitController.cs:1386-1391`):

> "WO-750 mobile-input ruling (owner 2026-07-19): this is a touch game — the ability ICON carries identity, so the medallions render with NO Q/W/E/R key-letter badge. The keyboard/gamepad bindings stay live in code (PC/dev fallback); they are just never surfaced on the touch HUD."

The combat action bar honours this. **Class-select was missed.**

## 3. SME findings — the full slot-letter bucket (read-only RCA, from `wip`)

**Headline: exactly ONE player-facing surface still paints a raw slot letter.** Every other Q/W/E/R path already went letterless.

| Site | file:line | Renders | Verdict |
|---|---|---|---|
| **`HeroSelectController.BuildSkillRow`** | `Assets/_Modules/Onboarding/HeroSelectController.cs:627` (letter painted ~:648; called at :560) | `ElarionUiKit.Label(badge.transform, slot, …)` — the raw slot string on a `"SlotBadge"` plate | **PAINTS-LETTER (the target)** |
| Combat action bar (WO-750) | `HudKitController.cs:1413` — `StyleAsRoundMedallion(slot, null)` | icon only | already letterless |
| ATB battle skills submenu | `BattleHudUgui.cs:519-521` | `"{Name} ({Cost} MP)"` + icon resolved by slot | already letterless |
| `ElarionUiKit.StyleAsRoundMedallion` keyBadge chip | `ElarionUiKit.cs:3649-3673` | optional `keyBadge` label | **latent** — code path exists but every caller passes `null`; dormant |

Surfaces checked and clear (no slot letters): hot-swap/assignable skill row (`HudKitController.BuildAssignableSkillRow`, uses `SetIcon`), `BattleHud9Zone`, skill/talent nodes (`UiStyle.Slot` is a plate-state helper, not a letter), quest "slot" (rotation slot, not Q/W/E/R), help menu. No skill-tree, loadout picker, or tooltip paints a letter.

**The paint site (verbatim, `HeroSelectController.cs:627-649`):**
```csharp
private static void BuildSkillRow(Transform parent, string slot, string name, float y0, float y1)
{
    var plateSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleElement, RpgUiCatalog.ElementStat);
    var badge = ElarionUiKit.AddImage(parent, "SlotBadge",
        new Vector2(0.02f, y0), new Vector2(0.115f, y1),
        plateSprite != null ? ElarionUiKit.ChromeTint : ElarionUi.GoldButton,
        rounded: plateSprite == null);
    // …plate sprite wiring…
    var badgeLbl = ElarionUiKit.Label(badge.transform, slot, 0f, 1f,   // <-- PAINTS THE RAW SLOT STRING
        plateSprite != null ? ElarionUi.Parchment : ElarionUi.Ink, ElarionUi.FontMicro,
        TextAlignmentOptions.Center, 0f, 1f, bold: true);
    badgeLbl.raycastTarget = false;
    FitLine(badgeLbl);
    var nameLbl = ElarionUiKit.Label(parent, name, y0, y1, …);         // the ability NAME
    // …
}
```
There is **no platform check, no feature flag, no conditional of any kind** — the letter is painted on every platform.

**The bad "F":** already scrubbed from the data source by WO-1166 (dated 2026-08-25); `HeroCatalog` now emits only Q/W/E/R. The *data* half is handled elsewhere. **This ticket is only about the structural act of painting the letter** — `BuildSkillRow` would faithfully paint any string it is handed.

## 4. The letterless precedent to mirror (the reuse pattern)

The action bar's medallion resolves the **ability's own icon** and shows no letter (`HudKitController.OnAbilities`, ~`:1980-2027`): `h.SetIcon(UiStyle.Icon(s.IconKey))`. Icon = identity. That is the shape candidate (c) below would follow.

The platform-detection mechanism the codebase already uses (for candidate (d)) — the **touchscreen-inclusive** form, which the codebase learned is more reliable than bare `isMobilePlatform` on WebGL (verbatim, `Assets/_Modules/Village/Hero/VirtualJoystick.cs:50-53`):
```csharp
private static bool IsTouchTarget()
{
    return Application.isMobilePlatform
        || UnityEngine.InputSystem.Touchscreen.current != null;   // covers mobile WebGL
}
```
It is `private`; a fix needs a Core-side public accessor (Onboarding → Core only). `FeatureFlags.IsDesktop` (`FeatureFlags.cs:1269-1273`) is the equivalent desktop-side test, also private.

---

## 5. ✅ OWNER RULING (2026-08-25) — RESOLVED: candidate (d)

> **Approved (d): desktop-only letter.** Mobile shows NO key-letter — the ability NAME owns the row (drop the `SlotBadge` letter, and the plate with it, so mobile does not inherit unnecessary visual shorthand). Desktop/EXE keeps Q/W/E/R on the badge (real keyboard). Owner note: "§13 retiring letters for cost representation doesn't need to kill a desktop badge treatment on a different surface." So implement the platform gate (§4, `IsTouchTarget()`-style, via a Core-side accessor). Desktop letter set = Q/W/E/R per canon. The candidate table below is retained for rationale only.

## 5a. Original decision gate (rationale — superseded by §5 ruling) — what does the badge show on mobile?

The CLI listed four candidates; **the owner rules.** For each, "desktop" = a Windows/Mac/Linux EXE build (has a real keyboard).

| # | Candidate | Mobile sees | Desktop / EXE sees | Cost | Colourblind | UI-seat note |
|---|---|---|---|---|---|---|
| **(a)** | Drop the badge — name owns the row | ability name only | ability name only | **lowest** (delete the badge on all platforms) | neutral | Clean, but **loses the EXE key hint the owner explicitly said she values** |
| **(b)** | Ordinal 1 / 2 / 3 / 4 | "1 · Sword Heroic" … | same | low | neutral | An ordinal implies input (press 1) the mobile player also lacks; trades one meaningless glyph for another |
| **(c)** | Ability's own icon | icon + name | icon + name | **high** | **safest — icon library is silhouette-picked** | Rhymes with WO-1195 (name a thing by its icon). **Blocked today:** `HeroSkillInfo` carries no icon; needs class+slot threaded into `BuildSkillRow`; and **mage E/R concept icons are deliberately unauthored** (`concept-icons.json` header) → mage rows would render **blank**. Not shippable until those icons exist. |
| **(d) ★** | Keep the letter, **desktop-only** (platform-gated) | **no letter** — name owns the row | Q/W/E/R + name | low (platform check + a public accessor) | neutral | **Recommended.** The only option that both honours the mobile ruling AND preserves the EXE case the owner named. On mobile it resolves to (a); on desktop the letter is a real, usable key. |

### UI-seat recommendation: **(d)**, with (c) as a later enhancement

Reasoning: The owner explicitly wants the EXE key hint kept ("I understand you're doing that for anyone that would build the EXE"). Only (d) keeps it while satisfying "mobile HUD shows NO key-letters." It is low-cost and colourblind-neutral. On mobile, (d) collapses to a clean name-owns-the-row layout (drop the `SlotBadge` plate text; the plate itself may stay as a decorative bullet or be removed — see the open sub-question below). Candidate (c) is the aspirational end-state (icon = identity, matches WO-1195), but it is **hard-blocked** until mage E/R concept icons are authored — until then it renders blanks and is worse than (d). Recommend shipping (d) now and revisiting (c) when the mage icons land.

**Open sub-questions for the owner (only if (d) or (a) is chosen):**
1. On mobile, does the `SlotBadge` plate **stay as a text-less decorative bullet**, or is it **removed** so the name reflows left? (UI-seat leans: remove it — the name is the identity; a bare plate is visual noise. Show me a capture either way.)
2. Confirm the desktop letter set is exactly **Q / W / E / R** (Q is the locked basic; W/E/R loadout-swappable), matching the action bar and `CLAUDE.md §7`.

---

## 6. Image-generation briefs (for the owner to render)

Render at the class-select stage-right proportions (a tall detail panel, "PRIMARY SKILLS" header over 3–4 skill rows). Dark obsidian panel (near-black `#050506`, thin gold rim), parchment-cream text (`#EAD9B0`). One row = a small square plate at far left + the ability name to its right. Knight kit for the example: **Sword Heroic · Shield Charge · Warden's Grace · Radiant Strike**. **No colour-coding of slots** (owner is red/green colourblind — distinguish by shape/text only).

- **Brief A — current (the problem):** each row has a small square gold-edged plate with a bold letter **Q / W / E / R** centered on it, ability name to the right. Caption: "TODAY — mobile shows keyboard letters."
- **Brief B — candidate (d) on MOBILE:** same rows, **no plate/letter** (or a blank text-less plate — render both variants for sub-question 1); the ability name owns the row, reflowed to the left margin. Caption: "MOBILE — name owns the row, no key-letter."
- **Brief C — candidate (d) on DESKTOP/EXE:** identical to Brief A (Q/W/E/R plate + name). Caption: "DESKTOP EXE — key-letter kept (real keyboard)."
- **Brief D — candidate (c) aspirational:** each plate holds a small **ability icon** (a sword-slash, a shield, a radiant burst, a warding sigil — silhouettes, greyscale-legible), name to the right, no letter. Caption: "FUTURE — icon = identity (blocked on mage E/R icon art)."

## 7. Acceptance criteria (checkable)

- [ ] **AC-1 — mobile letterless.** On a touch/mobile build, `HeroSelectController` primary-skill rows show **no** Q/W/E/R (or any) key-letter. Proving line: add a `[Flow:HeroSelect]` step logging `slot` + the resolved platform branch at row build; a mobile-branch run logs "badge=letterless".
- [ ] **AC-2 — desktop preserved (if (d)).** On an EXE/desktop build the Q/W/E/R letter still renders. Same proving line logs "badge=Q/W/E/R" on the desktop branch.
- [ ] **AC-3 — platform gate is the real one.** The conditional uses the touchscreen-inclusive test (`IsTouchTarget` semantics), not bare `Application.isMobilePlatform`. Cite the accessor added.
- [ ] **AC-4 — badge stays a label, never a control.** Whatever renders (letter/icon/nothing) keeps `raycastTarget = false`; it is **not** grown to `MinTouchPx` (a badge is a label, not a touch target — `LayoutOracle` allow-list stays at ArmyMuster + EquipDrawer, NO waiver added).
- [ ] **AC-5 — dormant path stays null.** `ElarionUiKit.StyleAsRoundMedallion` callers still pass `null` keyBadge (no regression that re-surfaces the letter on the action bar).
- [ ] **AC-6 — no other surface paints a letter.** A repo grep for player-facing slot-letter painting returns only the (now-fixed) `BuildSkillRow`.

## 8. Constraints (binding)

- **ASCII-only** strings (TMP tofu on device). Q/W/E/R and 1/2/3/4 are ASCII.
- **Never meaning by colour alone** — owner is red/green colourblind (`FOUNDATIONAL_RULINGS.md §4`: "This rule deliberately never asks her to choose between two hues"). No slot distinguished by tint.
- Build through **`ElarionUiKit`**. A badge is a **label**, not a control — do not inflate it to `MinTouchPx` (112).
- **`LayoutOracle` TouchBaseline allow-list stays at its two entries (ArmyMuster, EquipDrawer). NO waivers** (owner 2026-08-24). Adding a panel fails the ticket.

## 9. What NOT to touch

- ⛔ **Do NOT edit `Assets/Resources/Data/Canonical/abilities.json`** — it carries the owner-approved kit and is the source of truth for what the abilities ARE. This ticket is about how the SLOT is *presented*, not what the ability is called.
- Do not change the combat action bar (already letterless).
- Do not re-introduce the "F" (already fixed by WO-1166) — if candidate (c) is chosen, resolve icons by the real class+slot, never a hardcoded key string.

## 10. What this is NOT

Not a data fix (the F is handled). Not an action-bar change. Not a new numbering-authority mint — 1196 is provisional pending CLI reconciliation.
