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
            if (_mast == null) BuildAlwaysOnLayer();
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
            var existing = transform.Find("StoreBeacon_AlwaysOn");
            if (existing != null)
            {
                _mast = existing.Find("LightMast");
                _light = existing.GetComponentInChildren<Light>(true);
                return;
            }

            var root = new GameObject("StoreBeacon_AlwaysOn").transform;
            root.SetParent(transform, false);
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
                var color = new Color(1f, 0.72f, 0.18f, 1f);
                material.color = color;
                if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * 3.5f);
                renderer.sharedMaterial = material;
            }
            _mast = mast.transform;

            var lamp = new GameObject("StoreBeacon_Light");
            lamp.transform.SetParent(root, false);
            lamp.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            _light = lamp.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.range = 14f;
            _light.intensity = 2.6f;
            _light.color = new Color(1f, 0.68f, 0.22f);
            _light.shadows = LightShadows.None;
            FlowTrace.Step("RealmStoreBeacon", "always-on mast + real light built (zero VFX loop slots).");
        }

        private void StartNearAura()
        {
            if (_nearAura != null) return;
            _nearAura = VFXManager.PlayKey(NearAuraKey, transform.position + Vector3.up * 0.08f,
                Quaternion.identity, transform, null, 2.4f);
            if (_nearAura == null)
                FlowTrace.Throttle("RealmStoreBeacon", "near-missing", 5f,
                    "near aura key '" + NearAuraKey + "' did not acquire; the zero-slot mast remains visible.");
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
