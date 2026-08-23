# WORK ORDER 1126 — Glimmer was retired in design years ago and is still wired into the money path

**Status:** FIXED 2026-08-23 (51de6bd31, Codex lane) — the glimmer purge landed: `Assets/_Modules/Cosmetics/GlimmerCurrencyService.cs` (329 lines) and `BattlePassManager.cs` (342 lines) DELETED with their metas, `CosmeticOwnershipService.cs` added, cosmetics/quests/daily-quests canonical JSON rewritten in both copies, wardrobe preserved (dotr-cosmetics-v1 untouched, save still v38). Only comment residue remains in the tree (`BattlePassService.cs:66`, `BuildTimerConfig.cs:170`). Prior status: "READY TO IMPLEMENT — ⚠ BLOCKED ON ONE OWNER RULING (§3)" — the work landed and the line never moved. ⚠ NOT PROVEN: no felt-test; and the same commit records that the purge WRONGLY DELETED two lore quests (petbond.glimmermoth, vendor.market) which had to be restored — re-verify quest counts (24 quests / 63 stages) when felt-testing. AWAITING OWNER FELT-TEST TO CLOSE.
**Minted:** 2026-08-19 (CLI seat) — banner bumped 1126 → 1127 in the SAME edit
**Lane:** Economy / monetization. Touches Wallet, Cosmetics, Quests, Enemies, HUD, and 11 data files.
**Priority:** HIGH — it sits on the PAYMENT path. Not urgent only because payments cannot complete today
(four blockers, `docs/MONETIZATION_STATE_2026-08-19.md`), which is the one piece of luck in this ticket.
**Provenance:** owner 2026-08-19, verbatim: *"we removed glimmer long time ago"*, *"we use crystals"*.
Found while wiring the LevelPlay ad units (WO-1125).

---

## 1. THE FINDING

Glimmer is a **fully-wired parallel economy**: earned from kills, spent on cosmetics, sold in packs, and
**granted by the crypto payment path** — while the owner considers it retired and crystals the live
currency. The retirement happened in design and never in the tree.

**The part that matters most:** `CryptoPaymentManager.GrantGlimmer` (`:235`) reflects into
`DeNelle.Cosmetics.GlimmerCurrencyService.TryAddGlimmer(int)`. So a settled purchase credits **Glimmer**,
not crystals. And that balance lives in **PlayerPrefs** — `GlimmerCurrencyService.PrefKey =
"dotr-cosmetics-v1"`, seeded `StartingGlimmer = 25` — i.e. **outside `GameState`**, so it is not in the
signed save and does not sync to Neon.

A paid entitlement would therefore land in a **local, unsynced, retired currency**. The file already
names the failure mode for the case where the service is absent:

> "payment CONFIRMED (tx: …) but the {n}-Glimmer grant did NOT take — **PLAYER CHARGED, ENTITLEMENT
> LOST**. Needs reconciliation." (`CryptoPaymentManager.cs:215-217`)

This is a concrete instance of the open question the monetization audit flagged as an owner decision:
**what IS an entitlement, and where does it live?** That ruling is the schema.

## 1b. HOW OLD THIS IS, AND WHY THAT CHANGES THE MIGRATION

**The earn path has been live since 2026-05-27** — commit `28ebec0c3`, *"Gameplay systems + fixes: heart
health event, glimmer-on-kill, portal perf, doors, VFX"* (DEF-32). Owner, 2026-08-19: *"def 32 is very
old"*. Nearly three months.

That is not trivia. It means **real balances exist**: every kill since May has been paying glimmer into
`dotr-cosmetics-v1`, and anything bought with it is owned against that wallet. A purge is therefore a
**migration with real player state on the other side**, not a find-and-replace — and the state lives in
PlayerPrefs, outside the signed save, so it is the one balance a save-version bump cannot carry.

It also explains the shape of the drift: glimmer was not left half-built and abandoned. It was finished,
shipped, and then superseded in design without anything removing it. Nothing in the tree ever heard.

## 2. WHY IT SURVIVED

Nothing was wrong enough to fail. `GlimmerEconomyRegression` is REGISTERED (`DataRegression.cs:350`) and
**passes** — it asserts the glimmer economy is internally consistent, which it is. Consistency with a
retired design is not something a suite can notice. Same for `PackGrantRegression` (`:427`) and
`PackCosmeticIntegrityRegression` (`:461`).

So six suites actively pin glimmer in place. **Any migration must retire the suites in the same change,
or the tree will defend the currency you are trying to remove.**

## 3. ⛔ THE RULING — pick ONE. Everything below depends on it.

