# NIGHT WRAP — 2026-08-02 → 08-03 (CLI, solo run)

**Frozen point-in-time report. Do not rewrite; supersede by date.**

Owner went to bed after authorising: gate → commit everything → APK to Firebase →
exe → WebGL to Vercel. All of it ran solo. This is what happened.

---

## 1. GATES — all green, on the real tree

| Gate | Marker | Note |
|---|---|---|
| Compile | `COMPILE_GATE_OK` | zero errors |
| Data regression | **`REGRESSION_OK 104/104 suites`** | was 86 registered this morning |
| EditMode tests | **`TESTS_OK 912/912`** | was 884 |
| UI capture | `UI_CAPTURE_OK 28` | |

Re-run after the raid bake: still `REGRESSION_OK 104/104`.

**The gate found three real things before they shipped**, which is the whole point:

1. **A namespace/type collision.** The new `DeNelle.Core.Difficulty` namespace shadowed
   the existing `Difficulty` *enum* in `DeNelle.Core`, so every `DeNelle.Core.Difficulty.Hard`
   in the save tests resolved to a namespace. Renamed to `DeNelle.Core.Adaptive`
   (the enum is persisted in saves — renaming *it* was not an option).
2. **A stale test fixture.** `CoreSaveRegression` bound `"test-wallet-123"` and asserted it
   survived reset. A hyphen is not base58, so the new identity work correctly retires it —
   the test was asserting the *broken* behaviour. Swapped for a real base58 fixture.
3. **A false positive in a brand-new oracle.** `[dead-keys]` did a raw `IndexOf` over the whole
   file including comments, so it failed on the comment *documenting* that
   `scaleAggressiveTactics` was correctly left out. Now matches a declaration, not a mention.

---

## 2. WHAT SHIPPED — 15 commits, pushed

`e60b19e5` → **`8e70a3d4`**, local == origin.

Highlights, in the order they'll be felt:

- **Enemies now reach you.** Two independent causes of "they just mill around": DPS/Ranged
  enemies were targeting their *own wounded ally*, and `_stopTightenedForHero` survived pooling
  so a reused body halted 2.5 m out — outside the 1.5 m engage ring.
- **Pooled enemies no longer freeze as statues** (`_casting` was never cleared on death), and a
  reused ranger no longer fires arrows empty-handed.
- **Raids are not a square room.** 2.4% of the floor → **20 / 49 / 60%**, with a central spire as
  the win condition instead of a corpse count. Raid walls had **no colliders at all**, and no raid
  scene had a hero spawn point — the hero was landing on the castle courtyard coordinates, i.e.
  inside the walls on top of the objective.
- **Raid troops animate and aren't magenta.** Nothing under `Troops/` ever assigned an
  AnimatorController, and MagentaGuard only swept once at scene load — so anything spawned
  mid-raid was permanently invisible to it.
- **A level-1 Mage is no longer unarmed** (self-inflicted this evening; caught and fixed), shield
  upgrades finally do something, and the defense cap is one constant at 0.90 instead of fourteen
  literals that had drifted into two different numbers.
- **The tutorial's Hollow step can be completed.** It armed in an enemy-owned scene where
  building silently refuses, and asked for a "Lumberyard" that is a *stockpile* and harvests nothing.
- **Login can't hard-softlock**, stub wallets can't collide across devices, and **bug reports carry
  a stack trace** (they were arriving with the message only).
- **Cloud save and bug reports work at all.** See §4 — this was worse than we thought.
- **The check-in gate had never been running.** Not "ran the wrong suite" — the script did not
  *parse* under PowerShell 5.1.

---

## 3. BUILDS

| Artifact | Status |
|---|---|
| **Seeker APK** | Built fresh 22:28, **distributed to the `testers` group on Firebase** with release notes |
| **Windows exe** | Wiped and rebuilt, `Builds/Windows/` 1.7 GB, 22:34 |
| **Raid scenes** | Re-baked + navmesh, 4/4 walkable, committed |
| **WebGL → Vercel** | see §5 |

APK release id `18p6i1m81ctro`, app `1:264518851517:android:8e193b012cba6986d050d4`.

---

## 4. ⚠ THE ONE THING TO READ FIRST IN THE MORNING

**You ran `schema.sql` — that alone fixed production's nonce endpoint (HTTP 500 → 200).**

But the state we found is worth stating plainly, because it changes what the tester programme
has been measuring:

