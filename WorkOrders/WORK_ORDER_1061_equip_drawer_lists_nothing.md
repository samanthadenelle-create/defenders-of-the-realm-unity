**Status:** READY TO TRIAGE — three candidates, ONE log line separates them (§2). ⛔ No edit before it (CLAUDE.md §12).

# WORK ORDER 1061 — The equip drawer lists NOTHING — you cannot change your weapon

**Minted:** 2026-08-22 (UI seat — Claude UI; UI-block banner bumped 1061 -> 1062 in the SAME edit)
**Assigned:** CLI implements. UI writes no `.cs` (CLAUDE.md §2).
**Lane:** Village / hero equip
**Class:** DEFECT — **functional, not cosmetic.** A core verb is unreachable.
**Evidence:** owner screenshots 2026-08-22, `EquipmentPanel` on Thrain the Wise (mage, staff
equipped). Tapping the Weapon row opens *"Change Weapon (Main Hand)"* and the list body is **empty**.

---

## 0. One-line truth

**Thrain has an Oakheart Staff equipped, and the weapon drawer offers him nothing — not even the
staff he is holding.** Whatever else is true, an item the hero is *currently wearing* failing to
appear in its own slot's list is a strong tell about which filter is wrong.

---

## 1. The filter chain — read at source, all of it

`EquipVM.RebuildCompatible` (`EquipVM.cs:414-440`), main-hand branch:

```csharp
if (_store == null) return;                                        // (A) empty list
string job = t != null ? t.TargetClass : null;
foreach (var (w, qty) in _store.OwnedWeapons())                    // (B) source set
{
    if (offhand != w.IsOffHandItem) continue;                      // (C) hand split
    if (!string.IsNullOrEmpty(job) && !_store.WeaponFitsClass(w, job)) continue;   // (D) class gate
    _compatible.Add(...);
}
```

`WeaponFitsClass` -> `JobMatches` (`GearCatalog.cs:562-567`):

```csharp
if (string.IsNullOrEmpty(itemJob)) return true;      // unauthored job fits everyone
if (itemJob.Equals("any", ...))    return true;
return itemJob.Equals(heroJob ?? "", ...);           // else EXACT match
```

### ⛔ One candidate is already ELIMINATED — do not chase it

**An empty `TargetClass` cannot cause this.** `IEquipTarget.cs:160` returns `_class ?? ""`, and gate
(D) is guarded by `!string.IsNullOrEmpty(job)`. So an empty job **skips the class filter entirely**
and would show **more** items, never fewer. **If someone proposes "the hero's class is blank" as the
cause, that is backwards.**

---

## 2. THE MEASUREMENT — one line, three candidates, done

Log once on drawer open:

```
[Flow:Equip] drawer slot=<key> job='<TargetClass>' owned=<OwnedWeapons().Count>
             -> per weapon: id=<id> job='<w.job>' offHand=<w.IsOffHandItem> fits=<WeaponFitsClass>
```

| Reading | Cause | Fix lane |
|---|---|---|
| `owned=0` | **(B)** The store holds no weapons. The equipped staff was granted straight to the loadout without an inventory entry — **equipped is not the same as owned** | the grant path, not the UI |
| `owned>0`, every `fits=false` | **(D)** Weapons authored with a `job` that never equals this hero's class (e.g. `"knight"` rows against a `"mage"` hero) | `weapons` data authoring |
| `owned>0`, every `offHand=true` | **(C)** Everything owned is a shield; the main hand correctly excludes them | data / genuinely correct |

**`owned=0` is the leading candidate** precisely because the equipped staff is absent from its own
list — an item you are wearing should be the one guaranteed entry. **Leading, not concluded.**

⚠ **Relevant prior art, and a warning.** `GearCatalog.cs:572-576` records F8 seq-642 Fix B: the
class/level gate hole *"was masked only because the shop/equip UI pre-filters its lists."* **This
pre-filter is known-fragile and load-bearing.** Whatever the fix, do not make the UI more permissive
to paper over a data or grant problem — that would re-open the hole that fix closed.

---

## 3. ⚠ Why this cannot wait for the redesign

WO-1133's Armory Rail replaces this screen — **but its compare pane consumes the same query.** "Is
this better than what I have?" requires the list of candidates for a slot. If the query returns
empty, **the redesign inherits an empty pane** and the flagship feature of that ticket ships dead.

**Fix the query regardless of which screen renders it.**

---

## 4. The owner's UX point — already answered by the redesign

> *"clicking item instead of using window opens this new window"*

Correct, and it is the right complaint: tapping a slot opens a **drawer over the panel** — a second
window stacked on the first, which in the screenshot covers the hero, the other slots and the live
view all at once.

**WO-1133's design already resolves this** and needs no new ruling: the Armory Rail keeps a permanent
right-hand pane, so selecting a slot fills a surface that is *already on screen* instead of opening
anything. **Do not design a better drawer.** Fix the empty list here; the drawer disappears when the
redesign lands.

---

## 5. What NOT to touch

- **Do not loosen gate (D) to "fix" the emptiness.** See the §2 warning — that gate exists because
  every non-UI caller relies on it.
- **Do not add the equipped item to the list as a special case.** If it is missing, that is the
  symptom telling you where the bug is; hard-coding it in destroys the evidence and leaves the real
  gap.
- **Do not redesign the drawer** (§4).
- **The drawer's layout clipping** in the same screenshot is the WO-1056 / WO-1060 class. **Noted,
  not folded** — it wants the shared fix, and the new clamp oracle should catch it.

---

## 6. Acceptance

1. The captured line from §2 is **in the ticket** before any code edit.
2. Opening the weapon drawer on a hero with a weapon equipped lists **at least that weapon**.
3. Equipping a different weapon from the list works end to end and the change survives a save/reload.
4. Off-hand still lists **only** shields; main hand still **excludes** them (the WO-543 hand split).
5. The class gate is **not weakened** — prove a genuinely wrong-class weapon is still refused by a
   non-UI caller (`GearLoadout.EquipWeaponById`).
6. The same query feeds a non-empty compare list for WO-1133's pane (§3).
7. `COMPILE_GATE_OK`; brace-check every `.cs`; screenshots opened.

## 7. Files

**Read first:** `Assets/_Modules/Village/Hero/EquipVM.cs:414-440` ·
`Assets/_Modules/Village/Hero/GearCatalog.cs:437-441`, `:562-567`, `:572-576` ·
`Assets/_Modules/Village/Hero/IEquipTarget.cs:160` · the owner's 2026-08-22 screenshots.

**Likely edit (pending §2):** the weapon **grant/ownership** path, or the `weapons` data `job` values.
**Probably NOT `EquipVM`** — it is doing what it was told.

**Related:** WO-1133 (§3, §4) · WO-1059 (the blank preview in the same screenshot) ·
WO-1060 (the clamp oracle that should catch the drawer's clipping).
