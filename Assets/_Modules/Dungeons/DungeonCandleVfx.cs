using DeNelle.Core.Diagnostics;
using DeNelle.Village;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Dungeons
{
    /// <summary>Proximity-streamed pooled flame for an authored CandleAnchor marker.</summary>
    [DisallowMultipleComponent]
    public sealed class DungeonCandleVfx : MonoBehaviour
    {
        private const float StartRange = 13f;
        private const float StopRange = 16f;
        private Transform _hero;
        private VFXHandle _handle;
        private float _nextCheck;

        public void Configure(Transform hero) => _hero = hero;

        private void Update()
        {
            if (Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + 0.25f;
            if (_hero == null) return;

            float distanceSq = (_hero.position - transform.position).sqrMagnitude;
            if ((_handle == null || !_handle.IsAlive) && distanceSq <= StartRange * StartRange)
                StartFlame();
            else if (_handle != null && distanceSq >= StopRange * StopRange)
                StopFlame();
        }

        private void StartFlame()
        {
            var manager = VFXManager.Instance;
            if (manager == null) return;
            _handle = manager.PlayEnvironment(VFXType.Env_Candle, transform);
            if (_handle != null) _handle.SetPosition(transform.position);
        }

        private void StopFlame()
        {
            _handle?.Stop();
            _handle = null;
        }

        private void OnDisable() => StopFlame();
        private void OnDestroy() => StopFlame();
    }

    /// <summary>Connects bake-authored markers without hand-editing dungeon scenes.</summary>
    internal static class DungeonCandleVfxInstaller
    {
        private static bool s_hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (s_hooked) return;
            s_hooked = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Bind(SceneManager.GetActiveScene(), null);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Bind(scene, null);

        internal static void Rebind(Scene scene, Transform hero) => Bind(scene, hero);

        private static void Bind(Scene scene, Transform resolvedHero)
        {
            if (!scene.IsValid() || !scene.isLoaded ||
                (scene.name.IndexOf("Dungeon", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                 !scene.name.StartsWith("dg_", System.StringComparison.OrdinalIgnoreCase))) return;

            Transform hero = resolvedHero;
            if (hero == null)
            {
                var heroGo = GameObject.FindGameObjectWithTag("Player");
                hero = heroGo != null ? heroGo.transform : null;
            }
            if (hero == null) return;
            int bound = 0;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var marker in root.GetComponentsInChildren<Transform>(true))
            {
                if (marker == null || marker.name != "CandleAnchor") continue;
                var flame = marker.GetComponent<DungeonCandleVfx>();
                if (flame == null) flame = marker.gameObject.AddComponent<DungeonCandleVfx>();
                flame.Configure(hero);
                bound++;
            }
            if (bound > 0)
                FlowTrace.Step("DungeonVFX", $"bound {bound} CandleAnchor marker(s) to proximity-pooled Env_Candle flames in '{scene.name}'.");
        }
    }
}
