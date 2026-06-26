# V1 ASSEMBLY MAP — farm → build → level → raid → repeat (Pi descope)
### Built from a 6-way code read 2026-06-26. The plan, grounded in the real code.

## THE HEADLINE
The V1 loop is **~80% BUILT but SCATTERED and HIDDEN behind contradictory feature flags.** The work is
**ASSEMBLE + DECLUTTER + STABILIZE — not build-new.** And the single most elegant truth the read surfaced:
**cutting the arena is the same move that UNBLOCKS your V1 raid loop** (the arena flag actively suppresses
the clean outpost-raid). The cut isn't just descoping; it's the enabler.

## WHERE EACH PILLAR LANDS
| Pillar | State | Key gaps (stabilization, not invention) |
|---|---|---|
| **Farm offline** | ✅ Engine BUILT + persisted (10h accrual clock, save-wired) | Workforce is a 1-capsule click-stub (not the drag-drop/cap-5 vision); worker assignments don't persist; **Wood/Iron offline routing bug** (offline wood/iron lands in a non-persisted pool, lost on reload + invisible to upgrades) |
| **Build + upgrade** | ✅ Structures, build mode, WO-432 tech-tree all BUILT | **(CRITICAL) `VillageTierService.TryUpgrade` has ZERO callers → village tier stuck at 0 → EVERY upgrade/perk permanently locked**; base ships **EMPTY of defenses** (build-mode-gated); **duplicate tower systems** (DefenseTower vs legacy Tower); no tower cap (WO-514) |
| **Level up** | ✅ ~85% BUILT + loop-connected (XP→level→Wisdom+gear; raids scale to hero level) | Pick the V1 fight (raids already pay better than the arena); felt-tune; clarify the two point-currencies in the UI |
| **Raid** | ✅ Cleanest loop (walk-to **EnemyOutpost**) is reward-wired into farm/build/level | **It's suppressed by the arena flag** (`OverworldEncounter` ON) → cutting the arena unblocks it. Then: consolidate 3 overlapping raid systems → 1; fix the "next companion" reward mismatch (companions were junked); the raid button is a no-op today |
| **Persistence + scene** | ✅ SOLID — durable single-save, offline-aware, covers the whole loop | **Pick ONE base scene** (Village2 has the build/farm loop, but you *land* in MainCastle_Hall); pick ONE raid path; flag off the arena trio; strip out-of-scope (dungeons/ATB/garrison-teleport/cloud-sync/diagnostics) |
| **Pi / wallet** | 🟡 C# surface CONTAINED (PiWalletProvider + CurrencyKind.Pi + packs + auth gate) | **The real new work isn't the economy layer:** (1) a **WebGL↔Pi-JS `.jslib` bridge** (doesn't exist), (2) a **backend** — Pi payments are server-approved/two-phase and **the game is 100% client-side** (architectural gap), (3) the **mobile-webview performance gate** (147MB build in Pi Browser on a phone). Longest, riskiest pole. |

## THE HIGH-LEVERAGE UNLOCK FIXES (small effort, unlock the whole loop)
1. **Wire `VillageTierService.TryUpgrade`** to a Heart/HUD affordance — ONE gate currently freezes ALL upgrades. Highest leverage fix in the project.
2. **Seed a default starter base** (a few towers + wall ring + gates at the lanes) into the shipping scene's `BaseLayout` so it defends out-of-the-box; Build Mode becomes the *editor*, not the only source.
3. **Flag OFF the arena trio** (`overworldencounter`, `arena`, `battlehud9zone`) → this **cuts the arena AND unblocks the EnemyOutpost raid** in one move.
4. **Pick ONE of each** (decisions below) — base scene, raid path, tower system — and fence off the duplicates.
5. **Fix the Wood/Iron offline routing** (route offline grants through `GrantSpendable` / unify the two stores).
6. **Reward path:** ship the EnemyOutpost raid (already pays XP+wood/iron+crystals+gear correctly) and ignore the companion-reward raids.

## DECISIONS NEEDED (architect recommendations in **bold**)
- **Base scene:** **Village2** (it holds the actual farm/build/tower-defense loop) — route the boot there, OR bring the loop to MainCastle_Hall. *Rec: make Village2 the canonical base; it's where the loop lives.*
- **Raid path:** **walk-to-outpost (`EnemyOutpost`, `ff.raidwalk` ON)** — the north-star, reward-wired, no teleport/troops, unblocked by the arena cut. Retire the teleport/deploy path.
- **Tower system:** **System A (DefenseTower + Build Mode + BaseLayout)** — modern, multi-type, BaseLayout-persisted. Fence off legacy `TowerPlacementSystem`/`Tower.cs`.
- **"Build defenses" scope:** confirm this = the **live Village2 build/upgrade loop (KEEP, `BuildingUpgradePanel` ON)** — which is SEPARATE from the gated `ff.basebuilding` CoC-convert layer (leave OFF). They're different systems.
- **Pi for the 28th: HARD-GATE or FAST-FOLLOW?** *Rec: fast-follow* (see timeline).

## THE HONEST Pi2Day (June 28 — 2 days) CALL
- ✅ **Achievable by the 28th (focused work):** a **stabilized, descoped, PLAYABLE V1** — the assembled loop (farm→build→level→raid), arena cut, base seeded, village-tier gate wired, one raid path, persistence verified, fleet-clean. Live on web/itch. **THAT is your Pi2Day proof + "look what a PM shipped with AI."**
- ⚠️ **NOT realistic, polished, by the 28th:** the full **Pi payment integration** — the JS bridge + a payment backend + the mobile-webview gate + KYC + testnet→mainnet. That's the longer pole.
- **The smart split:** ship the **stabilized playable V1 on the 28th** (the genuine Pi2Day moment + the proof asset), and treat **Pi payment as the immediate fast-follow** (testnet proof first, mainnet after). Do NOT rush a half-working Pi payment + a 147MB game that may not run on a phone into the 28th — that ships something broken at the worst moment.
- **De-risk the Pi unknown cheaply, in parallel, now:** host the current build + open it in the Pi Browser on a phone. That ONE test tells us if a Unity WebGL game is even viable in Pi's webview — before any integration time. If it can't run on a phone, the entire Pi-payment effort is moot and we know it for ~free.

## SEQUENCED PATH
**Track 1 — Stabilize the playable V1 (the 28th target):**
1. Flag cut-list (arena trio off, pick base/raid/tower, strip out-of-scope).
2. Wire the village-tier gate (unlock upgrades).
3. Seed the default starter base.
4. Consolidate raids → the one EnemyOutpost loop; verify reward round-trip.
5. Fix Wood/Iron offline routing.
6. Felt-pass + fleet verify the full loop; build → web/itch.

**Track 2 — Pi (parallel, but fast-follow on payment):**
- Now: the mobile-webview gate test (cheap, decides viability).
- After/if viable: the `.jslib` bridge + Pi auth gate + the minimal approve/complete backend + one-pack-in-Pi proof → testnet → mainnet + KYC.

## THE ONE-LINE FRAME
**Most of the game already exists; it's been hiding behind flags. Descope by flagging, unlock with the
village-tier gate + a seeded base, consolidate the raid, stabilize → a real playable V1 by the 28th. Pi
payment is a fast-follow gated on a cheap "does it even run on a phone" test.**
