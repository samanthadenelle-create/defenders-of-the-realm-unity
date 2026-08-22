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
//
// ── Mote art: SHAPE FAMILIES, not tinted pellets (WO-1132 deliverable 5) ──
// THE FINDING: every scatter mote was the SAME plain sphere, tinted from the
// crafting-recipes.json `tint` hex. Those authored tints are largely PASTELS
// (a9c4ff pale blue, d8cbb0 pale cream, ffe58a pale yellow), so on an unlit
// sphere in a dark dungeon they all wash out to the SAME WHITE PELLET — the
// player could not tell a moonbloom from a cloth scrap without walking over it.
//
// COLOURBLIND LAW (the owner is red/green colourblind, and this is binding):
// identity must NEVER rest on hue alone. So the SILHOUETTE carries the meaning
// and the tint only reinforces it — the same law ComposedPropVisuals states for
// the composed-dungeon key/lock/trap/oil props, and this file follows that
// file's idiom deliberately (URP/Lit emissive primitives, colliders stripped).
//
// The shape is picked from the per-ingredient `glyph` char that
// crafting-recipes.json ALREADY authors (CraftingData.CraftingIngredient.Glyph)
// and that nothing had ever read for the world mote: '|' stalk, '~' droplet,
// '*' radiating cluster, '=' folded slab, 'T' mushroom, 'Y' forked root,
// 'b' round-bellied flask, unknown/absent -> the original sphere. Several
// ingredients share a glyph (five are '*', two are 'T') and that is FINE and
// intentional: they are the same FAMILY, and the tint separates within it. We
// do not chase twelve unique silhouettes.
//
// Second half of the white-pellet answer: the mote is EMISSIVE. An unlit pastel
// primitive is invisible at low lantern oil no matter what shape it is.
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
        /// crafting-recipes.json placement that has no scene-authored pickup. The
        /// collected ingredient rides the per-run <see cref="DungeonInventory"/> and is
        /// banked to the larder on exit (DungeonLootGrant). As a static member of this
        /// class it may set the private mote fields directly — no reflection.
        /// <para>
        /// The mote is no longer a bare tinted sphere: <paramref name="glyph"/> (the
        /// ingredient's authored <c>glyph</c> char) selects a SHAPE FAMILY so the
        /// silhouette — not the pastel hue — is what tells two ingredients apart. See
        /// the file header for the white-pellet finding and the colourblind law.
        /// A null/empty/unrecognised glyph keeps the original sphere, so an ingredient
        /// authored without one is never worse off than before.
        /// </para>
        /// </summary>
        public static IngredientPickup CreateRuntime(Transform parent, IngredientPlacement def,
            DungeonInventory inventory, Transform hero, Color tint, string glyph = null)
        {
            var root = new GameObject($"IngredientPickup_{(def != null ? def.PickupId : "scatter")}");
            if (parent != null) root.transform.SetParent(parent, false);

            // The mote BODY root: an empty transform the shape parts hang under, so the
            // whole silhouette bobs and spins as ONE piece. This is both _moteVisual
            // (hidden on collect) and _moteSpin (animated) — exactly the roles the bare
            // sphere used to play, so Configure/AnimateMote/ApplyCollectedVisual are
            // untouched. Overall footprint is kept to roughly the old 0.4-scale sphere.
            var mote = new GameObject("Mote");
            mote.transform.SetParent(root.transform, false);
            mote.transform.localPosition = new Vector3(0f, 0.6f, 0f);

            BuildMoteShape(mote.transform, glyph, tint);

            var pickup = root.AddComponent<IngredientPickup>();
            pickup._moteVisual = mote;
            pickup._moteSpin = mote.transform;
            pickup.Configure(def, inventory, hero);
            return pickup;
        }

        // ── Mote shape families (WO-1132 d5) ─────────────────────────────────

        /// <summary>
        /// Hangs the shape parts for <paramref name="glyph"/> under <paramref name="body"/>.
        /// One case per family; anything unknown falls through to the original sphere.
        /// </summary>
        private static void BuildMoteShape(Transform body, string glyph, Color tint)
        {
            char g = string.IsNullOrEmpty(glyph) ? '\0' : glyph[0];
            switch (g)
            {
                case '|':   // dry-reed — a tall thin stalk.
                    MotePart(body, "Stalk", PrimitiveType.Cylinder, tint,
                        new Vector3(0f, 0f, 0f), new Vector3(0.07f, 0.26f, 0.07f));
                    MotePart(body, "Node", PrimitiveType.Cylinder, tint,
                        new Vector3(0f, 0.06f, 0f), new Vector3(0.12f, 0.02f, 0.12f));
                    break;

                case '~':   // oil-soaked-cloth / spring water — a tapering droplet.
                    MotePart(body, "Belly", PrimitiveType.Sphere, tint,
                        new Vector3(0f, -0.06f, 0f), new Vector3(0.32f, 0.30f, 0.32f));
                    MotePart(body, "Taper", PrimitiveType.Sphere, tint,
                        new Vector3(0f, 0.12f, 0f), new Vector3(0.17f, 0.17f, 0.17f));
                    MotePart(body, "Tip", PrimitiveType.Sphere, tint,
                        new Vector3(0f, 0.22f, 0f), new Vector3(0.08f, 0.08f, 0.08f));
                    break;

                case '*':   // blooms + resin + herb — a small radiating shard cluster.
                {
                    MotePart(body, "Core", PrimitiveType.Sphere, tint,
                        Vector3.zero, new Vector3(0.15f, 0.15f, 0.15f));
                    // Four angled shards: the spiky silhouette is what reads as "*".
                    for (int i = 0; i < 4; i++)
                    {
                        float yaw = i * 90f;
                        var shard = MotePart(body, $"Shard{i}", PrimitiveType.Cube, tint,
                            Vector3.zero, new Vector3(0.05f, 0.24f, 0.05f));
                        shard.transform.localRotation = Quaternion.Euler(0f, yaw, 52f);
                        shard.transform.localPosition =
                            shard.transform.localRotation * new Vector3(0f, 0.13f, 0f);
                    }
                    break;
                }

                case '=':   // cloth scrap — a flat folded slab, two offset leaves.
                    MotePart(body, "LeafLower", PrimitiveType.Cube, tint,
                        new Vector3(0f, -0.05f, 0.03f), new Vector3(0.40f, 0.05f, 0.26f));
                    MotePart(body, "LeafUpper", PrimitiveType.Cube, tint,
                        new Vector3(0f, 0.02f, -0.04f), new Vector3(0.34f, 0.05f, 0.22f));
                    break;

                case 'T':   // shadowcap / quickfoot — a mushroom: stalk plus domed cap.
                    MotePart(body, "Stalk", PrimitiveType.Cylinder, tint,
                        new Vector3(0f, -0.11f, 0f), new Vector3(0.10f, 0.11f, 0.10f));
                    MotePart(body, "Cap", PrimitiveType.Sphere, tint,
                        new Vector3(0f, 0.06f, 0f), new Vector3(0.34f, 0.20f, 0.34f));
                    break;

                case 'Y':   // ironroot — a forked root.
                    MotePart(body, "Taproot", PrimitiveType.Cylinder, tint,
                        new Vector3(0f, -0.12f, 0f), new Vector3(0.08f, 0.12f, 0.08f));
                    MoteFork(body, "ForkA", tint, 34f);
                    MoteFork(body, "ForkB", tint, -34f);
                    break;

                case 'b':   // oil flask — a round belly with a narrow neck and a stopper.
                    MotePart(body, "Belly", PrimitiveType.Sphere, tint,
                        new Vector3(0f, -0.06f, 0f), new Vector3(0.30f, 0.28f, 0.30f));
                    MotePart(body, "Neck", PrimitiveType.Cylinder, tint,
                        new Vector3(0f, 0.13f, 0f), new Vector3(0.10f, 0.10f, 0.10f));
                    MotePart(body, "Stopper", PrimitiveType.Cube, tint,
                        new Vector3(0f, 0.24f, 0f), new Vector3(0.13f, 0.06f, 0.13f));
                    break;

                default:    // no authored glyph — the original sphere, unchanged.
                    MotePart(body, "Sphere", PrimitiveType.Sphere, tint,
                        Vector3.zero, Vector3.one * 0.4f);
                    break;
            }
        }

        /// <summary>One angled prong of the 'Y' forked-root silhouette.</summary>
        private static void MoteFork(Transform body, string name, Color tint, float roll)
        {
            var fork = MotePart(body, name, PrimitiveType.Cylinder, tint,
                Vector3.zero, new Vector3(0.07f, 0.14f, 0.07f));
            fork.transform.localRotation = Quaternion.Euler(0f, 0f, roll);
            fork.transform.localPosition =
                fork.transform.localRotation * new Vector3(0f, 0.14f, 0f);
        }

        /// <summary>
        /// One URP-safe, emissive, collider-free primitive of a mote body.
        /// <para>
        /// THREE things here are deliberate and must not be "cleaned up":
        /// (1) the primitive's collider is DESTROYED — pickup is a per-frame distance
        /// check, never a physics event, so a live collider would only block the hero
        /// or punch a hole in the NavMesh;
        /// (2) the material is built from URP/Lit with fallbacks, because
        /// CreatePrimitive ships the built-in Standard shader which URP renders
        /// MAGENTA (the "pink floor" lesson);
        /// (3) it is EMISSIVE — a lit-only pastel is invisible at low lantern oil.
        /// </para>
        /// </summary>
        private static GameObject MotePart(Transform body, string name, PrimitiveType type,
            Color tint, Vector3 localPos, Vector3 localScale, float emissiveMul = 0.75f)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(body, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;

            // (1) Distance-check pickup — the mote must never block hero or NavMesh.
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                // (2) URP-safe material construction — never leave the Standard shader on.
                var shader = Shader.Find("Universal Render Pipeline/Lit")
                             ?? Shader.Find("Universal Render Pipeline/Unlit")
                             ?? Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    var mat = new Material(shader);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint);
                    // (3) Emissive so the mote reads in an unlit dungeon.
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    if (mat.HasProperty("_EmissionColor"))
                        mat.SetColor("_EmissionColor", tint * emissiveMul);
                    rend.material = mat;
                }
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return go;
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
