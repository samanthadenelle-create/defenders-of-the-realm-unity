// =============================================================================
// CastleDefensePlansService -- WO-1013 "Castle Defense Plans": the ONE authored
// drop that unlocks the Arcane Spire after the player survives RequiredWavesSurvived
// waves (owner ruling 2026-08-16 moved that 2 -> 3; the constant is the authority --
// never restate the number in prose, that is how this header went stale once already).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Self-bootstraps like EchoWorkforceBootstrap (RuntimeInitializeOnLoadMethod,
// no scene authoring, no VillageSceneBuilder re-save). One persistent service;
// a cheap 1 s scan (the EchoWaveUnlockBridge cadence) decides everything from
// PERSISTED STATE, so every acceptance shape falls out of one rule:
//   spawn IFF GameState.WavesCompleted >= RequiredWavesSurvived AND the unlock is
//         not collected AND no prop is already standing AND this scene runs the
//         town wave loop.
// - "survive the waves": WavesCompleted is the persisted lifetime wave-clear counter
//   (EchoService increments it off WaveManager.OnWaveCleared). Waves only run
//   post-onboarding, so the gate is WAVES, not tutorial completion -- skip-tutorial
//   players are covered for free (WO-1013 acceptance).
// - "persists until collected": the prop is deterministically re-spawned from
//   state on every scene entry / restart until the flag flips. Nothing is saved
//   about the prop itself.
// - "later waves drop nothing scripted": once collected the flag closes the rule
//   forever. ShouldSpawnDrop is pure so the regression pins the truth table.
//
// The prop mirrors the DungeonTreasureCache visual grammar (primitives + point
// light -- no chest prefab ships under Resources/) and the ComposedKeyPickup
// trigger grammar (sphere trigger + hero check) via CastleDefensePlansPickup.
// No banner, no modal, no announcement chrome (WO-1013 SS3): it glints at the
// gate, nothing more. That ruling STANDS -- see BuildVisual for the WO-1105
// discoverability work, which stays strictly inside it (a world-space landmark
// pillar on the prop, the same one an enemy fortress already carries; still
// nothing announced, nothing on the HUD).
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Combat;
using DeNelle.Core.Dialogue;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;
using DeNelle.Core.UI;
using UnityEngine;
using CoreDialogue = DeNelle.Core.Dialogue.DialogueService;

namespace DeNelle.Village
{
    /// <summary>Installs the single persistent <see cref="CastleDefensePlansService"/>.</summary>
    public static class CastleDefensePlansBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (CastleDefensePlansService.Instance != null) return;
            var go = new GameObject("CastleDefensePlansService");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<CastleDefensePlansService>();

