> ## RECONCILED 2026-08-08 - true status is PARTIAL
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: 35b1020f shipped StructureTargetableRegression.cs and f2069890 ruled section 7's 50/30/20 split (RaidScoring.cs:123/137/144), but section 11 of this file records acceptance 1's pathing half as deferred and acceptance 2 as unmet.
> The previous Status line read "Status: READY TO IMPLEMENT" and was wrong; the board understated this.

# WORK ORDER 853 — Structures are targetable (the disjoint-contract seam)

**Status:** READY TO IMPLEMENT - partial (reconciled 2026-08-09, per this file's own 08-08 banner - `35b1020f` shipped `StructureTargetableRegression.cs` and `f2069890` ruled sec.7's 50/30/20 split (`RaidScoring.cs:123/137/144`), but sec.11 records acceptance 1's pathing half as deferred and acceptance 2 as unmet)

**Status: PARTIAL**
**Minted:** 2026-08-03 (CLI, main-line block; banner bumped 853 → 854 in the same edit)
**Lane:** Combat/AI + Village structures. Touches Core contract, Village structures, troop/hero targeting, raid scoring.
**Owner ruling needed on one number only** — see §7.

---

## 1. THE DEFECT, IN ONE LINE

**Nothing in this game can damage a wall, gate, or enemy tower** — so a raid is a fight, never a demolition.

Proven from code, not inferred:

- `WallSegment.cs:28` and `Gate.cs:45` implement **`IDamageableStructure` only**
  (`IsAlive` + `ApplyContactDamage` — no position, no faction, no HP).
- `TroopController.NearestHostile()` (`TroopController.cs:449-469`) sweeps for **`IDamageable`** via
  `GetComponentInParent<IDamageable>()` and rejects `Faction != Hostile` at `:460`.
  `PlayerAttackController.ResolveAttack` (`:592-637`) does the same.
- **The two interfaces are disjoint.** A wall can be *hit* (enemies reach it through
  `Enemy.ProbeForStructure`, a `~0`-masked physics sweep) but can never be *found* by anything that
  targets by searching.

**Consequence:** "Razed %" counts bodies. `RaidScoring.cs:126-155` = 60% spire HP + 40% dead enemies
(`SpireWeight = 0.60f`). Walls, gates and towers contribute **nothing**.

## 2. WHY THIS IS RANKED FIRST

It is the prerequisite under **both** long-range roadmaps: a raid can never be about bases under either
posture (drop-and-watch or hero-led), and a player-authored base that another player attacks has nothing
to attack. It therefore also makes the **WO-774.0 posture ruling free to defer** — the seam is required
either way.

## 3. THE PRECEDENT — EXTEND, DO NOT INVENT

**`RaidSpire` already solved this** (`Village/World/Camps/RaidSpire.cs:61`). It implements **both**
contracts, routes both entry points into one `ApplyDamage` (`:232`), exposes `WorldPosition` (`:213`) and
hardcodes `CombatFaction.Hostile` (`:210`). `RaidSpire.cs:10-31` is an explicit essay on exactly this
seam. `BreakableContainer.cs:38` is the same shape.

**Do not add a new interface, do not make `IDamageableStructure : IDamageable`.** Inheritance would force
`WorldPosition`/`Hp`/`Faction` onto all **18** implementors including `HeroHealth` and `HeartController`.
(⚠ `IDamageableStructure.cs:7` claims five implementors. There are **18**. Fix that comment.)

## 4. ⛔ THE CONSTRAINT THAT SHAPES EVERYTHING — DO NOT RELAYER WALLS

`RaidSpire` and `BreakableContainer` make themselves findable by **rewriting `gameObject.layer` to
`"Enemy"`** (`RaidSpire.cs:162-201`, `BreakableContainer.cs:145-146`), because every `IDamageable` sweep
masks on layer `Enemy` (7).

**That trick MUST NOT be copied for walls.** Layer `Structure` (8) is the **line-of-sight blocker mask**:
`DefenseTower.BlockedByWall:523-531`, `TowerCombat.cs:194-208`, `ArcaneTower.cs:377-385`,
`PlayerAttackController.cs:225`, `HeroTargetIndicator.cs:258` all linecast against it. Moving a wall to
`Enemy` would make **towers shoot through walls again** — regressing the fix shipped in `2cb3c40d`.

**Walls stay on `Structure`. The masks widen instead** — which is only safe because of §5.

## 5. THE DESIGN — MAKE `CombatFaction` REAL

`CombatFaction.Friendly` (`IDamageable.cs:31`) is **vestigial**: all four production implementors hardcode
`Hostile`, so the ~20 `Faction != Hostile` checks always pass. This WO gives it its first real producer.

1. **Dual-implement** on `WallSegment`, `Gate`, and `DefenseTower`, following the `RaidSpire` shape:
   add `IDamageable` with `WorldPosition`, `Hp`, and a **derived** `Faction`.
2. **Faction derives from ownership, never a serialized field:**
   - `DefenseTower` → `Allegiance == EnemyOwned ? Hostile : Friendly`
   - `WallSegment` / `Gate` → `SceneOwnership.IsEnemyOwned ? Hostile : Friendly`
     (`SceneOwnership.cs:33`, already flipped for raids by `RaidGarrisonSpawner.cs:153`)
3. **Add layer `Structure` to the target masks** on `TroopController._enemyMask` (`:57`),
   `PlayerAttackController._enemyLayer` (`:80`), `HeroAbilities._enemyMask` (`:65`),
   `HeroTargetIndicator._enemyMask` (`:249-250`). **This is only safe because step 2 makes the existing
   faction filters actually filter** — your own perimeter reports `Friendly` and is rejected at the
   sweep sites listed in §1.
4. **Fix the `IsAlive` overload.** `DefenseTower.cs:140` reads
   `IsAlive => Allegiance == PlayerOwned && Hp > 0 && !_broken`. That single expression is why an
   enemy turret is invisible to `Enemy.SweepForNearestStructure`, `DragonBoss.NearestAliveTower` and
   `StructureBurn`. Split liveness from ownership: `IsAlive => Hp > 0 && !_broken`, and move the
   ownership test to the **callers that meant ownership** (`:172` `ApplyContactDamage`, `:267-271`
   stat gates, `WaveDamageReport.cs:127`). ⚠ `RaidBaseGenerator.cs:746-749` documents enemy towers as
   "INDESTRUCTIBLE by design" — that comment is superseded by this WO; update it.
5. **A destroyed wall must actually collapse.** `WallSegment.Collapsed` and `DamageChanged` have
   **zero subscribers repo-wide** — at damage 100 the collider stays on, the mesh never changes, nothing
   spawns. It still stands, still blocks pathing, still blocks tower LoS. Implement the collapse:
   disable the solid collider, drop the LoS block, and give it a visible tell. `Gate` already has the
   only real destruction tell in the game (`_Collapse` shader ramp, `Gate.cs:285-308`) — mirror its shape.
   **Without this, steps 1–4 buy nothing the player can see.**

## 6. SCOPE EXCLUSIONS — read before estimating

- **Gates are OUT of raid scope.** There is **no `Gate` component in any `RaidBase_*` scene** — zero
  across all four. A raid "gate" is a literal skipped wall panel (`RaidBaseGenerator.cs:821`). Gates
  matter only for the player's own perimeter.
- **`RaidBase_IronBastion` is out.** No spire, no `RaidGarrisonSpawner`, so `DestructionPct` reads 0 and
  `RaidWon` is permanently false — it is **currently unwinnable**. It is on disk but NOT in build
  settings, so it is unreachable. **Recommend deleting it** rather than fixing it (separate ticket).
- **Do not touch `Destructible.cs`.** It is player-village-only (composed by `Building`, `DefenseTower`,
  `ArcaneTower` only) and steps 2–3 of `NotifyBroken` write `GameState.BaseLayout`, `FreeBuildsUsed` and a
  player-facing "rebuild it at full cost" toast. Firing that on an enemy raid wall is wrong-facing and
  risks corrupting the player's persisted layout.
- **Do not touch `Enemy.ProbeForStructure`.** The enemy→structure path already works.

## 7. ✅ RULED + SHIPPED 2026-08-07 — raid scoring weights

> **OWNER RULING: 50% spire · 30% structures · 20% garrison.** The proposal below was taken as authored.
> Implemented in `RaidScoring.cs` the same day. `COMPILE_GATE_OK` + `REGRESSION_OK 128/128 suites`,
> `RAID_SCORING_OK`, trace line reads `destruction split: spire 50 % / structures 30 % / garrison 20 %`.
>
> **What actually shipped, beyond the number:**
> - `SpireWeight` 0.60 → **0.50**; new `StructuresWeight` = **0.30**; `GarrisonWeight` is **derived**
>   (`1 - Spire - Structures`), never a fourth literal — three hand-typed weights are three chances to
>   publish a split that does not sum to 1.
> - `StructuresRazedPct` = census of `WallSegment` + **non-PlayerOwned** `DefenseTower`, denominator
>   captured once in `CaptureStructureCensus()` at raid start. Components are **cached**, not re-found
>   per frame — `DestructionPct` is read by the HUD every frame.
> - **Read through `HpFraction`, NOT the `1 - Damage/100` this section proposed.** A wall stores an
>   inverted 0–100 track (`WallSegment.cs:99-100`), so that expression is only equal to HpFraction while
>   `MaxHp` happens to be exactly 100. Reading the shared abstraction means a later change to that
>   constant cannot silently skew raid scores. An oracle now rejects the hardcoded form.
> - **Absent terms are RENORMALISED, not scored as zero.** A legacy base with no spire, or with no walls
>   and no turrets, would otherwise cap its razed bar at 70% or 50% forever — silently breaking star
>   thresholds and the loot scale for every scene predating the term. With neither present it collapses
>   to the original pure-garrison count, so nothing that shipped before WO-771.6 regresses.
>
> **Correction to this section's ⚠ warning:** it said any change trips
> `RaidArenaShapeRegression:531-542`, `RaidScoringTests.cs` and `RaidScoringRegression:115-116`. It did
> not. Those `0.60f` values are `destructionPct` **arguments** to `ComputeStars`, not the weight, and
> `:533` only asserts the literal string `"SpireWeight"` still appears. **Nothing anywhere pinned the
> split**, so the ruling would have landed with no guard at all and could have drifted silently. New
> section (D) in `RaidScoringRegression` now pins all three weights as *executed* assertions, asserts
> they sum to 1, and fails loudly if `StructuresWeight` ever returns to 0 — the exact defect §1 opened
> with — or if the spire stops being the largest term.
>
> **Also worth recording:** §1's "nothing in this game can damage a wall, gate, or enemy tower" was
> already fixed before this ruling. `WallSegment`, `Gate` and `DefenseTower` all implement `IDamageable`
> and `PlayerAttackController` reaches structures (all tagged WO-853 in source). Scoring was the last
> piece, which is why one number was the whole blocker.

---

## 7-original. ⚠ THE ONE OWNER DECISION — raid scoring weights *(kept for provenance)*

`RaidScoring.DestructionPct` (`:126-155`) has two terms. It needs a third.

**Proposed:** **50% spire · 30% structures · 20% garrison** (today: 60/0/40).
Rationale: keeps the spire the primary objective, makes demolition meaningfully rewarding, and stops a
pure corpse-farm from reading as a razed base.

Structures term = a census of `WallSegment` (`1 - Damage/100`), plus enemy-owned `DefenseTower`
`HpFraction`. Denominator captured at raid start — walls and towers exist from scene load, so unlike the
staggered garrison this needs **no peak-tracking**.

⚠ `RaidArenaShapeRegression.cs:531-542` asserts the literal source strings `"RaidWon"`,
`"ComputeStars(RaidWon"` and `"DestructionPct"` exist in `RaidScoring.cs`; `RaidScoringTests.cs` and
`RaidScoringRegression.cs:115-116` pin the `ComputeStars`/`ComputeLoot` signatures. Any change trips
these — update the oracles in the same commit.

## 8. SILOS (file-disjoint — one agent each, edit-only, none of them gate or commit)

| Silo | Files | Task |
|---|---|---|
| **A — Core contract** | `Core/Combat/IDamageableStructure.cs` | Fix the 5-vs-18 implementor comment. **No signature change.** Serialize FIRST; everything else reads it. |
| **B — Structures** | `Village/Walls/WallSegment.cs`, `Village/Gates/Gate.cs` | Dual-implement per §5.1-2 + the collapse behaviour §5.5. Also fix the false comment at `WallSegment.cs:167-168` (see §9). |
| **C — Towers** | `Village/Buildings/DefenseTower.cs` | Dual-implement + the `IsAlive` split §5.4 + caller fixes. |
| **D — Targeting masks** | `Village/Troops/TroopController.cs`, `Village/Enemies/PlayerAttackController.cs`, `Village/Hero/HeroAbilities.cs`, `Village/Hero/HeroTargetIndicator.cs` | Mask widening §5.3 ONLY. Do not touch faction-check logic — it already exists and is correct. |
| **E — Scoring** | `Village/Troops/RaidScoring.cs` + its three oracles | §7, once the owner confirms the weights. |
| **F — Oracle** | `Assets/Editor/Regression/` (new suite) | See §10. |

## 9. A LIVE BUG FOUND WHILE MAPPING — fold into silo B

`WallSegment.cs:167-168` states *"enemy strongholds do not use these components, so no ownership gate is
needed."* **False** — `RaidBaseGenerator.cs:982` puts a `WallSegment` on every raid wall panel.

Consequence: `StructureToughnessReduction` (`:171-189`) applies the hero's BULWARK `structureToughness`
talent to **enemy raid walls**, up to −50%. **Investing in your own defence makes enemy walls tougher.**
Gate it on faction in the same pass.

## 10. ACCEPTANCE

1. A deployed troop acquires and destroys an enemy raid wall; the wall visibly collapses and stops
   blocking pathing and tower LoS.
2. The hero can attack an enemy wall and an enemy tower.
3. **A troop never targets the player's own perimeter** in the hub — the faction filter holds.
4. **Towers still do not shoot through walls** — `TowerLosLogicTests` stays green (this is the §4 regression risk).
5. `DestructionPct` reflects razed structures per §7; the three scoring oracles updated and green.
6. BULWARK no longer protects enemy walls (§9).
7. New oracle asserts: every `IDamageable` implementor's `Faction` is derived, never serialized; walls
   remain on layer `Structure`; and **a `WallSegment` driven to 100 damage has its solid colliders
   disabled** — assert the BEHAVIOUR, not a subscriber count.

   > ⚠ **CORRECTED 2026-08-03 (silo B feedback — the original criterion was unsatisfiable).** It read
   > "`WallSegment.Collapsed` has at least one subscriber". The collapse is implemented as the
   > component's OWN lifecycle, mirroring `Gate.ApplyForceFieldState` which is likewise self-owned — so
   > the event correctly has zero external subscribers and always will. Asserting a subscriber count
   > would have forced a fake listener into existence to satisfy a gate. Assert the observable effect.

## 11. FOLLOW-ON TICKETS OPENED BY IMPLEMENTATION (do not fold into 853)

- **Razed raid walls will not open a walkable lane.** Wall pathing blocks come from carving
  `NavMeshObstacle`s, and `WallNavObstacleInstaller` only targets objects named `WallBarrier-*` (`:131`).
  `RaidBaseGenerator.PlaceSegment` names its objects `Wall_<name>` and adds no obstacle — raid arenas rely
  on the **baked** navmesh. So a razed raid wall stops blocking tower LoS and physics, but troops still
  will not path through the gap until raid walls carry carving obstacles or the arena is re-baked.
  Acceptance #1's pathing half is therefore **partially deferred** to this ticket.
- **Baked enemy turrets sit on layer `Default`.** `RaidBaseGenerator.ArmTower` (`:751`) assigns no layer
  and only wall panels are moved to `Structure` (`:995-996`), so widening the hero/troop masks to
  `Enemy|Structure` still will not return an enemy turret. Needs a `RaidSpire.EnsureHittable`-style layer
  move for `EnemyOwned` towers — safe here, because towers are not LoS blockers, so §4's wall constraint
  does not apply. **Acceptance #2 depends on this.**
- **Wall damage does not persist a collapse.** `_collapsed` is not serialized and nothing calls
  `Collapse()` outside `ApplyDamage`, so a wall restored from save at 100 damage stands with its collider on.
- **`Assets/_Modules/Core/README.md:10`** repeats the stale 5-implementor claim (there are 18).
8. Gates: `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n>` + `TESTS_OK <n>/<n>`, counts read off the markers.
9. Owner felt-verifies. **PO closes, not CLI.**
