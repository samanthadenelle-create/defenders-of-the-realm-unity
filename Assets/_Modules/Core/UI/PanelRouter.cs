// =============================================================================
// PanelRouter — a tiny, reflection-free registry that lets any assembly OPEN a
// named gameplay panel by id without referencing the panel's type (DEF-213).
// -----------------------------------------------------------------------------
// THE PROBLEM (DEF-213): building interactions were generic. BuildingInteractable
// (DeNelle.Village) needs to open panels that live in DeNelle.HUD
// (HeroTalentPanel, PetSkillTreePanel, CosmeticShopPanel) — but Village must NOT
// reference HUD (CLAUDE.md §5). The old code bridged that gap with
// System.Reflection (FindObjectOfType(typeName).Toggle()), which (a) was fragile,
// (b) used Toggle() so a 2nd interaction CLOSED the panel, and (c) had no way to
// map a SPECIFIC building to its ONE correct panel — every building funnelled
// through the same handful of reflection calls.
//
// THE FIX: both DeNelle.Village and DeNelle.HUD already reference DeNelle.Core.
// Each panel registers a plain delegate ("open me") here under a stable
// PanelId. A caller in ANY assembly opens the right panel with one call —
// PanelRouter.Open(PanelId.HeroTalents) — with NO reflection and NO cross-asmdef
// type reference. The registered open action is the panel's own Open()/Show(),
// which already routes through PanelManager (DEF-212), so the one-panel-at-a-time
// rule still holds.
//
// Pure static state, reset on domain reload like PanelManager. No scene object.
// =============================================================================

using System;
using System.Collections.Generic;

namespace DeNelle.Core.UI
{
    /// <summary>
    /// Stable identifiers for the routable gameplay panels. A building (or any
    /// other interactable) names the panel it opens by id; the panel registers
    /// its open action against the same id. Adding a panel = add one enum value
    /// + one Register call in that panel.
    /// </summary>
    public enum PanelId
    {
        /// <summary>Hero talent trees — the Arcane Tower.</summary>
        HeroTalents = 0,
        /// <summary>Village crafting bench — the Workshop.</summary>
        Crafting = 1,
        /// <summary>Resource-building upgrades — Farm / Lumbermill / Forge / Armorer / Mine.</summary>
        BuildingUpgrade = 2,
        /// <summary>Cosmetic shop — the Marketplace / Realm Store.</summary>
        CosmeticShop = 3,
        /// <summary>Pet / companion skill tree — the Pet House.</summary>
        PetSkillTree = 4,
    }

    /// <summary>
    /// Static, reflection-free panel-open registry. Panels call
    /// <see cref="Register(PanelId, Action)"/> when they come alive; interactables
    /// call <see cref="Open(PanelId)"/> to open the one correct panel. Routing the
    /// open through the panel's own method means the modal arbiter (PanelManager)
    /// still governs visibility — opening one panel closes any other.
    /// </summary>
    public static class PanelRouter
    {
        private static readonly Dictionary<PanelId, Action> _openers =
            new Dictionary<PanelId, Action>();

        /// <summary>
        /// Register (or replace) the open action for <paramref name="id"/>. Panels
        /// call this in Awake/OnEnable. Null actions are ignored. Idempotent: a
        /// re-spawned panel simply overwrites the previous opener for its id.
        /// </summary>
        public static void Register(PanelId id, Action open)
        {
            if (open == null) return;
            _openers[id] = open;
        }

        /// <summary>
        /// Remove the open action for <paramref name="id"/> if it is exactly
        /// <paramref name="open"/> (so a destroyed panel doesn't clobber a freshly
        /// spawned replacement that already re-registered). Null-safe.
        /// </summary>
        public static void Unregister(PanelId id, Action open)
        {
            if (open == null) return;
            if (_openers.TryGetValue(id, out var current) && current == open)
                _openers.Remove(id);
        }

        /// <summary>True when a panel is registered for <paramref name="id"/>.</summary>
        public static bool IsRegistered(PanelId id) => _openers.ContainsKey(id);

        /// <summary>
        /// Open the panel registered for <paramref name="id"/>. Returns false (and
        /// does nothing) when no panel is registered — the caller can then show a
        /// "coming soon" message instead of silently doing nothing. Exceptions from
        /// the panel's open action are swallowed (logged) so a bad panel can't break
        /// the interaction.
        /// </summary>
        public static bool Open(PanelId id)
        {
            if (!_openers.TryGetValue(id, out var open) || open == null)
                return false;
            try
            {
                open.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning(
                    "[PanelRouter] opening '" + id + "' threw: " + ex.Message);
                return false;
            }
        }
    }
}
