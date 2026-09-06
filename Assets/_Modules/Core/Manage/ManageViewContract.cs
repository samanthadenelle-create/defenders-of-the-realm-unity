// =============================================================================
// ManageViewContract - WO-2002, the ONE presentation contract BUILD / ARMY /
// RESEARCH all render through.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Manage
//
// WAVE 1 of the Manage redesign. Wave 0 shipped the STATE axes next door
// (ManageStateModel.cs: ManageOwnership / ManageUpgradeTrack /
// ManageActionAvailability / ManageAction / ManageRoute / ManageTileBadge) and the
// ManageStateInvariants validator. THIS file is the PRESENTATION shape the View
// binds. It CARRIES Wave 0's model, it does not duplicate it:
//
//     composer (tab VM, in DeNelle.Village)
//         -> ManageItemState        (Wave 0: ownership / track / actions / badge)
//         -> ManageVmProjection     (this folder: model-side, collapses 9 badges
//                                    to the 5 canon visual states and attaches the
//                                    delivered art keys)
//         -> ManageTileVM / ManageSelectionVM / ManageActionVM   (this file)
//         -> ManageWorkspacePanel   (this folder: the ONE dumb renderer)
//
// ⛔ ZERO UnityEngine. This file has NO `using UnityEngine`, on purpose and it is
// oracle-enforced (ManageDumbViewRegression case [contract-is-pure]). Every visual
// is addressed as a STRING KEY, never a Sprite, so the contract can be composed,
// validated and unit-tested with no Unity object graph at all - and so a second
// renderer (a capture harness, a device overlay) can bind the same VMs. A Sprite
// field here would silently make the contract un-testable outside play mode.
//
// ⛔ THE VIEW NEVER READS ManageRoute. Canon 9 forbids the View deciding "which
// destination a prerequisite CTA should open". So a ManageActionVM carries a
// LABEL and an ACTIVATE CALLBACK and nothing else - the projection has already
// bound the route into that callback. That is what lets the oracle ban the tokens
// `.Route` and `PanelId.` outright inside the renderer, rather than trusting a
// reviewer to notice.
//
// ⛔ NO STATE IS INFERRED FROM A NULL. A null Activate is an implementation detail,
// exactly as ManageStateModel.cs says of ManageAction.Invoke. Enabled and Visible
// are EXPLICIT fields and they are the only things the View may read. The renderer
// calls `Activate?.Invoke()` and never `if (Activate != null)` as a state test;
// the oracle bans the comparison form.
//
// ASCII-ONLY player copy. Every string on these records is already sanitised by
// the composer (the old path's ManageScreenVM.Ascii stays where it is - the View
// does not sanitise, because sanitising is deriving). Device tofu risk: no em-dash,
// no curly quote.
// =============================================================================

using System;
using System.Collections.Generic;

namespace DeNelle.Core.Manage
{
    // ── Which tab (canon 2: BUILD / ARMY / RESEARCH, QUEUE is global) ─────────

    /// <summary>
    /// The three player-facing Manage tabs. This is an IDENTITY, never a label source -
    /// <see cref="ManageTabVM.Label"/> carries the words. Canon 9 forbids the View
    /// deriving a label from an enum name.
    /// </summary>
    public enum ManageTabId
    {
        Build = 0,
        Army = 1,
        Research = 2
    }

    // ── The five canon visual states (canon 7) ────────────────────────────────

    /// <summary>
    /// The FIVE states canon 7 makes mandatory - and exactly the five status medallions
    /// delivered in Assets/Resources/RpgUi/manage/ (status-available / status-locked /
    /// status-inprogress / status-queue / status-max).
    ///
    /// <para>This is a NARROWER axis than <see cref="ManageTileBadge"/> (nine values), and
    /// the collapse happens ONCE, model-side, in <c>ManageVmProjection</c>. It is not a
    /// second truth: the badge stays the authored state, this is what the tile PAINTS.</para>
    ///
    /// <para>⚠ There is deliberately NO "Unaffordable" visual state. Owner ruling 15 and the
    /// precedent already shipped at <c>HeartPanel.cs:420-440</c>: an owned thing the player
    /// cannot currently afford is NOT locked, and painting a padlock on it teaches the same
    /// false "you can never get there" this whole program exists to kill. Unaffordable wears
    /// <see cref="Available"/> and carries its refusal in the CTA's DisabledReasonText.</para>
    /// </summary>
    public enum ManageTileVisualState
    {
        Available = 0,
        Locked = 1,
        InProgress = 2,
        QueueBlocked = 3,
        Max = 4
    }

