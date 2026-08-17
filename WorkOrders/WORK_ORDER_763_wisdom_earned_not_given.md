<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-24
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-24) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 763 — Wisdom is EARNED (level-up-gated), not sprayed by combat

**Status:** SPEC — READY (owner-requested 2026-07-25, live balance). EXISTING issue (tuning), CLI-implementable.
**Lane:** Economy / Progression tuning. Scope: **SMALL** (isolated constants + a couple of grant-site redirects; no architecture change).
**Owner intent (verbatim):** *"every kill people are getting lots of wisdom. Wisdom should be rare, or given at level up so it takes real time and strategy to get new skills, make them feel earned not given."*

---

## 0. RCA (code-verified 2026-07-25, read-only agent — NO edits yet)

**Headline: kills grant ZERO Wisdom.** A kill grants only **XP** (`Enemy.cs:2327` → `ProgressionManager.ReportKill` → `ProgressionManager.cs:141 Grant(xp)`). The "every kill → lots of wisdom" *feel* comes from two leaks stacking:

- **Flat +2 Wisdom on EVERY wave cleared** — `WaveFeedbackDirector.cs:110` `const int wisdomPerWave = 2;`, granted `:112`. Fires constantly → reads as kill-linked. **The biggest leak.**
- **Cheap early XP curve** — `HeroProgression.cs:104 XpToNextFor` makes L1→L2 = 150 XP, so level-ups (and their Wisdom) arrive after very few kills. XP tuning at `ProgressionManager.cs:36-42, 110-112`.

Wisdom is **minted on hero level-up** already (the intended gate): `HeroProgression.cs:251 Grant(WisdomForLevel(newLevel))`, amount `:115-118` = **2 (L≤8) / 3 (L>8)**. Secondary Wisdom sources: tier milestones (`TierSystem.cs:186 BonusWisdom`, level-gated), arena wins (`BattleArena.cs:2088-2092`), daily quests (`DailyQuestRewardBridge.cs:137-139 RewardWisdom`).

Wisdom is the **skill/magic-unlock currency** — spent in `WisdomCurrencyService.Unlock` (`:108-124`) via the talent/skill tree (`TalentTreePanel.cs:333`, `HeroSkillTreeVM.cs:191,312`); node costs 1/2/3 from `hero-talents.json` (`kind:"skill"` nodes carry `unlockAbility`). Wallet = `WisdomCurrencyService.cs` (PlayerPrefs `dotr-talents-v1`), not a GameState field.

## 1. The change — make Wisdom a level-up reward, exclusively

Goal: Wisdom comes from **leveling up (+ level-gated tier milestones)** and nothing else, so new skills/magic feel earned over real time.

1. **Remove the per-wave Wisdom leak.** `WaveFeedbackDirector.cs:110/:112` — set `wisdomPerWave = 0` (or delete the grant). This is the single biggest "constant Wisdom" source. *(Wave-clear can still reward gold/other feedback — just not Wisdom.)*
2. **Redirect daily-quest Wisdom → another wallet.** `DailyQuestRewardBridge.cs:137-139` — pay `RewardGold`/crystals instead of `RewardWisdom` (or zero the Wisdom field in `daily-quests.json`). Keeps dailies rewarding without making Wisdom cheap.
3. **Arena win Wisdom → owner call (default: keep as a RARE bonus, or redirect).** `BattleArena.cs:2088-2092`. Arena is infrequent + skill-based, so a small Wisdom bonus there is defensible as "earned." DEFAULT: keep it (rare enough), but redirect to crystals if owner wants Wisdom strictly level-up-only.
4. **Keep the level-up grant as THE source** — `HeroProgression.cs:251` + tier milestones. Optionally tune `WisdomForLevel` (`:115-118`, currently 2/3) if even level grants feel generous once the leaks are gone — but likely leave as-is and re-feel first.
5. **Do NOT touch the XP curve** — steepening `XpToNextFor`/`ProgressionManager` XP would slow ALL progression (hero power, unlocks), not just Wisdom. Wisdom pacing rides the level-up grant, not the XP economy.

## 2. Strictness — STRICT chosen (owner-confirmed 2026-07-25) + IMPLEMENTED

Owner: *"i thought i was getting wisdom on exit of win … from battle arena."* Data confirmed the arena grant was the BIG leak, not a bystander: `BattleArena.GrantWinReward:2088` paid `(1 + family/2 + threat/2) * starMult` = **~3–8 Wisdom PER WIN, re-payable** (star-scaled). A whole hero tree needs ~71 and L20 leveling gives ~50 — so a few arena wins minted skill points faster than an entire playthrough of leveling. That IS the "lots of wisdom on exit of win."

**STRICT chosen:** Wisdom is minted at **level-up + level-gated tier milestones ONLY**. All combat (kills / waves / arena wins) still earns Wisdom **INDIRECTLY** via XP → level-up (the one gate). Implemented 2026-07-25:
- **Per-wave** — `WaveFeedbackDirector`: hoisted `public const int WisdomPerWave = 0`; grant guarded `if (WisdomPerWave > 0)` → no per-wave Wisdom.
- **Arena win** — `BattleArena.GrantWinReward:2086`: direct Wisdom grant REMOVED (`summary.Wisdom = 0`). Arena STILL pays its generous XP (`20 + 8·family + 4·threat`), wood/iron, and gear drops — so a win still feels rewarding and still earns Wisdom via the levels that XP buys.
- **Daily quest** — `DailyQuestRewardBridge:137`: `RewardWisdom` redirected → `AddCrystals` (daily value preserved, off the skill economy).
- **Level-up** — `HeroProgression.WisdomForLevel` (2/3 curve) UNCHANGED = the gate. Tier milestones (`TierSystem`, level-gated) unchanged.
- **Regression** — `HeroProgressionRegression` block (2b) asserts `WaveFeedbackDirector.WisdomPerWave == 0` (permanent guard against re-adding the leak); existing block (2) still locks the level-up curve (budget 50 @ L20).
- **XP curve UNTOUCHED** (steepening it would slow all progression, not just Wisdom).

## 3. Acceptance criteria
- [ ] No Wisdom is granted on wave-clear (`wisdomPerWave` grant removed/zeroed).
- [ ] Daily-quest reward pays a non-Wisdom currency (or 0 Wisdom).
- [ ] Wisdom is granted on hero level-up (+ tier milestones) — unchanged/verified.
- [ ] Arena: DEFAULT keeps a small Wisdom bonus; STRICT redirects it (per owner pick).
- [ ] Skill/magic unlock still spends Wisdom normally (`Unlock` path untouched).
- [ ] XP curve unchanged (progression pacing not altered).
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK`; a regression asserts wave-clear grants 0 Wisdom and level-up grants `WisdomForLevel`.
- [ ] PO felt-verifies: Wisdom now accrues at level-up cadence, new skills feel earned.

## 4. Notes
- SMALL, isolated tuning — all cited lines. Fold into the next test build after owner picks DEFAULT/STRICT.
- Data source: read-only RCA 2026-07-25 (all file:line cited above), per §12 (no guess — grant sites captured before touching code).
