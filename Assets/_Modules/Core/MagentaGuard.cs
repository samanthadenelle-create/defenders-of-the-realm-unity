// =============================================================================
// MagentaGuard — global runtime safety net for "pink/magenta" renderers (TKT-1).
// -----------------------------------------------------------------------------
// SYMPTOM (owner playtest, recurring): ground / objects render PINK/MAGENTA in the
// BUILT player but look fine in the editor. CAUSE: a material on a Built-in/Standard/
// Legacy shader — or a Shader Graph not referenced by a serialized material in a built
// scene — is STRIPPED from the URP player build. At runtime its shader resolves to
// Hidden/InternalErrorShader = magenta. This only happens in a BUILD (the editor never
// strips), so an editor-only scan finds nothing (the autopilot magenta probe ran in the
// editor-built player here and reported ZERO — the strip can't reproduce in-editor).
//
// WHY A GLOBAL GUARD (not yet another per-object fixer): the project already has ~8
// TARGETED fixers (Tripo / Polyperfect / Tree / Portal / Worker / HeroArmor …). Each
// only covers ITS own objects, so a NEW magenta source (e.g. a procedurally-instantiated
// Quaternius floor) slips through until someone hunts it object-by-object. This catches
// ANY magenta renderer in ANY scene at load, recovers it, AND logs exactly what it caught
// (hierarchy path / scene / material / dead shader) — so the offender self-identifies in
// the break-log instead of costing a guess-and-rebuild cycle (§12: instrument, don't guess).
//
// WHAT IT DOES (asset-independent, idempotent, WebGL-safe):
//   On every scene load, scan active Renderers' sharedMaterials; any material whose shader
//   is null / Hidden/InternalErrorShader / Standard / Legacy / Specular setup is swapped
//   IN PLACE to URP/Lit, carrying base colour + main texture + emission. Valid URP / Unlit
//   / Particles / UI shaders are left untouched. Each unique material is processed once and
//   already-URP materials are skipped, so repeated loads are no-ops.
//
// WHAT IT DELIBERATELY DOES *NOT* TOUCH (2026-08-05): an EMPTY material slot on a
//   ParticleSystemRenderer whose other slot(s) hold a real material — that is the vendor
//   particle/trail pair, not a break (see IsVendorParticleNullSlot). The guard's own
//   "recovery" there was a shipped visual regression, not a fix.
//
// Mirrors GroundZFightFixer's lifecycle: [RuntimeInitializeOnLoadMethod(AfterSceneLoad)]
// + SceneManager.sceneLoaded re-arm (the player boots into Title and reaches gameplay
// scenes later), every entry point wrapped in try/catch (an uncaught sceneLoaded
// exception halts the WebGL player).
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core
{
    public static class MagentaGuard
    {
        private static Shader _lit;
        private static Shader _terrainLit;
        private static bool _hooked;
        private static readonly HashSet<string> _floorSeen = new HashSet<string>();

        // -- WO-869 WIDENING #1: DEFERRED RE-SWEEP (the timing blind spot) ------------
        // PROVEN BY THE DUNGEON PORTAL (owner Seeker capture 2026-08-04, 08-portal-magenta.png):
        // Sweep() is a ONE-TIME Object.FindObjectsByType<Renderer>() snapshot taken on
        // AfterSceneLoad + sceneLoaded. But this project is full of builders that place their
        // objects SECONDS LATER, because they wait on something: DungeonWorldPortalSpawner
        // re-tries placement every PlaceRetryInterval (1s) until the OuterWorld scene AND a
        // baked NavMesh exist; the townsfolk / outpost / POI injectors do the same. Every one
        // of those objects is built AFTER the last sceneLoaded fired, so the snapshot sweep was
        // STRUCTURALLY BLIND to it and any magenta on it stayed magenta forever. That is the
        // exact class the raid-troop miss documented at the SweepGameObject seam below - it was
        // fixed there for ONE caller (VisualFactory.Skin) instead of for the class.
        //
        // FIX: after each scene load, run a small BOUNDED ladder of follow-up sweeps. Cheap
        // (a handful of scans over a scene's renderers, then it stops forever for that load),
        // and it needs no cooperation from the builders - which is the point, since requiring
        // every future builder to remember a call is how this recurred.
        private static readonly float[] DeferredSweepDelays = { 1.0f, 3.0f, 8.0f };
        private static GameObject _driverGo;

        // -- WO-869 WIDENING #2: PROTECTED PRIMITIVE ART (the "cure is worse" blind spot) --
        // The SECOND reason the guard missed the portal, and the more dangerous one: the portal
        // arch is built from GameObject.CreatePrimitive(PrimitiveType.Cube). So even on a sweep
        // that DID see it, IsPrimitivePlaceholder() returns true and the scene sweep (which runs
        // hideStrayPrimitives:true) would have set r.enabled = false - DELETING THE DUNGEON
        // ENTRANCE from the player's view rather than fixing its colour. "Portal is invisible"
        // is strictly worse than "portal is magenta": the whole feature is "stumble on a glowing
        // arch", and an invisible arch cannot be stumbled on OR reported.
        //
        // The primitive-hide heuristic is still right for its real target (stray placeholder
        // pills), so it stays - but it is now OPT-OUT-able. A builder that legitimately composes
        // art out of primitives registers its subtree here, and the sweep RECOVERS those
        // renderers (fresh URP/Lit) instead of hiding them. Explicit registration, not another
        // guessing heuristic: a heuristic is what produced this blind spot.
        private static readonly HashSet<int> _protectedArt = new HashSet<int>();

        /// <summary>
        /// Declare that every renderer under <paramref name="root"/> is DELIBERATE ART even if it
        /// is built from Unity primitives, so the magenta sweep RECOVERS it (repaints to a fresh
        /// URP/Lit) instead of HIDING it as a stray placeholder pill. Idempotent; safe to call on
        /// a subtree that is still being assembled (register after the renderers exist).
        /// <para/>
        /// Call this from any runtime builder that composes visible art out of CreatePrimitive
        /// (the dungeon portal arch is the founding case, WO-869). Never call it on a genuine
        /// placeholder - hiding those is the behaviour we want to keep.
        /// </summary>
        /// <param name="root">Subtree root. Null = no-op.</param>
        /// <param name="owner">The registering builder, e.g. "DungeonWorldPortalSpawner.BuildArch" -
        /// printed in the recovery trace so the source self-identifies.</param>
        public static void ProtectPrimitiveArt(GameObject root, string owner)
        {
            if (root == null) return;
            try
            {
                var rends = root.GetComponentsInChildren<Renderer>(true);
                if (rends == null) return;
                int added = 0;
                foreach (var r in rends)
                {
                    if (r == null) continue;
                    if (_protectedArt.Add(r.gameObject.GetInstanceID())) added++;
                }
                if (added > 0)
                    FlowTrace.Step("MagentaGuard",
                        $"ProtectPrimitiveArt: {added} renderer(s) under '{root.name}' registered as deliberate " +
                        $"primitive art by '{owner}' - the sweep will RECOVER them, never hide them.");
            }
            catch (System.Exception e)
            {
                FlowTrace.Warn("MagentaGuard", $"ProtectPrimitiveArt('{owner}') threw: {e.Message} - subtree left unprotected.");
            }
        }

        /// <summary>True when this renderer was registered via <see cref="ProtectPrimitiveArt"/>.</summary>
        private static bool IsProtectedArt(Renderer r)
            => r != null && _protectedArt.Count > 0 && _protectedArt.Contains(r.gameObject.GetInstanceID());

        // ── RUNTIME-SPAWN SEAM (magenta raid troops, owner 2026-08-02) ───────────────
        // PROVEN CAUSE: Init is [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] + sceneLoaded,
        // and Sweep takes a ONE-TIME snapshot via Object.FindObjectsByType<Renderer>(). There is
        // no Update, no re-arm and (until now) no per-object entry point. A raid troop is built
        // MID-RAID - TroopDeployer.SpawnFromArmy -> SpawnTroop -> TroopFactory.Build ->
        // VisualFactory.Skin - i.e. AFTER every sceneLoaded has fired, so the guard was
        // structurally BLIND to it and the body stayed magenta forever. SweepGameObject is the
        // missing per-object entry point; VisualFactory.Skin (the one choke point every runtime
        // body funnels through) calls it.
        //
        // The recovered-material cache is STATIC (was a per-sweep local) so a repeated troop
        // model - 8 footmen off one tap, all sharing one broken source material - is recovered
        // ONCE and every later body just re-uses that instance. Per-spawn Material allocation
        // would leak one material per troop and break SRP batching.
        //
        // _floorSeen is deliberately NOT reachable from this path: it is ground-only and
        // process-static, and that exact dedup already caused a silent miss (see the RCA at the
        // colorless-floor branch below). The ground/floor fix stays in the scene Sweep.
        private static readonly Dictionary<Material, Material> _freshFor = new Dictionary<Material, Material>();

        // One probe line per OFFENDER, not per slot-visit: a null material slot is keyed by
        // "<path>#<slot>" so the same body re-skinned 20x names itself once.
        private static readonly HashSet<string> _nullSlotSeen = new HashSet<string>();
        private static Material _nullSlotFresh;

        // A large, flat, ground-like renderer (by name or by footprint) — the candidates for a
        // "pink floor". Used only to NAME them in the diagnostic, never to auto-mutate (a terrain
        // shader is valid; we fix the named cause, not blindly swap the floor).
        private static bool IsGroundLike(Renderer r)
        {
            if (r == null) return false;
            string n = r.name.ToLowerInvariant();
            if (n.Contains("floor") || n.Contains("ground") || n.Contains("terrain") ||
                n.Contains("plaza") || n.Contains("courtyard")) return true;
            string mesh = MeshName(r).ToLowerInvariant();
            if (mesh.Contains("floor") || mesh.Contains("ground") || mesh.Contains("plane")) return true;
            var b = r.bounds; // big footprint, thin in Y = a floor slab
            return b.size.x > 8f && b.size.z > 8f && b.size.y < 2f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (_hooked) return;
            _hooked = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            // sceneLoaded does NOT fire for the scene already active at boot — sweep it now.
            Sweep("boot");
            ScheduleDeferredSweeps("boot");
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Sweep(scene.name);
            ScheduleDeferredSweeps(scene.name);
        }

        // -- WO-869 widening #1 - the bounded follow-up ladder ------------------------
        // Runs Sweep() again at +1s / +3s / +8s after a load so objects built by the
        // "wait for the scene + a baked NavMesh, THEN place" builders are actually SEEN.
        // Bounded and self-terminating: three extra scans per load, then nothing. Each
        // Sweep is already idempotent (recovered materials are URP/Lit and skip), so the
        // repeats cost a renderer walk and change nothing when everything is healthy.
        private static void ScheduleDeferredSweeps(string sceneName)
        {
            try
            {
                var driver = EnsureDriver();
                if (driver == null)
                {
                    FlowTrace.Warn("MagentaGuard",
                        $"ScheduleDeferredSweeps('{sceneName}'): no driver - late-built objects will only be " +
                        "covered by the per-object SweepGameObject seam.");
                    return;
                }
                driver.StartCoroutine(DeferredSweepRoutine(sceneName));
            }
            catch (System.Exception e)
            {
                FlowTrace.Warn("MagentaGuard", $"ScheduleDeferredSweeps('{sceneName}') threw: {e.Message}");
            }
        }

        private static IEnumerator DeferredSweepRoutine(string sceneName)
        {
            float previous = 0f;
            for (int i = 0; i < DeferredSweepDelays.Length; i++)
            {
                float wait = DeferredSweepDelays[i] - previous;
                previous = DeferredSweepDelays[i];
                if (wait > 0f) yield return new WaitForSeconds(wait);
                Sweep($"{sceneName}+{DeferredSweepDelays[i]:0.#}s");
            }
        }

        // A single hidden DontDestroyOnLoad host so the static guard can run coroutines.
        // Created on demand; survives scene loads so one host serves every load.
        private static MonoBehaviour EnsureDriver()
        {
            if (_driverGo != null)
            {
                var existing = _driverGo.GetComponent<MagentaGuardDriver>();
                if (existing != null) return existing;
            }
            _driverGo = new GameObject("[MagentaGuardDriver]") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(_driverGo);
            return _driverGo.AddComponent<MagentaGuardDriver>();
        }

        /// <summary>Coroutine host for the static guard's deferred sweeps. No state of its own.</summary>
        private sealed class MagentaGuardDriver : MonoBehaviour { }

        private static void Sweep(string sceneName)
        {
            try
            {
                if (_lit == null) _lit = Shader.Find("Universal Render Pipeline/Lit");
                if (_lit == null)
                {
                    FlowTrace.Warn("MagentaGuard", "no URP/Lit shader found — cannot recover magenta materials.");
                    return;
                }

                var renderers = Object.FindObjectsByType<Renderer>();
                if (renderers == null || renderers.Length == 0) return;

                // GROUND/FLOOR pass — scene-only. It owns the process-static `_floorSeen` dedup and is
                // deliberately NOT part of SweepRenderers (the shared per-renderer recovery the runtime
                // spawn seam also calls): a spawned troop is never "ground-like", and letting a name
                // dedup that spans the whole process anywhere near the spawn path is exactly the silent
                // miss the RCA below documents.
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    var mats = r.sharedMaterials;
                    if (mats == null) continue;

                    // FLOOR FIX (TKT-1, owner "still pink floor"): the Raid PROVED the lavender floor is
                    // the Quaternius courtyard (material 'MI_WoodTrim', shader 'Shader Graphs/M_BaseMaterial')
                    // rendering an UNTEXTURED white surface the scene lighting tints lavender — its albedo
                    // didn't survive the build. NOT bright magenta, so the broken-shader recovery skipped it.
                    // So: a GROUND-LIKE renderer on a NON-URP-Lit, non-particles/terrain shader → retarget to
                    // URP/Lit, carrying any albedo texture we can find (Quaternius uses '_Base_Color'); if none
                    // survived, set a warm wood/stone base so it's NEVER lavender. Mutates the SHARED material
                    // once (all tiles fixed); idempotent (re-runs see URP/Lit and skip). Textured floors are
                    // left alone. Quiet: a single Step, not an error per tile.
                    if (_lit != null && IsGroundLike(r))
                    {
                        // PROVEN (RCA 2026-06-21): the colorless RE-CHECK below must NOT sit behind the
                        // `_floorSeen.Add` name-dedup — `_floorSeen` is process-static, so a name consumed on
                        // an EARLIER sweep made `Add` return false on the sweep where the tile is actually
                        // colorless, and the branch NEVER fired (no FLOOR-FIX(colorless) log, tiles stayed
                        // (0,0,0,0)). The dedup is only correct for the one-time nonLit IDENTITY swap; the
                        // colorless fix tests a mutable STATE and must run every sweep (self-idempotent:
                        // once repainted opaque, colorless=false -> no-op).
                        bool firstName = _floorSeen.Add(r.name);
                        var fm = mats.Length > 0 ? mats[0] : null;
                        string fsh = fm != null && fm.shader != null ? fm.shader.name : "";
                        bool nonLit = fm != null && fm.shader != null
                                      && fsh != "Universal Render Pipeline/Lit"
                                      && fsh.IndexOf("Particles", System.StringComparison.OrdinalIgnoreCase) < 0
                                      && fsh.IndexOf("Terrain", System.StringComparison.OrdinalIgnoreCase) < 0
                                      && fsh.IndexOf("Unlit", System.StringComparison.OrdinalIgnoreCase) < 0;
                        if (firstName && nonLit)
                        {
                            Texture tex = null;
                            foreach (var prop in new[] { "_BaseMap", "_MainTex", "_Base_Color", "_BaseColorMap", "_Albedo", "_AlbedoMap" })
                                if (fm.HasProperty(prop) && fm.GetTexture(prop) != null) { tex = fm.GetTexture(prop); break; }
                            fm.shader = _lit;
                            if (tex != null && fm.HasProperty("_BaseMap")) fm.SetTexture("_BaseMap", tex);
                            else if (fm.HasProperty("_BaseColor")) fm.SetColor("_BaseColor", new Color(0.42f, 0.34f, 0.24f, 1f)); // warm wood — never lavender
                            FlowTrace.Step("MagentaGuard",
                                $"FLOOR-FIX '{fm.name}' (was '{fsh}', scene '{r.gameObject.scene.name}') -> URP/Lit, albedoTex={(tex != null)} — lavender floor corrected.");
                        }
                        else if (fm != null && fsh == "Universal Render Pipeline/Lit")
                        {
                            // PROVEN (RCA 2026-06-21, floordiag-probe2.log): the visible castle floor is the
                            // CourtyardFloor_* tiles — URP/Lit, no _BaseMap. Their material is FBX-EMBEDDED
                            // (Floor_WoodDark.fbx, externalObjects:{}), so fm.HasProperty("_BaseColor") is FALSE.
                            // The PRIOR fix guarded the whole branch on HasProperty("_BaseColor") -> it short-
                            // circuited and NEVER fired (zero FLOOR-FIX lines in the full log) — THIS is why every
                            // floor fix (incl. mine) missed it. Under the lavender Trilight ambient a colorless lit
                            // tile renders pink. Fix: treat colorless OR property-missing as "needs paint", and
                            // ASSIGN A FRESH URP/Lit material to the renderer (mutating the embedded SHARED material
                            // may not stick in a built player). Self-idempotent: once repainted opaque, colorless=false.
                            bool hasTex = fm.HasProperty("_BaseMap") && fm.GetTexture("_BaseMap") != null;
                            bool hasBC  = fm.HasProperty("_BaseColor");
                            Color bc    = hasBC ? fm.GetColor("_BaseColor") : Color.clear;
                            bool colorless = !hasBC || bc.a < 0.05f || (bc.r + bc.g + bc.b) < 0.05f;
                            if (!hasTex && colorless)
                            {
                                // §12 proof line: logs the disambiguator (hasBC) at fix time — captured to the full
                                // Player.log (Step lands there; the break-log is errors-only — see BreakCaptureHarness).
                                var fresh = new Material(_lit) { name = fm.name + "_FloorFix" };
                                fresh.SetColor("_BaseColor", new Color(0.42f, 0.34f, 0.24f, 1f));   // opaque warm wood/stone
                                if (fresh.HasProperty("_Surface")) fresh.SetFloat("_Surface", 0f);  // URP: 0 = Opaque
                                if (fresh.HasProperty("_ZWrite"))  fresh.SetFloat("_ZWrite", 1f);
                                fresh.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                                var sm = r.sharedMaterials;
                                if (sm != null && sm.Length > 0) { sm[0] = fresh; r.sharedMaterials = sm; }
                                FlowTrace.Step("MagentaGuard",
                                    $"FLOOR-FIX(colorless URP/Lit) '{fm.name}' (scene '{r.gameObject.scene.name}') hasBaseColorProp={hasBC} was {bc} -> FRESH opaque warm wood (pink floor corrected).");
                            }
                        }
                    }
                }

                // BROKEN-MATERIAL pass — shared with the runtime spawn seam (SweepGameObject).
                SweepRenderers(renderers, sceneName, hideStrayPrimitives: true,
                               out int recovered, out int hiddenStray);

                // TERRAIN PASS (owner F8 2026-06-21 "Pink Floor", ran-from-exe): MainCastle_Hall's VISIBLE
                // floor is the Terrain in the merged world (the wood courtyard tiles are dropped to
                // Y=-0.5 by GroundZFightFixer and hidden). A Terrain is NOT a Renderer, so the loop above
                // never sees it AND the IsGroundLike path excludes "Terrain" shaders — so a stripped URP
                // Terrain/Lit shader (-> InternalError = pink) is structurally unreachable by the code above.
                // Recover it here. GUARDED: only acts when the terrain's shader is genuinely broken, so if the
                // pink is from something else (lighting), this is a safe no-op. SOURCE fix is to pin
                // "Universal Render Pipeline/Terrain/Lit" in the build (ExteriorTerrainBuilder.EnsureTerrainShaderIncluded).
                // ── FLOOR-DIAG (owner F8 2026-06-21 "still pink, PERSISTENT") ─────────────────
                // The narrow shader-broken fix didn't resolve it, so DUMP the full ground state on
                // every hub/merged-world load — the next F8 names the exact pink surface + cause. All
                // [Flow:FloorDiag] Fail lines land in break-log.jsonl. REMOVE once root-caused.
                // Causes this distinguishes: (a) terrain shader stripped (-> InternalError), (b) terrain
                // LAYER missing its diffuse texture (magenta even with a valid shader — the modified
                // Exterior_*.terrainlayer are suspects), (c) null terrain material, (d) a violet/tinted
                // scene light making a white floor read lavender, (e) the visible floor is a Renderer not
                // the Terrain. Keeps the broken-shader auto-recovery.
                int terrainFixed = 0;
                if (_terrainLit == null) _terrainLit = Shader.Find("Universal Render Pipeline/Terrain/Lit");
                var terrains = Object.FindObjectsByType<Terrain>();
                FlowTrace.Step("FloorDiag", $"sweep '{sceneName}': {(terrains != null ? terrains.Length : 0)} Terrain(s); URP-Terrain-Lit-found={_terrainLit != null}.");
                if (terrains != null)
                {
                    foreach (var t in terrains)
                    {
                        if (t == null) continue;
                        var tm = t.materialTemplate;
                        string sh = (tm != null && tm.shader != null) ? tm.shader.name : "<null-mat-or-shader>";
                        string layers = "<no terrainData>";
                        var td = t.terrainData;
                        if (td != null && td.terrainLayers != null)
                        {
                            var lb = new System.Text.StringBuilder();
                            for (int li = 0; li < td.terrainLayers.Length; li++)
                            {
                                var L = td.terrainLayers[li];
                                string dt = (L != null && L.diffuseTexture != null) ? L.diffuseTexture.name : "<NULL-DIFFUSE>";
                                lb.Append("[" + li + "]" + (L != null ? L.name : "<null>") + ":" + dt + " ");
                            }
                            layers = td.terrainLayers.Length + " layer(s): " + lb;
                        }
                        FlowTrace.Step("FloorDiag",
                            "TERRAIN '" + t.name + "' scene='" + t.gameObject.scene.name + "' pos=" + t.transform.position +
                            " mat='" + (tm != null ? tm.name : "<NULL>") + "' shader='" + sh + "' broken=" + (tm != null && IsBrokenShader(tm.shader)) + " " + layers);

                        // WO-1602: the same measurement, reduced to a ONE-LINE VERDICT under the
                        // [Flow:Terrain] tag the ticket asks for. The FloorDiag line above already
                        // carries every layer name and marks a missing texture <NULL-DIFFUSE>, but it
                        // is a long line a human has to parse, and the question the ticket actually
                        // asks — "did the terrain layers arrive, or is the ground showing a base
                        // colour?" — is a yes/no. Making it greppable is the difference between a
                        // trace that exists and a trace that answers.
                        //
                        // TIMING IS THE OTHER HALF, AND IT LIVES ELSEWHERE ON PURPOSE: this block
                        // runs on the bounded MagentaGuard sweep ladder only (load + 1/3/8 s). The
                        // owner's bad frames are at ~2 and ~5 MINUTES, far past the last sweep, so
                        // this line alone can never say whether the state held. AtmosphereProbe
                        // (Assets/_Modules/Village/World/AtmosphereProbe.cs) re-reads the same
                        // terrain fields out to T+300s; read the two together.
                        int terrainLayerCount = 0, terrainLayersMissing = 0;
                        if (td != null && td.terrainLayers != null)
                        {
                            terrainLayerCount = td.terrainLayers.Length;
                            for (int li = 0; li < td.terrainLayers.Length; li++)
                            {
                                var L2 = td.terrainLayers[li];
                                if (L2 == null || L2.diffuseTexture == null) terrainLayersMissing++;
                            }
                        }
                        FlowTrace.Step("Terrain",
                            "BIND '" + t.name + "' scene='" + t.gameObject.scene.name + "' mat='" +
                            (tm != null ? tm.name : "<NULL>") + "' shader='" + sh + "' layers=" + terrainLayerCount +
                            " layersMissingBaseColor=" + terrainLayersMissing +
                            (terrainLayersMissing > 0
                                ? " <-- PLACEHOLDER/UNSTREAMED: the ground is drawing a base colour, not its art."
                                : " (every layer carries a real base-colour texture at sweep time)."));
                        // PROVEN (RCA 2026-07-15; Player.log 07-14 22:10, the ONE FloorDiag TERRAIN line):
                        //   TERRAIN 'ExteriorTerrain' ... mat='<NULL>' shader='<null-mat-or-shader>' broken=False
                        // The scene references the terrain material by GUID (Main_Castle_Overworld.unity:16016
                        // -> 0eb083914b7ffae4eaf721e2353fea0b) but Assets/Generated/ was GITIGNORED, so
                        // ExteriorTerrainMaterial.mat was NEVER IN GIT (`git log --all -- <it>` = empty) — it only
                        // ever existed as a bake artifact on whichever machine last ran the terrain bake. Its
                        // siblings (ExteriorTerrainData.asset + the 5 .terrainlayers) ARE tracked, committed before
                        // the ignore rule landed — which is why the ground is WALKABLE (geometry survived) but
                        // MAGENTA (material did not). On any machine that never baked: dangling GUID ->
                        // materialTemplate == NULL -> the Terrain draws with the engine default = MAGENTA under URP.
                        // WHY THE OLD FIX COULD NEVER FIRE: the condition read `tm != null && IsBrokenShader(...)`,
                        // so a NULL material SHORT-CIRCUITED to false and the recovery was skipped — which is
                        // exactly why the log says broken=False on a terrain that is very much broken. Same class
                        // of blind spot as the HasProperty("_BaseColor") short-circuit fixed at :140.
                        // A NULL material IS the break. Treat it as one and assign a fresh URP/Terrain/Lit.
                        // Idempotent: once assigned, tm != null with a valid shader -> no-op on later sweeps.
                        bool terrainBroken = tm == null || IsBrokenShader(tm.shader);
                        if (terrainBroken && _terrainLit != null)
                        {
                            if (tm == null)
                            {
                                t.materialTemplate = new Material(_terrainLit) { name = "ExteriorTerrainMaterial_Recovered" };
                                // FAIL (not Warn): magenta ground in a shipped player IS a break — surface it in
                                // the F8 break-log so the source loss self-identifies instead of costing a cycle.
                                FlowTrace.Fail("FloorDiag",
                                    "-> recovered TERRAIN '" + t.name + "' NULL materialTemplate -> fresh URP Terrain/Lit " +
                                    "(magenta ground killed at runtime; SOURCE fix = restore Assets/Generated/Terrain/ExteriorTerrainMaterial.mat).");
                            }
                            else
                            {
                                tm.shader = _terrainLit;
                                FlowTrace.Warn("FloorDiag", "-> recovered TERRAIN '" + t.name + "' broken shader -> URP Terrain/Lit.");
                            }
                            terrainFixed++;
                        }
                    }
                }
                // Scene lighting — a violet/tinted ambient or sun makes a plain floor read lavender.
                var sun = RenderSettings.sun;
                FlowTrace.Step("FloorDiag",
                    "LIGHTING scene='" + sceneName + "' ambientMode=" + RenderSettings.ambientMode + " ambient=" + RenderSettings.ambientLight +
                    " sun=" + (sun != null ? (sun.name + " color=" + sun.color + " intensity=" + sun.intensity) : "<none>"));
                // Ground-like Renderers (in case the visible floor is a Renderer, not the Terrain).
                int gdump = 0;
                foreach (var r in Object.FindObjectsByType<Renderer>())
                {
                    if (r == null || !IsGroundLike(r) || gdump >= 12) continue;
                    var m0 = (r.sharedMaterials != null && r.sharedMaterials.Length > 0) ? r.sharedMaterials[0] : null;
                    string gsh = (m0 != null && m0.shader != null) ? m0.shader.name : "<null>";
                    Color gc = (m0 != null && m0.HasProperty("_BaseColor")) ? m0.GetColor("_BaseColor")
                             : (m0 != null && m0.HasProperty("_Color") ? m0.color : Color.clear);
                    FlowTrace.Step("FloorDiag", "GROUND '" + HierarchyPath(r.transform) + "' scene='" + r.gameObject.scene.name +
                        "' shader='" + gsh + "' baseColor=" + gc + " size=" + r.bounds.size);
                    gdump++;
                }

                if (recovered > 0 || hiddenStray > 0 || terrainFixed > 0)
                    FlowTrace.Step("MagentaGuard",
                        $"sweep '{sceneName}': recovered {recovered} lost-shader material(s) -> URP/Lit, hid {hiddenStray} stray placeholder primitive(s), fixed {terrainFixed} terrain(s).");
            }
            catch (System.Exception e)
            {
                FlowTrace.Fail("MagentaGuard", $"sweep '{sceneName}' threw: {e.Message}");
            }
        }

        // =====================================================================
        //  PER-OBJECT ENTRY POINT — the runtime-spawn seam (magenta raid troops)
        // =====================================================================

        /// <summary>
        /// Recovers every magenta renderer under <paramref name="root"/> RIGHT NOW, without waiting
        /// for a scene load. This is the entry point the scene-load-only guard never had: a raid
        /// troop is instantiated mid-raid (TroopDeployer.SpawnFromArmy -> TroopFactory.Build ->
        /// VisualFactory.Skin), long after the last sceneLoaded, so the snapshot sweep could never
        /// see it. Includes INACTIVE children (a body can be dressed/enabled a frame later).
        /// <para/>
        /// NEVER THROWS and never logs at error severity for a merely-absent art pack: this runs
        /// inside the shared skinning choke point, and an uncaught exception here halts the WebGL
        /// player (see the header note on the sceneLoaded lifecycle).
        /// </summary>
        /// <param name="root">The freshly built body (or any subtree). Null = no-op.</param>
        /// <param name="cause">The calling seam, e.g. "VisualFactory.Skin" — printed as cause= on
        /// every probe line so a capture says WHICH path produced the offender.</param>
        public static void SweepGameObject(GameObject root, string cause)
        {
            try
            {
                if (root == null) return;

                // Resolve URP/Lit through the ROBUST path (Shader.Find can return null in a stripped
                // player build; ResolveUrpLitShader then borrows a live one out of the loaded scene).
                if (ResolveUrpLitShader() == null)
                {
                    // A gitignored art pack that was never imported lands here. WARN + continue —
                    // never an error, never a throw: the caller still gets its (unrecovered) body.
                    FlowTrace.Warn("MagentaProbe",
                        $"cause={cause} obj='{root.name}': no URP/Lit shader resolvable - cannot recover " +
                        "magenta materials on this body (art pack not imported / shader stripped). Skipped.");
                    return;
                }

                var rends = root.GetComponentsInChildren<Renderer>(true);
                if (rends == null || rends.Length == 0) return;

                // hideStrayPrimitives:false — TroopFactory's model-missing fallback IS a deliberate
                // tinted primitive capsule; disabling it would make the troop invisible-but-alive.
                SweepRenderers(rends, cause, hideStrayPrimitives: false, out int recovered, out int hiddenStray);
                if (recovered > 0 || hiddenStray > 0)
                    FlowTrace.Step("MagentaProbe",
                        $"cause={cause} obj='{root.name}': recovered {recovered} magenta material(s), " +
                        $"hid {hiddenStray} stray placeholder(s) across {rends.Length} renderer(s).");
            }
            catch (System.Exception e)
            {
                // A magenta body is a cosmetic defect; a throw out of the skinner is a dead player.
                FlowTrace.Fail("MagentaProbe",
                    $"cause={cause} obj='{(root != null ? root.name : "<null>")}': sweep threw " +
                    $"{e.GetType().Name}: {e.Message} - swallowed (body kept as-is).");
            }
        }

        /// <summary>
        /// The shared per-renderer magenta recovery. Called by the scene <see cref="Sweep"/> with the
        /// scene-wide snapshot AND by <see cref="SweepGameObject"/> with one freshly spawned body, so
        /// both seams recover identically instead of drifting apart.
        /// <para/>
        /// Deliberately does NOT contain the ground/floor fix — that owns the process-static
        /// <c>_floorSeen</c> name dedup and stays in <see cref="Sweep"/>.
        /// </summary>
        public static void SweepRenderers(IList<Renderer> renderers, string context)
            => SweepRenderers(renderers, context, hideStrayPrimitives: true, out _, out _);

        private static void SweepRenderers(IList<Renderer> renderers, string context,
                                           bool hideStrayPrimitives,
                                           out int recovered, out int hiddenStray)
        {
            recovered = 0;
            hiddenStray = 0;
            if (renderers == null || renderers.Count == 0) return;
            if (_lit == null && ResolveUrpLitShader() == null) return;

            for (int ri = 0; ri < renderers.Count; ri++)
            {
                var r = renderers[ri];
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats == null) continue;

                // Is ANY slot an offender? A NULL slot counts: under URP an unassigned submesh
                // material draws with the engine default = MAGENTA (the same visible defect as a
                // stripped shader), so the old `m != null &&` detection silently skipped the very
                // case it was written to catch.
                // ...EXCEPT on a ParticleSystemRenderer that carries a real material in another
                // slot — there an empty slot is the VENDOR TRAIL CONVENTION, not a break. See
                // IsVendorParticleNullSlot: this is the F8 2026-08-05 false positive, and the
                // "recovery" it triggered was itself the shipped regression.
                bool particleTrailConvention = IsVendorParticleNullSlot(r, mats);
                Material brokenMat = null;
                bool anyOffender = false;
                foreach (var m in mats)
                {
                    if (m == null) { if (!particleTrailConvention) anyOffender = true; continue; }
                    if (IsBrokenShader(m.shader)) { brokenMat = m; anyOffender = true; break; }
                }
                if (!anyOffender) continue;
                string deadShader = brokenMat != null && brokenMat.shader != null ? brokenMat.shader.name : "<null>";

                // A magenta BUILT-IN PRIMITIVE (Capsule/Cube/Sphere...) is never intended art — it is a
                // stray placeholder pill (CastleSpawnMarkerHider exists for exactly these). Real art
                // would carry a valid URP material. So HIDE the primitive (matches the hider's intent)
                // rather than recolour it to a grey pill that still litters the scene.
                //
                // NOT on the runtime-spawn path: TroopFactory's model-missing FALLBACK is a deliberate
                // tinted CreatePrimitive capsule (TroopFactory.cs ~:96) — hiding it would turn "troop
                // rendered as a blue pill" into "troop is invisible but still fights", a strictly worse
                // bug. The scene sweep keeps the hide; a spawn-seam primitive is recovered instead.
                //
                // WO-869: ...UNLESS the subtree was registered via ProtectPrimitiveArt. The dungeon
                // portal arch IS three CreatePrimitive cubes, so the unguarded hide would have made
                // the dungeon entrance INVISIBLE rather than fixed its colour - a strictly worse bug
                // than the magenta it was hunting. A protected renderer falls through to the normal
                // recovery below and gets a fresh URP/Lit instead.
                if (brokenMat != null && hideStrayPrimitives && IsPrimitivePlaceholder(r) && !IsProtectedArt(r))
                {
                    r.enabled = false;
                    hiddenStray++;
                    FlowTrace.Fail("MagentaGuard",
                        $"hid stray MAGENTA placeholder '{HierarchyPath(r.transform)}' (scene '{r.gameObject.scene.name}') " +
                        $"mesh='{MeshName(r)}' material '{brokenMat.name}' dead-shader '{deadShader}' - built-in primitive, not real art.");
                    continue;
                }
                if (brokenMat != null && hideStrayPrimitives && IsPrimitivePlaceholder(r) && IsProtectedArt(r))
                    FlowTrace.Once("MagentaGuard", $"protected:{HierarchyPath(r.transform)}",
                        $"PROTECTED primitive art '{HierarchyPath(r.transform)}' (scene '{r.gameObject.scene.name}') " +
                        $"is MAGENTA (material '{brokenMat.name}', dead-shader '{deadShader}') - RECOVERING it " +
                        "instead of hiding it (ProtectPrimitiveArt). Fix at source.");

                // Real art that merely lost its shader (or its whole material): recover each unique
                // broken source to a FRESH URP/Lit (carrying colour/albedo/emission) and ASSIGN it into
                // the renderer's shared-materials array so it STICKS in the built player (the in-place
                // mutation the old path used did not stick — the arcane-tower white symptom).
                var work = r.sharedMaterials;   // mutable copy of this renderer's slots
                bool changed = false;
                for (int mi = 0; mi < work.Length; mi++)
                {
                    var m = work[mi];
                    bool nullSlot = m == null;
                    // Vendor trail-only particle slot: leave it EMPTY. Assigning anything here is the
                    // white-blob regression, and there is nothing to recover — no art is missing.
                    if (nullSlot && particleTrailConvention) continue;
                    if (!nullSlot && !IsBrokenShader(m.shader)) continue;

                    Material fresh;
                    if (nullSlot)
                    {
                        // ALL-NULL ParticleSystemRenderer — a genuine defect (nothing at all to draw),
                        // but URP/Lit is NEVER the right paint for a particle pass: opaque lit geometry
                        // where a soft additive quad belongs is how a ground rune became a white blob.
                        // This file resolves only URP/Lit + URP/Terrain/Lit — no particle shader — so
                        // REPORT it at error severity and leave the slots alone. Fix at source (assign
                        // the pack's particle material), which the probe line names exactly.
                        if (r is ParticleSystemRenderer)
                        {
                            if (_nullSlotSeen.Add(HierarchyPath(r.transform) + "#" + mi))
                                Probe(context, r, mi, null, wasRecovered: false);
                            continue;
                        }
                        // Mesh / SkinnedMesh null slot — unchanged: one shared white URP/Lit for every
                        // empty slot in the process. Re-created if a previous scene unload destroyed it
                        // (Unity fake-null).
                        if (_nullSlotFresh == null)
                            _nullSlotFresh = new Material(_lit) { name = "NullSlot_MagentaFix" };
                        fresh = _nullSlotFresh;
                        if (_nullSlotSeen.Add(HierarchyPath(r.transform) + "#" + mi))
                        {
                            recovered++;
                            Probe(context, r, mi, null, wasRecovered: true);
                        }
                    }
                    else if (!_freshFor.TryGetValue(m, out fresh) || fresh == null)
                    {
                        // STATIC cache: a repeated troop model is recovered ONCE for the whole process,
                        // not once per spawned body (which would leak a Material per troop and break
                        // SRP batching). `fresh == null` re-builds after a scene unload destroyed it.
                        fresh = BuildRecoveredMaterial(m);   // fresh URP/Lit, once per unique source
                        _freshFor[m] = fresh;
                        recovered++;
                        Probe(context, r, mi, m, wasRecovered: true);
                        // WARN (not Fail): this line is a RECOVERY (the material was fixed in place), not a
                        // failure, and it fired ~8x per castle load — flooding the errors-only break-log and
                        // masking the owner's real F8 flags.
                        //
                        // 2026-08-05: the de-flood NEVER ACTUALLY WORKED. This Warn was only the SECOND of
                        // two lines per offender — the companion probe line above still went out through
                        // FlowTrace.Fail, so every recovery kept landing in the errors-only break-log
                        // anyway (that is how the Hovl false positive reached the owner's F8 capture as an
                        // ERROR). Probe() now takes wasRecovered and picks the severity itself: a recovered,
                        // understood case logs at WARN (Player.log only, still naming the exact object so
                        // the source can be fixed at root); a genuine UNRECOVERED break still logs at FAIL
                        // and still trips the F8 capture. Recovery behavior unchanged.
                        FlowTrace.Warn("MagentaGuard",
                            $"recovered MAGENTA renderer '{HierarchyPath(r.transform)}' (scene '{r.gameObject.scene.name}') " +
                            $"material '{m.name}' dead-shader '{(m.shader != null ? m.shader.name : "<null>")}' " +
                            "-> FRESH URP/Lit (assigned to renderer so it sticks; fix at source).");
                    }
                    work[mi] = fresh;
                    changed = true;
                }
                if (changed) r.sharedMaterials = work;   // assignment is what makes the recovery stick
            }
        }

        /// <summary>
        /// The ONE diagnosable line per offender — never a silent repaint. Emitted once per unique
        /// source material (or per null slot), so a body respawned 20x names itself once and the
        /// errors-only break-log is not flooded.
        /// <para/>
        /// SEVERITY MATCHES OUTCOME (2026-08-05): <paramref name="wasRecovered"/> true = the slot was
        /// repainted, so this is an understood, already-handled case and logs at WARN (Player.log only,
        /// out of the errors-only F8 break-log). False = we could NOT recover it and the defect is
        /// still on screen, so it stays at FAIL and still trips the F8 capture. Before this the line
        /// was unconditionally Fail, which is why the comment at the recovery site claimed a de-flood
        /// that had never taken effect.
        /// <para/>
        /// class= splits the five magenta causes so a capture says WHICH one to fix at source:
        ///   M1 shader stripped from the build (null / Hidden/InternalErrorShader)
        ///   M2 material slot is NULL (dangling GUID / never assigned)
        ///   M3 Built-in pipeline shader (Standard / Specular setup / Legacy) under URP
        ///   M5 shader present + named but !isSupported (failed to compile on-device)
        /// <para/>
        /// On a NULL slot the shader/supported columns are hardcoded placeholders, NOT measurements —
        /// there is no shader to interrogate. Never read `supported=false` on an M2 line as evidence
        /// of a shader problem (that misread is what made the Hovl trail slot look like a real break).
        /// </summary>
        private static void Probe(string cause, Renderer r, int slot, Material m, bool wasRecovered)
        {
            var sh = m != null ? m.shader : null;
            string matName = m != null ? m.name : "NULL";
            string shName = sh != null ? sh.name : "NULL";
            string supported = sh != null ? sh.isSupported.ToString() : "n/a";
            string line =
                $"{(wasRecovered ? "RECOVERED" : "FAIL")} cause={cause} obj='{HierarchyPath(r.transform)}' slot={slot} " +
                $"material='{matName}' shader='{shName}' supported={supported} class={ClassifyMagenta(m)}";
            if (wasRecovered) FlowTrace.Warn("MagentaProbe", line);
            else FlowTrace.Fail("MagentaProbe", line);
        }

        // -- VENDOR PARTICLE / TRAIL CONVENTION (F8 false positive, 2026-08-05) ------------
        // PROVEN AT SOURCE, "Assets/Hovl Studio/Magic circles/Prefabs/Loop version/Magic circle
        // electro loop.prefab" (ParticleSystemRenderer &199707102822579710, GameObject
        // 'ElectricyCenter'): m_Materials is [ {fileID: 0}, Trail21cg.mat ]. On a
        // ParticleSystemRenderer slot 0 is the PARTICLE material and slot 1 is the TRAIL material,
        // so a trail-only system leaves slot 0 LITERALLY EMPTY by vendor design. That is an empty
        // slot, not a dangling GUID — no art is missing and nothing renders magenta, because with
        // no particle material there is simply nothing drawn for the particle pass. 28 of the 261
        // Hovl prefabs ship this pattern.
        //
        // The guard used to call that slot an offender and assign the shared OPAQUE URP/Lit
        // ("NullSlot_MagentaFix") into it. The assignment sat OUTSIDE the dedupe, so it was
        // unconditional and STUCK in the built player: the aura's ground rune shipped to Android as
        // a WHITE OPAQUE BLOB. The "recovery" was the real regression; the reported break was not
        // real. IsBrokenShader deliberately passes Particles shaders — but the null-slot branch
        // short-circuited before that whitelist was ever consulted, so it never protected particles.
        //
        // THE TEST: on a ParticleSystemRenderer a null slot is LEGITIMATE as long as some OTHER slot
        // holds a valid material (that is the particle/trail pair). An ALL-null particle renderer has
        // nothing to draw at all and IS still a genuine defect — it is reported, never repainted.
        // Mesh / SkinnedMesh renderers are untouched by this: their null slots stay real offenders.
        private static bool IsVendorParticleNullSlot(Renderer r, Material[] mats)
        {
            if (!(r is ParticleSystemRenderer) || mats == null) return false;
            foreach (var m in mats)
                if (m != null) return true;   // some OTHER slot is valid -> the empty one is by design
            return false;                     // every slot null -> a real defect, keep reporting it
        }

        /// <summary>Which of the five magenta classes this material is (see <see cref="Probe"/>).</summary>
        private static string ClassifyMagenta(Material m)
        {
            if (m == null) return "M2";
            var sh = m.shader;
            if (sh == null) return "M1";
            string sn = sh.name;
            if (string.IsNullOrEmpty(sn) || sn.Contains("InternalError")) return "M1";
            if (sn == "Standard" || sn == "Standard (Specular setup)" || sn.StartsWith("Legacy Shaders/")) return "M3";
            if (!sh.isSupported) return "M5";
            return "OK";
        }

        /// <summary>
        /// THE single authority for "would this shader render MAGENTA (or grey/wrong) under URP, or is
        /// it missing entirely". Valid URP / Unlit / Particles / UI / Skybox shaders are NOT broken and
        /// are left untouched.
        /// <para/>
        /// PUBLIC ON PURPOSE (2026-08-02). GhostPreview and EquipmentController each carried a LOCAL
        /// copy of this predicate and BOTH had DRIFTED: neither had the <c>!sh.isSupported</c> branch,
        /// i.e. both were structurally blind to the ANDROID / on-device case. A shader that compiles in
        /// the editor and fails against the device graphics API KEEPS ITS NAME, so every name-only test
        /// below waves it through while it renders magenta on the phone. Both copies are now deleted and
        /// call this.
        /// <para/>
        /// Do NOT re-privatise. An unreachable authority is exactly WHY the copies were written in the
        /// first place ("kept local so this silo never edits MagentaGuard") - and that reasoning is how
        /// the drift got sanctioned in review. ShaderPredicateSingleAuthorityRegression fails if this
        /// stops being public, stops testing isSupported, or if a second definition reappears anywhere.
        /// </summary>
        public static bool IsBrokenShader(Shader sh)
        {
            if (sh == null) return true;
            string sn = sh.name;
            if (string.IsNullOrEmpty(sn)) return true;
            // Android magenta/white slab: a shader that FAILS to compile on-device keeps its NAME
            // (so the name-only checks below skip it) but renders magenta/white with isSupported==false.
            // Flag it so the recovery re-assigns a fresh URP/Lit (which compiles + sticks).
            if (!sh.isSupported) return true;
            return sn == "Standard"
                || sn == "Standard (Specular setup)"
                || sn.StartsWith("Legacy Shaders/")
                || sn.Contains("InternalError")
                || sn.Contains("Hidden/InternalError");
        }

        // Build a FRESH URP/Lit material carrying the authored colour + albedo + emission read
        // robustly from the dead/stripped SOURCE. Returning a fresh instance (assigned to the
        // renderer by the caller) is what makes the recovery STICK in a built player — the old
        // in-place `src.shader = _lit` mutation of an embedded/shared material did not survive the
        // build (the arcane-tower white symptom; same class as the FLOOR fix at :142).
        //
        // ROBUST READ: the authored channels are exposed when the source shader still declares them
        // (e.g. "Standard (Specular setup)" carries _Color/_MainTex/_EmissionColor). Fall back across
        // property names so a swatch/variant still yields its colour; if truly nothing is readable we
        // default to white (never magenta). Preserves the LPUP tan/brown swatches on the arcane tower.
        private static Material BuildRecoveredMaterial(Material src)
        {
            Color col = Color.white;
            if (src != null && src.HasProperty("_Color")) col = src.GetColor("_Color");
            else if (src != null && src.HasProperty("_BaseColor")) col = src.GetColor("_BaseColor");

            Texture tex = null;
            if (src != null && src.HasProperty("_MainTex")) tex = src.GetTexture("_MainTex");
            if (tex == null && src != null && src.HasProperty("_BaseMap")) tex = src.GetTexture("_BaseMap");

            Color emis = (src != null && src.HasProperty("_EmissionColor")) ? src.GetColor("_EmissionColor") : Color.black;

            var fresh = new Material(_lit) { name = ((src != null && src.name != null) ? src.name : "Recovered") + "_MagentaFix" };
            if (fresh.HasProperty("_BaseColor")) fresh.SetColor("_BaseColor", col);
            if (fresh.HasProperty("_Color")) fresh.SetColor("_Color", col);
            if (tex != null)
            {
                if (fresh.HasProperty("_BaseMap")) fresh.SetTexture("_BaseMap", tex);
                if (fresh.HasProperty("_MainTex")) fresh.SetTexture("_MainTex", tex);
            }
            if (emis != Color.black && fresh.HasProperty("_EmissionColor"))
            {
                fresh.SetColor("_EmissionColor", emis);
                fresh.EnableKeyword("_EMISSION");
            }
            if (fresh.HasProperty("_Surface")) fresh.SetFloat("_Surface", 0f);   // URP: 0 = Opaque
            if (fresh.HasProperty("_ZWrite"))  fresh.SetFloat("_ZWrite", 1f);
            fresh.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
            return fresh;
        }

        // Resolve a URP/Lit shader ROBUSTLY for a RUNTIME-spawned renderer (the emergency hero
        // pill). Shader.Find can return null in a stripped player build; when it does, BORROW the
        // shader from any material already living in the loaded scene(s) - those ARE guaranteed to be
        // included in the build because they are serialized in a built scene (the very reason
        // MagentaGuard exists). Falls back to nothing (returns null) only when truly none exist, so a
        // caller can decide to leave a renderer alone rather than force a magenta Standard material.
        public static Shader ResolveUrpLitShader()
        {
            if (_lit == null) _lit = Shader.Find("Universal Render Pipeline/Lit");
            if (_lit != null) return _lit;
            try
            {
                foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    if (r == null) continue;
                    var mats = r.sharedMaterials;
                    if (mats == null) continue;
                    foreach (var m in mats)
                    {
                        var sh = m != null ? m.shader : null;
                        if (sh != null && sh.name == "Universal Render Pipeline/Lit")
                        {
                            _lit = sh;
                            return _lit;
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                FlowTrace.Warn("MagentaGuard", $"ResolveUrpLitShader scene-borrow threw: {e.Message}");
            }
            return _lit; // may be null
        }

        // Build a FRESH URP/Lit material tinted <paramref name="baseColor"/> for a runtime placeholder
        // (the emergency hero pill). GUARANTEES a NON-magenta material: it never degrades to the
        // Standard shader (which renders magenta under URP). Returns null ONLY when no URP/Lit shader
        // can be resolved at all - the caller then leaves the renderer's existing material rather than
        // forcing magenta. Mirrors BuildRecoveredMaterial's opaque setup, but sources its colour from a
        // caller instead of a dead source material and resolves the shader through the robust path above.
        public static Material BuildUrpLitMaterial(Color baseColor)
        {
            var sh = ResolveUrpLitShader();
            if (sh == null) return null;
            var m = new Material(sh) { name = "EmergencyHero_URP" };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseColor);
            if (m.HasProperty("_Color")) m.SetColor("_Color", baseColor);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 0f); // URP: 0 = Opaque
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 1f);
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
            return m;
        }

        // A renderer whose mesh is one of Unity's BUILT-IN PRIMITIVES (the meshes
        // GameObject.CreatePrimitive produces). A magenta one is a stray placeholder, not art.
        private static bool IsPrimitivePlaceholder(Renderer r)
        {
            string mesh = MeshName(r);
            return mesh == "Capsule" || mesh == "Cube" || mesh == "Sphere"
                || mesh == "Cylinder" || mesh == "Plane" || mesh == "Quad";
        }

        private static string MeshName(Renderer r)
        {
            if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null) return smr.sharedMesh.name;
            var mf = r.GetComponent<MeshFilter>();
            return mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "<none>";
        }

        private static string HierarchyPath(Transform t)
        {
            var sb = new System.Text.StringBuilder(t.name);
            for (var p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }
    }
}
