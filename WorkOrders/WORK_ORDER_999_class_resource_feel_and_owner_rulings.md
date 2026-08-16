# WORK ORDER 999 — Class resource economy: make it *feel* like magic (and lock owner rulings)

**Status:** READY TO IMPLEMENT — needs **owner balance rulings** in §2 before cost re-tunes; UI/plumbing items can ship first  
**Silo:** Hero combat + HUD presentation · **For:** CLAUDE CLI · **Date:** 2026-08-15  
**PO:** Samantha (owner) · **Author:** CLI SME pass  
**Parent:** WO-997 (DONE — plumbing + data v4 + bar floats + oracle). This ticket is the **residual**, not a second mana system.

---

## 0. SME north star (one sentence)

**Magic (and Vigor/Focus) is already built.** What is still soft is *legibility and a few design locks* — so the pool can bite *and the player can see and judge it*, and so open rulings from 997 do not rot as silent TODOs.

---

## 1. What is already true at HEAD (do not re-greenfield)

Verified 2026-08-15 against `abilities.json` v4 + `HeroAbilities` + HUD:

| Fact | Proof |
|------|--------|
| Spell resolution is real | `HeroAbilities` resolvers: dash, knockback, taunt, blink, DoT, HoT, invuln, gracebuff, shield, manaweave, drainshot + shared strike |
| One cost reader | `ManaCostOf(def)` — charge, gate, interrupt refund all route through it |
| Per-class economies | `classes.{mage,knight,ranger}.resource` — Mana 20@1.0/s · Vigor 10@1.5/s · Focus 12@0.6/s +1 on-hit |
| Kit mid/ult costs bite | Mage W/E/R = 5/7/10 · Knight = 3/4/6 · Ranger = 4/5/8 (Q free basic) |
| Bar is not integer-stuck | `ManaExact`/`MaxManaExact` through model; lerped fill + spend flash in `HudKitController` |
| Oracle pins the economy | `[class-resource]` / `ClassResourceRegression` 4/4 cases |
| Dual-copy safe | Resources ≡ StreamingAssets byte-identical |

**So the pre-v4 brief (“everything costs 0 against a 10 pool”) is STALE.** Do not re-author the whole system. Finish the product surface and the open rulings.

---

## 2. Owner balance rulings (ONLY you can decide — fill in before data retune)

Reply with numbers or **KEEP** (accept §2 of WO-997 as shipped).

| # | Question | Shipped default | Owner pick |
|---|----------|-----------------|------------|
| R1 | Pool sizes (Mage / Knight / Ranger) | 20 / 10 / 12 | |
| R2 | Passive regen /s | 1.0 / 1.5 / 0.6 | |
| R3 | Kit cost curves W/E/R (Q free) | M 5/7/10 · K 3/4/6 · R 4/5/8 | |
| R4 | Should **Q** ever cost resource? | No (free basic forever) | YES costs ___ / NO |
| R5 | Does **ranger.q Quick Shot** restore Focus? | No (melee path only) | YES +N per cast hit / NO |
| R6 | Keep names Mana / Vigor / Focus? | Yes | Rename? |
| R7 | Knight structural source (Barracks)? | Deferred | Ship tier keys this sprint? / Later |
| R8 | Universal skills stay free? | Yes (0 cost) | Keep / cost them |

Until R1–R3 change, **do not** thrash costs again. UI items in §3 do not need R1–R3.

---

## 3. Implementation (ordered — player-felt first)

### 3a. Ability faces show cost + unaffordable (player-felt) — no new systems
**Files (likely):**
- Ability bar / hotbar face builders that already show cooldowns (HUD kit ability slots — find via `AbilitySlot` / cast bar faces, not a second panel)
- Read cost via existing catalog + `HeroAbilities` afford path (must stay single-reader: never invent a parallel cost)

**Acceptance:**
- [ ] Each non-zero-cost skill face shows a cost pip (shape + number — colourblind law, never hue alone)
- [ ] When `Mana < ManaCostOf(def)`, face is darkened / disabled affordance and pip still readable
- [ ] Free Q / free universal faces show no false cost
- [ ] Interrupt refund still matches the face (same `ManaCostOf`)

### 3b. Resource bar label = class identity
**Files:**
- Nameplate / vitals presentation (`ElarionUiKitNameplate` or HUD vitals text if any)
- Optionally push `ResourceDisplayName` through `HeroVitalsModel` if the plate needs it

**Acceptance:**
- [ ] Mage plate reads **Mana** (not only a blue bar with no name)
- [ ] Knight reads **Vigor**, Ranger **Focus** when those classes are live
- [ ] No second bar built; same fill path as WO-997 §3b

### 3c. Ranger Focus from Quick Shot (only if R5 = YES)
**Files:**
- Cast resolution path for basic ranged (`HeroAbilities` / cast confirm for `ranger.q`) — NOT a second pool
- Reuse `RestoreMana(OnHitRestore)` 

**Acceptance:**
- [ ] Each *landed* Quick Shot restores the authored `onHitRestore` (default 1)
- [ ] Misses do not restore; multi-target does not multi-refund (same anti-farm as melee: once per cast)
- [ ] Melee path behaviour unchanged

### 3d. Data retune (only after R1–R3)
- Edit `abilities.json` both copies (byte-identical)
- Keep `ClassResourceRegression` green; extend cases if new rules appear
- No second `ManaCostOf` reader

### 3e. Structural knight vigor (only if R7 = ship now)
- New GameModifiers keys + coverage regression rows **in the same commit**
- Barracks tiers fold like Cathedral does for mage — identity mult/bonus, not a second pool

---

## 4. What NOT to touch

- Do **not** rewrite `HeroAbilities` effect resolvers (already live)
- Do **not** add a second mana field / second cost reader
- Do **not** touch ATB module
- Do **not** change Mana Draught / Manaweave drip semantics
- Do **not** re-open WO-997’s dual-copy or oracle shape unless a case fails
- Do **not** invent Barracks keys without R7 = yes

---

## 5. Acceptance (overall)

**Engineering**
- [ ] `COMPILE_GATE_OK`
- [ ] `[class-resource]` still green (or extended intentionally)
- [ ] Brace/NUL clean on every touched `.cs`

**Player-felt (owner)**
- [ ] As mage: casting W/E/R *empties the bar visibly*; you wait for regen or drink/weave — not “cooldown only forever”
- [ ] As knight: Vigor gates burst skills in a long fight
- [ ] As ranger: Focus management is a weave (basics ↔ skills) once R5 is decided
- [ ] Cost pips make the economy readable without opening a wiki

**Done when:** §3a+§3b shipped + owner R1–R8 recorded in this file (even if KEEP) + felt-close on one class of her choice.

---

## 6. Files (expected)

| Area | Paths |
|------|--------|
| Data (if retune) | `Assets/Resources/Data/Canonical/abilities.json` + StreamingAssets twin |
| Caster | `Assets/_Modules/Village/Hero/HeroAbilities.cs` (only if R5/R7) |
| Attack/cast hit | `PlayerAttackController` / cast path (R5) |
| HUD | ability faces + vitals/nameplate (`HudKitController`, producers, nameplate kit) |
| Oracle | `ClassResourceRegression.cs` (extend if needed) |

---

## 7. Sequencing note (from the brief — still correct)

Ship **§3a+§3b first** (legibility). Only then retune costs (R1–R3).  
If costs land while faces still hide cost, you cannot judge your own balance.

---

## 8. RESULT

`WorkOrders/WORK_ORDER_999_class_resource_feel_and_owner_rulings.RESULT.md` when done + owner felt-close.
