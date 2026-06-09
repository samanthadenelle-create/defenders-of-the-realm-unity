// SCAFFOLD — CLI must build-verify under Unity Test Framework
// =============================================================================
// AnimParamsTests (EditMode) — WO-329 regression suite.
// -----------------------------------------------------------------------------
// Guards the single source of truth for animator parameters (AnimParams.cs):
//   - every cached hash equals Animator.StringToHash(name) (no drift),
//   - the canonical death latch is the BOOL `Dead` (+ `DeathDir`), NOT the old
//     `Death` trigger — a regression here makes dead actors flicker back to idle,
//   - locomotion blend param is the raw `Speed` float.
// =============================================================================

using NUnit.Framework;
using UnityEngine;
using DeNelle.Core.Combat;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class AnimParamsTests
    {
        [Test]
        public void canonical_names_are_stable()
        {
            Assert.That(AnimParams.Speed, Is.EqualTo("Speed"));
            Assert.That(AnimParams.Dead, Is.EqualTo("Dead"), "death latch is the BOOL 'Dead'");
            Assert.That(AnimParams.DeathDir, Is.EqualTo("DeathDir"));
            Assert.That(AnimParams.InCombat, Is.EqualTo("InCombat"));
            Assert.That(AnimParams.Attack, Is.EqualTo("Attack"));
            Assert.That(AnimParams.Hit, Is.EqualTo("Hit"));
        }

        [Test]
        public void every_hash_matches_string_to_hash()
        {
            Assert.That(AnimParams.SpeedHash, Is.EqualTo(Animator.StringToHash(AnimParams.Speed)));
            Assert.That(AnimParams.InCombatHash, Is.EqualTo(Animator.StringToHash(AnimParams.InCombat)));
            Assert.That(AnimParams.AttackHash, Is.EqualTo(Animator.StringToHash(AnimParams.Attack)));
            Assert.That(AnimParams.ComboHash, Is.EqualTo(Animator.StringToHash(AnimParams.Combo)));
            Assert.That(AnimParams.CastHash, Is.EqualTo(Animator.StringToHash(AnimParams.Cast)));
            Assert.That(AnimParams.WindUpHash, Is.EqualTo(Animator.StringToHash(AnimParams.WindUp)));
            Assert.That(AnimParams.BlockHash, Is.EqualTo(Animator.StringToHash(AnimParams.Block)));
            Assert.That(AnimParams.HitHash, Is.EqualTo(Animator.StringToHash(AnimParams.Hit)));
            Assert.That(AnimParams.HitDirHash, Is.EqualTo(Animator.StringToHash(AnimParams.HitDir)));
            Assert.That(AnimParams.DeadHash, Is.EqualTo(Animator.StringToHash(AnimParams.Dead)));
            Assert.That(AnimParams.DeathDirHash, Is.EqualTo(Animator.StringToHash(AnimParams.DeathDir)));
            Assert.That(AnimParams.VictoryHash, Is.EqualTo(Animator.StringToHash(AnimParams.Victory)));
            Assert.That(AnimParams.TurnDirHash, Is.EqualTo(Animator.StringToHash(AnimParams.TurnDir)));
            Assert.That(AnimParams.EmoteHash, Is.EqualTo(Animator.StringToHash(AnimParams.Emote)));
        }

        [Test]
        public void dead_hash_is_distinct_from_other_params()
        {
            Assert.That(AnimParams.DeadHash, Is.Not.EqualTo(AnimParams.SpeedHash));
            Assert.That(AnimParams.DeadHash, Is.Not.EqualTo(AnimParams.HitHash));
            Assert.That(AnimParams.DeadHash, Is.Not.EqualTo(AnimParams.DeathDirHash));
        }

        [Test]
        public void death_direction_enum_has_canonical_ordering()
        {
            Assert.That((int)DeathDirection.Fall, Is.EqualTo(0));
            Assert.That((int)DeathDirection.Left, Is.EqualTo(1));
            Assert.That((int)DeathDirection.Right, Is.EqualTo(2));
        }
    }
}
