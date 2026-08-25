> ## RECONCILED 2026-08-08 - true status is DONE
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: 177b24a7 (the commit does not name the WO); TownBankCapacity.cs 708 lines + storage-caps.json + TownBankCapRegression.cs 779 lines.
> The previous Status line read "Status: READY TO IMPLEMENT" and was wrong; the board understated this.

# WORK ORDER 857 — CoC-style resource storage caps + HUD “have / max”

**Status:** DONE  
**Minted:** 2026-08-04 · **Owner-locked model:** 2026-08-04 (owner confirmed “yes” on three-full split)  
**Silo:** Economy / HUD / Catalog data  
**Roles:** CLI implement; Claude may do chip layout wireframe only (no `.cs` if house rule)  
**Depends on:** existing catalog fields `repo.storageCapacity` / `repo.storageResource` / collector `repo.capacity` (WO-707 data already authored)  
**Adjacent:** **WO-858** (collector resource icons + siege) · WO-855 economy · WO-672 raid stores later  

**Owner follow-up (tap to collect):** yes — CoC-style **tap-to-collect** is the right collect UX.
Engine already exists (`ResourceCollector.Collect`, `ResourceCollectorService.CollectAll`,
Echo `DumpSilos`, EchoWorkforceHud Collect All). This WO **must ship world + chip tap paths**
(§4.5) so players do not only learn collect via a buried panel.

---

## ★ BINDING PRODUCT MODEL (owner 2026-08-04 — do not invent a fourth layer)

### Buildings

| Player name | Catalog id | Layer |
|-------------|------------|--------|
| Farm | `collector_farm` | **Collector** — food pending cap **1000** |
| Lumbermill | `collector_lumbermill` | **Collector** — wood pending cap **800** |
| Forge | `collector_forge` | **Collector** — iron pending cap **600** |
| Jeweler | `jeweler` | **Shop only** — **out of this loop** (no food/wood/iron store) |
| Lumberyard / Foundry / Silo | `lumberyard` / `foundry` / `silo` | **Pallets / containers** — bank max for wood / iron / food |

### Flow

```
OFFLINE ECHO SILO ──Dump / Collect All──┐
FARM / LUMBERMILL / FORGE ──Tap collect─┼──► BANK (base + sum of pallets)
BATTLE REWARDS ─────────────────────────┘         │
                                                  ▼
                                           spend / build
```

### Three FULL states — three different player signals (never same toast)

| Full | Meaning | Player action | UI |
|------|---------|---------------|-----|
| **Collector full** | Pending hit `repo.capacity` | **Tap collect** (icon / building) | Resource icon on building (WO-858); “Farm is full — tap to collect.” |
| **Pallet / bank full** | Wallet hit max for that resource | **Spend** or **build another pallet** | HUD `current/max` + fill bar; “Wood storage full — build a Lumberyard or spend wood.” |
| **Offline echo silo full** | Away pool hit hour/resource cap | **Collect All / Dump** into bank | Echo / Collect All UI fill — **not** a farm icon |

### Expansion (both valid — different fulls)

| Want more… | Build |
|------------|--------|
| Production / more pending sites | **Another collector** (second farm, etc.) |
| Bank room when HUD is full | **Another pallet** (Lumberyard / Foundry / Silo) |

Collector full ≠ need a pallet. Bank full ≠ need another farm.

### Caps (starting numbers — retune later, structure fixed)

| Kind | Resource | Cap now |
|------|----------|---------|
| Collector pending | food / wood / iron | 1000 / 800 / 600 |
| Pallet each | wood / iron / food | 500 per building (raise if too small vs sinks) |
| Free bank without pallets | each of wood/iron/food | **1500** base (data) so early collect works |
| Max formula | | `BaseCap + sum(storageCapacity of live pallets of that resource)` |

### Battle rewards
Grant **into bank** (pallet capacity). Clamp + bank-full toast. Never into collector pending.

### Collect when bank is full
Pending may remain on collector; grant only what fits; toast is **bank full**, not “collect again.”

### Offline when bank is full
Dump fills what fits; remainder **stays in echo silo** (do not silently delete). Toast bank full if truncated.

### HUD bank fill (not different icons per %)
One resource icon + **`current / max`** + continuous fill bar; optional 25/50/75/FULL pips. Same icon always.

---

## 0. Player problem (owner)

> Farm / lumbermill / forge have hard pending caps; after that player needs clarity vs **pallets** (bank) and vs **offline** overflow.  
> Need CoC-like **have/max** on bank + **tap to collect** on collectors — three fulls must not look the same.

CoC grammar (keep Elarion art):

