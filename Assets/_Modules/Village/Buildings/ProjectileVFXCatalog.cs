// =============================================================================
// ProjectileVFXCatalog — replaces the old billboard-sprite projectile look with
// REAL particle VFX from the owner's packs (Spells Pack + Lana Studio).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHY THIS EXISTS:
//   The previous projectile visual was a camera-facing 2D sprite quad
//   (ProjectileSpriteBillboard) + a procedural amber cube/trail fallback. The
//   owner asked to "remove it and use VFX instead". This catalog spawns a real
//   particle-system projectile as the FLYING visual and a particle burst on
//   IMPACT, while the existing projectile LOGIC (travel / damage / targeting)
//   is untouched — we only swap the visual.
//
// SOURCE PREFABS (mirrored, WebGL-safe, into Resources/VFX/Projectiles):
//   • Flying body : Spells Pack  Projectile_<Element>   (a flying particle FX)
//   • Impact burst: Spells Pack  Explosion_<Element>    (matched explosion FX)
//   • Generic pop : Lana Studio  Flash_* / Burst_*      (crisp neutral bursts)
//
//   A Resources.Load on any of these pulls its referenced materials + textures
//   into the build automatically (no AssetDatabase / no StreamingAssets / no
//   File IO → IL2CPP + WebGL safe). Mirrors the ProjectileArtCatalog load path.
//
// THE FLYING PREFAB IS USED AS A PURE VISUAL:
//   The Spells Pack flying prefab ships with a demo `Projectile` MonoBehaviour +
//   Rigidbody + SphereCollider that would try to MOVE/collide on its own. We are
//   driving travel from our own logic (PooledProjectile / ProjectileMover), so on
//   spawn we STRIP every non-visual component (Rigidbody, Collider, and any
//   MonoBehaviour that isn't a ParticleSystem renderer) and parent the cleaned FX
//   to our mover. It then rides exactly the path the logic already drives.
//
// URP SAFETY (important):
//   Both packs author their particle materials against BUILT-IN particle shaders.
//   Unimported, those render MAGENTA under URP. Rather than depend on the owner
//   importing each pack's "URP upgrade" unitypackage, we remap built-in particle
//   shaders → the URP particle shader at spawn (cached per material). Mirrors the
//   established TripoMaterialFixer self-heal pattern.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Combat;

namespace DeNelle.Village
{
    /// <summary>Resolves + spawns real particle VFX for a projectile's flying body and
    /// its impact burst, by DamageElement / tower name. Lazy, cached, WebGL-safe.</summary>
    public static class ProjectileVFXCatalog
    {
        private const string Root = "VFX/Projectiles/";

        // Cached loaded source prefabs (by Resources path), and the "never found" set
        // so a missing prefab logs once and is skipped thereafter.
        private static readonly Dictionary<string, GameObject> _prefabCache
            = new Dictionary<string, GameObject>();
        private static readonly HashSet<string> _missing = new HashSet<string>();

        // ---------------------------------------------------------------------
        // ELEMENT → PREFAB MAP
        // ---------------------------------------------------------------------
        // The project's DamageElement enum is intentionally small (None/Aether/Flame/Ice).
        // For the FLYING body, physical/None uses the Storm bolt — a fast, bright,
        // arrow-like streak that reads well for an archer/ballista shot.
        private static string FlyingPath(DamageElement element) => element switch
        {
            DamageElement.Aether => Root + "Projectile_Arcane",
            DamageElement.Flame  => Root + "Projectile_Fire",
            DamageElement.Ice    => Root + "Projectile_Ice",
            _                    => Root + "Projectile_Storm", // None / Physical → fast storm bolt
        };

        // Impact burst. Matched Spells explosion per element; physical pops a crisp
        // neutral Lana flash so a plain arrow/storm hit still reads.
        private static string ImpactPath(DamageElement element) => element switch
        {
            DamageElement.Aether => Root + "Explosion_Arcane",
            DamageElement.Flame  => Root + "Explosion_Fire",
            DamageElement.Ice    => Root + "Explosion_Ice",
            _                    => Root + "Explosion_Storm", // None / Physical → storm crackle
        };

        // ---------------------------------------------------------------------
        // PUBLIC API
        // ---------------------------------------------------------------------

        /// <summary>Spawn the flying VFX body for <paramref name="element"/>, parented to
        /// <paramref name="mover"/> at its origin so it rides the logic-driven path. The FX
        /// is cleaned of physics/demo-scripts (pure visual). Returns the spawned instance
        /// (or null if the prefab is missing). Caller may reuse/destroy it.</summary>
        public static GameObject SpawnFlying(Transform mover, DamageElement element)
        {
            if (mover == null) return null;
            var prefab = Load(FlyingPath(element));
            if (prefab == null) return null;

            var go = Object.Instantiate(prefab, mover.position, mover.rotation, mover);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            CleanToVisualOnly(go);
            FixUrpShaders(go);
            PlayAll(go);
            return go;
        }

        /// <summary>Re-skin a pooled projectile's flying VFX for a (possibly new) element:
        /// destroys the previous visual and spawns the element-matched one fresh. Always
        /// correct (never shows a stale element); the per-shot Instantiate is cheap for a
        /// short-lived particle body. Returns the live instance (or null if missing).</summary>
        public static GameObject ReskinFlying(GameObject current, Transform mover, DamageElement element)
        {
            if (current != null) Object.Destroy(current);
            return SpawnFlying(mover, element);
        }

        /// <summary>Re-play the persistent flying particle FX already parented under
        /// <paramref name="host"/> (built ONCE by MoverProjectilePool and reused per lease).
        /// Clears + restarts every child ParticleSystem so a pooled body's flying streak is
        /// fresh each shot without a per-shot Instantiate/Destroy. No-op when there is no FX
        /// body (e.g. a placeholder body whose visual is a mesh + TrailRenderer).</summary>
        public static void ReplayFlying(GameObject host)
        {
            if (host == null) return;
            PlayAll(host);
        }

