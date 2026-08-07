# WO AUDIT — verified resolution ledger (2026-08-07)

**What this is:** the 4-day work-order audit produced a list of "not done" findings. Every one was checked
**at source** on 2026-08-07 before any of it was worked. **Six turned out to be already resolved.** This file
records those with their proof, so the next sweep does not re-open them and nobody spends a cycle re-deriving
what was already true.

**Why it exists at all:** a stale finding costs more than a missing one. A missing finding is discovered by
playing the game; a stale finding sends someone to "fix" something that is already correct, and in the worst
case — WO-902 — to actively revert a newer owner ruling.

**Rule going forward:** an audit finding is not actionable until someone has opened the file. "Grep returned
zero hits" is a lead, not a verdict.

---

## ✅ VERIFIED STALE — do not re-open

### 1. WO-909 — "`ff.knightonly` strands installs on Knight"
**RESOLVED before the audit ran.** `Assets/_Modules/Core/State/PlayableHeroes.cs:19`:
> *"THE UNLOCK LANDED 2026-08-05 (owner ruling): `ff.knightonly` now defaults OFF"*

Corroborated by `TalentStrategyRegression.cs:194`, which records its `{ ranger, mage }` set being **emptied**
for the same reason. The flag survives as an opt-in to restore the solo-Knight V1 pivot.

### 2. WO-901 — "`CollectorStackView` says 'collect it' when the bank is full"
**MISREAD.** The view is wired (`StructureFactory.cs:776` → `CollectorStackView.Attach`) and pinned by
`CollectorIncomeRegression` case 12 `[tell-wired]`, which exists precisely because this 437-line component
once sat with zero callers. The copy is deliberate and owner-worded (`CollectorStackView.cs:428-438`):
singleton-correct ("collect it, or upgrade it to hold more" — never "place another"), and the word *"Storage"*
is **banned** there because it belongs to the town bank.

The real residue is a **different** gap — the bank have/max HUD chip — tracked separately below.

### 3. WO-890 — "stacks two auras per node"
**IMPOSSIBLE AS DESCRIBED.** `HarvestAura.cs:204-205` guards:
```csharp
var a = host.GetComponent<HarvestAura>();
if (a == null) a = host.AddComponent<HarvestAura>();
```
and the two callers (`CollectorStackView.cs:150`, `MineNode.cs:170`) act on **different hosts**.

### 4. "Three regression suites registered nowhere"
**ALL THREE DELIBERATE.**
- `BlankStartCensusRegression` and `RepairProbeRegression` both carry the header token
  *"NOT wired into DataRegression"*, which `RegressionMarkerRegression` RULE 2 honours as an explicit opt-out
  (`StandaloneOptOutTokens`, lines 120-124).
- `ArenaCombatOracle` exposes `Run()` with **no `out string`**, so RULE 2's `RunSignature` filter never scans
  it. It is a **play-mode** oracle and cannot run inside `DataRegression` at all.

### 5. WO-892 — "the WO contradicts the owner's felt-test"
**SHIPPED, and the framing was backwards.** `StructureDamageVisuals.cs:108` carries the WO-892 critical-beacon
threshold, and `damage-states.json`'s own note documents the rework as landed (*"WO-892 moved these off
Ember_Burn/Raid_Explosion — Ember_Burn pointed at a Hovl path that does not exist so it never reached the
catalog and never rendered"*).

The owner's burning-building F8 is **not** a contradiction of WO-892 — it is WO-892's alarm working correctly
and pointing at an action the player cannot take. That reframing also let hypothesis 4 (destroyed vs damaged)
be ruled out cheaply: `damage-states.json` states the beacon **stops** at hp==0 and switches to a ruin column,
so a burning structure is *damaged*, and "no repair option" is wrong behaviour rather than correct behaviour.
Folded into defect #35.

### 6. Earlier-confirmed stale
WO-870 (Aether) · WO-854 (oracle registered) · WO-837 (lumberyard removed from `FoundingKit`) ·
WO-863 (canon tagline) · WO-911 Q9 (`RealmStorePurchase => Get("realmstorepurchase", defaultOn: true)`).

