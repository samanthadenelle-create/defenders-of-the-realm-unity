# WC3 production / CoC invasion — experience analysis + work-order program

**Status:** LIVING program doc (update when WOs land or rulings change)  
**Minted:** 2026-07-30 (CLI, from owner deep-analysis request)  
**Audience:** Owner (rulings) · Claude (pull WOs, design/read-only where marked) · CLI (implement + gate)  
**Canon spine:** one hero you control; city work autonomous; raids = train → army → teleport → deploy → watch  
**Anchors:** `docs/RAID_NORTHSTAR.md` · `docs/PAIN_POINTS_2026-07-26.md` · `docs/ARCHITECTURE_PRINCIPLES.md` · `KEY_FACTS.md`

---

## 0. How Claude / CLI pulls this program

1. Read **this file** (map + priority + WO table).  
2. Open the **specific WO** for the lane you are assigned — do not invent scope outside that file.  
3. For queue visual work, also read `docs/UI/WO-798_wc3_queue/CODE_AS_IS.md` (live chip).  
4. **Claude:** only write design/copy/mockups when the WO says `READ-ONLY / UI SEAT`. Never edit `.cs`.  
5. **CLI:** implement only after owner sign-off where the WO requires image-pair / felt ruling.  
6. When a WO ships, mark it in §2 table and bump status in the WO file RESULT if any.

### Paste boot (Claude)

```text
Read docs/WC3_COC_EXPERIENCE_ANALYSIS.md (this program).
Then open the assigned WorkOrders/WORK_ORDER_NNN_*.md only.
Obey Roles in that WO. Design WOs = no .cs. Implement WOs = for CLI, not you.
```

---

## 1. System map (truth)

```
HUB / CITY                         RAID (CoC PvE attack)
─────────────────────────          ────────────────────────────
BuildMode place/move               RaidSelection → Army pre-raid
Multi-channel queue (773)          SceneRouter.GoRaid → RaidBase_*
  Builders | Training | Research   Tap deploy tray → TroopDeployer
Structure level timers             TroopController auto-fight
Perk grid (WC3 enhancements)       RaidScoring 180s / stars / loot
VillageTier (Heart tech gate)     Reconcile wounded + veterancy
ArmyStorage + Barracks train       (async PvP of YOUR base = V2)
Waves / home defense
```

| Pillar | Code truth | Feel gap |
|--------|------------|----------|
| Production queue | Engine multi-channel + right-column chip + 5-deep **text** | WC3 glance needs icons/rings |
| Upgrades | Level timers + perk grid + VillageTier | Too many doors / words |
| Train / army | Timed train + housing + recovery | Loadout handoff soft |
| You invade | Full Teleport/Deploy spine | Ring, naming, loadout, stakes |
| You defend | Waves + towers | Not CoC “someone hit my base” (V2) |

---

## 2. Work-order index (program board)

### Already on disk (do not re-mint — pull these)

| WO | File | Lens | Role | Status |
|----|------|------|------|--------|
| **774** | `WorkOrders/WORK_ORDER_774_raid_loadout_deployring_naming.md` | CoC invasion P0 | CLI implement | READY |
| **798** | `WorkOrders/WORK_ORDER_798_wc3_style_queue_visual.md` | WC3 queue glance | **Claude design** → CLI later | READY FOR UI SEAT |
| **799** | `WorkOrders/WORK_ORDER_799_queue_cancel_refund_engine.md` | Queue cancel/refund | CLI engine; cancel **UI** after 798 | READY |

### Minted under this program (2026-07-30)