        /// <summary>Fire a one-shot impact burst for <paramref name="element"/> at
        /// <paramref name="position"/>; it self-destroys after its particle lifetime.
        /// No-op when the prefab is missing.</summary>
        public static void SpawnImpact(Vector3 position, DamageElement element)
        {
            var prefab = Load(ImpactPath(element));
            if (prefab == null) return;

            // Pool the impact burst (GC-free, no per-shot transient GameObject) when the
            // pool is up; fall back to the legacy Instantiate+Destroy otherwise.
            if (ImpactFXPool.Instance != null)
            {
                ImpactFXPool.Instance.Play(prefab, position, Quaternion.identity, -1f, prepared: false);
                return;
            }

            var go = Object.Instantiate(prefab, position, Quaternion.identity);
            CleanToVisualOnly(go);
            FixUrpShaders(go);
            PlayAll(go);
            Object.Destroy(go, LongestLifetime(go) + 0.3f);
        }

        // ---------------------------------------------------------------------
        // POOL HOOKS (used by ImpactFXPool — keeps the clean/URP/replay logic here)
        // ---------------------------------------------------------------------

        /// <summary>Prepare a freshly-Instantiated catalog FX instance for pooled reuse:
        /// strip physics/demo-scripts and remap built-in particle shaders to URP. Called
        /// ONCE by the pool when it first builds a body for a given prefab.</summary>
        internal static void PreparePooledInstance(GameObject go)
        {
            if (go == null) return;
            CleanToVisualOnly(go);
            FixUrpShaders(go);
        }

        /// <summary>Replay all particle systems on a pooled FX body.</summary>
        internal static void ReplayPooled(GameObject go) => PlayAll(go);

        /// <summary>The on-screen lifetime (seconds) of a pooled FX body, so the pool knows
        /// when to reclaim it.</summary>
        internal static float PooledLifetime(GameObject go) => LongestLifetime(go) + 0.3f;

        // ---------------------------------------------------------------------
        // LOAD + CLEAN + URP FIXUP
        // ---------------------------------------------------------------------

        private static GameObject Load(string path)
        {
            if (_prefabCache.TryGetValue(path, out var cached)) return cached;
            if (_missing.Contains(path)) return null;

            GameObject prefab = null;
            try { prefab = Resources.Load<GameObject>(path); }
            catch (System.Exception e)
            {
                Debug.LogWarning("[ProjectileVFXCatalog] load failed for " + path + ": " + e.Message);
            }

            if (prefab == null)
            {
                _missing.Add(path);
                Debug.LogWarning("[ProjectileVFXCatalog] missing prefab '" + path +
                    "' — projectile will fly with no FX body. Ensure it exists under " +
                    "Assets/Resources/" + path + ".prefab");
                return null;
            }
            _prefabCache[path] = prefab;
            return prefab;
        }

        /// <summary>Strip everything that isn't a pure visual: the Spells demo Projectile
        /// script, Rigidbody, and Colliders — so the FX is a passive child that follows the
        /// logic mover instead of moving / colliding on its own.</summary>
        private static void CleanToVisualOnly(GameObject go)
        {
            // Rigidbodies (root + children).
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
                Object.Destroy(rb);

            // Colliders (so it never triggers anything; our logic owns hit detection).
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
                Object.Destroy(col);

            // Any MonoBehaviour that isn't ours — the pack's demo Projectile mover/exploder.
            // (ParticleSystem / renderers / TrailRenderer are NOT MonoBehaviours, so they
            // survive. A null entry = a missing-script ref on our Resources copy; skip it.)
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null) Object.Destroy(mb);
        }

        // Built-in particle shader → URP particle shader remap (cached per material so we
        // don't thrash). Only touches materials still pointing at a built-in/legacy
        // particle shader (the magenta case); leaves correctly-authored URP mats alone.
        private static Shader _urpParticleShader;
        private static bool _urpLookupDone;
        private static readonly HashSet<Material> _fixedMaterials = new HashSet<Material>();

        private static void FixUrpShaders(GameObject go)
        {
            if (!_urpLookupDone)
            {
                _urpLookupDone = true;
                _urpParticleShader =
                    Shader.Find("Universal Render Pipeline/Particles/Unlit")
                    ?? Shader.Find("Universal Render Pipeline/Particles/Lit");
            }
            if (_urpParticleShader == null) return; // not URP, or shader stripped — leave as-is

            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || m.shader == null) continue;
                    if (_fixedMaterials.Contains(m)) continue;

                    string sn = m.shader.name;
                    bool builtinParticle =
                        sn == "Particles/Standard Unlit" ||
                        sn == "Particles/Standard Surface" ||
                        sn.StartsWith("Legacy Shaders/Particles") ||
                        sn.StartsWith("Particles/") ||
                        sn == "Hidden/InternalErrorShader"; // already-magenta → recover

                    if (builtinParticle)
                    {
                        m.shader = _urpParticleShader;
                        changed = true;
                    }
                    _fixedMaterials.Add(m); // examined once either way
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        // ---------------------------------------------------------------------
        // PARTICLE HELPERS
        // ---------------------------------------------------------------------

        private static void PlayAll(GameObject go)
        {
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Clear();
                ps.Play();
            }
        }

        private static float LongestLifetime(GameObject go)
        {
            float max = 0f;
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var m = ps.main;
                float d = m.duration + m.startLifetime.constantMax;
                if (d > max) max = d;
            }
            return Mathf.Max(max, 0.5f);
        }
    }
}
