// =============================================================================
// DungeonWorldPortalSpawner — WO-165 / DEF-188: hidden dungeon portals in the
// open world.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World
//
// THE FEATURE (lightweight, runtime, code-built, WebGL-safe):
//   The dungeons used to be reachable only from a fixed ring of entrances around
//   the village Heart (DungeonEntranceBootstrap). This relocates dungeon access
//   to a few DISCOVERABLE portals hidden out in the OuterWorld regions: the hero
//   roams a region, stumbles on a glowing arch, walks up to it, and it loads the
//   dungeon. Portals start UNDISCOVERED (dimmed) and "light up" the first time the
//   hero gets close — a small fog-of-war reveal so finding one feels like a find.
//
// RECONCILIATION (no parallel system — CLAUDE.md §9). EVERYTHING is reused:
//   * PLACEMENT  — AUTHORED WORLD-POSITION TABLE (owner ruling 2026-07-13:
//     "Portals are NOT in town — portals are wherever in the world we want",
//     refined live: "visible from castle but a little walk"). The old random
//     region-fan placement could exhaust its attempts and silently place
//     NOTHING (the MaxPlaceAttempts=24 give-up); the table is deterministic —
//     a portal ALWAYS appears at its authored spot, NavMesh-seated with the
//     townsfolk-injector ground band [-0.35..0.75] so it never lands on a
//     wall-top. Retune by editing AuthoredPortals below.
//   * PORTAL VISUAL + INTERACTION + LOAD — the EXISTING DungeonPortal component:
//     it already builds its own PortalVFXController glow, shows the proximity
//     [Tap/F] prompt + MobileInteractButton, and routes via
//     SceneManager.LoadScene("Dungeon_" + id). We just place it and Configure() it.
//   * DUNGEON DATA — Resources/Dungeons/*.asset (DungeonDef) when authored; else an
//     inline fallback covering only the dungeon scenes that ACTUALLY exist in the
//     build (Dungeon_HealersCottage, Dungeon_FolksGranary) — identical policy to
//     DungeonEntranceBootstrap so a fresh clone never routes to a missing scene.
//   * DISCOVERY — self-contained proximity reveal (mirrors NodeDiscoverySystem's
//     dim->fade-in approach) so portals are genuinely hidden-until-found WITHOUT
//     editing NodeDiscoverySystem (which only tracks Mine/Settlement/Camp nodes).
//
// ISOLATION:
//   * Self-bootstraps via RuntimeInitializeOnLoadMethod(AfterSceneLoad) — NO scene
//     edit, NO prefab hard-dependency, NO bake. It waits (re-scan) for the OuterWorld
//     scene + a baked NavMesh, then places once. No existing file is modified.
//   * Village -> Core only. Cross-module reads are null-conditional.
//   * Coexists with DungeonEntranceBootstrap (village ring) — that bootstrap is NOT
//     auto-added to any scene here, so by default the WORLD portals are the access
//     path. If both are present they simply offer two doors to the same dungeon.
//
// Canon: the village is Elarion (never Avalon). ASCII-only runtime strings.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.World
{
    /// <summary>
    /// Places a few hidden, discoverable dungeon portals at NavMesh-valid positions
    /// out in the OuterWorld regions and wires each to load its dungeon via the
    /// existing <see cref="DungeonPortal"/>. Self-bootstrapping; placement comes from
    /// the authored AuthoredPortals table (owner ruling 2026-07-13), seated on the
    /// NavMesh; reuses DungeonDef data and the DungeonPortal interaction/load path.
    /// </summary>
    public sealed class DungeonWorldPortalSpawner : MonoBehaviour
    {
        public static DungeonWorldPortalSpawner Instance { get; private set; }

        // ── Tunables (code-only; no SO authoring) ────────────────────────────
        [Tooltip("How far the hero must come for an undiscovered portal to reveal (metres). " +
                 "Larger than DungeonPortal's own ActivateRadius so the portal lights up " +
                 "as you approach, before the [F] prompt arms.")]
        public float DiscoverRadius = 26f;

        [Tooltip("Seconds the reveal fade-in takes when a portal is first discovered.")]
        public float FadeInSeconds = 0.8f;

        [Tooltip("How dim an undiscovered portal renders (0 hidden, 1 full). A faint " +
                 "silhouette reads as 'something is out there' without giving it away.")]
        [Range(0f, 1f)] public float UndiscoveredDim = 0.12f;

        [Tooltip("Seconds between placement attempts while waiting for OuterWorld + a baked " +
                 "NavMesh. Once placed, this stops re-trying.")]
        public float PlaceRetryInterval = 1.0f;

        [Tooltip("Height of the portal arch opening in metres (the keystone sits ~1.1 m above this). " +
                 "WO-869: raised 4 -> 6 so the arch reads as a landmark from across the field at " +
                 "2340x1080, not a thin frame you only notice up close. The hero is ~1.8 m, so 6 m " +
                 "is roughly three hero-heights of clear opening.")]
        public float PortalHeight = 6f;

        // ── Authored world placements (owner ruling 2026-07-13) ─────────────
        // "Portals are NOT in town — portals are wherever in the world we want",
        // refined live: "they should travel to get there ... make visible from
        // castle but a little walk". Geography: wall ring ~r=44, bridge outer
        // ends ~r=66-72, south gate ~z=-62 reads adjacent, the cave at z=-404 is
        // a trek. These sit ~95-100m past the wall ring (~140m from origin,
        // ~70-75m walk from the nearest bridge end) on open overworld ground with
        // a clear sightline back to the castle, on two compass sides so the two
        // dungeons pull the player different ways. Retune by editing this table
        // (owner has the Orient/dev tools). FacingYawDeg fronts the arch toward
        // the castle so the hero reads its face on approach.
        private struct AuthoredPortal
        {
            public string DungeonId;
            public Vector3 WorldPos;
            public float FacingYawDeg;
            public AuthoredPortal(string id, Vector3 pos, float yaw)
            { DungeonId = id; WorldPos = pos; FacingYawDeg = yaw; }
        }

        private static readonly AuthoredPortal[] AuthoredPortals =
        {
            // EAST of the walls: ~141m from origin (~97m past the r~44 ring,
            // ~72m walk from the east bridge end). Yaw 262 faces the castle.
            // REROUTE (WO): the east portal now leads to the composed KayKit starter-loop
            // dungeon 'dg_starter_loop' (GraphDungeonComposer) instead of Dungeon_HealersCottage.
            // DungeonPortal routes by this id; LoadDefs injects a matching def when the scene is
            // in the build. Overworld is origin-centered, so (140,0,20) stays reachable.
            new AuthoredPortal("dg_starter_loop", new Vector3(140f, 0f, 20f), 262f),
            // WO-1001 Phase 2A: Sunken Vault — NW of the walls (distinct compass pull from
            // starter east + cottage south). Same ~141m radius class as the east portal.
            // (x,z) = (-100, 100) ≈ 141m; yaw faces castle (SE).
            new AuthoredPortal("dg_sunken_vault", new Vector3(-100f, 0f, 100f), 142f),
            // WO-1001 Phase 2B: the Bonecrypt — SOUTH-WEST, the fourth compass pull (E starter,
            // NW vault, S cottage). Same ~141m radius class as the others: (-100,-100) ~= 141m.
            // Yaw 45 fronts the arch back toward the castle (atan2 of origin - pos).
            new AuthoredPortal("dg_bonecrypt", new Vector3(-100f, 0f, -100f), 45f),
            // WO-1001 Phase 2C: the Ember Deep — DUE NORTH, and deliberately the longest walk of
            // the set (~145m on a clear axis) because it is the hardest crawl: 6 floors, orc into
            // troll, and the richest deep-boss table. Yaw 180 faces the castle.
            new AuthoredPortal("dg_ember_deep", new Vector3(0f, 0f, 145f), 180f),
            // WEST row REMOVED (WO-776, 2026-07-30): Folk's Granary is a contentless stub
            // (no DungeonController, no layout JSON, zero lore, one canned encounter) — a
            // real door into a hollow room, and the owner's felt-test walked straight into
            // it. GATED until the content WO lands (layout + Inn-Keeper boss per
            // docs/DUNGEON_DESIGNS.md); the scene + FolksGranaryBuilder stay in-repo,
            // buildable dev-only. Re-add the row here to promote:
            //   new AuthoredPortal("FolksGranary", new Vector3(-140f, 0f, -20f), 82f),
            // SOUTH of the walls -- the third compass pull, and the row that puts the
            // fully-authored Dungeon_HealersCottage back in normal play. When the EAST row
            // was rerouted to 'dg_starter_loop' the HealersCottage def (Resources/Dungeons/
            // HealersCottage.asset, DungeonId "HealersCottage") lost its only table row, so
            // TryGetAuthored returned false every session and the loop logged
            // "has NO authored world position ... portal not placed" -- the richest dungeon
            // in the game (lore stones, mini-boss, chests, crafting, checkpoints) was
            // UNREACHABLE outside the dev overlay. Id is the bare "HealersCottage": it must
            // equal def.DungeonId for TryGetAuthored, and DungeonPortal.EnterDungeon then
            // resolves the scene via its "Dungeon_" + id fallback (no scene is named
            // "HealersCottage").
            //
            // Seat = the EAST row rigidly yaw-rotated +90 deg about the origin,
            // (x,z) -> (z,-x): (140,20) -> (20,-140). Same 141.4m radius, same ~96m walk
            // past the r~44 plinth face. Yaw = Atan2(-x,-z) = Atan2(-20,140) = -8.13 deg
            // -> 352 (the east row's 262 + 90), so the arch fronts the castle like its
            // siblings. GROUND: this lands in the WO-468 cave-road corridor, which
            // ExteriorTerrainBuilder holds at EXACTLY Y=0 for |x| <= 20 over z in
            // [-700,-76] (CorridorWeight / CavePathFlattenHalf=20) -- the only one of the
            // three seats whose height is pinned inside the [-0.35..0.75] ground band
            // rather than relying on the SeatOnGround search (the other two sit past
            // ReliefBlendRadius=140, where +-1.8m of relief applies). Tree/rock scatter is
            // rejected within 37m of that road line, so the seat is flat and prop-free,
            // 10m clear of the painted road and ~78m outside the r=44..62 moat band.
            // If a headless run reports navmesh-seated=False, retune to (16f, 0f, -140f).
            new AuthoredPortal("HealersCottage", new Vector3(20f, 0f, -140f), 352f),
        };

        // Townsfolk-injector ground band (CastleTownsfolkInjector.GroundMinY/MaxY):
        // a NavMesh sample outside [-0.35..0.75] is elevated mesh (wall-top /
        // bridge deck) — never a valid portal seat.
        private const float GroundMinY = -0.35f;
        private const float GroundMaxY = 0.75f;

        // PlayerPrefs key prefix for "this portal has been discovered" (position-keyed,
        // same convention as NodeDiscoverySystem so discovery survives a reload).
        private const string PrefKeyPrefix = "dotr-dungeon-portal-discovered-";

        // Cap on placement retries. Each retry runs NavMesh.CalculateTriangulation()
        // (HEAVY on the big OuterWorld mesh). If no portal can ever seat (every region
        // match fails), the un-capped retry re-ran that triangulation forever on the
        // main thread = a hard hang on OuterWorld load. After MaxPlaceAttempts failures
        // we give up (portals simply don't appear) so the game stops grinding.
        private const int MaxPlaceAttempts = 24;

        private bool _placed;
        private int _placeAttempts;
        private float _retryTimer;
        private Transform _root;
        private Transform _hero;

        // One placed portal + its discovery bookkeeping.
        private sealed class Portal
        {
            public Transform Root;
            public string Key;
            public bool Discovered;
            public Renderer[] Renderers;
            public Color[] BaseColors;
            public float FadeT;       // 0..1 reveal progress (1 = done)
            public VFXHandle GateVfx; // looping magic-circle rune ring at the arch base (null until attached)
            // WO-869 threshold aura. Held so the WO-753 one-owner teardown can Stop() it -
            // an orphaned looping portal aura with no portal under it is the exact bug that
            // rule exists to prevent. Stays null until the owner tags the key (see below).
            public VFXHandle ThresholdVfx;
        }

        private readonly List<Portal> _portals = new List<Portal>();

        // =====================================================================
        // Self-bootstrap (no scene edit). Runs after every scene load; idempotent.
        // =====================================================================
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // Dungeon entry is ON by default (FeatureFlags.DungeonPortals, defaultOn: true) — the
            // portals spawn unless explicitly disabled. (It was gated OFF around 2026-07-01 while the
            // first 2 dungeons were placeholder-primitive "pill" scenes and the KayKit dungeon art
            // wasn't wired; the default has since been flipped back ON.) Disable for testing:
            // PlayerPrefs "ff.dungeonportals" = 0.
            if (!DeNelle.Core.FeatureFlags.DungeonPortals) return;
            if (Instance != null) return;
            var go = new GameObject("DungeonWorldPortalSpawner");
            go.AddComponent<DungeonWorldPortalSpawner>();
            Object.DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            // Destroy(this), not the host — DDOL singleton pattern (CLAUDE.md memory).
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!_placed)
            {
                _retryTimer -= Time.deltaTime;
                if (_retryTimer <= 0f)
                {
                    _retryTimer = Mathf.Max(0.25f, PlaceRetryInterval);
                    TryPlace();
                }
                return;
            }

            EnsureHero();
            TickDiscovery();
        }

        // =====================================================================
        // Placement — one portal per authored table row (owner ruling 2026-07-13).
        // Waits (retry) only for a baked NavMesh; once the mesh exists placement is
        // DETERMINISTIC: every authored portal is built (ground-band-seated when the
        // sample lands, raw authored point otherwise) — the old random-region path
        // could exhaust its attempts and silently place NOTHING.
        // =====================================================================
        private void TryPlace()
        {
            // Owner session 2026-07-13 (proving line: "no baked NavMesh after 24 attempts —
            // stopping retries ... (no portals this session)"): the DDOL bootstrap starts
            // this retry clock on the TITLE screen, where no navmesh exists — all capped
            // attempts burned in the menus and placement retired before the overworld ever
            // loaded. Attempts only count IN the overworld; menus don't spend the budget.
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (!DeNelle.Core.HubScenes.IsOverworld(active) && !DeNelle.Core.HubScenes.IsHub(active))
                return;   // menu/battle scene — wait, no attempt spent

            var defs = LoadDefs();
            if (defs.Count == 0) return; // nothing to place (no built dungeon scenes)

            // Gate on a baked NavMesh existing — the ground-band seat needs it. The
            // triangulation is empty until the OuterWorld NavMesh bake is present at
            // runtime. Retries stay CAPPED: CalculateTriangulation is HEAVY and an
            // uncapped retry loop hard-hung the OuterWorld load pre-cap.
            var tri = NavMesh.CalculateTriangulation();
            if (tri.vertices == null || tri.vertices.Length == 0)
            {
                _placeAttempts++;
                if (_placeAttempts >= MaxPlaceAttempts)
                {
                    _placed = true;
                    Debug.LogWarning($"[DungeonWorldPortals] no baked NavMesh after {_placeAttempts} attempts — " +
                        "stopping retries to avoid a CalculateTriangulation freeze (no portals this session).");
                }
                return;
            }

            _root = new GameObject("[DungeonWorldPortals]").transform;
            DontDestroyOnLoad(_root.gameObject);

            int placed = 0;
            for (int i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                if (def == null || string.IsNullOrEmpty(def.DungeonId)) continue;

                if (!TryGetAuthored(def.DungeonId, out AuthoredPortal entry))
                {
                    // Never fall back to random ground — an unauthored dungeon is a
                    // loud authoring gap, not a silent roll of the dice.
                    Debug.LogWarning($"[DungeonWorldPortals] '{def.DungeonId}' has NO authored world position " +
                        "in AuthoredPortals — portal not placed; add a table row.");
                    continue;
                }

                Vector3 pos = SeatOnGround(entry.WorldPos, out bool seated);
                BuildPortal(def, pos, entry.FacingYawDeg);
                placed++;
                FlowTrace.Step("DungeonPortal",
                    $"placed '{def.DungeonId}' at ({pos.x:F1}, {pos.y:F1}, {pos.z:F1}) " +
                    $"(authored {entry.WorldPos}, navmesh-seated={seated}, facingYaw={entry.FacingYawDeg:F0}).");
            }

            // Authored placement is one-pass deterministic — never re-roll.
            _placed = true;
            Debug.Log($"[DungeonWorldPortals] Placed {placed}/{defs.Count} authored dungeon portal(s) in the world.");
        }

        private static bool TryGetAuthored(string dungeonId, out AuthoredPortal entry)
        {
            for (int i = 0; i < AuthoredPortals.Length; i++)
            {
                if (string.Equals(AuthoredPortals[i].DungeonId, dungeonId, System.StringComparison.OrdinalIgnoreCase))
                {
                    entry = AuthoredPortals[i];
                    return true;
                }
            }
            entry = default;
            return false;
        }

        // Ground-band NavMesh seat (the townsfolk-injector idiom): accept a sample only
        // inside [-0.35..0.75] so a portal never seats on a wall-top / bridge deck;
        // widen the search once, then fall back to the raw authored point at y=0 —
        // a portal ALWAYS appears where authored.
        private static Vector3 SeatOnGround(Vector3 authored, out bool seated)
        {
            float[] radii = { 8f, 20f };
            for (int i = 0; i < radii.Length; i++)
            {
                if (NavMesh.SamplePosition(authored, out NavMeshHit hit, radii[i], NavMesh.AllAreas)
                    && hit.position.y >= GroundMinY && hit.position.y <= GroundMaxY)
                {
                    seated = true;
                    return hit.position;
                }
            }
            FlowTrace.Warn("DungeonPortal",
                $"SeatOnGround: no ground-band [-0.35..0.75] NavMesh hit within 20m of authored {authored} — " +
                "using the raw authored point (verify walkability in the next fleet run).");
            seated = false;
            return new Vector3(authored.x, 0f, authored.z);
        }

        // =====================================================================
        // Build one portal: a code-built placeholder arch + the EXISTING DungeonPortal
        // driver (proximity prompt + SceneManager.LoadScene). DungeonPortal adds its own
        // PortalVFXController glow in Start(), so the arch reads as a portal with no asset.
        // =====================================================================
        private void BuildPortal(DungeonDef def, Vector3 pos, float facingYawDeg)
        {
            using var _ = FlowTrace.Enter("DungeonPortals", $"BuildPortal id='{def?.DungeonId}'");
            if (def == null)
            {
                FlowTrace.Fail("DungeonPortals", "BuildPortal: null def — skipping (no NRE-abort of the placement loop).");
                return;
            }

            var root = new GameObject($"DungeonWorldPortal_{def.DungeonId}");
            root.transform.SetParent(_root, false);
            root.transform.position = pos;
            // Authored facing (fronts read toward the castle — owner-retunable in the table).
            root.transform.rotation = Quaternion.Euler(0f, facingYawDeg, 0f);

            var renderers = BuildArch(root.transform, PortalHeight, def.AccentColor);

            // V (TGVRU-V): a portal with no live renderer is INVISIBLE — the hero can never find
            // it (the whole feature is "stumble on a glowing arch"). Count enabled renderers with
            // a material; Warn-loud if none so an invisible portal self-reports instead of being a
            // silent "feature does nothing". Non-fatal — the trigger still loads the dungeon if hit.
            int liveRenderers = 0;
            if (renderers != null)
                foreach (var r in renderers)
                    if (r != null && r.enabled && r.sharedMaterial != null) liveRenderers++;
            if (liveRenderers == 0)
                FlowTrace.Warn("DungeonPortals",
                    $"BuildPortal: '{def.DungeonId}' arch has 0 live renderer(s) at {pos} — portal will be INVISIBLE (null shader/material?).");

            // Trigger + kinematic RB so the transform-moved hero collider fires OnTriggerEnter
            // on the portal side (same pattern as DungeonEntranceBootstrap / WO-08 gates).
            var sphere = root.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 2.5f;
            sphere.center = new Vector3(0f, 1f, 0f);
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // The EXISTING interaction + load driver. Configure() sets which dungeon it routes
            // to; its EnterDungeon() loads "Dungeon_" + dungeonId via SceneManager.LoadScene.
            var portal = root.AddComponent<DungeonPortal>();
            portal.Configure(def.DungeonId, def.ResolveName());

            // Discovery bookkeeping — start dimmed/hidden until the hero finds it.
            var colors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) colors[i] = SafeColor(renderers[i]);

            var entry = new Portal
            {
                Root = root.transform,
                Key = MakeKey(pos),
                Renderers = renderers,
                BaseColors = colors,
            };
            entry.Discovered = PlayerPrefs.GetInt(entry.Key, 0) == 1;
            entry.FadeT = entry.Discovered ? 1f : 0f;
            ApplyDim(entry, entry.Discovered ? 1f : UndiscoveredDim);
            _portals.Add(entry);

            // Owner felt-test 2026-07-15 ("the dungeon portal arch looks plain -- make it
            // magical"): a portal already found in a prior session (persisted Discovered)
            // gets its arcane rune-ring immediately so it reads as an active gateway on
            // load; a fresh (undiscovered) portal blooms it on when the hero finds it
            // (Discover()), preserving the fog-of-war reveal.
            if (entry.Discovered) { AttachGateVfx(entry); AttachThresholdAura(entry); }

            // WO-869 belt-and-braces: recover this portal's materials RIGHT NOW rather than
            // waiting for the guard's next deferred sweep. SweepGameObject is the per-object
            // seam (hideStrayPrimitives:false, so it repaints and never hides), and the arch is
            // already registered as protected art above.
            DeNelle.Core.MagentaGuard.SweepGameObject(root, "DungeonWorldPortalSpawner.BuildPortal");

            FlowTrace.Step("DungeonPortals",
                $"BuildPortal: '{def.ResolveName()}' portal committed at {pos} " +
                $"(discovered={entry.Discovered}, liveRenderers={liveRenderers}).");
        }

        // =====================================================================
        // THE THRESHOLD ARCH (WO-869 REBUILD, owner 2026-08-04: "we need that portal to
        // look way better ... the point is the whole thing needs redone").
        // ---------------------------------------------------------------------
        // WHAT WAS WRONG (owner Seeker capture docs/ui-review/2026-08-04-seeker/08-portal-magenta.png):
        // the old arch was TWO 0.3 m sticks and a flat bar - a rectangle drawn in the air,
        // 1.8 m wide and 4 m tall, with ZERO depth. Nothing about it said "this leads
        // somewhere": no ground it stood on, no threshold to cross, no near/far face, and
        // at 2340x1080 across an open field it read as a thin frame, not a landmark.
        //
        // THE REBUILD, and why each part earns its place:
        //   PLINTH (2 stepped slabs) - the portal is PLANTED. A landmark you navigate toward
        //     needs a footprint on the ground; the upper step is also the floor you cross,
        //     which is what turns a frame into a THRESHOLD.
        //   TWO PILLAR RINGS (front + back, 1.8 m apart in Z) - this is the single most
        //     important change. Depth is what makes an opening read as a way INTO somewhere
        //     rather than a picture frame: you see the near pillars, the far pillars behind
        //     them, and a lit surface suspended between. One ring can never do that.
        //   STEPPED PILLARS (base drum / shaft / capital) - a varying silhouette survives
        //     distance. A constant-width stick reads as a line; a stepped column still reads
        //     as architecture when it is 40 m away and 30 px tall.
        //   LINTEL PER RING + CORNICE + KEYSTONE - gives the top a PEAK instead of a flat bar,
        //     so the shape is identifiable against the skyline from across the field.
        // The active threshold SURFACE and the aura are owned by PortalVFXController, which
        // now fits itself to this arch's real bounds (so this geometry can be retuned freely).
        //
        // Still 100% code-built primitives: NO .unity scene edit, NO new art asset, NO bake
        // (CLAUDE.md section 3). Cost is ~18 cube renderers sharing ONE material, so they
        // SRP-batch - for the single landmark that is the entrance to a whole pillar.
        // =====================================================================

        // Half-width of the walk-through opening. 1.8 m half = a 3.6 m doorway: the hero is
        // ~1.8 m, so it reads as something you walk INTO rather than squeeze past.
        private const float ArchHalfWidth = 1.8f;
        // Half-depth: the two pillar rings sit at +/- this, i.e. a 1.8 m deep threshold.
        private const float ArchHalfDepth = 0.9f;
        // Pillar cross-section.
        private const float PillarThickness = 0.45f;

        /// <summary>
        /// Build the threshold arch (see the block comment above). Returns every renderer so the
        /// discovery layer can dim/restore them. Colliders stripped - the sphere trigger on the
        /// root is the only collider, so the player can walk through the opening.
        /// </summary>
        private Renderer[] BuildArch(Transform parent, float h, Color accent)
        {
            // MAGENTA ROOT CAUSE (WO-869): this used a RAW Shader.Find, which returns NULL in a
            // stripped player build - and when it did, `mat` stayed null, MakeBox's
            // `if (mat != null)` skipped the assignment, and the cube kept Unity's DEFAULT
            // material, which under URP renders MAGENTA. That is the pink arch in the Seeker
            // capture, and the old unlit-sprite-shader fallback behind it only ever made it
            // worse (an unlit sprite shader on a 3D arch). MagentaGuard.ResolveUrpLitShader is
            // the ROBUST resolver written for exactly this: it falls back to BORROWING a URP/Lit
            // shader off a material already live in the loaded scene, which is guaranteed to be
            // in the build because it is serialized in a built scene.
            Shader lit = DeNelle.Core.MagentaGuard.ResolveUrpLitShader();
            FlowTrace.Step("DungeonPortals",
                $"BuildArch: URP/Lit resolved={(lit != null)} via MagentaGuard.ResolveUrpLitShader " +
                $"(rawShaderFind={(Shader.Find("Universal Render Pipeline/Lit") != null)}) - " +
                "a false rawShaderFind with a true resolve IS the magenta cause, captured.");
            if (lit == null)
                FlowTrace.Fail("DungeonPortals",
                    "BuildArch: NO URP/Lit shader resolvable - the arch will render with Unity's default " +
                    "material, which is MAGENTA under URP. Add 'Universal Render Pipeline/Lit' to " +
                    "GraphicsSettings AlwaysIncludedShaders.");

            Material mat = lit != null ? new Material(lit) { name = "PortalArch_URP" } : null;
            if (mat != null)
            {
                mat.EnableKeyword("_EMISSION");
                // DEF-94: arch reads as an arcane-violet portal (per-dungeon accent kept
                // as a subtle tint), NOT a flat pastel AccentColor frame. WO-272's glow
                // layers on top of this base.
                Color archBase = PortalVFXController.ArchBaseColor(accent);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", archBase);
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", PortalVFXController.ArchEmissionColor(accent));
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f);   // URP: 0 = Opaque
                if (mat.HasProperty("_ZWrite"))  mat.SetFloat("_ZWrite", 1f);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
            }

            float hw = ArchHalfWidth;
            float dz = ArchHalfDepth;
            float pt = PillarThickness;
            float pillarX = hw + pt * 0.5f;             // pillar centre, just outside the opening
            float outerW  = (pillarX + pt * 0.5f) * 2f; // full frame width including pillars
            float outerD  = dz * 2f + pt;               // full frame depth including both rings

            var list = new List<Renderer>(18);

            // 1) PLINTH - two stepped slabs. Plants the portal and gives the threshold a floor.
            list.Add(MakeBox(parent, "Arch_PlinthLower", new Vector3(0f, 0.175f, 0f),
                             new Vector3(outerW + 1.2f, 0.35f, outerD + 1.0f), mat));
            list.Add(MakeBox(parent, "Arch_PlinthUpper", new Vector3(0f, 0.50f, 0f),
                             new Vector3(outerW + 0.4f, 0.30f, outerD + 0.3f), mat));

            // 2) FOUR PILLARS in two depth rings - the read of DEPTH lives here.
            float drumH    = 0.50f;
            float capH     = 0.40f;
            float drumTop  = 0.65f + drumH;                       // plinth top (0.65) + drum
            float capBase  = h - capH;
            float shaftH   = Mathf.Max(0.6f, capBase - drumTop);   // never inverts on a small h
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    float x = pillarX * sx;
                    float z = dz * sz;
                    list.Add(MakeBox(parent, "Arch_PillarBase", new Vector3(x, 0.65f + drumH * 0.5f, z),
                                     new Vector3(pt * 1.6f, drumH, pt * 1.6f), mat));
                    list.Add(MakeBox(parent, "Arch_PillarShaft", new Vector3(x, drumTop + shaftH * 0.5f, z),
                                     new Vector3(pt, shaftH, pt), mat));
                    list.Add(MakeBox(parent, "Arch_PillarCapital", new Vector3(x, capBase + capH * 0.5f, z),
                                     new Vector3(pt * 1.5f, capH, pt * 1.5f), mat));
                }
            }

            // 3) A LINTEL ACROSS EACH RING - closes each ring into its own arch, so the
            //    silhouette is two nested openings, not one flat frame.
            for (int sz = -1; sz <= 1; sz += 2)
                list.Add(MakeBox(parent, "Arch_Lintel", new Vector3(0f, h + 0.225f, dz * sz),
                                 new Vector3(outerW + 0.3f, 0.45f, pt * 1.3f), mat));

            // 4) CORNICE + KEYSTONE - ties the two rings together and gives the top a PEAK,
            //    which is what makes the shape identifiable against the sky at distance.
            list.Add(MakeBox(parent, "Arch_Cornice", new Vector3(0f, h + 0.60f, 0f),
                             new Vector3(outerW + 0.8f, 0.30f, outerD + 0.4f), mat));
            list.Add(MakeBox(parent, "Arch_Keystone", new Vector3(0f, h + 1.10f, 0f),
                             new Vector3(0.90f, 0.90f, pt * 1.6f), mat));

            // WO-869: tell MagentaGuard these primitives are DELIBERATE ART. Without this the
            // scene sweep classifies every Cube as a stray placeholder pill and DISABLES it -
            // which would turn "magenta portal" into "no portal at all", a strictly worse bug
            // (the feature is literally "stumble on a glowing arch"). Registered here, after the
            // renderers exist, so the guard's very next sweep recovers rather than hides them.
            DeNelle.Core.MagentaGuard.ProtectPrimitiveArt(parent != null ? parent.gameObject : null,
                                                          "DungeonWorldPortalSpawner.BuildArch");

            return list.ToArray();
        }

        private static Renderer MakeBox(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col); // arch shouldn't block movement; the trigger is the only collider
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (r != null)
            {
                if (mat != null) r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }
            return r;
        }

        // =====================================================================
        // Discovery — dim until the hero approaches, then fade the arch up + persist.
        // Mirrors NodeDiscoverySystem; kept self-contained so no existing file is edited.
        // =====================================================================
        private void TickDiscovery()
        {
            float revealSqr = DiscoverRadius * DiscoverRadius;
            bool heroValid = _hero != null;

            for (int i = _portals.Count - 1; i >= 0; i--)
            {
                var p = _portals[i];
                if (p == null || p.Root == null)
                {
                    // Release the pooled rune-ring loop back to the shared VFX pool so a
                    // torn-down portal never leaves an orphaned looping effect behind.
                    p?.GateVfx?.Stop(true);
                    p?.ThresholdVfx?.Stop(true);   // WO-753: no aura outlives its portal
                    _portals.RemoveAt(i);
                    continue;
                }

                if (!p.Discovered)
                {
                    if (heroValid &&
                        (p.Root.position - _hero.position).sqrMagnitude <= revealSqr)
                        Discover(p);
                    continue;
                }

                if (p.FadeT < 1f)
                {
                    float step = FadeInSeconds > 0f ? Time.deltaTime / FadeInSeconds : 1f;
                    p.FadeT = Mathf.Clamp01(p.FadeT + step);
                    ApplyDim(p, Mathf.Lerp(UndiscoveredDim, 1f, p.FadeT));
                }
            }
        }

        private void Discover(Portal p)
        {
            if (p == null || p.Discovered) return;
            p.Discovered = true;
            p.FadeT = 0f;
            PlayerPrefs.SetInt(p.Key, 1);
            PlayerPrefs.Save();
            // Bloom the arcane rune ring on as the hero finds it -- the discovery reads
            // as the gateway "activating" (owner felt-test: make the entrance magical).
            AttachGateVfx(p);
            AttachThresholdAura(p);
            Debug.Log($"[DungeonWorldPortals] Hero discovered a hidden dungeon portal (key {p.Key}).");
        }

        // =====================================================================
        // Magical portal VFX (owner felt-test 2026-07-15 "the dungeon portal arch looks
        // plain -- can creative make it magical?"; retuned 2026-07-24 "the harsh magenta
        // swirl + light beams read too hot -- give me a SOFT aura spilling out of the portal").
        // Uses the shared VFXManager pool (catalog key "PP_GroundFog") as a soft ground mist
        // that pools at the arch base -- so the entrance reads as a gentle magical emanation,
        // on top of the arch's own PortalVFXController soft-violet halo. ONE pooled looping
        // system per portal (mobile-cheap, returned to the pool on teardown). COLORBLIND-SAFE
        // (owner red/green): the read is soft LUMINANCE + slow drift, the faint violet is only a hint.
        // =====================================================================
        // SOFT aura spilling out of the portal (owner felt-test 2026-07-24: the harsh
        // magenta rune-ring + light pillars read too hot -- swap the layer to a low, soft
        // ground mist that POOLS around the arch base). Luminance-led + near-neutral: a
        // pale cool white with only the faintest violet hint, at reduced alpha, so it reads
        // as a gentle glow spilling out rather than a bright saturated ring. COLORBLIND-SAFE
        // (owner red/green): the read is soft LUMINANCE + slow motion, hue is barely a whisper.
        private static readonly Color GateTint = new Color(0.82f, 0.80f, 0.92f, 0.45f); // pale cool white, faint violet hint, low alpha
        private const string GateVfxKey  = "PP_GroundFog";   // soft ground mist emanation (was harsh "Dungeon_Portal_Gate" rune-ring)
        private const float  GateVfxScale = 3.4f;  // wide + low: mist pools around the ~1.8 m arch base, not a framed ring

        private void AttachGateVfx(Portal p)
        {
            if (p == null || p.Root == null) return;
            if (p.GateVfx != null) return;   // already holding the loop (idempotent)

            // Seat the mist just above the arch base so it pools on the floor threshold
            // without z-fighting the ground. Parent to the arch so it tracks + tears down with it.
            Vector3 mistPos = p.Root.position + Vector3.up * 0.05f;
            p.GateVfx = VFXManager.PlayKey(
                GateVfxKey, mistPos, Quaternion.identity, p.Root,
                GateTint, GateVfxScale);

            FlowTrace.Step("Portal",
                $"AttachGateVfx: '{GateVfxKey}' soft ground mist " +
                (p.GateVfx != null
                    ? $"spawned at ({mistPos.x:F1}, {mistPos.y:F1}, {mistPos.z:F1}) (loop held) -- soft aura now spills out of the portal base."
                    : "no-op (VFXManager/catalog not ready or key unauthored -- regen the Hovl catalog) -- procedural glow remains."));
        }

        // =====================================================================
        // WO-869 THRESHOLD AURA - routing WIRED, prefab pick HELD for the owner.
        // ---------------------------------------------------------------------
        // The WO asks for an aura from the shipped Mirza Beig Ultimate VFX pack, and the pack
        // does contain exactly five portal loops (verified on disk, and the inventory doc
        // docs/asset-inventory/04_vfx_spells_audio.md independently counts "portal x5"):
        //     pf_vfx-ult_demo_psys_loop_ghostPortal
        //     pf_vfx-ult_demo_psys_loop_ghostPortal2
        //     pf_vfx-ult_demo_psys_loop_portalBlue
        //     pf_vfx-ult_demo_psys_loop_portalBlueTutorial
        //     pf_vfx-ult_demo_psys_loop_portalOrange
        //
        // WHICH ONE IS NOT MINE TO DECIDE. The standing owner directive is: the OWNER tags the
        // VFX key in the Caster, the CLI maps key -> hook VERBATIM, and an UNTAGGED hook is HELD,
        // never filled by whatever looks right (memory: vfx-map-owner-tags-no-creative-pick).
        // None of the five is tagged in Assets/Editor/VfxManualPicks.json today, and the owner's
        // 2026-08-04 relaxation covered the "projectile" category for TOWERS - not the portal.
        //
        // So the SEAM is fully built and the pick is the only thing missing: the moment the owner
        // tags a row with this key, the aura lights up with no further code change. Until then
        // PlayKey is a clean no-op and the rebuilt geometry + PP_GroundFog mist carry the read.
        private const string ThresholdAuraKey = "Portal_Threshold_Aura";
        // Sized to the 2*ArchHalfWidth opening so a tagged prefab fills the threshold rather than
        // being lost inside it - retune once a real prefab is behind the key.
        private const float ThresholdAuraScale = ArchHalfWidth * 2f;

        private void AttachThresholdAura(Portal p)
        {
            if (p == null || p.Root == null) return;
            if (p.ThresholdVfx != null) return;   // already holding the loop (idempotent)

            // Centre of the opening, between the two pillar rings - the surface you cross.
            Vector3 thresholdPos = p.Root.position + p.Root.up * (PortalHeight * 0.5f);
            p.ThresholdVfx = VFXManager.PlayKey(
                ThresholdAuraKey, thresholdPos, p.Root.rotation, p.Root,
                null, ThresholdAuraScale);

            if (p.ThresholdVfx != null)
            {
                FlowTrace.Step("Portal",
                    $"AttachThresholdAura: '{ThresholdAuraKey}' aura spawned at the threshold centre " +
                    $"({thresholdPos.x:F1}, {thresholdPos.y:F1}, {thresholdPos.z:F1}) scale={ThresholdAuraScale:0.0} (loop held).");
                return;
            }

            // Section 12: say WHY it is absent, and say it once, with the shortlist - so this reads as a
            // deliberate HOLD in a capture rather than a broken effect someone re-debugs later.
            FlowTrace.Once("Portal", "threshold-aura-untagged",
                $"AttachThresholdAura: key '{ThresholdAuraKey}' is NOT catalogued - the aura is HELD, " +
                "awaiting an owner tag in the VfxCaster (owner tags, CLI maps verbatim). Shortlist from the " +
                "owned Mirza Beig Ultimate VFX pack: pf_vfx-ult_demo_psys_loop_ghostPortal, ghostPortal2, " +
                "portalBlue, portalBlueTutorial, portalOrange. Routing is wired - tagging the key is the only " +
                "step left. The rebuilt arch + PP_GroundFog mist carry the read meanwhile.");
        }

        private static void ApplyDim(Portal p, float brightness)
        {
            if (p == null || p.Renderers == null) return;
            brightness = Mathf.Clamp01(brightness);
            bool visible = brightness > 0.001f;
            for (int i = 0; i < p.Renderers.Length; i++)
            {
                var r = p.Renderers[i];
                if (r == null) continue;
                Color baseCol = (p.BaseColors != null && i < p.BaseColors.Length) ? p.BaseColors[i] : Color.white;
                Color dimmed = new Color(baseCol.r * brightness, baseCol.g * brightness,
                                         baseCol.b * brightness, baseCol.a);
                if (r.enabled != visible) r.enabled = visible;
                SetColor(r, dimmed);
            }
        }

        private static Color SafeColor(Renderer r)
        {
            if (r == null) return Color.white;
            var m = r.sharedMaterial;
            if (m == null) return Color.white;
            if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
            if (m.HasProperty("_Color")) return m.color;
            return Color.white;
        }

        private static void SetColor(Renderer r, Color c)
        {
            if (r == null) return;
            var m = r.material; // instanced — don't stomp the shared arch material across portals
            if (m == null) return;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            else if (m.HasProperty("_Color")) m.color = c;
        }

        // =====================================================================
        // Data — DungeonDef from Resources, else an inline fallback covering ONLY the
        // dungeon scenes that exist in the build (identical policy to DungeonEntranceBootstrap).
        // =====================================================================
        private static List<DungeonDef> LoadDefs()
        {
            var all = new List<DungeonDef>(Resources.LoadAll<DungeonDef>("Dungeons"));
            var built = new List<DungeonDef>(all.Count);
            foreach (var d in all)
            {
                if (d == null || string.IsNullOrEmpty(d.SceneName)) continue;
                // WO-776 (2026-07-30): Folk's Granary is DELIBERATELY GATED — a contentless
                // stub the owner walked into. Skipping the def here (Step, not Warn: this is
                // by-design, not an anomaly) also prevents the per-boot "NO authored world
                // position" warning its now-removed portal row would otherwise cause.
                if (string.Equals(d.DungeonId, "FolksGranary", System.StringComparison.OrdinalIgnoreCase))
                {
                    FlowTrace.Step("DungeonPortal",
                        "def 'FolksGranary' skipped — stub dungeon gated until its content WO lands (WO-776).");
                    continue;
                }
                // Only place portals to scenes that are actually loadable (in Build Settings).
                if (d.SceneExists && UnityEngine.Application.CanStreamedLevelBeLoaded(d.SceneName))
                    built.Add(d);
            }

            // REROUTE (WO): guarantee the composed starter-loop dungeon is represented whenever
            // its scene is in the build, without needing a Resources/Dungeons .asset. Its
            // DungeonId IS the full scene name, so DungeonPortal loads it verbatim (see
            // DungeonPortal.EnterDungeon). The east AuthoredPortal row keys placement to it.
            const string StarterLoop = "dg_starter_loop";
            if (UnityEngine.Application.CanStreamedLevelBeLoaded(StarterLoop)
                && !built.Exists(x => x != null && string.Equals(x.DungeonId, StarterLoop, System.StringComparison.OrdinalIgnoreCase)))
            {
                built.Add(MakeDef(StarterLoop, StarterLoop, "Starter Loop", new Color(0.62f, 0.72f, 1f)));
            }

            // WO-1001 Phase 2A: composed multi-level Sunken Vault (same def inject pattern).
            const string SunkenVault = "dg_sunken_vault";
            if (UnityEngine.Application.CanStreamedLevelBeLoaded(SunkenVault)
                && !built.Exists(x => x != null && string.Equals(x.DungeonId, SunkenVault, System.StringComparison.OrdinalIgnoreCase)))
            {
                built.Add(MakeDef(SunkenVault, SunkenVault, "Sunken Vault of Elarion", new Color(0.45f, 0.62f, 0.85f)));
            }

            // WO-1001 Phase 2B: the Bonecrypt (5 floors, key-gated deep floor, Necromancer).
            const string Bonecrypt = "dg_bonecrypt";
            if (UnityEngine.Application.CanStreamedLevelBeLoaded(Bonecrypt)
                && !built.Exists(x => x != null && string.Equals(x.DungeonId, Bonecrypt, System.StringComparison.OrdinalIgnoreCase)))
            {
                built.Add(MakeDef(Bonecrypt, Bonecrypt, "The Bonecrypt", new Color(0.38f, 0.66f, 0.44f)));
            }

            // WO-1001 Phase 2C: the Ember Deep (6 floors, orc into troll, Ogre warlord).
            const string EmberDeep = "dg_ember_deep";
            if (UnityEngine.Application.CanStreamedLevelBeLoaded(EmberDeep)
                && !built.Exists(x => x != null && string.Equals(x.DungeonId, EmberDeep, System.StringComparison.OrdinalIgnoreCase)))
            {
                built.Add(MakeDef(EmberDeep, EmberDeep, "The Ember Deep", new Color(0.92f, 0.55f, 0.28f)));
            }

            if (built.Count > 0) return built;

            // Inline fallback — the two dungeon scenes that exist in the build today.
            var fb = new List<DungeonDef>(2);
            if (UnityEngine.Application.CanStreamedLevelBeLoaded("Dungeon_HealersCottage"))
                fb.Add(MakeDef("HealersCottage", "Dungeon_HealersCottage", "Healer's Cottage", new Color(1f, 0.82f, 0.48f)));
            // FolksGranary fallback REMOVED (WO-776) — stub dungeon gated; see the def skip above.
            return fb;
        }

        private static DungeonDef MakeDef(string id, string scene, string name, Color accent)
        {
            var d = ScriptableObject.CreateInstance<DungeonDef>();
            d.DungeonId = id; d.SceneName = scene; d.DisplayName = name;
            d.AccentColor = accent; d.SceneExists = true;
            return d;
        }

        // =====================================================================
        // Helpers.
        // =====================================================================
        private void EnsureHero()
        {
            if (_hero != null) return;
            var p = SafeFindWithTag("Player");
            _hero = p != null ? p.transform : null;
        }

        private static GameObject SafeFindWithTag(string tag)
        {
            try { return GameObject.FindWithTag(tag); }
            catch (UnityEngine.UnityException) { return null; }
        }

        private static string MakeKey(Vector3 pos) =>
            PrefKeyPrefix +
            Mathf.RoundToInt(pos.x) + "_" +
            Mathf.RoundToInt(pos.y) + "_" +
            Mathf.RoundToInt(pos.z);
    }
}
