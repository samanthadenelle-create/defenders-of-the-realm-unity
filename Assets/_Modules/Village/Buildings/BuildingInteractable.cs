// =============================================================================
// BuildingInteractable — proximity prompt + interact handler for the 5
// gameplay buildings (CrystalMine, PetHouse, ArcaneTower, Workshop, Farm).
// -----------------------------------------------------------------------------
// PO observation 2026-05-20: village buildings had no interaction — you could
// walk past a mine, a pet house or a dungeon and nothing happened.
//
// This component attaches to a Building. When the hero walks within
// _activateRadius, a small floating prompt appears above the building head.
// Pressing F (or tapping the shared button) opens the ONE panel that building
// owns — building-SPECIFIC routing (DEF-213):
//   • ArcaneTower            → Hero Talents          (PanelId.HeroTalents)
//   • Workshop               → Crafting bench        (PanelId.Crafting)
//   • Forge (Armorer)        → Building Upgrade       (PanelId.BuildingUpgrade)
//   • Farm / Lumbermill /
//     CrystalMine            → Building Upgrade       (PanelId.BuildingUpgrade)
//   • PetHouse               → (no panel — pet skill-tree RETIRED 2026-07-08; harvest/companion only)
// The panel is opened through DeNelle.Core.UI.PanelRouter — no cross-asmdef
// reference, no reflection. Each panel routes its own open through PanelManager
// (DEF-212), so the one-panel-at-a-time rule still holds. A building with no
// panel registered yet shows a clean "coming soon" note instead of a wrong panel.
//
// DEF-213 root cause this replaces: the old code mapped every building through a
// reflection-driven Toggle() of a panel found by FindAnyObjectByType, and EVERY
// BuildingInteractable in the scene listened for the same global F key. With
// overlapping proximity radii a single F press fired several buildings at once
// (e.g. Arcane Tower's Hero Talents AND a neighbour's Companion panel), and the
// Toggle() semantics meant a second press closed an unrelated panel. We now (a)
// only let the NEAREST in-range building act on F, (b) suppress F while any modal
// panel is open, and (c) open (never toggle) the one correct panel by id.
// =============================================================================