| CoC | Elarion |
|-----|---------|
| Collectors / mines | Farm, Lumbermill, Forge collectors |
| Storages | Lumberyard, Foundry, Silo **pallets** |
| Top bar current/max | Resource dock chips |
| Tap full collector | World icon + chip tap (WO-858 + §4.5) |

---

## 1. What already exists (do not reinvent)

| Piece | State |
|-------|--------|
| `RepoProps.storageCapacity` + `storageResource` | On **lumberyard / foundry / silo** (500 each in catalog) — **data only**, not enforced |
| `RepoProps.capacity` | **Collector reserve** (farm 1000 / lumber 800 / forge 600) — pending buffer before FULL |
| `ResourceCollector` | Accrues to **pending**, collect moves to wallet — **not** village max |
| HUD `CurrencyChip` | Shows **amount only** (`SetAmount`) — no max |
| `ResourceLedger` / GameState wood/food/iron/crystals | Effectively **uncapped** on grant |
| Guide copy | Mentions echo silo / collect — not container bank |

**Gap:** wallet max + UI + “you need a Lumberyard” teaching.

---

## 2. Mental model (binding taxonomy — three layers)

```
[1] PRODUCERS          Farm / Lumbermill / Forge / Echoes / Mines
        │  generate units over time
        ▼
[2] COLLECTOR RESERVE  collector_* pending buffer (repo.capacity)
        │  player taps Collect → moves into bank
        ▼
[3] VILLAGE BANK       wallet (ResourceLedger) capped by STORAGE buildings
        │  Lumberyard / Foundry / Silo (+ optional starter free cap)
        ▼
     SPEND             build / train / upgrade / shop
```

| Building | Layer | Role |
|----------|-------|------|
| Farm, Lumbermill, Forge (production) | 1 | Make resource; **no** bank max |
| collector_farm / lumbermill / forge | 2 | Hold **uncollected** haul; show full bubble |
| **lumberyard** | 3 | **Wood** bank max |
| **foundry** | 3 | **Iron** bank max |
| **silo** | 3 | **Food** bank max |
| Jeweler | — | Vendor only; **not** storage |
| mill / lumbermill (gameplay) | 1 or shop | Confirm live behavior; do not double-count as bank unless catalog says `storageCapacity` |

**Raid later (out of scope except note):** CoC loot hits storages — WO-672/707 already pointed `IsStorageContainer` at raid targets. This WO only wires **cap + HUD**.

---

## 3. Product rules

### 3.1 Max capacity formula (generic)

```
Max(resource) = BaseCap(resource) + sum( storageCapacity of LIVE placed/built
                 containers where storageResource == resource )
```

- **Live** = not sold, active GameObject / BaseLayout record + StructureSingleton rules.  
- Upgrading a container later: if `maxLevel` + per-level capacity table appears, use it; **V1** = flat `storageCapacity` per building (already 500).  
- Multiple silos: **sum** (CoC multi-storage) unless owner rules singleton — catalog currently **not** singleton on lumberyard/foundry/silo → allow multi; softcap via economy WO if spam.

### 3.2 BaseCap (starter so player is not softlocked)

Without any storage building, player still needs a small wallet:

| Resource | Suggested BaseCap (tune in data) |
|----------|----------------------------------|
| wood | 1000–2000 |
| iron | 1000–2000 |
| food | 1000–2000 |
| crystals | §3.4 |

Put BaseCap in **`economy-balance.json`** or **`storage-caps.json`** (dual-copy). Do **not** hardcode only in HUD.

### 3.3 Grant clamp (bank full)

Every path that **adds** wood/food/iron to the village bank must:

```
add = min(requested, Max(resource) - Current(resource))
if add < requested: FlowTrace / once toast "Storage full — build or upgrade a {Container}."
```

**Single choke:** prefer **one** method on ResourceLedger (or EconomyService) e.g. `TryGrant(resource, amount, out granted)` used by:

- Collect from collectors  
- Echo claim / offline harvest bank  
- Mine extract to wallet  
- Quest/reward grants  
- Dev grant (optional bypass flag for AutoPilot)

**Spend** unchanged (already refuses unaffordable).

**Collectors:** when **bank** is full for that resource, either:

- **A (CoC-like):** collector can still fill **pending** but Collect grants 0 with toast, **or**  
- **B (tighter):** collector accrual pauses when bank full (pending also blocked).  

**Default = A** (pending can hold; bank full is the teachable moment on Collect). Document in RESULT.

### 3.4 Crystals

Options (owner default if silent = **C**):

| | Rule |
|---|------|
| **A** | Uncapped (monetization faucet) |
| **B** | Cap from crystal mine / vault building (none today) |
| **C** | Soft cap = BaseCapCrystals + f(mine level) or fixed high cap (e.g. 5000) shown on chip |

