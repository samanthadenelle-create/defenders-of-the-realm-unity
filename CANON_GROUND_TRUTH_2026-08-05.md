# CANON GROUND TRUTH — 2026-08-05

**This is the single live anchor (§15). It supersedes `CANON_GROUND_TRUTH_2026-08-03.md`.**
Every session and every agent checks docs against this file. Sourced from HEAD commits and the working
tree, never from assumption.

**Branch:** `wip/village2-and-f8-tickets` · **HEAD at write time:** `0ab0eece`
**Gates green at HEAD:** `COMPILE_GATE_OK` · `REGRESSION_OK 113/113 suites` · `STATIC GATE: PASS`

---

## 0. THE PATTERN OF 2026-08-04 — read this before diagnosing anything

**Nine separate defects in one session were the same shape: a system that was fully built and wired to
nothing, or wired to the wrong thing.** Not missing features — orphaned ones. If a system "doesn't work,"
check whether it exists and is unreached BEFORE assuming it needs building:

| System | Was |
|---|---|
| Crystal Mine payout | Gated at L3; L3 unreachable. Never paid a crystal in its life |
| Windmill food perks | Authored under `windmill`, collector is `farm`. +45% reached nothing |
| `CollectorStackView` (437 lines) | The complete "collector is full" tell. `Attach` had ZERO callers |
| `UnderConstructionVisual` | Greys + silences a building tower. Never called on the Build-Menu path |
| Tower `repo.upgradeCost` | No tower authored one; all fell through a x1/x2 fallback |
| `Resources/Title/Title_L.jpg` | A PORTRAIT file under the landscape name. Real art sat unused in `Art/` |
| `EliteVFXController` | Fully written, never attached — all elite/boss VFX dead |
| `storageCapacity` / `IsStorageContainer` | Authored since WO-707, zero consumers, a `TODO` in the source |
| `ISiegeLootTarget.PendingLoot` | Zero readers. The reverse raid loop is ABSENT, not flagged off |

**The regressions added on 08-04 encode the general law:** *authored data with no reachable consumer is
resources the player spends for nothing.* See `[upgrader-reaches-receiver]`, `[yield-reachable-at-founding]`,
`[dead-tell]`, `[combo-pays-gold]`, `[one-reader]`.

---

## 1. Economy — REBALANCED 2026-08-04 (WO-855)

**Before the pass there was no grind at all**: mid-game a tower cost **4 seconds** of income, and every
wood/iron/food sink late-game was under 60 seconds. Root cause was structural — income scaled, costs did
not.

- **Collector faucets cut**: early **9.2x**, mid **13.1x**, late **20.2x**. Late wood/sec 211 → **11.7**.
- **Collector capacity is measured in HOURS, not units** — capacity scales on the same basis as rate, so
  hours-to-full is constant (**8h**: farm 7500 / lumbermill 5760 / forge 3456). Before, the curve ran
  BACKWARDS: upgrading a collector *shortened* unattended runtime.
- **Per-collector capacity IS the offline cap.** No second time-based cap.
- **Collectors accrue offline** (per-collector last-accrual stamp, PlayerPrefs, no save bump). Away-in-a-
  dungeon and app-closed are deliberately ONE case.
- **Town bank caps are LIVE** — `TownBankCapacity`, `baseCap` 2000 wood/iron/food, `AbsoluteMinBaseCap`
  1000. **CLAMP AND WARN uniformly** (owner ruling); the warn is load-bearing.
- ⚠ **Crystals and Coins are UNCAPPED, structurally** — a named `UncappableResources` array, guarded by
  `[no-crystal-cap]`. Not a comment.
- **Container fill/drain order: by CAPACITY ASCENDING, fill smallest first** → pallets fill and drain
  LAST. ⚠ `storageCapacity` must stay **below** `baseCap` (500, not 2000+) or the order silently inverts.
