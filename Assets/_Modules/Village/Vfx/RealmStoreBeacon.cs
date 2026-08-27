using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    public sealed class RealmStoreBeacon : MonoBehaviour
    {
        public const string NearAuraKey = "store.beacon.near";
        public const float NearRadius = 20f;

        // WO-1052 Layer A: an 18 m gold cylinder along world +Y (Unity cylinder scale.y=9
        // at local Y=9). Owner bounce UI-001 2026-08-27: "there is a VFX exiting about town
        // along Y and it needs removed or turned off". Device proof is F8
        // flag_20260827-164913_06.png -- a single gold vertical shaft in the plaza from
        // build-mode bird's-eye, color-matched to MastColor below. The tree-aura column
        // (HubAmbientVfxInjector.EnableTreeAura) is already OFF; this mast is the remaining
        // town Y-column. Near-field Marker8 ring is a ground loop (startSpeed 0), not Y-travel.
        public const string VerticalMastEmitterId = "StoreBeacon_AlwaysOn/LightMast";
        // static readonly, not const: the ON branch must stay compilable (same reason as
        // AmbientAuraPolicy.HeartTreeFirefliesExempt -- a const false would CS0162 the mast builder).
        private static readonly bool EnableVerticalMast = false;
        private static readonly Color MastColor = new Color(1f, 0.72f, 0.18f, 1f);

        private VFXHandle _nearAura;
        private Transform _hero;
        private Light _light;
        private Transform _mast;
        private float _findAt;

        public bool NearAuraRunning => _nearAura != null;
        public Vector3 BeaconPosition => transform.position + Vector3.up * 4f;

        private void Awake() => BuildAlwaysOnLayer();

        private void OnEnable()
        {
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            if (transform.Find("StoreBeacon_AlwaysOn") == null) BuildAlwaysOnLayer();
        }

        private void Update()
        {
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 1.35f);
            if (_light != null) _light.intensity = Mathf.Lerp(2.2f, 3.0f, wave);
            if (_mast != null)
            {
                var s = _mast.localScale;
                s.x = s.z = Mathf.Lerp(0.16f, 0.21f, wave);
                _mast.localScale = s;
            }

            EnsureHero();
            bool near = _hero != null && (_hero.position - transform.position).sqrMagnitude <= NearRadius * NearRadius;
            if (near) StartNearAura(); else StopNearAura("left proximity ring");
        }

        private void BuildAlwaysOnLayer()
        {
            Transform root = transform.Find("StoreBeacon_AlwaysOn");
            if (root == null)
            {
                var go = new GameObject("StoreBeacon_AlwaysOn");
                root = go.transform;
                root.SetParent(transform, false);
            }

            _mast = root.Find("LightMast");
            _light = root.GetComponentInChildren<Light>(true);

            if (!EnableVerticalMast)
            {
                StripVerticalMast(root);
                EnsurePointLight(root);
                FlowTrace.Step("RealmStoreBeacon",
                    "Y-column emitter id='" + VerticalMastEmitterId +
                    "' DISABLED (UI-001 owner bounce 2026-08-27: VFX exiting town along world Y). " +
                    "Not spawned; zero VFX loop slots. Point light + proximity Marker8 ring remain.");
                return;
            }

            if (_mast == null)
            {
                var mast = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                mast.name = "LightMast";
                mast.transform.SetParent(root, false);
                mast.transform.localPosition = new Vector3(0f, 9f, 0f);
                mast.transform.localScale = new Vector3(0.18f, 9f, 0.18f);
                var collider = mast.GetComponent<Collider>(); if (collider != null) Destroy(collider);
                var renderer = mast.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                    var material = new Material(shader) { name = "RealmStoreBeacon_Emissive_Runtime" };
                    material.color = MastColor;
                    if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", MastColor * 3.5f);
                    renderer.sharedMaterial = material;
                }
                _mast = mast.transform;
            }

            EnsurePointLight(root);
            FlowTrace.Step("RealmStoreBeacon", "always-on mast + real light built (zero VFX loop slots).");
        }

        private void StripVerticalMast(Transform root)
        {
            if (_mast == null) _mast = root.Find("LightMast");
            if (_mast == null) { _mast = null; return; }
            var doomed = _mast.gameObject;
            _mast = null;
            doomed.SetActive(false);
            Destroy(doomed);
            FlowTrace.Step("RealmStoreBeacon",
                "Y-column emitter id='" + VerticalMastEmitterId +
                "' found live and stripped (renderer off, GameObject destroyed).");
        }

        private void EnsurePointLight(Transform root)
        {
            if (_light != null) return;
            var lamp = new GameObject("StoreBeacon_Light");
            lamp.transform.SetParent(root, false);
            lamp.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            _light = lamp.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.range = 14f;
            _light.intensity = 2.6f;
            _light.color = new Color(1f, 0.68f, 0.22f);
            _light.shadows = LightShadows.None;
        }

        private void StartNearAura()
        {
            if (_nearAura != null) return;
            _nearAura = VFXManager.PlayKey(NearAuraKey, transform.position + Vector3.up * 0.08f,
                Quaternion.identity, transform, null, 2.4f);
            if (_nearAura == null)
                FlowTrace.Throttle("RealmStoreBeacon", "near-missing", 5f,
                    "near aura key '" + NearAuraKey + "' did not acquire; point light remains (Y-column mast is OFF).");
            else
                FlowTrace.Step("RealmStoreBeacon", "near aura started: Marker8 safe-zone ring + shockwave.");
        }

        private void StopNearAura(string reason)
        {
            if (_nearAura == null) return;
            _nearAura.StopSoft(0.35f);
            _nearAura = null;
            FlowTrace.Step("RealmStoreBeacon", "near aura stopped: " + reason + "; handle cleared.");
        }

        private void EnsureHero()
        {
            if (_hero != null || Time.unscaledTime < _findAt) return;
            _findAt = Time.unscaledTime + 0.5f;
            var hero = FindAnyObjectByType<HeroLocomotion>();
            if (hero != null) _hero = hero.transform;
        }

        private void OnSceneUnloaded(Scene _) => StopNearAura("scene unload");
        private void OnDisable() { SceneManager.sceneUnloaded -= OnSceneUnloaded; StopNearAura("disabled"); }
        private void OnDestroy() => StopNearAura("destroyed");
    }
}
