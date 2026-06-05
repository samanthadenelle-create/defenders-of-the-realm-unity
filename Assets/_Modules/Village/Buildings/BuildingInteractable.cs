// =============================================================================
// BuildingInteractable — proximity prompt + interact handler for the 5
// gameplay buildings (CrystalMine, PetHouse, ArcaneTower, Workshop, Farm).
// -----------------------------------------------------------------------------
// PO observation 2026-05-20: village buildings had no interaction — you could
// walk past a mine, a pet house or a dungeon and nothing happened.
//
// This component attaches to a Building. When the hero walks within
// _activateRadius, a small floating prompt appears above the building head.
// Pressing F (or tapping the shared button) opens the ONE panel that building
// owns — building-SPECIFIC routing (DEF-213):
//   • ArcaneTower            → Hero Talents          (PanelId.HeroTalents)
//   • Workshop               → Crafting bench        (PanelId.Crafting)
//   • Forge (Armorer)        → Building Upgrade       (PanelId.BuildingUpgrade)
//   • Farm / Lumbermill /
//     CrystalMine            → Building Upgrade       (PanelId.BuildingUpgrade)
//   • PetHouse               → Pet / Companion tree   (PanelId.PetSkillTree)
// The panel is opened through DeNelle.Core.UI.PanelRouter — no cross-asmdef
// reference, no reflection. Each panel routes its own open through PanelManager
// (DEF-212), so the one-panel-at-a-time rule still holds. A building with no
// panel registered yet shows a clean "coming soon" note instead of a wrong panel.
//
// DEF-213 root cause this replaces: the old code mapped every building through a
// reflection-driven Toggle() of a panel found by FindObjectOfType, and EVERY
// BuildingInteractable in the scene listened for the same global F key. With
// overlapping proximity radii a single F press fired several buildings at once
// (e.g. Arcane Tower's Hero Talents AND a neighbour's Companion panel), and the
// Toggle() semantics meant a second press closed an unrelated panel. We now (a)
// only let the NEAREST in-range building act on F, (b) suppress F while any modal
// panel is open, and (c) open (never toggle) the one correct panel by id.
// =============================================================================