**Option A — PURGE. Glimmer becomes crystals everywhere.** One premium currency, matching stated intent.
Largest change, and it needs a **save/PlayerPrefs migration**: existing players hold a glimmer balance in
`dotr-cosmetics-v1` plus owned cosmetics keyed to it. Conversion rate is an owner call
(1:1? by relative price?). Cosmetic ownership must survive regardless — a player who bought an outfit
keeps it, whatever the wallet does.

**Option B — KEEP glimmer as a cosmetics-only wallet, fix only the payment grant** so real money credits
crystals. Much smaller and immediately safe on the money path, but leaves two currencies alive and this
ticket permanently half-open.

**Option C — KEEP as-is and make it deliberate.** If glimmer earned-from-kills → spent-on-cosmetics is
actually a design you want, the fix is documentation, not code: record that it is live and intentional,
and correct the payment grant only if crystals should be what money buys.

⚠ **A and B are not reversible in the same way.** A purge changes player balances; a grant fix does not.
Do A only if the answer is "one currency", and expect the migration to be the real work.

## 4. THE FULL INVENTORY (measured 2026-08-19, `grep -ri glimmer`)

### 4.1 The money path — fix in EVERY option
| file | what it does |
|---|---|
| `Assets/_Modules/Wallet/CryptoPaymentManager.cs` (41 hits) | `GrantGlimmer` at `:235`, reflection bridge, the CHARGED-BUT-LOST failure at `:215` |
| `Assets/_Modules/Wallet/PackStore.cs`, `PackCatalog.cs`, `PackStoreVM.cs` (25), `ShortfallPackOffer.cs` | pack pricing/grants in glimmer |
| `Assets/_Modules/Wallet/Tests/PackStoreVMTests.cs` | pins the above |

### 4.2 The currency service and its consumers
`Assets/_Modules/Cosmetics/GlimmerCurrencyService.cs` (**70 hits — the owner of the balance**),
`BattlePassManager.cs` (19), `CosmeticCatalog.cs`, `CosmeticApplier.cs`,
`Assets/_Modules/HUD/CosmeticShopPanel.cs` (29), `Core/UI/ShopTheme.cs`, `Core/UI/PanelRouter.cs`

### 4.3 Earn paths — glimmer drops from gameplay
`Village/Enemies/Enemy.cs` (23 — `GlimmerReward` granted on kill, DEF-32),
`WildlandsRoster.cs` (`GlimmerReward = 3`), `TribeManager.cs` (`GlimmerReward = 3`),
`EnemyFamilyTestSpawner.cs`, `OutpostEnemyGroupSpawner.cs`, `OverworldEncounterSpawner.cs`,
`FamilyTestSpawner.cs`, `RegionMobSpawner.cs`, `Waves/WaveData.cs`, `WaveFeedbackDirector.cs`,
`World/Camps/{CampDefenseWave,CampGuards,EnemyOutpost,GarrisonStatBlocks}.cs`,
`World/WardTetherService.cs`, `Village/Arena/BattleArena.cs`, `Village/Progression/TierSystem.cs` (19)

### 4.4 Quests / tutorial / pets
`Core/Quests/DailyQuests.cs`, `Village/Quests/DailyQuestRewardBridge.cs` (15),
`Village/Buildings/DailyQuestTowerBridge.cs`, `HUD/DailyQuestHud.cs`, `HUD/DailyQuestVM.cs`,
`Village/Tutorial/{DialogueCommandSink,TutorialWaveSpawner,V2/TutorialFlow}.cs`,
`Pets/{PetDeployer,PetEmoteController,MineNodeBridge}.cs`, `Core/Platform/StakeRewardsResolver.cs`

**Total: 48 `.cs` files.**

### 4.5 Shipped data — ⚠ DUAL COPIES, Resources WINS at load; keep them byte-identical
| file | hits |
|---|---|
| `Resources/Data/Canonical/cosmetics.json` + StreamingAssets twin | 38 each (`glimmerCost` on every item) |
| `Resources/Data/Canonical/packs.json` + twin | 15 each (**glimmer is SOLD**: `glimmer=300`, `glimmer=400`, a bundle with `glimmer: 1000, crystals: 15000`) |
| `Resources/Data/Canonical/daily-quests.json` + twin | 11 each |
| `Resources/Data/Canonical/ad-placements.json` | 4 (`reward.glimmer.trickle`, 15 glimmer — **an ORPHAN reward: no placement references it**) |
| `Resources/Data/Canonical/quests.json` + twin | 3 each |
| `StreamingAssets/.../battle_monthly_packs.sample.json` | 25 |
| `StreamingAssets/.../skr_store.json` | 3 |

**11 data files.**

