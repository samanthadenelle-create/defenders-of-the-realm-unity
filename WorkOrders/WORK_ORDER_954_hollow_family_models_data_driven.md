# WORK ORDER 954 — Hollow family still wears KayKit skeletons; enemy id→model mapping goes data-driven

**Status:** DONE — owner-confirmed 2026-08-21 (resolved 2026-08-20).

> ## §1b — TRUE STATE 2026-08-20 (verified at source, not from this document)
> **Deliverable 1 (data-driven mapping): SHIPPED.** Recorded by the 08-14 correction block below,
> which the Status line never caught up with.
>
> **Deliverable 2 (the hollow re-skin, OWNER-PINNED): TWO ROWS LANDED, SIX STILL WEAR KAYKIT.**
> Commit `577bde576` (2026-08-20) wired two owner-delivered AccuRig bodies end to end —
> `hollow-walker → Hollow_Walker` and `cellar-hollow → Cellar_Hollow` — retiring the
> `Skeleton_Minion` stand-in on the only rows that were ever borrowing it. Read from
> `Assets/Resources/Data/Canonical/enemies.json` today, the family stands at:
>
> | Still KayKit `Skeleton_*` (6) | Non-KayKit (4) |
> |---|---|
> | `hollow-warrior`, `hollow-rogue`, `hollow-acolyte`, `hollow-mage`, `hollow-reaper`, `hollow-apprentice` | `hollow-walker` (Hollow_Walker), `cellar-hollow` (Cellar_Hollow), `hollow-brute` (Skeleton_Golem_NEW), `necromancer` (Necromancer_NEW) |
>
> ⛔ **AND THE THING THIS WO PREDICTED WRONG: it is NOT a one-word JSON edit per row.** §1's closing
> claim ("with 1 landed it is now a one-word JSON edit per row") did not survive contact. Pointing
> `enemies.json` at a new model is **INERT** on a Hollow id: `EnemyFactory.ModelForEnemy` routes
> Hollows through `EnemyResolver.TryResolveHollowModel`, which honours a data `modelKey` **only if it
> is in `KnownHollowModels`**. A row naming art outside that set silently keeps spawning
> `Skeleton_Minion` **while `EnemyResolverRegression` reds the tree** — inert and failing at the same
> time. The real change is **five edits per body across four files**: `EnemyResolver.KnownHollowModels`
> · `EnemyResolver.CommittedModels` · the `HollowTable` row (ModelKey + AnimatorRig) ·
> `EnemyAnimatorFactory.RigFor` · `EnemyResolverRegression.ExpectedBaseModel` · plus `enemies.json`
> (**both** copies). And the one easiest to miss: **`EnemyFactory.AccuRigIntake`** — both bodies are
> `CC_Base` **+X-forward** exports and would have spawned turned 90°. They belong in the intake
> **despite** routing to the SkeletonHumanoid rig, which is the distinction that set exists to draw:
> **rig class = which CLIPS a body plays; the intake = which way its mesh FACES.** The KayKit
> `Skeleton_*` bodies share that controller and face **+Z**; these two share it and face **+X**.
> Judging either by the other stands a body sideways in a raid.
>
> Also landed in that commit: **`cellar-hollow` LOSES its `Variant "cellar"`** (the kneel-rock sorrow
> idle). Owner's ruling for that body is *"a tanky type or barbarian ish type"* — a kneeling mourner
> is the opposite read — so `Variant` is **null**, not carried.
>
> **Proof:** `REGRESSION_OK 227/227 suites — 227 green, 0 red, 0 skipped` + `COMPILE_GATE_OK`. The
> four failures standing before it (all naming `Hollow_Walker` or `Cellar_Hollow`) are closed.
**Minted:** 2026-08-10 (CLI seat, main line — banner bumped 954 → 955 in the same edit)
**Silo:** Village/Enemies + enemies.json — coordinate with nothing currently in flight
**Origin:** owner 2026-08-10: *"I am still seeing a lot of kaykat enemies and didnt think we had any
left. thought it was resolved."*

---

## 1. Verified state (2026-08-10, at source + her live log)

- Proving line: `[Flow:Enemy] instantiated 'Enemy (hollow-acolyte)' ... (family 'hollow', model
  'Skeleton_Healer')` — clean resolve, no fallback Warn. AUTHORED, not a regression.