HUD still shows `current/max` if capped; if uncapped show current only or `current / ∞` avoided — show current only for crystals if A.

### 3.5 Gold / coins

If gold is not a harvestable bank resource, **leave chip as amount-only** (or separate rule). Do not invent gold storages in this WO.

---

## 4. UI — “how much vs max” (CoC grammar)

### 4.1 Resource dock chips (required)

Today: `HudKitController` → `_resChips[i].SetAmount(e.Wood)`.

**Change (minimal API extension):**

```
SetAmount(current)                    // keep working
SetAmountAndMax(current, max)         // NEW — label "1.2K / 5K" or "1200/5000"
optional SetFill01(current/max)       // thin bar under chip (recommended)
```

- Colorblind: text always; bar is bonus.  
- When current ≥ max: slight **dim or full pip** (not red flash spam — house no-flash law).  
- Expand dock: each of Wood / Iron / Food (and Crystal if capped).  

### 4.2 Storage building focus (recommended, small)

Tap Lumberyard / Foundry / Silo → building card line:

`Wood storage: 1200 / 5000 (this building +500)`  

Reuse existing building focus / upgrade panel if present; else one FlowTrace + chip is enough for V1.

### 4.3 Collect toast when full

One toast per resource per N seconds:  
`Wood storage is full. Build a Lumberyard or free space by spending wood.`

### 4.4 Build menu affordance

If a storage type missing and resource often full, optional coach once — **out of scope** unless free (822-style). Prefer toast + chip.

### 4.5 Tap-to-collect methods (CoC grammar — **required**)

Do **not** invent a second collect economy. Wire UX to existing:

| API (keep) | Role |
|------------|------|
| `ResourceCollector.Collect()` | One building: pending → wallet (then clamp via §3.3) |
| `ResourceCollectorService.CollectAll()` | All collectors + `EchoService.DumpSilos()` |
| `EchoWorkforceHud` Collect All | Already a panel button — keep as secondary |
| `AutoHarvestService` | Perk auto-taps CollectAll — leave alone |

#### Player-facing taps (ship at least A + B)

**A — World tap on the collector (primary CoC feel)**  
- When `PendingAmount > 0` (or fill ≥ threshold, e.g. 10%), the placed collector is tappable.  
- Tap / interact / mobile confirm → `thatCollector.Collect()` only.  
- Prefer existing mobile interact / building select path (same as “tap building”); **do not** open upgrade panel when the intent is collect if pending is meaningful — rule:  
  - If pending > 0 and not broken: **first tap = Collect** (SFX + floating +N).  
  - Optional second tap / hold / “Upgrade” button = upgrade panel.  
- Full collectors already have fill stack / IsFull — show a **simple floating pip or glow** when pending > 0 so the tap target is obvious (reuse StepChanged / stack view if present).

**B — Resource dock chip tap (quality of life)**  
- Tap **Wood** chip → collect all **wood** collectors only (filter by resource).  
- Same for Iron / Food.  
- If that resource’s **bank is full** (§3): toast storage full; still drain pending only as far as bank room allows (clamp grant).  
- Optional: long-press chip = Collect All (all resources) — nice-to-have.

**C — Collect All control (keep)**  
- Existing Echo workforce / Collect All remains for “claim everything including echoes.”  
- Optional HUD small “Collect” when `TotalPending() > 0` — do not clutter if A+B are clear.

#### Feedback (minimal)
- Floating `+120 Wood` at collector world position (or toast if no floater kit).  
- Chip count-tweens via existing `SetAmount` / `SetAmountAndMax`.  
- FlowTrace.Step `Harvest` on each tap path.

#### AutoCollect perk
- When `GameModifiers.AutoCollect` is on, auto CollectAll stays; world tap still works for manual play without perk.

#### What not to do
- Auto-bank every frame with no player agency (kills CoC loop) unless perk.  
- Collect from **jeweler** or storage building itself (storages hold bank max, not pending).  
- Open full Echo panel required to collect town wood.

---

## 5. Data work (dual-copy)

### 5.1 Confirm / tune containers in `structures-catalog.json`

| id | storageResource | storageCapacity (V1) | Notes |
|----|-----------------|----------------------|-------|
| lumberyard | wood | 500 → consider **2000–5000** for mobile feel | Raise if 500 is tiny vs train costs |
| foundry | iron | same order | |
| silo | food | same order | |

Optional V1.1: `storageCapacityByLevel: [2000, 4000, 8000]` if maxLevel > 1 — only if upgrade path exists.

### 5.2 New or extend JSON — `storage-caps.json` (recommended)

