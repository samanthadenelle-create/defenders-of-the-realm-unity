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
        /// <summary>Party weapon/armor shop — the native code-built MVVM gear store (PartyShopPanelMvvm).</summary>
        PartyShop = 5,
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

        // DEF-186: optional context-aware openers. A panel that can FOCUS on a
        // specific subject (e.g. the BuildingUpgrade panel scrolling to / highlighting
        // the exact building the player interacted with) registers an Action<string>
        // here in ADDITION to its plain Action above. Callers that know the subject id
        // (BuildingInteractable knows which building was tapped) route through
        // Open(id, context); callers that don't keep using Open(id). Kept as a SEPARATE
        // map so the reflection-free plain-Action contract above is untouched.
        private static readonly Dictionary<PanelId, Action<string>> _contextOpeners =
            new Dictionary<PanelId, Action<string>>();

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

        /// <summary>
        /// Register (or replace) a CONTEXT-aware open action for <paramref name="id"/>
        /// (DEF-186). The string arg is a subject id — e.g. the building id the player
        /// interacted with — letting the panel focus on / highlight that subject. A
        /// panel typically registers BOTH this and the plain <see cref="Register(PanelId, Action)"/>
        /// (the plain one is the "open with no particular focus" fallback). Idempotent.
        /// </summary>
        public static void Register(PanelId id, Action<string> openWithContext)
        {
            if (openWithContext == null) return;
            _contextOpeners[id] = openWithContext;
        }

        /// <summary>
        /// Remove the context-aware open action for <paramref name="id"/> if it is
        /// exactly <paramref name="openWithContext"/> (mirrors the plain Unregister).
        /// </summary>
        public static void Unregister(PanelId id, Action<string> openWithContext)
        {
            if (openWithContext == null) return;
            if (_contextOpeners.TryGetValue(id, out var current) && current == openWithContext)
                _contextOpeners.Remove(id);
        }

        /// <summary>True when a panel is registered for <paramref name="id"/>.</summary>
        public static bool IsRegistered(PanelId id) =>
            _openers.ContainsKey(id) || _contextOpeners.ContainsKey(id);

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

        /// <summary>
        /// Open the panel registered for <paramref name="id"/>, focusing it on
        /// <paramref name="context"/> (a subject id — DEF-186). Prefers the context-aware
        /// opener if the panel registered one; otherwise falls back to the plain
        /// <see cref="Open(PanelId)"/> (so a panel that ignores context still opens).
        /// Returns false only when NEITHER opener is registered. Exceptions are
        /// swallowed (logged) exactly like the plain Open.
        /// </summary>
        public static bool Open(PanelId id, string context)
        {
            if (_contextOpeners.TryGetValue(id, out var openCtx) && openCtx != null)
            {
                try
                {
                    openCtx.Invoke(context);
                    return true;
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning(
                        "[PanelRouter] context-opening '" + id + "' threw: " + ex.Message);
                    return false;
                }
            }
            // No context-aware opener — fall back to the plain open (ignores context).
            return Open(id);
        }
    }
}