using DeNelle.Core.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Building))]
    public sealed class BuildingInteractable : MonoBehaviour
    {
        private const float ActivateRadius = 6f;
        private const float ProximityHeightAboveBuilding = 3.2f;

        private Building _building;
        private Transform _hero;
        private GameObject _promptGo;
        private TextMesh _promptText;

        private void Awake()
        {
            _building = GetComponent<Building>();
        }

        private void Start()
        {
            ResolveHero();
        }

        private void ResolveHero()
        {
            // Reflection-free direct find — HeroLocomotion lives in this asmdef.
            var hero = UnityEngine.Object.FindObjectOfType<HeroLocomotion>();
            if (hero != null) _hero = hero.transform;
        }

        private void Update()
        {
            if (_hero == null) { ResolveHero(); return; }

            float distSqr = (_hero.position - transform.position).sqrMagnitude;
            bool inRange = distSqr <= ActivateRadius * ActivateRadius;

            // DEF-203: register the shared on-screen Interact button while in range so
            // touch/mobile (no keyboard) can fire the same action. Desktop F unchanged.
            if (inRange)
                MobileInteractButton.Request(this, "Interact: " + LabelFor(_building.Type), Interact);
            else
                MobileInteractButton.Release(this);

            // DEF-217: the shared MobileInteractButton is now the SINGLE canonical
            // interaction prompt. Suppress the world-space bubble whenever the button is
            // showing ANY interact prompt (this building or a higher-priority upgrade /
            // crafting watcher on the same building). The bubble only survives as a
            // fallback if the button host failed to spawn. Kills the "bubble + button"
            // (and "bubble + button + 2nd watcher") triple prompt.
            bool buttonActive = MobileInteractButton.IsActive;
            if (inRange && !buttonActive && _promptGo == null) ShowPrompt();
            else if ((!inRange || buttonActive) && _promptGo != null) HidePrompt();

            // DEF-213: F is a SINGLE global key that every BuildingInteractable polls.
            // Only the building NEAREST the hero (and only when no modal is already
            // open) acts on the press, so two buildings with overlapping radii can't
            // both open a panel on one keystroke. The shared touch button already
            // routes to exactly one owner per frame, so this guard is desktop-F only.
            if (inRange && !PanelManager.AnyOpen && Input.GetKeyDown(KeyCode.F) && IsNearestInRange())
            {
                Debug.Log($"[BuildingInteractable] F pressed nearest to {_building.Type} — invoking Interact.");
                Interact();
            }
        }

        /// <summary>
        /// True when THIS building is the closest in-range BuildingInteractable to the
        /// hero. Resolves the multi-fire on a shared F key when proximity radii overlap.
        /// </summary>
        private bool IsNearestInRange()
        {
            if (_hero == null) return false;
            float myDistSqr = (_hero.position - transform.position).sqrMagnitude;
            foreach (var other in UnityEngine.Object.FindObjectsByType<BuildingInteractable>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (other == null || other == this) continue;
                float otherDistSqr = (_hero.position - other.transform.position).sqrMagnitude;
                if (otherDistSqr > ActivateRadius * ActivateRadius) continue; // not in range
                if (otherDistSqr < myDistSqr) return false;                    // someone closer
            }
            return true;
        }

        private void OnDisable()
        {
            MobileInteractButton.Release(this);
        }

        // ── Prompt ──────────────────────────────────────────────────────────
        private void ShowPrompt()
        {
            // Approach modal — bright golden colored badge with the building
            // label + key prompt. Owner direction 2026-05-20: needs to read as
            // an action affordance, not a debug overlay.
            _promptGo = BuildBubble(
                $"〔 Tap / F 〕 {LabelFor(_building.Type)}",
                ProximityHeightAboveBuilding,
                new Color(0.18f, 0.10f, 0.04f, 0.96f),     // deep amber-black
                new Color(1f, 0.78f, 0.32f, 1f));          // bright gold rim
        }

        private void HidePrompt()
        {
            if (_promptGo != null) UnityEngine.Object.Destroy(_promptGo);
            _promptGo = null;
        }

        // ── Action dispatch ─────────────────────────────────────────────────
        private void Interact()
        {
            // PARAMETERIZED BUILDING HOOK: the economy buildings (farm/lumbermill/forge/
            // market/pet-house) open the ONE shared Yarn dialogue (portrait + Buy/Sell/
            // Upgrade/Talk), scoped to their own domain by $structureId. One node, the
            // parameter differs. Falls through to the legacy panels for the rest
            // (ArcaneTower → Hero Talents, Workshop → Crafting).
            string hookId = StructureHookIdFor(_building);
            if (hookId != null && DialogueService.PlayStructure(hookId))
            {
                Debug.Log($"[BuildingInteractable] {_building.Type} → structure dialogue '{hookId}'.");
                return;
            }

            // DEF-213: open the ONE panel this specific building owns, by id, through
            // the reflection-free PanelRouter. Each panel's registered open action
            // routes through PanelManager, so opening it closes any other panel.
            if (TryPanelFor(_building, out PanelId panelId))
            {
                // DEF-186: for the shared Building Upgrade panel, pass the SPECIFIC
                // building's id as context so the panel opens focused on the building
                // the player actually tapped (e.g. the Lumbermill) instead of a generic
                // list. Other panels ignore context (plain-Open fallback in PanelRouter).
                string context = ContextIdFor(_building);
                bool opened = string.IsNullOrEmpty(context)
                    ? PanelRouter.Open(panelId)
                    : PanelRouter.Open(panelId, context);
                if (opened)
                {
                    Debug.Log($"[BuildingInteractable] {_building.Type} → opened {panelId} (focus='{context}').");
                    return;
                }

                // The right panel exists in canon but isn't registered yet (e.g. its
                // bootstrap hasn't spawned). Show a clean note rather than a wrong panel.
                Debug.Log($"[BuildingInteractable] {_building.Type} → {panelId} not ready.");
                ShowFloatingNote($"{LabelFor(_building.Type)} — coming soon");
                return;
            }

            // No panel mapped for this building: clean name tooltip, never a wrong panel.
            Debug.Log($"[BuildingInteractable] {_building.Type} has no panel — name tooltip only.");
            ShowFloatingNote($"{LabelFor(_building.Type)} — coming soon");
        }

        /// <summary>
        /// Maps a building to the ONE panel it opens (DEF-213 canon). Returns false
        /// for buildings that have no panel yet (the caller shows a "coming soon"
        /// note). Matches the upgrade buildings by BuildingType first, then by the
        /// id-only resource buildings (Lumbermill / Forge have no enum value).
        /// </summary>
        // The structure id for buildings that use the parameterized Yarn hook (the
        // economy buildings that have a Portraits/<id> NPC), else null → legacy panel.
        private static string StructureHookIdFor(Building building)
        {
            if (building == null) return null;
            string id = (building.BuildingId ?? "").ToLowerInvariant();
            if (id.Length > 0)
            {
                if (id.Contains("lumbermill")) return "lumbermill";
                if (id == "forge" || id.Contains("armorer")) return "forge";
                if (id.Contains("farm")) return "farm";
                if (id.Contains("market")) return "market";
                if (id.Contains("pet")) return "pet-house";
            }
            switch (building.Type)   // buildings authored without an explicit id
            {
                case BuildingType.Farm: return "farm";
                case BuildingType.Lumbermill: return "lumbermill";
                case BuildingType.Forge: return "forge";
            }
            return null;
        }

        private static bool TryPanelFor(Building building, out PanelId panelId)
        {
            panelId = default;
            if (building == null) return false;

            switch (building.Type)
            {
                case BuildingType.ArcaneTower:
                    panelId = PanelId.HeroTalents;
                    return true;
                case BuildingType.Workshop:
                    panelId = PanelId.Crafting;
                    return true;
                case BuildingType.PetHouse:
                    panelId = PanelId.PetSkillTree;
                    return true;
                // Resource + Armorer buildings all share the Building Upgrade panel.
                case BuildingType.CrystalMine:
                case BuildingType.Farm:
                case BuildingType.Lumbermill:
                case BuildingType.Forge:
                    panelId = PanelId.BuildingUpgrade;
                    return true;
            }

            // Id-keyed resource buildings (Lumbermill / Forge may carry the enum value
            // OR be authored as the default type with only an id set) → Upgrade panel.
            string id = building.BuildingId;
            if (!string.IsNullOrEmpty(id))
            {
                string lower = id.ToLowerInvariant();
                if (lower.Contains("lumbermill") || lower.Contains("forge") ||
                    lower.Contains("armorer") || lower.Contains("farm") || lower.Contains("mine"))
                {
                    panelId = PanelId.BuildingUpgrade;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Resolves the FOCUS subject id for the upgrade panel (DEF-186): the resource
        /// building progression id (farm / lumbermill / forge) the panel should scroll
        /// to / highlight. Prefers the explicit Building.BuildingId, then maps the
        /// BuildingType for the resource buildings, else null (no focus — generic open).
        /// </summary>
        private static string ContextIdFor(Building building)
        {
            if (building == null) return null;

            string id = building.BuildingId;
            if (!string.IsNullOrEmpty(id))
            {
                string lower = id.ToLowerInvariant();
                if (lower.Contains("lumbermill")) return Buildings.Progression.ResourceBuildingProgression.LumbermillId;
                if (lower.Contains("forge") || lower.Contains("armorer")) return Buildings.Progression.ResourceBuildingProgression.ForgeId;
                if (lower.Contains("farm")) return Buildings.Progression.ResourceBuildingProgression.FarmId;
                // Pass any other explicit id through verbatim (panel resolves/ignores it).
                if (Buildings.Progression.ResourceBuildingProgression.IsResourceBuilding(id)) return id;
            }

            switch (building.Type)
            {
                case BuildingType.Lumbermill: return Buildings.Progression.ResourceBuildingProgression.LumbermillId;
                case BuildingType.Forge:      return Buildings.Progression.ResourceBuildingProgression.ForgeId;
                case BuildingType.Farm:       return Buildings.Progression.ResourceBuildingProgression.FarmId;
            }
            return null; // CrystalMine / non-resource: no focus, generic open.
        }

        private void ShowFloatingNote(string text)
        {
            var note = BuildBubble(
                text,
                ProximityHeightAboveBuilding + 0.7f,
                new Color(0.08f, 0.05f, 0.13f, 0.94f),
                new Color(0.55f, 0.85f, 1f, 0.85f));
            UnityEngine.Object.Destroy(note, 2.5f);
        }

        /// <summary>
        /// Builds a polished mini chat-bubble (backdrop quad + outline + text)
        /// for the prompt / toast. Owner direction 2026-05-20: bare TextMesh
        /// floated like debug overlay — needed a real bubble shape.
        /// </summary>
        private GameObject BuildBubble(string text, float localY, Color bgColor, Color outlineColor)
        {
            var go = new GameObject("Bubble");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * localY;

            // Estimate panel size from text length so short labels don't get a
            // huge empty card and long ones don't overflow.
            float charsApprox = Mathf.Max(text.Length, 8);
            float w = Mathf.Clamp(charsApprox * 0.10f + 0.4f, 1.0f, 3.2f);
            float h = 0.36f;

            // Outline (slightly larger).
            var outline = GameObject.CreatePrimitive(PrimitiveType.Quad);
            outline.name = "Outline";
            DestroyImmediate(outline.GetComponent<Collider>());
            outline.transform.SetParent(go.transform, false);
            outline.transform.localPosition = new Vector3(0f, 0f, 0.012f);
            outline.transform.localScale = new Vector3(w + 0.06f, h + 0.06f, 1f);
            ApplyRounded(outline.GetComponent<Renderer>(), outlineColor, (w + 0.06f) / (h + 0.06f));

            // Fill backdrop.
            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.name = "Bg";
            DestroyImmediate(bg.GetComponent<Collider>());
            bg.transform.SetParent(go.transform, false);
            bg.transform.localPosition = new Vector3(0f, 0f, 0.006f);
            bg.transform.localScale = new Vector3(w, h, 1f);
            ApplyRounded(bg.GetComponent<Renderer>(), bgColor, w / h);

            // Tail — small triangle dropping toward the building.
            var tail = BuildTail(outlineColor, bgColor);
            tail.transform.SetParent(go.transform, false);
            tail.transform.localPosition = new Vector3(0f, -h * 0.5f - 0.07f, 0.006f);

            // Text.
            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(go.transform, false);
            txtGo.transform.localPosition = new Vector3(0f, 0f, 0f);
            txtGo.transform.localScale = Vector3.one * 0.06f;
            var tm = txtGo.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 96;
            tm.characterSize = 0.30f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.97f, 0.95f, 0.88f);

            var billboard = go.AddComponent<PromptBillboard>();
            billboard.Camera = Camera.main;
            return go;
        }

        private static void ApplyFlat(Renderer renderer, Color colour)
        {
            if (renderer == null) return;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Unlit/Color")
                            ?? Shader.Find("Sprites/Default");
            if (shader == null) return;
            var mat = new Material(shader) { color = colour };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colour);
            // Unity 6 URP unlit's transparency knobs.
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // Transparent
            renderer.sharedMaterial = mat;
        }

        private static void ApplyRounded(Renderer renderer, Color colour, float aspect)
        {
            if (renderer == null) return;
            Shader rounded = Shader.Find("DeNelle/UI/RoundedChatBubble");
            if (rounded == null) { ApplyFlat(renderer, colour); return; }
            var mat = new Material(rounded);
            mat.SetColor("_BaseColor", colour);
            if (mat.HasProperty("_Radius")) mat.SetFloat("_Radius", 0.30f);
            if (mat.HasProperty("_Aspect")) mat.SetFloat("_Aspect", Mathf.Max(0.5f, aspect));
            renderer.sharedMaterial = mat;
        }

        /// <summary>
        /// Builds a small triangle that points downward toward the speaker
        /// (the building), matching the bubble's outline + fill colours.
        /// </summary>
        private static GameObject BuildTail(Color outline, Color fill)
        {
            var root = new GameObject("Tail");

            var outlineGo = MakeTriangle(0.32f, 0.34f, outline);
            outlineGo.transform.SetParent(root.transform, false);
            outlineGo.transform.localPosition = new Vector3(0f, 0f, 0.001f);

            var fillGo = MakeTriangle(0.24f, 0.26f, fill);
            fillGo.transform.SetParent(root.transform, false);
            fillGo.transform.localPosition = new Vector3(0f, 0.02f, 0f);

            return root;
        }

        private static GameObject MakeTriangle(float width, float height, Color colour)
        {
            var go = new GameObject("Tri");
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    new Vector3(-width * 0.5f,  height * 0.5f, 0f),
                    new Vector3( width * 0.5f,  height * 0.5f, 0f),
                    new Vector3( 0f,           -height * 0.5f, 0f),
                },
                triangles = new[] { 0, 1, 2 },
                uv = new[] { new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 0) },
            };
            mesh.RecalculateNormals();
            mf.sharedMesh = mesh;
            ApplyFlat(mr, colour);
            return go;
        }

        private static string LabelFor(BuildingType t) => t switch
        {
            BuildingType.CrystalMine => "Mine",
            BuildingType.PetHouse    => "Pet House",
            BuildingType.ArcaneTower => "Tower",
            BuildingType.Workshop    => "Workshop",
            BuildingType.Farm        => "Farm",
            _ => "Building",
        };
    }

    /// <summary>Keeps a world-space text element facing the camera.</summary>
    [DisallowMultipleComponent]
    internal sealed class PromptBillboard : MonoBehaviour
    {
        public Camera Camera;
        private void LateUpdate()
        {
            if (Camera == null) Camera = Camera.main;
            if (Camera == null) return;
            transform.rotation = Quaternion.LookRotation(transform.position - Camera.transform.position);
        }
    }
}
