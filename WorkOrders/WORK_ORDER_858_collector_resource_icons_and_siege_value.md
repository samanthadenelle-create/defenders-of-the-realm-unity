> ## RECONCILED 2026-08-08 - true status is SUPERSEDED
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: WO-901 section 5 supersedes this as a plan, and the resource-type icon half was never built (CollectorStackView.cs:478 billboards only the info canvas).
> The previous Status line read "Status: READY TO IMPLEMENT" and was wrong; the board overstated this.

# WORK ORDER 858 — Collector collect icons + high-value invasion targets

**Status:** SUPERSEDED  
**Minted:** 2026-08-04 (CLI / Grok — owner: visual icon for what to collect; extra collectors are high-value invasion targets)  
**Silo:** Village collectors / presentation / siege targeting  
**Roles:** CLI implement; Claude may author icon placement mock only  
**Depends on / with:** **WO-857** (owner-locked three-full model + bank pallets — implement together or 858 after 857 tap hooks)  
**Prior art (reuse, do not rewrite):**  
- `ResourceCollector` + `Collect()` / `ISiegeLootTarget` / `SiegeRoleValue`  
- `CollectorStackView` (diegetic pile + FULL “!”)  
- `concept-icons.json` wood/iron/food/crystal → Icons_Obsidian  
- `EnemyBrain` already prefers `ISiegeLootTarget` with role value  

**Owner-locked (2026-08-04):** Icons = **collector full / tap to collect only**.  
HUD `current/max` = **pallet bank** (857). Offline echo silo = **separate** Collect All UI.  
Never use the collect icon to mean “need a Lumberyard.” Second farm = more production; pallet = more bank.

---

## 0. Owner need

1. **Visual icon** so players know **what resource** is ready to collect and that the building is tappable (CoC floating gold/elixir icon grammar).  
2. **Additional collectors** exist / will exist as **high-value targets** during enemy invasions (steal pending, prioritize when full).

---

## 1. What already exists

| System | Today |
|--------|--------|
| Pending + Collect | `ResourceCollector.Collect()` banks pending |
| Fill visual | `CollectorStackView` — logs/ingots/sacks **or** abstract bar + “!” when full |
| Siege | `ISiegeLootTarget`; `SiegeRoleValue = 0.85 * (1 + fill*0.75)`; destroy steals 50% pending |
| Enemy AI | Overlap scan prefers loot targets over generic structures |
| Catalog collectors | `collector_farm` / `collector_lumbermill` / `collector_forge` only |
| HUD icons | `concept-icons.json` currency_wood/iron/food/crystal |

**Gap:** No clear **billboard resource icon** (CoC-style floating sprite) that says “tap me — this is wood.” Stack props help if catalog loads; many clones fall back to bar + “!” only. Extra collector types / premium siege weight not data-driven.

---

## 2. Product — collect icon (required)

### 2.1 Look (CoC grammar, Elarion art)

When `PendingAmount ≥ threshold` (default **> 0** or fill ≥ **0.05**):

```
        [ resource icon ]     ← billboard, faces camera
              │
         (bob if full)
              │
        [ collector building ]
```

| State | Icon |
|-------|------|
| empty / broken | hidden |
| filling | resource icon, scale 0.85, slight bob optional |
| full (`IsFull`) | larger scale 1.0 + existing “!” / glint may stay |
| after collect | hide until pending builds again |

**Icon source (generic):** map `HarvestResource` → concept-icons key:

| Resource | concept-icons key |
|----------|-------------------|
| Wood | `wood` |
| Iron | `iron` |
| Food | `food` |
| Crystals | `crystal` |

Resolve sprite the same way CurrencyChip does (kit concept resolver). Fallback: letter W/I/F/C in a gold disc — never blank.

### 2.2 Placement

- World-space canvas **or** SpriteRenderer billboard above collector bounds (height = renderer max Y + 0.6–1.2m).  
- Presentation-only component (e.g. `CollectorCollectIconView`) — **reads** ResourceCollector, never mutates except routing tap to Collect.  
- Attach alongside `CollectorStackView` (same Attach path / factory).

### 2.3 Tap

- Icon (+ optional larger invisible hit sphere) is a **tap target** for `Collect()` (WO-857 §4.5 A).  
- Min touch ~48–64 dp projected; mobile-first.  
- On success: hide or shrink icon; floating `+N` text optional (reuse damage/loot floater if exists).  
- Colorblind: icon shape/sprite + optional small amount text under icon — not hue-only.

### 2.4 Do not

- Replace CollectorStackView piles (icons **add** to stack, don’t remove).  
- Put bank max UI on the icon (bank max is HUD chip / storage buildings).  
- Require opening upgrade panel to collect.

---

## 3. Product — high-value invasion collectors

### 3.1 Rule (CoC)

Full / rich collectors are **juicier raid targets**:

- Higher AI priority when pending high  
- Steal more pending on destroy (optional by tier)  
- Optional: enemies path to fullest collector first  

### 3.2 Data-driven siege tier (generic — any collector row)

Extend catalog `repo` (dual-copy) with optional fields (defaults preserve today):

```json
"repo": {
  "behaviorId": "ResourceCollector",
  "collectorBuildingId": "farm",
  "capacity": 1000,
  "siegeValue": 1.0,
  "raidLootFraction": 0.5,
  "highValueTarget": false
}
```

| Field | Meaning | Default if absent |
|-------|---------|-------------------|
| `siegeValue` | Multiplier into `SiegeRoleValue` | 1.0 |
| `raidLootFraction` | Fraction of pending stolen on break | 0.5 (current const) |
| `highValueTarget` | If true, floor role value higher + optional VFX ring for invaders | false |