    /// <summary>
    /// How a button should LOOK. The model chooses the role; the View maps the role to its
    /// kit face. A role, not a colour - the owner is red/green colourblind (memory
    /// `owner-colorblind-delegate-visual-creative`), so the renderer must pair every role
    /// with a SHAPE or a WORD, never a hue alone.
    /// </summary>
    public enum ManageActionStyleRole
    {
        /// <summary>The one thing the player most likely wants. Gold face.</summary>
        Primary = 0,
        /// <summary>A supporting action (cancel a peek, view details). Quiet face.</summary>
        Secondary = 1,
        /// <summary>Destructive - sell, cancel a paid job. Danger face, never the primary slot.</summary>
        Destructive = 2,
        /// <summary>Pure navigation to a blocker's home (ruling 18). Confirm face.</summary>
        Navigate = 3
    }

    // ── One button ────────────────────────────────────────────────────────────

    /// <summary>
    /// WO-2002's <c>ManageActionVM</c>. One button's worth of PRESENTATION, projected from a
    /// Wave-0 <see cref="ManageAction"/>.
    ///
    /// <para><b>Enabled and Visible are separate on purpose.</b> A hidden button says "this
    /// verb does not exist here"; a visible-but-disabled button with a
    /// <see cref="DisabledReasonText"/> says "this verb exists and here is why you cannot use
    /// it yet" - canon 11 question 6. Collapsing them is how a screen ends up telling the
    /// player nothing.</para>
    /// </summary>
    public sealed class ManageActionVM
    {
        /// <summary>ASCII words on the face, supplied by the model ("UPGRADE", "VIEW HEART").</summary>
        public string Label;

        /// <summary>Explicit. The View binds it; it never derives it from a null callback.</summary>
        public bool Enabled = true;

        /// <summary>Explicit. False hides the control entirely.</summary>
        public bool Visible = true;

        public ManageActionStyleRole StyleRole = ManageActionStyleRole.Primary;

        /// <summary>
        /// The command. Already carries the route for a navigation CTA, so the View never
        /// decides a destination (canon 9). Invoked as <c>Activate?.Invoke()</c>.
        /// </summary>
        public Action Activate;

        /// <summary>
        /// ASCII sentence shown when <see cref="Enabled"/> is false ("Need 320 more Wood.",
        /// "The Builder line is full."). Null when the action is enabled.
        /// </summary>
        public string DisabledReasonText;

        /// <summary>Pre-formatted ASCII cost line for the face ("320 Wood, 80 Iron"), or null.</summary>
        public string CostText;

        /// <summary>An action that renders nothing. Use instead of a null field so the View never null-tests.</summary>
        public static ManageActionVM Hidden => new ManageActionVM { Visible = false, Enabled = false };
    }

    // ── Selected-item detail rows ─────────────────────────────────────────────

    /// <summary>One "name : value" line in the selection card. Both halves are model-supplied.</summary>
    public sealed class ManageStatVM
    {
        public string Label;
        public string Value;
        /// <summary>ASCII "what changes next" fragment ("+120/hr"), already computed. Null when none.</summary>
        public string DeltaText;
    }

    /// <summary>
    /// One resource line in a cost basket. <see cref="Affordable"/> is DECIDED BY THE MODEL -
    /// canon 9 forbids the View inspecting player resources.
    /// </summary>
    public sealed class ManageCostVM
    {
        public string Label;
        public string AmountText;
        /// <summary>Resources key for the currency glyph, or null.</summary>
        public string IconKey;
        public bool Affordable = true;
    }

    // ── One grid tile ─────────────────────────────────────────────────────────

    /// <summary>
    /// WO-2002's <c>ManageTileVM</c>. The common tile contract for BUILD and ARMY (and the
    /// RESEARCH school row, which is the same shape).
    ///
    /// <para>Canon 8 is MANDATORY here: every tile carries an actionable state indicator
    /// (<see cref="VisualState"/> + <see cref="StateText"/> + <see cref="StateIconKey"/>).
    /// A grid where the player must tap each item to find out what can be acted on is the
    /// defect this program exists to remove.</para>
    /// </summary>
    public sealed class ManageTileVM
    {
        /// <summary>Stable id. Carried for the composer's own bookkeeping - the View NEVER parses it.</summary>
        public string Id;

