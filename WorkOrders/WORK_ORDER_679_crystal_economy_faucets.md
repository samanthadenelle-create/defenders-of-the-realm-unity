> ## RECONCILED 2026-08-08 - true status is NOT STARTED
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: no streak/faucet code exists; grep DailyStreak/StreakModal returns 0 files.
> The previous Status line read "Status: SPEC - needs owner pin sign-off, then READY" and was wrong; the board overstated this.

# WORK ORDER 679 — Crystal economy: earn loop, login streaks, purchasable packs

> **STALE: 2026-08-04 (WO-856).** The faucet table below is wrong on its first row. It calls
> `CrystalMine` *"LIVE — passive accrual, the only steady faucet"*. **It was not live and never had
> been.** The payout was gated behind `_currentLevel == MaxLevel` on a private field that persisted
> nowhere and had no external writer, and `mine_crystal` authored no `maxLevel`/`upgradeCost`, so the
> BuildMode Upgrade verb answered *"Max tier reached."* on a freshly-built mine. The mine had never
> paid a single crystal. **WO-856 fixed it:** the level is now read from the persisted
> `PlacedStructure.level`, and the yield is an authored curve (`buildings.json` `crystal-mine`
> `crystalsPerWave: [2, 4, 7]`, ~18/36/63 per hour of active wave-fighting). Read that row as
> *"LIVE as of 2026-08-04"*. The rest of this WO (login streak, crystal packs, the dead-end daily
> quests) is unaffected and still open. Body frozen per CLAUDE.md §15 — not rewritten.

