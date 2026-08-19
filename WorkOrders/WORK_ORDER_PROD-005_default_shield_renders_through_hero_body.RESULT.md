# RESULT — PROD-005 — the default shield

**Verdict:** **LANDED — mechanism PROVEN ON DEVICE BY MEASUREMENT; the ticket's own acceptance criterion (§5.3/§5.4, the dungeon→town port) is UNPROVEN.**
**Commits:** `c072e5736` (2026-08-18 19:50) + `228908bfc` (2026-08-18 21:40).
**Written:** 2026-08-19 by a read-only verification pass. Re-verified at HEAD `399bfb900`; **no Unity was run** for this file — every number below is quoted from an artefact already on disk, with its source.

---

## 1. What was wrong

Two distinct defects, one after the other.

1. **The swap looked done and changed nothing.** `weapons.json` (both dual copies) already pointed
   `knight_shield_starter` at `gear/weapon/ShieldWithItemLogic`, but **nothing published that address** —
   `Gear.asset` held the 426 Blink addresses and not this one. The addressable load failed and
   `EquipmentController` fell back to the legacy `shield_A` mesh with its stranded 2026-07-07 offsets.
   A build from that tree would have silently shipped the old broken shield. (`c072e5736` body.)
2. **Then the shield read as MISSING** (owner, felt-test: *"shield is missing and sword is now wrong"*).
   It was not missing — it was **1.73× too small**. Legacy `shield_A` is `fullOverride`, so
   `_offHandParentCompensate` is FALSE and it kept localScale 1.04 → **0.918 m** rendered; the new native
   prefab compensates, giving 0.60 → **0.53 m**. Sheathed flat on the back at 27% of hero height instead
   of 50%, it reads as absent. (`228908bfc` body.)

## 2. What shipped

| Thing | file:line, verified at HEAD |
|---|---|
| Catalog row points at the address | `Assets/Resources/Data/Canonical/weapons.json:75` and `Assets/StreamingAssets/Data/Canonical/weapons.json:73` |
| The address is actually published | `Assets/AddressableAssetsData/AssetGroups/Gear.asset:1914` → `m_Address: gear/weapon/ShieldWithItemLogic` |
| The publisher tool (editor-only, allow-list of ONE prop) | `Assets/Editor/Catalog/SupercyanGearAddressableMarker.cs` (159 lines, tracked) |
| Size parity restored in the AUTHORED channel with a DERIVED value | `Assets/Resources/OffsetForge/offsets.json:23-41` — `id: ShieldWithItemLogic`, **rot/pos all 0**, `scale: 1.733`, `fullOverride: false` |
| Per-frame recompute of `CompensateParentScale` gated | `Assets/_Modules/Village/Hero/EquipmentController.cs` (`228908bfc`, +151/-…) |
| New oracle `[gear-prop-renders]` — reds on a gear address no group publishes, or a prefab with no active meshed renderer | `Assets/Editor/Regression/GearPropRendersRegression.cs`, **registered** at `Assets/Editor/Regression/DataRegression.cs:894` |

## 3. THE PROVING EVIDENCE

Source: `docs/proof/2026-08-18-overnight-gear-structures/README.md` (committed `fef3656d8`, 2026-08-18 21:59), device = Solana Seeker SM02G4061955851, builds **331306 → 331367**.

```
before  worldBounds s(0.42, 0.53, 0.41)
after   worldBounds s(0.72, 0.92, 0.72)   <- 0.92 m longest vs legacy shield_A 0.918 m = parity
trace:  "parent-scale compensate: off-hand id='knight_shield_starter'
         mesh='ShieldWithItemLogic' on 'Hero (Blaise)' ... authored=1.73"
volume: ~1800 lines/60 s BEFORE -> 4 lines for the ENTIRE session AFTER
```

- That trace line **names `mesh='ShieldWithItemLogic'` on a device build**, which is direct evidence for
  **acceptance §5.5** — the Addressables address resolves on hardware, not only in the editor. Had it
  fallen back, the mesh key would read `shield_A`.
- Parity to within **2 mm** (0.920 vs 0.918) is the stated goal: restore the size that already shipped,
  not pick a new one.
- Gate markers quoted by the commits: `COMPILE_GATE_OK`, `REGRESSION 207/211` (`c072e5736`) and
  `209/213` (`228908bfc`), both "4 known-baseline reds, no new red".

## 4. ⚠ A DELIBERATE DEVIATION FROM THE TICKET — read this before "correcting" it

**§5.6 and §7 of the WO say an `offsets.json` row for `ShieldWithItemLogic` must NOT exist.** One now does
(`offsets.json:23-41`). This was not an oversight and it is not the bug class the ticket retired:

- The ticket's rule existed to keep the shield off **hand-dialled rotational constants** — the WO-970
  stranding. **The authored row is rotationally EMPTY:** `rot = (0,0,0)`, `pos = (0,0,0)`. Nothing was
  dialled by eye.
- The only non-default value is `scale: 1.733`, and it is **derived arithmetic, not a dial**:
  `(1 / 1.666) × 1.733 = 1.04` local = **0.918 m** world = exactly the size that already shipped.
- `shield_A`'s row is untouched (`offsets.json:4-9`, still `rot=(-160,-180,-84)`) and
  `AlignAxesYLongXNarrowZWide` was not touched — both owner rulings held.

**If the owner wants a different shield size, `offsets.json:35` is the one knob.** Do not delete the row
to satisfy §5.6 literally — that reinstates the 0.53 m shield the owner reported as missing.

## 5. WHAT IS NOT PROVEN

1. **THE ACTUAL ANCESTOR BUG.** §5.3 — *enter a dungeon, exit to town, seat unchanged* — **was never
   exercised.** The proof README says so in its own LIMITS §3: *"The dungeon→town port … was NOT exercised
   at all."* Everything above proves the shield is the right size and resolves on device; **none of it
   proves the port no longer breaks the seat.**
2. **No post-port screenshot exists**, so §5.4 is open. §4 of the WO is explicit that headless gates cannot
   see orientation — the green markers above are necessary and not sufficient.
3. **Whether 0.918 m LOOKS right** is an owner call. It is parity with the previous build, which is a
   defensible default, not a design decision.
4. **Sheathed-on-back is correct, not a defect** (`EquipmentController.cs:2318` in the working tree — `bool drawn = _combatActive && ...`; the commit body cites `:2175`, the line has since moved. Out of combat both props go to the back socket, deliberate since 2026-07-04), so a "shield not on the arm in town" report is expected
   behaviour and must not be re-opened as this bug.
5. §5.1/§5.2 (fresh knight, shield on the off-hand arm, in town and in dungeon) have **no captured frame** —
   only the measurement.

**What would settle it:** one device session that ports dungeon→town and captures a frame plus the
`parent-scale compensate` line on both sides of the port.
