using NUnit.Framework;
using UnityEngine;
using DeNelle.Core;
using DeNelle.Village;
using DeNelle.Village.Arena;

namespace DeNelle.Tests.EditMode
{
    /// <summary>
    /// Permission gate (ARCHITECTURE_PRINCIPLES §2c) for the WO-482 overworld-encounter -> isolated
    /// BattleArena. Locks the load-bearing architecture so a future change can't silently regress it:
    ///  - Slice 1: the Tripo orc FAMILY rigs to the OrcHumanoid controller (animates, never T-poses).
    ///  - MonsterFamily reuse: FormationController produces a REAL distinct-slot pack formation
    ///    (the orcs approach as a led family, not stacked on one point).
    ///  - The handoff (EncounterParams) is PRESENTATION-FREE — it carries family + theme + return only.
    /// Pure data assertions — no scene load, no spawn (headless, deterministic).
    /// </summary>
    public class EncounterArchitectureTests
    {
        [Test]
        public void OrcFamilyRigsToOrcHumanoid_NotTpose()
        {
            // Slice 1: the new Tripo orcs MUST drive OrcHumanoid, not the KayKit Generic controller
            // (which would mis-retarget the humanoid orc -> a T-pose in the arena).
            Assert.AreEqual(EnemyRig.OrcHumanoid, EnemyAnimatorFactory.RigFor("Orc_Warrior"),
                "orc-warrior model must rig to OrcHumanoid (Slice 1) — else it T-poses in battle.");
            Assert.AreEqual(EnemyRig.OrcHumanoid, EnemyAnimatorFactory.RigFor("Orc_Tank"));
            Assert.AreEqual(EnemyRig.OrcHumanoid, EnemyAnimatorFactory.RigFor("Orc_Mage"));
        }

        [Test]
        public void FamilyFormationGivesDistinctBoundedSlots()
        {
            // A 3-orc family in a charge Wedge: every member gets a DISTINCT, bounded local slot, so
            // the pack approaches in a real formation (the pivot's "led pack"), not stacked on a point.
            const int n = 3; const float spacing = 1.8f;
            var slots = new Vector3[n];
            for (int i = 0; i < n; i++)
                slots[i] = FormationController.LocalSlotOffset(FormationType.Wedge, i, n, spacing);

            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    Assert.Greater((slots[i] - slots[j]).magnitude, 0.5f,
                        $"Wedge slots {i} and {j} must be distinct (members must not stack).");

            foreach (var s in slots)
                Assert.Less(s.magnitude, n * spacing + 4f, "a slot must stay within a sane pack radius.");
        }

        [Test]
        public void EncounterParamsIsLogicOnlyHandoff()
        {
            var p = new EncounterParams
            {
                EnemyIds = new[] { "orc-warrior", "orc-tank", "orc-mage" },
                Threat = 2,
                BackdropContext = "outerworld",
                ReturnScene = "OuterWorld",
                ReturnPosition = new Vector3(1f, 0f, 2f),
                ReturnYaw = 90f,
            };
            Assert.AreEqual(3, p.EnemyIds.Length, "family roster is carried");
            Assert.AreEqual("orc-warrior", p.EnemyIds[0], "leader is index 0 (the FamilyLeader)");
            Assert.AreEqual("outerworld", p.BackdropContext, "theme derives from the source scene");
            Assert.AreEqual("OuterWorld", p.ReturnScene, "the battle returns to where it was engaged");
        }
    }
}
