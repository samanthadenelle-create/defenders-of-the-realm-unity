// =============================================================================
// FishSchool (WO-590) - a small, cheap, autonomous fish school for the castle moat.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World
//
// Spawned by CastleMoatBuilder over the dip-fill water so the moat reads as a living
// waterfront. Each fish wanders forward and gently Slerps back toward centre when it
// nears the box bounds, with small per-fish speed/scale variation and a subtle vertical
// bob. NO physics, NO per-fish collider, ONE shared material (GPU-instanced) -> Pi-cheap.
//
// ASSET POLICY (CLAUDE.md S4): tries Resources.Load("Env/Fish") for a nicer model; if
// absent (the polyperfect pack is gitignored and not on a clean clone) it BUILDS a tiny
// primitive fish procedurally instead -> the school ALWAYS renders, never hard-errors,
// and needs no committed gitignored asset. FlowTrace notes which path was taken.
//
// Self-contained: drop it on a GameObject, call Configure(...) BEFORE Start runs (the
// builder does: AddComponent then Configure, Start fires next frame). No scene wiring.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.World
{
    /// <summary>
    /// A tiny autonomous fish school (wander + stay-in-bounds) for the castle moat fill.
    /// Mobile/Pi-cheap: shared instanced material, primitive-or-Resources model, no physics.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishSchool : MonoBehaviour
    {
        // ---- Config (set by Configure before Start) -----------------------------------
        private int _count = 10;
        private Vector3 _halfExtents = new Vector3(8f, 0.4f, 16f); // local box half-size the fish roam
        private float _swimY = -0.7f;                              // world Y the fish swim at (under the surface)

        // ---- Tunables -----------------------------------------------------------------
        private const int MaxFish = 12;            // hard cap (owner runs on a Pi)
        private const float BaseSpeed = 0.7f;      // m/s forward cruise
        private const float SpeedVariance = 0.35f; // +/- per-fish
        private const float TurnRate = 2.2f;       // rad/sec Slerp toward a new heading
        private const float EdgeMargin = 1.2f;     // start turning this far from the bounds edge
        private const float BobAmplitude = 0.06f;  // vertical bob (m)
        private const float BobSpeed = 1.6f;       // bob cycles/sec scale
        private const float BaseFishScale = 0.5f;  // base fish length-ish
        private const float ScaleVariance = 0.25f;

        private static readonly Color FishColor = new Color(0.32f, 0.40f, 0.46f, 1f);

        private readonly List<Fish> _fish = new List<Fish>();
        private Material _sharedMat;
        private bool _spawned;

        private struct Fish
        {
            public Transform t;
            public float speed;
            public float bobPhase;
            public Vector3 home; // local-space centre this fish bobs around
        }

        /// <summary>Set the school size, roam-box half-extents (local), and swim depth (world Y).</summary>
        public void Configure(int count, Vector3 halfExtents, float swimY)
        {
            _count = Mathf.Clamp(count, 0, MaxFish);
            _halfExtents = halfExtents;
            _swimY = swimY;
        }

        private void Start()
        {
            if (_spawned) return;
            _spawned = true;
            Guard.Try("FishSchool", "spawn school", BuildSchool);
        }

        private void BuildSchool()
        {
            if (_count <= 0) return;

            GameObject model = Resources.Load<GameObject>("Env/Fish");
            bool fromResources = model != null;
            if (!fromResources)
            {
                // Graceful, asset-free fallback: a tiny primitive fish, no gitignored dependency.
                Debug.LogWarning("[FishSchool] Resources/Env/Fish not found (gitignored pack absent " +
                                 "on clean clone) -> using a primitive fish instead (no hard error).");
                FlowTrace.Warn("FishSchool", "no Resources/Env/Fish model; building primitive fish (asset gap noted).");
            }
            else
            {
                FlowTrace.Step("FishSchool", "loaded Resources/Env/Fish model for the school.");
            }

            _sharedMat = BuildFishMaterial();

            for (int i = 0; i < _count; i++)
            {
                GameObject fishGo = fromResources ? InstantiateModel(model) : BuildPrimitiveFish();
                if (fishGo == null) continue;

                fishGo.name = "Fish_" + i;
                fishGo.transform.SetParent(transform, false);

                Vector3 local = new Vector3(
                    Random.Range(-_halfExtents.x, _halfExtents.x),
                    0f,
                    Random.Range(-_halfExtents.z, _halfExtents.z));
                fishGo.transform.localPosition = local;
                fishGo.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                float s = BaseFishScale * (1f + Random.Range(-ScaleVariance, ScaleVariance));
                if (!fromResources) fishGo.transform.localScale = new Vector3(s * 0.45f, s * 0.32f, s);

                _fish.Add(new Fish
                {
                    t = fishGo.transform,
                    speed = BaseSpeed * (1f + Random.Range(-SpeedVariance, SpeedVariance)),
                    bobPhase = Random.Range(0f, Mathf.PI * 2f),
                    home = local,
                });
            }

            FlowTrace.Step("FishSchool", "spawned " + _fish.Count + " fish (cap " + MaxFish + ", "
                + (fromResources ? "Resources" : "primitive") + ").");
        }

        private void Update()
        {
            if (_fish.Count == 0) return;

            float dt = Time.deltaTime;
            for (int i = 0; i < _fish.Count; i++)
            {
                Fish f = _fish[i];
                if (f.t == null) continue;

                Vector3 p = f.t.localPosition;

                // Wander forward along the fish's facing (local space).
                Vector3 fwd = f.t.localRotation * Vector3.forward;
                p += fwd * (f.speed * dt);

                // Stay in bounds: if near an XZ edge, Slerp the heading back toward centre.
                bool nearEdge =
                    Mathf.Abs(p.x) > (_halfExtents.x - EdgeMargin) ||
                    Mathf.Abs(p.z) > (_halfExtents.z - EdgeMargin);
                if (nearEdge)
                {
                    Vector3 toCentre = new Vector3(-p.x, 0f, -p.z);
                    if (toCentre.sqrMagnitude > 0.0001f)
                    {
                        Quaternion want = Quaternion.LookRotation(toCentre.normalized, Vector3.up);
                        f.t.localRotation = Quaternion.Slerp(f.t.localRotation, want, TurnRate * dt);
                    }
                    // Clamp so a fast fish can never escape the box this frame.
                    p.x = Mathf.Clamp(p.x, -_halfExtents.x, _halfExtents.x);
                    p.z = Mathf.Clamp(p.z, -_halfExtents.z, _halfExtents.z);
                }

                // Subtle vertical bob around the swim plane (y is local; parent sits at swimY).
                f.bobPhase += BobSpeed * dt;
                p.y = Mathf.Sin(f.bobPhase) * BobAmplitude;

                f.t.localPosition = p;
                _fish[i] = f;
            }
        }

        // One shared, GPU-instanced URP/Lit material for the whole school (one draw-friendly batch).
        private Material BuildFishMaterial()
        {
            Material mat = null;
            Guard.Try("FishSchool", "build fish material", () =>
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null) sh = Shader.Find("Standard");
                if (sh == null) return;
                mat = new Material(sh) { name = "MoatFish", enableInstancing = true };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", FishColor);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", FishColor);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.2f);
            });
            return mat;
        }

        // Tiny primitive fish: a single stretched sphere (ellipsoid) -> reads as a fish blob
        // underwater. One mesh + the shared instanced material => cheap, no gitignored asset.
        private GameObject BuildPrimitiveFish()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            var r = go.GetComponent<Renderer>();
            if (r != null && _sharedMat != null) r.sharedMaterial = _sharedMat;
            return go;
        }

        // Instantiate the Resources model; strip any colliders so fish never block.
        private GameObject InstantiateModel(GameObject model)
        {
            var go = Object.Instantiate(model);
            foreach (var c in go.GetComponentsInChildren<Collider>(true))
                Object.Destroy(c);
            return go;
        }
    }
}
