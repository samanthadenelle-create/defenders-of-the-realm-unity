# PATH TO V1 — The Through-Line (architect doc, 2026-06-23)

**Owner sets the north star; the orchestrator drives this path; the owner does NOT steer the route.**
Purpose: stop reactive F8-patching — define what V1 **IS**, then walk the ordered critical path so we
**converge**. Pairs with `COMBAT_PIVOT_NORTHSTAR.md`, `WORK_ORDER_483`, `BACKLOG_SNAPSHOT_2026-06-23`,
and memories `combat-pivot-single-hero-northstar` / `overworld-encounter-isolated-battle` / `tripo-roster`.

**The frame:** make the SINGLE Knight + the overworld real-time battle PERFECT. One class, one enemy
family (Orc), one felt-complete vertical. Everything else folds in behind it.

## 1. V1 DONE-STATE — player-facing truths (the checklist)

**The hero (Knight is the star):**
- [ ] I start the game **as a Knight** — Tripo **armored** body. Never Mage, never bare Blink base.
- [ ] My Knight **animates** (idle/walk/attack); movement + facing correct (walk where I aim; no walk-north-face-east).
- [ ] My Knight visibly holds **sword (main) + shield (off-hand)**, gripped right, correct materials (no white/corrupted).
- [ ] My Knight has a **4-skill kit** I see + choose: Q basic + heal + ranged + burst/control. Every fight is a decision.

**The loop (felt-complete vertical):**
- [ ] I **walk the overworld** with a follow cam; floor under me everywhere walkable (no pink void / falling through).
- [ ] I **encounter a rep** (Orc family) roaming relative to me; a red-skull tell reads the threat.
- [ ] Engaging **drops me into an isolated arena fight** — composed stage, Knight vs a few telegraphed Orcs.
- [ ] I **fight and win** with the kit; **winning grants a reward I feel** (skill points / gear / resources), not XP-into-the-void.
- [ ] I **return to the world** and keep going. Loop closed: walk → engage → fight → win → reward → return → walk.

**World traversal:**
- [ ] I can walk **castle → OuterWorld AND back** through the south seam; floor + navmesh continuous; never stuck.

**The feel:**
- [ ] The core loop is **felt-good** end to end — verified by play, not just green gates.

Every box checked = V1 ships. Anything off this list is **not** V1.

## 2. DONE / committed this session (gate-green, felt-UNVERIFIED, NOT pushed)
start-as-Knight source fix (`cb127060`) · Tripo armored body + KnightOnly build-chokepoint (`b7b49092`/`807e382f`) ·
facing fix (`c460afd9`) · shield + gear + barracks (`368fc222`/`65daca8e`) · weapon-material fix (`3b78cf22`) ·
terrain re-center + navmesh + pink-floor (`f3ef39f9`/`5f7c780c`) · encounter→arena loop (`22081724`) · JSON arena
contracts (`3224e942`) · real-path encounter proven + skill tree + 4-slot loadout (`fe58e4ce`) · runtime seam
(`61de6a28`/`5361d4fa`). **Gates green** (CompileGate, PROMOTE_KNIGHT_OK, build SUCCESS, fleet real-path PASS;
EditMode 8 pre-existing fails only). **Dominant remaining risk is FELT, not code.**

## 3. REMAINING for V1
**[V1-CRIT] code:** **C2 reward loop-close** (`BattleArena.GrantWinReward` is XP-only today → skill points (Wisdom) +
light gear/resources; retire dead "unlock next companion") · **Knight kit feel-tune** (4 slots feel distinct/good).
**[V1-CRIT] felt-verify of committed work:** seam both ways · white-skeleton-cloth bug confirm · the whole §1 list.
**[POLISH] (V1 if cheap, never blocks the spine):** arena bounded-JSON refactor (WO-482) · skill-tree Slice 2 (wood/iron
cost) · LifeForce + 1 wood Echo (C7/C8) · `WorldGeometry` constant (S5) · decorative-mesh navmesh noise.

## 4. EXPLICITLY V2 — do NOT pull in
Winding dungeon (WO-485) · store preview pane (WO-486; shop categories already shipped) · gear-stats/weapon-VFX beyond
the Knight set · base-building / convert-on-clear (WO-475) · multi-class (Ranger/Wizard) · armor sets / visible swap ·
fog-of-war · echo depth (drag-drop/5-cap/flex/bonds) · defense right-half (waves/tower-mages/troop slots) · seamless
cross-zone walk (WO-453) · ATB (frozen — don't invest, don't delete).

## 5. CRITICAL PATH — ordered, with parallelism
**Serial spine:** `SEAM-FV → C2 → KIT-FV → V1-FELT-COMPLETE → flip ff.overworldencounter ON → PUSH.`
- **SEAM-FV** (FIRST, felt): walk castle↔OuterWorld both ways; continuous floor/navmesh; never stuck. Broken → repair
  `RuntimeRegionGate` bake (shrink `ConfirmMinRadius` toward 12) before anything downstream.
- **C2**: reward loop-close → the win becomes felt-meaningful (the payoff).
- **KIT-FV**: felt-verify + tune the 4-skill kit in a real Orc fight.
- **V1-FELT**: full play-through confirms every §1 box → flip the flag → push.

**Parallel POLISH silos (disjoint, code-only, never block the spine):** skill-tree Slice 2 · C7 LifeForce + C8 one wood
Echo · gear-stats wiring · `WorldGeometry` constant. **Serial-locked (single owner, editor-closed):** world bakes ·
seam bake repair · WorldGeometry write.

## 6. THE ONE NEXT THING
**Felt-verify the runtime seam** — walk castle → OuterWorld and BACK through `RuntimeRegionGate`; floor + navmesh
continuous, hero never stuck (`Assets/_Modules/Village/World/RuntimeRegionGate.cs`; `61de6a28`+`5361d4fa`). Clean both
ways → drive **C2 reward loop-close** next. Broken → repair its bake first.

**Critical files:** `BattleArena.cs` (GrantWinReward 449-460 = C2) · `RuntimeRegionGate.cs` (seam) ·
`HeroLoadout.cs` (kit feel) · `WisdomCurrencyService.cs` (skill-point reward + Slice 2) · `LootTableCatalog.cs` (C2 gear/resource).
