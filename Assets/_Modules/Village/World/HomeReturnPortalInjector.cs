// =============================================================================
// HomeReturnPortalInjector (WO-602) — the way back home.
// -----------------------------------------------------------------------------
// FLEET-PROVEN ROOT CAUSE (2026-07-12, 4/4 runs):
//   HOME_RETURN_FAIL :: gate=<none> — NO outer return entrance exists
//   (no SceneTransitionTrigger targets the hub scene). AutoPilotDriver.cs:5480.
//   Scene inventory confirms: Main_Castle_Overworld.unity bakes exactly ONE
//   SceneTransitionTrigger (targetSceneName: Outpost1, the cave portal) — nothing
//   in the merged overworld targets the hub, so a player who leaves the castle
//   has no discoverable, functioning way back in (owner felt-test 2026-07-03).
//
// FIX: author FOUR "Enter Elarion" return portals at runtime, one at each
// moat-bridge OUTER end (South 0° · West 90° · North 180° · East 270°, the locked
// clone convention — docs/SEAM_BRIDGE_OFFSETS_LOCKED_2026-07-04.md). Each portal
// is a SceneTransitionTrigger whose target IS the hub scene (SceneRouter.Castle):
//   • Under ff.mergedworld (current default ON) the target scene is ALREADY the
//     active scene, so Cross() takes its "already-loaded — repositioning only"
//     branch: fade to black → HeroLocomotion.WarpTo(courtyard) (disable agent →
//     move → re-warp onto navmesh → re-enable → OnTeleported) → fade in. No
//     scene load, no seam — a pure port-around, per the no-seams-ever canon.
//   • Under the legacy two-scene model the same trigger additively loads the hub
//     and warps — the primitive handles both worlds unchanged.
//
// WHY runtime-authored: the merged scene is builder-baked and NEVER hand-edited
// (CLAUDE.md §3); this mirrors the proven CavePortalRepointInjector pattern —
// [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] + sceneLoaded re-arm, WEBGL-safe
// try/catch on every entry, idempotent per load. No navmesh rebake is needed:
// the portal only needs its ProximityRadius to OVERLAP walkable surface (the
// hero warps from the navmesh edge; he need not touch the marker), and the
// position is NavMesh.SamplePosition-seated onto the baked mesh at author time.
//
// PLACEMENT: the south bridge deck spans r=44 (plinth face) → ~66 (outer end
// seats on overworld ground), centreline x=-4.5 (owner-locked pose). The portal
// anchors 6 m beyond the deck end at (-4.5, ·, -72) — on open, baked overworld
// ground the fleet has walked (bridge-crossing shot box x≈-4.4±5, z -50..-76) —
// with a 16 m radius that reaches back over the deck mouth. N/W/E are the same
// point yaw-rotated about the world origin, matching the bridge clones.
//
// AFFORDANCE (owner canon — no invisible triggers): each portal carries
//   • a code-built stone gate arch (two pillars + lintel, pale-gold banner cap —
//     shape/luminance read, colorblind-safe, colliders stripped so nothing blocks
//     or re-carves the mesh), and
//   • a PoiBeacon LANDMARK (the sanctioned far-field callout family, rendered by
//     PoiCalloutSystem) so "home" reads from range like other world targets, and
//   • the standard walk-up confirm prompt "Enter Elarion" (promptOverride ⇒
//     IsWalkUpEntry ⇒ the authored radius is honored, never travel-gated).
//
// ORACLE FIT: AutoPilotDriver.HomeReturnRoundTrip finds the nearest trigger with
// targetSceneName == ActiveScene() (Ordinal), walks to it, taps "Enter Elarion",
// and asserts |y-liftY|<=0.5 && r<44. These portals satisfy that predicate; the
// exit-seam pickers explicitly EXCLUDE target==hub, so exit coverage is untouched.
// =============================================================================

