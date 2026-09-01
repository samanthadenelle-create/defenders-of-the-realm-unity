using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Mobile-cheap attack-token broker. Movement and target selection remain owned by
    /// EnemyBrain; this class only bounds how many enemies may commit an attack against
    /// the same target at once.
    /// </summary>
    public static class EnemyAttackDirector
    {
        private sealed class Lease
        {
            public Enemy Attacker;
            public Object Target;
        }

        private static readonly Dictionary<int, Lease> ByAttacker = new Dictionary<int, Lease>();
        private static readonly Dictionary<int, int> CountByTarget = new Dictionary<int, int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ByAttacker.Clear();
            CountByTarget.Clear();
        }

        public static bool TryAcquire(Enemy attacker, Object target, int maxCommitters)
        {
            if (attacker == null || target == null) return false;
            PruneDeadLeases();

            int attackerId = attacker.GetInstanceID();
            if (ByAttacker.ContainsKey(attackerId)) return true;

            int targetId = target.GetInstanceID();
            CountByTarget.TryGetValue(targetId, out int count);
            if (count >= Mathf.Max(1, maxCommitters)) return false;

            ByAttacker.Add(attackerId, new Lease { Attacker = attacker, Target = target });
            CountByTarget[targetId] = count + 1;
            return true;
        }

        public static void Release(Enemy attacker)
        {
            if (attacker == null) return;
            Release(attacker.GetInstanceID());
        }

        private static void Release(int attackerId)
        {
            if (!ByAttacker.TryGetValue(attackerId, out Lease lease)) return;
            ByAttacker.Remove(attackerId);
            if (lease.Target == null) return;

            int targetId = lease.Target.GetInstanceID();
            if (!CountByTarget.TryGetValue(targetId, out int count)) return;
            if (count <= 1) CountByTarget.Remove(targetId);
            else CountByTarget[targetId] = count - 1;
        }

        private static void PruneDeadLeases()
        {
            if (ByAttacker.Count == 0) return;
            List<int> stale = null;
            foreach (var pair in ByAttacker)
            {
                Lease lease = pair.Value;
                if (lease == null || lease.Attacker == null || lease.Attacker.IsDead || lease.Target == null)
                {
                    if (stale == null) stale = new List<int>();
                    stale.Add(pair.Key);
                }
            }
            if (stale == null) return;
            for (int i = 0; i < stale.Count; i++) Release(stale[i]);
        }
    }
}
