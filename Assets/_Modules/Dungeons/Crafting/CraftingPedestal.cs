// =============================================================================
// CraftingPedestal — the dungeon crafting-station interactable (Workstream C).
// -----------------------------------------------------------------------------
// Part of the v2 dungeon crafting system. The pedestal is where the Keeper
// binds collected ingredients into a finished item. It REPLACES the §5.4
// placeholder "crafting shard pedestal" primitive in the Hidden Vault with a
// real, working interactable.
//
// ── Idiom ──
// Proximity activation, the SAME pattern as Checkpoint.cs / LoreStone.cs: a
// per-frame XZ-plane distance check against the hero transform. While the
// Keeper stands inside the radius an "interact" prompt shows; pressing the
// interact key (legacy Input Manager — UnityEngine.Input.GetKeyDown, project
// mandate) or calling Interact() from a touch button opens the crafting panel.
//
// The pedestal owns NO UI itself — it raises typed UnityEvents the
// CraftingPanelController (a sibling component on the dungeon HUD UIDocument)
// subscribes to. That keeps the pedestal a pure scene actor and the panel a
// pure UI Toolkit view; the DungeonController wires the two together on load.
//
// Configured by DungeonController from crafting-recipes.json — the pedestal
// block carries its position, radius and the recipe id it fulfils. Once the
// recipe is crafted the crystal settles from violet to a warm gold (the same
// "settled" visual language as the Checkpoint shrine).
// =============================================================================

using UnityEngine;
using UnityEngine.Events;

