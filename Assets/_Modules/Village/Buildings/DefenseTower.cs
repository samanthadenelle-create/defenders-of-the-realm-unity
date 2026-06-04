// =============================================================================
// DefenseTower — the auto-firing defensive structure (defensive-catalog v0 test).
// -----------------------------------------------------------------------------
// Proves "placement = role": an Archer tower on the GROUND (CanHitAir = false,
// short range) can't touch a flying dragon; a Wizard tower on the WALL-WALK
// (CanHitAir = true, long range, elevated) can. Targeting is by ROLE PRIORITY —
// the owner's "scamper to the DPS and healers" — squishy backline first.
//
// Reuses: IDamageable (DeNelle.Core.Combat) for find+damage, EnemyBrain.Role for
// priority, ProjectileMover for the visual bolt. All data-tunable.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Combat;

namespace DeNelle.Village
{
    public sealed class DefenseTower : MonoBehaviour
    {
        public float Range       = 14f;
        public float Damage      = 8f;
        public float FireRate    = 1.2f;   // shots per second
        public bool  CanHitAir   = false;  // ground archers: false · wall wizards: true
        public float AirThreshold = 3.5f;  // target above this Y counts as "flying"
        public Color BoltColor   = Color.white;
        public DamageElement Element = DamageElement.None;

        private float _cd;
        private float _scan;
        private readonly List<IDamageable> _hostiles = new List<IDamageable>();

        private void Update()
        {
            _scan -= Time.deltaTime;
            if (_scan <= 0f) { Rescan(); _scan = 0.4f; }   // refresh target list a few times/sec

            _cd -= Time.deltaTime;
            if (_cd > 0f) return;
            var target = Acquire();
            if (target == null) return;
            _cd = 1f / Mathf.Max(0.1f, FireRate);
            Fire(target);
        }

        private void Rescan()
        {
            _hostiles.Clear();
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (mb is IDamageable d && d.Faction == CombatFaction.Hostile)
                    _hostiles.Add(d);
        }

        private IDamageable Acquire()
        {
            IDamageable best = null;
            int   bestPri = int.MaxValue;
            float bestSqr = float.MaxValue;
            foreach (var d in _hostiles)
            {
                if (d == null || !d.IsAlive) continue;
                Vector3 p = d.WorldPosition;
                float sqr = (p - transform.position).sqrMagnitude;
                if (sqr > Range * Range) continue;
                if (p.y > AirThreshold && !CanHitAir) continue;   // ground tower can't reach a flier
                int pri = Priority(d);
                if (pri < bestPri || (pri == bestPri && sqr < bestSqr))
                {
                    bestPri = pri; bestSqr = sqr; best = d;
                }
            }
            return best;
        }

        // "Scamper to the DPS and healers" — squishy backline first, tanks last.
        private static int Priority(IDamageable d)
        {
            var mb = d as MonoBehaviour;
            var brain = mb != null ? mb.GetComponent<EnemyBrain>() : null;
            if (brain == null) return 2;   // bosses / unknown — middling
            switch (brain.Role)
            {
                case EnemyRole.Healer:   return 0;   // kill the healer first
                case EnemyRole.Ranged:   return 1;
                case EnemyRole.DPS:      return 1;
                case EnemyRole.MiniBoss: return 2;
                case EnemyRole.Tank:     return 3;   // tanks last
                default:                 return 2;
            }
        }

        private void Fire(IDamageable target)
        {
            Vector3 muzzle = transform.position + Vector3.up * 2f;

            var bolt = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bolt.name = "Bolt";
            bolt.transform.localScale = Vector3.one * 0.4f;
            var col = bolt.GetComponent<Collider>(); if (col != null) Destroy(col);
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh != null)
            {
                var m = new Material(sh);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", BoltColor);
                if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", BoltColor * 2f); }
                var r = bolt.GetComponent<Renderer>(); if (r != null) r.sharedMaterial = m;
            }
            bolt.transform.position = muzzle;
            bolt.AddComponent<ProjectileMover>().Launch(target.WorldPosition + Vector3.up * 1f, 40f, CanHitAir ? 0.1f : 0.35f);

            target.TakeDamage(Damage, Element);   // hitscan damage on fire (bolt is the feel)
        }
    }
}