---

## ⚠ WORSE THAN "NOT DONE" — WO-902 was actively dangerous

WO-902 (archer tower → medieval castle visuals) still read **`READY TO IMPLEMENT`** while being **dead**.
`structures-catalog.json`, row `tower_ground_archer`, field `_bug22`:
> *"SUPERSEDED 2026-08-06 by the owner's ALL-WOOD ladder … `Tower_Wooden_Watchtower` / `_L2` / `_L3` —
> owner-sourced Tripo art"*

WO-902 routes L1–L3 to polyperfect castles and its §2 explicitly **bans** the wooden look. Anyone pulling it
off the board would have swapped the owner's own commissioned art back out **and believed they were following
the WO correctly.** Bannered `⚠ SUPERSEDED` on 2026-08-07, body left intact per CLAUDE.md §15.

---

## ✅ RESOLVED BY DOING IT (2026-08-07)

| Item | Outcome |
|---|---|
| **WO-853 §7** — raid scoring | Owner ruled **50 spire / 30 structures / 20 garrison**. Shipped. `RAID_SCORING_OK`. §1's "nothing can damage a wall" was **already fixed** — the seam had landed; scoring was the last piece. Nothing anywhere pinned the split, so a guard was added with it. |
| **WO-912** — ad revenue | D1/D2/D4/D5/D7/D8 ruled. D2 settled to **LevelPlay** by an external constraint (AppLovin will not onboard without a published app). Q2a/Q3a moot. `AD_SEAM_OK` + `AD_COVENANT_OK` added. |
| **WO-902** | Bannered SUPERSEDED (above). |

---

## 🔴 GENUINELY OPEN — confirmed by zero-hit token search, re-verified 2026-08-07

| Token | Hits (excl. regressions) | Meaning |
|---|---|---|
| `SetAmountAndMax` | 0 | **WO-857** — the bank cap ENGINE ships (`TownBankCapacity`, `StorageCapsCatalog`, `BankOverflowToastPresenter`, `TownBankCapRegression`) but has **zero consumers under `Assets/_Modules/HUD`**. The player never sees have/max, so a bank at cap is a number that silently stopped moving. Task #44. |
| `StorageStackView` | 0 | same family |
| `siegeValue` / `highValueTarget` | 0 | WO-853's per-structure scoring weights. The 50/30/20 split shipped using an `HpFraction` census instead, so these are a **refinement**, not a blocker. |
| `Vfx.On(` / `VfxBones` | 0 | VFX facade surface never built |
| `AddComponent<EliteVFXController>` | **0** | **The sharpest one.** `EliteVFXController` has **13 references** — the class exists and `Enemy.cs` knows about it — but **nothing ever attaches it**. Built, referenced, never instantiated. Needs the WO-874 ruling. Task #42. |

**`Env_Candle` (WO-884)** — one reference in the entire codebase, its own enum declaration (`VFXType.cs:227`),
while being generated, mirrored, pooled (`isLoop:true, poolSize:6`) and shipped in the APK. Both dungeon
pipelines now seat `CandleAnchor` markers (25 + 24 in the outpost alone) but deliberately do **not** play it:
the type `VfxEmitter` that three WOs name **does not exist**, and ~44 loop instances would blow VFXManager's
20-slot global budget. Runtime seam proposed, not faked. Task #45.

---

## Still needing the PO, not the CLI

- **#42 / WO-874** — `EliteVFXController` was to be wired; code overrode it with no reversal recorded.
- **#38** — dungeon difficulty after `enemies.json` became authoritative.
- **#36** — app installs as *"Defenders of the Realm"*, store name is *"Echoes of Elarion"*.
- **#22 / #17** — first-dungeon torch tutorial; pausing the Heart while in a dungeon.
- **WO-912** — send the Unity Regulated Activities pre-approval. **D3 blocks the SDK until it returns in writing.**