namespace DeNelle.Dungeons
{
    /// <summary>
    /// The crafting-pedestal interactable. The Keeper approaches, opens the
    /// crafting panel (interact key / touch), and crafts the pedestal's recipe
    /// once every ingredient is in the <see cref="DungeonInventory"/>. Raises
    /// typed events the UI Toolkit crafting panel subscribes to.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CraftingPedestal : MonoBehaviour
    {
        // ── Crystal tones (mirrors Checkpoint's violet -> gold settle) ───────

        /// <summary>Violet shard tone while the recipe is uncrafted.</summary>
        private static readonly Color Violet = new Color(0.608f, 0.424f, 1f);   // #9b6cff

        /// <summary>Warm gold tone the shard settles to once the recipe is crafted.</summary>
        private static readonly Color Gold = new Color(1f, 0.812f, 0.420f);     // #ffcf6b

        [Header("Interaction")]
        [Tooltip("Optional world-space prompt object shown while the Keeper is in range.")]
        [SerializeField] private GameObject _interactPrompt;

        [Header("Scene refs")]
        [Tooltip("The shared crafting inventory the pedestal crafts against.")]
        [SerializeField] private DungeonInventory _inventory;

        [Tooltip("The floating shard mesh renderer — recolours violet -> gold on craft.")]
        [SerializeField] private Renderer _shardRenderer;

        [Tooltip("The shard's point light — a 'follow the light' cue from afar.")]
        [SerializeField] private Light _shardLight;

        [Tooltip("The shard transform — pulse-scaled / spun while uncrafted.")]
        [SerializeField] private Transform _shard;

        [Header("Shard animation")]
        [Tooltip("Pulse rate of the uncrafted shard (radians/sec sine input).")]
        [SerializeField] private float _pulseSpeed = 2f;

        [Tooltip("Pulse depth — the uncrafted shard scales +/- this fraction.")]
        [SerializeField] private float _pulseDepth = 0.08f;

        [Tooltip("When true, the shard pulse is pinned flat (reduced motion).")]
        [SerializeField] private bool _reducedMotion;

        [Header("Audio")]
        [Tooltip("Optional one-shot SFX played the moment a recipe is crafted.")]
        [SerializeField] private AudioSource _craftAudio;

        [Header("Events")]
        [Tooltip("Raised with the in-range state each time it changes — the UI " +
                 "shows / hides the 'press E to craft' affordance off this.")]
        public UnityEvent<bool> RangeChanged = new UnityEvent<bool>();

        [Tooltip("Raised when the Keeper opens the pedestal — the crafting panel " +
                 "subscribes and shows the recipe + the live have/need state.")]
        public UnityEvent<CraftingPanelRequest> OpenRequested = new UnityEvent<CraftingPanelRequest>();

        [Tooltip("Raised when the panel should close (the Keeper walked away).")]
        public UnityEvent CloseRequested = new UnityEvent();

        [Tooltip("Raised with the recipe id once a recipe is successfully crafted.")]
        public UnityEvent<CraftingRecipe> Crafted = new UnityEvent<CraftingRecipe>();

        [Tooltip("Raised with the canon bible-voice toast string on a successful craft.")]
        public UnityEvent<string> ToastRequested = new UnityEvent<string>();

        // ── Runtime data (handed in via Configure) ───────────────────────────

        private CraftingPedestalDef _def;
        private CraftingDataSet _craftingData;
        private CraftingRecipe _recipe;
        private Transform _hero;

        /// <summary>True while the Keeper is within reach of the pedestal.</summary>
        public bool InRange { get; private set; }

        /// <summary>True while the crafting panel is open against this pedestal.</summary>
        public bool IsPanelOpen { get; private set; }

        /// <summary>True once this pedestal's recipe has been crafted this run.</summary>
        public bool IsCrafted =>
            _inventory != null && _recipe != null && _inventory.HasCrafted(_recipe.Id);

        /// <summary>The recipe this pedestal fulfils — null until configured.</summary>
        public CraftingRecipe Recipe => _recipe;

        // ── Configuration ────────────────────────────────────────────────────

        /// <summary>
        /// Wires the pedestal to its authored definition, the crafting data set
        /// (for the recipe + ingredient lookups) and the shared inventory, and
        /// positions it at the layout's coordinates. Called by the dungeon
        /// controller against crafting-recipes.json.
        /// </summary>
        public void Configure(
            CraftingPedestalDef def,
            CraftingDataSet craftingData,
            DungeonInventory inventory,
            Transform hero)
        {
            _def = def;
            _craftingData = craftingData;
            _inventory = inventory;
            _hero = hero;

            if (_def != null)
                transform.position = _def.Position.ToWorld();

            _recipe = craftingData != null && _def != null
                ? craftingData.FindRecipe(_def.RecipeId)
                : null;

            if (_recipe == null)
            {
                Debug.LogWarning(
                    $"[CraftingPedestal] Pedestal '{_def?.Id}' references recipe " +
                    $"'{_def?.RecipeId}' which is not in crafting-recipes.json — " +
                    "the pedestal will be inert.");
            }

            if (_interactPrompt != null) _interactPrompt.SetActive(false);
            ApplyShardTone();
        }

        /// <summary>Sets the reduced-motion preference — pins the shard pulse flat.</summary>
        public void SetReducedMotion(bool reduced)
        {
            _reducedMotion = reduced;
        }

        // ── Per-frame ────────────────────────────────────────────────────────

        private void Update()
        {
            if (_def == null || _hero == null) return;

            AnimateShard();
            UpdateProximity();

            // The keyboard interact key was REMOVED — the on-screen touch /
            // MobileInteractButton path (Interact(), called while InRange) is the
            // sole entry point for opening the crafting panel.
        }

        /// <summary>
        /// Tracks the Keeper's proximity. On entering range the interact prompt
        /// shows + <see cref="RangeChanged"/> fires; on leaving range the prompt
        /// hides and any open panel is closed.
        /// </summary>
        private void UpdateProximity()
        {
            Vector3 a = transform.position; a.y = 0f;
            Vector3 b = _hero.position; b.y = 0f;
            float r = _def.Radius;
            bool nowInRange = (a - b).sqrMagnitude <= r * r;
            if (nowInRange == InRange) return;

            InRange = nowInRange;

            // The prompt only shows for an uncrafted recipe (a crafted pedestal
            // still opens — to show the finished state — but gives no nudge).
            if (_interactPrompt != null)
                _interactPrompt.SetActive(InRange && _recipe != null && !IsCrafted);

            RangeChanged.Invoke(InRange);

            // Walking away closes an open panel so the UI cannot linger.
            if (!InRange && IsPanelOpen)
                ClosePanel();
        }

        /// <summary>Pulse-scales + spins the uncrafted shard — frozen under reduced motion.</summary>
        private void AnimateShard()
        {
            if (_shard == null) return;
            if (IsCrafted)
            {
                // A crafted shard holds its scale and rotates gently.
                if (!_reducedMotion) _shard.Rotate(0f, 0.24f, 0f);
                _shard.localScale = Vector3.one;
                return;
            }
            if (_reducedMotion)
            {
                _shard.localScale = Vector3.one;
                return;
            }
            float s = 1f + Mathf.Sin(Time.time * _pulseSpeed) * _pulseDepth;
            _shard.localScale = new Vector3(s, s, s);
            _shard.Rotate(0f, 0.72f, 0f);
        }

        // ── Interaction ──────────────────────────────────────────────────────

        /// <summary>
        /// Opens the crafting panel against this pedestal. The input layer (the
        /// interact key, or a touch button bound to this) calls this only while
        /// <see cref="InRange"/>. Builds a <see cref="CraftingPanelRequest"/>
        /// snapshot and raises <see cref="OpenRequested"/> for the UI.
        /// </summary>
        public void Interact()
        {
            if (_recipe == null || !InRange || IsPanelOpen) return;
            IsPanelOpen = true;
            OpenRequested.Invoke(BuildRequest());
        }

        /// <summary>Closes the crafting panel — raises <see cref="CloseRequested"/>.</summary>
        public void ClosePanel()
        {
            if (!IsPanelOpen) return;
            IsPanelOpen = false;
            CloseRequested.Invoke();
        }

        /// <summary>
        /// Attempts the craft — the crafting panel's "Craft" button calls this.
        /// Consumes the ingredients through the inventory, settles the shard to
        /// gold, raises <see cref="Crafted"/> + <see cref="ToastRequested"/>, and
        /// returns a fresh request snapshot so the panel can repaint the finished
        /// state. Returns null when the craft could not be afforded.
        /// </summary>
        public CraftingPanelRequest TryCraft()
        {
            if (_recipe == null || _inventory == null) return null;
            if (!_inventory.Craft(_recipe)) return null;

            if (_craftAudio != null) _craftAudio.Play();
            ApplyShardTone();
            Crafted.Invoke(_recipe);
            if (!string.IsNullOrEmpty(_recipe.CraftedToast))
                ToastRequested.Invoke(_recipe.CraftedToast);

            if (_interactPrompt != null) _interactPrompt.SetActive(false);
            return BuildRequest();
        }

        /// <summary>
        /// Builds a fresh snapshot of the panel state — the recipe, the crafting
        /// data (for ingredient display names) and the live inventory. The panel
        /// reads have/need straight off the inventory each time it is shown.
        /// </summary>
        private CraftingPanelRequest BuildRequest()
        {
            return new CraftingPanelRequest
            {
                Pedestal = this,
                Recipe = _recipe,
                CraftingData = _craftingData,
                Inventory = _inventory,
            };
        }

        /// <summary>Applies the violet (uncrafted) or gold (crafted) shard tone.</summary>
        private void ApplyShardTone()
        {
            bool crafted = IsCrafted;
            Color tone = crafted ? Gold : Violet;

            if (_shardRenderer != null && _shardRenderer.material != null)
            {
                var mat = _shardRenderer.material;
                mat.color = tone;
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", tone * (crafted ? 1.4f : 2.2f));
                }
            }

            if (_shardLight != null)
            {
                _shardLight.color = tone;
                _shardLight.intensity = crafted ? 1.6f : 2.4f;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (_def == null) return;
            Gizmos.color = new Color(0.608f, 0.424f, 1f, 0.4f);
            Gizmos.DrawWireSphere(_def.Position.ToWorld(), _def.Radius);
        }
    }

    /// <summary>
    /// The payload of a crafting-panel open request — handed to the UI Toolkit
    /// crafting panel. Carries live references; the panel reads have/need off
    /// the inventory each time it repaints.
    /// </summary>
    public sealed class CraftingPanelRequest
    {
        /// <summary>The pedestal that raised the request — the panel calls back into it.</summary>
        public CraftingPedestal Pedestal;

        /// <summary>The recipe being shown.</summary>
        public CraftingRecipe Recipe;

        /// <summary>The crafting data set — for ingredient display names / glyphs.</summary>
        public CraftingDataSet CraftingData;

        /// <summary>The live inventory — the panel reads have-counts off this.</summary>
        public DungeonInventory Inventory;
    }
}
