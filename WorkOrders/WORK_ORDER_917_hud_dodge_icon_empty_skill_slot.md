# WORK ORDER 917 — HUD residual (WO-899 §4): dodge icon + empty skill-slot placeholder

**Status: READY TO IMPLEMENT** (blocked only on owner art pick for dodge glyph if no existing icon key fits)  
**Minted:** 2026-08-07 (CLI / Grok — residual of WO-899; explicitly **not** done in `a35163e1`)  
**Silo:** HUD / UI  
**Roles:** CLI implement; owner creative pick if no in-catalog dodge/roll icon  
**Depends on:** WO-899 §1–3 landed (analog stick, compass strip, attack pill blend)  
**Parent:** `WorkOrders/WORK_ORDER_899_hud_polish_joystick_compass_attack.md` §4

---

## 0. One-line truth

WO-899 shipped stick + compass + attack blend. **§4 was deliberately not smuggled:** dodge still reads as bare text, and empty ability slots still render **blank** (looks broken vs fillable). This WO finishes that section without reopening §1–3.

---

## 1. Grounded today

| Control | State after WO-899 |
|---------|---------------------|
| Analog stick | Landed — `HudMoveInput` continuous vector |
| Compass strip | Landed — full status width; residual layout = WO-914 |
| Attack pill | Landed — obsidian + amber, masked icon |
| **Dodge** | Text / unlabeled icon path — no dedicated dodge/roll glyph |
| **Empty ability slots** | `BuildActionSlot` leaves icon disabled when unmapped (`_empty`) → blank plate |

Commit message (`a35163e1`): *“No dodge/roll concept art exists and the only candidate is flat teal clipart in a different style from the HUD set — that is an owner creative pick.”*

---

## 2. Scope

### Phase A — Dodge icon

1. Search existing icon catalogs for a shape that reads **dodge / roll / dash** without teal clipart mismatch:
   - `UiStyle.Icon(...)` keys, `concept-icons.json`, `RpgUiCatalog`, existing HUD icon set.  
2. If a **style-matched** key exists → wire it via the same path attack uses (`SetIcon` / slot icon).  
3. If **none** fit → **stop and ask owner** (attach 2–3 candidates or state “none”). Do **not** invent a new art style or paste flat teal.  
4. Blend on the plate like the attack pill (obsidian language, not a second teal island).

### Phase B — Empty skill slot placeholder

When a slot has no mapped ability (`_empty` / no ability id):

1. Show a **faint gold “+”** (or “add skill”) on a dimmed frame — never a pure blank.  
2. **On tap:** short toast/hint: *“Add a skill to activate”* (or house string from canon-strings if one exists).  
3. Prefer routing intent toward skill-tree loadout (WO-896) **without** building a second loadout UI — open existing path if one exists; else toast-only is V1 OK.  
4. Filled slots unchanged (ability icon + cooldown).

### Phase C — Out of scope

- Replacing the analog stick again  
- Compass / waveBlock layout (WO-914)  
- Adding a new dodge **mechanic** if none exists — **icon + affordance only**; if dodge is not a real command, do not invent combat behavior  
- UXML

---

## 3. Files (likely)

| File | Action |
|------|--------|
| `Assets/_Modules/HUD/Kit/HudKitController.cs` | Dodge face + empty-slot bind |
| `Assets/_Modules/Core/UI/ElarionUiKit.cs` / Conformance | `BuildActionSlot` empty placeholder |
| `Assets/Resources/Data/Canonical/concept-icons.json` (+ dual-copy) | Only if a new key is owner-approved |
| Ability / action-bar model | Read empty state; no new slot type |

---

## 4. Acceptance

- [ ] Dodge control shows a clear icon (or RESULT records owner “no art — deferred” with no half-wired teal).  
- [ ] Every empty ability slot shows “+” / add-skill affordance, never blank.  
- [ ] Tap empty → hint (and route if available).  
- [ ] Filled slots + attack pill + stick unchanged.  
- [ ] Headless/UI capture of combat HUD; open PNGs.  
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK` + `UI_CAPTURE_OK` path used by the project.

---

## 5. RESULT

`WorkOrders/WORK_ORDER_917_hud_dodge_icon_empty_skill_slot.RESULT.md`
