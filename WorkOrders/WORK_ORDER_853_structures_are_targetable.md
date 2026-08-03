# WORK ORDER 853 — Structures are targetable (the disjoint-contract seam)

**Status: READY TO IMPLEMENT**
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

## 7. ⚠ THE ONE OWNER DECISION — raid scoring weights

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
   remain on layer `Structure`; and `WallSegment.Collapsed` has at least one subscriber (the §5.5 gap
   was invisible precisely because nothing asserted it).
8. Gates: `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n>` + `TESTS_OK <n>/<n>`, counts read off the markers.
9. Owner felt-verifies. **PO closes, not CLI.**