        public string Title;
        /// <summary>Second line ("LEVEL 3", "12 READY"). Model-supplied, never assembled by the View.</summary>
        public string Subtitle;

        /// <summary>Resources key for the item art.</summary>
        public string PortraitKey;

        public bool IsSelected;

        public ManageTileVisualState VisualState = ManageTileVisualState.Available;

        /// <summary>ASCII state word ("UPGRADING", "QUEUE FULL", "MAX"). Never derived from the enum.</summary>
        public string StateText;

        /// <summary>Resources key for the status medallion. Supplied by the projection.</summary>
        public string StateIconKey;

        /// <summary>Resources key for the tile frame. Supplied by the projection.</summary>
        public string FrameKey;

        /// <summary>0..1 while a job runs on this item; null when nothing runs. Never computed here.</summary>
        public float? Progress01;

        /// <summary>Pre-formatted ASCII countdown ("4m 12s"). Null when nothing runs.</summary>
        public string TimerText;

        /// <summary>Selects this tile. Invoked as <c>Activate?.Invoke()</c>.</summary>
        public Action Activate;
    }

    // ── The selected-item region ──────────────────────────────────────────────

    /// <summary>
    /// WO-2002's <c>ManageSelectionVM</c>. Answers canon 11's seven questions for the selected
    /// thing and nothing beyond them.
    ///
    /// <para>Canon 3: "no nested scroll region inside a selected-item detail card". So this
    /// contract is deliberately FLAT and SHORT - stats and costs are small arrays the renderer
    /// lays out in fixed pixel bands, not a list it can scroll. If a tab needs more, it belongs
    /// behind secondary detail or the Queue (canon 11).</para>
    /// </summary>
    public sealed class ManageSelectionVM
    {
        /// <summary>False when nothing is selected; the renderer then paints <see cref="EmptyText"/>.</summary>
        public bool Visible;

        /// <summary>Shown in place of the card when <see cref="Visible"/> is false. ASCII, model-supplied.</summary>
        public string EmptyText;

        public string Title;
        /// <summary>"LEVEL 3 of 6" - already formatted (question 1).</summary>
        public string LevelText;
        /// <summary>What it does (question 2).</summary>
        public string Description;

        public ManageTileVisualState State = ManageTileVisualState.Available;
        /// <summary>ASCII state word (question 6's first half).</summary>
        public string StateText;
        public string StateIconKey;
        public string PortraitKey;

        /// <summary>What changes next (question 3). Never empty-null-checked into an inferred state.</summary>
        public IReadOnlyList<ManageStatVM> Stats = Array.Empty<ManageStatVM>();
        /// <summary>What it costs (question 4). Affordability is decided model-side.</summary>
        public IReadOnlyList<ManageCostVM> Costs = Array.Empty<ManageCostVM>();

        /// <summary>What can I do now (question 5).</summary>
        public ManageActionVM PrimaryAction;
        public ManageActionVM SecondaryAction;
        /// <summary>Where do I go to resolve the blocker (question 7). Ruling 18 - P0.</summary>
        public ManageActionVM RequirementAction;

        /// <summary>0..1 while a job runs on the selected item; null otherwise.</summary>
        public float? Progress;
        /// <summary>Pre-formatted ASCII progress/countdown line.</summary>
        public string ProgressText;

        /// <summary>One extra ASCII sentence the model wants under the card (a caution, a hint).</summary>
        public string AuxiliaryText;
    }

    // ── The contextual current-job strip ──────────────────────────────────────

    /// <summary>
    /// WO-2002's <c>ManageActivityVM</c>. The "what is running right now" strip that sits
    /// between the grid and the Queue door. Tab-contextual: BUILD shows the Builder line,
    /// ARMY the Train line.
    /// </summary>
    public sealed class ManageActivityVM
    {
        public bool Visible;
        public string IconKey;
        public string Title;
        public string TimerText;
        /// <summary>"2 QUEUED" - already counted and worded by the model (canon 9 bans queue reads).</summary>
        public string QueuedCountText;
        /// <summary>Opens the global Queue (ruling 17).</summary>
        public Action OpenQueue;
    }

    // ── The global queue door ─────────────────────────────────────────────────

