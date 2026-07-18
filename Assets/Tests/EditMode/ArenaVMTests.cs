// =============================================================================
// ArenaVMTests (EditMode) — §2c permission gate for the Arena panel MVVM slice.
// Locks the opponent projection + affordability + the toggle/start-raid commands +
// the OnRaidEnded capture that MOVED out of ArenaPanel into the pure ArenaVM. Uses a
// FAKE IArenaBackend (no scene, no ArenaMode singleton, no PlayerPrefs wallet).
// =============================================================================

using System;
using System.Collections.Generic;
using NUnit.Framework;
using DeNelle.Village.Arena;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class ArenaVMTests
    {
        private sealed class FakeArenaBackend : IArenaBackend
        {
            public List<ArenaOpponentDef> Opp = new List<ArenaOpponentDef>
            {
                new ArenaOpponentDef { Id = "arena_a", DisplayName = "Ironhold", Flavour = "camp", Tier = 1, GuardCount = 3, Wager = 50L },
                new ArenaOpponentDef { Id = "arena_b", DisplayName = "Grimwatch", Flavour = "warband", Tier = 2, GuardCount = 6, Wager = 100L },
            };
            public long BalanceValue = 75L;
            public int WinsValue, LossesValue, StreakValue;
            public bool CastleFlag;
            public int StartCalls;
            public bool StartResult = true;
            public int AttackCalls, DefenseCalls;

            public IReadOnlyList<ArenaOpponentDef> Opponents => Opp;
            public long Balance => BalanceValue;
            public int Wins => WinsValue;
            public int Losses => LossesValue;
            public int Streak => StreakValue;
            public bool CanAfford(long wager) => BalanceValue >= wager;
            public bool UsePlayerCastle { get => CastleFlag; set => CastleFlag = value; }
            public bool TryStartRaid(ArenaOpponentDef opponent) { StartCalls++; return StartResult; }
            public bool BeginAttack() { AttackCalls++; return true; }
            public bool BeginDefense() { DefenseCalls++; return true; }
            public event Action<ArenaOpponentDef, ArenaResult, long> RaidEnded;
            public void FireRaidEnded(ArenaOpponentDef o, ArenaResult r, long d) => RaidEnded?.Invoke(o, r, d);
        }

        [Test]
        public void opponents_project_name_wager_and_affordability()
        {
            var b = new FakeArenaBackend { BalanceValue = 75L };   // affords 50, not 100
            using var vm = new ArenaVM(b, null);

            Assert.That(vm.Opponents.Count, Is.EqualTo(2));
            Assert.That(vm.Opponents[0].Name, Is.EqualTo("Ironhold"));
            Assert.That(vm.Opponents[0].Price, Is.EqualTo(50));
            Assert.That(vm.Opponents[0].Affordable, Is.True,  "75 SKR affords the 50 stake");
            Assert.That(vm.Opponents[1].Affordable, Is.False, "75 SKR does not afford the 100 stake");

            Assert.That(vm.FlavourFor("arena_a"), Is.EqualTo("camp"));
            Assert.That(vm.TierFor("arena_b"), Is.EqualTo(2));
            Assert.That(vm.GuardCountFor("arena_b"), Is.EqualTo(6));
            Assert.That(vm.WinPurseFor("arena_a"), Is.EqualTo(100L));
        }

        [Test]
        public void toggle_use_my_castle_mutates_and_fires_changed()
        {
            var b = new FakeArenaBackend();
            using var vm = new ArenaVM(b, null);
            int changed = 0; vm.Changed += () => changed++;

            Assert.That(vm.UsePlayerCastle, Is.False);
            vm.ToggleUseMyCastle();
            Assert.That(b.CastleFlag, Is.True, "toggle must flip the backend flag");
            Assert.That(vm.UsePlayerCastle, Is.True);
            Assert.That(vm.DefenderLabel, Is.EqualTo("My Castle"));
            Assert.That(changed, Is.GreaterThan(0), "toggle must raise Changed");
        }

        [Test]
        public void try_start_raid_routes_to_backend()
        {
            var b = new FakeArenaBackend { StartResult = true };
            using var vm = new ArenaVM(b, null);
            Assert.That(vm.TryStartRaid("arena_a"), Is.True);
            Assert.That(b.StartCalls, Is.EqualTo(1));

            Assert.That(vm.TryStartRaid("nope"), Is.False, "unknown id must not call the backend");
            Assert.That(b.StartCalls, Is.EqualTo(1));
        }

        [Test]
        public void begin_attack_and_defense_route_to_backend()
        {
            var b = new FakeArenaBackend();
            using var vm = new ArenaVM(b, null);
            Assert.That(vm.BeginAttack(), Is.True);
            Assert.That(vm.BeginDefense(), Is.True);
            Assert.That(b.AttackCalls, Is.EqualTo(1));
            Assert.That(b.DefenseCalls, Is.EqualTo(1));
        }

        [Test]
        public void raid_ended_push_captures_result_and_fires()
        {
            var b = new FakeArenaBackend();
            using var vm = new ArenaVM(b, null);
            int ended = 0; vm.RaidEnded += () => ended++;

            b.FireRaidEnded(b.Opp[1], ArenaResult.Win, 100L);

            Assert.That(ended, Is.EqualTo(1), "the VM must re-raise RaidEnded");
            Assert.That(vm.LastOpponentName, Is.EqualTo("Grimwatch"));
            Assert.That(vm.LastResult, Is.EqualTo(ArenaResult.Win));
            Assert.That(vm.LastDelta, Is.EqualTo(100L));
        }

        [Test]
        public void dispose_unsubscribes_from_raid_ended()
        {
            var b = new FakeArenaBackend();
            var vm = new ArenaVM(b, null);
            int ended = 0; vm.RaidEnded += () => ended++;
            vm.Dispose();
            b.FireRaidEnded(b.Opp[0], ArenaResult.Loss, -50L);
            Assert.That(ended, Is.EqualTo(0), "after Dispose the VM must not react to RaidEnded");
        }
    }
}