**Status: NOT STARTED** (owner ask 2026-07-12, from the live
Forge Enhancements preview: "we pay in crystals — how do we earn crystals? Could crystals be
earned through consecutive login days / additional purchases?").
**Classification (pipeline §13): NEW FEATURE** (login streak + crystal packs) **+ wiring gaps**
(existing faucets dead-ended). **Lane:** Economy / Meta. **Silo:** monetization/backend-adjacent
(§9 isolated lane).

## The gap (verified from code 2026-07-12)

Crystals are the SINK for the whole upgrade arc (tier unlocks e.g. Village Tier 500c, research,
WO-672 Repair All, tower charges when `repo.cost` is unauthored) — but the FAUCETS are thin:

| Source | State |
|---|---|
| `CrystalMine` (structure) | LIVE — passive accrual, the only steady faucet |
| Story quest beat rewards (`QuestService` → QuestRewardBridge) | LIVE — finite |
| Daily quests (`DailyQuests.RewardCrystals` authored) | **DEAD-END** — `DailyQuestHud` is display-only, NO claim/dispense flow (MASTER_CATALOG P2 #16) |
| Promo codes (crystals+coins) | STUB — backend never deployed |
| Referral rewards (crystals) | STUB — backend never deployed |
| SKR staking "Crystal Dust" +10/day | Defined in `StakeRewardsResolver` — crypto-build only |
| Login streak | **DOES NOT EXIST** |
| Purchasable crystal packs | **DOES NOT EXIST** (packs.json sells packs, no crystal SKU) |

## Design (answering the owner's two questions)

### A. Consecutive login days → crystal streak (NEW)
- **7-day repeating track**, escalating: d1 25c → d2 35 → d3 50 → d4 65 → d5 80 → d6 100 →
  **d7 150 + a small bonus chest** (values = first pass, tune vs upgrade costs; Ignite the Forge
  is 700W/450F — crystals stay the premium-feel currency, so a week ≈ one mid-tier unlock).
- **Mobile-kind rules:** a missed day RESETS to d1 **but** one grace day per cycle (canon
  kindness pattern — an interruption must not feel punitive); day rolls at local midnight;
  offline-first (PlayerPrefs/GameState field + additive save-schema bump), server-verifiable
  later when the backend deploys (anti-clock-cheat = server timestamp when available, trust
  local until then — same resilient pattern as GameStateService delta-sync).
- **Surface:** claim on first hub entry of the day — a small Obsidian modal (master-frame
  factory, one CLAIM action, streak pips d1-d7; colorblind law: claimed = check stamp, today =
  gold rim + label, missed = dim). Auto-dismiss after claim; never a wall.
- **Reuse:** dispense through the SAME grant path as quest rewards (QuestRewardBridge /
  `GrantSpendable`-style dual-write — the Wood/Iron dual-wallet hazard says never write one
  store only).

### B. Purchasable crystals (NEW SKUs on the BUILT store)
- Add crystal SKUs to `packs.json` + PackStore (the ~70%-built stack — reconcile, don't
  greenfield): e.g. Pouch 500c / Chest 1,200c / Vault 3,000c.
- **Per-channel rails (standing canon):** store build = native IAP; web = Stripe; crypto builds
  = wallet/SKR. All behind the existing provider abstraction; server-side verification required
  before grant (backend WO-107 dependency for real money — until then TESTNET/preview-gated,
  like `ff.skrpreview`).
- **⚠ P2W tension — named, not hidden (ARCH lens: easy-vs-right):** canon is "sell flex, not
  power," but crystals buy UPGRADE PROGRESSION. This is the CoC time-skip convenience model —
  genre-acceptable, but it's a real line: keep every upgrade EARNABLE at a reasonable rate
  (faucets A+C are the guarantee), no purchase-exclusive power, and cap purchase impact vs the
  wave/arena competitive surfaces. Owner pin #1 ratifies this stance.

### C. Wire the dead faucets (cheapest wins, do FIRST)
1. **Daily-quest claim flow** — the data + display exist; add the claim verb + dispense through
   QuestRewardBridge. Also fix the stale `FeatureShipped` gate filtering out quest templates for
   features that DO exist (MASTER_CATALOG #16).
2. **Wave/raid crystal drops** — small crystal component in deeper wave rewards (ties the
   press-your-luck DEFEND loop to the premium sink; data-only via the reward tables).
3. Promo/referral stay backend-gated (no change until WO-107 deploys).

## Acceptance
- [ ] Streak modal appears once per day on hub entry, claims correctly, survives reload +
      offline days per the rules; schema bump additive + migrator default (v-next precedent).
- [ ] Crystal grants land in `GameState.Resources.Crystals` (the canonical store — never the
      deprecated AetherCrystals) and both wallet reads agree.
- [ ] Daily quests claimable; rewards dispense once (no double-claim across reload).
- [ ] Crystal SKUs purchasable on the preview/testnet rail end-to-end; store build compiles
      crypto OUT unchanged.
- [ ] Fleet probe: simulate 9 login days (incl. one miss + grace) headless → assert streak
      state + total crystals granted match the table. DataRegression on new pack/streak rows.
- [ ] `COMPILE_GATE_OK` + owner felt-pass on the streak modal feel (PO closes).

## Owner pins (answer before READY)
1. **Ratify the P2W stance** (§B) — crystals purchasable as time-skip convenience, everything
   earnable: yes/no/tune?
2. Streak values + reset/grace rules (7-day repeating vs 30-day calendar?).
3. Crystal SKU price points + which channels launch first (testnet-only until backend?).
4. Should the streak chest ever contain non-crystal items (gear/cosmetic = more "flex, not
   power" aligned)?

## What NOT to touch
- Crystal SINK prices (upgrade/repair costs) — tuning them is a different WO; faucets first.
- PackStore scene-wiring (still disabled pending its own PanelSettings — standing note).
- No backend deploy in this WO; everything must degrade gracefully offline (resilient-stub
  pattern, no silent failures).

*Cross-refs:* `docs/NORTH_STAR.md` (monetization guardrails: sell flex not power; rewarded-ads
matrix) · `docs/MONETIZATION_REVIEW_2026-07-02.md` (loot boxes NO-GO mainnet / GO testnet) ·
MASTER_CATALOG P2 #16 (daily-quest gap) · `PromoCodeService`/`ReferralService` (backend-gated
faucets) · `StakeRewardsResolver` (SKR trickle) · WO-107 (backend) · WO-672 (crystal repair sink).
