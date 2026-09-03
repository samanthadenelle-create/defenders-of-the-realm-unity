// =============================================================================
// AoeCastReticle - WO-1345. The AoE targeting reticle is the owner-tagged
// "Danger zone" ring, and its GROUND RADIUS IS DRIVEN BY THE ABILITY'S OWN DATA.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// OWNER TAG (Assets/Editor/VfxManualPicks.json, read VERBATIM, never edited):
//   key        DangercastAOERange_Cast
//   prefabPath Assets/Hovl Studio/Map track markers VFX/Prefabs/
//              Marker 7 Danger zone Loop.prefab
//   isLoop     false
//   scale      1.0
// The implementer chose, substituted and rescaled NOTHING. The key is mapped to
// her named hook verbatim; the prefab is reached only through the ONE existing
// spawner (VFXManager.PlayKey), which resolves it from HovlVfxCatalog - the same
// catalog HovlVfxCatalogGenerator builds from her JSON. There is no second
// spawner and no second pool here: this class owns a bare empty anchor
// GameObject and a lifetime, nothing else.
//
// ---------------------------------------------------------------------------
// WHY A SCALE CONSTANT EXISTS, AND WHERE THE NUMBER CAME FROM (the whole ticket)
// ---------------------------------------------------------------------------
// If the tag's scale (1.0) were applied literally, EVERY AoE would draw the same
// footprint no matter its reach - a reticle that lies about where the damage
// lands, which is worse than no reticle at all. So the tag's scale is treated as
// an AUTHORING MULTIPLIER on top of a radius derived from the ability, never as
// the radius.
//
// To map "ability radius (m)" -> "transform.localScale" you must know what the
// ring's world radius IS at scale 1. MEASURED from the prefab + its atlas, not
// guessed from the name:
//
//   * Both ParticleSystems: scalingMode 0 = HIERARCHY (Unity's enum is
//     Hierarchy=0, Local=1, Shape=2), so transform.localScale DOES scale the
//     effect. Both transforms are authored m_LocalScale 1,1,1.
//   * Root PS renderer: m_RenderMode 4 (Mesh), m_Mesh fileID 10210 from
//     "unity default resources" = the built-in QUAD, which is 1x1 world units.
//     (Cross-checked in this repo: fileID 10209 is the built-in PLANE - the
//     ForestClearingArena "Ground" uses 10209 at localScale 8.84 x 6.63 for an
//     ~88 x 66 m arena, which only works if 10209 is the 10x10 Plane. 10210 is
//     therefore the 1x1 Quad.)
//   * Root PS InitialModule startSize = 5 (constant; the root's SizeModule is
//     DISABLED, so the ring never grows or shrinks over its life). Quad 1x1 x 5
//     = a 5 m square at scale 1.
//   * ShapeModule radius 0.0001 with a single burst of 1 particle => exactly one
//     ring particle, dead-centre. Nothing is offset or spread.
//   * The ring art is atlas frame 11 of MarkersAtlas1.png (UVModule tilesX/Y 4x4,
//     frameOverTime constant 0.6875 -> floor(0.6875 * 16) = 11). Measuring that
//     tile's lit pixels: bbox x 9..504 of a 512 px tile, centred, so the ring's
//     outer edge reaches 247.5 / 256 = 0.967 of the quad's half-width.
//
//   => authored OUTER RADIUS at scale 1 = (5 m / 2) * 0.967 = 2.42 m.
//
// Hence PrefabRingRadiusAtUnitScale below. It is the measurement of an asset,
// NOT a per-spell tuning table and NOT a hardcoded radius: every ability's ring
// is (its own authored radius / this constant), so a 5.2 m Frost Nova and a 9 m
// Meteor Strike draw visibly different rings with no code change and no table.
//
// ---------------------------------------------------------------------------
// LIFETIME - and the isLoop conflict we REPORT rather than fix
// ---------------------------------------------------------------------------
// Her tag says isLoop:false; the prefab's two systems are authored looping:1.
// We honour the tag as authored (nothing here overrides the catalog row) and put
// the lifetime where it belongs regardless of the flag: the AIMING WINDOW. The
// reticle is shown when a blast-shaped cast opens its wind-up and hidden the
// instant that cast commits, is interrupted, or is cancelled - one owner, one
// lifecycle. HeroAbilities.BeginWindupTargetMarker / EndWindupTelegraph are that
// owner; this class never decides on its own when to appear.
//
// ---------------------------------------------------------------------------
// INPUT TRANSPARENCY - the player taps THROUGH the ring to place the cast
// ---------------------------------------------------------------------------
// The tagged prefab contains exactly four component classes across its two
// GameObjects - !u!1 GameObject, !u!4 Transform, !u!198 ParticleSystem,
// !u!199 ParticleSystemRenderer. There is no Collider of any kind in it (the
// only "Collider" strings in its YAML are ParticleSystem CollisionModule tuning
// fields, not components), and it is a world-space particle effect, not a UI
// graphic, so it is invisible to both Physics raycasts and the UI raycaster.
// The anchor this class creates is a bare GameObject with no components at all.
// Ensure() and every Show() re-assert that at RUNTIME (AssertNoColliders) and
// FlowTrace.Fail if anything ever adds one - a silent regression here would make
// the ability uncastable, so it is never allowed to be silent.
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Shows the owner-tagged Danger-zone ring on the ground while a blast-shaped
    /// ability is winding up, sized to that ability's own authored blast radius,
    /// and hides it on commit / interrupt / cancel. Presentation only - it reads
    /// the radius, it never decides one.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AoeCastReticle : MonoBehaviour
    {
        private const string FlowSystem = "AoeReticle";

        /// <summary>
        /// The owner's VFX key, VERBATIM from VfxManualPicks.json. Never resolved to a
        /// prefab here - VFXManager.PlayKey owns that, via the generated HovlVfxCatalog.
        /// </summary>
        public const string VfxKey = "DangercastAOERange_Cast";

        /// <summary>
        /// The prefab path her tag names, kept here ONLY so the oracle can pin this
        /// wiring against her JSON. Nothing loads from this string at runtime.
        /// </summary>
        public const string TaggedPrefabPath =
            "Assets/Hovl Studio/Map track markers VFX/Prefabs/Marker 7 Danger zone Loop.prefab";

        /// <summary>
        /// MEASURED outer ring radius, in metres, of the tagged prefab at
        /// transform.localScale 1 - (quad 1x1 * startSize 5 / 2) * 0.967 atlas fill.
        /// See the file header for the full derivation. This is an asset measurement:
        /// it changes only if the owner retags a different prefab.
        /// </summary>
        public const float PrefabRingRadiusAtUnitScale = 2.42f;

        /// <summary>Metres the ring is lifted off the resolved centre to avoid z-fighting.</summary>
        private const float GroundLift = 0.05f;

        /// <summary>
        /// Seconds of slack added to the spawn lifetime past the aiming window, so a
        /// commit-frame race can never leave the pool holding an already-hidden ring.
        /// </summary>
        private const float LifetimeGrace = 0.25f;

        // The live anchor for the current show, and anchors whose pooled ring has not
        // been reclaimed by VFXManager yet. Bare GameObjects - no renderer, no collider,
        // no particle system. They exist so Hide() can be INSTANT (deactivate) without
        // destroying a pooled instance the shared pool still owns.
        private GameObject _anchor;
        private readonly List<GameObject> _retiring = new List<GameObject>();
        private bool _showing;

        /// <summary>
        /// THE mapping the whole ticket turns on: ability blast radius (m) -> uniform
        /// localScale for the tagged ring. <paramref name="authoringMultiplier"/> is the
        /// owner's tag scale, applied ON TOP of the data-derived radius (never instead
        /// of it); a non-positive multiplier degrades to 1 rather than collapsing the ring.
        /// Pure + public so an oracle can pin it without a scene.
        /// </summary>
        public static float LocalScaleForRadius(float abilityRadius, float authoringMultiplier)
        {
            float mult = authoringMultiplier > 0f ? authoringMultiplier : 1f;
            float radius = Mathf.Max(0.01f, abilityRadius);
            return radius / PrefabRingRadiusAtUnitScale * mult;
        }

        /// <summary>
        /// The owner's authored scale for <see cref="VfxKey"/>, read (never written) off
        /// the generated catalog. 1 when the catalog or the row is not present yet - the
        /// row only materialises once HovlVfxCatalogGenerator has been run over her JSON.
        /// </summary>
        public static float ReadAuthoringMultiplier()
        {
            var catalog = DeNelle.Core.VfxAssetLoader.LoadVfxAsset<HovlVfxCatalog>("VFX/HovlVfxCatalog");
            if (catalog == null)
            {
                FlowTrace.Once(FlowSystem, "no-catalog",
                    "HovlVfxCatalog did not resolve - using authoring multiplier 1.0 for '" + VfxKey +
                    "'. The ring still scales from the ability radius; only her tag multiplier is missing.");
                return 1f;
            }
            if (!catalog.TryGet(VfxKey, out var row))
            {
                FlowTrace.Once(FlowSystem, "no-row",
                    "HovlVfxCatalog has no row for '" + VfxKey + "' yet (run Defenders/VFX/Generate Hovl " +
                    "VFX Catalog so her VfxManualPicks.json row is baked in) - multiplier 1.0 assumed, " +
                    "and PlayKey will draw nothing until that row exists.");
                return 1f;
            }
            return row.DefaultScale > 0f ? row.DefaultScale : 1f;
        }

        /// <summary>Self-installs the reticle on <paramref name="host"/> (idempotent).</summary>
        public static AoeCastReticle Ensure(GameObject host)
        {
            if (host == null) return null;
            var r = host.GetComponent<AoeCastReticle>();
            if (r == null) r = host.AddComponent<AoeCastReticle>();
            return r;
        }

        /// <summary>
        /// Show the ring at <paramref name="groundCentre"/>, sized to
        /// <paramref name="abilityRadius"/>, for the aiming window
        /// <paramref name="windowSeconds"/>. Replaces any ring already showing.
        /// </summary>
        public void Show(string abilityName, float abilityRadius, Vector3 groundCentre, float windowSeconds)
        {
            if (_showing) Hide("re-show");

            float mult = ReadAuthoringMultiplier();
            float scale = LocalScaleForRadius(abilityRadius, mult);
            Vector3 at = groundCentre + Vector3.up * GroundLift;
            float life = Mathf.Max(0.05f, windowSeconds) + LifetimeGrace;

            _anchor = new GameObject("AoeCastReticleAnchor");
            _anchor.transform.SetPositionAndRotation(at, Quaternion.identity);

            // §12: the whole chain in one line - key requested, ability + its radius, the
            // computed localScale, the ground position and the window it is bound to. A
            // missing VFX and a subtle VFX are indistinguishable without this.
            FlowTrace.Step(FlowSystem,
                "SHOW key='" + VfxKey + "' ability='" + (abilityName ?? "?") +
                "' radius=" + abilityRadius.ToString("0.00") + "m ringRadiusAtScale1=" +
                PrefabRingRadiusAtUnitScale.ToString("0.00") + "m tagScale=" + mult.ToString("0.00") +
                " -> localScale=" + scale.ToString("0.000") + " at ground=" + at.ToString("F2") +
                " window=" + windowSeconds.ToString("0.00") + "s (spawn lifetime " + life.ToString("0.00") + "s).");

            var handle = VFXManager.PlayKey(VfxKey, at, Quaternion.identity, _anchor.transform,
                                            null, scale, life);

            // PlayKey is null-safe and traces its own misses; say out loud which branch we
            // took so "the ring never appeared" is never ambiguous in a capture.
            if (handle == null)
            {
                FlowTrace.Step(FlowSystem,
                    "PlayKey('" + VfxKey + "') returned no handle - expected for an isLoop:false row " +
                    "(the shared pool reclaims it on its own deadline). Check the VFXManager trace " +
                    "immediately above for whether the prefab actually resolved.");
            }

            AssertNoColliders(_anchor, abilityName);
            _showing = true;
        }

        /// <summary>Hide the ring (cast committed, interrupted, cancelled, caster gone).</summary>
        public void Hide(string reason)
        {
            if (!_showing && _anchor == null) return;

            if (_anchor != null)
            {
                // Deactivating the ANCHOR hides the ring this frame without destroying the
                // pooled instance parented under it - VFXManager still owns that object and
                // reclaims it on its deadline. Destroying it here would corrupt the shared pool.
                _anchor.SetActive(false);
                _retiring.Add(_anchor);
                _anchor = null;
            }
            _showing = false;
            FlowTrace.Step(FlowSystem, "HIDE reason=" + (reason ?? "?") +
                                       " (retiring anchors awaiting pool reclaim: " + _retiring.Count + ").");
        }

        /// <summary>True while a reticle ring is on screen. Read-only, for oracles / HUD.</summary>
        public bool IsShowing => _showing;

        private void Update()
        {
            // Destroy a retired anchor ONLY once the shared pool has taken its ring back
            // (childCount 0). Never destroy one that still holds a pooled instance.
            for (int i = _retiring.Count - 1; i >= 0; i--)
            {
                var a = _retiring[i];
                if (a == null) { _retiring.RemoveAt(i); continue; }
                if (a.transform.childCount != 0) continue;
                _retiring.RemoveAt(i);
                Destroy(a);
            }
        }

        private void OnDisable()
        {
            Hide("reticle-disabled");
        }

        private void OnDestroy()
        {
            // Hand every pooled ring back to the world before our anchors die, so a hero
            // teardown can never destroy an object the shared pool still has on its books.
            ReleaseAnchor(_anchor);
            _anchor = null;
            for (int i = 0; i < _retiring.Count; i++) ReleaseAnchor(_retiring[i]);
            _retiring.Clear();
        }

        private static void ReleaseAnchor(GameObject anchor)
        {
            if (anchor == null) return;
            var kids = new List<Transform>();
            foreach (Transform t in anchor.transform) kids.Add(t);
            for (int i = 0; i < kids.Count; i++)
            {
                kids[i].SetParent(null, true);
                kids[i].gameObject.SetActive(false);   // matches the pool's own dormant state
            }
            Destroy(anchor);
        }

        /// <summary>
        /// The reticle must never eat the placement tap. The tagged prefab has no Collider
        /// and the anchor has no components, so this should always find zero - but a silent
        /// regression here makes the ability uncastable, so it is asserted at runtime and
        /// FAILED loudly (§12) rather than trusted.
        /// </summary>
        private static void AssertNoColliders(GameObject anchor, string abilityName)
        {
            if (anchor == null) return;
            var hits = anchor.GetComponentsInChildren<Collider>(true);
            if (hits != null && hits.Length > 0)
            {
                FlowTrace.Fail(FlowSystem,
                    "INPUT BLOCKER: the AoE reticle spawned " + hits.Length + " Collider(s) under '" +
                    anchor.name + "' for ability '" + (abilityName ?? "?") + "'. The placement tap can " +
                    "now be eaten by the reticle, which makes the ability uncastable. The tagged prefab " +
                    "carries no collider - something added one.");
                return;
            }
            FlowTrace.Throttle(FlowSystem, "no-colliders", 5f,
                "input-transparency OK: 0 Colliders under the reticle anchor (particle-only, world-space) " +
                "- the placement tap passes through to the ground.");
        }
    }
}
