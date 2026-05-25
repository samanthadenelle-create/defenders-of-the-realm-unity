// =============================================================================
// DungeonEntrance — WO-19: the in-world "press F to enter" surface for a dungeon.
// Mirrors GateProximityOpener (WO-08): hero identity = HeroLocomotion component
// (not tags/layers); proximity via a trigger; per-instance accent via
// MaterialPropertyBlock (never mutates the shared base-prefab material).
// =============================================================================

using DeNelle.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeNelle.Village
{
    /// <summary>
    /// Drives one dungeon entrance from its assigned <see cref="DungeonDef"/>:
    /// applies the accent colour, shows a "Press F to enter" prompt while the
    /// hero is inside the trigger, and routes to the dungeon scene on F.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DungeonEntrance : MonoBehaviour
    {
        [SerializeField] private DungeonDef _def;
        [Tooltip("World-space TextMesh shown while the hero is in range. Optional.")]
        [SerializeField] private TextMesh _prompt;
        [Tooltip("Renderers tinted with the def's AccentColor (emissive). Auto-filled from children when empty.")]
        [SerializeField] private Renderer[] _accentRenderers;

        private bool _heroInRange;
        private MaterialPropertyBlock _mpb;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>The dungeon this entrance leads to.</summary>
        public DungeonDef Def => _def;

        /// <summary>True while the hero is inside the entrance trigger.</summary>
        public bool IsHeroInRange => _heroInRange;

        /// <summary>Assigns the dungeon at runtime (the placement bootstrap calls this).</summary>
        public void Configure(DungeonDef def)
        {
            _def = def;
            ApplyDef();
        }

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            if (_prompt == null) _prompt = GetComponentInChildren<TextMesh>(true);
            if (_accentRenderers == null || _accentRenderers.Length == 0)
                _accentRenderers = CollectAccentRenderers();
            ApplyDef();
        }

        // Every child Renderer except the prompt's text renderer (so the accent
        // tint colours the doorway, not the "Press F" label).
        private Renderer[] CollectAccentRenderers()
        {
            var all = GetComponentsInChildren<Renderer>(true);
            var list = new System.Collections.Generic.List<Renderer>(all.Length);
            foreach (var r in all)
                if (r != null && r.GetComponent<TextMesh>() == null) list.Add(r);
            return list.ToArray();
        }

        private void ApplyDef()
        {
            if (_prompt != null)
            {
                _prompt.text = string.Empty;
                _prompt.gameObject.SetActive(false);
            }
            if (_def == null || _accentRenderers == null) return;

            _mpb ??= new MaterialPropertyBlock();
            foreach (var r in _accentRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, _def.AccentColor);
                _mpb.SetColor(EmissionColorId, _def.AccentColor);
                r.SetPropertyBlock(_mpb);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null || other.GetComponentInParent<HeroLocomotion>() == null) return;
            _heroInRange = true;
            ShowPrompt(true);
        }

        private void OnTriggerExit(Collider other)
        {
            if (other == null || other.GetComponentInParent<HeroLocomotion>() == null) return;
            _heroInRange = false;
            ShowPrompt(false);
        }

        private void Update()
        {
            if (!_heroInRange || _def == null) return;

            bool fPressed =
                (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) ||
                UnityEngine.Input.GetKeyDown(KeyCode.F);

            if (fPressed) EnterDungeon();
        }

        private void EnterDungeon()
        {
            if (_def == null || string.IsNullOrEmpty(_def.SceneName)) return;
            if (!_def.SceneExists)
            {
                Debug.LogWarning($"[DungeonEntrance] '{_def.SceneName}' scene not built yet — entry is a no-op (scaffold).");
                return;
            }
            Debug.Log($"[DungeonEntrance] Entering '{_def.SceneName}'.");
            // SceneRouter.LoadScene logs + aborts if the scene isn't in Build Settings.
            SceneRouter.LoadScene(_def.SceneName);
        }

        private void ShowPrompt(bool show)
        {
            if (_prompt == null) return;
            _prompt.text = show ? $"Press F — {_def?.ResolveName()}" : string.Empty;
            _prompt.gameObject.SetActive(show);
        }
    }
}