using DeNelle.Core.Catalog;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;
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
        // Walk-away auto-close: once the hero is this far from the building, close the
        // structure dialogue it opened (generous buffer past ActivateRadius so it only
        // closes on a clear walk-away, not at the edge).
        private const float StructureCloseRadius = ActivateRadius + 4f;

        private Building _building;
        private Transform _hero;
        private GameObject _promptGo;
        private TextMesh _promptText;
        private bool _openedStructure;   // this building opened the shared structure dialogue
        private string _myHookId;        // cached structure-dialogue id for this building
        private bool _focusHeld;         // true while THIS building holds the HUD upgrade focus (transition-logged)
        private bool _npcGapWarned;      // F8 2026-07-07: once-only Warn when no NPC covers this building

        // WO-415: structure ids whose FRONT NPC owns the talk → the matching building defers its
        // own interact prompt (the NPC opens the same shared dialogue — one trigger, not two).
        private static readonly System.Collections.Generic.HashSet<string> _npcCovered =
            new System.Collections.Generic.HashSet<string>();

        /// <summary>Called when an NPC is placed at a structure's front: that structure's building
        /// stops prompting (the NPC is the single talk trigger; the shared hook is untouched).</summary>
        public static void MarkNpcCovered(string structureId)
        {
            if (!string.IsNullOrEmpty(structureId)) _npcCovered.Add(structureId);
        }

        // =====================================================================
        //  PROD-002 Deliverable A — STRUCTURES WITH NO TALK DOOR.
        //
        //  THE RULE (owner, 2026-08-17): "what is the value of having a NPC at the lumbermill, the
        //  Arcane Tower, or the Barracks, if you can no longer enter through them, now the entrance
        //  is through manage which is cleaner" / "they dont offer any value only store shops".
        //  An NPC or a building exists to open something that HAS NO OTHER ENTRANCE. Once Manage
        //  owns a flow, a prompt in front of that building is a door to a room that moved: it costs
        //  a tap target on a phone and teaches the player that talking to people is pointless right
        //  before they meet a vendor who isn't.
        //
        //  ⛔ BOTH DOORS MUST GO TOGETHER, WHICH IS WHY THIS LIVES BESIDE _npcCovered.
        //  A structure can be entered from TWO places — the front NPC's CastleNpcInteractable and
        //  the building's own prompt — and they are coupled: MarkNpcCovered makes the building
        //  defer so only one fires. Remove the NPC's door alone and the building's prompt SILENTLY
        //  COMES BACK (it is only suppressed while covered), so the door would appear to move rather
        //  than close. Suppressing here, at the building, closes the half the NPC change cannot.
        //
        //  ⚠ NOT A SERVICE CHECK. Deliberately an explicit list of the ids the owner NAMED, not a
        //  derived "has no vendor row" rule. A derived rule would also strip Brom (rumour board) and
        //  the Herbalist (alchemy), whose doors are their ONLY entrance — silently deleting real
        //  service access while looking like a tidy generalisation.
        // =====================================================================
        private static readonly System.Collections.Generic.HashSet<string> _noTalkDoor =
            new System.Collections.Generic.HashSet<string>
            {
                "collector_lumbermill",   // production building; Manage owns the flow
                "arcane-tower",           // upgrades moved to Manage
                // Owner ruling PROD-002 option (a), 2026-08-18: the barracks door CLOSES and its
                // once-teach toast is RETIRED (removed in BarracksNpcInjector, same change).
                // The drillmaster's Talk opened only DialogueService.PlayStructure("barracks", …) —
                // structure dialogue, never a training panel — so it was a dead door by this file's
                // own rule.
                //
                // ⚠ PROD-002's OTHER stated reason — "Manage owns training" — WAS NOT TRUE ON
                //   2026-08-18, and this comment used to assert it as settled fact. It was not:
                //   ManageScreenVM.BuildTroopsBrowse emitted UPGRADE rows only, so closing this door
                //   removed the LAST player-reachable entrance to troop training. The owner found it
                //   the same week ("under manage i see option to upgrade the troops, but i dont se a
                //   way to train troops"). Keeping the wrong reason on the books is how the next seat
                //   repeats the mistake, so it is corrected here rather than deleted.
                //
                //   IT IS TRUE NOW: PROD-013 (2026-08-20) added the TRAIN rows and the Armies/muster
                //   entry to the Manage screen's Troops tab (ManageScreenVM.BuildTroopsBrowse /
                //   TrainTroop / AddMusterRow), so Manage genuinely owns training and this door stays
                //   closed. ⛔ Do NOT reopen it to "fix" a training complaint — canon (CLAUDE.md §7)
                //   is ONE door, and that door is Manage. If training is unreachable again, the
                //   defect is in ManageScreenVM, not here.
                //
                // ⚠ The drillmaster BODY still stands at the barracks. Only the affordance goes —
                // same as the other two.
                "barracks",
            };

        /// <summary>
        /// True when this structure must offer NO talk door — no [F] prompt, no tap target, no sign.
        /// The NPC BODY still stands there: "they add no value" was true of the DOOR, not the person,
        /// and a town with people working in it is not a diorama.
        /// </summary>
        public static bool HasNoTalkDoor(string structureId)
        {
            return !string.IsNullOrEmpty(structureId) && _noTalkDoor.Contains(structureId);
        }

        private void Awake()
        {
            _building = GetComponent<Building>();
        }

        private void Start()
        {
            ResolveHero();
            _myHookId = StructureHookIdFor(_building);

            // T-034: always-visible world sign so the player can read this building's
            // TYPE (shop / upgrade / pet / spell) from a distance, separate from the
            // proximity "Interact" prompt. Skipped for buildings whose FRONT NPC owns
            // the talk — the NPC carries its own sign (CastleVendorNpcInjector), so the
            // building deferring its prompt must not double up a sign over the same spot.
            // PROD-002 A: no sign either. A sign is an advertisement for a door — leaving one over a
            // building that no longer opens is the same broken promise as the prompt, just quieter.
            if (HasNoTalkDoor(_myHookId))
            {
                FlowTrace.Once("Village", "no-talk-door-" + _myHookId,
                    $"BuildingInteractable: '{_myHookId}' offers NO talk door (PROD-002 A) — prompt and " +
                    "sign both suppressed; Manage owns this flow. The NPC body stays.");
            }
            else if (_myHookId == null || !_npcCovered.Contains(_myHookId))
            {
                InteractableSign.ForBuilding(_building, _myHookId);
            }
        }

        private void ResolveHero()
        {
            // Reflection-free direct find — HeroLocomotion lives in this asmdef.
            var hero = UnityEngine.Object.FindAnyObjectByType<HeroLocomotion>();
            if (hero != null) _hero = hero.transform;
        }

        private void Update()
        {
            if (_hero == null) { ResolveHero(); return; }

            // WO-415: this structure has a front NPC that owns the talk → defer entirely. Release the
            // shared button + hide our bubble so only the NPC's CastleNpcInteractable opens the dialogue.
            // PROD-002 A: this structure offers no talk door at all. Same release path as the
            // npc-covered case — the difference is that NOTHING takes over, by design.
            if (HasNoTalkDoor(_myHookId))
            {
                MobileInteractButton.Release(this);
                if (_promptGo != null) HidePrompt();
                return;
            }

            if (_myHookId != null && _npcCovered.Contains(_myHookId))
            {
                MobileInteractButton.Release(this);
                if (_promptGo != null) HidePrompt();
                return;
            }

            // Build mode: the player is AUTHORING (placing structures), not interacting.
            // Release the shared button, hide our world bubble, and skip the in-range
            // re-show + the desktop [F] press for the whole session. Restored on exit
            // automatically because s_buildModeActive flips back to false (the button's
            // BuildModeChanged hook), so this Update resumes showing the prompt again.
            if (MobileInteractButton.Suppressed)
            {
                MobileInteractButton.Release(this);
                if (_promptGo != null) HidePrompt();
                return;
            }

            float distSqr = (_hero.position - transform.position).sqrMagnitude;
            bool inRange = distSqr <= ActivateRadius * ActivateRadius;

            // Walk-away auto-close: if this building opened the structure dialogue and the
            // hero has wandered off (or the dialogue already ended), close + clear.
            if (_openedStructure)
            {
                if (!DialogueService.IsRunning)
                    _openedStructure = false;
                else if (distSqr > StructureCloseRadius * StructureCloseRadius)
                {
                    DialogueService.Stop();
                    _openedStructure = false;
                }
            }

            // WO-337: while a dialogue is on screen, the proximity "Interact" prompt
            // (shared button + world bubble) must NOT linger behind / over the dialogue
            // panel — it stacks under the choice options and reads as a third layer.
            // Release the button and hide the bubble for the duration of the dialogue,
            // and skip the in-range re-show below so it can't immediately reappear.
            if (DialogueService.IsRunning)
            {
                MobileInteractButton.Release(this);
                if (_promptGo != null) HidePrompt();
                return;
            }

            // Owner F8 2026-07-07 "interact only through npc — remove the black interact"
            // (supersedes DEF-203 for BUILDINGS): a building never raises the shared black
            // Interact button — its front NPC's Talk (TalkPromptRegistry -> HUD Talk) is the
            // ONE interaction path. The old NPC-cover gate leaked (StructureHookIdFor returns
            // null for Apothecary/JewelersBench, so the :103 early-return never fired for them
            // — the "second black affordance" she flagged). A building with NO covering NPC now
            // self-reports (Warn, once) instead of silently losing reach: that's a content gap
            // (spawn its NPC), not a reason to keep the banned affordance. Mines/portals/seams
            // (non-building requesters) keep the shared button — out of this directive's scope.
            if (inRange)
            {
                if (!_npcGapWarned && (_myHookId == null || !_npcCovered.Contains(_myHookId)))
                {
                    _npcGapWarned = true;
                    FlowTrace.Warn("Interact", "building '" + _building.Type + "' (hook='" +
                        (_myHookId ?? "<null>") + "') has NO covering NPC — its Interact() is now " +
                        "unreachable by design (owner F8 2026-07-07: interact only through NPC). " +
                        "Spawn/point its front NPC to restore reach.");
                }
                // Owner 2026-06-20: tell the HUD an upgradable, not-maxed building is in reach
                // so its bottom button swaps Quest -> Upgrade, focused on THIS building's id.
                bool want = IsUpgradableNotMaxed(_myHookId);
                if (want) DeNelle.Core.UI.HudBuildingFocus.Set(_myHookId);
                else DeNelle.Core.UI.HudBuildingFocus.Clear(_myHookId);
                if (want != _focusHeld)
                {
                    _focusHeld = want;
                    FlowTrace.Step("HUD", "BuildingFocus " + (want ? "SET" : "in-range-but-NOT-upgradable") +
                        " id='" + (_myHookId ?? "<null>") + "' type=" + _building.Type +
                        " (catalogUpgradable=" + DeNelle.Core.State.BuildingTierCatalog.IsUpgradable(_myHookId ?? "") + ")");
                }
            }
            else
            {
                MobileInteractButton.Release(this);
                DeNelle.Core.UI.HudBuildingFocus.Clear(_myHookId);   // release focus if we held it
                if (_focusHeld)
                {
                    _focusHeld = false;
                    FlowTrace.Step("HUD", "BuildingFocus CLEAR id='" + (_myHookId ?? "<null>") + "' (out of range)");
                }
            }

            // F8 2026-07-07 (supersedes DEF-217 for buildings): with the building no longer
            // requesting the shared button, the old "bubble when button inactive" fallback
            // would RESURFACE the world bubble as a new second affordance. Buildings show no
            // proximity interact prompt at all — the front NPC's Talk is the one path.
            if (_promptGo != null) HidePrompt();

            // Mobile-first: interaction fires through the shared on-screen Interact
            // button (requested above while in range). The desktop F-key trigger was
            // removed — Interact() stays reached by the touch button / HUD path.
        }

        private void OnDisable()
        {
            MobileInteractButton.Release(this);
            DeNelle.Core.UI.HudBuildingFocus.Clear(_myHookId);   // don't leave the HUD focused on a gone building
        }

        /// <summary>
        /// True when this building can still be upgraded (owner 2026-06-20 — drives the
        /// HUD's Quest↔Upgrade context swap). Mirrors the Interact() upgrade gate: a city
        /// tier-catalog building is upgradable only while its current tier is below max; a
        /// legacy resource building not in the catalog (e.g. farm) counts as upgradable and
        /// shows its own maxed state in the panel. Forge/Lumbermill live in the catalog, so
        /// they get the precise not-maxed gate.
        /// </summary>
        private static bool IsUpgradableNotMaxed(string hookId)
        {
            if (string.IsNullOrEmpty(hookId)) return false;
            if (DeNelle.Core.State.BuildingTierCatalog.IsUpgradable(hookId))
                return DeNelle.Core.State.ModifierService.TierOf(hookId)
                     < DeNelle.Core.State.BuildingTierCatalog.MaxTier(hookId);
            // Legacy resource building: upgradable ONLY while not at max level. Without the
            // !IsMaxLevel gate a maxed Farm/Lumbermill/Forge kept HudBuildingFocus set, so the
            // HUD context button stayed in Upgrade mode (comet ring/"circle") instead of reverting
            // to the Quest face. (City-tier branch above already reverts correctly.)
            return Buildings.Progression.ResourceBuildingProgression.IsResourceBuilding(hookId)
                && !Buildings.Progression.ResourceBuildingState.IsMaxLevel(hookId);
        }

        // ── Prompt ──────────────────────────────────────────────────────────
        private void ShowPrompt()
        {
            // Approach modal — bright golden colored badge with the building
            // label + key prompt. Owner direction 2026-05-20: needs to read as
            // an action affordance, not a debug overlay.
            _promptGo = BuildBubble(
                $"[ Tap / F ] {LabelFor(_building.Type)}",
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
            // PARAMETERIZED BUILDING HOOK: the economy buildings (farm/lumbermill/forge/
            // market/pet-house) open the ONE shared Yarn dialogue (portrait + Buy/Sell/
            // Upgrade/Talk), scoped to their own domain by $structureId. One node, the
            // parameter differs. Falls through to the legacy panels for the rest
            // (ArcaneTower → Hero Talents, Workshop → Crafting).
            // WO-413: upgradable-vs-shop is decided IN the Yarn menu (StructureMenu gates Buy/Sell on
            // $structureCanShop, seeded from BuildingCatalog.IsUpgradable in DialogueCommandBridge) —
            // ONE chokepoint that also covers the castle vendor-NPC path (CastleNpcInteractable).
            string hookId = StructureHookIdFor(_building);
            // §12 / WO-413: name the routing branch this building takes. The shop-vs-upgrade
            // split is then decided data-driven INSIDE the StructureMenu Yarn node (gated on
            // $structureCanShop / $structureCanUpgrade, seeded from BuildingCatalog caps in
            // CmdStructureStatus) — NOT here. This trace pins which hook id the building used so a
            // "wrongly offers shop" report maps straight to the catalog entry that gated it.
            FlowTrace.Step("Village", $"Interact {_building.Type} (id='{_building.BuildingId}') -> " +
                (hookId != null ? $"structure hook '{hookId}' (StructureMenu gates shop/upgrade)"
                                : "legacy panel route"));
            // WO-951 (owner ruling 2026-08-10): the Echo Hollow is the Echoes' building — tapping
            // it opens the EXISTING Echo roster popup ("they open the echos pop up on right?
            // Simple and easy."). Routed BEFORE the upgrade/Yarn branches so neither steals the
            // tap; the old Yarn grant menu (Echo Warden choose-a-pet) is superseded as the tap
            // surface — Echoes unlock by level now (EchoService) and the starter grant rides the
            // founding-arc ARRIVE beat. ONE chokepoint with the keeper NPC: the same
            // CastleNpcInteractable route decision (ResolveRoute -> "echo-roster") governs both.
            // EchoRoster.Open self-traces ([Flow:Echo] RosterOpen) + registers with PanelManager.
            if (CastleNpcInteractable.IsEchoHollowId(hookId))
            {
                FlowTrace.Step("Village", $"Interact {_building.Type} -> Echo roster (WO-951 Hollow repurpose).");
                EchoRoster.Open();
                return;
            }
            // UPGRADE IS DIRECT, NEVER YARN (owner 2026-06-20, severe): an upgradable building
            // ALWAYS opens the code-built Building Upgrade panel — it must NEVER fall through to the
            // Yarn StructureMenu. The Yarn upgrade path ran a DIFFERENT backend (ResourceBuildingState
            // vs the panel's BuildingUpgradeService) and decided the action from Yarn vars, so a panel
            // registration race could fire the WRONG logic on the same click. Decoupling upgrade from
            // YarnSpinner also shrinks Yarn's fragile surface. Upgradable = city tiers
            // (BuildingTierCatalog) OR legacy resource buildings (ResourceBuildingProgression).
            // Market/shop + Talk-only buildings still route to Yarn below (Buy/Sell/Talk unchanged).
            // Ticket #11 / owner "match everywhere else": a TALK-FUNCTION building (barracks troop training)
            // is a Talk target first — its upgrade is the HUD context button, like a shoppable vendor. Exclude
            // it from the upgrade short-circuit so the body-tap opens its primary function, agreeing with the NPC.
            bool isUpgradable = hookId != null &&
                !CastleNpcInteractable.HasTalkFunctionId(hookId) &&
                (DeNelle.Core.State.BuildingTierCatalog.IsUpgradable(hookId) ||
                 Buildings.Progression.ResourceBuildingProgression.IsResourceBuilding(hookId));
            if (isUpgradable)
            {
                if (PanelRouter.Open(PanelId.BuildingUpgrade, hookId))
                    FlowTrace.Step("Village", $"Interact {_building.Type} -> MVVM Building Upgrade (focus='{hookId}').");
                else
                    FlowTrace.Warn("Village", $"Building Upgrade panel opener not registered for '{hookId}' — " +
                        "NOT falling through to Yarn (upgrade is direct-only; check PanelRouter registration).");
                return;   // upgradable buildings NEVER reach the Yarn StructureMenu
            }

            if (hookId != null)
            {
                // Pass the building's OWN sign label so the dialogue title matches the
                // big-letters world sign (DisplayLabel = "Forge"), not the titleized id.
                string label = !string.IsNullOrEmpty(_building.DisplayLabel)
                    ? _building.DisplayLabel : LabelFor(_building.Type);
                if (DialogueService.PlayStructure(hookId, label))
                {
                    _openedStructure = true;   // walk-away auto-close watches this
                    Debug.Log($"[BuildingInteractable] {_building.Type} → structure dialogue '{hookId}' ('{label}').");
                    return;
                }
            }

            // DEF-213: open the ONE panel this specific building owns, by id, through
            // the reflection-free PanelRouter. Each panel's registered open action
            // routes through PanelManager, so opening it closes any other panel.
            if (TryPanelFor(_building, out PanelId panelId))
            {
                // DEF-186: for the shared Building Upgrade panel, pass the SPECIFIC
                // building's id as context so the panel opens focused on the building
                // the player actually tapped (e.g. the Lumbermill) instead of a generic
                // list. Other panels ignore context (plain-Open fallback in PanelRouter).
                string context = ContextIdFor(_building);
                bool opened = string.IsNullOrEmpty(context)
                    ? PanelRouter.Open(panelId)
                    : PanelRouter.Open(panelId, context);
                if (opened)
                {
                    Debug.Log($"[BuildingInteractable] {_building.Type} → opened {panelId} (focus='{context}').");
                    return;
                }

                // The right panel exists in canon but isn't registered yet (e.g. its
                // bootstrap hasn't spawned). Show a clean note rather than a wrong panel.
                Debug.Log($"[BuildingInteractable] {_building.Type} → {panelId} not ready.");
                ShowFloatingNote($"{LabelFor(_building.Type)} — coming soon");
                return;
            }

            // No panel mapped for this building: clean name tooltip, never a wrong panel.
            Debug.Log($"[BuildingInteractable] {_building.Type} has no panel — name tooltip only.");
            ShowFloatingNote($"{LabelFor(_building.Type)} — coming soon");
        }

        /// <summary>
        /// Maps a building to the ONE panel it opens (DEF-213 canon). Returns false
        /// for buildings that have no panel yet (the caller shows a "coming soon"
        /// note). Matches the upgrade buildings by BuildingType first, then by the
        /// id-only resource buildings (Lumbermill / Forge have no enum value).
        /// </summary>
        // The structure id for buildings that use the parameterized Yarn hook (the
        // economy buildings that have a Portraits/<id> NPC), else null → legacy panel.
        private static string StructureHookIdFor(Building building)
        {
            if (building == null) return null;
            string id = (building.BuildingId ?? "").ToLowerInvariant();
            // Fall back to the GameObject NAME when no explicit BuildingId was authored (e.g. a placed
            // "CastleBarracks" object with no id field) so name-based buildings still resolve their hook.
            if (string.IsNullOrEmpty(id)) id = (building.gameObject != null ? building.gameObject.name : "").ToLowerInvariant();
            if (id.Length > 0)
            {
                if (id.Contains("lumbermill")) return "lumbermill";
                if (id.Contains("armorer")) return "armorer";
                if (id == "forge") return "forge";
                if (id.Contains("farm")) return "farm";
                if (id.Contains("market")) return "market";
                if (id.Contains("barracks")) return "barracks";   // WO-432: CastleBarracks -> barracks upgrade panel
                if (id.Contains("pet")) return "pet-house";
                if (id.Contains("workshop")) return "workshop";
                if (id.Contains("arcane")) return "arcane-tower";
            }
            switch (building.Type)   // buildings authored without an explicit id
            {
                case BuildingType.Farm: return "farm";
                case BuildingType.Lumbermill: return "lumbermill";
                case BuildingType.Forge: return "forge";
                case BuildingType.Armorer: return "armorer";
                case BuildingType.Workshop: return "workshop";
                case BuildingType.ArcaneTower: return "arcane-tower";
                case BuildingType.PetHouse: return "pet-house";
            }
            return null;
        }

        private static bool TryPanelFor(Building building, out PanelId panelId)
        {
            panelId = default;
            if (building == null) return false;

            switch (building.Type)
            {
                case BuildingType.ArcaneTower:
                    // OWNER 2026-07-04 consolidation, finished EYES-SWEEP 2026-07-06: the legacy
                    // HeroTalents route is REMOVED entirely (a stale ff.herotalents PlayerPrefs
                    // re-armed the dead route → panel_HeroTalents rendered black). Always HeroSkillTree.
                    panelId = PanelId.HeroSkillTree;
                    return true;
                case BuildingType.Workshop:
                    panelId = PanelId.Crafting;
                    return true;
                // RETIRED (2026-07-08): the PetHouse -> Pet skill-tree panel hook is removed — the pet
                // SKILL-TREE stack was deleted (dead content; pets are harvest/companion-only per
                // docs/COMBAT_PIVOT_NORTHSTAR.md). PetHouse now has no panel (Interact shows "coming soon").
                // Apothecary workbench → the consumable-crafting / alchemy bench. Routed here
                // (NOT via StructureHookIdFor) ON PURPOSE: Interact() tries the Yarn structure
                // dialogue BEFORE TryPanelFor, so apothecary must return null from
                // StructureHookIdFor (it does — no case added there) to fall through and open
                // PanelId.ConsumableCrafting DIRECTLY, with no Yarn detour.
                case BuildingType.ApothecaryWorkbench:
                    panelId = PanelId.ConsumableCrafting;
                    return true;
                // Jeweler's bench → the jewelry-crafting bench. Routed here (NOT via
                // StructureHookIdFor, which returns null for it) so Interact() falls through the
                // Yarn path and opens PanelId.JewelerCrafting DIRECTLY, exactly like the Apothecary.
                case BuildingType.JewelersBench:
                    panelId = PanelId.JewelerCrafting;
                    return true;
                // Resource + Armorer buildings all share the Building Upgrade panel.
                case BuildingType.CrystalMine:
                case BuildingType.Farm:
                case BuildingType.Lumbermill:
                case BuildingType.Forge:
                case BuildingType.Armorer:
                    panelId = PanelId.BuildingUpgrade;
                    return true;
            }

            // Id-keyed resource buildings (Lumbermill / Forge may carry the enum value
            // OR be authored as the default type with only an id set) → Upgrade panel.
            string id = building.BuildingId;
            if (!string.IsNullOrEmpty(id))
            {
                string lower = id.ToLowerInvariant();
                if (lower.Contains("lumbermill") || lower.Contains("forge") ||
                    lower.Contains("armorer") || lower.Contains("farm") || lower.Contains("mine"))
                {
                    panelId = PanelId.BuildingUpgrade;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Resolves the FOCUS subject id for the upgrade panel (DEF-186): the resource
        /// building progression id (farm / lumbermill / forge) the panel should scroll
        /// to / highlight. Prefers the explicit Building.BuildingId, then maps the
        /// BuildingType for the resource buildings, else null (no focus — generic open).
        /// </summary>
        private static string ContextIdFor(Building building)
        {
            if (building == null) return null;

            string id = building.BuildingId;
            if (!string.IsNullOrEmpty(id))
            {
                string lower = id.ToLowerInvariant();
                if (lower.Contains("lumbermill")) return Buildings.Progression.ResourceBuildingProgression.LumbermillId;
                if (lower.Contains("forge") || lower.Contains("armorer")) return Buildings.Progression.ResourceBuildingProgression.ForgeId;
                if (lower.Contains("farm")) return Buildings.Progression.ResourceBuildingProgression.FarmId;
                // Pass any other explicit id through verbatim (panel resolves/ignores it).
                if (Buildings.Progression.ResourceBuildingProgression.IsResourceBuilding(id)) return id;
            }

            switch (building.Type)
            {
                case BuildingType.Lumbermill: return Buildings.Progression.ResourceBuildingProgression.LumbermillId;
                case BuildingType.Forge:      return Buildings.Progression.ResourceBuildingProgression.ForgeId;
                case BuildingType.Farm:       return Buildings.Progression.ResourceBuildingProgression.FarmId;
            }
            return null; // CrystalMine / non-resource: no focus, generic open.
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

        // The CATALOG is the single naming authority for player-facing building words
        // (WO-1161: StructureRoles.By[role].DisplayName reads the row that claims the role).
        // A word typed into this file drifts the moment creative renames a row — which is
        // exactly how this dialogue TITLE BAR came to announce "Workshop" for the building
        // the palette had already relabelled "Crafting Station". The fallback is reached
        // ONLY when the catalog has not loaded, and is deliberately the GENERIC "Building",
        // never a competing proper noun: say nothing rather than say something false.
        private static string RoleWord(string role)
            => StructureRoles.By[role].DisplayName ?? "Building";

        private static string LabelFor(BuildingType t) => t switch
        {
            // ── Roles the catalog authors: the word comes from the row, never from here ──
            BuildingType.Workshop    => RoleWord(StructureRole.CraftingStation), // id "workshop"   -> "Crafting Station"
            BuildingType.Farm        => RoleWord(StructureRole.FoodProducer),      // id "collector_farm"
            BuildingType.Lumbermill  => RoleWord(StructureRole.WoodProducer),      // id "collector_lumbermill" -> "Lumber Mill"
            BuildingType.Forge       => RoleWord(StructureRole.Weaponsmith),     // id "forge" (SELLS WEAPONS)
            BuildingType.Armorer     => RoleWord(StructureRole.Armorer),         // id "armorer" (SELLS ARMOUR)
            BuildingType.JewelersBench => RoleWord(StructureRole.Jeweler),       // id "jeweler"
            // ── No catalog row claims a role for these yet, so the word stays local ──
            BuildingType.CrystalMine => "Mine",
            BuildingType.PetHouse    => "Echo Hollow",
            BuildingType.ArcaneTower => "Tower",
            BuildingType.ApothecaryWorkbench => "Apothecary",
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
