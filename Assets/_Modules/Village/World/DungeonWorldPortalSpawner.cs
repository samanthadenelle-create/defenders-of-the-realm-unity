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
//   * PLACEMENT  — region-gated NavMesh-valid points via the SAME pattern as
//     RegionMobSpawner.TryFindSpawnPoint: pick a candidate in an outer region
//     (ZoneManager.GetZone / RegionSpawnTable.HasRoster) and snap it to the baked
//     NavMesh (NavMesh.SamplePosition). One portal per assigned region.
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
using DeNelle.Core.World;

namespace DeNelle.Village.World
{
    /// <summary>
    /// Places a few hidden, discoverable dungeon portals at NavMesh-valid positions
    /// out in the OuterWorld regions and wires each to load its dungeon via the
    /// existing <see cref="DungeonPortal"/>. Self-bootstrapping; reuses ZoneManager,
    /// the NavMesh, DungeonDef data and the DungeonPortal interaction/load path.
    /// </summary>
    public sealed class DungeonWorldPortalSpawner : MonoBehaviour
    {
        public static DungeonWorldPortalSpawner Instance { get; private set; }

        // ── Tunables (code-only; no SO authoring) ────────────────────────────
        [Tooltip("Where in a region to aim a portal: distance OUT past the village wall " +
                 "(along the region's dominant axis) the placement ray targets.")]
        public float RegionDepthMeters = 95f;

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

        // Which region each available dungeon lives in. Assigned round-robin across the
        // outer (non-Village) regions in danger order so the easy dungeon sits in the
        // gentle region and harder ones sit deeper. Reuses ZoneManager's RegionId set.
        private static readonly RegionId[] OuterRegionsByDanger =
        {
            RegionId.Goldfields, // tier 1 — gentlest
            RegionId.Stoneback,  // tier 2
            RegionId.Mirewood,   // tier 3
            RegionId.Ashwood,    // tier 4 — deadliest
        };

        // PlayerPrefs key prefix for "this portal has been discovered" (position-keyed,
        // same convention as NodeDiscoverySystem so discovery survives a reload).
        private const string PrefKeyPrefix = "dotr-dungeon-portal-discovered-";

        private bool _placed;
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
        }

        private readonly List<Portal> _portals = new List<Portal>();

        // =====================================================================
        // Self-bootstrap (no scene edit). Runs after every scene load; idempotent.
        // =====================================================================
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
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
        // Placement — one portal per assigned dungeon, region-gated + NavMesh-snapped.
        // Waits (retry) until OuterWorld is loaded and a NavMesh is baked.
        // =====================================================================
        private void TryPlace()
        {
            var defs = LoadDefs();
            if (defs.Count == 0) return; // nothing to place (no built dungeon scenes)

            // Gate on a baked NavMesh existing — without it every SamplePosition fails and
            // we'd place portals on un-walkable ground. The triangulation is empty until the
            // OuterWorld NavMesh bake is present at runtime.
            var tri = NavMesh.CalculateTriangulation();
            if (tri.vertices == null || tri.vertices.Length == 0) return;

            _root = new GameObject("[DungeonWorldPortals]").transform;
            DontDestroyOnLoad(_root.gameObject);

            int placed = 0;
            for (int i = 0; i < defs.Count; i++)
            {
                RegionId region = OuterRegionsByDanger[i % OuterRegionsByDanger.Length];
                if (TryFindPortalPoint(region, out Vector3 pos))
                {
                    BuildPortal(defs[i], pos);
                    placed++;
                }
                else
                {
                    Debug.LogWarning($"[DungeonWorldPortals] No NavMesh-valid point in {region} for " +
                        $"'{defs[i].DungeonId}' — portal not placed this region.");
                }
            }

            if (placed > 0)
            {
                _placed = true;
                Debug.Log($"[DungeonWorldPortals] Placed {placed} hidden dungeon portal(s) across the outer world.");
            }
            else
            {
                // Couldn't seat any yet (NavMesh maybe still loading) — leave _placed false to retry.
                if (_root != null) { Destroy(_root.gameObject); _root = null; }
            }
        }

        // Aim a point deep in the target region (dominant-axis fan-out, mirrors ZoneManager's
        // model) and snap it to the baked NavMesh — the SAME approach as
        // RegionMobSpawner.TryFindSpawnPoint, but biased to a region instead of the player.
        private bool TryFindPortalPoint(RegionId region, out Vector3 pos)
        {
            Vector3 dir = RegionDirection(region);
            for (int attempt = 0; attempt < 12; attempt++)
            {
                // Jitter around the region's depth target so portals aren't perfectly radial.
                float depth = RegionDepthMeters * Random.Range(0.8f, 1.25f);
                Vector2 lat = Random.insideUnitCircle * 30f; // lateral spread off the axis
                Vector3 perp = new Vector3(-dir.z, 0f, dir.x);
                Vector3 want = dir * depth + perp * lat.x + new Vector3(0f, 0f, 0f);
                want.y = 0f;

                if (NavMesh.SamplePosition(want, out NavMeshHit hit, 12f, NavMesh.AllAreas)
                    && ZoneManager.GetZone(hit.position) == region)
                {
                    pos = hit.position;
                    return true;
                }
            }
            pos = Vector3.zero;
            return false;
        }

        // Outward unit direction toward a region core (matches ZoneManager's cardinal fan-out:
        // East +X Goldfields, West -X Stoneback, South -Z Mirewood, North +Z Ashwood).
        private static Vector3 RegionDirection(RegionId region)
        {
            switch (region)
            {
                case RegionId.Goldfields: return Vector3.right;     // +X East
                case RegionId.Stoneback:  return Vector3.left;      // -X West
                case RegionId.Mirewood:   return Vector3.back;      // -Z South
                case RegionId.Ashwood:    return Vector3.forward;   // +Z North
                default:                  return Vector3.right;
            }
        }

        // =====================================================================
        // Build one portal: a code-built placeholder arch + the EXISTING DungeonPortal
        // driver (proximity prompt + SceneManager.LoadScene). DungeonPortal adds its own
        // PortalVFXController glow in Start(), so the arch reads as a portal with no asset.
        // =====================================================================
        private void BuildPortal(DungeonDef def, Vector3 pos)
        {
            var root = new GameObject($"DungeonWorldPortal_{def.DungeonId}");
            root.transform.SetParent(_root, false);
            root.transform.position = pos;
            // Face the arch back toward the village centre so the hero reads its front.
            Vector3 toVillage = -new Vector3(pos.x, 0f, pos.z);
            if (toVillage.sqrMagnitude > 0.01f)
                root.transform.rotation = Quaternion.LookRotation(toVillage.normalized);

            var renderers = BuildArch(root.transform, PortalHeight, def.AccentColor);

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

            Debug.Log($"[DungeonWorldPortals] '{def.ResolveName()}' portal at {pos} " +
                      $"(discovered={entry.Discovered}).");
        }

        // Two posts + a lintel, tinted with the dungeon accent. Returns the renderers so the
        // discovery layer can dim/restore them. Colliders stripped (the trigger is the only one).
        private Renderer[] BuildArch(Transform parent, float h, Color accent)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
            Material mat = lit != null ? new Material(lit) : null;
            if (mat != null)
            {
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", accent);
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", accent * 0.6f);
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
                if (p == null || p.Root == null) { _portals.RemoveAt(i); continue; }

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
            Debug.Log($"[DungeonWorldPortals] Hero discovered a hidden dungeon portal (key {p.Key}).");
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
