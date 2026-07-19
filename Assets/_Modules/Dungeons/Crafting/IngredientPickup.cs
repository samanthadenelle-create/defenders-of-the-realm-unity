// =============================================================================
// IngredientPickup — a collectible crafting-ingredient pickup (Workstream C).
// -----------------------------------------------------------------------------
// Part of the v2 dungeon crafting system. A small floating ingredient mote the
// Keeper walks over to collect — the first half of the craft-a-torch loop.
//
// ── Idiom ──
// Proximity-activation, the SAME pattern as Checkpoint.cs / EncounterTrigger.cs:
// a per-frame XZ-plane distance check against the hero transform, firing ONCE
// the first time the Keeper crosses the pickup radius. Auto-pickup (no tap) is
// deliberate — an ingredient mote on the floor reads as "walk over to grab",
// which keeps the loop legible (owner acceptance checklist: make it clear).
//
// The pickup writes its ingredient into the shared DungeonInventory; the
// inventory's CollectPickup() dedupes by pickup id, so even a pickup re-walked
// after the ATB round-trip is granted only once. On collection the mote hides
// itself and raises Collected for a toast / SFX layer.
//
// Configured by DungeonController from the crafting-recipes.json placements —
// the strongly-typed IngredientPlacement carries the ingredient id + position.
// =============================================================================

using UnityEngine;
using UnityEngine.Events;

namespace DeNelle.Dungeons
{
    /// <summary>
    /// A collectible crafting-ingredient pickup — granted to the
    /// <see cref="DungeonInventory"/> the first time the Keeper walks within
    /// <see cref="_pickupRadius"/>. One ingredient, collected once per run.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IngredientPickup : MonoBehaviour
    {
        [Header("Interaction")]
        [Tooltip("World-unit radius within which the Keeper auto-collects this pickup.")]
        [SerializeField] private float _pickupRadius = 1.6f;

        [Header("Scene refs")]
        [Tooltip("The shared crafting inventory the pickup writes its ingredient into.")]
        [SerializeField] private DungeonInventory _inventory;

        [Tooltip("The visual mote — hidden once the pickup is collected. Optional: " +
                 "defaults to this GameObject's first child renderer's object.")]
        [SerializeField] private GameObject _moteVisual;

        [Tooltip("Optional bob/spin transform — the mote floats while uncollected.")]
        [SerializeField] private Transform _moteSpin;

        [Header("Mote animation")]
        [Tooltip("Spin rate of the uncollected mote (degrees/sec).")]
        [SerializeField] private float _spinSpeed = 70f;

        [Tooltip("Vertical bob amplitude of the uncollected mote (world units).")]
        [SerializeField] private float _bobAmplitude = 0.18f;

        [Tooltip("Vertical bob rate (radians/sec sine input).")]
        [SerializeField] private float _bobSpeed = 2.2f;

        [Tooltip("When true, the mote bob/spin is pinned flat (reduced motion).")]
        [SerializeField] private bool _reducedMotion;

        [Header("Audio")]
        [Tooltip("Optional one-shot collect SFX played when the Keeper grabs the ingredient.")]
        [SerializeField] private AudioSource _collectAudio;

        [Header("Events")]
        [Tooltip("Raised with the ingredient id the moment this pickup is collected.")]
        public UnityEvent<string> Collected = new UnityEvent<string>();

        // ── Runtime data (handed in via Configure) ───────────────────────────

        private IngredientPlacement _def;
        private Transform _hero;
        private float _baseMoteY;

        /// <summary>True once this pickup has been collected this run.</summary>
        public bool IsCollected { get; private set; }

        /// <summary>The ingredient id this pickup grants — e.g. <c>dry-reed</c>.</summary>
        public string IngredientId => _def?.IngredientId ?? string.Empty;

        /// <summary>The pickup's stable id — e.g. <c>reed-garden</c>.</summary>
        public string PickupId => _def?.PickupId ?? string.Empty;

        // ── Configuration ────────────────────────────────────────────────────

        /// <summary>
        /// Wires the pickup to its authored placement + the shared inventory,
        /// and positions it at the layout's coordinates. Called by the dungeon
        /// controller against crafting-recipes.json. A pickup already collected
        /// on a resumed run (the ATB round-trip) shows as already taken.
        /// </summary>
        public void Configure(IngredientPlacement def, DungeonInventory inventory, Transform hero)
        {
            _def = def;
            _inventory = inventory;
            _hero = hero;

            if (_def != null)
                transform.position = _def.Position.ToWorld();

            if (_moteSpin != null) _baseMoteY = _moteSpin.localPosition.y;

            // A pickup already collected this run (e.g. on an ATB-resume reload)
            // stays hidden — the inventory dedupes by pickup id.
            IsCollected = _inventory != null && _def != null
                && _inventory.HasCollectedPickup(_def.PickupId);
            ApplyCollectedVisual();
        }

        /// <summary>Sets the reduced-motion preference — pins the mote bob/spin flat.</summary>
        public void SetReducedMotion(bool reduced)
        {
            _reducedMotion = reduced;
        }

        // ── Runtime factory (WO-749 — scatter without a scene bake) ──────────

        /// <summary>
        /// Builds a runtime ingredient-scatter mote so the WO-749 12-ingredient floor
        /// scatter needs NO scene bake: DungeonController spawns one of these per
        /// crafting-recipes.json placement that has no scene-authored pickup. The mote
        /// is a small URP-tinted sphere (bobs/spins); the collected ingredient rides
        /// the per-run <see cref="DungeonInventory"/> and is banked to the larder on
        /// exit (DungeonLootGrant). As a static member of this class it may set the
        /// private mote fields directly — no reflection.
        /// </summary>
        public static IngredientPickup CreateRuntime(Transform parent, IngredientPlacement def,
            DungeonInventory inventory, Transform hero, Color tint)
        {
            var root = new GameObject($"IngredientPickup_{(def != null ? def.PickupId : "scatter")}");
            if (parent != null) root.transform.SetParent(parent, false);

            // The visual mote — a small tinted sphere the Keeper walks over.
            var mote = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mote.name = "Mote";
            mote.transform.SetParent(root.transform, false);
            mote.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            mote.transform.localScale = Vector3.one * 0.4f;

            // Proximity pickup is a distance check, not a physics event — drop the
            // primitive collider so the mote never blocks the hero or the NavMesh.
            var col = mote.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // CreatePrimitive ships the built-in Standard shader (magenta under URP);
            // build a URP-compatible tinted material (the "pink floor" URP/Lit lesson).
            var rend = mote.GetComponent<Renderer>();
            if (rend != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit")
                             ?? Shader.Find("Universal Render Pipeline/Unlit")
                             ?? Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    var mat = new Material(shader);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint);
                    rend.material = mat;
                }
            }