- **Existing saves are grandfathered** — nothing writes a wallet down; over-cap totals drain by spending.
- **Tower upgrade ladders: 1.0–1.2x place basket (L1→L2), 2.0–2.5x (L2→L3)** — owner ruling, SUPERSEDES
  the 4x/8x ruled earlier the same day.
- **Purchased grants are EXEMPT from the clamp** (`BankGrantKind.PurchasedOrPromised`) — the clamp was
  eating 3,080 of a 5,000-food pack.

**Measured baseline:** `docs/ECONOMY_REWARD_MEASUREMENT_2026-08-04.md`. **Owner rulings:**
`docs/design/OWNER_RULINGS_2026-08-04.md`.

---

## 2. ⚠ The reward economy is NOT yet fixed — measured, spec'd, not done

- **Active play pays LESS than idling**: 0.24x per wall-clock wave, 0.83x per combat-hour, and the ratio
  **never reaches 1.0** across all 20 waves.
- **Rewards do not scale with enemy level** — there is no level or tier field. A wave-20 walker has 2.5x
  the HP of a wave-1 walker and pays the identical 4 gold.
- **The apex dragon pays NOTHING** — 4,200 HP, the longest fight in the game, zero gold/XP/crystals.
- **Endless mode is an unbounded inflation exploit** — `_rewardScalingStepCap = 0`, +20% every 5 waves
  forever against difficulty that clamps at wave 60 (x41 at wave 1000).
- **Overworld reps are the actual optimal strategy** — zero contact damage, ranged-killable, 10s respawn,
  XP paid for a full pack while one body spawns.
- ✅ **CLOSED 08-04:** the kill-combo bonus paid **25+60 Aether Crystals** at ~400x the designed rate and
  supplied ~70% of banked value. Owner ruled it pays **GOLD**; pinned by `[combo-pays-gold]`.

---

## 3. Combat / towers

- **Two parallel tower systems still exist** and caused THREE bugs on 08-04 (wrong tower placed, cancel
  minting crystals, scaffold not applying): `BuildModeController`/`StructureFactory`/`DefenseTower`+
  `ArcaneTower` vs `TowerPlacementSystem`/`TowerConstructionQueue`/`Tower`. **Owner ruling: System B
  (Tower/TowerCombat) is DEAD legacy — do not touch it.**
- **A tower under construction cannot fight** (`UnderConstructionVisual` silences `DefenseTower` +
  `ArcaneTower` + `TowerCombat`). Before 08-04 Arcane Spires fired through their whole 4.5-minute build.
- **Projectile size is DERIVED from range** — `ProjectileVisualFraction = 0.06`, 0.84m at range 14 up to
  2.16m at range 36. Every tagged pick is `scale: 1.0`, authored for a demo scene.
- **`tower_catapult` is UNREACHABLE** — the build menu lists only the cheapest FOUR of five tower rows.
  It is **intended future content**: a DEPLOYED offensive siege unit (WO-906), not a placed tower.
- ⚠ **Only `tower_arcane_spire` authors an element (Aether). The other four author NONE, and enemies
  author none at all.** Per-tower affinity is spec'd as WO-907 — **match bonus, never a lock**, the Echo
  grammar.

**HELD awaiting owner tags in `VfxCasterWindow`** (routing built, will light up when tagged):
Sky Ballista projectile · Arcane Spire projectile (needs an **Aether** pick — fireball was reversed and
reassigned to the hero cast lane) · tower on-hit/impact keys (23 `*_Impact` rows exist, **none**
tower-keyed) · portal threshold aura.

---

## 4. UI — the recurring failure class

⚠ **Fraction-band culling is the documented root cause of repeated UI defects.** Bands must be
**FIXED PIXELS >= font line height, never a fraction of parent.**

**And a second mechanism, found three times on 08-04:** `ElarionUiKit.ClampMinTouch` grows a sub-floor
control **SYMMETRICALLY ABOUT ITS CENTRE**. A 60px control at a 16px inset ends at **-10px** — off screen.
It broke the Echo picker (WO-852), `Connect Wallet` (WO-868) and the skills action row (WO-865).

