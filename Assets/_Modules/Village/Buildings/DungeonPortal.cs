// =============================================================================
// DungeonPortal — proximity entrance to Dungeon_HealersCottage from the village.
// -----------------------------------------------------------------------------
// Owner ask 2026-05-20: "make sure dungeon is connected and playtest what you
// can". Village had no in-world hook into the dungeon scene; only the DevPanel
// "Jump → Dungeon" debug button could route there. This adds a real portal:
// a glowing stone arch placed near the village edge, with a Press-F prompt
// that calls SceneRouter.GoDungeon("Dungeon_HealersCottage").
//
// Visual is a placeholder primitive arch (two cube uprights + a plank lintel +
// a translucent purple sheet that pulses) so it reads as a portal without
// needing a KayKit asset. The interaction logic is what matters.
// =============================================================================

using Cysharp.Threading.Tasks;
using DeNelle.Core;
using UnityEngine;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    public sealed class DungeonPortal : MonoBehaviour
    {
        private const float ActivateRadius = 5.5f;
        private const float PromptHeight = 4.4f;

        [SerializeField] private string _dungeonId = "Dungeon_HealersCottage";
        [SerializeField] private string _displayName = "Healer's Cottage";

        public void Configure(string dungeonId, string displayName)
        {
            _dungeonId = dungeonId;
            _displayName = string.IsNullOrEmpty(displayName) ? dungeonId : displayName;
        }

        private Transform _hero;
        private GameObject _promptGo;
        private Renderer _shimmer;
        private float _t;
        private bool _loading;

        private void Start() => ResolveHero();

        private void ResolveHero()
        {
            var hero = UnityEngine.Object.FindObjectOfType<HeroLocomotion>();
            if (hero != null) _hero = hero.transform;
        }

        private void Update()
        {
            // Slow pulse on the shimmer sheet so the portal reads as live.
            if (_shimmer != null)
            {
                _t += Time.deltaTime;
                float pulse = 0.55f + Mathf.Sin(_t * 2.0f) * 0.18f;
                if (_shimmer.sharedMaterial != null && _shimmer.sharedMaterial.HasProperty("_BaseColor"))
                {
                    var c = _shimmer.sharedMaterial.GetColor("_BaseColor");
                    c.a = pulse;
                    _shimmer.sharedMaterial.SetColor("_BaseColor", c);
                }
            }

            if (_hero == null) { ResolveHero(); return; }
            if (_loading) return;

            float distSqr = (_hero.position - transform.position).sqrMagnitude;
            bool inRange = distSqr <= ActivateRadius * ActivateRadius;

            if (inRange && _promptGo == null) ShowPrompt();
            else if (!inRange && _promptGo != null) HidePrompt();

            if (inRange && Input.GetKeyDown(KeyCode.F))
                EnterDungeon();
        }

        /// <summary>
        /// Owner ask 2026-05-20 ("is trigger firing to go to healer
        /// cottage?"): make the BoxCollider trigger DO something — walking
        /// into the portal routes straight to the dungeon. Removes the F
        /// step entirely for players who prefer movement-only interaction.
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            if (_loading) return;
            if (other == null) return;
            // Only the hero triggers — pets are kinematic and would otherwise
            // route the player to the dungeon while orbiting.
            var hero = other.GetComponentInParent<HeroLocomotion>();
            if (hero == null) return;
            Debug.Log("[DungeonPortal] Trigger entered by hero — routing to " + _dungeonId);
            EnterDungeon();
        }

        public void BindShimmer(Renderer r) => _shimmer = r;

        private void ShowPrompt()
        {
            _promptGo = BuildBubble(
                "〔 F 〕 " + _displayName,
                PromptHeight,
                new Color(0.10f, 0.04f, 0.20f, 0.96f),
                new Color(0.78f, 0.55f, 1f, 1f));
        }

        private void HidePrompt()
        {
            if (_promptGo != null) UnityEngine.Object.Destroy(_promptGo);
            _promptGo = null;
        }

        private void EnterDungeon()
        {
            _loading = true;
            HidePrompt();
            Debug.Log("[DungeonPortal] Entering dungeon " + _dungeonId);
            try
            {
                SceneRouter.GoDungeon(_dungeonId).Forget();
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[DungeonPortal] GoDungeon threw: " + ex);
                _loading = false;
            }
        }

        // ── Reuses BuildingInteractable's bubble look for visual consistency. ──
        private GameObject BuildBubble(string text, float localY, Color bgColor, Color outlineColor)
        {
            var go = new GameObject("PortalPrompt");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * localY;

            float charsApprox = Mathf.Max(text.Length, 8);
            float w = Mathf.Clamp(charsApprox * 0.10f + 0.4f, 1.0f, 3.4f);
            float h = 0.38f;

            var outline = GameObject.CreatePrimitive(PrimitiveType.Quad);
            outline.name = "Outline";
            DestroyImmediate(outline.GetComponent<Collider>());
            outline.transform.SetParent(go.transform, false);
            outline.transform.localPosition = new Vector3(0f, 0f, 0.012f);
            outline.transform.localScale = new Vector3(w + 0.06f, h + 0.06f, 1f);
            ApplyRounded(outline.GetComponent<Renderer>(), outlineColor, (w + 0.06f) / (h + 0.06f));

            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.name = "Bg";
            DestroyImmediate(bg.GetComponent<Collider>());
            bg.transform.SetParent(go.transform, false);
            bg.transform.localPosition = new Vector3(0f, 0f, 0.006f);
            bg.transform.localScale = new Vector3(w, h, 1f);
            ApplyRounded(bg.GetComponent<Renderer>(), bgColor, w / h);

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(go.transform, false);
            txtGo.transform.localPosition = Vector3.zero;
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

        private static void ApplyRounded(Renderer renderer, Color colour, float aspect)
        {
            if (renderer == null) return;
            Shader rounded = Shader.Find("DeNelle/UI/RoundedChatBubble")
                             ?? Shader.Find("Universal Render Pipeline/Unlit")
                             ?? Shader.Find("Unlit/Color");
            if (rounded == null) return;
            var mat = new Material(rounded);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colour);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color", colour);
            if (mat.HasProperty("_Radius")) mat.SetFloat("_Radius", 0.30f);
            if (mat.HasProperty("_Aspect")) mat.SetFloat("_Aspect", Mathf.Max(0.5f, aspect));
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            renderer.sharedMaterial = mat;
        }
    }
}
