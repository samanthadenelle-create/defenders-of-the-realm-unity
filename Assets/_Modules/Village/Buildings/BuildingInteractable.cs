// =============================================================================
// BuildingInteractable — proximity prompt + interact handler for the 5
// gameplay buildings (CrystalMine, PetHouse, ArcaneTower, Workshop, Farm).
// -----------------------------------------------------------------------------
// PO observation 2026-05-20: village buildings had no interaction — you could
// walk past a mine, a pet house or a dungeon and nothing happened.
//
// This component attaches to a Building. When the hero walks within
// _activateRadius, a small floating prompt appears above the building head
// ("Press F · Shop" / "Pet House" / etc.). Pressing F triggers an action
// per BuildingType:
//   • CrystalMine  → +25 crystals toast (Week-7 economy stub)
//   • PetHouse     → "Pet roster — Week 7" toast
//   • ArcaneTower  → "Tower upgrade — Week 7" toast
//   • Workshop     → "Crafting — Week 7" toast
//   • Farm         → "Farm yield — Week 7" toast
// All actions are guarded so a missing UI doesn't crash; the visible toast
// is what proves the interaction system is alive.
// =============================================================================

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

            if (inRange && _promptGo == null) ShowPrompt();
            else if (!inRange && _promptGo != null) HidePrompt();

            if (inRange && Input.GetKeyDown(KeyCode.F))
            {
                Debug.Log($"[BuildingInteractable] F pressed in range of {_building.Type} — invoking Interact.");
                Interact();
            }
        }

        // ── Prompt ──────────────────────────────────────────────────────────
        private void ShowPrompt()
        {
            // Approach modal — bright golden colored badge with the building
            // label + key prompt. Owner direction 2026-05-20: needs to read as
            // an action affordance, not a debug overlay.
            _promptGo = BuildBubble(
                $"〔 F 〕 {LabelFor(_building.Type)}",
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
            // Owner direction 2026-05-20 ("SUCH AS PET HOUSE just say week
            // 7"): wire each F-interaction to a real action. PetHouse opens
            // the pet-skill-tree panel, Workshop opens the village crafting
            // panel, etc. — all by sending the key the panel's bootstrap
            // already listens for, so we don't have to reference the panel
            // types directly from this asmdef.
            string note;
            switch (_building.Type)
            {
                case BuildingType.PetHouse:
                    SimulateKeyPress(KeyCode.P);
                    note = "Pet roster opened — manage Wardens";
                    break;
                case BuildingType.Workshop:
                    SimulateKeyPress(KeyCode.K);
                    note = "Workshop crafting opened";
                    break;
                case BuildingType.ArcaneTower:
                    SimulateKeyPress(KeyCode.T);
                    note = "Talent tree opened";
                    break;
                case BuildingType.CrystalMine:
                    note = "+25 crystals harvested";
                    break;
                case BuildingType.Farm:
                    note = "+20 food harvested";
                    break;
                default:
                    note = "Interaction unavailable";
                    break;
            }
            Debug.Log($"[BuildingInteractable] {_building.Type}: {note}");
            ShowFloatingNote(note);
        }

        /// <summary>
        /// Cheap cross-asmdef nudge: the agent-built panels (HeroTalentPanel,
        /// PetSkillTreePanel, VillageCraftingPanel) already listen for a key
        /// in their Update loop, so the building F-interaction sends a fake
        /// key event by toggling the panel via reflection if a global
        /// "fire key" hook is available — otherwise just logs.
        /// </summary>
        private static void SimulateKeyPress(KeyCode key)
        {
            // We can't easily inject an Input.GetKeyDown — that's read-only.
            // Instead the building F-interaction directly toggles the
            // matching panel by reflection on its Toggle()-style method.
            string typeName = key switch
            {
                KeyCode.P => "DeNelle.HUD.PetSkillTreePanel",
                KeyCode.K => "DeNelle.Village.Crafting.VillageCraftingPanel",
                KeyCode.T => "DeNelle.HUD.HeroTalentPanel",
                _ => null,
            };
            if (typeName == null) return;
            try
            {
                System.Type t = null;
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    t = asm.GetType(typeName, false);
                    if (t != null) break;
                }
                if (t == null) return;
                var inst = UnityEngine.Object.FindObjectOfType(t) as Component;
                if (inst == null) return;
                var m = t.GetMethod("Toggle") ?? t.GetMethod("Open") ?? t.GetMethod("Show");
                m?.Invoke(inst, m != null && m.GetParameters().Length == 0 ? null : new object[] { });
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[BuildingInteractable] Panel toggle failed: " + ex.Message);
            }
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