```json
{
  "version": 1,
  "baseCap": { "wood": 1500, "iron": 1500, "food": 1500, "crystals": 5000 },
  "crystalsMode": "softCap",
  "toastCooldownSeconds": 12,
  "display": { "compactThousands": true }
}
```

Dual-copy Resources + StreamingAssets. Oracle parity.

### 5.3 Collectors

Keep `repo.capacity` as **pending** only — do **not** merge with bank max. UI for collector full already (if any); do not replace with bank bar.

---

## 6. Code surface (thin — no rewrite)

| File / area | Change |
|-------------|--------|
| New `VillageStorage` or `ResourceCapService` (Village or Core pure) | `MaxOf(resource)`, `RoomFor(resource)`, sum containers from catalog + layout |
| `ResourceLedger` (or single grant API) | Clamp grants; raise event `StorageChanged` |
| `HudKitController` + `CurrencyChipHandle` | `SetAmountAndMax` / fill |
| Resource producers / collect / echo claim | Call clamped grant only |
| DataRegression | caps parse; dual-copy; Max ≥ BaseCap; lumberyard has storageResource wood |

**Do not:**

- Put capacity math in HUD alone  
- Cap inside every random spawner with copy-paste  
- Use Jeweler as storage  
- Hand-edit scenes  

**Presentation law:** HUD reads snapshot (current + max) via Core gate / existing economy event — same pattern as today for amounts.

---

## 7. Discovery / teaching (light)

1. First time any resource hits **≥ 90%** max: toast once per save key `storage_teach_wood` etc.  
2. Quest already can require build silo — keep.  
3. Guide section optional one-liner update: “Lumberyard / Foundry / Silo raise how much you can hold.”

---

## 8. Phased implementation

| Phase | Work |
|-------|------|
| **0** | Confirm live grant paths (list in RESULT) |
| **1** | `storage-caps.json` + `VillageStorage.MaxOf` sum from placed containers |
| **2** | Clamp all bank grants at ledger (incl. `Collect()` path) |
| **3** | HUD chip current/max (+ fill) |
| **4** | **Tap-to-collect:** world tap collector (§4.5 A) + chip tap by resource (§4.5 B) |
| **5** | Full-storage toast + optional building card line |
| **6** | Retune catalog storageCapacity numbers vs train/tower costs (with WO-855 if concurrent) |
| **7** | Oracle + RESULT |

---

## 9. Acceptance

- [ ] With **no** lumberyard, wood max = BaseCap; chip shows `cur / base`  
- [ ] Place lumberyard → wood max increases by its `storageCapacity`  
- [ ] Sell lumberyard → max drops (current clamped down or left until spend — **prefer clamp current down only on grant side; never delete resources on sell** unless owner rules otherwise — **default: keep current, block further grants until under max**)  
- [ ] Collect / harvest cannot push wood above max; toast when truncated  
- [ ] Iron/food same with foundry/silo  
- [ ] Jeweler does not change caps  
- [ ] Collector pending still fills independently (rule A)  
- [ ] **Tap collector in world** with pending > 0 banks that collector (no Echo panel required)  
- [ ] **Tap Wood/Iron/Food chip** collects that resource’s pending (or documents if only world tap ships first — prefer both)  
- [ ] Collect All (existing) still works  
- [ ] COMPILE_GATE_OK + REGRESSION_OK  
- [ ] Dual-copy JSON  
- [ ] PO felt: “I know when I’m full and what building fixes it” + “I can tap to collect like CoC”  

---

## 10. Owner rulings (defaults if silent)

| # | Question | Default |
|---|----------|---------|
| R1 | Sell storage with current > new max? | Keep current; block new grants |
| R2 | Crystals | Soft high cap shown on chip |
| R3 | Multi silo/lumberyard? | Sum capacities (allow multi) |
| R4 | Collector when bank full | Rule A (pending OK; Collect clamps) |

---

## 11. Paste for Claude / CLI

```text
Implement WORK_ORDER_857_coc_resource_storage_caps_hud.md.
CoC-style village BANK caps from lumberyard/foundry/silo storageCapacity + baseCap.
Clamp grants at one ledger choke. HUD chips show current/max (and optional fill).
TAP-TO-COLLECT: world-tap collector Collect() + chip-tap by resource; keep CollectAll.
Collectors stay pending buffers (repo.capacity) — not bank max.
Jeweler is NOT storage. Dual-copy JSON. No system rewrite beyond storage service + chip API + tap wiring.
COMPILE_GATE_OK + REGRESSION_OK. Brace-check every .cs.
```

---

## 12. One-line truth

**Producers make · collectors hold uncollected · tap to collect (world + chips) · Lumberyard/Foundry/Silo set bank max · HUD shows have/max like CoC.**

