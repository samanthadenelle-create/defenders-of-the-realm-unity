// =============================================================================
// MoverProjectilePool — pooling for HERO + COMPANION ranged projectiles.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHY THIS EXISTS:
//   Hero / companion ranged attacks (RangedAttackVFX.FireArrow / FireSpellOrb)
//   previously Instantiate'd a fresh ProjectileMover-hosting GameObject PER SHOT
//   and Destroy'd it on arrival — at high cadence (companion volleys + sustained
//   fire) that is a per-shot GC + transient-GameObject churn the owner asked us to
//   kill ("use pooling to control strays — we had leaks before").
//
//   The existing ProjectilePool / PooledProjectile (WO-82) is TOWER-ONLY and a
//   DIFFERENT flight model: PooledProjectile HOMES onto an IDamageable and owns its
//   own Update/damage. Hero/companion shots fly an ARC/LERP to a fixed world point
//   via ProjectileMover with an onArrive callback (no homing). The shapes don't
//   match, so this is a SIBLING pool modeled identically on ProjectilePool
//   (self-bootstrap, DontDestroyOnLoad, queue, dead-ref skip) rather than a
//   generalization of the tower pool.
//
// KEYED BY BODY KIND:
//   A Ranger arrow and a Mage orb (and their suppressed placeholders) have
//   different visuals, so they pool SEPARATELY by ProjectileBodyKind. Each pooled
//   body builds its child visual ONCE (placeholder primitive or the cleaned
//   ProjectileVFXCatalog particle FX) and REPLAYS it on lease — so the per-shot
//   flying-FX Instantiate/Destroy is eliminated too.
//
// RESET CONTRACT (kept invisible to gameplay):
//   • Acquire: re-home under the pool, reset transform to the launch origin/rot,
//     re-enable, replay the flying particle FX. ProjectileMover.Launch re-arms
//     speed/arc/distance/timer/onArrive fresh each shot.
//   • Release: stop motion (ProjectileMover clears its launched state), clear any
//     TrailRenderer so the next shot doesn't streak from the old land point,
//     unsubscribe the onArrive payload (Mover clears it after firing), disable.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Combat;   // DamageElement

namespace DeNelle.Village
{
    /// <summary>The distinct hero/companion projectile body visuals; each pools separately.</summary>
    public enum ProjectileBodyKind
    {
        RangerArrowVfx,   // particle-FX storm bolt (Ranger arrow, live default)
        MageOrbVfx,       // particle-FX arcane orb (Mage orb, live default)
        PlaceholderArrow, // code-built brown capsule + trail (dev opt-in)
        PlaceholderOrb,   // code-built blue emissive sphere + trail (dev opt-in)
    }

    /// <summary>Persistent pool of reusable hero/companion ProjectileMover bodies,
    /// keyed by visual kind. Singleton, self-installed, survives scene loads.</summary>
    public sealed class MoverProjectilePool : MonoBehaviour
    {
        public static MoverProjectilePool Instance { get; private set; }

        [SerializeField] private int _initialPerKind = 8;

        // One queue per body kind — an arrow lease never hands back an orb body.
        private readonly Dictionary<ProjectileBodyKind, Queue<ProjectileMover>> _pools
            = new Dictionary<ProjectileBodyKind, Queue<ProjectileMover>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("MoverProjectilePool").AddComponent<MoverProjectilePool>();   // Awake handles DDOL
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private Queue<ProjectileMover> QueueFor(ProjectileBodyKind kind)
        {
            if (!_pools.TryGetValue(kind, out var q))
            {
                q = new Queue<ProjectileMover>();
                _pools[kind] = q;
                for (int i = 0; i < _initialPerKind; i++)
                    q.Enqueue(CreateNew(kind));
            }
            return q;
        }

        // Built under this (DontDestroyOnLoad) pool so pooled bodies survive scene
        // loads instead of being destroyed under the unloading scene (the WO-82 trap).
        private ProjectileMover CreateNew(ProjectileBodyKind kind)
        {
            var go = new GameObject("HeroProjectile_" + kind);
            go.transform.SetParent(transform, false);

            // Build the body's visual ONCE; it persists and replays per lease.
            ProjectileBodyVisual.Build(go, kind);

            var mover = go.AddComponent<ProjectileMover>();
            mover.BindToPool(this, kind);
            go.SetActive(false);
            return mover;
        }

