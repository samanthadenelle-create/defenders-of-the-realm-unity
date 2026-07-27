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
        // DEF-26 (2026-05-27): tightened from 5.5 → 3.0 m so the [F] prompt
        // only fires when the hero is at the portal arch entrance. 5.5 m was
        // activating from ~2 m before the portal disc edge, which looked like
        // the prompt was firing in the open field when the hero was still clearly
        // approaching. 3.0 m matches ~the disc radius (3.5 m) and the "~2–3 m
        // from the door" spec in DEF-26.
        private const float ActivateRadius = 3.0f;
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
        private PortalVFXController _portalVfx;

        // DEF-40: perf — throttle distance checks; cache in-range state so
        // GetKeyDown can still fire every frame without regression.
        private bool _heroFound;
        private bool _isInRange;
        private float _nextProximityCheck;
        private const float CheckInterval = 0.15f;

        private void Start()
        {
            ResolveHero();
            // DEF-100: the controller is self-sufficient (builds its own glow/light/
            // vortex in code) — ensure one exists even if the portal wasn't authored
            // with it, so the interior glow always renders + reacts to the hero.
            _portalVfx = GetComponent<PortalVFXController>();
            if (_portalVfx == null) _portalVfx = gameObject.AddComponent<PortalVFXController>();
        }

        private void ResolveHero()
        {
            if (_heroFound) return;
            var hero = UnityEngine.Object.FindAnyObjectByType<HeroLocomotion>();
            if (hero != null) { _hero = hero.transform; _heroFound = true; }
        }

        private void Update()
        {
            // Shimmer pulse — gated to in-range so distant portals don't burn
            // a SetColor + material property lookup every frame.
            if (_shimmer != null && _isInRange)
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

            if (!_heroFound) { ResolveHero(); return; }
            if (_loading) return;

            // The cached hero ref can go null if the village rebuilds/replaces the
            // hero rig after we first found it. Re-resolve rather than dereferencing
            // a destroyed Transform — otherwise the line below NREs EVERY FRAME and
            // the exception/stack-trace spam tanks the framerate (reads to the player
            // as "frozen, can't move"). DEF-40 regression: the _heroFound cache
            // removed the per-frame re-resolve that used to mask this.
            if (_hero == null) { _heroFound = false; return; }

            // Build mode: authoring, not interacting — release the button, hide the
            // bubble, skip the [F] press. Restored automatically on build exit.
            if (MobileInteractButton.Suppressed)
            {
                MobileInteractButton.Release(this);
                if (_promptGo != null) HidePrompt();
                return;
            }

            // Throttled proximity check (0.15 s) — prompt show/hide.
            if (Time.time >= _nextProximityCheck)
            {
                _nextProximityCheck = Time.time + CheckInterval;
                float distSqr = (_hero.position - transform.position).sqrMagnitude;
                bool nowInRange = distSqr <= ActivateRadius * ActivateRadius;
                if (nowInRange != _isInRange)
                {
                    _isInRange = nowInRange;
                    if (_isInRange) ShowPrompt();
                    else           HidePrompt();
                }
            }

            // DEF-203: register the shared on-screen Interact button while in range so
            // touch/mobile (no keyboard) can enter too. Desktop F + walk-in unchanged.
            if (_isInRange)
                MobileInteractButton.Request(this, "Enter: " + _displayName, EnterDungeon);
            else
                MobileInteractButton.Release(this);

            // DEF-217: the shared button is the single canonical prompt — drop the
            // redundant world-space bubble while it is showing.
            if (_promptGo != null && MobileInteractButton.IsActive) HidePrompt();

            // Mobile-first: entering fires ONLY through the shared on-screen Interact
            // button (requested above). WO-777: the walk-into-trigger auto-route was
            // removed (accidental-delve footgun); OnTriggerEnter now only arms VFX.
        }

        private void OnDisable()
        {
            MobileInteractButton.Release(this);
        }

        /// <summary>
        /// WO-777: the trigger no longer AUTO-ROUTES on walk-in. Walking into the
        /// portal used to call EnterDungeon() immediately (an accidental-delve
        /// footgun with no confirm — owner ask 2026-05-20 originally wired it that
        /// way). Now the trigger only ARMS the portal VFX (interior glow reacts to
        /// the approaching hero); the SOLE entry path is the shared Interact button
        /// requested in Update() (MobileInteractButton.Request → EnterDungeon), so
        /// entering is always an explicit tap/[F], never a walk-by.
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            if (other == null) return;
            // Only the hero arms the portal — pets are kinematic and would otherwise
            // trigger the approach VFX while orbiting.
            var hero = other.GetComponentInParent<HeroLocomotion>();
            if (hero == null) return;
            Debug.Log("[DungeonPortal] Trigger entered by hero — arming portal (entry is via the Interact button, no walk-by).");
            _portalVfx?.OnHeroApproach();
        }

        public void BindShimmer(Renderer r) => _shimmer = r;

        private void ShowPrompt()
        {
            _promptGo = BuildBubble(
                "〔 Tap / F 〕 " + _displayName,
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
            if (_loading) return;

            // Resolve the target scene name. Legacy portals pass a bare dungeon id
            // ("HealersCottage") and the scene is "Dungeon_" + id. Composed dungeons
            // (GraphDungeonComposer, e.g. "dg_starter_loop") ship the FULL scene name
            // as the id, so prefer the id verbatim when it is already loadable; only
            // then fall back to the legacy "Dungeon_" prefix form.
            string sceneName = _dungeonId;
            bool loadable = Application.CanStreamedLevelBeLoaded(sceneName);
            if (!loadable)
            {
                string prefixed = "Dungeon_" + _dungeonId;
                if (Application.CanStreamedLevelBeLoaded(prefixed))
                {
                    sceneName = prefixed;
                    loadable = true;
                }
            }

            // DEAD-LATCH FIX: SceneManager.LoadScene does NOT throw on an unregistered
            // scene — it logs + no-ops, so the try/catch never fires, _loading would
            // latch true, and every later tap would be dead. Guard with
            // CanStreamedLevelBeLoaded: self-report via FlowTrace.Fail and stay live
            // (reset _loading) so the portal is never permanently dead on device.
            if (!loadable)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Fail("DungeonPortal",
                    $"scene not loadable id='{_dungeonId}' (tried '{_dungeonId}' and 'Dungeon_{_dungeonId}') " +
                    "- not in Build Settings; portal stays live (no dead-latch).");
                _loading = false;
                return;
            }

            _loading = true;
            HidePrompt();
            Debug.Log("[DungeonPortal] Entering dungeon scene: " + sceneName);
            // Owner 2026-05-20 ("freezes on load dungeon"): the fade-based
            // GoDungeon path nulls its Fader reference when the village scene
            // unloads, then awaits a FadeIn on a destroyed object — appears
            // as a frozen black screen. Use the plain synchronous LoadScene
            // path to side-step the fader entirely.
            try
            {
                _portalVfx?.OnHeroEnter();
                UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
            }
            catch (System.Exception ex)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Fail("DungeonPortal", "LoadScene threw: " + ex);
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