    /// <summary>
    /// WO-2002's <c>ManageQueueVM</c>. The always-available global QUEUE affordance (canon 2).
    /// <see cref="AtCapacity"/> is a MODEL verdict - canon 9 forbids the View computing queue
    /// capacity, which is precisely the <c>if (queue.Count &lt; max)</c> shape the work order bans.
    /// </summary>
    public sealed class ManageQueueVM
    {
        public bool Visible = true;
        /// <summary>The door's words ("QUEUE").</summary>
        public string Label;
        /// <summary>"3 RUNNING" - model-counted.</summary>
        public string CountText;
        /// <summary>"5 OF 5" - model-counted.</summary>
        public string CapacityText;
        /// <summary>Model verdict. The renderer paints a WORD and a shape for it, never a colour alone.</summary>
        public bool AtCapacity;
        public Action Open;
    }

    // ── One filter chip ───────────────────────────────────────────────────────

    /// <summary>
    /// One BUILD filter chip (canon 3: ALL / ECONOMY / DEFENSE / CRAFT / STORAGE / CIVIC).
    /// Membership is decided by the model (canon 5 says the same for RESEARCH schools); the
    /// View never infers a category from an id.
    /// </summary>
    public sealed class ManageFilterVM
    {
        public string Id;
        public string Label;
        public bool IsActive;
        public Action Activate;
    }

    // ── One tab ───────────────────────────────────────────────────────────────

    /// <summary>
    /// WO-2002's <c>ManageTabVM</c>. Everything one tab paints. A tab supplies STATE; it does
    /// not rewrite action logic (WO-2002 "Tab-specific code should provide state, not rewrite
    /// action logic").
    /// </summary>
    public sealed class ManageTabVM
    {
        public ManageTabId Id;
        /// <summary>ASCII tab words. Never derived from <see cref="Id"/> by the View.</summary>
        public string Label;
        public bool IsActive;
        /// <summary>Switches to this tab.</summary>
        public Action Activate;

        /// <summary>Empty when the tab has no filter row (ARMY, canon 4).</summary>
        public IReadOnlyList<ManageFilterVM> Filters = Array.Empty<ManageFilterVM>();

        /// <summary>The tiles, ALREADY ordered and ALREADY filtered by the model (WO-2002 "item order").</summary>
        public IReadOnlyList<ManageTileVM> Tiles = Array.Empty<ManageTileVM>();

        /// <summary>
        /// Columns the model wants. BUILD asks 4 (canon 3: 12+ visible), ARMY asks 3 (canon 4:
        /// one 3x3 grid). The renderer treats this as a REQUEST and reports in px when the
        /// measured well cannot honour it - it never silently re-columns.
        /// </summary>
        public int GridColumns = 3;

        /// <summary>ASCII sentence when <see cref="Tiles"/> is empty. Model-supplied - the View invents no copy.</summary>
        public string EmptyText;

        public ManageSelectionVM Selection;
        public ManageActivityVM Activity;
    }

    // ── The whole workspace ───────────────────────────────────────────────────

    /// <summary>
    /// The root the renderer binds. Named <c>ManageWorkspaceVM</c> rather than canon 10's
    /// <c>ManageScreenVM</c> BECAUSE THAT NAME IS TAKEN by the old rail path
    /// (Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs, 2830 lines). Canon 10 allows
    /// repository naming; two live types called ManageScreenVM would make WO-2001's re-point
    /// ambiguous the moment both namespaces are in scope.
    /// </summary>
    public sealed class ManageWorkspaceVM
    {
        public string HeaderTitle;
        public string HeaderSubtitle;

        public IReadOnlyList<ManageTabVM> Tabs = Array.Empty<ManageTabVM>();

        /// <summary>Index into <see cref="Tabs"/>. The model owns "last-used tab" (canon 2).</summary>
        public int ActiveTabIndex;

        /// <summary>The global QUEUE door (ruling 17). Always available, hence not per-tab.</summary>
        public ManageQueueVM Queue;

        /// <summary>The tab to paint. Returns null rather than guessing when the index is out of range.</summary>
        public ManageTabVM ActiveTab
        {
            get
            {
                if (Tabs == null) return null;
                if (ActiveTabIndex < 0 || ActiveTabIndex >= Tabs.Count) return null;
                return Tabs[ActiveTabIndex];
            }
        }
    }
}
