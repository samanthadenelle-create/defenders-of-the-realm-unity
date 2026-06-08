// =============================================================================
// ArenaHeraldSpawner — the in-village ENTRY POINT that makes the Arena reachable.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Arena
//
// THE GAP IT CLOSES: the Arena MVP (ArenaPanel.Open / ArenaMode) was fully built
// but NOTHING opened it -- it was unreachable. This places a discoverable "Arena
// Herald" marker (a code-built glowing banner) near the village Heart and, when the
// hero comes close, offers an Interact prompt that calls ArenaPanel.Open(). The
// player walks up to it and taps to open the opponent-select / wager screen.
//
// PATTERN REUSE (CLAUDE.md SS9 -- no new system, no scene bake):
//   * Self-bootstraps via RuntimeInitializeOnLoadMethod(AfterSceneLoad) -- NO scene
//     edit, NO prefab dependency, NO bake. Mirrors DungeonWorldPortalSpawner /
//     CampSystem / NodeDiscoverySystem exactly.
//   * Proximity interaction reuses the SHARED MobileInteractButton (touch) plus the
//     desktop [F] key -- the same dual-input affordance every village structure uses
//     (DEF-203). Suppressed automatically in Build Mode + while a modal panel is open
//     (MobileInteractButton.Suppressed / PanelManager.AnyOpen).
//   * Panel lifecycle MIRRORS ShopPanel's entry (NPCCommandBridge.CmdOpenShop):
//     FindFirstObjectByType<ArenaPanel>() or create a host GameObject, then Open().
//
// DDOL singleton: Destroy(this), NOT the host (CLAUDE.md "singleton dedup destroys
// host"). Village -> Core only; cross-module reads are null-conditional.
// Canon: village is Elarion. ASCII-only runtime strings.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village.Arena
{
    /// <summary>
    /// Places a discoverable "Arena Herald" marker in the village and opens
    /// <see cref="ArenaPanel"/> when the hero interacts with it (touch button or [F]).
    /// Self-bootstrapping; reuses MobileInteractButton + the ShopPanel open pattern.
    /// </summary>
    public sealed class ArenaHeraldSpawner : MonoBehaviour
    {
        public static ArenaHeraldSpawner Instance { get; private set; }

        // ── Tunables (code-only; no SO authoring) ────────────────────────────
        [Tooltip("Where the Arena herald stands, relative to the village Heart (0,0,0). " +
                 "A few metres off the plaza so it reads as its own landmark.")]
        public Vector3 HeraldOffset = new Vector3(8f, 0f, 6f);

        [Tooltip("How close (metres) the hero must be for the Interact prompt to arm.")]
        public float InteractRadius = 4.5f;

        [Tooltip("Visual height of the placeholder banner pole (metres).")]
        public float BannerHeight = 3.2f;

        private const float PlaceRetryInterval = 1.0f;

        private bool _placed;
        private float _retryTimer;
        private Transform _heraldRoot;
        private Transform _hero;
        private ArenaPanel _panel;

        // =====================================================================
        // Self-bootstrap (no scene edit). Runs after every scene load; idempotent.
        // =====================================================================
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("ArenaHeraldSpawner");
            go.AddComponent<ArenaHeraldSpawner>();
            Object.DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            // Destroy(this), not the host -- DDOL singleton (CLAUDE.md memory).
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
            TickProximity();
        }

        // =====================================================================
        // Placement — one herald near the village Heart. Waits for the village
        // scene's hero to exist so we only place inside the village (not e.g. the
        // intro / dungeon scenes that have no "Player").
        // =====================================================================
        private void TryPlace()
        {
            // Only place in a scene that actually has a hero (the village / outer
            // world). This keeps the herald out of the intro, dungeons, etc.
            var hero = SafeFindWithTag("Player");
            if (hero == null) return;

            _heraldRoot = BuildHerald(HeraldOffset);
            _placed = true;
            Debug.Log($"[ArenaHeraldSpawner] Arena herald placed at {_heraldRoot.position}. " +
                      "Walk up + Interact (Tap / F) to open the Arena.");
        }

        // =====================================================================
        // Proximity — arm the shared Interact prompt while the hero is in range;
        // [F] or the touch button opens the Arena. Mirrors every village structure.
        // =====================================================================
        private void TickProximity()
        {
            if (_heraldRoot == null || _hero == null) return;

            float sqr = InteractRadius * InteractRadius;
            if ((_heraldRoot.position - _hero.position).sqrMagnitude > sqr) return;

            // Touch path: the shared bottom-centre button (auto-suppressed in build
            // mode + while a modal is open). Tapping it opens the Arena.
            MobileInteractButton.Request(this, "Enter Arena", OpenArena);

            // Desktop path: [F]. Skip while build mode suppresses interaction.
            if (!MobileInteractButton.Suppressed && Input.GetKeyDown(KeyCode.F))
                OpenArena();
        }

        // =====================================================================
        // Open the Arena — MIRRORS ShopPanel's entry (NPCCommandBridge.CmdOpenShop):
        // find-or-create the panel host, then Open().
        // =====================================================================
        private void OpenArena()
        {
            if (_panel == null) _panel = FindFirstObjectByType<ArenaPanel>();
            if (_panel == null)
            {
                var host = new GameObject("ArenaPanelHost");
                _panel = host.AddComponent<ArenaPanel>();
            }
            _panel.Open();
            Debug.Log("[ArenaHeraldSpawner] Opened the Arena panel.");
        }

        // =====================================================================
        // Build a code-built placeholder banner so the herald reads as a landmark
        // with no art dependency (BuildArch pattern from DungeonWorldPortalSpawner).
        // =====================================================================
        private Transform BuildHerald(Vector3 offset)
        {
            var root = new GameObject("ArenaHerald");
            DontDestroyOnLoad(root);
            root.transform.position = offset; // Heart is at world origin (0,0,0).

            // Face the banner back toward the Heart so the hero reads its front.
            Vector3 toHeart = -new Vector3(offset.x, 0f, offset.z);
            if (toHeart.sqrMagnitude > 0.01f)
                root.transform.rotation = Quaternion.LookRotation(toHeart.normalized);

            Color accent = new Color(0.85f, 0.20f, 0.20f); // arena crimson

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
            Material mat = lit != null ? new Material(lit) : null;
            if (mat != null)
            {
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", accent);
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", accent * 0.6f);
            }

            // Pole + a banner flag panel near the top.
            MakeBox(root.transform, new Vector3(0f, BannerHeight * 0.5f, 0f),
                    new Vector3(0.22f, BannerHeight, 0.22f), mat);
            MakeBox(root.transform, new Vector3(0.55f, BannerHeight * 0.82f, 0f),
                    new Vector3(1.0f, BannerHeight * 0.5f, 0.08f), mat);

            return root.transform;
        }

        private static void MakeBox(Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "HeraldPart";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col); // marker only; proximity is distance-checked, not a trigger
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (mat != null && r != null) r.sharedMaterial = mat;
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
    }
}
