# WO-1594 — Raid countdown clock + stars that start lit and go dark

**Status:** READY TO IMPLEMENT — creative milestone table needs owner OK (Q1)  
**Minted:** 2026-09-07 — program WO-1592  
**Priority:** P0 felt — “onscreen clock counting down starting with 3 stars; lose third then second as milestones pass”  
**Lane:** Raid HUD / scoring presentation  
**Files (expected):** `RaidHudController.cs`, `RaidScoring.cs` (presentation + projected stars), regression `RaidScoringRegression.cs`

---

## 1. Problem

Today the raid has a **180s clock** and **0–3 stars** computed from clear / boss / under-time (`RaidScoring`). The HUD shows timer + star diamonds, but the **felt story** the owner wants is CoC-adjacent:

1. Fight opens with **three stars lit**.  
2. A **countdown** is always on screen.  
3. As **milestones fail**, stars **extinguish** in order (3rd, then 2nd) — pressure you can read without math.

That is a **presentation + milestone contract**, not necessarily a new loot formula. Loot can keep using final settled stars; the live HUD must narrate the loss.

---

## 2. Creative proposal — “Honor clock”

### 2.1 Always-on readout (top raid band — keep `HudLayoutBands.RaidReadoutBand`)

| Element | Spec |
|---|---|
| **Countdown** | `M:SS` large, fills/shrinks bar; pulse under 30s (already partly there) |
| **Three stars** | Start **all lit** at raid engage (when clock truly starts — after WO-1520 engagement, not spawn) |
| **Destruction %** | Keep as secondary (shape + number, not hue-only) |

### 2.2 How stars go out (proposed milestones — owner may retune)

Think **time bands + failure floors**, not opaque formulas:

| Star | Starts | Extinguishes when… | Player read |
|---|---|---|---|
| ★★★ (third) | Lit at engage | Elapsed > **T3** (propose **90s**) **OR** hero dead (already caps at 2 — WO-1526) | “Speed honor lost” |
| ★★ (second) | Lit at engage | Elapsed > **T2** (propose **150s**) **OR** destruction% still below **D2** (propose **50%**) at T2 | “Raid going long / unfinished” |
| ★ (first) | Lit at engage | Only lost if raid ends with near-zero destruction (existing settle) — **never snuff mid-fight for time alone** | “You still get something if you cracked the camp” |

**At settle:** final stars = min(projected HUD stars, existing `ComputeStars` rules) so loot cannot exceed what the live HUD promised, and cannot invent stars the scorer forbids.

### 2.3 Juice (cheap, high feel)

- When a star dies: short **scale pop + dim** + one SFX (`SfxId` existing or soft UI click) + FlowTrace `star-lost reason=…`.  
- Optional toast once: `"3-star window closed"` / `"2-star window closed"` — ASCII, dismissible, not modal.  
- Colorblind: stars are **shape fill** (lit vs hollow), never red/green alone.

---

## 3. Implementation notes (grounded)

- Clock already lives on `RaidScoring` (`DefaultClockSeconds = 180`). Prefer **countdown display** of remaining, not only elapsed.  
- `RaidHudController` already paints diamonds + `n/3` — change **projection** to start full and snuff on milestones.  
- Wire star-loss to **engagement start** if WO-1520 has landed (clock must not eat stars during staging).  
- Do **not** change loot tables in this WO except to clamp to HUD honesty.

---

## 4. Owner rulings

**Q1.** Accept T3=90s / T2=150s / D2=50%, or name new numbers.  
**Q2.** On hero death: snuff ★★★ immediately (aligns with 2★ cap) — **recommend YES**.

---

## 5. Acceptance

1. At engagement, HUD shows **3/3** lit stars + countdown from 3:00 (or authored).  
2. Crossing T3 snuffs the third star with visible feedback; crossing T2 snuffs the second if D2 unmet (or per Q1).  
3. Staging (pre-engagement) does **not** advance the honor clock.  
4. End screen stars ≤ what the HUD showed in the last second of the fight (no surprise demotion unexplained).  
5. Regression: pure function tests for milestone → projected stars; `COMPILE_GATE_OK`.  
6. Open PNGs of HUD at 0s / post-T3 / post-T2.

## 6. Not in scope

KayKit art (1593), AI roles (1595), army caps, garrison HP retune.