            var pickup = root.AddComponent<IngredientPickup>();
            pickup._moteVisual = mote;
            pickup._moteSpin = mote.transform;
            pickup.Configure(def, inventory, hero);
            return pickup;
        }

        // ── Per-frame ────────────────────────────────────────────────────────

        private void Update()
        {
            if (IsCollected || _def == null || _hero == null) return;

            AnimateMote();
            CheckProximity();
        }

        /// <summary>
        /// Collects the pickup the first time the Keeper enters its radius —
        /// grants the ingredient to the inventory, hides the mote, raises
        /// <see cref="Collected"/> and plays the collect SFX.
        /// </summary>
        private void CheckProximity()
        {
            Vector3 a = transform.position; a.y = 0f;
            Vector3 b = _hero.position; b.y = 0f;
            float r = _pickupRadius;
            if ((a - b).sqrMagnitude > r * r) return;

            // CollectPickup returns false if this pickup id was already taken.
            bool granted = _inventory != null
                && _inventory.CollectPickup(_def.PickupId, _def.IngredientId);
            if (!granted && _inventory != null && _inventory.HasCollectedPickup(_def.PickupId))
            {
                // Already collected — just settle the visual and stop ticking.
                IsCollected = true;
                ApplyCollectedVisual();
                return;
            }
            if (!granted) return;

            IsCollected = true;
            ApplyCollectedVisual();
            if (_collectAudio != null) _collectAudio.Play();
            Collected.Invoke(_def.IngredientId);
        }

        /// <summary>Spins + bobs the uncollected mote — frozen under reduced motion.</summary>
        private void AnimateMote()
        {
            if (_moteSpin == null || _reducedMotion) return;
            _moteSpin.Rotate(0f, _spinSpeed * Time.deltaTime, 0f);
            Vector3 p = _moteSpin.localPosition;
            p.y = _baseMoteY + Mathf.Sin(Time.time * _bobSpeed) * _bobAmplitude;
            _moteSpin.localPosition = p;
        }

        /// <summary>Shows the mote while uncollected, hides it once collected.</summary>
        private void ApplyCollectedVisual()
        {
            if (_moteVisual != null) _moteVisual.SetActive(!IsCollected);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.886f, 0.694f, 0.353f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, _pickupRadius);
        }
    }
}