            // WO-1105 (canon sec 12): PROVE the service installed. Until now Init emitted
            // nothing and every early return in Update was silent, so a run where the drop
            // never spawned produced a log byte-identical to one where it did -- which is
            // precisely why F8 seq 2505 was nearly mis-diagnosed as "the service never runs
            // in the hub". One line here + the Update heartbeat close that hole.
            FlowTrace.Step("Progression",
                "plans-service installed (DDOL, self-bootstrap; threshold " +
                CastleDefensePlansService.RequiredWavesSurvived + " waves survived) -- WO-1013");
        }
    }

    /// <summary>
    /// Owns the WO-1013 plans-drop lifecycle: decides from persisted state whether
    /// the drop should stand, spawns/repairs the prop at the gate, and emits the
    /// [Flow:Progression] funnel lines (drop-spawned / first-spire-built; the
    /// collected/unlocked lines live in <see cref="CastleDefensePlansPickup"/>).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CastleDefensePlansService : MonoBehaviour
    {
        /// <summary>Waves the player must have SURVIVED before the plans drop (WO-1013 SS1).
        /// OWNER RULING 2026-08-16, verbatim: "it should be given after wave 3" -- was 2.
        /// The celebration screen + the Echo FTUE beat that ride this same moment are WO-1104.</summary>
        public const int RequiredWavesSurvived = 3;

        /// <summary>Metres the seat is pulled INSIDE the wall line, measured from the GATE
        /// itself. One inset for both seat sources (Gate component or WaveSpawnPoint), so
        /// there is exactly one number describing "just inside the gate mouth".</summary>
        public const float GateInsetMetres = 3.5f;

        /// <summary>The AUTHORED gate-to-spawn offset: CastleHubBuilder.PlaceCastleSpawnPoints
        /// seats every WaveSpawnPoint exactly this far OUTSIDE the gate it feeds (canon SS7).
        /// Only used to recover a gate anchor from a marker whose GatePosition was never
        /// Configure()'d -- the authored GatePosition is preferred in every normal case.</summary>
        public const float SpawnToGateMetres = 12f;

        /// <summary>Close enough to read immediately, outside the pickup trigger so
        /// the movement that ended the wave cannot collect the plans unseen.</summary>
        public const float PlayerDropOffsetMetres = 3.25f;

        public static CastleDefensePlansService Instance { get; private set; }

        private GameObject _prop;
        private float _nextScan;
        private const float ScanInterval = 1.0f;   // the EchoWaveUnlockBridge cadence

        /// <summary>Seconds between [Flow:Progression] not-spawning heartbeat lines. The scan
        /// itself runs at 1 Hz; the heartbeat is throttled well below that so a long session
        /// cannot flood the log while still naming the reason on every look.</summary>
        private const float HeartbeatSeconds = 5f;

        // first-spire-built funnel: emit ONCE, on the transition observed this session
        // (a save that already built one stays silent -- baseline taken on first scan).
        private bool _baselineTaken;
        private bool _firstSpireTraced;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// The ONE spawn rule, pure so the guardrail regression pins it headless:
        /// spawn IFF enough waves survived AND not yet collected AND no live prop.
        /// Collected-forever is what makes wave 3+ (and every wave after) drop
        /// nothing scripted -- this is not a drop system (WO-1013 SS3).
        /// </summary>
        public static bool ShouldSpawnDrop(int wavesCompleted, bool unlocked, bool propAlive)
            => !unlocked && !propAlive && wavesCompleted >= RequiredWavesSurvived;

        private void Update()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + ScanInterval;

            var svc = GameStateService.Instance;
            var state = svc != null ? svc.State : null;
            if (state == null)
            {
                // WO-1105 (canon sec 12): NAME the reason. Every early return below used to be
                // silent, so "the drop did not spawn" and "the drop spawned fine" logged the
                // same nothing.
                FlowTrace.Throttle("Progression", "plans-idle-nostate", HeartbeatSeconds,
                    "plans-drop idle: no GameState yet (GameStateService" +
                    (svc == null ? ".Instance" : ".State") + " is null) -- still scanning");
                return;
            }

            // -- first-spire-built funnel (transition-only, once per session) --------
            if (!_baselineTaken)
            {
                _firstSpireTraced = state.HasEverBuilt(CastleDefensePlansPickup.SpireCatalogId);
                _baselineTaken = true;
            }
            if (!_firstSpireTraced && state.HasEverBuilt(CastleDefensePlansPickup.SpireCatalogId))
            {
                _firstSpireTraced = true;
                FlowTrace.Step("Progression",
                    "first-spire-built: tower_arcane_spire committed through the normal build flow " +
                    "(everBuiltStructureIds) -- WO-1013 funnel complete");
            }

            // -- the one spawn rule --------------------------------------------------
            bool unlocked = ProgressionUnlocks.IsUnlocked(CastleDefensePlansPickup.SpireCatalogId);
            bool propAlive = _prop != null;
            if (!ShouldSpawnDrop(state.WavesCompleted, unlocked, propAlive))
            {
                FlowTrace.Throttle("Progression", "plans-idle-rule", HeartbeatSeconds,
                    $"plans-drop not spawning: wavesCompleted={state.WavesCompleted} " +
                    $"(need >={RequiredWavesSurvived}) unlocked={unlocked} propAlive={propAlive} " +
                    "-- ShouldSpawnDrop false");
                return;
            }

            // Only the defended town runs the village wave loop; a raid/dungeon/battle
            // scene has no village WaveManager and must never grow the drop.
            if (FindAnyObjectByType<WaveManager>() == null)
            {
                FlowTrace.Throttle("Progression", "plans-idle-nowavemgr", HeartbeatSeconds,
                    $"plans-drop WITHHELD: the rule says spawn (wavesCompleted={state.WavesCompleted}) " +
                    "but this scene has no village WaveManager -- not the defended town " +
                    "(raid/dungeon/battle), so no drop grows here");
                return;
            }

            Guard.Try("Progression", "spawn plans drop", () => SpawnDrop(state.WavesCompleted));
        }

        // =====================================================================
        //  SPAWN
        // =====================================================================

        private void SpawnDrop(int wavesCompleted)
        {
            Vector3 seat = ResolveDropSeat(out string seatSource);

            _prop = new GameObject("CastleDefensePlans_Drop");
            // Scene-owned on purpose (NOT under this DDOL service): the prop dies with
            // the scene and the scan deterministically re-spawns it from state.
            _prop.transform.position = seat;

            BuildVisual(_prop.transform);

            var sphere = _prop.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 1.6f;
            // Kinematic body so the trigger fires regardless of how the hero rig is
            // composed (CharacterController vs collider+rigidbody).
            var rb = _prop.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            _prop.AddComponent<CastleDefensePlansPickup>();
            _prop.AddComponent<CastlePlansDiscoveryNudge>();

            FlowTrace.Step("Progression",
                $"plans-drop-spawned @ {seat} (seat={seatSource}, wavesCompleted={wavesCompleted}, " +
                "one authored drop -- persists until collected; WO-1013)");
        }

        /// <summary>Prefer a visible position just ahead of the hero who earned the
        /// reward. The deterministic gate seat remains the loading/headless fallback.</summary>
        private static Vector3 ResolveDropSeat(out string source)
        {
            var heroes = FindObjectsByType<HeroHealth>(FindObjectsSortMode.None);
            if (heroes != null)
            {
                for (int i = 0; i < heroes.Length; i++)
                {
                    var hero = heroes[i];
                    if (hero == null || !hero.gameObject.activeInHierarchy) continue;

                    Vector3 forward = hero.transform.forward;
                    forward.y = 0f;
                    if (forward.sqrMagnitude < 0.01f)
                    {
                        forward = -hero.transform.position;
                        forward.y = 0f;
                    }
                    if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;

                    source = "player:" + hero.name;
                    return GroundSnap(hero.transform.position +
                                      forward.normalized * PlayerDropOffsetMetres);
                }
            }

            Vector3 fallback = ResolveGateSeat(out string fallbackSource);
            source = "fallback:" + fallbackSource;
            return fallback;
        }

        /// <summary>
        /// One candidate seat source for the plans drop: the marker's NAME (the stable
        /// tie-break key), its world position, and the world position of the gate it feeds
        /// (<see cref="Vector3.zero"/> when the marker was never Configure()'d). A plain
        /// value type so <see cref="TryResolveSpawnSeat"/> is pure and pinnable headless.
        /// </summary>
        public readonly struct SeatCandidate
        {
            public readonly string Name;
            public readonly Vector3 Position;
            public readonly Vector3 GatePosition;

            public SeatCandidate(string name, Vector3 position, Vector3 gatePosition)
            {
                Name = name ?? "";
                Position = position;
                GatePosition = gatePosition;
            }
        }

        /// <summary>
        /// Seat the drop JUST INSIDE THE GATE MOUTH (WO-1013 SS1, corrected by WO-1105):
        /// prefer the first cardinal <see cref="Gate"/> by ordinal gateId, pulled
        /// <see cref="GateInsetMetres"/> toward the village centre; else the cardinal
        /// <see cref="WaveSpawnPoint"/> chosen by ordinal NAME, seated off the gate that
        /// marker feeds and pulled the same inset inward; else a fixed near-centre
        /// fallback. Ground-snapped by raycast.
        /// </summary>
        private static Vector3 ResolveGateSeat(out string source)
        {
            Vector3 seat;
            var gates = FindObjectsByType<Gate>(FindObjectsSortMode.None);
            if (gates != null && gates.Length > 0)
            {
                Gate pick = gates[0];
                for (int i = 1; i < gates.Length; i++)
                {
                    if (gates[i] == null) continue;
                    if (pick == null || string.CompareOrdinal(
                            gates[i].GateId ?? "", pick.GateId ?? "") < 0)
                        pick = gates[i];
                }
                seat = PullTowardCentre(pick.transform.position, GateInsetMetres);
                source = $"gate:{pick.GateId ?? pick.name}";
            }
            else
            {
                // WO-1038 (owner report 2026-08-16 "when do i get the arcane spire plans?", F8 seq
                // 2434-2442): this read GameObject.FindGameObjectsWithTag("SpawnPoint") and the tag
                // IS NOT DECLARED in TagManager.asset -- FindGameObjectsWithTag THROWS on an
                // undeclared tag, so every scan died here, the fallback seat below was unreachable,
                // and the drop never spawned. The merged hub has no Gate objects, so this branch is
                // the live path, not the rare one. Resolve by COMPONENT, the way the gate branch
                // above already does: WaveSpawnPoint is what CastleHubBuilder.PlaceCastleSpawnPoints
                // actually seats (canon SS7, 12 m outside each gate), and a component lookup cannot
                // throw on missing project settings. CastleHubBuilder.cs:2415 already noted that
                // EnemyBrain guards undefined tags -- this site was the one that did not.
                var spawns = FindObjectsByType<WaveSpawnPoint>(FindObjectsSortMode.None);
                var candidates = new List<SeatCandidate>(spawns != null ? spawns.Length : 0);
                if (spawns != null)
                {
                    for (int i = 0; i < spawns.Length; i++)
                    {
                        if (spawns[i] == null) continue;
                        candidates.Add(new SeatCandidate(
                            spawns[i].name, spawns[i].transform.position, spawns[i].GatePosition));
                    }
                }

                if (!TryResolveSpawnSeat(candidates, out seat, out source))
                {
                    seat = new Vector3(0f, 0f, 10f);   // near the Heart, on the approach
                    source = "fallback:heart-approach";
                }
            }
            return GroundSnap(seat);
        }

        /// <summary>
        /// The seat maths, PURE so the guardrail pins both properties headless: the seat lands
        /// INSIDE the wall line, and the same candidate set always yields the same seat no
        /// matter what order FindObjectsByType returned them in.
        /// </summary>
        /// <remarks>
        /// TWO WO-1105 defects live here, both found from owner F8 seq 2505 ("Im on wave five
        /// and still cannot build arcane towers" -- the drop HAD spawned at wave 3; she never
        /// walked over it):
        ///
        /// 1. OUTSIDE THE WALLS. The old code pulled a FIXED 8 m off the SPAWN MARKER and the
        ///    comment claimed that seated it "well inside". It did not. CastleHubBuilder
        ///    .PlaceCastleSpawnPoints seats every marker 12 m OUTSIDE its gate, so in the hub
        ///    the markers sit at |p| ~= 52.8 while the gate ring is at |p| ~= 40.8 -- an 8 m
        ///    pull landed the prop at ~44.8, roughly 4 m OUTSIDE the wall, behind a perimeter
        ///    the player has no reason to walk. The fix takes no second magic number: each
        ///    marker already CARRIES the world position of the gate it feeds
        ///    (WaveSpawnPoint.GatePosition, authored at bake time), so the anchor is READ from
        ///    the scene and the only tunable left is the one inset shared with the Gate branch.
        ///
        /// 2. NON-DETERMINISM. All four cardinal markers are equidistant from the centre, so
        ///    the old "nearest by sqrMagnitude, strict d &lt; best" compare never broke the tie
        ///    and resolved on FindObjectsByType ITERATION ORDER -- the same save could drop at
        ///    a different gate run to run, while the docstring promised determinism. The pick
        ///    is now by ordinal NAME, a key that cannot tie.
        /// </remarks>
        /// <returns>False when there is no usable candidate (caller falls back).</returns>
        public static bool TryResolveSpawnSeat(IReadOnlyList<SeatCandidate> candidates,
                                               out Vector3 seat, out string source)
        {
            seat = Vector3.zero;
            source = null;
            if (candidates == null || candidates.Count == 0) return false;

            int pick = -1;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (pick < 0 ||
                    string.CompareOrdinal(candidates[i].Name, candidates[pick].Name) < 0)
                    pick = i;
            }
            if (pick < 0) return false;

            var c = candidates[pick];
            Vector3 anchor = c.GatePosition;
            string via = "gate-pos";
            if (anchor.x * anchor.x + anchor.z * anchor.z < 0.01f)
            {
                // Marker never Configure()'d: recover the gate by walking the AUTHORED
                // gate-to-spawn offset back inward from the marker itself.
                anchor = PullTowardCentre(c.Position, SpawnToGateMetres);
                via = "spawn-inset";
            }
            if (anchor.x * anchor.x + anchor.z * anchor.z < 0.01f) return false;

            seat = PullTowardCentre(anchor, GateInsetMetres);
            source = "spawnpoint:" + c.Name + "(" + via + ")";
            return true;
        }

        private static Vector3 PullTowardCentre(Vector3 from, float metres)
        {
            var flat = new Vector3(from.x, 0f, from.z);
            if (flat.sqrMagnitude < 0.01f) return from;
            var dir = (-flat).normalized;   // toward the Heart at (0,0,0)
            return from + dir * metres;
        }

        private static Vector3 GroundSnap(Vector3 seat)
        {
            if (Physics.Raycast(seat + Vector3.up * 20f, Vector3.down, out var hit, 60f))
                return hit.point + Vector3.up * 0.15f;
            return seat + Vector3.up * 0.3f;
        }

        // =====================================================================
        //  VISUAL -- primitives + glint (the DungeonTreasureCache grammar; no
        //  chest prefab ships under Resources/). No announcement chrome.
        // =====================================================================

        private static void BuildVisual(Transform parent)
        {
            // A small satchel-of-plans: leather body, strap, and a rolled scroll on top.
            AddDecor(parent, PrimitiveType.Cube, new Vector3(0f, 0.30f, 0f),
                new Vector3(0.85f, 0.50f, 0.55f), new Color(0.42f, 0.29f, 0.13f));      // leather body
            AddDecor(parent, PrimitiveType.Cube, new Vector3(0f, 0.42f, 0f),
                new Vector3(0.90f, 0.12f, 0.60f), new Color(0.86f, 0.71f, 0.30f));      // gilt strap
            AddDecor(parent, PrimitiveType.Cylinder, new Vector3(0f, 0.66f, 0f),
                new Vector3(0.14f, 0.42f, 0.14f), new Color(0.92f, 0.87f, 0.72f),
                euler: new Vector3(0f, 0f, 90f));                                        // rolled plans
            var lightGo = new GameObject("Plans_Glint");
            lightGo.transform.SetParent(parent, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = GlintTint;
            light.range = 14f;      // WO-1105: 8 m did not reach past the gate arch
            light.intensity = 2.4f;

            var glint = parent.gameObject.AddComponent<CastlePlansGlint>();
            glint.Bind(light);

            // -- DISCOVERABILITY (WO-1105, owner F8 seq 2505) ------------------------
            // The drop spawned correctly at wave 3 and the owner played two more waves
            // without ever seeing it. An 0.85 m primitive satchel with a short point light,
            // ~37 m from a town-centred camera, is simply not findable at hub scale -- the
            // WO-1013 SS1 ruling was made for a small village.
            //
            // THE RULING IS NOT OVERTURNED. "No banner, no modal, no announcement chrome"
            // still holds exactly: nothing is announced, nothing is pushed to the HUD, no
            // text appears, and the player is never interrupted. This is the minimum that
            // makes it FINDABLE without crossing that line -- the SAME far-field landmark
            // pillar the open world already uses to mark a landmark you can see from range
            // (PoiBeacon.Landmark -> PoiCalloutSystem -> the catalogued "Poi_Landmark"
            // pillar loop). It is still only a thing that glints at the gate; now it is a
            // thing you can see glinting FROM the town centre.
            //
            // Colour-blind canon (PoiBeacon header): the pillar reads by verticality /
            // motion / luminance, never hue -- the tint stays the neutral pale gold of the
            // glint, not a semantic colour.
            //
            // Guarded + null-tolerant on purpose: PoiCallouts is a feature flag and PlayKey
            // no-ops when the key is unauthored, so the beacon is a BONUS on top of the
            // prop's own light -- if the callout system is off the drop is still lit, and a
            // failure here can never cost the player the unlock.
            Guard.Try("Progression", "plans landmark beacon", () =>
            {
                var beacon = PoiBeacon.Attach(parent.gameObject, PoiBeacon.PoiTier.Landmark,
                    calloutRadius: 500f,     // landmark tier: visible across the hub
                    handoffRadius: 4f,       // fades as the hero arrives on it
                    tint: GlintTint);
                // Prove the marker exists, so a future "I still could not see it" is triaged
                // from data instead of theory (canon sec 12).
                FlowTrace.Step("Progression",
                    "plans-drop landmark beacon " + (beacon != null ? "attached" : "NOT attached") +
                    " (PoiCallouts flag=" + DeNelle.Core.FeatureFlags.PoiCallouts +
                    ") -- world-space pillar only; no banner, no modal (WO-1013 SS3 upheld)");
            });
        }

        /// <summary>Neutral high-luminance pale gold shared by the prop's glint light and its
        /// landmark pillar. NOT a semantic hue (owner is red/green colour-blind).</summary>
        private static readonly Color GlintTint = new Color(1f, 0.86f, 0.5f, 1f);

        private static void AddDecor(Transform parent, PrimitiveType type, Vector3 localPos,
            Vector3 scale, Color color, Vector3 euler = default)
        {
            Guard.Try("Progression", "build plans decor", () =>
            {
                var go = GameObject.CreatePrimitive(type);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = localPos;
                go.transform.localScale = scale;
                if (euler != Vector3.zero) go.transform.localEulerAngles = euler;
                // Strip the primitive's solid collider: the pickup's own sphere TRIGGER is
                // the one collider this prop owns (a solid box at the gate would snag paths).
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
                var rend = go.GetComponent<Renderer>();
                if (rend != null)
                {
                    var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                    if (sh != null) rend.material = new Material(sh) { color = color };
                    else rend.material.color = color;
                }
            });
        }
    }

    /// <summary>WO-1151: once-ever 2D Echo nudge when the approved beacon enters view.</summary>
    internal sealed class CastlePlansDiscoveryNudge : MonoBehaviour
    {
        public const string SeenKey = "spire_plans_beacon_nudge";
        public const string CopyKey = "spirePlansBeaconNudge";
        private const float MaxSightDistance = 140f;
        private const float EdgeInset = 0.04f;
        private float _nextCheck;
        private bool _deliveredThisSession;

        private void Update()
        {
            if (_deliveredThisSession || Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + 0.25f;

            var svc = GameStateService.Instance;
            var state = svc != null ? svc.State : null;
            if (state == null || state.SeenTutorials == null) return;
            if (state.SeenTutorials.TryGetValue(SeenKey, out bool seen) && seen)
            {
                _deliveredThisSession = true;
                return;
            }

            // BattleLock is the existing battle/wave authority. The other two
            // checks prevent a companion nudge from stacking over a live modal.
            if (BattleLock.IsInBattle() || PanelManager.AnyOpen || CoreDialogue.IsRunning)
                return;

            var camera = Camera.main;
            if (camera == null) return;
            Vector3 sightPoint = transform.position + Vector3.up * 4f;
            if ((camera.transform.position - sightPoint).sqrMagnitude > MaxSightDistance * MaxSightDistance)
                return;
            if (!IsInViewport(camera.WorldToViewportPoint(sightPoint), EdgeInset)) return;

            if (!CoreDialogue.PlayDef(BuildDialogue(VillageStrings.Canon(CopyKey))))
            {
                FlowTrace.Warn("Progression",
                    "plans beacon nudge could not open; seen flag remains clear for retry");
                return;
            }

            _deliveredThisSession = true;
            svc.MarkTutorialSeen(SeenKey);
            FlowTrace.Step("Progression",
                "plans beacon nudge SHOWN and persisted (2D guide portrait; no Echo body; beacon untouched)");
        }

        /// <summary>Pure viewport oracle for the focused regression.</summary>
        public static bool IsInViewport(Vector3 viewport, float inset = EdgeInset)
            => viewport.z > 0f && viewport.x >= inset && viewport.x <= 1f - inset &&
               viewport.y >= inset && viewport.y <= 1f - inset;

        private static DialogueDef BuildDialogue(string line)
        {
            var def = new DialogueDef { Id = SeenKey, StartNode = "notice" };
            var node = new DialogueNode { Id = "notice" };
            node.Lines.Add(new DialogueLine { Speaker = "{guide}", Text = line ?? string.Empty });
            def.Nodes.Add(node);
            return def;
        }
    }

    /// <summary>
    /// The drop's GLINT (WO-1013 SS1 "it glints; no banner announces it"): a gentle
    /// bob + slow spin + light pulse. Pure presentation, runtime-only, self-contained
    /// (the IconGlowPulse pattern). Unscaled time so it breathes through any pause.
    /// </summary>
    internal sealed class CastlePlansGlint : MonoBehaviour
    {
        private Light _light;
        private float _baseY;
        private float _baseIntensity = 1.8f;

        public void Bind(Light light)
        {
            _light = light;
            if (_light != null) _baseIntensity = _light.intensity;
        }

        private void Start() => _baseY = transform.position.y;

        private void Update()
        {
            float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.2f);
            var p = transform.position;
            p.y = _baseY + Mathf.Lerp(0f, 0.14f, k);
            transform.position = p;
            transform.Rotate(0f, 30f * Time.unscaledDeltaTime, 0f, Space.World);
            if (_light != null)
                _light.intensity = Mathf.Lerp(_baseIntensity * 0.75f, _baseIntensity * 1.25f, k);
        }
    }
}