- `player_data`: **2 rows**, both test fixtures, newest **2026-05-31**. No player's progress had
  ever reached Neon.
- `bug_reports`: **0 rows**. Every report ever filed from Settings returned 500
  (`column "player_id" does not exist`).
- `auth_nonces` did not exist, so the wallet rail was unreachable regardless of the client.
- 1,039 `auth_failed` events on 08-02 alone.

**The `api/` code is deployed to PREVIEW only.** The game hardcodes the *production* domain, and
preview URLs are SSO-protected, so tonight's server work is **not yet reachable by the game**.
Promotion is yours:

```
vercel deploy --prod          # or promote from the dashboard
```

Verified safe: the new `load.js` still accepts `?playerId=`, which is exactly what the shipped
client sends — no build already in the wild breaks.

**Sequence after promoting:** the APK now on Firebase already contains the client half
(`CanCloudSync` + the `X-Guest-Id` header), so guests should save once production is live. Then
file one bug from Settings and check `/api/admin/db?view=bugreports` — that table has been
0 rows forever, so the first row appearing is unambiguous proof.

**Do NOT flip `BACKEND_AUTH_ENFORCED` yet.** `auth_nonces` exists now, but the wallet signing
path has never been proven end-to-end on a device.

---

## 5. DECISIONS WAITING ON YOU

1. **Promote `api/` to production** (§4) — the single highest-value action on the board.
2. **`lumberyard` in `FoundingKit`** — a stockpile is currently a founding freebie, which is in
   tension with your WO-837 ruling. Removing it can only *add* softlock risk, so it was left.
3. **Author `category` on ten legacy `weapons.json` rows** (`knight_starter`, `ranger_starter`,
   `cleric_starter`, all four `aegis_*`) — the armed-hero oracle then upgrades to a hard
   per-class weapon-kind assertion with no code change.
4. **Should shields drop at all?** Both exact-rarity weapon loops can still award an off-hand item
   as a main hand — the same shape as the unarmed-Mage bug. The one-line fix removes shields
   from the drop pool entirely, which is a design call.
5. **Killable enemy raid towers** — patch written and waiting; needs a felt-test, not a compile.
6. **Melee has no on-hit visual** for non-perfect swings now that Perfect Hit is a real timed
   input (you deleted the burst when the stamp was unconditional).

---

## 6. KNOWN-OPEN, FILED NOT FIXED

- **Adaptive difficulty is INERT.** The math is correct and oracle-proven (at-target lands on
  1.000 exactly, both rails reachable), but `WaveManager` records none of the six fields it needs,
  so every read returns 1.0. The telemetry lane was stopped mid-edit to gate; `Enemy.SetBaseStats`
  / `ApplyDifficulty` and the spawn hooks are in, the measurements are not.
- **A cancelled build has always burned the charge** — `CancelPlacing` refunds `TowerData.cost`
  crystals, which is 0 for DevTower. Pre-existing, but now costs ~70 wood + 40 iron.
- **Placement still spawns `Towers/DevTower`** regardless of which tower row you pick.
- **`Place()` charges after `loader.Spawn`**, so a declined charge leaves a structure standing.
  Now loud rather than silent; reordering is its own ticket.
- **A fourth copy of the shader predicate** lives in `HeroBodySwapper`, without the `isSupported`
  branch, on the runtime hero-body path that ships to Android.
- **Three more spawners never apply role tactics** — the dungeon group path is the worst
  (every dungeon mob runs with no tactics at all).
- **`GearLoadout.EquipArmorById` enforces nothing** — no class, weight or level gate.
- **Guests can't migrate to a wallet** — connecting after playing as guest orphans the guest row.

Full list with file:line: `OUTSTANDING_FOR_GROK_2026-08-02.md`.

---

## 7. NOT VERIFIED BY ME — needs your eyes

Everything above is gate-verified or data-verified. These are **felt** judgements no headless run
can make:

- The rescaled raids: whether 5.0 s worst-case time-to-death at Extreme reads as threatening or
  punishing, and whether the spire fight has the right length.
- Perfect Hit as a double-tap on a touchscreen — the 100 ms window is unvalidated by hand.
- Whether the raid troops actually *pose* (the wiring is proven; posing needs a rendered session).
- The hub foliage: whether 150 props at 74–185 m reads as fuller or as a distant treeline.
- The new glossary tabs — UI capture only shoots the default-selected tab, so those are unproven.