- The whole hollow family (10 ids incl. necromancer) maps to plain-named `Skeleton_*` models = the
  KayKit skeleton pack. The 2026-08-09 AccuRig wave imported `Skeleton_Golem_NEW`, `Necromancer_NEW`
  + the troll family (`_NEW` suffix distinguishes AccuRig from KayKit) — it never covered the hollows.
- ⚠ `AtbCombatantSwapper.cs:561` comments `Skeleton_Warrior` as "AccuRig melee" — FALSE (comments lie).
- **The mapping is scattered CODE, not data** — at least four independent tables:
  `AtbCombatantSwapper.cs:505-563` · `OutpostEnemyGroupSpawner.cs:529-537` ·
  `RegionMobSpawner.cs:539+` · `EnemyFactory.cs:517` (comment) — the WO-772 divergence class, alive
  for MODELS. `enemies.json` rows carry NO model field (verified: all 19 rows).

> ## ⚠ CORRECTION 2026-08-14 (implementation seat, verified at source)
> Two §1 claims did NOT survive re-verification and the implementation was re-scoped accordingly:
> - **"`enemies.json` rows carry NO model field (verified: all 19 rows)" is FALSE.** Every one of the
>   19 rows already carries `modelKey`, and `EnemyFactory.ModelForEnemy:511` already passed it to
>   `EnemyResolver.TryResolveHollowModel`. The field did not need adding — what was missing is that
>   **only the Hollow branch honoured it**; for every other family the code switch won outright.
>   That is the divergence that was actually fixed.
> - **`OutpostEnemyGroupSpawner` is no longer a divergent table** — WO-1001 landed after this WO was
>   written and made it read `enemies.json` first (`DefFor`), with the code block demoted to a
>   fallback that already `FlowTrace.Warn`s. No change needed there.
> - **`RegionMobSpawner.ModelForRoamer` had NO CALLERS** (repo-wide grep: one hit, its own
>   declaration) — dead code stating the OPPOSITE of live behaviour. Removed.
> - Newly found and fixed en route: `AtbCombatantSwapper` mapped `hollow-king` → `"Dragon"`, a mesh
>   **retired 2026-07-24 and absent from Resources/Enemies** — that boss staged as a violet capsule
>   pill, and the null-load was logged as a `Step` reading *"expected fallback"*.
>
> **Deliverable 1 is implemented. Deliverable 2 (the re-skin) remains OWNER-PINNED and untouched** —
> with 1 landed it is now a one-word JSON edit per row, exactly as this WO intended.

## 2. Deliverables

1. **Data-driven mapping:** add a `model` field per row in `enemies.json` (dual-copy, version bump —
   note §10.3 canon: a content change bumps the version), seeded EXACTLY with today's live mappings
   (behavior-preserving first commit). ONE resolver reads it (extend `EnemyResolver` — it exists for
   this); the four code tables collapse to reading the data, kept only as the last-resort fallback
   with a `FlowTrace.Warn` naming the missing row. Regression: every enemies.json row has a model
   that resolves on disk; the code tables and data agree (until the tables are deleted in a follow-up).
2. **The hollow re-skin (⚠ OWNER PIN):** which models replace the KayKit skeletons for the hollow
   family is a CREATIVE pick. Available in-tree/downloads as of 08-09-10: the AccuRig troll family,
   `Skeleton_Golem_NEW`, `Necromancer_NEW`, plus un-imported zips in Downloads (fantasy orc, stone
   golem, Orc Warlord). If none fit the hollow look, that is an art-sourcing task, not a code task —
   with deliverable 1 landed, her pick becomes a one-word JSON edit per row.
3. QR-5.2/5.3 discipline on any newly imported model (Humanoid rig verify, -90 yaw by NAME opt-in,
   never blanket).

## 3. What NOT to touch

The 08-09 troll/AccuRig wiring · EnemyFactory pooling · stat blocks (WO-772's other half) · no model
deletions (KayKit skeletons may stay for other uses).

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — NEEDS OWNER RULING. Evidence: `enemies.json hollow-* still Skeleton_*` — six re-skins need owner bodies. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.

> **OWNER RULING 2026-08-21 (verbal, this session):** Hollow family re-skin: owner states this was resolved yesterday. NOTE for whoever reopens it: the 08-21 audit read enemies.json as still mapping hollow-* to Skeleton_*. Owner statement is ground truth and wins; if capsule/skeleton Hollows show on device, that read is the lead.