        /// <summary>Lease a projectile body of <paramref name="kind"/> at the given launch
        /// pose (expands if drained, skips any destroyed entries). Caller then calls
        /// <see cref="ProjectileMover.Launch"/> to arm it.</summary>
        public ProjectileMover Acquire(ProjectileBodyKind kind, Vector3 origin, Quaternion rotation)
        {
            var q = QueueFor(kind);

            ProjectileMover mover = null;
            while (mover == null && q.Count > 0) mover = q.Dequeue();   // skip dead refs
            if (mover == null) mover = CreateNew(kind);

            var t = mover.transform;
            t.SetParent(null, false);          // detach so world travel isn't pool-relative
            t.position = origin;
            t.rotation = rotation;

            mover.ResetForLease();             // clears trail + launched state, replays FX
            mover.gameObject.SetActive(true);
            return mover;
        }

        /// <summary>Return a spent body to its kind queue (deactivated + re-homed).</summary>
        public void Release(ProjectileMover mover, ProjectileBodyKind kind)
        {
            if (mover == null) return;
            mover.gameObject.SetActive(false);
            var t = mover.transform;
            if (t.parent != transform) t.SetParent(transform, false);
            QueueFor(kind).Enqueue(mover);
        }
    }

    /// <summary>Builds the persistent child visual for a pooled projectile body. Mirrors the
    /// per-kind visuals RangedAttackVFX used to build per shot — but built ONCE per pooled body
    /// and replayed on lease, so there is no per-shot Instantiate/Destroy of the flying FX.</summary>
    internal static class ProjectileBodyVisual
    {
        public static void Build(GameObject host, ProjectileBodyKind kind)
        {
            switch (kind)
            {
                case ProjectileBodyKind.RangerArrowVfx:
                    // Storm-bolt particle FX for the physical arrow (cleaned, URP-safe).
                    ProjectileVFXCatalog.SpawnFlying(host.transform, DamageElement.None);
                    break;
                case ProjectileBodyKind.MageOrbVfx:
                    ProjectileVFXCatalog.SpawnFlying(host.transform, DamageElement.Aether);
                    break;
                case ProjectileBodyKind.PlaceholderArrow:
                    BuildPlaceholderArrow(host);
                    break;
                case ProjectileBodyKind.PlaceholderOrb:
                    BuildPlaceholderOrb(host);
                    break;
            }
        }

        private static void BuildPlaceholderArrow(GameObject host)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "ArrowPlaceholder";
            go.transform.SetParent(host.transform, false);
            go.transform.localScale = new Vector3(0.06f, 0.35f, 0.06f);
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(0.55f, 0.35f, 0.1f); // brown wood
                rend.material = mat;
            }

            var trail = host.AddComponent<TrailRenderer>();
            trail.time       = 0.12f;
            trail.startWidth = 0.03f;
            trail.endWidth   = 0f;
            trail.material   = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit"));
            trail.startColor = new Color(0.9f, 0.8f, 0.5f, 0.7f);
            trail.endColor   = new Color(0.9f, 0.8f, 0.5f, 0f);
        }

        private static void BuildPlaceholderOrb(GameObject host)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "SpellOrbPlaceholder";
            go.transform.SetParent(host.transform, false);
            go.transform.localScale = Vector3.one * 0.22f;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(0.35f, 0.5f, 1f);  // blue-purple
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.15f, 0.3f, 1f) * 1.8f);
                rend.material = mat;
            }

            var trail = host.AddComponent<TrailRenderer>();
            trail.time       = 0.2f;
            trail.startWidth = 0.15f;
            trail.endWidth   = 0f;
            trail.material   = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit"));
            trail.startColor = new Color(0.4f, 0.55f, 1f, 0.8f);
            trail.endColor   = new Color(0.4f, 0.55f, 1f, 0f);
        }
    }
}
</content>
</invoke>
