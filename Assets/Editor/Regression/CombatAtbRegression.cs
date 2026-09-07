// =============================================================================
// CombatAtbRegression — the 5th SME headless suite: the COMBAT / ATB architecture
// path (docs/MASTER_CATALOG/battle-atb.md + docs/COMBAT_PIVOT_NORTHSTAR.md). Pure
// "real object in -> assert real response" (INSTRUMENTATION_STANDARD §4). NO scene
// loads, NO PlayMode — the ATB Engine is a deterministic pure-C# port (mulberry32
// RNG, golden-vector bit-parity), so this LEANS INTO that determinism.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (editor-only). Contract mirrors the sibling
// SME oracles wired into DataRegression.RunAll:
//   public static bool Run(out string reason)
//   markers COMBAT_OK (Debug.Log) / COMBAT_FAIL (Debug.LogError → break-log.jsonl
//   per INSTRUMENTATION_STANDARD §4/§5). The orchestrator owns the one-line RunAll
//   registration.
//
// DELIBERATELY NOT DUPLICATED (owned by AtbEngineRegression — the pre-existing ATB
// oracle): tuning-table slot/stat completeness, BattleController.MapToEngineDef
// totality, and whole-battle AutoResolve determinism/termination. This suite goes
// one layer DEEPER on the primitives those rely on: RNG reproducibility + aliasing,
// RoundTs half-up parity, per-hit damage invariants, turn-order/outcome logic, wave
// scaling monotonicity, the ability cast-gate, enemies.json stat sanity, and the
// KNOWN synthesized-orc-raider stat DIVERGENCE (a fail-by-design oracle, per the
// VillageEconomyRegression B2 precedent).
//
// FAIL-BY-DESIGN (expected RED on first run): the orc-raider base stat block
// diverges across the synthesized spawner tables (RegionMobSpawner=95 vs the
// GarrisonController/GarrisonStatBlocks table=170). Check H names it and keeps it
// loud until the tables are unified. Tagged [FAIL-BY-DESIGN] so it is never mistaken
// for a true regression.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using DeNelle.BattleATB.Engine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Editor
{
    public static class CombatAtbRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();   // true regressions
            var byDesign = new List<string>();    // intentional named divergences (fail-by-design)
            var log = new StringBuilder();
            log.AppendLine("=== CombatAtbRegression: ATB engine + combat data spine ===");

            try
            {
                CheckRngDeterminism(failures, log);        // A
                CheckRoundTsParity(failures, log);         // B
                CheckDamageInvariants(failures, log);      // C
                CheckTurnAndOutcomeLogic(failures, log);   // D
                CheckWaveScalingMonotonicity(failures, log); // E
                CheckAbilityCastGate(failures, log);       // F
                CheckEnemyStatBlockSanity(failures, log);  // G
                CheckSynthesizedStatDivergence(byDesign, log); // H (fail-by-design)
                CheckSynthesizerVsCatalog(failures, log);     // H2 (F18/F46/F47)
                CheckEnemyScaleTracePresent(failures, log);   // H3 (WO-1530)
                CheckGarrisonStatSsot(failures, log);         // H4 (WO-1535)
                NoteKnownHardcodes(log);                   // I (documented skips)
            }
            catch (Exception ex)
            {
                failures.Add($"CombatAtbRegression threw: {ex.GetType().Name}: {ex.Message}");
            }

            return Verdict(failures, byDesign, log, out reason);
        }

        // =====================================================================
        //  A. RNG DETERMINISM & PARITY (Rng.cs / RngOps) — F-RNG-1/4. The engine is
        //     save/replay-safe ONLY if the same seed reproduces the same stream and
        //     the Rng stays a shared MUTABLE REFERENCE (a struct would fork it and
        //     break every golden vector). Bit-parity vs TS is guarded by the NUnit
        //     RngGoldenVectorTest; here we guard the reproducibility + aliasing +
        //     range contracts that survive without a hand-embedded golden constant.
        // =====================================================================
        private static void CheckRngDeterminism(List<string> failures, StringBuilder log)
        {
            const int seed = 20260712;
            const int draws = 24;

            // A1: same seed -> identical sequence (deterministic reproducibility).
            var a = RngOps.CreateRng(seed);
            var b = RngOps.CreateRng(seed);
            bool identical = true;
            var seq = new double[draws];
            for (int i = 0; i < draws; i++)
            {
                seq[i] = RngOps.RngNext(a);
                double y = RngOps.RngNext(b);
                if (seq[i] != y) identical = false;
                // A4: every draw is in [0,1).
                if (seq[i] < 0.0 || seq[i] >= 1.0)
                    failures.Add($"RngNext produced {seq[i]} outside [0,1) at draw {i}");
            }
            if (!identical)
                failures.Add("RNG NOT reproducible: two RNGs with the same seed diverged (replay/save integrity broken)");

            // A2: a different seed diverges (guards a constant/stuck stream).
            var c = RngOps.CreateRng(seed + 1);
            bool differs = false;
            for (int i = 0; i < draws; i++)
                if (RngOps.RngNext(c) != seq[i]) { differs = true; break; }
            if (!differs)
                failures.Add("RNG seed had NO effect: seed+1 reproduced the seed sequence verbatim (seed ignored)");

            // A3: Rng is a shared mutable REFERENCE — aliasing one advances the other
            //     (a struct regression would fork the stream; F-RNG-1/4).
            var shared = RngOps.CreateRng(seed);
            var alias = shared;
            uint before = shared.Seed;
            RngOps.RngNext(alias);
            if (shared.Seed == before)
                failures.Add("Rng is not a shared mutable reference: advancing an alias did NOT move the original cursor (a struct would fork the golden stream)");

            // A4b: derived helper contracts. RngChance(0) never true, RngChance(1)
            //      always true (RngNext ∈ [0,1)); RngInt stays within bounds.
            var d = RngOps.CreateRng(seed);
            for (int i = 0; i < 200; i++)
            {
                if (RngOps.RngChance(d, 0.0)) { failures.Add("RngChance(rng, 0.0) returned true (impossible for a [0,1) draw)"); break; }
            }
            var e = RngOps.CreateRng(seed);
            for (int i = 0; i < 200; i++)
            {
                if (!RngOps.RngChance(e, 1.0)) { failures.Add("RngChance(rng, 1.0) returned false (impossible for a [0,1) draw)"); break; }
            }
            var f = RngOps.CreateRng(seed);
            for (int i = 0; i < 500; i++)
            {
                int r = RngOps.RngInt(f, 3, 7);
                if (r < 3 || r > 7) { failures.Add($"RngInt(3,7) returned {r} out of [3,7]"); break; }
            }

            // A5: CloneBattle yields an INDEPENDENT Rng (deep-clone isolation — the
            //     SO owns isolated state; mutating the clone must not touch the source).
            var state = BattleStateOps.CreateBattle(MakeSetup(seed: 424242, wave: 3));
            uint sourceSeed = state.Rng.Seed;
            var clone = BattleStateOps.CloneBattle(state);
            if (ReferenceEquals(clone.Rng, state.Rng))
                failures.Add("CloneBattle shared the SAME Rng reference — a cloned battle would perturb the original's stream");
            RngOps.RngNext(clone.Rng);
            if (state.Rng.Seed != sourceSeed)
                failures.Add($"CloneBattle Rng not isolated: advancing the clone moved the source cursor ({sourceSeed} -> {state.Rng.Seed})");

            log.AppendLine($"  RNG: reproducible + seed-sensitive + reference-aliased + clone-isolated ({draws} draws) OK");
        }

        // =====================================================================
        //  B. RoundTs HALF-UP PARITY (PortMath) — F-STATE-1. Every damage/heal/scale
        //     rounds through RoundTs (JS Math.round half-up), NOT C# banker's rounding.
        //     A regression to Math.Round would silently under-round every .5 and break
        //     bit-parity with the TS reference. Assert the half-up boundary directly.
        // =====================================================================
        private static void CheckRoundTsParity(List<string> failures, StringBuilder log)
        {
            (double x, int expect)[] cases =
            {
                (0.5, 1), (1.5, 2), (2.5, 3), (3.5, 4),   // banker's would give 0/2/2/4 — the .5 divergence
                (2.4, 2), (2.6, 3), (0.49, 0), (0.0, 0),
                (-0.5, 0), (-1.5, -1),                     // Floor(x+0.5): -0.5->0, -1.5->-1 (matches Math.round)
            };
            foreach (var (x, expect) in cases)
            {
                int got = PortMath.RoundTs(x);
                if (got != expect)
                    failures.Add($"RoundTs({x}) = {got}, expected {expect} (half-up parity broke — banker's rounding regression?)");
            }
            // Contrast proof: the classic banker's divergence must NOT reproduce.
            if (PortMath.RoundTs(2.5) == 2)
                failures.Add("RoundTs(2.5) rounded to 2 (banker's) — must be 3 (half-up) for TS bit-parity");
            log.AppendLine("  RoundTs half-up parity OK (0.5->1, 2.5->3, ...)");
        }

        // =====================================================================
        //  C. DAMAGE INVARIANTS (Combat) — the per-hit resolution contract. All
        //     comparisons use FRESH RNGs on the SAME seed + CanCrit=false so the crit
        //     draw is short-circuited and only the (identical) spread draw fires —
        //     isolating the modifier under test. F-CMB-1 (shield after both draws).
        // =====================================================================
        private static void CheckDamageInvariants(List<string> failures, StringBuilder log)
        {
            // C6: ElementMultiplier — the flame>ice>aether>flame RPS ring + neutral.
            void Elem(ElementType at, ElementType df, double expect)
            {
                double m = Combat.ElementMultiplier(at, df);
                if (Math.Abs(m - expect) > 1e-9)
                    failures.Add($"ElementMultiplier({at}->{df}) = {m}, expected {expect}");
            }
            Elem(ElementType.Flame, ElementType.Ice, 1.25);
            Elem(ElementType.Ice, ElementType.Aether, 1.25);
            Elem(ElementType.Aether, ElementType.Flame, 1.25);
            Elem(ElementType.Ice, ElementType.Flame, 0.85);
            Elem(ElementType.Aether, ElementType.Ice, 0.85);
            Elem(ElementType.Flame, ElementType.Aether, 0.85);
            Elem(ElementType.Physical, ElementType.Physical, 1.0);
            Elem(ElementType.Flame, ElementType.Physical, 1.0);

            const int seed = 9001;

            // C1: damage floor — even vs a heavily-armoured target a live hit is >= 1.
            var armoured = MakeTarget(defense: 0.9, element: ElementType.Physical);
            var r1 = Combat.CalculateDamage(new DamageInput
            {
                Target = armoured, BasePower = 10, Element = ElementType.Physical,
                CanCrit = false, Rng = RngOps.CreateRng(seed),
            });
            if (r1.Damage < 1)
                failures.Add($"CalculateDamage floor broken: {r1.Damage} (< 1) against a 0.9-defense target");

            // C3: aether ignores armour — vs a physical-element target (elementMul==1
            //     both ways) an aether hit out-damages a physical hit of equal power.
            var tgt = MakeTarget(defense: 0.3, element: ElementType.Physical);
            var phys = Combat.CalculateDamage(new DamageInput
            {
                Target = tgt, BasePower = 100, Element = ElementType.Physical,
                CanCrit = false, Rng = RngOps.CreateRng(seed),
            });
            var aeth = Combat.CalculateDamage(new DamageInput
            {
                Target = tgt, BasePower = 100, Element = ElementType.Aether,
                CanCrit = false, Rng = RngOps.CreateRng(seed),
            });
            if (!(aeth.Damage > phys.Damage))
                failures.Add($"Aether did NOT ignore armour: aether {aeth.Damage} !> physical {phys.Damage} vs a 0.3-defense target");

            // C4: Defend halves — a defending target takes ~half of what it would open.
            var open = MakeTarget(defense: 0.1, element: ElementType.Physical);
            var guarded = MakeTarget(defense: 0.1, element: ElementType.Physical);
            guarded.Defending = true;
            var dOpen = Combat.CalculateDamage(new DamageInput
            {
                Target = open, BasePower = 100, Element = ElementType.Physical,
                CanCrit = false, Rng = RngOps.CreateRng(seed),
            });
            var dGuard = Combat.CalculateDamage(new DamageInput
            {
                Target = guarded, BasePower = 100, Element = ElementType.Physical,
                CanCrit = false, Rng = RngOps.CreateRng(seed),
            });
            if (!(dGuard.Damage < dOpen.Damage))
                failures.Add($"Defend did not reduce damage: guarded {dGuard.Damage} !< open {dOpen.Damage}");
            if (Math.Abs(dGuard.Damage * 2 - dOpen.Damage) > 2)
                failures.Add($"Defend is not ~50%: guarded {dGuard.Damage} vs open {dOpen.Damage} (x2 should ~= open)");

            // C2: Shield soaks the hit ENTIRELY and is consumed on apply (F-CMB-1).
            var shielded = MakeTarget(defense: 0.1, element: ElementType.Physical);
            shielded.Statuses.Add(new StatusEffect { Kind = StatusKind.Shield, Turns = 1, Potency = 0 });
            int hpBefore = shielded.Hp;
            var rShield = Combat.CalculateDamage(new DamageInput
            {
                Target = shielded, BasePower = 100, Element = ElementType.Physical,
                CanCrit = false, Rng = RngOps.CreateRng(seed),
            });
            if (!rShield.Shielded || rShield.Damage != 0)
                failures.Add($"Shield did not soak the hit: Shielded={rShield.Shielded} Damage={rShield.Damage}");
            int lost = Combat.ApplyDamage(shielded, rShield);
            if (lost != 0 || shielded.Hp != hpBefore)
                failures.Add($"ApplyDamage(shielded) removed HP: lost={lost} hp {hpBefore}->{shielded.Hp}");
            if (BattleStateOps.HasStatus(shielded, StatusKind.Shield))
                failures.Add("Shield was not consumed after soaking a hit (would block every hit forever)");

            // C5: ApplyDamage clamps to 0 + flips Alive; ApplyHeal clamps to MaxHp,
            //     no-ops on the dead and on non-positive amounts.
            var dying = MakeTarget(defense: 0.0, element: ElementType.Physical);
            dying.Hp = 5;
            Combat.ApplyDamage(dying, new DamageResult { Damage = 999, Crit = false, Shielded = false });
            if (dying.Hp != 0 || dying.Alive)
                failures.Add($"ApplyDamage did not clamp/kill: hp={dying.Hp} alive={dying.Alive}");
            if (Combat.ApplyHeal(dying, 50) != 0 || dying.Hp != 0)
                failures.Add("ApplyHeal revived/healed a DEAD unit (must be a no-op)");

            var hurt = MakeTarget(defense: 0.0, element: ElementType.Physical);
            hurt.MaxHp = 100; hurt.Hp = 90;
            int healed = Combat.ApplyHeal(hurt, 50);
            if (hurt.Hp != 100 || healed != 10)
                failures.Add($"ApplyHeal did not clamp at MaxHp: hp={hurt.Hp} healed={healed} (expected 100 / 10)");
            if (Combat.ApplyHeal(hurt, 0) != 0 || Combat.ApplyHeal(hurt, -5) != 0)
                failures.Add("ApplyHeal for a non-positive amount returned non-zero");

            log.AppendLine("  damage: element RPS + floor>=1 + aether-pierce + defend-half + shield-soak/consume + clamp OK");
        }

        // =====================================================================
        //  D. TURN ORDER + OUTCOME LOGIC (Turn / BattleStateOps). Pure decisions, no
        //     RNG. WO-169: IsPlayerControlled reads ControlMode, NOT UnitKind.
        // =====================================================================
        private static void CheckTurnAndOutcomeLogic(List<string> failures, StringBuilder log)
        {
            // D1: ReadyUnit returns the lowest-index unit at ATB_FULL (tie-break).
            var u0 = MakeTarget(0.0, ElementType.Physical); u0.Id = "u0"; u0.Atb = Defs.ATB_FULL;
            var u1 = MakeTarget(0.0, ElementType.Physical); u1.Id = "u1"; u1.Atb = Defs.ATB_FULL;
            var s = new BattleState
            {
                Units = new List<BattleUnit> { u0, u1 },
                Reserve = new List<RallyReserveUnit>(),
                Log = new List<BattleLogEntry>(),
                Inventory = new Dictionary<ItemKind, int>(),
                Rng = RngOps.CreateRng(1),
            };
            var ready = Turn.ReadyUnit(s);
            if (ready == null || ready.Id != "u0")
                failures.Add($"ReadyUnit tie-break wrong: got '{ready?.Id ?? "<null>"}', expected lowest-index 'u0'");

            // D3: IsBattleOver / ComputeOutcome across the three states.
            var hero = BattleStateOps.BuildHeroUnit(HeroClass.Knight, "K", "hero", ControlMode.Player, false);
            var foe = BattleStateOps.BuildEnemyUnit(new BreachEnemySpec { DefId = "skeleton" }, 0, 1);
            var live = new BattleState
            {
                Units = new List<BattleUnit> { hero, foe },
                Reserve = new List<RallyReserveUnit>(),
                Log = new List<BattleLogEntry>(),
                Inventory = new Dictionary<ItemKind, int>(),
                Rng = RngOps.CreateRng(1),
            };
            if (BattleStateOps.IsBattleOver(live)) failures.Add("IsBattleOver true while both sides live");
            if (BattleStateOps.ComputeOutcome(live) != BattleOutcome.None)
                failures.Add("ComputeOutcome != None while both sides live");
            foe.Alive = false;
            if (!BattleStateOps.IsBattleOver(live)) failures.Add("IsBattleOver false after the enemy side was wiped");
            if (BattleStateOps.ComputeOutcome(live) != BattleOutcome.Victory)
                failures.Add("ComputeOutcome != Victory with party alive + enemies dead");
            foe.Alive = true; hero.Alive = false;
            if (BattleStateOps.ComputeOutcome(live) != BattleOutcome.Defeat)
                failures.Add("ComputeOutcome != Defeat with the party wiped");

            // D4: IsPlayerControlled is decoupled from UnitKind (WO-169). An AI-flagged
            //     HERO must NOT pause for input; a Player-flagged PET must.
            var aiHero = BattleStateOps.BuildHeroUnit(HeroClass.Mage, "M", "h2", ControlMode.AI, false);
            var playerPet = BattleStateOps.BuildPetUnit(PetSpecies.IceWolf, "W", 2, PetAiMode.Balanced, "p2", ControlMode.Player, false);
            if (Turn.IsPlayerControlled(aiHero))
                failures.Add("IsPlayerControlled(AI hero) == true — still hard-tied to UnitKind.Hero (WO-169 regression)");
            if (!Turn.IsPlayerControlled(playerPet))
                failures.Add("IsPlayerControlled(Player pet) == false — control mode not honoured (WO-169 regression)");

            log.AppendLine("  turn/outcome: ready tie-break + victory/defeat + control-mode decoupling OK");
        }

        // =====================================================================
        //  E. WAVE SCALING MONOTONICITY (BattleScaling + BuildEnemyUnit). The endless
        //     curve must never get EASIER as waves climb, the boss cadence lands every
        //     6, and BuildEnemyUnit must fold the boss HP multiplier onto BOSS defs on
        //     boss waves ONLY — never onto grunts.
        // =====================================================================
        private static void CheckWaveScalingMonotonicity(List<string> failures, StringBuilder log)
        {
            double prevHp = -1, prevDmg = -1, prevSpeed = -1; int prevCount = -1;
            for (int w = 1; w <= 30; w++)
            {
                var sc = BattleScaling.WaveScaling(w);
                if (sc.HpMul < prevHp)   failures.Add($"WaveScaling HpMul decreased at wave {w} ({sc.HpMul} < {prevHp})");
                if (sc.HeartDmgMul < prevDmg) failures.Add($"WaveScaling HeartDmgMul decreased at wave {w}");
                if (sc.SpeedMul < prevSpeed) failures.Add($"WaveScaling SpeedMul decreased at wave {w} ({sc.SpeedMul} < {prevSpeed})");
                if (sc.SpeedMul > 1.28 + 1e-9) failures.Add($"WaveScaling SpeedMul {sc.SpeedMul} exceeded the 1.28 cap at wave {w}");
                if (sc.EnemyCount < prevCount) failures.Add($"WaveScaling EnemyCount decreased at wave {w}");
                if (sc.EnemyCount > 12) failures.Add($"WaveScaling EnemyCount {sc.EnemyCount} exceeded the 12 cap at wave {w}");
                prevHp = sc.HpMul; prevDmg = sc.HeartDmgMul; prevSpeed = sc.SpeedMul; prevCount = sc.EnemyCount;
            }
            if (BattleScaling.WaveScaling(1).EnemyCount != 8)
                failures.Add($"WaveScaling(1).EnemyCount = {BattleScaling.WaveScaling(1).EnemyCount}, expected 8 (React wave-1 spec)");

            // Boss cadence: true EXACTLY at multiples of BOSS_EVERY, ordinal/hp increasing.
            for (int w = 1; w <= 24; w++)
            {
                bool expect = w % BattleScaling.BOSS_EVERY == 0;
                if (BattleScaling.IsBossWave(w) != expect)
                    failures.Add($"IsBossWave({w}) = {BattleScaling.IsBossWave(w)}, expected {expect}");
            }
            if (!(BattleScaling.BossHpMul(6) < BattleScaling.BossHpMul(12) && BattleScaling.BossHpMul(12) < BattleScaling.BossHpMul(18)))
                failures.Add($"BossHpMul not increasing per ordinal: w6={BattleScaling.BossHpMul(6)} w12={BattleScaling.BossHpMul(12)} w18={BattleScaling.BossHpMul(18)}");

            // BuildEnemyUnit monotonic MaxHp for a fixed grunt def across waves.
            var grunt = new BreachEnemySpec { DefId = "skeleton" };
            int prevMax = -1;
            for (int w = 1; w <= 20; w++)
            {
                var unit = BattleStateOps.BuildEnemyUnit(grunt, 0, w);
                if (unit.MaxHp <= 0 || unit.Attack <= 0 || unit.Speed <= 0)
                    failures.Add($"BuildEnemyUnit(skeleton, wave {w}) produced non-positive stats (hp {unit.MaxHp}, atk {unit.Attack}, spd {unit.Speed})");
                if (unit.MaxHp < prevMax)
                    failures.Add($"BuildEnemyUnit(skeleton) MaxHp decreased at wave {w} ({unit.MaxHp} < {prevMax})");
                prevMax = unit.MaxHp;
            }

            // Boss-mult application: a BOSS def on a boss wave gets BossHpMul folded in;
            // a GRUNT on the same wave does NOT. Recompute the expectation from the same
            // public primitives to prove BuildEnemyUnit's wiring (not tautological — the
            // unit builder is the code under test).
            var kingDef = Defs.ENEMY_DEFS["hollow-king"];
            int bossW = 12;
            int expectedKing = PortMath.RoundTs(kingDef.BaseHp * BattleScaling.WaveScaling(bossW).HpMul * BattleScaling.BossHpMul(bossW));
            var king = BattleStateOps.BuildEnemyUnit(new BreachEnemySpec { DefId = "hollow-king" }, 0, bossW);
            if (king.MaxHp != expectedKing)
                failures.Add($"BuildEnemyUnit(hollow-king, wave 12) MaxHp {king.MaxHp} != {expectedKing} (boss HP multiplier not folded in)");
            var skelDef = Defs.ENEMY_DEFS["skeleton"];
            int expectedSkel = PortMath.RoundTs(skelDef.BaseHp * BattleScaling.WaveScaling(bossW).HpMul); // no boss mult
            var skel12 = BattleStateOps.BuildEnemyUnit(grunt, 0, bossW);
            if (skel12.MaxHp != expectedSkel)
                failures.Add($"BuildEnemyUnit(skeleton, wave 12) MaxHp {skel12.MaxHp} != {expectedSkel} (boss mult wrongly applied to a grunt?)");

            // Unknown def id is a hard guard (must throw, never silently spawn a phantom).
            bool threw = false;
            try { BattleStateOps.BuildEnemyUnit(new BreachEnemySpec { DefId = "no-such-enemy" }, 0, 1); }
            catch { threw = true; }
            if (!threw)
                failures.Add("BuildEnemyUnit did not throw on an unknown def id (would build a phantom enemy)");

            log.AppendLine("  wave-scaling: monotone hp/dmg/speed/count + boss cadence + boss-mult wiring + unknown-guard OK");
        }

        // =====================================================================
        //  F. ABILITY CAST-GATE (BattleStateOps.AvailableAbilities — the real TryCast
        //     gate). An ability is castable IFF Resource >= Cost AND cooldown <= 0. Pet
        //     kit is bond-rank gated. This is the exact filter the HUD + AI read.
        // =====================================================================
        private static void CheckAbilityCastGate(List<string> failures, StringBuilder log)
        {
            var knight = BattleStateOps.BuildHeroUnit(HeroClass.Knight, "K", "hero", ControlMode.Player, false);
            if (BattleStateOps.UnitAbilityKit(knight).Count != 4)
                failures.Add($"Knight UnitAbilityKit = {BattleStateOps.UnitAbilityKit(knight).Count}, expected 4 (Q/W/E/R)");

            // Mana gate: at Resource 10 only the free ability (Guard, cost 0) is castable.
            knight.Resource = 10;
            var poor = BattleStateOps.AvailableAbilities(knight);
            if (poor.Count != 1 || poor[0].Cost != 0)
                failures.Add($"Mana gate wrong: at Resource 10 the Knight had {poor.Count} castable ({string.Join(",", poor.Select(a => a.Name))}); expected only the cost-0 ability");

            // Full resource: all four castable (all cooldowns 0).
            knight.Resource = knight.MaxResource;
            var rich = BattleStateOps.AvailableAbilities(knight);
            if (rich.Count != 4)
                failures.Add($"At full resource the Knight had {rich.Count} castable, expected 4");

            // Cooldown gate: a slot on cooldown drops out even when affordable.
            var freeSlot = rich.First(a => a.Cost == 0).Slot;
            knight.Cooldowns[freeSlot] = 2;
            var afterCd = BattleStateOps.AvailableAbilities(knight);
            if (afterCd.Any(a => a.Slot == freeSlot))
                failures.Add($"Cooldown gate leaked: slot {freeSlot} (cd=2) was still castable");
            if (afterCd.Count != 3)
                failures.Add($"Cooldown gate wrong count: {afterCd.Count} castable with one slot on cd, expected 3");
            if (BattleStateOps.CooldownOf(knight, freeSlot) != 2)
                failures.Add($"CooldownOf({freeSlot}) != 2 after set");

            // Pet kit is bond-rank gated (PetUnlockedAbilityCount 0/1/2).
            if (BattleStateOps.PetUnlockedAbilityCount(0) != 0 ||
                BattleStateOps.PetUnlockedAbilityCount(1) != 1 ||
                BattleStateOps.PetUnlockedAbilityCount(2) != 2 ||
                BattleStateOps.PetUnlockedAbilityCount(4) != 2)
                failures.Add("PetUnlockedAbilityCount ladder wrong (expected 0->0, 1->1, 2+->2)");
            var pet0 = BattleStateOps.BuildPetUnit(PetSpecies.FlamePup, "P", 0, PetAiMode.Balanced, "pet", ControlMode.AI, false);
            var pet2 = BattleStateOps.BuildPetUnit(PetSpecies.FlamePup, "P", 2, PetAiMode.Balanced, "pet", ControlMode.AI, false);
            if (BattleStateOps.UnitAbilityKit(pet0).Count != 0)
                failures.Add($"Bond-0 pet kit = {BattleStateOps.UnitAbilityKit(pet0).Count}, expected 0");
            if (BattleStateOps.UnitAbilityKit(pet2).Count != 2)
                failures.Add($"Bond-2 pet kit = {BattleStateOps.UnitAbilityKit(pet2).Count}, expected 2");

            // Data sanity on the ability tables (costs/cooldowns non-negative).
            foreach (var kv in Defs.HERO_ABILITIES)
                foreach (var abil in kv.Value)
                {
                    if (abil.Cost < 0) failures.Add($"{kv.Key} '{abil.Name}' has negative Cost {abil.Cost}");
                    if (abil.CooldownTurns < 0) failures.Add($"{kv.Key} '{abil.Name}' has negative CooldownTurns");
                }

            log.AppendLine("  cast-gate: mana + cooldown + bond-rank gating (+ table sanity) OK");
        }

        // =====================================================================
        //  G. ENEMY STAT-BLOCK SANITY (enemies.json) — parsed through the SAME real
        //     path WaveDataLoader uses (CanonicalJson -> EnemyCatalog). Distinct from
        //     DataRegression.CheckEnemies (which resolves the MODEL prefab path): here
        //     we assert the COMBAT numbers are alive — hp/moveSpeed/contactDamage/
        //     interval/height all > 0 and ids unique (a 0 collapses a stat at runtime).
        // =====================================================================
        private static void CheckEnemyStatBlockSanity(List<string> failures, StringBuilder log)
        {
            string json = DeNelle.Core.CanonicalJson.Read(DeNelle.Village.WaveDataLoader.EnemiesRelativePath);
            if (string.IsNullOrEmpty(json))
            {
                failures.Add("enemies.json unreadable (CanonicalJson.Read returned empty)");
                return;
            }
            DeNelle.Village.EnemyCatalog catalog;
            try { catalog = JsonConvert.DeserializeObject<DeNelle.Village.EnemyCatalog>(json); }
            catch (Exception ex) { failures.Add($"enemies.json failed to parse: {ex.Message}"); return; }

            if (catalog == null || catalog.Enemies == null || catalog.Enemies.Count == 0)
            {
                failures.Add("enemies.json deserialized to 0 EnemyDef objects (mapping break or empty 'enemies')");
                return;
            }

            var seen = new HashSet<string>();
            foreach (var e in catalog.Enemies)
            {
                if (e == null || string.IsNullOrEmpty(e.Id)) { failures.Add("enemies.json has an entry with null/empty id"); continue; }
                if (!seen.Add(e.Id))
                    failures.Add($"enemies.json duplicate id '{e.Id}' (later stat block silently wins)");
                if (string.IsNullOrEmpty(e.DisplayName)) failures.Add($"enemies.json '{e.Id}' has no displayName");
                if (e.Hp <= 0f)             failures.Add($"enemies.json '{e.Id}' hp {e.Hp} <= 0 (dead-on-spawn)");
                if (e.MoveSpeed <= 0f)      failures.Add($"enemies.json '{e.Id}' moveSpeed {e.MoveSpeed} <= 0 (never advances)");
                if (e.ContactDamage <= 0f)  failures.Add($"enemies.json '{e.Id}' contactDamage {e.ContactDamage} <= 0 (harmless)");
                if (e.AttackInterval <= 0f) failures.Add($"enemies.json '{e.Id}' attackInterval {e.AttackInterval} <= 0 (attacks every frame)");
                if (e.Height <= 0f)         failures.Add($"enemies.json '{e.Id}' height {e.Height} <= 0 (degenerate capsule/bar)");
            }
            log.AppendLine($"  enemies.json: {catalog.Enemies.Count} stat block(s), ids unique, hp/speed/dmg/interval/height all > 0 OK");
        }

        // =====================================================================
        //  H. SYNTHESIZED-ENEMY STAT DIVERGENCE (FAIL-BY-DESIGN oracle). The Wildlands
        //     ids (orc-raider/caveman/…) are NOT in enemies.json — each overworld
        //     spawner CODE-BUILDS its own EnemyDef via a private switch. Those switches
        //     have DRIFTED: RegionMobSpawner/EnemyOutpost/CampDefenseWave stat
        //     orc-raider at hp 95, while the GarrisonController path (GarrisonStatBlocks)
        //     stats the SAME id at hp 170. Same id, two truths — a raider is nearly 2x
        //     tougher depending on which system spawned it. We RESOLVE both from the
        //     REAL builders (RegionMobSpawner via reflection; GarrisonStatBlocks via its
        //     public API, base recovered by dividing out GlobalDifficultyMult) and emit
        //     ONE tagged red naming it. Expected RED until the tables are unified into a
        //     single source of truth (a Wildlands enemies.json roster or a shared table).
        // =====================================================================
        // =====================================================================
        //  H2. SYNTHESIZER vs CATALOG  (audit F18 / F46 / F47)
        // =====================================================================
        // H (above) compares two synthesizers against EACH OTHER. That answers "do the
        // spawners agree?" but never "do they agree with the SSOT?" - so a value that is
        // consistently wrong everywhere reads as green. This one joins each synthesizer
        // against enemies.json, which is the authority.
        //
        // Measured by the 2026-08-09 audit and reproduced here:
        //   F18 TribeManager.BuildRaiderDef      orc-raider Hp 60 vs catalog 130
        //                                        necromancer Hp 90 vs catalog 1700
        //   F46 caveman / feral-wolf / tiefling-cultist are spawned by roster code but
        //       have NO enemies.json row at all - no catalog entry for a designer to tune
        //   F47 WildlandsRoster's fallback, whose own comment claims "IDENTICAL numbers to
        //       the enemies.json orc-raider entry, so a missing catalog can NEVER
        //       reintroduce the divergence", carries XpReward 22 against the catalog's 24
        //
        // A REFLECTION MISS IS A FAILURE HERE, NOT A SKIP. The sibling check H logs
        // "could not resolve both sources - skipped" and returns green, so renaming
        // BuildRoamerDef silently disarms it (the plan pass flagged this shape at :555-558).
        // An oracle that cannot reach its subject has not passed; it has stopped working.
        /// <summary>The spawn[] context TribeManager operates in (roaming raider tribes).</summary>
        private const string TribeSpawnContext = "roam";

        /// <summary>
        /// DATED, RATCHETED spawn-context violations (2026-08-09). A pinned entry is a
        /// recorded content decision still owed; a NEW violation hard-fails.
        /// </summary>
        private static readonly HashSet<string> KnownSpawnContextViolations =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                // boss:true, role:elite, spawn:["wave"], 1700 Hp / 220 xp - "Alduin's
                // Necromancer". TribeManager emits the same id as a 90 Hp / 28 xp roaming
                // "Wound Necromancer". One id, two creatures. The fix is a content call:
                // give the tribe caster its own id + catalog row, point it at an existing
                // legal id, or drop it. NOT to raise the raider to boss stats.
                "necromancer",
            };

        private static void CheckSynthesizerVsCatalog(List<string> failures, StringBuilder log)
        {
            using var _ = FlowTrace.Enter("CombatAtb", "CheckSynthesizerVsCatalog");

            // --- the SSOT ---
            DeNelle.Village.EnemyCatalog catalog = null;
            string json = DeNelle.Core.CanonicalJson.Read(DeNelle.Village.WaveDataLoader.EnemiesRelativePath);
            if (!string.IsNullOrEmpty(json))
            {
                try { catalog = JsonConvert.DeserializeObject<DeNelle.Village.EnemyCatalog>(json); }
                catch (Exception ex) { failures.Add($"[synth-vs-catalog] enemies.json parse error: {ex.Message}"); return; }
            }
            if (catalog?.Enemies == null || catalog.Enemies.Count == 0)
            {
                failures.Add("[synth-vs-catalog] enemies.json produced 0 EnemyDef objects - cannot join synthesizers against the SSOT");
                return;
            }

            var bySlug = new Dictionary<string, DeNelle.Village.EnemyDef>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in catalog.Enemies)
                if (e != null && !string.IsNullOrEmpty(e.Id)) bySlug[e.Id] = e;

            // --- F18: TribeManager.BuildRaiderDef vs catalog ---
            var villageAsm = typeof(DeNelle.Village.EnemyDef).Assembly;
            var tribe = villageAsm.GetType("DeNelle.Village.TribeManager");
            var raider = tribe?.GetMethod("BuildRaiderDef", BindingFlags.NonPublic | BindingFlags.Static);
            if (raider == null)
            {
                FlowTrace.Fail("CombatAtb", "TribeManager.BuildRaiderDef not reachable");
                failures.Add("[synth-vs-catalog] TribeManager.BuildRaiderDef not found (renamed or moved). This oracle " +
                             "cannot reach its subject, which is a FAILURE and not a skip - a silent skip here is how a " +
                             "rename disarms a guard while the board stays green.");
            }
            else
            {
                string[] rosterIds = { "orc-raider", "necromancer", "caveman", "feral-wolf", "tiefling-cultist" };
                foreach (var id in rosterIds)
                {
                    DeNelle.Village.EnemyDef built = null;
                    try { built = raider.Invoke(null, new object[] { "audit", 0, id }) as DeNelle.Village.EnemyDef; }
                    catch (Exception ex)
                    { failures.Add($"[synth-vs-catalog] BuildRaiderDef('{id}') threw {ex.GetType().Name}"); continue; }
                    if (built == null) continue;

                    DeNelle.Village.EnemyDef cat;
                    if (!bySlug.TryGetValue(id, out cat))
                    {
                        // F46: spawned by roster code, absent from the catalog entirely.
                        FlowTrace.Warn("CombatAtb", "roster id has no catalog row: " + id);
                        log.AppendLine($"  [synth-vs-catalog] NO CATALOG ROW for roster id '{id}' (Hp {built.Hp:0.#} is " +
                                       "hardcoded and untunable - F46)");
                        continue;
                    }

                    if (Mathf.Abs(built.Hp - cat.Hp) > 0.5f)
                    {
                        FlowTrace.Fail("CombatAtb", $"{id} Hp synth={built.Hp} catalog={cat.Hp}");
                        log.AppendLine($"  [synth-vs-catalog] DIVERGENCE '{id}' Hp: TribeManager={built.Hp:0.#} vs " +
                                       $"enemies.json={cat.Hp:0.#}");
                    }

                    // --- SPAWN-CONTEXT CONTRACT (the necromancer finding) ---------------
                    // enemies.json declares WHERE each enemy may appear via spawn[], and the
                    // contract is already implemented at WaveData.cs:214-221 (family +
                    // spawnContext filter). TribeManager does not use it, so it can - and
                    // does - spawn an id whose declaration forbids the context.
                    //
                    // 'necromancer' is boss:true, role:elite, spawn:["wave"] - "Alduin's
                    // Necromancer", 1700 Hp / 220 xp. TribeManager emits that SAME id as a
                    // roaming raider at 90 Hp / 28 xp. That is not a stat typo, it is an ID
                    // COLLISION: one id, two creatures. Raising the raider to 1700 would be
                    // the WRONG fix - it drops a wave boss into a roaming tribe.
                    //
                    // Ratcheted rather than hard-failed: the violation is pre-existing and
                    // its remedy (which creature the tribe caster should actually be) is a
                    // content decision, not a gate action. Known entries are recorded; a NEW
                    // context violation fails.
                    var declared = cat.Spawn ?? new List<string>();
                    bool roamOk = false;
                    foreach (var s in declared)
                        if (string.Equals(s, TribeSpawnContext, StringComparison.OrdinalIgnoreCase)) { roamOk = true; break; }

                    if (!roamOk)
                    {
                        string ctx = declared.Count == 0 ? "<none>" : string.Join(",", declared.ToArray());
                        if (KnownSpawnContextViolations.Contains(id))
                        {
                            log.AppendLine($"  [synth-vs-catalog] KNOWN spawn-context violation '{id}': declares " +
                                           $"spawn[{ctx}] but TribeManager spawns it as '{TribeSpawnContext}'" +
                                           (cat.Boss ? " AND IT IS A BOSS (boss:true)" : "") +
                                           " - pinned, awaiting a content ruling on what the tribe variant should be.");
                        }
                        else
                        {
                            FlowTrace.Fail("CombatAtb", $"NEW spawn-context violation: {id} declares [{ctx}]");
                            failures.Add($"[synth-vs-catalog] NEW SPAWN-CONTEXT VIOLATION '{id}': enemies.json declares " +
                                         $"spawn[{ctx}], which does not include '{TribeSpawnContext}', yet TribeManager " +
                                         "spawns it there. The contract is already implemented at WaveData.cs:214-221; " +
                                         "this spawner bypasses it. Either declare the context in the catalog or stop " +
                                         "emitting this id from the tribe roster.");
                        }
                    }
                }
            }

            // --- F47: CLOSED BY DELETION (WO-1535), and this probe now guards the closure ---
            // F47 was: WildlandsRoster's code fallback promised "IDENTICAL numbers to the
            // enemies.json orc-raider entry, so a missing catalog can NEVER reintroduce the
            // divergence" - and had already drifted from it (XpReward 22 vs the catalog's 24).
            // The finding was never that the copy needed fixing; a hand-maintained mirror of a
            // live table is duplicated state and drifts again (CLAUDE.md sec.2 / sec.5 / sec.16).
            // WO-1535 DELETED the mirror. What BaseDef returns for an unresolvable id is now ONE
            // shared EMERGENCY SENTINEL that is deliberately nothing's stats.
            //
            // ⛔ THIS ASSERTS THE SENTINEL SHAPE, NOT AN Xp COMPARISON. If someone reinstates a
            //    per-id fallback, the probe stops looking like the sentinel and fails here -
            //    which is the whole guard. The old comparison would have gone quietly
            //    meaningless the moment the mirror it compared against ceased to exist.
            DeNelle.Village.EnemyDef probe = null;
            try { probe = DeNelle.Village.WildlandsRoster.BaseDef("__audit_probe_unknown_id__"); }
            catch (Exception ex)
            { failures.Add($"[synth-vs-catalog] WildlandsRoster.BaseDef probe threw {ex.GetType().Name}"); }

            if (probe == null)
            {
                // Never null: CampDefenseWave.cs:296 / CampGuards.cs:212 / EnemyOutpost.cs:935 /
                // RegionMobSpawner.cs:574 all dereference BaseDef's result unguarded.
                failures.Add("[synth-vs-catalog] WildlandsRoster.BaseDef returned NULL for an unknown id. Four spawners " +
                             "dereference this result without a null check (CampDefenseWave/CampGuards/EnemyOutpost/" +
                             "RegionMobSpawner) - this is four NullReferenceExceptions at spawn, not a missing stat.");
            }
            else if (Mathf.Abs(probe.Hp - DeNelle.Village.WildlandsRoster.SentinelHp) > 0.01f)
            {
                DeNelle.Village.EnemyDef orc;
                bool looksLikeOrc = bySlug.TryGetValue("orc-raider", out orc) && orc != null &&
                                    Mathf.Abs(probe.Hp - orc.Hp) <= 0.5f;
                FlowTrace.Fail("CombatAtb", $"BaseDef unknown-id probe Hp={probe.Hp} (sentinel={DeNelle.Village.WildlandsRoster.SentinelHp})");
                failures.Add($"[synth-vs-catalog] F47 REOPENED: WildlandsRoster.BaseDef('__audit_probe_unknown_id__') " +
                             $"returned Hp {probe.Hp:0.#} (id '{probe.Id}'), not the emergency sentinel " +
                             $"{DeNelle.Village.WildlandsRoster.SentinelHp:0.#}. A per-id code fallback has been " +
                             "reinstated - that is the duplicate stat table WO-1535 deleted, and it will drift from " +
                             "enemies.json exactly as the last one did." +
                             (looksLikeOrc ? " It currently returns ORC-RAIDER's stats for an unknown id, which is the " +
                                             "original defect: every unauthored enemy silently becomes an orc raider." : ""));
            }
            else
            {
                log.AppendLine($"  [synth-vs-catalog] F47 CLOSED: BaseDef's per-id code mirror is gone; an unresolvable " +
                               $"id returns the emergency sentinel (hp={probe.Hp:0.#}, id echoes the request as " +
                               $"'{probe.Id}') and FlowTrace.Fail names it. No table left to drift.");
            }

            FlowTrace.Step("CombatAtb", "synth-vs-catalog joined " + bySlug.Count + " catalog row(s)");
            log.AppendLine($"  [synth-vs-catalog] joined against {bySlug.Count} enemies.json row(s). " +
                           "NOTE: WardTetherService.BuildKindleDef (F19) is NOT covered here - it requires a live " +
                           "WardStone instance to invoke, so asserting it needs a fixture rather than reflection. " +
                           "Recorded as an explicit gap rather than skipped silently.");
        }

        // =====================================================================
        //  H3. WO-1530 — the PERMANENT [Flow:EnemyScale] spawn measurement must exist.
        //      The enemy level-scaling formula is only measurable in play because one
        //      FlowTrace.Step names built -> levelled -> final HP/damage at every
        //      garrison + raid spawn. CLAUDE.md §12: instrumentation is never stripped,
        //      so its removal is a REGRESSION, asserted here from source text (the call
        //      sites are MonoBehaviour spawn paths that cannot be invoked headless).
        // =====================================================================
        private static void CheckEnemyScaleTracePresent(List<string> failures, StringBuilder log)
        {
            var sites = new Dictionary<string, int>
            {
                { "_Modules/Village/World/Camps/GarrisonStatBlocks.cs",  1 },  // the ONE Step
                { "_Modules/Village/World/Camps/RaidGarrisonSpawner.cs", 2 },  // boss + guard
                { "_Modules/Village/World/Camps/GarrisonController.cs",  1 },  // additive camp
            };

            foreach (var kv in sites)
            {
                string path = System.IO.Path.Combine(Application.dataPath, kv.Key);
                if (!System.IO.File.Exists(path))
                {
                    failures.Add($"[enemy-scale-trace] source not found: Assets/{kv.Key} (moved or renamed — the WO-1530 measurement cannot be proven)");
                    continue;
                }

                string src = System.IO.File.ReadAllText(path);
                string needle = kv.Key.EndsWith("GarrisonStatBlocks.cs")
                    ? "FlowTrace.Step(\"EnemyScale\""
                    : "GarrisonStatBlocks.TraceSpawnScale(";

                int hits = 0, at = 0;
                while ((at = src.IndexOf(needle, at, StringComparison.Ordinal)) >= 0) { hits++; at += needle.Length; }

                if (hits < kv.Value)
                    failures.Add($"[enemy-scale-trace] Assets/{kv.Key} has {hits} '{needle}' occurrence(s), expected at least " +
                                 $"{kv.Value}. The WO-1530 enemy level-scale measurement was stripped or bypassed — " +
                                 "instrumentation is PERMANENT (CLAUDE.md §12); flag it off, never remove it.");
                else
                    log.AppendLine($"  [enemy-scale-trace] Assets/{kv.Key}: {hits} site(s) OK (>= {kv.Value})");
            }
        }

        // =====================================================================
        //  H4. WO-1535 — THE GARRISON STAT-TABLE SSOT ORACLE.
        //
        //  H compares two synthesizers against each other; H2 joins TribeManager
        //  against the catalog. NEITHER of them asks the question this ticket exists
        //  for: does the RAID GARRISON path — the one that builds every defender the
        //  player fights in a raid — read the authored table, or a code literal?
        //  It did not: 12 of 13 ids were hardcoded in GarrisonStatBlocks.BuildTypedDef,
        //  and the audit's own numbers show the drift (raid Berserker 260 Hp against
        //  the authored 117).
        //
        //  THE ASSERTION. For every id WildlandsRoster.Owns(), the def BuildTypedDef
        //  actually hands EnemyFactory must equal the enemies.json row times that
        //  path's declared CONTEXT folds, on ALL SIX scalars — hp, moveSpeed,
        //  contactDamage, attackInterval, height, xpReward. Six, not just Hp: a
        //  damage-only hardcode sails straight through an Hp check, which is how
        //  orc-shaman's 9-vs-3 would have survived.
        //
        //  THE FOLDS ARE ASSERTED, NOT DIVIDED OUT. Multiplicative stats are compared
        //  as row * fold == built, so the oracle also proves the fold is honest — an
        //  extra hidden multiplier fails here instead of hiding inside a division.
        //    every id : GlobalDifficultyMult (BuildGenericDef)
        //    troll    : additionally the BuildTrollDef threat-2 template scale (1.20),
        //               and XpReward = row + threat*2 rather than row.
        //  Non-multiplicative stats (moveSpeed / attackInterval / height) fold by 1.
        //
        //  RED ON HEAD, BY CONSTRUCTION: before this ticket BuildTypedDef("orc-berserker")
        //  returned Hp 260*1.2 = 312 while the row says 117*1.2 = 140.4 — a failure on
        //  the first id. Re-hardcode ANY migrated id and this goes red again, which is
        //  the acceptance criterion ("a fourth copy can never reappear silently").
        //
        //  PINNED IDS ARE LOGGED, NOT FAILED — a DATED RATCHET, the same shape as
        //  KnownSpawnContextViolations above. Each carries a recorded reason and an
        //  owner ruling it is waiting on; a pin is a debt that stays visible on every
        //  run, not a silent skip. ⛔ A NEW hardcode is NOT covered by the pin: the
        //  ratchet is keyed by id, so adding a literal for any OWNED id hard-fails.
        // =====================================================================

        /// <summary>
        /// DATED, RATCHETED garrison ids that still resolve from code (2026-09-07,
        /// WO-1535). Each maps to the owner ruling it is waiting on. Entries here are
        /// LOGGED; anything not here must resolve from enemies.json or it FAILS.
        /// </summary>
        private static readonly Dictionary<string, string> KnownGarrisonHardcodes =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["necromancer"] =
                    "ID COLLISION, not stat drift - enemies.json's row is the boss:true / spawn:[wave] / 1700 Hp " +
                    "'Alduin's Necromancer'; the garrison wants a 300 Hp camp elite. Migrating would put a 2040 Hp " +
                    "boss (1700 x GlobalDifficultyMult) into an ordinary raid garrison. Same ruling already recorded " +
                    "at KnownSpawnContextViolations. OWNER RULING NEEDED: give the camp elite its own id + row.",
                ["caveman"] =
                    "NO enemies.json row (audit F46) AND the three code tables disagree ~3x - GarrisonStatBlocks 220 Hp " +
                    "vs RegionMobSpawner/CampDefenseWave 70. Seeding either PICKS a balance winner, which WO-1535 sec.3 " +
                    "forbids. docs/enemy-codex.md sec.2.10 has only an unratified ATB-scale anchor. OWNER RULING NEEDED.",
                ["feral-wolf"] =
                    "NO enemies.json row (audit F46) - GarrisonStatBlocks 90 Hp vs RegionMobSpawner/CampDefenseWave 42. " +
                    "docs/enemy-codex.md sec.2.11 has only an unratified ATB-scale anchor. OWNER RULING NEEDED.",
                ["tiefling-cultist"] =
                    "NO enemies.json row (audit F46) - GarrisonStatBlocks 130 Hp vs RegionMobSpawner/CampDefenseWave 80. " +
                    "docs/enemy-codex.md sec.2.12 has only an unratified ATB-scale anchor. OWNER RULING NEEDED.",
            };

        /// <summary>The full set of ids GarrisonStatBlocks.BuildTypedDef switches on. Every
        /// one must EITHER resolve from enemies.json OR be a named, dated pin above.</summary>
        private static readonly string[] GarrisonRecipeIds =
        {
            "troll", "orc-berserker", "orc-shaman", "orc-necromancer", "orc-raider",
            "hollow-walker", "hollow-warrior", "hollow-rogue", "hollow-acolyte",
            "necromancer", "caveman", "feral-wolf", "tiefling-cultist",
        };

        private static void CheckGarrisonStatSsot(List<string> failures, StringBuilder log)
        {
            using var _ = FlowTrace.Enter("CombatAtb", "CheckGarrisonStatSsot");

            // --- the SSOT ---
            DeNelle.Village.EnemyCatalog catalog = null;
            string json = DeNelle.Core.CanonicalJson.Read(DeNelle.Village.WaveDataLoader.EnemiesRelativePath);
            if (!string.IsNullOrEmpty(json))
            {
                try { catalog = JsonConvert.DeserializeObject<DeNelle.Village.EnemyCatalog>(json); }
                catch (Exception ex)
                { failures.Add($"[garrison-ssot] enemies.json parse error: {ex.Message}"); return; }
            }
            if (catalog?.Enemies == null || catalog.Enemies.Count == 0)
            {
                failures.Add("[garrison-ssot] enemies.json produced 0 EnemyDef objects - the garrison SSOT cannot be joined. " +
                             "This oracle cannot reach its subject, which is a FAILURE and not a skip.");
                return;
            }

            var bySlug = new Dictionary<string, DeNelle.Village.EnemyDef>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in catalog.Enemies)
                if (e != null && !string.IsNullOrEmpty(e.Id)) bySlug[e.Id] = e;

            float global = DeNelle.Village.World.Camps.GarrisonStatBlocks.GlobalDifficultyMult;
            if (global <= 0f)
            {
                failures.Add($"[garrison-ssot] GlobalDifficultyMult is {global} (<= 0) - the fold cannot be asserted.");
                return;
            }

            // (A) THE LIST CANNOT GO STALE. GarrisonRecipeIds restates BuildTypedDef's switch
            //     keys, so a 14th `case "new-id":` carrying a fresh literal would be invisible
            //     to every assertion below - the exact "a fourth copy reappears silently"
            //     failure this ticket closes. Read the source and demand the two agree.
            //     (Source-text, like H3: BuildTypedDef's switch is not enumerable at runtime.)
            string gsbPath = System.IO.Path.Combine(Application.dataPath,
                "_Modules/Village/World/Camps/GarrisonStatBlocks.cs");
            if (!System.IO.File.Exists(gsbPath))
            {
                failures.Add("[garrison-ssot] source not found: Assets/_Modules/Village/World/Camps/GarrisonStatBlocks.cs " +
                             "(moved or renamed) - the recipe-id list cannot be proven complete.");
            }
            else
            {
                string src = System.IO.File.ReadAllText(gsbPath);
                int from = src.IndexOf("public static EnemyDef BuildTypedDef(", StringComparison.Ordinal);
                if (from < 0)
                {
                    failures.Add("[garrison-ssot] BuildTypedDef not found in GarrisonStatBlocks.cs (renamed?) - this " +
                                 "oracle cannot reach its subject, which is a FAILURE and not a skip.");
                }
                else
                {
                    var declared = new HashSet<string>(GarrisonRecipeIds, StringComparer.OrdinalIgnoreCase);
                    var found = new List<string>();
                    // Scan only the method body: stop at the next method declaration after it.
                    int stop = src.IndexOf("private static EnemyDef FromTable(", from, StringComparison.Ordinal);
                    if (stop < 0) stop = src.Length;
                    int at = from;
                    while ((at = src.IndexOf("case \"", at, StringComparison.Ordinal)) >= 0 && at < stop)
                    {
                        int s = at + 6;
                        int e = src.IndexOf('"', s);
                        if (e < 0) break;
                        string caseId = src.Substring(s, e - s);
                        found.Add(caseId);
                        if (!declared.Contains(caseId))
                            failures.Add($"[garrison-ssot] BuildTypedDef has `case \"{caseId}\":` but that id is NOT in " +
                                         "GarrisonRecipeIds, so NOTHING here asserts where its stats come from. A garrison " +
                                         "stat block was added outside the SSOT - add the id to GarrisonRecipeIds, then " +
                                         "either author its enemies.json row (and Owns() it) or pin it in " +
                                         "KnownGarrisonHardcodes with the reason and the ruling it awaits.");
                        at = e + 1;
                    }
                    foreach (string id in GarrisonRecipeIds)
                        if (!found.Contains(id, StringComparer.OrdinalIgnoreCase))
                            failures.Add($"[garrison-ssot] GarrisonRecipeIds lists '{id}' but BuildTypedDef has no " +
                                         $"`case \"{id}\":` - the oracle is asserting an id the builder no longer serves.");
                    log.AppendLine($"  [garrison-ssot] BuildTypedDef switch: {found.Count} case id(s), all accounted for in " +
                                   "GarrisonRecipeIds");
                }
            }

            // (B) The roster's own promise. Owns() is what BaseDef/BuildTypedDef trust; if it
            //     claims an id this oracle never iterates, that id resolves from the table with
            //     nothing checking it.
            foreach (string owned in DeNelle.Village.WildlandsRoster.OwnedIdList)
                if (!GarrisonRecipeIds.Contains(owned, StringComparer.OrdinalIgnoreCase))
                    failures.Add($"[garrison-ssot] WildlandsRoster.Owns('{owned}') is true but the id is not in " +
                                 "GarrisonRecipeIds - it resolves from enemies.json with no oracle joining it.");

            int migrated = 0, pinned = 0;

            foreach (string id in GarrisonRecipeIds)
            {
                bool owned = DeNelle.Village.WildlandsRoster.Owns(id);
                bool isPinned = KnownGarrisonHardcodes.ContainsKey(id);

                // (0) Every recipe id is EITHER owned by the roster OR a named pin. A new id
                //     that is neither has been added with no decision recorded either way.
                if (!owned && !isPinned)
                {
                    FlowTrace.Fail("CombatAtb", "garrison id neither owned nor pinned: " + id);
                    failures.Add($"[garrison-ssot] recipe id '{id}' is neither owned by WildlandsRoster (resolves from " +
                                 "enemies.json) nor a dated entry in KnownGarrisonHardcodes. A garrison stat block was " +
                                 "added with no SSOT decision recorded - add the enemies.json row and Owns() it, or pin " +
                                 "it here with the reason and the ruling it awaits.");
                    continue;
                }
                if (owned && isPinned)
                {
                    failures.Add($"[garrison-ssot] recipe id '{id}' is BOTH roster-owned and pinned as a hardcode - " +
                                 "the two lists contradict each other; remove the pin now that the row is authored.");
                    continue;
                }

                if (isPinned)
                {
                    pinned++;
                    bool hasRow = bySlug.ContainsKey(id);
                    log.AppendLine($"  [garrison-ssot] PINNED HARDCODE '{id}' (catalog row {(hasRow ? "EXISTS" : "ABSENT")}): " +
                                   KnownGarrisonHardcodes[id]);
                    continue;
                }

                // (1) An owned id MUST have a row. Owns() promising a table that is not there
                //     is how the emergency sentinel would silently become the stat block.
                DeNelle.Village.EnemyDef row;
                if (!bySlug.TryGetValue(id, out row) || row == null)
                {
                    FlowTrace.Fail("CombatAtb", "owned garrison id has no catalog row: " + id);
                    failures.Add($"[garrison-ssot] '{id}' is WildlandsRoster.Owns() but has NO enemies.json row - " +
                                 "BaseDef falls through to the emergency sentinel, so this enemy's real stats are " +
                                 $"WildlandsRoster.SentinelHp={DeNelle.Village.WildlandsRoster.SentinelHp:0.#}, not anything authored.");
                    continue;
                }

                // (2) The def the player actually meets, straight off the live builder.
                DeNelle.Village.EnemyDef built;
                try { built = DeNelle.Village.World.Camps.GarrisonStatBlocks.BuildTypedDef(id, 0); }
                catch (Exception ex)
                { failures.Add($"[garrison-ssot] BuildTypedDef('{id}') threw {ex.GetType().Name}: {ex.Message}"); continue; }
                if (built == null)
                { failures.Add($"[garrison-ssot] BuildTypedDef('{id}') returned null."); continue; }

                // (3) The DECLARED context folds for this id. Asserted as row*fold==built so a
                //     hidden extra multiplier fails here rather than hiding inside a division.
                //     troll routes through BuildTrollDef(2), which layers its threat template
                //     scale (1 + 0.10*2) on top of GlobalDifficultyMult and pays row+threat*2 XP.
                bool isTroll = string.Equals(id, "troll", StringComparison.OrdinalIgnoreCase);
                float statFold = isTroll ? global * (1f + 0.10f * 2f) : global;
                int expectedXp = isTroll ? row.XpReward + 2 * 2 : row.XpReward;

                int before = failures.Count;
                Compare(failures, id, "hp",             built.Hp,             row.Hp * statFold,          0.01f);
                Compare(failures, id, "contactDamage",  built.ContactDamage,  row.ContactDamage * statFold, 0.01f);
                Compare(failures, id, "moveSpeed",      built.MoveSpeed,      row.MoveSpeed,              0.005f);
                Compare(failures, id, "attackInterval", built.AttackInterval, row.AttackInterval,         0.005f);
                Compare(failures, id, "height",         built.Height,         row.Height,                 0.005f);
                Compare(failures, id, "xpReward",       built.XpReward,       expectedXp,                 0.5f);

                if (failures.Count == before)
                {
                    migrated++;
                    log.AppendLine($"  [garrison-ssot] '{id}' resolves from enemies.json OK " +
                                   $"(row hp={row.Hp:0.#} dmg={row.ContactDamage:0.##} x fold {statFold:0.###} " +
                                   $"= built hp={built.Hp:0.#} dmg={built.ContactDamage:0.##})");
                }
            }

            log.AppendLine($"  [garrison-ssot] {migrated}/{GarrisonRecipeIds.Length} garrison recipe ids resolve from " +
                           $"enemies.json; {pinned} PINNED awaiting an owner content ruling (see the lines above). " +
                           "WO-1535: the pins are debt kept visible, not passes.");
            FlowTrace.Step("CombatAtb", $"garrison-ssot migrated={migrated} pinned={pinned}");
        }

        /// <summary>One scalar of the garrison SSOT join. Named failure, both numbers, and
        /// what the divergence means - never a bare "expected X got Y".</summary>
        private static void Compare(List<string> failures, string id, string field,
                                    float built, float expected, float tol)
        {
            if (Mathf.Abs(built - expected) <= tol) return;
            FlowTrace.Fail("CombatAtb", $"garrison-ssot {id}.{field} built={built} expected={expected}");
            failures.Add($"[garrison-ssot] '{id}' {field}: GarrisonStatBlocks.BuildTypedDef builds {built:0.###} but " +
                         $"enemies.json x the declared context fold gives {expected:0.###}. The raid garrison path is " +
                         "reading a CODE literal, not the authored table - that is a second stat table for one id, and " +
                         "it is what WO-1535 removed. Point this id at WildlandsRoster.BaseDef, or (if the divergence " +
                         "is deliberate) record it in KnownGarrisonHardcodes with the reason and the ruling it awaits.");
        }

        private static void CheckSynthesizedStatDivergence(List<string> byDesign, StringBuilder log)
        {
            const string id = "orc-raider";
            var villageAsm = typeof(DeNelle.Village.EnemyDef).Assembly;

            // Source 1 — RegionMobSpawner.BuildRoamerDef (private static, ease-free at
            // threat 0 → the raw base literal, environment-independent).
            float regionHp = float.NaN;
            var region = villageAsm.GetType("DeNelle.Village.RegionMobSpawner");
            var roamer = region?.GetMethod("BuildRoamerDef", BindingFlags.NonPublic | BindingFlags.Static);
            if (roamer == null)
            {
                log.AppendLine("  [divergence] RegionMobSpawner.BuildRoamerDef not found (renamed?) — cannot resolve source 1");
            }
            else
            {
                var def = roamer.Invoke(null, new object[] { id, 0 }) as DeNelle.Village.EnemyDef;
                if (def != null) regionHp = def.Hp;
            }

            // Source 2 — GarrisonStatBlocks.BuildTypedDef (the GarrisonController /
            // RaidGarrisonSpawner path). BuildGenericDef bakes in GlobalDifficultyMult;
            // divide it out to recover the authored base (170) for an apples-to-apples id.
            float garrisonBaseHp = float.NaN;
            var typed = DeNelle.Village.World.Camps.GarrisonStatBlocks.BuildTypedDef(id, 0);
            float mult = DeNelle.Village.World.Camps.GarrisonStatBlocks.GlobalDifficultyMult;
            if (typed != null && mult > 0f) garrisonBaseHp = typed.Hp / mult;

            if (float.IsNaN(regionHp) || float.IsNaN(garrisonBaseHp))
            {
                log.AppendLine($"  [divergence] could not resolve both sources (region={regionHp}, garrisonBase={garrisonBaseHp}) — skipped");
                return;
            }

            log.AppendLine($"  [divergence] orc-raider Hp: RegionMobSpawner={regionHp:0.#} | GarrisonStatBlocks base={garrisonBaseHp:0.#} (x{mult:0.#} global = {typed.Hp:0.#})");

            if (Mathf.Abs(regionHp - garrisonBaseHp) > 0.5f)
            {
                byDesign.Add(
                    $"[FAIL-BY-DESIGN] synthesized orc-raider stat DIVERGENCE — RegionMobSpawner/EnemyOutpost/" +
                    $"CampDefenseWave build it at Hp {regionHp:0.#}, GarrisonController (GarrisonStatBlocks) builds " +
                    $"the SAME id at base Hp {garrisonBaseHp:0.#} (x{mult:0.#} global = {typed.Hp:0.#}). One id, two stat " +
                    $"blocks — unify the Wildlands roster into a single source (enemies.json / a shared table) to clear this red.");
            }
        }

        // =====================================================================
        //  I. KNOWN ATB HARDCODES — assert-and-name where data-decidable, else name
        //     the presentation/play-mode ones as documented skips (INSTRUMENTATION
        //     §4: headless owns data+logic; PlayMode owns render/felt).
        // =====================================================================
        private static void NoteKnownHardcodes(StringBuilder log)
        {
            // F-WAVE-1: BattleState.Wave IS real + scaled (proven here), yet
            // BattleHudUgui.Render hard-codes _waveText.text = "WAVE 1". The data is
            // right; only the HUD label lies — a PRESENTATION bug (not headless-decidable).
            var w7 = BattleStateOps.CreateBattle(MakeSetup(seed: 5, wave: 7));
            log.AppendLine($"  [note] BattleState.Wave carries the real wave ({w7.Wave}) — but BattleHudUgui.Render hard-codes \"WAVE 1\" (F-WAVE-1, play-mode/HUD fix, not assertable headless)");
            // F-SWAP-2: AtbCombatantSwapper.ResolveEnemySlug is hard-coded "Skeleton_Warrior"
            // so the ATB enemy MODEL never varies by the breach roster. Pure play-mode
            // visual resolve (loads Resources / reads GameState) — SKIP with why.
            log.AppendLine("  [note] AtbCombatantSwapper.ResolveEnemySlug hard-codes \"Skeleton_Warrior\" (F-SWAP-2) — ATB enemy visual never varies; play-mode visual concern, not assertable headless");
        }

        // =====================================================================
        //  Shared builders
        // =====================================================================

        /// <summary>A minimal live BattleUnit for damage tests — full control over
        /// Defense/Element/Statuses without routing through the stat tables.</summary>
        private static BattleUnit MakeTarget(double defense, ElementType element)
        {
            return new BattleUnit
            {
                Id = "t", Side = Side.Enemy, Name = "Target", Kind = UnitKind.Enemy,
                ControlMode = ControlMode.AI,
                Hp = 1000, MaxHp = 1000, Resource = 0, MaxResource = 0, ResourceRegen = 0,
                Atb = 0, Speed = 1.0, Defense = defense, Attack = 10, Element = element,
                Statuses = new List<StatusEffect>(),
                Cooldowns = new Dictionary<AbilitySlot, int>(),
                Defending = false, Alive = true,
            };
        }

        /// <summary>A real, minimal Knight-vs-skeleton setup (authoritative multi-member
        /// party path) — the same shape BattleController.BuildSetup produces.</summary>
        private static BattleSetup MakeSetup(int seed, int wave)
        {
            return new BattleSetup
            {
                Wave = wave,
                Seed = seed,
                PartyMembers = new List<PartyMemberSpec>
                {
                    new PartyMemberSpec
                    {
                        Id = "hero", Name = "K", HeroClass = HeroClass.Knight, Species = null,
                        BondRank = 0, AiMode = PetAiMode.Balanced, ControlMode = ControlMode.Player,
                    },
                },
                HeroClass = HeroClass.Knight,
                HeroName = "K",
                Pets = new List<PartyPetSpec>(),
                Enemies = new List<BreachEnemySpec> { new BreachEnemySpec { DefId = "skeleton" } },
                Inventory = new Dictionary<ItemKind, int> { { ItemKind.Potion, 3 } },
                Reinforcements = false,
            };
        }

        // =====================================================================
        //  Verdict + markers
        // =====================================================================
        private static bool Verdict(List<string> failures, List<string> byDesign, StringBuilder log, out string reason)
        {
            if (failures.Count == 0 && byDesign.Count == 0)
            {
                reason = "COMBAT/ATB OK — RNG reproducibility/aliasing + RoundTs half-up + damage invariants " +
                         "(element/floor/pierce/defend/shield/clamp) + turn-order/outcome + wave-scaling monotonicity " +
                         "+ ability cast-gate + enemies.json stat sanity all hold; no stat divergence";
                Debug.Log("COMBAT_OK\n" + log);
                return true;
            }

            var parts = new List<string>();
            if (failures.Count > 0) parts.Add($"{failures.Count} regression(s): " + string.Join(" | ", failures));
            if (byDesign.Count > 0) parts.Add($"{byDesign.Count} fail-by-design: " + string.Join(" | ", byDesign));
            reason = "COMBAT/ATB — " + string.Join("  ||  ", parts);

            Debug.LogError($"COMBAT_FAIL: {failures.Count} regression(s) + {byDesign.Count} fail-by-design\n" + log +
                           (failures.Count > 0 ? "\n REGRESSIONS:\n - " + string.Join("\n - ", failures) : "") +
                           (byDesign.Count > 0 ? "\n FAIL-BY-DESIGN (intentional, keeps a known divergence loud):\n - " + string.Join("\n - ", byDesign) : ""));
            return false;
        }
    }
}
