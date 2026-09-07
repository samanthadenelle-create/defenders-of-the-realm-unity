using System;
using System.Reflection;
using DeNelle.Village;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>Focused executable proof for attack concurrency and occupancy contracts.</summary>
    public static class CombatFoundationRegression
    {
        // WO-1496: this suite asserts by THROWING (Require -> InvalidOperationException).
        // Registered in DataRegression.RunAll through the wrapper below, which turns the
        // throw into a red reason string. It must not be registered as the raw void Run():
        // a throw inside Guard.Try is swallowed, the suite emits neither a [tag] line nor a
        // failure, and it vanishes from the denominator (the G1 shortfall this WO exists for).
        public static bool Run(out string reason)
        {
            try
            {
                Run();
                reason = "COMBAT FOUNDATION OK -- fodder attack tokens cap at 2 and are reusable after " +
                         "Release, the elite committer slot is exclusive, EnemyOccupancySlot seats are " +
                         "single-occupant and reusable, both HitFrame seams exist, and the mobile " +
                         "telegraph/recover floors + the 4 Hz occupancy budget hold";
                return true;
            }
            catch (Exception ex)
            {
                reason = "COMBAT FOUNDATION: " + ex.GetType().Name + ": " + ex.Message;
                Debug.LogError("COMBAT_FOUNDATION_REGRESSION_FAIL " + reason);
                return false;
            }
        }

        public static void Run()
        {
            GameObject targetA = null, targetB = null, a = null, b = null, c = null;
            try
            {
                targetA = new GameObject("combat-target-a");
                targetB = new GameObject("combat-target-b");
                a = NewEnemy("attacker-a"); b = NewEnemy("attacker-b"); c = NewEnemy("attacker-c");
                var ea = a.GetComponent<Enemy>();
                var eb = b.GetComponent<Enemy>();
                var ec = c.GetComponent<Enemy>();

                Require(EnemyAttackDirector.TryAcquire(ea, targetA, 2), "first fodder token denied");
                Require(EnemyAttackDirector.TryAcquire(eb, targetA, 2), "second fodder token denied");
                Require(!EnemyAttackDirector.TryAcquire(ec, targetA, 2), "third fodder token incorrectly granted");
                EnemyAttackDirector.Release(ea);
                Require(EnemyAttackDirector.TryAcquire(ec, targetA, 2), "released token was not reusable");

                EnemyAttackDirector.Release(eb); EnemyAttackDirector.Release(ec);
                Require(EnemyAttackDirector.TryAcquire(ea, targetB, 1), "elite token denied");
                Require(!EnemyAttackDirector.TryAcquire(eb, targetB, 1), "second elite committer incorrectly granted");

                var slot = targetA.AddComponent<EnemyOccupancySlot>();
                slot.ConfigureRuntime("regression-sentry", EnemyOccupancyRole.Sentry, targetB.transform);
                Require(slot.TryReserve(ea), "occupancy reservation denied");
                Require(!slot.TryReserve(eb), "double occupancy incorrectly granted");
                slot.Release(ea);
                Require(slot.TryReserve(eb), "released occupancy seat was not reusable");

                Require(typeof(Enemy).GetMethod("OnAnimationHitFrame") != null,
                    "Enemy HitFrame gameplay seam missing");
                Require(typeof(EnemyAnimationEventRelay).GetMethod("HitFrame") != null,
                    "visual-rig HitFrame relay missing");

                Require(ReadPrivateConstant<float>(typeof(Enemy), "ContactTelegraphFloor") >= 1f,
                    "mobile telegraph floor is below one second");
                Require(ReadPrivateConstant<float>(typeof(Enemy), "ContactRecoverSeconds") >= 0.4f,
                    "recovery window is too short to read");
                Require(Mathf.Approximately(
                        ReadPrivateConstant<float>(typeof(EnemyOccupancyAgent), "TickSeconds"), 0.25f),
                    "occupancy decisions are not budgeted at 4 Hz");

                Debug.Log("COMBAT_FOUNDATION_REGRESSION_OK tokens=2/2 elite=1/1 occupancy=exclusive " +
                          "hitframe=present telegraph>=1s recover>=0.4s occupancyTick=4Hz");
            }
            finally
            {
                if (a != null) UnityEngine.Object.DestroyImmediate(a);
                if (b != null) UnityEngine.Object.DestroyImmediate(b);
                if (c != null) UnityEngine.Object.DestroyImmediate(c);
                if (targetA != null) UnityEngine.Object.DestroyImmediate(targetA);
                if (targetB != null) UnityEngine.Object.DestroyImmediate(targetB);
            }
        }

        private static GameObject NewEnemy(string name)
        {
            var go = new GameObject(name);
            go.AddComponent<Enemy>();
            return go;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Combat foundation regression: " + message);
        }

        private static T ReadPrivateConstant<T>(Type type, string name)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
            if (field == null) throw new InvalidOperationException($"Missing {type.Name}.{name}");
            return (T)field.GetRawConstantValue();
        }
    }
}
