# PROGRAM — WO-732 → WO-736 · Barracks Troop Roster + Tier Unlocks

**Status:** MINTED 2026-07-16 (owner-directed: default types + upgrade unlocks)  
**Numbering:** next free after layout WO-737 = **738** (`CLI_LANES_WO_NUMBERS.md`)  
**Parent program:** CoC attack spine `WORK_ORDER_PROGRAM_723_731_coc_arena_barracks.md` (especially **WO-724** Barracks live)  
**Audience:** Claude (spec/UI seat) + CLI implementer — specs are READY TO IMPLEMENT  
**Claude paste packet:** `WorkOrders/CLAUDE_HANDOFF_2026-07-16_barracks_coc_roster.md`  

---

## One-line summary

Expand the army from **2 always-on troops** to a **7-type roster**: day-one **Footman + Archer**; higher types unlock as the player **upgrades the Barracks building** (T2–T6). Data-driven, CoC-shaped, no new combat stack.

---

## Why this program exists

- CoC WOs 723–731 **wire** train/deploy; they do **not** define a progression roster.
- Today `troops.json` has only `troop-footman` + `troop-archer`; `TroopTrainingPanel` lists **all** catalog rows with **no unlock gate**.
- Barracks `building-tiers.json` has **6 tiers of stat mults** + army-cap perk — **no unit unlocks**.
- Owner ruling: **create the collection of default types; upgrade unlocks other types.**

---

## Locked product table (authoritative for all WOs below)

| Unlock | Barracks tier | Id | Display name | Role | Slots | Combat job |
|--------|---------------|-----|--------------|------|-------|------------|
| **DEFAULT** | **1** (placed / Muster) | `troop-footman` | Footman | melee | 1 | Front line |
| **DEFAULT** | **1** | `troop-archer` | Archer | ranged | 1 | Back-line DPS |
| Unlock | **2** Drill Yard | `troop-spearman` | Spearman | melee | 1 | Reach melee, anti-clump |
| Unlock | **3** War College | `troop-shieldguard` | Shieldguard | melee | 2 | Tank / choke hold |
| Unlock | **4** Standing Army | `troop-outrider` | Outrider | melee | 2 | Fast flank / tower hunt |
| Unlock | **5** Warhost | `troop-battlemage` | Battlemage | ranged | 2 | High damage, fragile |
| Unlock | **6** Legion of Elarion | `troop-echo-legionnaire` | Echo Legionnaire | melee | 3 | Elite expensive flex |

### Economy (initial balance — PO may retune; do not invent other costs)

| Id | wood | iron | food | buildSeconds | maxHp | dmg | cd | range | move | hunt |
|----|------|------|------|--------------|-------|-----|-----|-------|------|------|
| footman | 40 | 10 | 5 | 30 | 100 | 12 | 1.0 | 2.5 | 4.0 | 14 |
| archer | 30 | 20 | 5 | 45 | 60 | 29 | 1.2 | 14 | 4.0 | 18 |
| spearman | 50 | 25 | 10 | 50 | 90 | 16 | 1.1 | 3.5 | 4.0 | 15 |
| shieldguard | 60 | 40 | 15 | 70 | 180 | 10 | 1.3 | 2.2 | 3.2 | 12 |
| outrider | 80 | 50 | 20 | 90 | 95 | 18 | 0.9 | 2.5 | 5.5 | 16 |
| battlemage | 40 | 80 | 25 | 100 | 55 | 42 | 1.8 | 16 | 3.5 | 20 |
| echo-legionnaire | 100 | 100 | 40 | 150 | 160 | 28 | 1.0 | 2.8 | 4.2 | 16 |

### Model keys (day-one placeholders — real art may replace later)

| Id | `model` (Resources/Heroes/) | Interim if missing |
|----|----------------------------|--------------------|
| footman | `SC_Footman` | already exists |
| archer | `SC_Archer` | already exists |
| spearman | `SC_Footman` | same body; future spear art |
| shieldguard | `Knight` or `SC_Footman` | prefer larger/tanker read |
| outrider | `Ranger` | fast silhouette |
| battlemage | `Mage` | caster silhouette |
| echo-legionnaire | `Knight` | elite read |

`modelYaw` per pack: SC_* → `0`; Tripo/Knight/Ranger/Mage → typically `-90` (match existing TroopDef comments).

---

## Dependency graph

```
732 data roster + schema  ──► 733 unlock gate + train UI
                           ──► 734 barracks tier copy + modifiers text
                           ──► 735 visuals/portraits (∥ 733 after 732)
                                      │
                                      ▼
                                   736 regression + dual-copy + RESULT close
```

**Relation to WO-724:** 732–736 may land **before or with** 724. Unlock logic must not require full barracks discovery if tests force-set tier; production train still respects `ff.barracks` / `BarracksUnlock` if present.

---

## Work order index

| WO | Title | File |
|----|-------|------|
| **732** | Troop roster data + `unlockBarracksTier` schema | `WORK_ORDER_732_troop_roster_data_schema.md` |
| **733** | Training unlock UX + train refuse gate | `WORK_ORDER_733_troop_unlock_train_ui_gate.md` |
| **734** | Barracks tiers announce unit unlocks | `WORK_ORDER_734_barracks_tier_unlock_copy.md` |
| **735** | Placeholder models / portraits / tray icons | `WORK_ORDER_735_troop_visual_placeholders.md` |
| **736** | Dual-copy, DataRegression, fleet smoke, canon | `WORK_ORDER_736_troop_roster_verify_canon.md` |
| **737** | **Obsidian layout contract** for Train panel (zones, lock/select, CTAs) | `WORK_ORDER_737_barracks_train_obsidian_layout.md` |

**UI note:** Implement **737** with **733** (layout + unlock projection). Do not ship unlock math with a non-conformant list.

---

## Claude seat instructions (BINDING for implementers)

1. **Be SME first:** read `troops.json`, `TroopDef.cs`, `TroopCatalog.cs`, `TroopTrainingPanel.cs`, `TroopDialogueCommands.cs`, `TroopFactory.cs`, `ArmyStorage.cs`, `building-tiers.json` barracks block, `ModifierService.TierOf("barracks")`.
2. **Data-driven only** — no hard-coded troop id switches in combat code for unlocks; unlock = catalog field + tier compare.
3. **Dual-copy rule:** edit StreamingAssets **and** Resources mirror for any JSON under `Data/Canonical/`.
4. **No UXML** — training UI stays code-built Obsidian kit.
5. **No hand-edit `.unity` scenes.**
6. **Instrument:** FlowTrace system `"Barracks"` or `"TroopTrain"` on refuse/unlock reasons.
7. **Brace + NUL gate** on every `.cs` touch; CompileGate before done.
8. **Do not flip `ff.barracks` default ON** here (that is WO-731 / 724 close).
9. **Do not invent a second army system** — Path A `ArmyStorage` only.
10. After each WO: write `WorkOrders/WORK_ORDER_NNN_*.RESULT.md`.

---

## Out of program

- Live PvP, new combat AI, projectile archer VFX polish (unless free).
- Full custom Tripo troop art (placeholders OK; art pass later).
- WO-726 deploy loop (consumes roster; does not define it).