| WO | File | Lens | Depends on | Role | Status |
|----|------|------|------------|------|--------|
| **800** | `WorkOrders/WORK_ORDER_800_building_focus_card_unify.md` | WC3+CoC upgrade clarity | — | Claude design → CLI | READY |
| **801** | `WorkOrders/WORK_ORDER_801_queue_glance_icons_multichannel.md` | WC3 production feel | **798** sign-off | CLI implement | READY (blocked on 798) |
| **802** | `WorkOrders/WORK_ORDER_802_raid_coc_stakes_casualties_loot.md` | CoC stakes F1 | 774 preferred first | CLI | READY |
| **803** | `WorkOrders/WORK_ORDER_803_raid_session_comfort.md` | CoC session feel | **774** | CLI | READY (after 774) |
| **804** | `WorkOrders/WORK_ORDER_804_raid_structure_destruction_stars.md` | CoC stars language | 802 + copy stable | CLI | READY (later) |
| **805** | `WorkOrders/WORK_ORDER_805_upgrade_construction_feedback_parity.md` | WC3/CoC trust | 800 optional | CLI | READY |

**Next free after this mint:** **806** (see `CLI_LANES_WO_NUMBERS.md`).

---

## 3. Recommended dispatch order

```
798 design (Claude) ──► owner image-pair ──► 801 implement glance
                                              │
799 engine (can parallel) ──► cancel row UI after 801 chips exist
                                              │
800 design (Claude) ──► owner ──► 800 CLI unify building card
                                              │
774 raid P0 (CLI) ──► 803 session comfort ──► 802 stakes
                                              │
805 construction feedback (anytime after timers known)
804 structure-% stars (only if owner wants CoC building stars)
```

**Parallel safe:** 798 design ∥ 774 implement ∥ 799 engine ∥ 800 design (file-disjoint).  
**Not parallel:** 801 vs 798 (same chip); 803 vs 774 (same raid deploy path).

---

## 4. WC3 tweaks (summary)

| Priority | Tweak | WO |
|----------|--------|-----|
| P0 | Icon + ring + pending strip on live QueueStatus chip | 798 → 801 |
| P0 | One building card: Level \| Enhancements \| active job | 800 |
| P1 | Cancel training/build with refund | 799 (+ UI after 801) |
| P1 | Train/Research in glance when busy (M2) | 801 |
| P1 | Under-construction world + HUD + complete pop parity | 805 |
| P2 | Selection-driven “this building’s job” highlight | fold into 800/801 |

**Do not copy from WC3:** peon micro, raid unit micro, full detached tech tree.

---

## 5. CoC invasion tweaks (summary)

| Priority | Tweak | WO |
|----------|--------|-----|
| P0 | Loadout handoff + deploy ring + Army/Deploy naming + Defenders% copy | **774** |
| P0/P1 | Casualties + readable loot by star | **802** |
| P1 | 2× speed, ghost drop, Auto Recommend, scout stub | **803** |
| P1.5 | Structure destruction % stars | **804** |
| V2 | Async defense of player base, shields, sim PvP | park |

**Do not build for V1:** walk-to outpost as raid loop, hero fortress micro, deterministic RaidSim authority.

---

## 6. Owner rulings still open (block or shape WOs)

| # | Question | Affects |
|---|----------|---------|
| R1 | Building card: Level \| Enhancements \| Queue — approve? | 800 |
| R2 | Glance multi-channel M1 vs M2 vs M3? | 798/801 |
| R3 | Stars stay garrison-based for V1 or invest structure-% now? | 804 go/no-go |
| R4 | Casualty % formula sketch? | 802 numbers |
| R5 | Raid clock 180s keep / shorter mobile? | 803 feel |
| R6 | Extra builder slot vs train bay as primary IAP? | monetization later |

---

## 7. Success bar (program)

- **Production:** glance reads as WC3 production line without opening a wall of text.  
- **Upgrade:** one building door; player never asks “upgrade vs unlock vs tier?”  
- **Invasion:** train → choose army → deploy outside walls → watch → stars/loot hurt and reward like CoC.  
- **No new fantasy pillars** until those three feel good on a phone.

---

## 8. Related (not in this program)

- WO-779 UI spacing sweep (global layout law)  
- WO-795 no stacked screens  
- WO-782 capsule standee / art travel  
- Echo lanes WO-784, VFX WO-785  

Park them unless they block a row above.