**Suggested formula (keep simple):**

```
SiegeRoleValue = baseRole * siegeValue * (1 + FillFraction * fillBoost)
// baseRole default 0.85, fillBoost default 0.75 (today)
// if highValueTarget: baseRole = max(baseRole, 1.1) or siegeValue default 1.35
```

Wire by reading catalog on collector init — **no** hard-coded list of premium ids in EnemyBrain.

### 3.3 “Additional collectors” the owner has

Today only three catalog collectors. Owner says they have **more** high-value ones:

| Action | How |
|--------|-----|
| If structures already in catalog without ResourceCollector | Add/fix `behaviorId: ResourceCollector` + `collectorBuildingId` + capacity + siege fields |
| If only art/prefabs exist | Add catalog rows + StructureFactory already handles ResourceCollector |
| Premium examples (owner retags) | e.g. crystal collector, elite farm, “war silo collector” — **data only** |

**Inventory task for implementer (Phase 0):** list every structure id that should be a collector or siege loot target; ensure each implements `ISiegeLootTarget` via ResourceCollector **or** thin adapter for non-collector storages later (storages = WO-857 bank; loot-from-storage is WO-672 — **out of scope** unless already ISiegeLootTarget).

### 3.4 Enemy / wave feel

- Prefer **no EnemyBrain rewrite** beyond ensuring it already reads `SiegeRoleValue` (it does).  
- Tuning `siegeValue` / `highValueTarget` is enough for “extra collectors are priority.”  
- Optional FlowTrace when AI picks a highValue collector.  
- On break: existing steal + damage report “looted N” — keep.

### 3.5 Player telegraph (fairness)

High-value collectors should **look** worth defending:

- Collect icon always uses correct resource  
- Optional gilt ring / banner when `highValueTarget` and fill ≥ 0.5  
- Do not softlock: player can still Collect before wave  

---

## 4. Code surface (thin)

| Touch | Change |
|-------|--------|
| New `CollectorCollectIconView.cs` | Billboard icon + tap → Collect |
| `ResourceCollector` or factory Attach | Spawn icon view with stack view |
| `RepoProps` | optional siegeValue, raidLootFraction, highValueTarget |
| `ResourceCollector` init | Read siege fields; SiegeRoleValue / RaidLootFraction from data |
| `structures-catalog.json` dual-copy | Mark premium collectors; retune siege on all three basics slightly if needed |
| Concept icon resolve | Shared helper with CurrencyChip if exists |

**Forbidden:** new combat system; rewriting wave manager; jeweler as collector; bank capacity logic (857).

---

## 5. Phases

| Phase | Work |
|-------|------|
| **0** | Inventory all collector / high-value structure ids; screenshot current stack/full state |
| **1** | Billboard resource icon from concept-icons; show/hide on pending |
| **2** | Tap icon → Collect(); wire with 857 clamp |
| **3** | Repo siege fields + wire SiegeRoleValue / loot fraction |
| **4** | Catalog: mark additional/premium collectors highValue; capacity if missing |
| **5** | Headless/oracle: icon path non-null for 4 resources; highValue SiegeRoleValue ≥ normal at full |
| **6** | RESULT + PO felt (tap icon collects; invaders path to full premium first) |

---

## 6. Acceptance

- [ ] Each live ResourceCollector with pending shows a **clear resource icon** (wood/iron/food/crystal)  
- [ ] Tap icon (or icon hit volume) collects that collector; +feedback  
- [ ] Icon hides when empty/broken  
- [ ] Stack view / bar still works (icon is additive)  
- [ ] Catalog-authored `highValueTarget` / `siegeValue` raises AI priority vs basic collector at same fill  
- [ ] Destroy steals pending per `raidLootFraction` (data)  
- [ ] All additional collector rows placeable + accrue + collect + siege  
- [ ] Dual-copy catalog; COMPILE_GATE_OK + REGRESSION_OK  
- [ ] Colorblind-safe (sprite + optional count, not red/green only)  
- [ ] Elarion copy only  

---

## 7. Owner dials

| Dial | Default |
|------|---------|
| Icon show threshold | pending > 0 |
| Basic siegeValue | 1.0 |
| Premium siegeValue | 1.35–1.6 |
| Premium raidLootFraction | 0.5–0.7 |
| Icon height above building | ~1m |

---

## 8. Relationship to WO-857

| 857 | 858 |
|-----|-----|
| Bank max + HUD have/max | World **icon** for pending type |
| Tap building / chip collect | **Icon** as primary diegetic tap affordance |
| Lumberyard/Foundry/Silo storage | Collectors remain pending + raid loot |

Implement **858 icons + siege data** even if 857 caps slip; clamp Collect when 857 lands.

---

## 9. Paste for CLI

```text
Implement WORK_ORDER_858_collector_resource_icons_and_siege_value.md.
Add billboard resource icons (concept-icons wood/iron/food/crystal) on collectors
with pending; tap icon = Collect(). Data-drive siegeValue/raidLootFraction/highValueTarget
on catalog collectors so premium collectors are invasion priorities. Reuse
CollectorStackView + ISiegeLootTarget — no AI rewrite. Dual-copy JSON.
COMPILE_GATE_OK + REGRESSION_OK. Brace-check every .cs.
```

---

## 10. One-line truth

**Show a tappable resource icon on every filling collector; mark premium collectors in data so invasions treat full ones as high-value loot.**
