# WORK ORDER 822 — Barracks teach v2 (813b): discovery beats, not toasts

**Status: READY TO IMPLEMENT**
**Source:** WO-813 shipped only the safety half (intro toast + empty-army redirect); PM review
2026-08-01 verdict: "Toasts are not teach. Players will still miss the drillmaster if they never
walk the right pad." This WO is the full discovery loop 813 specced but did not land.
**Silo:** Onboarding/NPCs/Quests

## Scope (in order)
1. **Coach beat -> world marker.** Post-Onboarded (and barracks unlocked), a one-time Sylas/coach
   line points at the Barracks; place a world marker/compass ping on the barracks pad or the
   drillmaster (reuse the existing objective-marker infra if present; else the HudBuildingFocus seam).
2. **Soft quest: "Train 3 Footmen".** Reuse the existing quest/counter infra (DailyQuests/Quest
   catalog pattern) — one authored quest row, counter driven off BarracksService.Changed /
   TrainTroopEffect completions. Reward small (gold or wood), owner tunes.
3. **First-raid coach.** On the FIRST successful Raids open with a deployable army: one-line tip
   about deploy + wounded-return (once-per-save key "raid_intro", via GameStateService.MarkTutorialSeen
   or the OnceKeys common when it lands).
4. **Do not burn the intro on a toast.** `barracks_intro` SeenTutorials key must be claimed ONLY when
   the full dialogue/coach beat completes — move the mark out of the toast path
   (BarracksNpcInjector currently writes it raw on toast; also route through MarkTutorialSeen so it
   SAVES — the raw-dict write is a known bypass bug, commons audit cand. 2).
5. **Barracks presence oracle** (review suggestion): DataRegression check — dual-copy barracks row
   parity + singleton + bakedTwins contains CastleBarracks (already in CheckSingletons) + FREE FIRST
   PLACE asserted (first-of-type freebie applies to the 150w/80i cost row — a freebie regression is a
   first-place softlock) + drillmaster anchor resolvable (visualPrefabPath Structures/barracks exists
   in Resources — a missing prefab is a silent empty-pad bug).

## Acceptance criteria
- [ ] Fresh save: after onboarding, the player is POINTED at the barracks (marker visible) without
      opening any menu.
- [ ] Train-3 quest appears, counts, completes, rewards.
- [ ] First raid entry with troops shows the deploy/wounded tip exactly once per save.
- [ ] barracks_intro persists across an app kill (Save() proven), and only after the beat completes.
- [ ] DataRegression barracks-presence checks green in both copies.

## Do NOT touch
- WO-820 gate logic (readiness formula is ArmyReadiness.Compute — consume, never re-roll).
- The queue engine; training itself is untouched.
