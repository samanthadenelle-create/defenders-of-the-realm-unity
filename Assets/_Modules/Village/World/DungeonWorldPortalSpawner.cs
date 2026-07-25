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

        [Tooltip("Visual height of the placeholder portal arch (metres).")]
        public float PortalHeight = 4f;

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
            // WEST of the walls, mirrored — the opposite compass pull. Yaw 82
            // faces the castle.
            new AuthoredPortal("FolksGranary",   new Vector3(-140f, 0f, -20f), 82f),
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
            if (entry.Discovered) AttachGateVfx(entry);

            FlowTrace.Step("DungeonPortals",
                $"BuildPortal: '{def.ResolveName()}' portal committed at {pos} " +
                $"(discovered={entry.Discovered}, liveRenderers={liveRenderers}).");
        }

        // Two posts + a lintel, tinted with the dungeon accent. Returns the renderers so the
        // discovery layer can dim/restore them. Colliders stripped (the trigger is the only one).
        private Renderer[] BuildArch(Transform parent, float h, Color accent)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
            if (lit == null)
                FlowTrace.Warn("DungeonPortals", "BuildArch: URP/Lit shader not found — arch will use the fallback material (may render untinted).");
            Material mat = lit != null ? new Material(lit) : null;
            if (mat != null)
            {
                mat.EnableKeyword("_EMISSION");
                // DEF-94: arch reads as an arcane-violet portal (per-dungeon accent kept
                // as a subtle tint), NOT a flat pastel AccentColor frame. WO-272's glow
                // layers on top of this base.
                Color archBase = PortalVFXController.ArchBaseColor(accent);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", archBase);
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", PortalVFXController.ArchEmissionColor(accent));
            }

            var list = new List<Renderer>(3);
            list.Add(MakeBox(parent, new Vector3(-0.9f, h * 0.5f, 0f), new Vector3(0.3f, h, 0.3f), mat));
            list.Add(MakeBox(parent, new Vector3(0.9f, h * 0.5f, 0f), new Vector3(0.3f, h, 0.3f), mat));
            list.Add(MakeBox(parent, new Vector3(0f, h + 0.15f, 0f), new Vector3(2.1f, 0.3f, 0.3f), mat));
            return list.ToArray();
        }

        private static Renderer MakeBox(Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Arch";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col); // arch shouldn't block movement; the trigger is the only collider
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (mat != null && r != null) r.sharedMaterial = mat;
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

            if (built.Count > 0) return built;

            // Inline fallback — the two dungeon scenes that exist in the build today.
            var fb = new List<DungeonDef>(2);
            if (UnityEngine.Application.CanStreamedLevelBeLoaded("Dungeon_HealersCottage"))
                fb.Add(MakeDef("HealersCottage", "Dungeon_HealersCottage", "Healer's Cottage", new Color(1f, 0.82f, 0.48f)));
            if (UnityEngine.Application.CanStreamedLevelBeLoaded("Dungeon_FolksGranary"))
                fb.Add(MakeDef("FolksGranary", "Dungeon_FolksGranary", "Folk's Granary", new Color(0.60f, 0.90f, 0.70f)));
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
