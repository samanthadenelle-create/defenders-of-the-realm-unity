# WO-811 — Echo tasks: gather a resource node **or** repair structures

**Status:** DONE — the repair half implemented 2026-08-10 (the gather half was already WO-830's);
regressions green, RESULT filed; owner felt-verify pending. See the RESULT file.
**Minted:** 2026-07-30  
**Lane:** Village/Harvest + structure repair consumer (single lane for Echo assignment product)  
**Origin:** owner Echo card screenshot 2026-07-30 20:49 — *Echoes need to either gather a node (wood/iron/or whatever resource) or repair structures*  
**Capture:** `C:\Users\Elden\OneDrive\Pictures\Screenshots\Screenshot 2026-07-30 204955.png` (also `Logs/screenshot-echoes-2026-07-30-204955.png`)  
**Program hub (adjacent):** `docs/WC3_COC_EXPERIENCE_ANALYSIS.md` (city autonomy) · **WO-784** (lane consumers — see § Relationship)  
**Roles:** Claude = READ-ONLY task picker UI if needed; CLI = assignment model + consumers + card  

---

## Why (screenshot + code)

### What the player sees
- Card: **Elowen, Idle — waiting for your word**
- “Assign … to a task”
- One blank grey bar + one real button **Crafting**
- No clear **Wood / Iron / Food gather** and no **Repair**

### What code does today
| Piece | Truth |
|-------|--------|
| Assignment storage | `EchoAssignments` CSV `lane:level` |
| Pickable lanes | **Only** `harvest` + `crafting` (`PickableLanes`) |
| Legacy `wood`/`iron`/`food` | Normalized **into** generic Harvest on read — **not offered as separate picks** |
| Harvest consumer | Silo rate via `EchoService` / bonuses (resource **split** by element weights — not “this Echo works the lumber node”) |
| Crafting consumer | Multiplier often **write-only** (WO-784: Core `EchoLaneBonuses` under-consumed) |
| Defense / Exploration | Not pickable (owner 2026-07-24) |
| **Repair** | **No Echo lane / consumer** |

**Product gap:** owner wants V1 Echo agency to be **binary and concrete**:

1. **Gather** a resource (wood / iron / food — a *node* or resource focus)  
2. **Repair** damaged structures  

Not a vague “Harvest” + orphan “Crafting” row with a dead chip.

---

## Owner product rule (BINDING for this WO)

> Every owned Echo is either **Idle**, **Gathering &lt;resource&gt;**, or **Repairing structures**.  
> Crafting / Defense / Exploration are **out of the V1 picker** unless already assigned in save (read-only display only).

**Gather** means: that Echo’s work contributes **primarily** to the chosen resource (wood, iron, or food) — player-readable, not a silent global mix.  
**Repair** means: while structures are damaged, that Echo advances repair (offline-fair preferred); when nothing needs repair, show honest empty state (“Nothing to repair”) and optionally fall idle.

---

## Relationship to WO-784

| WO-784 | WO-811 |
|--------|--------|
| Wire Core `EchoLaneBonuses` consumers (Harvest seam fix, Defense passive, etc.) | **Player-facing task model**: gather resource **or** repair |
| Defense = city defense passive | Repair = **fix damaged buildings** (different job) |
| Crafting/Exploration consumers | **Do not expand Crafting in the picker** for V1 |

Implement **811 task model first** for honesty of the card. Fold Harvest multiplier seam fixes from 784 if they share files; do **not** block 811 on Defense/Exploration unlock design.

---

## Scope

### 1. Task vocabulary (storage)
- Prefer explicit tokens (pick one approach in implementation; document in RESULT):
  - **A (recommended):** restore pickable **resource gathers** `wood` | `iron` | `food` + new `repair`  
  - **B:** keep `harvest` but require a **resource focus** field (`GameState` / token `harvest:wood:level`)  
- Normalize: old `harvest` without focus → default **wood** (or element-preferred resource).  
- Idle remains unassigned.  
- Crafting not in `PickableLanes` for V1 (if save has `crafting`, show as status but no new assign).

### 2. Card / picker UI (`EchoCardView` / `EchoCardVM`)
- Fix empty grey bar (broken harvest chip).  
- Offer clear CTAs, full labels (no clip):
  - **Gather Wood** · **Gather Iron** · **Gather Food**  
  - **Repair structures**  
  - Optional: **Clear task** (idle)  
- Selected task marked by **text** (“(now)”), not color alone.  
- Preferred resource for spirit affinity: note line (“Preferred”) when relevant.  
- Status line examples: `Gathering wood - Lv 1` · `Repairing - Lv 1` · `Idle - waiting for your word.`  

### 3. Gather consumer
- Echo on wood/iron/food increases **that** resource’s silo/income share (or rate) in a way the player can verify (FlowTrace + HUD resources).  
- Prefer routing through a single bonus seam (align 784 Harvest contract if practical).  
- Offline-fair: same clock as existing Echo silo / harvest catch-up.

### 4. Repair consumer (new)
- While assigned to `repair`, tick repair progress on damaged structures (reuse existing repair/HP APIs — e.g. wall/tower/`Building` damage maps, `WallRepairController` patterns, structure HP — **do not** invent a parallel HP system).  
- Skip **destroyed = lost** structures (WO-753).  
- Priority: most damaged first, or nearest to Heart — pick one, document, FlowTrace.  
- When zero repair targets: no silent fake progress; status can say nothing to repair.  
- Offline: apply elapsed repair on load (mirror queue/recovery offline pattern).

### 5. Proof
- EditMode: assign wood → token persisted; assign repair → token; legacy `harvest` migrates.  
- EditMode or headless: gather increases targeted resource path; repair reduces damage given a damaged fixture.  
- Felt: assign Elowen to wood → wood income story; assign repair with a damaged wall → wall improves over time.

---

## Acceptance

- [ ] Idle Echo can be assigned **Gather Wood / Iron / Food** or **Repair** with full readable buttons  
- [ ] No blank chip row; Crafting not the only real button  
- [ ] Gather assignment changes that resource’s harvest outcome measurably  
- [ ] Repair assignment advances real structure repair when damage exists  
- [ ] Destroyed structures not “repaired” back  
- [ ] Save/load keeps assignment  
- [ ] ASCII / colorblind / MinTouchPx on CTAs  
- [ ] FlowTrace Enter/Step on assign + repair tick  

---

## Do NOT

- Make Echoes fight or path as combat units  
- Re-open Defense/Exploration picker design (stay parked)  
- Expand Crafting minigame  
- UXML  
- Hand-edit scenes  
- Touch raid/barracks WOs  
- Break silo claim UX  

---

## Files (expected)

| Area | Paths |
|------|--------|
| Assign | `EchoAssignments.cs`, `GameState.EchoLanes` |
| Card UI | `EchoCardVM.cs`, `EchoCardView.cs` |
| Gather | `EchoService.cs`, `EchoBonusCalculator.cs`, maybe `EchoLaneBonuses` |
| Repair | new small helper under Village/Harvest or Buildings + tick from EchoService or existing timer host |
| Tests | EditMode for assign tokens + repair math if pure |

---

## Claude paste (if design pass wanted first)

```text
Read WorkOrders/WORK_ORDER_811_echo_gather_or_repair_tasks.md
and the Echo card screenshot (Idle Elowen, blank bar, only Crafting).
Wireframe task picker: Gather Wood / Iron / Food + Repair structures + Idle.
No .cs. Full labels, MinTouchPx, master-detail if needed.
```

## CLI note
If UI is obvious (four chips + idle), implement without waiting on Claude; still obey product rule in § Owner product rule.
