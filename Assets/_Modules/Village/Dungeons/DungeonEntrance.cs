// =============================================================================
// DungeonEntrance — WO-19: the in-world "press F to enter" surface for a dungeon.
// Mirrors GateProximityOpener (WO-08): hero identity = HeroLocomotion component
// (not tags/layers); proximity via a trigger; per-instance accent via
// MaterialPropertyBlock (never mutates the shared base-prefab material).
// =============================================================================

using DeNelle.Core;
using UnityEngine;

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

            // DEF-94: the doorway must read as an arcane-violet PORTAL, not a flat
            // pastel AccentColor frame. AccentColor stays the per-dungeon identity cue,
            // but it is mixed into the canonical violet base (and a dim emissive accent)
            // rather than painting the whole doorway its raw colour. WO-272's additive
            // glow layers on top of this base.
            Color archBase     = PortalVFXController.ArchBaseColor(_def.AccentColor);
            Color archEmission = PortalVFXController.ArchEmissionColor(_def.AccentColor);
            _mpb ??= new MaterialPropertyBlock();
            foreach (var r in _accentRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, archBase);
                _mpb.SetColor(EmissionColorId, archEmission);
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
            // Build mode: authoring, not interacting — release the button, hide the
            // world prompt, and skip the [F] press. Restored on build exit.
            if (MobileInteractButton.Suppressed)
            {
                MobileInteractButton.Release(this);
                if (_prompt != null) _prompt.gameObject.SetActive(false);
                return;
            }

            if (!_heroInRange || _def == null)
            {
                MobileInteractButton.Release(this);
                return;
            }

            // DEF-203: register the shared on-screen Interact button while in range so
            // touch/mobile (no keyboard) can enter the dungeon too. Desktop F unchanged.
            MobileInteractButton.Request(this, "Enter: " + _def.ResolveName(), EnterDungeon);

            // DEF-217: the shared button is the single canonical prompt — suppress the
            // redundant world-space TextMesh prompt while the button is showing.
            if (_prompt != null)
                _prompt.gameObject.SetActive(!MobileInteractButton.IsActive);

            // Mobile-first: entering fires through the shared on-screen Interact button
            // (requested above). The desktop F-key trigger was removed.
        }

        private void OnDisable()
        {
            MobileInteractButton.Release(this);
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
            _prompt.text = show ? $"Tap — {_def?.ResolveName()}" : string.Empty;
            _prompt.gameObject.SetActive(show);
        }
    }
}
