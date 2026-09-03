using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    public sealed class RealmStoreBeacon : MonoBehaviour
    {
        /// <summary>
        /// The aura key this site played BEFORE WO-1343, and the key it still plays under
        /// <see cref="NightStoreAuraMode.LegacyBeaconRing"/>: the Marker8 safe-zone ground loop
        /// (<c>Assets/Resources/VFX/Markers/Marker8_SafeZoneLoop.prefab</c>).
        /// <para>
        /// (!) IT IS NO LONGER THE KEY THIS SITE NORMALLY PLAYS. Owner ask (WO-1343 Ask 2,
        /// verbatim): "the one night realm or night store is to replace the current one on the
        /// night store". The live key now comes from <see cref="NightStoreAuraSelector"/>, which
        /// is the ONE decider; this constant stays because the legacy ring must remain reachable
        /// by a single database row rather than by a rebuild, and because the name is the honest
        /// record of what was swapped out.
        /// </para>
        /// </summary>
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

        // ── WO-1343: the aura SELECTION and its two clocks ────────────────────────
        // ONE spawn owner is unchanged: StartNearAura below is still the only place this
        // component calls VFXManager.PlayKey, and there is still exactly one live handle.
        // What changed is WHICH key it asks for and WHEN it asks again. NightStoreAuraSelector
        // decides; this component obeys and traces.
        private string _activeKey;          // the key currently seated (selector's answer)
        // ⛔ THE HANDLE IS NOT THE SEAT, AND CONFLATING THEM WOULD RE-FIRE EVERY FRAME.
        // VFXManager.PlayKey returns a VFXHandle ONLY for a LOOP row; a one-shot auto-returns to
        // the pool and yields NULL. Both of the owner's store candidates are measured one-shots,
        // so `_nearAura != null` is FALSE for the shipped default - guarding on it would spawn a
        // burst on every Update. `_seated` is the presence flag; `_nearAura` is only the stop
        // handle for the loop cases (rotate-family, legacy ring).
        private bool _seated;
        private int _cadenceTick;           // monotonic cadence-tick counter; drives the rotation walk
        private float _nextCadenceAt;       // absolute unscaled time of the next cadence tick
        private float _nextBurstAt;         // absolute unscaled time of the next EXTRA burst re-fire
        private bool _clockArmed;           // false until the first proximity entry arms the clocks
        private bool _townGateTraced;       // the not-in-town withhold is said ONCE, never silently

        /// <summary>True while an aura seat is established at this beacon. Reads <c>_seated</c>,
        /// NOT the handle: a one-shot row (both owner-tagged store candidates) is seated with a
        /// null handle, and reporting that as "not running" would be wrong.</summary>
        public bool NearAuraRunning => _seated;

        /// <summary>The catalog key currently seated at this beacon, or null. Read by the
        /// regression oracle and printed in every trace - a felt-test that cannot name the key
        /// it was looking at cannot report anything actionable (CLAUDE.md s12).</summary>
        public string ActiveAuraKey => _activeKey;

        /// <summary>How many cadence ticks have fired this session. Drives the rotation walk.</summary>
        public int CadenceTick => _cadenceTick;

        /// <summary>
        /// TRUE only in the town/hub context. WO-1343: "'In town' means the aura clock runs in the
        /// town/hub context only. Do not tick it during a raid, a battle or a dungeon."
        /// <para>Delegates to <see cref="HubScenes"/> rather than inventing a second scene
        /// predicate - a duplicated scene list is the drift CLAUDE.md s2/s5 keeps recording.</para>
        /// </summary>
        private static bool InTown => HubScenes.IsHub(SceneManager.GetActiveScene().name);
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
            if (!near) { StopNearAura("left proximity ring"); return; }

            // WO-1343 Ask 2: the clock runs IN TOWN ONLY. A raid / battle / dungeon copy of this
            // beacon keeps whatever it has and never re-rolls. Said ONCE rather than silently -
            // "the aura never changed" must be a LINE in the capture, not an absence (CLAUDE.md s12).
            if (!InTown)
            {
                if (!_townGateTraced)
                {
                    _townGateTraced = true;
                    FlowTrace.Step("RealmStoreBeacon",
                        "cadence clock NOT armed: active scene '" + SceneManager.GetActiveScene().name +
                        "' is not a town/hub scene (HubScenes.IsHub false). The aura is seated once and " +
                        "never re-rolls here, by design - WO-1343 confines the clock to town.");
                }
                StartNearAura();
                return;
            }

            StartNearAura();
            TickAuraClocks();
        }

        // ── WO-1343: the two clocks ───────────────────────────────────────────────
        // CADENCE   - her "every 30~min". In a BURST mode it re-fires the burst; in ROTATE mode it
        //             advances to the next family aura; against the continuous legacy ring it is
        //             inert. NightStoreAuraSelector.CadenceMeaningFor names which, every time.
        // BURST     - an OPTIONAL extra re-fire inside one cadence period, shipped OFF (0). It
        //             exists because "30~min" was a rough number and both of her store candidates
        //             are MEASURED one-shot bursts (every ParticleSystem looping:0), so a period
        //             that reads as "nothing ever happens" is fixable by a row, not a rebuild.
        //
        // Neither clock spawns anything itself: both route back through StartNearAura, which is
        // still the single spawn owner for this presence.
        private void TickAuraClocks()
        {
            float now = Time.unscaledTime;
            var mode = NightStoreAuraSelector.Mode;
            var meaning = NightStoreAuraSelector.CadenceMeaningFor(mode);

            if (!_clockArmed)
            {
                _clockArmed = true;
                _nextCadenceAt = now + NightStoreAuraSelector.CadenceSeconds;
                float repeat = NightStoreAuraSelector.BurstRepeatSeconds;
                _nextBurstAt = repeat > 0f ? now + repeat : float.MaxValue;
                NightStoreAuraSelector.LogConfiguration("beacon clock armed at the store");
                return;
            }

            if (now >= _nextCadenceAt)
            {
                _cadenceTick++;
                _nextCadenceAt = now + NightStoreAuraSelector.CadenceSeconds;

                if (meaning == NightStoreCadenceMeaning.Inert)
                {
                    FlowTrace.Step("RealmStoreBeacon",
                        "cadence tick " + _cadenceTick + " INERT: mode=" + mode + " seats a CONTINUOUS " +
                        "effect ('" + _activeKey + "') that is already playing, so there is nothing to " +
                        "re-fire or advance. Next tick in " +
                        (NightStoreAuraSelector.CadenceSeconds / 60f).ToString("0.#") + " min.");
                }
                else
                {
                    FlowTrace.Step("RealmStoreBeacon",
                        "cadence tick " + _cadenceTick + ": mode=" + mode + " meaning=" + meaning +
                        " -> re-seating the aura (was '" + (_activeKey ?? "none") + "'). Next tick in " +
                        (NightStoreAuraSelector.CadenceSeconds / 60f).ToString("0.#") + " min.");
                    ReseatNearAura("cadence tick " + _cadenceTick + " (" + meaning + ")");
                }

                float repeat = NightStoreAuraSelector.BurstRepeatSeconds;
                _nextBurstAt = repeat > 0f && meaning == NightStoreCadenceMeaning.RefireBurst
                    ? now + repeat
                    : float.MaxValue;
                return;
            }

            if (meaning != NightStoreCadenceMeaning.RefireBurst) return;

            float repeatSeconds = NightStoreAuraSelector.BurstRepeatSeconds;
            if (repeatSeconds <= 0f) { _nextBurstAt = float.MaxValue; return; }
            if (_nextBurstAt == float.MaxValue) _nextBurstAt = now + repeatSeconds;
            if (now < _nextBurstAt) return;

            _nextBurstAt = now + repeatSeconds;
            FlowTrace.Throttle("RealmStoreBeacon", "burst-repeat", 60f,
                "burst RE-FIRE of '" + _activeKey + "' (" +
                DeNelle.Core.Ops.RemoteTunables.KeyVfxNightStoreAuraBurstRepeatSec + "=" +
                repeatSeconds.ToString("0.#") + "s, inside the " +
                (NightStoreAuraSelector.CadenceSeconds / 60f).ToString("0.#") +
                " min cadence period). Her tag is a one-shot burst and this is the extra pulse, " +
                "shipped OFF at 0 - a non-zero value here is an OVERRIDE of today's behaviour.");
            ReseatNearAura("burst repeat");
        }

        /// <summary>Stop the current seat and start the selector's current answer. Goes through the
        /// one spawn owner; never opens a second handle.</summary>
        private void ReseatNearAura(string reason)
        {
            StopNearAura(reason);
            StartNearAura();
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

        // ── THE ONE SPAWN OWNER for the Night Store's aura presence ───────────────
        // WO-1343 changed WHICH key this asks for; it did NOT add a spawner. There is still
        // exactly one PlayKey call and one live handle at this site. NightStoreAuraSelector
        // decides the key and never spawns; this method spawns and never decides.
        //
        // ⛔ SCALE: the 2.4 below is the seat's PRE-EXISTING scale for this beacon and is
        // UNTOUCHED by this ticket. Nothing here rescales an owner-tagged prefab
        // (memory vfx-map-owner-tags-no-creative-pick).
        private void StartNearAura()
        {
            if (_seated) return;
            _seated = true;

            var mode = NightStoreAuraSelector.Mode;
            string key = NightStoreAuraSelector.SelectKey(
                mode,
                NightStoreAuraSelector.FamilyMask,
                _cadenceTick,
                VFXManager.CanPlayKey,     // read-only resolve check; spawns nothing
                out string why);

            _activeKey = key;

            _nearAura = VFXManager.PlayKey(key, transform.position + Vector3.up * 0.08f,
                Quaternion.identity, transform, null, 2.4f);

            // A no-show must NAME ITSELF (WO-1343 instrumentation clause): key requested, whether a
            // prefab resolved, where it was seated, which tunable mode was live, and which cadence
            // tick produced it. A silent VFX absence is indistinguishable from "the artist's prefab
            // is subtle", and that ambiguity costs a felt-test round trip.
            if (_nearAura == null)
            {
                // A NULL handle here is NOT necessarily a failure: a row tagged isLoop:false is a
                // ONE-SHOT, which PlayKey auto-returns to the pool and always reports as null. Both
                // of her store candidates are measured one-shot bursts, so this is the EXPECTED
                // path for the shipped default. It is Step, not Warn, and it says why - a Warn on
                // the normal path is how a log stops being read.
                FlowTrace.Throttle("RealmStoreBeacon", "near-oneshot-or-missing:" + key, 5f,
                    "night-store aura: requested key '" + key + "' at " +
                    (transform.position + Vector3.up * 0.08f).ToString("F2") +
                    " (mode=" + mode + ", cadenceTick=" + _cadenceTick +
                    ", canPlay=" + VFXManager.CanPlayKey(key) + ") -> PlayKey returned a NULL handle. " +
                    "That is EXPECTED for a one-shot (isLoop:false) row, which is what both owner-" +
                    "tagged store candidates are: the burst fired and auto-returned. If canPlay is " +
                    "FALSE above, the key has no catalog row / no prefab and NOTHING drew - the " +
                    "point light remains (the Y-column mast is OFF). Provenance: " + why);
            }
            else
            {
                FlowTrace.Step("RealmStoreBeacon",
                    "night-store aura SEATED: key '" + key + "' at " +
                    (transform.position + Vector3.up * 0.08f).ToString("F2") +
                    " (mode=" + mode + ", cadenceMeaning=" + NightStoreAuraSelector.CadenceMeaningFor(mode) +
                    ", cadenceTick=" + _cadenceTick + ", handle held). Provenance: " + why);
            }
        }

        private void StopNearAura(string reason)
        {
            if (!_seated && _nearAura == null) return;

            // A held handle exists only for a LOOP row. A one-shot seat has nothing to stop - it
            // already returned itself to the pool - so clearing the seat flag IS the stop.
            if (_nearAura != null)
            {
                _nearAura.StopSoft(0.35f);
                _nearAura = null;
            }

            FlowTrace.Step("RealmStoreBeacon",
                "night-store aura seat released ('" + (_activeKey ?? "none") + "'): " + reason +
                "; handle cleared, clocks keep running while the hero is in range.");

            _activeKey = null;
            _seated = false;
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
