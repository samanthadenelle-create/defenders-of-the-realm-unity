// =============================================================================
// SafeZoneRecovery -- full HP + MP restore on entering a SAFE ZONE (Castle/Town/Base).
// -----------------------------------------------------------------------------
// Owner brief (2026-06-29, SURVIVAL RULE): Health AND Mana do NOT auto-restore
// after combat (gated by FeatureFlags.NoAutoHeal). In the field the hero relies on
// crafted potions. Full passive recovery happens ONLY at a SAFE ZONE -- the home
// hub scenes (Castle/Town/Base) enumerated by DeNelle.Core.HubScenes.IsHub
// (MainCastle_Hall, Village2, CastleHub, CastleHub_MainKeep).
//
// WHAT IT DOES (on every scene load where HubScenes.IsHub(scene.name) is true):
//   - HeroHealth.Instance.RestoreToFull()   -> full HP (clears a downed latch too)
//   - HeroAbilities.RestoreManaToFull()     -> full MP
// This runs REGARDLESS of ff.noautoheal -- safe zones ALWAYS fully heal; that is
// the design (the flag gates the POST-COMBAT field auto-heal, not this).
//
// WHY A SELF-BOOTSTRAPPING DDOL SINGLETON (not a scene edit) -- mirrors
// HubAmbientVfxInjector: re-saving a .unity carries the project's scene-resave
// corruption risk (CLAUDE.md §3 "NEVER hand-edit"). This finds the hero at runtime
// via its singleton / component, so it never touches a scene file.
//
// Village -> Core only (FeatureFlags / HubScenes / FlowTrace / Guard). No
// cross-asmdef ref, no reflection. Null-safe + Guard-wrapped throughout. ASCII only.
// =============================================================================

using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>Restores the hero to full HP + MP on entering a safe-zone (hub) scene,
    /// and while the hero STANDS inside the town/castle footprint, regens HP by tick.</summary>
    public sealed class SafeZoneRecovery : MonoBehaviour
    {
        public static SafeZoneRecovery Instance { get; private set; }

        // ── TOWN-FOOTPRINT tick regen (owner 2026-07-08 felt-test) ────────────────────
        // The on-load RestoreToFull below tops the hero off when it ENTERS a hub. But
        // combat can happen INSIDE a hub with NO scene reload — the wave-loop-in-hub, or
        // the FTUE teaching wave that floors the hero at 1 HP (HeroHealth.TakeDamage). In
        // that case OnSceneLoaded never re-fires, so the hero sits at 1 HP with no recovery
        // option (the exact bug the owner hit). This adds a CONTINUOUS while-standing-in-town
        // top-up: while the hero is in a hub scene AND inside the town/castle footprint
        // (within TownRadius of the Heart-at-origin — mirroring the HUD's HudContextEvaluator
        // radial model), regen HP by tick up to full.
        //
        // SAFE-ZONE ONLY (preserves the ff.noautoheal field difficulty): the ring test IS the
        // gate. Outside the ring / in the field / in enemy-owned raid scenes the hero never
        // regens here, so no-auto-heal-in-the-field still holds; the town footprint is the sole
        // exception (identical design to the on-load RestoreToFull, which also ignores the flag).
        private const float TownRegenFractionPerSecond = 0.12f; // ~8s empty->full; "rest a few seconds to top up"
        private const float TownRadius     = 60f;   // matches HudContextEvaluator.TownRadius (Heart at world origin, canon §7)
        private const float TownRadiusHyst = 8f;    // matches HudContextEvaluator hysteresis so the edge doesn't chatter

        private HeroLocomotion _hero;
        private bool _inTownRing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject(nameof(SafeZoneRecovery)).AddComponent<SafeZoneRecovery>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            // The hero may already be present in the scene that booted us (e.g. starting in
            // MainCastle_Hall) -- recover immediately so a fresh boot into a safe zone tops off.
            if (HubScenes.IsHub(SceneManager.GetActiveScene().name))
                Recover(SceneManager.GetActiveScene().name);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (HubScenes.IsHub(scene.name))
                Recover(scene.name);
        }

        /// <summary>
        /// TOWN-FOOTPRINT tick regen: while the hero stands inside the town/castle safe ring
        /// of a hub scene, feed a small HP top-up each frame up to full. This covers the
        /// battle->town RETURN and the in-hub combat cases the on-load RestoreToFull misses
        /// (fight in town / FTUE 1-HP floor -> no scene reload -> nothing re-fires the restore).
        /// Gated to the safe ring ONLY, so the field keeps its ff.noautoheal difficulty.
        /// </summary>
        private void Update()
        {
            var health = HeroHealth.Instance;
            if (health == null || !health.IsAlive) return;
            if (health.Hp >= health.MaxHp) return;   // already full — nothing to regen

            if (!HubScenes.IsHub(SceneManager.GetActiveScene().name)) { _inTownRing = false; return; }

            // Town footprint test = HudContextEvaluator's radial model: hub scene AND hero within
            // TownRadius of the Heart-at-origin (canon §7). Hero not resolved yet -> treat the hub
            // as safe (matches the HUD's "default to town before the hero spawns").
            bool inRing;
            if (_hero == null || !_hero) _hero = Object.FindAnyObjectByType<HeroLocomotion>();
            if (_hero == null)
            {
                inRing = true;
            }
            else
            {
                Vector3 p = _hero.transform.position;
                float distSqr = p.x * p.x + p.z * p.z;   // horizontal distance to the Heart at origin
                float edge = _inTownRing ? TownRadius + TownRadiusHyst : TownRadius;  // hysteresis at the edge
                inRing = distSqr <= edge * edge;
            }
            _inTownRing = inRing;
            if (!inRing) return;

            float amount = health.MaxHp * TownRegenFractionPerSecond * Time.deltaTime;
            if (amount <= 0f) return;
            float before = health.Hp;
            health.RegenTick(amount);
            FlowTrace.Throttle("SafeZone", "town-regen", 1f,
                $"town regen +{(health.Hp - before):F1} -> {health.Hp:F0}/{health.MaxHp:F0} " +
                "(inside town/castle footprint; field still no-auto-heal).");
        }

        /// <summary>Full HP + MP restore. Null-safe; logs each leg; never throws into the load.</summary>
        private void Recover(string sceneName)
        {
            Guard.Try("SafeZone", "full HP+MP recovery", () =>
            {
                HeroHealth.Instance?.RestoreToFull();

                var abilities = Object.FindAnyObjectByType<HeroAbilities>();
                abilities?.RestoreManaToFull();

                FlowTrace.Step("SafeZone",
                    $"SAFE-ZONE full recovery ({sceneName}): hero HP+MP restored to full.");
            });
        }
    }
}