- `MinTouchPx = 112`, `FontFloor = 30`. **Text-encoded state, never colour alone** (owner is red/green
  colourblind). ASCII-only TMP strings — no glyph icons, they render as tofu.
- **Landscape locked** — portrait autorotate disabled.
- ⚠ **The Seeker's real surface is 2670x1200**, not 2340x1080. 2340x1080 is the HARNESS capture size.
- `SafeAreaInset` (`Core/UI`) is now the shared `Screen.safeArea` helper — there was none before 08-04.

---

## 5. UI capture — how the review is actually fed

⚠ **`RunCapture()` cannot work in `-batchmode -quit`** and its own header says so. **`AutoPilot` is the
headed capture path** — it renders the real game and writes `panel_<Screen>.png`, which is what
`UI_REVIEW/_mapping.json` reads.

⚠ **`run-autopilot-fleet.ps1` MUST be run with `-Graphics`.** Without it the fleet writes flat-black
frames — 33,150 bytes each — and **overwrites real review shots**. That is exactly what happened on
08-04: 35 blanks the assembler then badged "PAIR COMPLETE".

Now guarded: capture refuses to write with no graphics device; blank frames are refused rather than
counted; `-Graphics` runs purge stale shots first and default to 2340x1080.

**Command:** `run-autopilot-fleet.ps1 -Count 1 -Graphics -Width 2670 -Height 1200`
**Result at HEAD:** `UI_REVIEW/INDEX.html` = **30 pairs complete / 2 declared-exempt**, 0 blanks.

⚠ **`RunCaptureHeadless` renders code-built PANELS only.** It cannot see world-space UI, native aspect or
real DPI. **`UI_CAPTURE_OK` is necessary but NOT sufficient** — the collector fill tell, the combat HUD,
the build worker and the portal all need device verification.

---

## 6. Save / data

- **Save schema v36.** Nothing on 08-04 bumped it — the collector offline stamp is PlayerPrefs, and
  `completeOn` is catalog content, not the persisted contract.
- **Dual-copy canonical JSON law holds** — `Resources/Data/Canonical/` WINS at runtime;
  `StreamingAssets/` must be byte-identical. ⚠ **`weapons.json` is EXEMPT** (owner ruling): it is a
  generated curated export (`GearCurationExporter`), hand-editing it corrupts it.
- ⚠ **`/Assets/Resources/Structures/` is GITIGNORED** — mirrored prefabs must be registered in
  `CatalogPrefabImporter`, or they work locally and are missing on a fresh clone.

---

## 7. Known-broken, recorded, NOT fixed

- **Collectors have no offline accrual outside the hub** — the component only exists there. The phantom
  direct grant was masking it by paying everywhere.
- **Gear improve is instant** (`GearProgression.cs:250`) — the only progression sink costing resources
  but no time.
- **A queued Crystal Mine pays crystals during its own construction**; `HealingFountain` heals while
  unbuilt. Both carry wave subscriptions so silencing them is not side-effect-free.
- **`HealingFountain`** authors `maxLevel: 3` AND keeps a legacy Coins F-key path — two systems can each
  level one building.
- **`ApplyTierStats`** hard-codes three component types; wants a generic `IStructureLevelReceiver` seam.
- **Six review screens have NO design template**: Hero Loadout, Game Guide, Echo Workforce, Raid
  Selection, Raid Deploy, Troop Training.
- **WO-837 step 1 never shipped** — `lumberyard` is still in `BuildModeController.FoundingKit`,
  contradicting the ruling that storage buildings are never founding freebies.

---

## 8. WO numbering

**Main line next free = 908.** ⚠ The main line **collided with the UI seat's reserved 860–899** and now
**jumps to 900+**. UI seat next free = **884**. Bump the banner in the SAME edit as any mint —
`CLI_LANES_WO_NUMBERS.md` is the sole authority.
