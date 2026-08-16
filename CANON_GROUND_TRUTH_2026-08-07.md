> ## ⚠ SUPERSEDED 2026-08-16 — the live anchor is `CANON_GROUND_TRUTH_2026-08-16.md`. Frozen ledger.
> This was the only non-live anchor still claiming to be live (the "THE single live anchor" line
> immediately below). It was superseded by the 08-08 anchor the next day and never bannered.

# CANON GROUND TRUTH — 2026-08-07 (02:2x, overnight run)

> **~~THE single live anchor (CLAUDE.md §15).~~ (Was true on 2026-08-07 only — see the banner above.)**
> Supersedes `CANON_GROUND_TRUTH_2026-08-06.md`.
> Every session and agent checks docs against THIS file. Sourced from HEAD commits, the working
> tree, and captured device/EXE data — never assumption.

**Branch:** `wip/village2-and-f8-tickets` · **HEAD:** `2e6c4709` · **63+ commits ahead of origin,
NOTHING PUSHED** (push only on the owner's explicit word).
**Gates as of this file:** `COMPILE_GATE_OK` + **`REGRESSION_OK 125/125 suites`** (the two new ones
are `[dungeon-multilevel]` and `[dungeon-encounter-family]`, from WO-1001 slices 1 and 2).

---

## 0. What changed overnight (2026-08-06 evening → 2026-08-07 ~02:30)

Nine commits. In dependency order:

| Commit | What |
|---|---|
| `5ec4a983` | **Wallet connect root cause** — `runInBackground: 0` froze the Unity main thread mid-handshake |
| `3f54a8d2` | Wallet connected-state signal + label (`Wallet CHKK...sfkC`) |
| `d7e1844f` | Node aura → `Poi_NodeAura` (owner retag; the motes were invisible in daylight) |
| `7d79d3da` | First-build 5s grace + the bow parent-scale fix that never reached `HeroBowAttachment` |
| `088b9cda` | Casters carry nothing + a weapon scale sanity guard |
| `71dd6320` | **Arena stranding fix** — the arena owns the way home again |
| `a6c65090` | F8 harness un-blinded (throttled a per-frame logger) + tower bake made idempotent |
| `d1294d73` | Healer's Cottage encounter no longer fires on the spawn point |
| `287ac354` · `0414d44d` · `c2a9cfb4` · `21d166c9` · `0fe78780` · `4fab809f` · `2e6c4709` | skill tree, army muster, ad gate, Manage screen, town suspension + barracks, regression, docs |

---

## 1. THE HARD FACTS a new session must not get wrong

**The rewarded-ad path is GATED SHUT and must stay that way.** `RewardedAdManager.ShowAdInternal`
used to be `{ onReward?.Invoke(); }` — it granted the timer skip with **no ad and no SDK**, on all
three channels after the WO-911 work widened it. There is still NO ad SDK in the project.
`FeatureFlags.RewardedAdSkip` defaults **OFF**; the stub now returns `false` and never invokes the
reward. **Two hard prerequisites before it may EVER be switched on:** a real SDK granting only from
`OnUserEarnedReward`, and **WO-912 server-side window validation** — the ad window is stamped from
the DEVICE clock, so rolling the clock forward mints a fresh allowance, which behind a live SDK is
fabricated impressions and an account ban.

**The arena is IN-PLACE at (5000, 0, 5000) in the same scene — not a separate scene.** "Exit battle"
is a `WarpHero` teleport plus a stage teardown. Any fix that loads a scene is wrong. A hero position
of ~(5000, *, 5000) while `scene=Main_Castle_Overworld` means STILL IN THE ARENA.

**Save schema is v37.** `BuildJobData` gained `paid{Wood,Food,Iron,Crystals,Magic}` so a cancel
refunds exactly what was charged (owner ruling Q1, 100% flat). Pre-v37 jobs refund zero, traced.
The `EchoSpecializationRegression` pin was moved 36 → 37 in the same breath.

**The bottom bar is 6 faces, not 7.** `ActionBarButtonId.Upgrade` is RE-POINTED to the Manage/Queues
screen; Map moved into Bag as a tab and is **flag-gated OFF** (`ff.maptab`) because
`RealmMapPanel.cs:30` says travel is a DISABLED stub until WO-827 — the areas genuinely do not
connect. `ButtonCount` stays 7 (enum identity); a new `MaxVisibleFaces = 6` drives geometry.

**RESEARCH IS TIME-BASED, and the research LINE is global until player-owned bases** (owner ruling
2026-08-07). Building-perk research used to grant INSTANTLY for Gold; it now pays up front, runs on
the Research channel for a real duration, and applies on completion — the Warcraft 3 Blacksmith
model, which also makes it cancellable at the flat 100% refund (ruling Q1) and rushable with
crystals (the Clash half of the lens; WC3 has no rush).
- **The divergence from WC3 is deliberate and TEMPORARY.** In Warcraft the *building* is occupied,
  so a second Blacksmith buys parallel research. We run ONE global Research line with slots, which
  is the Clash single-Laboratory model and fits the Echo-gated crystal-priced extra slot already
  built.
- **Player-owned bases are where the lines FAN OUT** (owner, 2026-08-07). Each owned base runs its
  OWN queue lines, so three bases means three research lines — that is the payoff that makes owning
  a base worth the investment, and it is why the WC3 per-building model is deferred rather than
  rejected. Until bases ship, a second research building must NOT silently grant a second line.
- **THE SINGLETON IS TO THE BASE, not to the process** (owner ruling 2026-08-07). This is the whole
  architecture in one line: `BuildTimerService` / `ObsidianQueueState` is scoped to a BASE, so a
  player with three bases has three sets of Builder/Train/Research lines. Every line-count question
  answers itself from that — fan-out is not a feature to add later, it is what falls out of scoping
  the singleton correctly.
- **Extra queue slots are therefore PER BASE** (owner). Purchased slots already live on
  `ChannelState.boughtSlots` inside `ObsidianQueueState` (v35), so base-scoping the singleton makes
  the slot purchase per-base **for free** — no new save field. That also protects the crystal sink:
  an account-wide slot would get *diluted* as the player takes more bases (more free lines, less
  reason to buy), whereas a per-base slot scales WITH the empire.
- **Implementation constraint, cheap now and expensive later:** do not bake "one global Research
  channel" into anything that would have to be unpicked. Callers resolve a channel through
  `BuildTimerService` rather than assuming a process-wide singleton, and any new state keyed "the
  queue" is keyed "the queue OF a base". ⚠ `ObsidianQueueState` is currently ONE global instance on
  the save (`SaveSchema` v35) — sharding it per base is a future save bump, so **do not add fields
  to it that assume a single town**.

**Queue channels are ONLY Builder / Train / Research.** Upgrades ride Builder. The owner's CONTENT
tabs CROSS those channels: Defence AND Buildings share the ONE Builder rail. Weapons/armour have no
queue at all and are deliberately omitted.

**Dev chips are OFF by default everywhere**, including development builds. There were TWO — the
`ResourceDevTool` chip and `OwnerDevToolsOverlay`, and the latter had **no gate at all**, which is
why the first fix didn't remove it. **F8 capture is untouched** — only the on-screen chips are
hidden. Restore with `ff.devresourcetool=1` / `ff.flagbutton=1`.

**WO numbering: the UI seat is now on 1000–1099** (owner ruling). The old 860–899 block is CLOSED
(full at 899). The CLI main line is at **912** and MUST NEVER CROSS 1000. The previously recommended
"913+" would have collided.

---

## 2. Store readiness (Solana dApp Store, Path A)

- **KYB APPROVED** (Sumsub, portal confirmed).
- **Privacy policy LIVE and publicly readable**: `https://echoes-of-elarion.vercel.app/privacy`
  (fetched externally AND confirmed by the owner in a browser). Website:
  `https://echoes-of-elarion.vercel.app/`. ⚠ Vercel SSO turns itself on at project creation — re-check
  after ANY redeploy; a login wall there fails the listing while every internal check passes.
- **Listing copy RULED:** name `Echoes of Elarion` (17/30 chars — note the owner typed "Echos", a
  slip; every artefact spells it Echoes). Subtitle `They gave their souls to survive.`
- **Assets produced:** `Builds/StoreAssets/icon_FINAL_512.png`, `banner_FINAL_1200x600.png`, and
  `previews/` conformed to **1920×1080** (3 of 4 done; the missing one is build-mode/town — nothing
  in the set currently says "base-builder").
- **Payment path deferred until AFTER submit (owner ruling).** `Web3.Wallet` is ALWAYS null because
  Connect goes through `TargetedLocalAssociationScenario` (which is what forces the Seeker wallet),
  so `SendPayment` can never complete. There is also **no devnet SKR mint** (`SkrMintDevnet` is
  empty), and mainnet payments are hard-blocked at `SolanaWalletProvider.cs:429`.
- **WebGL is deprioritised** (owner: zero traffic; kept as an option, not a channel).

---

## 3. Known-broken / open, with evidence

| # | Item | State |
|---|---|---|
| 14 | Dungeon arena softlock | Healer's Cottage trigger fixed (`d1294d73`); the "cannot move" cause was the encounter firing ON the spawn point, warping the hero 7,084 m. **Retest needed.** |
| 17 | Town pause | Threat systems suspended. **Build timers NOT paused** — they run on wall-clock (`TimeSource.NowUnixMs`), so suspending ticks does nothing. Needs a queue time-model change. |
| 28 | Wave-clear banner clamp | Logged by the game as an error: `body rows COMPRESSED to fit ... every band is now below its own content size` |
| 31 | Barracks | Adopted at unlock (`0fe78780`). ~0.5 s window where the build card still reads BUILDABLE; closing it cleanly needs a `repo.townGranted` catalog opt-in |
| 33 | `Bow.fbx` | Imports a STATIC prop as a SkinnedMeshRenderer (`animationType: 2`) with frozen half-written bounds → the -33.56 m. Only TWO prefabs in the tree override renderer bounds: `Bow.prefab` and **`Boss_Dragon.prefab`** (4×, unreported) |
| 34 | Gear panel showed "Thrain Lv 1" while the HUD said "Grom Lv 3" | Unreproduced since the hero reset |
| 35 | Fire with no repair | INSTRUMENTED not guessed. One real fix: the repair *installer* checked 4 structure types while the backend prices **8** |

**Only `dg_starter_loop` is a proven-working dungeon end to end** (owner cleared it: loot, caches,
torch recipe unlock, first-clear recorded). `Dungeon_FolksGranary` is a STUB with no
`DungeonController`.

**WO-1001 slice 1 (multi-level descents) LANDED — read this before planning any dungeon work.**
- **Composed dungeons can now descend floors.** Before 2026-08-07 they could not, at all: both stair
  sockets pointed DOWN at local Y=0 (a pair scored `align = -1`), the mate nudge was planar-only, and
  a *correct* vertical stack was reported as an OVERLAP — a hard bake abort. All three are fixed.
  Proven by `dg_descent_probe`: `mate OK ... align=1.00`, `matesFail=0 saved=True`, two Y levels.
- **The WO's own §1 premise was WRONG** ("supports multi-level via StairDown/StairUp"). It is now
  banner-corrected in the WO. Do not re-plan against it.
- **⚠ PLACED, NOT WALKABLE.** `path[entry->deep_vault]=PathPartial`. The stair rooms are flat 6x6
  rooms with a socket marker — **no stair geometry, no floor cut, no NavMeshLink**. Floors are two
  disconnected navmesh islands. Slice 1b, task #37. **Do not seat a hero in a multi-level composed
  dungeon until then** — it would look enterable and dead-end.
- **The composed path has NO `DungeonController`, `Lantern`, `EncounterTrigger`, or chests.**
  `PopulateForPlay` seats a hero root + enemy spawner markers and nothing else; the oil/lantern
  pillar is hand-wired into the single Healer's Cottage scene. WO-1001 slices 3-6 therefore each
  need the composed path to gain a controller FIRST — a shared prerequisite the WO does not name.
- **WO-1001 slice 2 LANDED: `EncounterSpec.kind` now selects a real enemy family.** Kinds =
  `none | hollow-group | orc-group | troll-group | mixed`; ids come from `enemies.json`, weights stay
  a C# design table, no boss ids are reachable. An unknown kind falls back to hollow **and warns** —
  previously `DungeonBaker` compared `kind` only to `"none"`, so `"orc-group"` *silently spawned
  hollows*. `OutpostEnemyGroupSpawner` also stopped hand-writing stat blocks that ignored (and
  disagreed with) `enemies.json`; the catalog is now the single source, via the existing
  `CanonicalJson` + `EnemyCatalog` path.
  **⚠ FELT CHANGE PENDING A RULING (task #38): dungeon hollows got tougher** — hollow-rogue Hp
  **34 → 70**, walker 40 → 52, acolyte 60 → 90. The spawn STREAM is unchanged (proven bit-identical
  over 20k seeded rolls); only the stats moved. If it plays too hard, tune `enemies.json` — do **not**
  restore hardcoded numbers, that recreates the two-sources-of-truth bug.
  Roles deliberately still come from a design table, not `EnemyDef.RoleKind`: that maps
  hollow-rogue's `skirmisher` to `EnemyRole.Ranged`, a stand-off/bow posture that would change
  shipped melee behaviour. **Depth scaling is NOT implemented** — no depth/tier value is plumbed.
- Still true: `EncounterSpec.seatMode`, `confine.mode`, `confine.returnHome` are read by **zero** code.
- New suite `[dungeon-multilevel]` (5/5) pins all of it, including a case that reads the **shipped**
  stair prefabs — the room prefabs are GENERATED, so a builder edit is inert until
  `DefaultDungeonRoomsBuilder.BuildAll` re-runs.

---

## 4. Decisions waiting on the owner

> **Added overnight 2026-08-07 (fast ones first — each is a one-line change once ruled):**
>
> **A. Dungeon difficulty (task #38) — a felt playtest closes it.** Making `enemies.json`
> authoritative made dungeon hollows tougher: hollow-rogue Hp **34 → 70**, walker 40 → 52,
> acolyte 60 → 90. Spawn stream unchanged. If it plays too hard, tune the JSON — **never** put
> numbers back in C#, that was the bug.
>
> **B. App label vs store name (task #36) — needed BEFORE store submission.** The APK installs as
> **"Defenders of the Realm"** while the listing and the Firebase app both say **"Echoes of
> Elarion"**. ⚠ Not a simple rename: `productName` feeds `Application.persistentDataPath` on
> **desktop**, so changing it orphans every existing EXE save. Android is keyed on the package name
> and is unaffected. Needs either a save-path migration or a platform-scoped override.
>
> **C. Stair traversal model (task #37).** Multi-level dungeons now PLACE correctly but are not
> WALKABLE between floors. Before slice 1b is built: is descending a **walk-through staircase**
> (needs stair geometry + a floor cut + a NavMeshLink) or a **triggered transition** (cheaper, but
> reads as a loading seam)?

1. **Structure height ladder.** Normalization runs, but 20 of 29 structures have NO authored
   `heightMul` and sit on a flat 4.00 m. A **farm (5.60 m) is taller than every tower (4.80 m) and
   the Cathedral (4.00 m)**, which is level with a garden wall.
2. **WO-910** — hide, wire, or leave the 31 dead Ranger/Mage talent nodes. Ranger has **1** working
   talent of 20; Mage 5. The new progression line marks them INERT so the tree stops lying, which
   makes the imbalance visible rather than fixing it.
3. **`extraSlotBaseCrystals = 250`** — an agent had to invent this price; no WO set one.
4. **In-progress wave at dungeon entry** — suspend-and-resume (default) or cancel (kinder, but
   farmable: enter a dungeon to wipe a wave going badly). Both built; one line to switch.
5. **Q9/B8** — `RealmStorePurchase` + the "Coming soon" branch are untouched, so the broke-case route
   lands on a dead store. Two-line fix once cleared.

---

## 5. Monetization reality (from the SME review, verified at source)

- **The entire crystal sink is ~154 crystals** and a fresh save starts with **250**. A new player can
  instant-finish every timer in the catalog before buying anything. **Fix the SINK before the price.**
- **153 convenience tokens across 13 packs evaporate on grant** — zero handlers exist.
- **40 pack cosmetic SKUs → 25 exist as rows → 0 render anything.** A cosmetic is a hex colour swatch.
- **`skr_store.json` prices SKR 2.9× cheaper than `packs.json`** — a $19.99 Token Coffer buys the
  $49.99 Founder's Vow with change. Not live (no runtime loader) but authored and will activate.
- Pack pricing is back to REAL values (25/60/120/240/600 SKR); the 1-SKR solo-test override is gone.