### 4.6 The six suites that will fight the migration
`GlimmerEconomyRegression.cs` (35, registered `DataRegression.cs:350`),
`PackGrantRegression.cs` (23, `:427`), `PackCosmeticIntegrityRegression.cs` (10, `:461`),
`ImpulsePackRegression.cs`, `EconomyMetaCatalogRegression.cs`, `DataRegression.cs` itself.

## 5. ONE THING TO CHECK BEFORE ANY OPTION

`reward.glimmer.trickle` in `ad-placements.json` grants 15 glimmer. It is currently an **orphan** — no
placement points at it — so it is inert. But `_LAW_1_NO_PREMIUM_CURRENCY` bans an ad reward paying "any
currency bought with real money", and **glimmer IS sold in `packs.json` today**. If the covenant guard's
premium-currency list does not include glimmer, wiring any placement to that reward ships a policy
violation that the guard would pass. **Delete the orphan reward as part of this ticket regardless of
which option is chosen** — it costs nothing and removes a loaded gun.

## 6. ACCEPTANCE

1. The ruling in §3 is recorded in this file before any code moves.
2. Whatever the option: **a real-money purchase credits the currency the owner says it should**, proven
   by a captured line showing the balance moving — not by the absence of an error.
3. Cosmetic OWNERSHIP survives any migration. A player who bought an outfit still has it.
4. Both catalog copies byte-identical (Resources wins at load).
5. The six suites updated in the SAME change, and each still able to FAIL. A suite that passes because
   it no longer asserts anything is worse than a deleted one.
6. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` (read the count off the marker).

## 7. WHAT NOT TO DO

- Do **not** start before the §3 ruling. This touches player balances.
- Do **not** sweep `grep -ri glimmer` blindly. `GlimmerReward` on an enemy is an EARN path; `glimmerCost`
  on a cosmetic is a SPEND path; `GrantGlimmer` in the wallet is a PAID path. They have different
  correct answers, and only the paid path is unambiguously wrong today.
- Do **not** delete `GlimmerCurrencyService` without a PlayerPrefs migration — existing players hold a
  balance in `dotr-cosmetics-v1` that is not in the signed save and will not come back.
- Do **not** leave the suites passing on a half-migrated tree.

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `CryptoPaymentManager.cs:209; GlimmerCurrencyService.cs` — purge needs owner ruling. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.


---

# ★★ OWNER RULING 2026-08-21 - **PURGE GLIMMER. FULL REMOVAL.**

Owner verbatim: *"purge glimmer"*. This unblocks §3 and closes the ticket's one open ruling.
Consistent with the same day's stripping of glimmer from every pack (*"nothing real and money has
never been active"*) and with the standing canon *"we removed glimmer long time ago, we use crystals"*.

## AND: **RETIRE `BattlePassManager`** (same ruling breath: *"retire battlepassmanager"*)
This SUPERSEDES the earlier KEEP/dormant ruling. The KEEP was granted because
`WORK_ORDER_battle_and_monthly_packs` was thought to depend on it; that ticket has since been
implemented on a NEW runtime (`BattlePassService` + `MonthlyCardService`), so the dependency that
justified keeping it no longer exists. It is now a SECOND battle-pass runtime whose premium purchase
costs **2400 Glimmer** - i.e. it is part of the glimmer surface being purged, not a bystander.
⚠ **Lift its `LevelUpVFX` bridge before deleting** - that bridge is the one live thing it owns.

## HOW MUCH CARE THIS ACTUALLY NEEDS - OWNER CORRECTION 2026-08-21

Owner verbatim: **"there are no live games as pay path has never been activated"**.

⚠ An earlier draft of this section (written minutes before, and WRONG) called this a "live-game
constraint" and demanded a careful balance-preserving MIGRATION with an owner ruling on what happens
to banked glimmer. **That caution was misplaced and is retired.** The app is published on the Solana
dApp Store, but **the payment path was never activated - nobody has ever bought anything, so no
player holds a purchased glimmer balance.** There is no real value to protect and no owner ruling
owed on conversion rates.

**So this is a CLEAN PURGE, not a migration:**
- Delete the glimmer surface outright - service, currency kind, pack pricing, cosmetic prices, quest
  and kill rewards, HUD readouts, the 11 data files.
- **Still read-migrate the save field so an existing dev/test save LOADS** rather than throwing on an
  unknown field. That is ordinary defensive deserialisation, not balance preservation - an absent or
  present `glimmer` field must simply be ignored.
- **No schema bump.** v38 stands (`SaveSchema.CurrentVersion`).
- No conversion to crystals, no grandfathering, no compensation flow. There is nothing to compensate.

**The general lesson, recorded so it does not recur:** "published on a store" and "taking money" are
DIFFERENT facts, and this repo's canon says the first loudly while the second was never true. Check
which one actually applies before pricing a change's risk.
