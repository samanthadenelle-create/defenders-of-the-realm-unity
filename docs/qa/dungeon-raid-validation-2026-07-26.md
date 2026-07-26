# Dungeon + Raid Work-Order Validation (handoff readiness) — 2026-07-26

**Purpose:** certify the dungeon and raid work-order set is complete, correct against the
code, and firmly implementable by CLI. Independent adversarial verification was run over
each set (coverage, citation re-check, determinism, implementability) and every finding was
resolved. This is the "make sure everything is good for raids and dungeons" sign-off.

**Deliverables validated (all in `docs/qa/`):**
`dungeon-regression-2026-07-26.md`, `WORK_ORDER_770_dungeon_functional.md`,
`WORK_ORDER_771_raid_system.md` (v2), `WORK_ORDER_772_enemy_system.md`,
`WORK_ORDER_773_obsidian_queue.md`, `coc-raid-system-design.md`.

---

## 1. Dungeons — verified complete

**Citations:** all 5 load-bearing regression citations re-verified against the current
tree — **CONFIRMED, no drift** (D1 `ExitToVillage` 0 callers + no PauseController/exit pad;
D2 no `DungeonController`/`folks-granary.json`; D3 `EncounterTrigger.cs:312`; D4
`DungeonController.cs:627`; D6 `LoreStone.Read()` 0 callers + no modal).

**Coverage (D1–D16 → work order):** complete. The two holes the review found are closed:
- **D10** (placeholder hero vitals) — now owned by **WO-770.10** (was uncovered).
- **D14** (dead dialogue) — now firmly owned by **WO-770.7**; the circular 770.7↔770.9
  handoff was removed.

**Implementability fixes applied:** WO-770.1 declares its dependency on 770.3 and grounds the
exit/back-door in real layout room bounds (no invented coordinates); WO-770.3 pins the
`BattleController.HandleOutcome` seam and locks the defeat behavior; WO-770.8 is content-only
(hero code split to 770.10).

**Owner-requested additions folded in:** first-class entrance+exit system (WO-770.1);
**dungeon enemy-placement system (WO-770.11)**; enemies' classes/families/armor/weapons via
the shared **WO-772**.

Dungeon set: **11 sub-orders (770.1–770.11)**, every finding owned, no circular handoffs.

---

## 2. Raids — rebuilt to firm COC-fidelity (v2) and re-verified

The v1 raid set was rebuilt after an adversarial review caught load-bearing errors. A final
verification pass **CONFIRMED every v2 fix against the code**:

| v2 fix | Verdict | Location |
|---|---|---|
| `IDamageableStructure` is in `DeNelle.Village`, not Core → move to Core (WO-771.0) | CONFIRMED | `Village/Enemies/Enemy.cs:34/43`; implemented only by Village types |
| `IDamageable` in Core + `TakeDamage(float, DamageElement)`; Pet's 1-arg is wrong sig → adapter needed | CONFIRMED | `Core/Combat/IDamageable.cs:42,62`; `Pet.cs:268` |
| No tower-fire code exists → WO-771.10 greenfield | CONFIRMED | no `Tower*.cs`/projectile; towers are `List<int>` (`GameState.cs:72`) |
| `SaveSchema.CurrentVersion == 10` → bump to 11 (WO-771.1b) | CONFIRMED | `SaveSchema.cs:30` |
| Loot grant = new mutator on the patch pattern (`:266`), not `RecordRun` (`:321`) | CONFIRMED | `GameStateService.cs:266` vs `:321` |
| Paths `Pets/Pet.cs`, `Village/Enemies/Enemy.cs` | CONFIRMED | both exist as cited |

**COC end-to-end coverage:** all 13 loop steps have a real owning sub-order — **no GAP**
(train→771.8, barracks→771.9, shield-aware matchmaking→771.7, deploy→771.4, watch→771.5+771.3,
tower fire-back→771.10, breach re-route→771.3 §2, live HUD→771.11, win/lose/stars/timer→771.3/771.6,
attacker loot + defender loss/shield/revenge→771.6+771.12, replay/anti-cheat→771.7, art→771.13+772).

**Determinism:** the WO-771.3 approach (fixed-point Q32.32, no float/Mathf/Vector3 in the
authority hot path; integer flow-field recomputed on wall-death; stable-ordered iteration;
lowest-index tie-break; defined tick-rounding; golden-vector gate) is **sufficient for
byte-identical cross-architecture replay + server re-sim**. `Combat.CalculateDamage` is safe
to reuse (pure `double`, no transcendentals).

**Final verification findings — all resolved:**
1. *(real defect)* 771.3↔771.9 dependency-vs-critical-path contradiction → **fixed**: 771.9 is
   a **soft** dep of 771.3 (sim runs on the level-1 baseline; 771.9 built after per the
   critical path).
2. Stale NavMesh-polyline prose in the design-doc body → **fixed** (A5/B4b/B6 now describe the
   fixed-point flow-field).
3. Deploy-log coordinates → **fixed**: WO-771.4 now requires `Xz` quantized to the sim grid at
   capture, so the replay triple is arch-independent by construction.

Raid set: **15 sub-orders (771.0–771.14)**, all citations accurate, no phantom files, cross-refs
consistent.

---

## 3. Cross-cutting systems

- **WO-772 (enemy system):** classes + families + equippable armor/weapons; one `EnemyResolver`
  feeds both dungeon placement (770.11) and raid rosters/art (771.13) — no duplication.
- **WO-773 (Obsidian queue):** one slotted, offline-fair job queue; buildings/upgrades/troops/
  towers all `Enqueue` into it. Raid training (771.8) and upgrades (771.9) route through it;
  the schema lands via the single WO-771.1b migration; existing `PendingBuilds`/`BuildingCooldowns`
  migrate in. Verified cross-references are consistent.

---

## 4. Residual owner actions (not blockers — outside CLI's code work)

1. **Asset staging (content-gated ACs):** KayKit Dungeon Remastered (WO-770.8), KayKit
   Adventurers **+ Skeletons** (WO-771.13/772), `echoes-beneath-elarion.mp3` (WO-770.8), and
   hero/troop walk clips. SESSION_HANDOFF says Adventurers is staged; **confirm Skeletons +
   Dungeon-Remastered**. Every content-gated AC has a code-verifiable counterpart that does
   NOT need the asset, so implementation can proceed and only the "renders/animates" checks wait.
2. **Balancing constants** (`[OPEN]`) are consolidated in WO-771.14, gated on
   `monetization-v2-spec.md` / `wallets-of-record.md`.
3. **Push access (blocked, 403):** all of the above is committed locally only. Grant this
   session push to `samanthadenelle-create/defenders-unity` to push + open the draft PR.

---

## Verdict

**Dungeons and raids are handoff-ready.** Every regression finding is owned; the raid loop is
COC-complete with a sound determinism model; all code citations are verified accurate; the two
owner systems (enemy taxonomy, Obsidian queue) are specced and wired in. The only waits are
owner-side asset staging and balancing — neither blocks CLI from starting the code work in
dependency order (raids: 771.0 → 771.1 → 771.1b → 771.2 → 771.3 → 771.10 → 771.4/.5 → …;
dungeons: 770.3 → 770.1, then the rest).
