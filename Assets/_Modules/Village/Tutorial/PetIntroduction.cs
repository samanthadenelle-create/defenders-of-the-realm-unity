// =============================================================================
// PetIntroduction — Scene 6 of the FTUE: name the starter pet + choose its role.
// -----------------------------------------------------------------------------
// "One of the Echoes — it's chosen you." The starter pet (deployed by the
// existing PetDeployer) runs up; the player NAMES it via a text prompt with a
// species-appropriate default, then picks DEFEND or GATHER. We persist the name
// to GameState (WO-277 PetName) and set the live Pet's mode + its harvester
// disposition. Combat/economy systems are USED, never modified — Pet.Mode and
// PetHarvester already drive defend vs gather.
//
// ── UI (code-built, NOT UXML — project-wide rule, PIPELINE_STATE §8) ──────────
//   The name prompt + Defend/Gather choice is a UI Toolkit overlay built entirely
//   in C# on a runtime UIDocument whose PanelSettings is BORROWED from an existing
//   scene UIDocument (the same trick PetSkillTreePanelBootstrap / JupiterSwap use).
//   No PanelSettings → no overlay, and we fall back to auto-naming + Defend so the
//   tutorial still completes (never wedges).
//
// Isolation/safety: lives in DeNelle.Village (references DeNelle.Pets via the
// Village asmdef). Every cross-call null-guarded.
// =============================================================================