using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.World
{
    public static class HomeReturnPortalInjector
    {
        private const string PortalPrefix = "HomeReturnPortal_";
        private const string PromptLabel  = "Enter Elarion";

        /// <summary>South-bridge outer anchor: centreline x=-4.5 (locked pose), 6 m past the
        /// deck outer end (~r=66) at z=-72. N/W/E = this point yaw-rotated about the origin
        /// (South 0° · West 90° · North 180° · East 270°, the locked clone convention).</summary>
        private static readonly Vector3 SouthAnchor = new Vector3(-4.5f, 0f, -72f);
        private static readonly string[] Sides = { "South", "West", "North", "East" };

        /// <summary>Walk-up prompt radius (m). Reaches back over the bridge-deck mouth (portal
        /// sits 6 m past the deck end) so the prompt arms while the hero is still on walkable
        /// mesh — the seam-radius lesson: the radius must OVERLAP the baked surface.</summary>
        private const float PortalRadius = 16f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SafeBuild();   // also cover the scene already active at app start
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SafeBuild();

        // Never throw out of a sceneLoaded handler (an uncaught throw halts the WebGL player).
        private static void SafeBuild()
        {
            try { BuildReturnPortals(); }
            catch (System.Exception e)
            {
                Debug.LogWarning("[HomeReturn] portal authoring threw (non-fatal): " + e);
            }
        }

        /// <summary>Author the four hub-return portals on the merged overworld scene.
        /// Public so a test/probe can drive it directly. Idempotent per scene load.</summary>
        public static void BuildReturnPortals()
        {
            string active = SceneManager.GetActiveScene().name;
            if (!DeNelle.Core.HubScenes.IsOverworld(active)) return;   // overworld-only authoring

            // MERGED-WORLD SKIP (owner F8 2026-07-16, Main_Castle_Overworld, verbatim:
            // "should not have a enter elarion screen. that is wrong and needs to go").
            // Under ff.mergedworld the courtyard and the outer ring are ONE seamless scene
            // -- the hero NEVER leaves Elarion, so a "way back IN" gate is meaningless here
            // and the four "Enter Elarion" arches/prompts read as wrong (you are already in
            // Elarion; just walk back to the courtyard). WO-602 solved the TWO-SCENE problem
            // (leaving the standalone castle scene had no discoverable return seam); the merge
            // removes that problem, so these return portals are authored ONLY on the legacy
            // two-scene path. Navigation is unaffected under merge: the courtyard is walkable
            // from anywhere on the same navmesh. Reversible: turn ff.mergedworld OFF (or this
            // is a no-op there) to restore the WO-602 portals.
            if (DeNelle.Core.FeatureFlags.MergedWorld)
            {
                FlowTrace.Once("HomeReturn", "merged-skip",
                    "ff.mergedworld ON -- 'Enter Elarion' return portals NOT authored (one seamless " +
                    "scene; already in Elarion, walk back to the courtyard). WO-602 portals are " +
                    "legacy-two-scene only (owner F8 2026-07-16: the screen is wrong, removed).");
                return;
            }

            if (!DeNelle.Core.FeatureFlags.HomeReturnPortal)
            {
                FlowTrace.Once("HomeReturn", "flag-off",
                    "ff.homereturnportal OFF — return portals NOT authored (WO-602 fix disabled by flag).");
                return;
            }

            // Idempotency: a portal set already authored on this scene is left alone.
            var existing = Object.FindObjectsByType<SceneTransitionTrigger>();
            if (existing != null)
            {
                for (int i = 0; i < existing.Length; i++)
                {
                    var t = existing[i];
                    if (t != null && t.name != null &&
                        t.name.StartsWith(PortalPrefix, System.StringComparison.Ordinal))
                    {
                        FlowTrace.Step("HomeReturn",
                            $"portals already authored on '{active}' (found '{t.name}') — idempotent skip.");
                        return;
                    }
                }
            }

            using var _ = FlowTrace.Enter("HomeReturn", $"BuildReturnPortals on '{active}' (WO-602)");

            // Courtyard landing: the plinth-top centre, the same navmesh-proven point the
            // fleet's own courtyard warp uses ((0, liftY, 0); HeroLocomotion.WarpTo re-samples
            // onto the courtyard navmesh from there). liftY mirrors CastleHubBuilder's
            // PlayerPrefs-tunable island raise.
            // OWNER RULING 2026-08-05: (0, liftY, 0) is 12 m from the trunk centre and INSIDE the
            // tree's >=16 m canopy — landing here is landing in the tree. Prefer HubSpawnInjector's
            // tree-edge + 2 m, navmesh-seated hub point. requireCurrentScene:false because this is
            // deliberately a point in the hub scene we are travelling TO (this legacy two-scene path
            // authors from OUTSIDE the hub). Falls back to the old literal when it has not resolved.
            float liftY = PlayerPrefs.GetFloat("castle.liftY", 3f);
            Vector3 courtyard;
            if (!HubSpawnInjector.TryGetHubSpawn(out courtyard, requireCurrentScene: false))
                courtyard = new Vector3(0f, liftY, 0f);
            string hubTarget = DeNelle.Core.SceneRouter.Castle;   // merged: the active scene itself

            int built = 0;
            for (int i = 0; i < Sides.Length; i++)
            {
                float yaw = 90f * i;   // South 0 · West 90 · North 180 · East 270 (locked convention)
                Vector3 pos = Quaternion.Euler(0f, yaw, 0f) * SouthAnchor;

                // Seat the portal ON the baked navmesh so the hero's agent can actually path
                // into the prompt radius (an off-mesh marker strands an input-driven agent at
                // the mesh edge — the seam-radius lesson).
                bool seated = NavMesh.SamplePosition(pos, out NavMeshHit hit, 25f, NavMesh.AllAreas);
                if (seated) pos = hit.position;
                else
                    FlowTrace.Warn("HomeReturn",
                        $"portal {Sides[i]}: NavMesh.SamplePosition found no mesh within 25m of {pos} — " +
                        "placing at the raw anchor; the 16m radius must carry it (verify in the next fleet run).");

                var go = new GameObject(PortalPrefix + Sides[i]);
                go.transform.position = pos;
                go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);   // arch faces the castle like its bridge

                var trig = go.AddComponent<SceneTransitionTrigger>();
                trig.targetSceneName = hubTarget;        // == ActiveScene() under ff.mergedworld ⇒ reposition-only warp
                trig.targetPosition  = courtyard;
                trig.loadAdditive    = true;             // merged: no-op (already loaded); legacy: additive hub load
                trig.ProximityRadius = PortalRadius;
                trig.promptOverride  = PromptLabel;      // walk-up entry ⇒ authored radius honored, never travel-gated
                trig.suppressPrompt  = false;            // owner canon: no invisible triggers
                trig.requireConfirm  = true;             // legacy field (runtime is confirm-only regardless)

                BuildArchVisual(go.transform);

                // Far-field callout: the sanctioned Landmark beacon family (PoiCalloutSystem
                // renders it) so home reads from range like other world targets. Pale gold —
                // luminance/shape read, never hue-only (owner is red/green colorblind).
                PoiBeacon.Attach(go, PoiBeacon.PoiTier.Landmark,
                    calloutRadius: 300f, handoffRadius: 30f,
                    tint: new Color(1f, 0.94f, 0.72f, 1f));

                built++;
                FlowTrace.Step("HomeReturn",
                    $"portal '{go.name}' ONLINE @ {pos} (navmesh-seated={seated}, radius {PortalRadius}m) " +
                    $"-> '{hubTarget}' @ {courtyard} — prompt '{PromptLabel}'.");
            }

            FlowTrace.Step("HomeReturn",
                $"{built}/4 return portals authored on '{active}' — the way back home is wired (WO-602).");
        }

        // ---------------------------------------------------------------------
        // Code-built gate arch: two stone pillars + a lintel + a pale-gold cap.
        // Shape + luminance affordance (colorblind-safe). Colliders are stripped
        // so the arch neither blocks movement nor perturbs the baked navmesh.
        // ---------------------------------------------------------------------
        private static void BuildArchVisual(Transform parent)
        {
            var stone = MakeMat(new Color(0.55f, 0.53f, 0.50f, 1f), emissive: false);
            var gold  = MakeMat(new Color(1f, 0.94f, 0.72f, 1f), emissive: true);

            // Pillars flank the local X axis; the hero walks through along local Z.
            AddBlock(parent, "Pillar_L", new Vector3(-2.2f, 2.0f, 0f), new Vector3(0.9f, 4.0f, 0.9f), stone);
            AddBlock(parent, "Pillar_R", new Vector3( 2.2f, 2.0f, 0f), new Vector3(0.9f, 4.0f, 0.9f), stone);
            AddBlock(parent, "Lintel",   new Vector3(0f,   4.2f, 0f), new Vector3(5.6f, 0.6f, 1.0f), stone);
            AddBlock(parent, "BannerCap", new Vector3(0f,  4.8f, 0f), new Vector3(5.8f, 0.35f, 1.1f), gold);
        }

        private static void AddBlock(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);   // presentation only — never block or re-carve
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null && mat != null) mr.sharedMaterial = mat;
        }

        private static Material MakeMat(Color c, bool emissive)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return null;
            var m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            else m.color = c;
            if (emissive && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * 1.6f);
            }
            return m;
        }
    }
}
