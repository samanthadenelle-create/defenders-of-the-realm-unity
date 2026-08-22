// =============================================================================
// RealmStoreVendor — the world-side door to the Realm Store.
// -----------------------------------------------------------------------------
// PROD-003 (owner ruling 2026-08-18, placement option (a)): the game's ONLY
// monetization surface was the third dialogue option on Coppin, a produce
// vendor, below a scroll fold. Reordering it first was a two-minute mitigation,
// not the fix — the Realm Store is not a vendor's inventory, it is the game's
// storefront, and reaching it through another merchant's small talk is a
// CATEGORY error that no amount of reordering repairs.
//
// ⛔ WHY THIS IS ITS OWN COMPONENT AND NOT A CastleNpcInteractable.
// That one opens the shared STRUCTURE DIALOGUE for a catalog id
// (DialogueService.PlayStructure). The Realm Store has no catalog row and must
// never get one — a catalog row would put it in the build palette, which is
// exactly the failure PROD-003 exists to prevent:
//     sellable   -> the player deletes their own store
//     movable    -> it gets buried behind walls
//     damageable -> a raid takes revenue OFFLINE
//     placeable  -> a brand-new player has no store at all, which is backwards
//                   for the session most likely to spend
// So it is baked hub furniture, like the Heart, and its door opens a PANEL
// directly rather than routing through structure dialogue.
//
// The opener already exists: PackStoreBootstrap registers PanelId.RealmStore at
// boot, so this is a DOOR, not a system. Nothing here knows what the store sells.
//
// Interaction matches the vendor convention deliberately (TalkPromptRegistry ->
// the HUD TALK button). WO-416 retired the bottom-centre "Talk:" element as a
// redundant duplicate, so raising MobileInteractButton here would reintroduce the
// exact clutter that was removed.
// =============================================================================

using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>Proximity door that opens the Realm Store panel. Baked, never placeable.</summary>
    public sealed class RealmStoreVendor : MonoBehaviour
    {
        /// <summary>Matches the vendor activate radius so the store does not feel different to approach.</summary>
        private const float ActivateRadius = 6f;

        private Transform _hero;
        private bool _registered;

        private void Awake()
        {
            // Existing baked scenes predate WO-1052. Keep the scene-builder attachment as the
            // authored path, but self-heal old baked furniture so a fresh player build cannot
            // silently ship the store door without its landmark.
            if (GetComponent<RealmStoreBeacon>() == null) gameObject.AddComponent<RealmStoreBeacon>();
        }

        private void Update()
        {
            if (_hero == null)
            {
                var hero = FindAnyObjectByType<HeroLocomotion>();
                if (hero != null) _hero = hero.transform;
                return;
            }

            // While dialogue is on screen, drop the prompt so it does not stack under it —
            // same rule the vendor NPCs follow.
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
            // ⚠ Guard + trace rather than a bare call: if the opener was never registered (a boot
            // order change, PackStoreBootstrap not running), a silent no-op would look to the player
            // like the store is broken and to us like nothing happened. PanelRouter.Open returns
            // false in that case, so say so.
            Guard.Try("RealmStore", "open the Realm Store panel", () =>
            {
                if (PanelRouter.Open(PanelId.RealmStore))
                {
                    FlowTrace.Step("RealmStore", "storefront vendor opened PanelId.RealmStore.");
                }
                else
                {
                    FlowTrace.Fail("RealmStore",
                        "PanelRouter.Open(PanelId.RealmStore) returned FALSE — the opener is not " +
                        "registered. PackStoreBootstrap registers it at boot; if that did not run, " +
                        "the storefront is a door to nothing and the player gets no feedback at all.");
                }
            });
        }
    }
}