using System;
using DeNelle.Core.State;
using DeNelle.Pets;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Village
{
    /// <summary>
    /// Runs the pet-introduction beat: shows a code-built name prompt + Defend /
    /// Gather choice, persists the chosen name, and applies the role to the live
    /// <see cref="Pet"/>. The <see cref="TutorialDirector"/> awaits
    /// <see cref="IsComplete"/> and reads <see cref="ChoseGather"/> /
    /// <see cref="PetName"/> for its follow-up narration.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PetIntroduction : MonoBehaviour
    {
        /// <summary>The role the player picked for the pet.</summary>
        public enum PetRole { Defend, Gather }

        private Pet _pet;
        private UIDocument _doc;
        private VisualElement _root;
        private TextField _nameField;

        private bool _complete;
        private string _petName = "";
        private PetRole _role = PetRole.Defend;

        /// <summary>True once the player has named the pet + chosen a role (or it auto-resolved).</summary>
        public bool IsComplete => _complete;

        /// <summary>The chosen pet name (never null/empty once complete).</summary>
        public string PetName => _petName;

        /// <summary>True when the player chose GATHER (→ pet runs to nodes); false = Defend.</summary>
        public bool ChoseGather => _role == PetRole.Gather;

        /// <summary>
        /// Begins the introduction: resolves the live starter pet, then shows the
        /// name + role prompt. Falls back to an auto-name + Defend role when no UI
        /// can be built so the tutorial always finishes.
        /// </summary>
        public void Begin()
        {
            _complete = false;
            _pet = ResolveStarterPet();

            string suggestion = SuggestName(_pet);
            _petName = suggestion;

            if (!TryBuildPrompt(suggestion))
            {
                // No UI host — auto-name + Defend so the beat resolves cleanly.
                Debug.LogWarning("[PetIntroduction] No PanelSettings to host the name prompt — " +
                                 $"auto-naming the pet '{suggestion}' and defaulting to Defend.");
                Finish(PetRole.Defend);
            }
        }

        // ── Pet resolution ───────────────────────────────────────────────────

        // The starter pet is deployed by PetDeployer; grab the first live Pet in
        // the scene as the one that "chose" the player.
        private static Pet ResolveStarterPet()
        {
            return FindAnyObjectByType<Pet>();
        }

        // Species-flavoured default name (WO example: "Scrap" for Crow, "Fang" for
        // Grimhound). Maps the deployed pet's element/species to a fitting default.
        private static string SuggestName(Pet pet)
        {
            string species = pet != null ? (pet.Species ?? "") : "";
            switch (species.ToLowerInvariant())
            {
                case "ice-wolf":      return "Fang";
                case "flame-pup":     return "Ember";
                case "aether-sprite": return "Wisp";
                default:              return "Scrap";
            }
        }

        // ── UI (code-built UI Toolkit) ────────────────────────────────────────

        private bool TryBuildPrompt(string suggestion)
        {
            PanelSettings panel = FindPanelSettings();
            if (panel == null) return false;

            var go = new GameObject("TutorialPetPrompt");
            go.transform.SetParent(transform, false);
            _doc = go.AddComponent<UIDocument>();
            _doc.panelSettings = panel;
            _doc.sortingOrder = 260f;   // above the HUD + the FTUE bubbles

            _root = _doc.rootVisualElement;
            if (_root == null) return false;
            _root.Clear();
            _root.style.flexGrow = 1;
            _root.style.alignItems = Align.Center;
            _root.style.justifyContent = Justify.Center;
            _root.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);

            var card = new VisualElement();
            card.style.width = Length.Percent(86f);
            card.style.maxWidth = 460;
            card.style.paddingLeft = 26; card.style.paddingRight = 26;
            card.style.paddingTop = 22;  card.style.paddingBottom = 22;
            card.style.backgroundColor = new Color(0.07f, 0.08f, 0.12f, 0.97f);
            SetRadius(card, 20);
            SetBorder(card, new Color(0.85f, 0.72f, 0.36f, 0.9f), 3);
            _root.Add(card);

            var title = new Label("Name your Echo");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 22;
            title.style.color = new Color(0.95f, 0.84f, 0.5f);
            title.style.marginBottom = 6;
            card.Add(title);

            var sub = new Label("One of the Echoes has chosen you. What will you call it?");
            sub.style.fontSize = 15;
            sub.style.color = new Color(0.82f, 0.86f, 0.92f);
            sub.style.whiteSpace = WhiteSpace.Normal;
            sub.style.marginBottom = 14;
            card.Add(sub);

            _nameField = new TextField { value = suggestion };
            _nameField.style.fontSize = 18;
            _nameField.style.marginBottom = 18;
            card.Add(_nameField);

            var roleLabel = new Label("What should it do?");
            roleLabel.style.fontSize = 15;
            roleLabel.style.color = new Color(0.82f, 0.86f, 0.92f);
            roleLabel.style.marginBottom = 8;
            card.Add(roleLabel);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            card.Add(row);

            var defend = new Button(() => Finish(PetRole.Defend)) { text = "Defend the line" };
            StyleChoiceButton(defend);
            row.Add(defend);

            var gather = new Button(() => Finish(PetRole.Gather)) { text = "Gather supplies" };
            StyleChoiceButton(gather);
            row.Add(gather);

            return true;
        }

        private void Finish(PetRole role)
        {
            if (_complete) return;
            _role = role;

            // Read the typed name (fall back to the suggestion if blanked).
            string typed = _nameField != null ? _nameField.value : null;
            if (!string.IsNullOrWhiteSpace(typed)) _petName = typed.Trim();

            // Persist the name (WO-277 — PetName on GameState) + Save.
            var svc = GameStateService.Instance;
            if (svc != null && svc.State != null)
            {
                svc.State.PetName = _petName;
                svc.Save();
            }

            // Apply the role to the live pet. Pet.Mode already drives Defend vs
            // (effectively) idle/gather: PetHarvester auto-gathers whenever the pet
            // isn't actively defending, so Gather = Idle mode (pet not hunting →
            // harvester takes over → runs to the nearest node). Defend = Defend mode.
            if (_pet != null)
                _pet.Mode = role == PetRole.Gather ? PetMode.Idle : PetMode.Defend;

            ClosePrompt();
            _complete = true;
        }

        private void ClosePrompt()
        {
            if (_root != null)
            {
                _root.Clear();
                _root.style.display = DisplayStyle.None;
            }
            if (_doc != null)
            {
                _doc.enabled = false;
                Destroy(_doc.gameObject);
                _doc = null;
            }
        }

        private void OnDestroy()
        {
            if (_doc != null) Destroy(_doc.gameObject);
        }

        // ── UI helpers ─────────────────────────────────────────────────────────

        private static PanelSettings FindPanelSettings()
        {
            var docs = UnityEngine.Object.FindObjectsByType<UIDocument>(
                FindObjectsInactive.Include);
            foreach (var d in docs)
                if (d != null && d.panelSettings != null) return d.panelSettings;
            return null;
        }

        private static void StyleChoiceButton(Button b)
        {
            b.style.flexGrow = 1;
            b.style.fontSize = 16;
            b.style.unityFontStyleAndWeight = FontStyle.Bold;
            b.style.height = 46;
            b.style.marginLeft = 4; b.style.marginRight = 4;
            b.style.whiteSpace = WhiteSpace.Normal;
        }

        private static void SetRadius(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r;
            e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = r;
            e.style.borderBottomRightRadius = r;
        }

        private static void SetBorder(VisualElement e, Color c, float w)
        {
            e.style.borderTopColor = c; e.style.borderBottomColor = c;
            e.style.borderLeftColor = c; e.style.borderRightColor = c;
            e.style.borderTopWidth = w; e.style.borderBottomWidth = w;
            e.style.borderLeftWidth = w; e.style.borderRightWidth = w;
        }
    }
}
