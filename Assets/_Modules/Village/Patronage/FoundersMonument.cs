// =============================================================================
// FoundersMonument - WO-1073, the world-side door onto the Benefactors of the
// Realm wall.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Owner ruling 2026-08-27(c), verbatim: "Because the tier ships with a
// placeholder monument, the monument EXISTS from day one - so it is the wall's
// door immediately, and the 'where does the wall open from' question needs no
// separate answer. Walking up to the monument and reading the names is the
// moment; a menu item is not."
//
// So there is exactly ONE door onto PanelId.Benefactors and it is this object.
// No action-bar face (CLAUDE.md section 7 caps the calm(town) bar and spends
// paragraphs on why), no settings entry, no menu item.
//
// -----------------------------------------------------------------------------
// WHY THIS IS ITS OWN COMPONENT, and not a CastleNpcInteractable / a catalog row
// -----------------------------------------------------------------------------
// Identical reasoning to RealmStoreVendor (PROD-003), which is the component this
// one is modelled on line for line:
//     sellable   -> the player deletes the realm's honour roll
//     movable    -> a founder's monument gets buried behind a wall
//     damageable -> a raid takes the wall offline
//     placeable  -> a new player has no monument at all
// It is baked-equivalent hub furniture, like the Heart, placed at runtime by
// FoundersMonumentInjector. It must NEVER acquire a structures-catalog row.
//
// Interaction matches the vendor convention deliberately (TalkPromptRegistry ->
// the HUD TALK button). WO-416 retired the bottom-centre "Talk:" element as a
// redundant duplicate, so raising MobileInteractButton here would reintroduce
// exactly the clutter that was removed.
//
// ⛔ THIS COMPONENT NEVER TOUCHES ART. It does not load a mesh, does not know
// which mesh it is sitting on, and does not care whether that mesh is the shared
// stand-in or a patron's bespoke FBX. That belongs to the injector, so the art
// swap is a data change in one place. See FoundersMonumentInjector's header.
//
// ASCII only. Instrumentation: FlowTrace tag "Benefactors". Never strip it.
// =============================================================================

using DeNelle.Core.Diagnostics;
using DeNelle.Core.Patronage;
using DeNelle.Core.UI;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>Proximity door that opens the Benefactors of the Realm wall.</summary>
    public sealed class FoundersMonument : MonoBehaviour
    {
        /// <summary>Matches RealmStoreVendor.ActivateRadius so no landmark in the hub
        /// feels different to approach. One number, one convention.</summary>
        public const float ActivateRadius = 6f;

        private Transform _hero;
        private bool _registered;

        private void Update()
        {
            if (_hero == null)
            {
                var hero = FindAnyObjectByType<HeroLocomotion>();
                if (hero != null) _hero = hero.transform;
                return;
            }

            // While dialogue is on screen, drop the prompt so it does not stack under it -
            // the same rule every vendor NPC follows.
            if (MobileInteractButton.Suppressed || DialogueService.IsRunning)
            {
                if (_registered) { TalkPromptRegistry.Deregister(transform); _registered = false; }
                return;
            }

            bool inRange = (_hero.position - transform.position).sqrMagnitude
                           <= ActivateRadius * ActivateRadius;

            if (inRange && !_registered)
            {
                TalkPromptRegistry.Register(transform, Open);
                _registered = true;
            }
            else if (!inRange && _registered)
            {
                TalkPromptRegistry.Deregister(transform);
                _registered = false;
            }
        }

        private void OnDisable()
        {
            if (!_registered) return;
            TalkPromptRegistry.Deregister(transform);
            _registered = false;
        }

        private void Open()
        {
            // Guard + trace rather than a bare call. If the opener was never registered (a
            // boot-order change, BenefactorsWallPanelBootstrap not running), a silent no-op
            // would look to the player like the monument is decoration and to us like
            // nothing happened at all. PanelRouter.Open returns false in that case, so we
            // say so, loudly, naming the fix.
            Guard.Try(BenefactorsCatalog.Sys, "open the Benefactors of the Realm wall", () =>
            {
                if (PanelRouter.Open(PanelId.Benefactors))
                {
                    FlowTrace.Step(BenefactorsCatalog.Sys,
                        "Founders Monument opened PanelId.Benefactors (standing rows=" +
                        BenefactorsCatalog.Count + ", provenance=" + BenefactorsCatalog.Provenance + ").");
                }
                else
                {
                    FlowTrace.Fail(BenefactorsCatalog.Sys,
                        "PanelRouter.Open(PanelId.Benefactors) returned FALSE - the opener is not " +
                        "registered. BenefactorsWallPanelBootstrap spawns the panel, which registers " +
                        "the id in Awake; if that did not run, the Founders Monument is a door to " +
                        "nothing and the player gets no feedback whatsoever.");
                }
            });
        }
    }
}
