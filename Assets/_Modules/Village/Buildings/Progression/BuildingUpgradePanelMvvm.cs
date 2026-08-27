// =============================================================================
// BuildingUpgradePanelMvvm — the building ENHANCEMENT panel VIEW (MVVM).
// A DUMB SKIN over clean uGUI chrome that BINDS a BuildingUpgradeVM. ALL
// state/logic (affordability, unlock, tier gating, perks) lives in the VM — the
// View never reads game state.  Namespace: DeNelle.Village.Buildings.Progression
// Assembly: DeNelle.Village
//
// MASTER-DETAIL REDESIGN (owner-approved mockup 2026-07-17, Screenshot 060241 —
// supersedes the vertical Tier-1/2/3 list AND the ornate carved-stone frame that
// made the panel read as a mess).  The owner wants the OBSIDIAN palette (dark
// near-black panel + runic gold + green accents) rendered CLEAN — a flat rounded
// dark rectangle with thin subtle borders, NOT the ornate FrameTalent stone
// chrome.  So this View builds its OWN clean container (code-built uGUI, no UXML,
// no BuildObsidianPanel) and only borrows the kit's clean primitives (Label,
// AddImage/ApplyRounded, StyleButtonColors, FitSingleLine/FitBlock).
//
// LAYOUT (matches 060241 exactly, BUILDING-AGNOSTIC — every field is VM data):
//   HEADER   : shield medallion top-left + centered GOLD "<Building> Enhancements".
//   CURRENCY : a row of pill chips (icon left, value right) — the spendable set.
//   TABS     : "Upgrade" | "Skills". 50/50. Selected = gold UNDERLINE + gold text on
//              the dark plate (WO-832: a tab NEVER wears the CTA's gold fill).
//   BODY (Upgrade tab) = WO-895 "NEXT UPGRADE ONLY", ONE full-width column:
//     1. PROGRESS STRIP — "Tier N of 6" + a 6-SEGMENT bar (filled = owned, hollow =
//        not yet; the segments differ by SHAPE/fill, never by hue alone) + "Now:
//        <current tier name>". One slim row.
//     2. NEXT UPGRADE CARD — the focus. Icon + "NEXT UPGRADE - TIER N+1" kicker +
//        the next tier's NAME, a plain description, the tier's BONUSES as SEPARATE
//        wrapped rows (never one clipped run-on string), the COST as chips that name
//        the shortfall IN WORDS, and ONE stateful action button.
//     3. MAX TIER — the card becomes "Fully enhanced" (no action), bar full.
//   BODY (Skills tab) = the per-tier RESEARCH PERKS as a scroll list (unchanged
//           row grammar), so the Skills side keeps its existing content/behaviour.
//   FOOTER   : one centered gold-bordered "Close".
//
// WO-895 (2026-08-05) — WHY THE 6-CARD RAIL DIED: at 6-across every tier card
// truncated mid-word ("Muster the Bar", "Unlock 'Muste") and the detail pane clipped
// too. The owner's ruling: "we don't need to see all the upgrades, just details on
// what they can get to next." A drill-in from the Manage screen lands on THIS card,
// so it must stand alone. No horizontal tier rail survives; no per-tier SELECTION
// state survives (there is nothing to select — the next tier is the subject).
//
// THE ONE TRUE BUTTON (WO-832 one-button law, WO-895 state machine). Its state is a
// PURE REFLECTION of vm.ActionState — which reads the SAME authorities the queue
// uses (BuildTimerService active/pending + catalog gates + the GameState wallet).
// Four player states, each distinguishable with COLOUR REMOVED (the owner is red/
// green colourblind — CLAUDE.md law):
//   Ready            "Upgrade to <Name>"      bright plate, ARROW glyph, TAPPABLE
//   Missing resources"Missing resources"      dark plate, EMPTY-BOX glyph, inert
//   Queued           "Queued - waiting..."    dark plate, HOLLOW-ARROW glyph, inert
//   In progress      "In progress - M:SS"     dark plate, live COUNTDOWN + a FILL BAR
// Only "In progress" carries a growing fill bar; only "Ready" is interactable; every
// label is unique TEXT. Hue is decoration, never the signal.
//
// WO-841 live tick carried forward: Update() rewrites JUST the countdown label's text
// (and the fill bar's width) when the whole second changes; ContentSignature excludes
// RemainingSeconds so there is no per-second full rebuild.
// WO-832 §4 carried forward: every text band is a FIXED ref-pixel band sized in whole
// FontFloor line boxes (the RumorBoard TMP vertical-cull lesson) — fraction bands
// under-heighted the font's line at real aspects and TMP truncated mid-word.
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Core.UI.Mvvm;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.Buildings.Progression
{
    [DisallowMultipleComponent]
    public sealed class BuildingUpgradePanelMvvm : MonoBehaviour, IPanelView
    {
        // ── Clean obsidian palette (dark near-black + runic gold + green accent) ──
        private static readonly Color PanelFill    = new Color(0.043f, 0.041f, 0.049f, 0.985f); // near-black obsidian
        private static readonly Color SubPanelFill = new Color(0.055f, 0.052f, 0.060f, 1f);      // left/right body sub-panels
        // (WO-895: the CardFill/CardFillLit/CardFillDim tier-card tints died with the rail.)
        private static readonly Color TabDark      = new Color(0.085f, 0.082f, 0.078f, 1f);      // unselected tab
        private static readonly Color PillFill     = new Color(0.062f, 0.059f, 0.055f, 1f);      // currency pill
        private static readonly Color BorderDim    = new Color(0.42f, 0.40f, 0.36f, 0.45f);      // subtle rule
        private static readonly Color BorderGold   = new Color(0.831f, 0.686f, 0.216f, 1f);      // gold rim (selected)
        private static readonly Color BorderGoldDim= new Color(0.58f, 0.48f, 0.22f, 0.75f);      // gold rim (available)

        // WO-1195: the currency-icon ROLE constant is gone with the switch it fed. Naming a role
        // folder here is half a registry; the role now lives only in concept-icons.json.

        private BuildingUpgradeVM _vm;

        private GameObject _ui;
        private RectTransform _bodyHost;          // content host (below tab row)
        private GameObject _upgradePage;          // Upgrade page root (single "next only" column)
        private GameObject _skillsPage;           // Skills page root (scroll list)
        private RectTransform _progressHost;      // WO-895 — the slim "Tier N of M" strip
        private RectTransform _nextCardHost;      // WO-895 — the NEXT-upgrade hero card
        private RectTransform _skillsContent;     // Skills-tab scroll content (perk rows)
        private int _activeTab;                   // 0 = Upgrade, 1 = Skills

        // Custom clean currency pills (built once; values refreshed on each Render).
        private struct PillRef { public ElarionUiKit.CurrencyKind Kind; public TMPro.TextMeshProUGUI Value; }
        private readonly List<PillRef> _pills = new List<PillRef>();

        // Tab visuals (restyled per active tab). WO-832: tabs carry a gold UNDERLINE
        // when selected (never the CTA's gold fill — one true button rule).
        private struct TabRef { public Image Fill; public Image Underline; public TMPro.TextMeshProUGUI Label; }
        private readonly List<TabRef> _tabs = new List<TabRef>();

        // Status is transient: toast only NEW statuses, never the open-time baseline.
        private string _lastStatus;

        // ── Render dedup (WO fix 2026-07-19) ──────────────────────────────────────
        // EconomyService.OnChanged fires after EVERY mutation (passive income ticks,
        // pet/outpost harvest, etc.) -> BuildingUpgradeVM re-raises Changed EVERY tick ->
        // this View re-ran a FULL destroy+rebuild of every tier card + skills row each
        // tick. That churned the layout (visual jitter), drained perf, and re-armed the
        // one-shot UiKitTextFitGuard on every rebuilt label EVERY frame (the "band too
        // short" warning spam). We now hash the RENDERED state (perks + selection +
        // affordability + effect/cost strings); the expensive rebuild runs ONLY when that
        // hash actually changes. Cheap per-tick work (pill values, status toast) still runs.
        private string _lastContentSig;

        // Building-portrait cache (portraits import as plain Texture2D — wrap once).
        private static readonly Dictionary<string, Sprite> _portraitCache = new Dictionary<string, Sprite>();

        private PanelHandle _panelHandle;

        public bool IsOpen => _ui != null;

        private const float RowHeightPx   = 132f;   // Skills-tab perk row height
        private const float RowGapPx      = 12f;
        private const float ButtonFadeSec = 0.12f;   // hover/press transition — never snap

        // ── WO-832 §4 truncation-proof text bands (fixed REF-PIXEL) ──────────────
        // RumorBoard lesson 2026-08-02: fraction bands scale with the card/pane height
        // and under-height the font's line box at real aspects — TMP then TRUNCATES the
        // fitted text mid-word. Every text band below is a FIXED-pixel band sized as a
        // whole number of FLOOR LINES (the kit's FontFloor auto-size floor x the ~1.25em
        // TMP line box), so the floor-size text always seats. Never sub-floor literals —
        // everything derives from the kit constants. Public so the EditMode suite
        // (BuildingUpgradePanelLayoutTests) pins the invariants.
        public const float FloorLinePx      = ElarionUiKit.FontFloor * 1.25f + 2f; // one floor-size line box
        public const float BandGapPx        = 8f;

        // ── WO-895 "next only" bands (all FIXED ref px, whole floor-line multiples) ──
        // BUDGET (the reason each number is what it is). The Upgrade body is ~0.61 of the
        // panel, which is ~0.9 of the canvas: at the 1080-tall landscape reference that is
        // ~593 ref px. The strip + the card's FIXED bands must leave the BONUS list a real
        // three-row zone, because the bonuses ARE the answer to "what do I get next". Every
        // band below is therefore the SMALLEST whole floor-line count that still seats its
        // content — a fatter header here silently starves the bonus rows.
        //   strip 59 + gap 16 -> card ~518
        //   card: 8 + header 79 + 8 + desc 40 + 8 + [BONUS ~162] + 10 + cost 59 + 12 + cta 132
        /// <summary>The slim progress strip: one label line tall (the bar rides inside it).</summary>
        public const float ProgressStripPx  = FloorLinePx * 1.5f;
        /// <summary>Icon + kicker line + next-tier NAME line.</summary>
        public const float NextHeaderBandPx = FloorLinePx * 2f;
        /// <summary>The plain description sentence (one line - it is a short composed sentence).</summary>
        public const float NextDescBandPx   = FloorLinePx;
        /// <summary>One short bonus line.</summary>
        public const float BonusRow1Px      = FloorLinePx + 6f;
        /// <summary>One wrapping bonus line (2 lines).</summary>
        public const float BonusRow2Px      = FloorLinePx * 2f + 6f;
        /// <summary>Longest bonus text trusted to a single row line at full card width
        /// (the card is now the whole body — ~1730 ref px — so a line holds far more).</summary>
        public const int   BonusSingleLineChars = 58;
        /// <summary>Hard cap on rendered bonus rows — a dropped tail beats a mid-word cut.</summary>
        public const int   BonusMaxRows     = 6;
        /// <summary>The UPGRADE COST row: its caption sits at the LEFT of the same band as the
        /// chips (a stacked caption cost a whole line the bonus rows needed).</summary>
        public const float CostBandPx       = FloorLinePx * 1.5f;
        /// <summary>The ONE action button — the kit's canonical CTA height (132), which clears
        /// two floor lines, so a long state label WRAPS instead of clipping.</summary>
        public const float ActionBandPx     = ElarionUiKit.CanonCtaHeight;
        /// <summary>Bottom inset of the action band inside the card.</summary>
        public const float ActionBottomPx   = 12f;
        /// <summary>Gap between the cost row and the action button.</summary>
        public const float CostGapPx        = 10f;
        /// <summary>Height of the in-progress fill bar along the button's bottom edge.</summary>
        public const float ActionFillPx     = 10f;

        // ── WO-841 live countdown (carried into the WO-895 button) ───────────────
        // The "In progress - M:SS" label + its fill bar, cached so Update() can tick JUST
        // their text/width once per whole second — never a full RebuildUpgrade
        // (ContentSignature deliberately excludes RemainingSeconds; 2026-07-19 dedup note).
        private TMPro.TextMeshProUGUI _ctaCountdownLabel;
        private RectTransform _ctaProgressFill;
        private int _ctaCountdownLastSec = -1;

        // §12 — the last button state we RENDERED. Every transition is traced with the
        // data that decided it, so a capture proves which state was shown and why.
        private UpgradeActionState _lastActionState = UpgradeActionState.Unavailable;
        private bool _hasRenderedAction;

        // ── THE MODEL BAND (owner ruling 2026-08-16: "go to the modeled page") ────
        // A live 3D render of the structure that stands in the town, in a LEFT column beside
        // the next-upgrade card. Built on the PROVEN RawImage + manually-driven off-screen
        // camera rig (TowerPreviewCamera / RotateModelMenu:115-120 / HeroPreviewViewer) —
        // NOT BuildPreviewModal (UI conformance debt baseline) and NOT the UIElements
        // TowerPlacementRotateMenu (UXML does not render in player builds, CLAUDE.md §8).
        // When no prefab resolves the column is not built at all and the card keeps the full
        // width with its existing 2D portrait — never an empty black box.
        private RectTransform _previewHost;
        private RawImage _previewImage;
        private TowerPreviewCamera _previewRig;
        private string _previewKey;               // "<id>@<level>" the live rig was built for
        private float _previewYaw;
        /// <summary>Fraction of the body the model column takes when a model resolved.</summary>
        public const float PreviewColumnWidth = 0.32f;
        /// <summary>Idle turntable speed (deg/sec) — motion, never a colour signal.</summary>
        private const float PreviewSpinDegPerSec = 18f;
        // A narrower card wraps sooner, so the single-line budget shrinks with it. Derived,
        // never a second literal (WO-832 §4: fixed bands, honest thresholds).
        private int BonusLineChars => _previewImage != null
            ? Mathf.RoundToInt(BonusSingleLineChars * (1f - PreviewColumnWidth - 0.02f))
            : BonusSingleLineChars;

        // ── Registration ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _panelHandle = PanelManager.Register("Building Enhancements", Close, () => IsOpen);
            PanelRouter.Register(PanelId.BuildingUpgrade, OpenGeneric);
            PanelRouter.Register(PanelId.BuildingUpgrade, (System.Action<string>)Open);
        }

        private void OnDestroy()
        {
            Unbind();
            _vm?.Dispose();
            _vm = null;
            DisposePreviewRig();   // the RenderTexture + off-screen rig never outlive the panel
            if (_ui != null) Destroy(_ui);
            _ui = null;
            PanelRouter.Unregister(PanelId.BuildingUpgrade, OpenGeneric);
            PanelRouter.Unregister(PanelId.BuildingUpgrade, (System.Action<string>)Open);
        }

        // PanelRouter plain (no-context) open — the VM resolves the default building.
        private void OpenGeneric() => Open(null);

        // ── Open: construct + bind the VM, build chrome ───────────────────────────

        public void Open(string buildingId)
        {
            Close();

            // VM FIRST — it resolves the default building + economy handle itself, so this
            // View never touches a service, and the chrome's title composes from the name.
            _vm = BuildingUpgradeVM.CreateDefault(buildingId, Close);
            _hasRenderedAction = false;   // fresh open -> the first button state is a traced transition

            BuildChrome();

            Bind(_vm);

            FlowTrace.Step("UpgradeUI", "open '" + (_vm != null ? _vm.Title : "?")
                + "' next-only card (Upgrade+Skills), tab=" + _activeTab);

            // Arbiter closes any other open panel first + applies the battle-lock.
            if (!PanelManager.NotifyOpened(_panelHandle))
            {
                // Rejected (e.g. in battle) — NotifyOpened already invoked our Close.
                return;
            }
        }

        // ── IPanelView ────────────────────────────────────────────────────────────

        public void Bind(IPanelViewModel vm)
        {
            Unbind();
            _vm = vm as BuildingUpgradeVM;
            if (_vm == null) return;
            _vm.Changed += Render;
            Render();
        }

        public void Unbind()
        {
            if (_vm != null) _vm.Changed -= Render;
        }

        // Queue-state repaint (F8 2026-07-30): the CTA now reads the live builder gates, so
        // when a job starts/finishes WHILE this panel is open the button must re-resolve
        // (busy countdown -> Upgrade, or vice versa). Poll the published queue snapshot's
        // Version — the same change-detect seam the HUD chip uses; a repaint only fires on
        // an actual publish, never per-frame.
        private int _queueVersionSeen;
        private void Update()
        {
            if (_vm == null) return;
            var st = DeNelle.Core.UI.ObsidianQueueGate.Status;
            if (st.Version != _queueVersionSeen) { _queueVersionSeen = st.Version; Render(); }

            // The model band's turntable. SetRotation IS the draw (URP never auto-renders an
            // off-screen Base camera), so this call is what keeps the RawImage alive — it runs
            // ONLY while the Upgrade tab is showing a live rig.
            if (_previewRig != null && _previewRig.IsValid && _activeTab == 0 && _previewImage != null)
            {
                _previewYaw = Mathf.Repeat(_previewYaw + PreviewSpinDegPerSec * Time.unscaledDeltaTime, 360f);
                _previewRig.SetRotation(Quaternion.Euler(0f, _previewYaw, 0f));
            }

            // WO-841 — the cheap per-second countdown tick. If the button is showing the
            // "In progress - M:SS" label, rewrite ONLY its text (and nudge the fill bar)
            // when the whole second flips — one TMP string assignment + one anchor write,
            // no teardown, no fit re-arm, no rebuild. Completion needs no work here:
            // IsBuilding flips -> the queue publish above bumps Version -> Render -> the
            // signature changes -> RebuildUpgrade swaps the button back and clears this cache.
            if (_ctaCountdownLabel != null && _vm.UnderConstruction)
            {
                int sec = _vm.ActionRemainingSeconds;
                if (sec != _ctaCountdownLastSec)
                {
                    _ctaCountdownLastSec = sec;
                    _ctaCountdownLabel.text = FormatActionLabel(UpgradeActionState.InProgress, sec, null);
                    if (_ctaProgressFill != null)
                    {
                        float p = Mathf.Clamp01(_vm.ActionProgress);
                        _ctaProgressFill.anchorMax = new Vector2(p, _ctaProgressFill.anchorMax.y);
                    }
                }
            }
        }

        /// <summary>WO-895 — ASCII M:SS (never a locale format, never a non-ASCII separator).
        /// "0:07" / "2:45" / "12:03"; hours roll into the minutes field so the shape never
        /// changes on a long job. Clamps negatives (a completion race can read below zero).</summary>
        public static string FormatMinutesSeconds(int seconds)
        {
            if (seconds < 0) seconds = 0;
            int m = seconds / 60;
            int s = seconds % 60;
            return m + ":" + (s < 10 ? "0" : "") + s;
        }

        /// <summary>
        /// WO-895 — the ONE composer for the action button's label. Build-time and the
        /// per-second tick both call this, so the string stays byte-identical (a drifting
        /// string would visibly restyle the button on the first tick). ASCII ONLY, and every
        /// state's text is UNIQUE — that is what makes the four states readable with colour
        /// removed (the owner is red/green colourblind).
        /// </summary>
        public static string FormatActionLabel(UpgradeActionState state, int seconds, string nextName)
            => FormatActionLabel(state, seconds, nextName, 0, 0);

        /// <summary>
        /// WO-1045 — <see cref="FormatActionLabel(UpgradeActionState,int,string)"/> plus the queue
        /// DEPTH readout the <see cref="UpgradeActionState.QueueFull"/> face carries
        /// ("Queue full - 5 of 5 lined up").
        /// <para>
        /// ⚠ The numbers are DEPTH (how many may be LINED UP), never CREWS (how many run at once).
        /// Those are different axes with different remedies, and a face that showed "2 of 2" here
        /// would send the player to buy parallelism for a problem parallelism alone does not name.
        /// Pass 0/0 to get the bare word — that is what the ASCII/uniqueness oracle exercises.
        /// </para>
        /// </summary>
        public static string FormatActionLabel(UpgradeActionState state, int seconds, string nextName,
                                               int queueDepth, int queueLimit)
        {
            switch (state)
            {
                case UpgradeActionState.Ready:
                    return string.IsNullOrEmpty(nextName) ? "Upgrade" : ("Upgrade to " + nextName);
                case UpgradeActionState.MissingResources: return "Missing resources";
                case UpgradeActionState.Queued:           return "Queued - waiting for a builder";
                case UpgradeActionState.InProgress:       return "In progress - " + FormatMinutesSeconds(seconds);
                case UpgradeActionState.VillageGated:     return "Raise Village Tier";
                case UpgradeActionState.Maxed:            return "Fully enhanced";
                case UpgradeActionState.QueueFull:
                    return queueLimit > 0
                        ? string.Format(Copy(CopyKeyQueueFullDetail, "Queue full - {0} of {1} lined up"),
                                        queueDepth, queueLimit)
                        : Copy(CopyKeyQueueFull, "Queue full");
                default:                                  return "No upgrades here";
            }
        }

        // ── WO-1045 player-facing copy (CLAUDE.md §7 — never a literal in the View) ──
        // Authored in canon-strings.json so the wording can be re-pointed without a code change.
        // The fallbacks below are NOT a second authority: they are the crash-mat for a missing key
        // (VillageStrings would otherwise render a literal "[[missing:key]]" onto the button, which
        // is a worse failure than the sentence it replaced). A fallback that fires is LOGGED.
        //
        // ⛔ The REFUSAL sentences are NOT here and must never be copied here — they come live from
        // BuildTimerService.LineFullMessage / TryBuySlot via the VM. Two copies of a refusal is the
        // drift bug this WO exists to close.
        public const string CopyKeyQueueFull       = "upgradeQueueFull";
        public const string CopyKeyQueueFullDetail = "upgradeQueueFullDetail";
        public const string CopyKeyQueueSlotOffer  = "upgradeQueueSlotOffer";
        public const string CopyKeyQueueCrews      = "upgradeQueueCrews";

        // ── WO-1037 shortfall-offer copy (same canon-strings discipline) ──────────
        // ⚠ THE HARVEST LINE IS DELIBERATELY FIRST AND ON THE WIDER HALF (WO-1037 §1: "the existing
        // path stays first-class and visible; the pack is an ALTERNATIVE, never the recommended
        // route"). If a future edit ever makes the offer the primary plate, that guardrail is broken
        // — the layout IS the guardrail here, not a comment about it.
        public const string CopyKeyShortDetail     = "upgradeShortDetail";
        public const string CopyKeyShortHarvest    = "upgradeShortHarvest";
        public const string CopyKeyShortPackOffer  = "upgradeShortPackOffer";
        public const string CopyKeyShortPackSoon   = "upgradeShortPackSoon";

        /// <summary>Canon-strings lookup with a logged fallback (never renders "[[missing:...]]").</summary>
        internal static string Copy(string key, string fallback)
        {
            string s = DeNelle.Village.VillageStrings.Canon(key);
            if (string.IsNullOrEmpty(s) || s.StartsWith("[[missing:", System.StringComparison.Ordinal))
            {
                FlowTrace.Warn("UpgradeUI", "canon-strings key '" + key
                    + "' is missing - rendering the built-in fallback '" + fallback
                    + "'. Add the key to canon-strings.json (Resources AND StreamingAssets).");
                return fallback;
            }
            return s;
        }

        // ── Render: repaint from vm.* ONLY ────────────────────────────────────────

        private void Render()
        {
            if (_vm == null) return;

            // Refresh the currency pills (plain set — no red/green flash, colorblind law).
            // Only assign when the value string actually changed — avoids a TMP mesh regen
            // on every idle income tick.
            for (int i = 0; i < _pills.Count; i++)
                if (_pills[i].Value != null)
                {
                    string v = ElarionUi.CompactNumber(WalletValue(_pills[i].Kind));
                    if (_pills[i].Value.text != v) _pills[i].Value.text = v;
                }

            // Status is transient — pop a toast only when it CHANGES to a new, non-empty message.
            string status = _vm.Status;
            if (!string.IsNullOrEmpty(status) && status != _lastStatus)
            {
                _lastStatus = status;
                BuildFeedbackToast.Show(status);
            }

            // EVENT-DRIVEN rebuild: the full card/skills teardown+rebuild (and the fit-guard
            // re-arm it triggers) runs ONLY when the rendered state changed, not every tick.
            string sig = ContentSignature();
            if (sig != _lastContentSig)
            {
                RebuildUpgrade();
                RebuildSkills();
                _lastContentSig = sig;
            }

            RestyleTabs();
            ApplyTabVisibility();
        }

        // Hash of everything the progress strip / next card / skills rows render from, so
        // Render can skip the expensive rebuild when nothing visible actually changed.
        // WO-895: the BUTTON STATE is part of the hash (that is the whole point of the state
        // machine — a start/queue/completion must repaint the button EXACTLY once), while the
        // per-second remainder is deliberately EXCLUDED (Update() ticks just that label).
        private string ContentSignature()
        {
            if (_vm == null) return "";
            var sb = new StringBuilder(256);
            sb.Append(_vm.Title).Append('|')
              .Append(_vm.CurrentTier).Append('/').Append(_vm.MaxTier).Append('|')
              .Append((int)_vm.ActionState).Append('|')
              .Append(_vm.NextTierName).Append('|')
              .Append(_vm.NextAffordable ? '1' : '0')
              // WO-1045 — the queue readout is part of the rendered content, so the DEPTH counter
              // and the slot price repaint as the line drains. Without these the band would freeze
              // at "5 of 5" while items completed, because ActionState alone does not move until
              // the LAST one clears. Cheap: four ints on a signature already built per publish.
              // (The auto RE-ENABLE itself is not new plumbing — Update() polls the published
              // ObsidianQueueGate.Status.Version at ~1 Hz, so a freed slot repaints with no reopen.)
              .Append('|').Append(_vm.BuilderQueueDepth).Append('/').Append(_vm.BuilderQueueLimit)
              .Append('|').Append(_vm.BuilderCrewsBusy).Append('/').Append(_vm.BuilderCrewSlots)
              .Append('|').Append(_vm.CanBuyQueueSlot ? '1' : '0').Append(_vm.QueueSlotPrice)
              // WO-1037 — the shortfall offer's DISMISSED state is rendered content: without it a
              // dismiss tap would mutate the set and repaint nothing, because ActionState and the
              // cost lines are both unchanged by a dismissal. (The offer's IDENTITY needs no entry:
              // it is a pure function of the cost lines, which are already hashed below.)
              .Append('|').Append(IsOfferDismissed() ? '1' : '0');
            foreach (var b in _vm.NextBonuses) sb.Append('~').Append(b);
            foreach (var c in _vm.NextCostLines)
                sb.Append('$').Append(c.Label).Append(':').Append(c.Amount)
                  .Append(c.Short ? '!' : '.').Append(c.Missing);
            foreach (var item in _vm.Perks)
            {
                sb.Append('#').Append(item.Id)
                  .Append(';').Append(item.Name)
                  .Append(';').Append(item.Equipped ? '1' : '0')
                  .Append(item.Locked ? '1' : '0')
                  .Append(item.Affordable ? '1' : '0')
                  .Append(';').Append(item.LockReason)
                  .Append(';').Append(_vm.EffectFor(item.Id))
                  .Append(';').Append(_vm.CostFor(item.Id));
            }
            return sb.ToString();
        }

        // ── Chrome — CLEAN flat dark panel (no ornate frame) + zones ──────────────

        private void BuildChrome()
        {
            _ui = ElarionUiKit.BuildModalCanvas("BuildingUpgradePanelMvvmUI", 31000);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, onTapClose: () => _vm?.Close());

            string titleText = (_vm != null ? _vm.Title : "Building") + " Enhancements";

            // CLEAN flat obsidian panel: a rounded near-black rectangle with a thin subtle
            // gold rim + corner rivets (mockup 060241) — NOT the ornate stone frame.
            RectTransform panel = RoundedCard(_ui.transform, "Panel",
                new Vector2(0.035f, 0.05f), new Vector2(0.965f, 0.95f),
                PanelFill, new Color(BorderGold.r, BorderGold.g, BorderGold.b, 0.32f), 2.5f);
            AddCornerRivets(panel);

            // HEADER — shield medallion (top-left) + centered gold title.
            BuildMedallion(panel);
            var title = ElarionUiKit.Label(panel, titleText, 0.905f, 0.995f,
                ElarionUi.Gilt, ElarionUi.FontTitle, TMPro.TextAlignmentOptions.Center,
                0.11f, 0.89f, bold: true);
            title.raycastTarget = false;
            ElarionUiKit.FitSingleLine(title);

            // CURRENCY pill row.
            var walletStrip = MakeZone(panel, "CurrencyRow", new Vector2(0.012f, 0.815f), new Vector2(0.988f, 0.888f));
            BuildCurrencyPills(walletStrip);

            // TAB row: Upgrade | Skills.
            var tabHost = MakeZone(panel, "TabRow", new Vector2(0.012f, 0.720f), new Vector2(0.988f, 0.800f));
            BuildTabs(tabHost);

            // BODY host (below tabs, above footer).
            _bodyHost = MakeZone(panel, "BodyHost", new Vector2(0.012f, 0.095f), new Vector2(0.988f, 0.705f));
            _upgradePage = BuildUpgradePage(_bodyHost);
            // No Skills tab => no Skills page. RebuildSkills/ApplyTabVisibility are null-safe.
            _skillsContent = null;
            _skillsPage = null;
            if (_tabs.Count > 1) _skillsPage = BuildScrollPage(_bodyHost, "SkillsPage", out _skillsContent);
            else _activeTab = 0;
            ApplyTabVisibility();

            // FOOTER — one centered gold-bordered Close.
            BuildCloseButton(panel);

            // Capture the open-time status as the toast baseline (do NOT toast the idle hint).
            _lastStatus = _vm != null ? _vm.Status : null;

            // WO-832 §4: settle the fresh canvas' first layout pass NOW so the first
            // RebuildUpgrade (Bind -> Render, same frame) can read real rect heights for
            // its fixed-pixel band math — rects are otherwise zero until the canvas lays
            // out (the RumorBoard "unknowable at build time" trap).
            Canvas.ForceUpdateCanvases();

            // Eased open: scale 0.92->1 + fade 0->1, ease-out (scale the outer card incl. border).
            var fx = _ui.AddComponent<PanelOpenCloseFx>();
            fx.PlayOpen((panel.parent as RectTransform) ?? panel);
        }

        // Small shield medallion disc, top-left (mockup 060241).
        private void BuildMedallion(RectTransform panel)
        {
            RectTransform disc = RoundedCard(panel, "Medallion",
                new Vector2(0.014f, 0.905f), new Vector2(0.058f, 0.985f),
                new Color(0.10f, 0.075f, 0.03f, 1f), BorderGoldDim, 2.5f);
            var shield = RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconShield);
            if (shield != null)
            {
                var g = new GameObject("Shield", typeof(Image));
                g.transform.SetParent(disc, false);
                var rt = g.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.18f, 0.18f); rt.anchorMax = new Vector2(0.82f, 0.82f);
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                var img = g.GetComponent<Image>();
                img.sprite = shield; img.preserveAspect = true; img.raycastTarget = false;
                img.color = ElarionUi.Gilt;
            }
            else
            {
                var glyph = ElarionUiKit.Label(disc, ElarionUi.CrestGlyph, 0.10f, 0.90f,
                    ElarionUi.Gilt, ElarionUi.FontHead, TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f, bold: true);
                glyph.raycastTarget = false;
                ElarionUiKit.FitSingleLine(glyph);
            }
        }

        // Four subtle corner rivet dots (mockup 060241 flat-panel detail).
        private static void AddCornerRivets(RectTransform panel)
        {
            var pts = new[]
            {
                new Vector2(0.012f, 0.972f), new Vector2(0.988f, 0.972f),
                new Vector2(0.012f, 0.020f), new Vector2(0.988f, 0.020f)
            };
            foreach (var p in pts)
            {
                var go = ElarionUiKit.AddImage(panel, "Rivet",
                    new Vector2(p.x - 0.006f, p.y - 0.010f), new Vector2(p.x + 0.006f, p.y + 0.010f),
                    new Color(0.30f, 0.28f, 0.25f, 0.9f));
                go.GetComponent<Image>().raycastTarget = false;
            }
        }

        private void BuildCloseButton(RectTransform panel)
        {
            RectTransform frame = RoundedCard(panel, "Close",
                new Vector2(0.40f, 0.018f), new Vector2(0.60f, 0.082f),
                new Color(0.10f, 0.095f, 0.088f, 1f), BorderGold, 2.5f);
            var host = frame.parent as RectTransform ?? frame;   // bordered outer carries the button
            var b = host.gameObject.AddComponent<Button>();
            b.targetGraphic = host.GetComponent<Image>();
            ElarionUiKit.StyleButtonColors(b);
            SoftenButton(b);
            b.onClick.AddListener(() => { FlowTrace.Step("UpgradeUI", "close"); _vm?.Close(); });
            var lbl = ElarionUiKit.Label(frame, "Close", 0.10f, 0.90f,
                ElarionUi.Parchment, ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f, bold: true);
            lbl.raycastTarget = false;
            ElarionUiKit.FitSingleLine(lbl);
        }

        // ── Currency pill row (clean: dark rounded pill, icon left, value right) ──

        private void BuildCurrencyPills(RectTransform strip)
        {
            _pills.Clear();
            if (strip == null) return;

            var kinds = DeriveSpendableCurrencies();
            int n = kinds.Count;
            if (n == 0) return;

            const float gap = 0.01f;
            for (int i = 0; i < n; i++)
            {
                float x0 = (float)i / n + (i == 0 ? 0f : gap * 0.5f);
                float x1 = (float)(i + 1) / n - (i == n - 1 ? 0f : gap * 0.5f);
                BuildCurrencyPill(strip, kinds[i], x0, x1);
            }
        }

        private void BuildCurrencyPill(RectTransform strip, ElarionUiKit.CurrencyKind kind, float x0, float x1)
        {
            RectTransform pill = RoundedCard(strip, "Pill_" + kind,
                new Vector2(x0, 0.06f), new Vector2(x1, 0.94f), PillFill, BorderDim, 1.5f);

            float textX0 = 0.08f;
            // WO-1195 criterion 5: the icon resolves through the ONE data path
            // (ConceptIconResolver -> concept-icons.json), never a hardcoded sprite name.
            // The switch that used to live here was a SECOND icon registry, and it carried the
            // same bug the kit had: it returned "currency_food" for CurrencyKind.Food, which
            // canon §7 retired for Stone.
            var icon = UiStyle.Icon(ElarionUiKit.ConceptIdFor(kind));
            if (icon != null)
            {
                var g = new GameObject("Icon", typeof(Image));
                g.transform.SetParent(pill, false);
                var rt = g.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.06f, 0.18f); rt.anchorMax = new Vector2(0.26f, 0.82f);
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                var img = g.GetComponent<Image>();
                img.sprite = icon; img.preserveAspect = true; img.raycastTarget = false;
                textX0 = 0.30f;
            }

            long v = WalletValue(kind);
            Color valColor = kind == ElarionUiKit.CurrencyKind.Gold ? ElarionUi.Gilt : ElarionUi.Parchment;
            var val = ElarionUiKit.Label(pill, ElarionUi.CompactNumber(v), 0.10f, 0.90f,
                valColor, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.MidlineRight, textX0, 0.92f, bold: true);
            val.raycastTarget = false;
            ElarionUiKit.FitSingleLine(val);
            _pills.Add(new PillRef { Kind = kind, Value = val });
        }

        // WO-1195: CurrencyIconName is DELETED, not re-pointed. It was the repo's second
        // currency icon registry; the one authority is ElarionUiKit.ConceptIdFor + UiStyle.Icon.

        // Scan the VM's per-tile cost strings for the currency keywords this building spends.
        // Fixed display order (Gold primary first). Falls back to all five when nothing parses.
        private List<ElarionUiKit.CurrencyKind> DeriveSpendableCurrencies()
        {
            bool gold = false, wood = false, food = false, iron = false, crystal = false;
            if (_vm != null && _vm.Perks != null)
            {
                foreach (var item in _vm.Perks)
                {
                    string cost = _vm.CostFor(item.Id);
                    if (string.IsNullOrEmpty(cost)) continue;
                    string c = cost.ToLowerInvariant();
                    if (c.Contains("gold"))    gold = true;
                    if (c.Contains("wood"))    wood = true;
                    if (c.Contains("food"))    food = true;
                    if (c.Contains("iron"))    iron = true;
                    if (c.Contains("crystal")) crystal = true;
                }
            }

            var list = new List<ElarionUiKit.CurrencyKind>();
            if (gold)    list.Add(ElarionUiKit.CurrencyKind.Gold);
            if (wood)    list.Add(ElarionUiKit.CurrencyKind.Wood);
            if (food)    list.Add(ElarionUiKit.CurrencyKind.Food);
            if (iron)    list.Add(ElarionUiKit.CurrencyKind.Iron);
            if (crystal) list.Add(ElarionUiKit.CurrencyKind.Crystal);

            if (list.Count == 0)
            {
                list.Add(ElarionUiKit.CurrencyKind.Gold);
                list.Add(ElarionUiKit.CurrencyKind.Wood);
                list.Add(ElarionUiKit.CurrencyKind.Food);
                list.Add(ElarionUiKit.CurrencyKind.Crystal);
            }
            return list;
        }

        private long WalletValue(ElarionUiKit.CurrencyKind kind)
        {
            if (_vm == null) return 0;
            switch (kind)
            {
                case ElarionUiKit.CurrencyKind.Gold:    return _vm.Coins;
                case ElarionUiKit.CurrencyKind.Wood:    return _vm.Wood;
                case ElarionUiKit.CurrencyKind.Food:    return _vm.Food;
                case ElarionUiKit.CurrencyKind.Iron:    return _vm.Iron;
                case ElarionUiKit.CurrencyKind.Crystal: return _vm.Crystals;
                default:                                return 0;
            }
        }

        // ── Tab row (dark plates; selected = gold underline + gold text, WO-832) ──

        /// <summary>
        /// True when this building has ANY research perk to show. A placed structure (tower /
        /// wall / container) has NO perk data in building-tiers.json at all, so its Skills tab
        /// would render nothing but the "No research skills" note — an empty tab is a worse
        /// divergence from "the others" than a hidden one, so the tab is not built. The perk SET
        /// is static per building (only its owned/affordable state moves), so deciding once at
        /// chrome-build time can never go stale while the panel is open.
        /// </summary>
        private bool HasSkills()
        {
            if (_vm == null) return false;
            foreach (var item in _vm.Perks)
                if (item.Id != null && item.Id.StartsWith("perk:")) return true;
            return false;
        }

        private void BuildTabs(RectTransform host)
        {
            _tabs.Clear();
            string[] labels = HasSkills()
                ? new[] { "Upgrade", "Skills" }
                : new[] { "Upgrade" };
            const float gap = 0.012f;
            for (int i = 0; i < labels.Length; i++)
            {
                float x0 = (float)i / labels.Length + (i == 0 ? 0f : gap * 0.5f);
                float x1 = (float)(i + 1) / labels.Length - (i == labels.Length - 1 ? 0f : gap * 0.5f);
                RectTransform fill = RoundedCard(host, "Tab_" + labels[i],
                    new Vector2(x0, 0.06f), new Vector2(x1, 0.94f), TabDark, BorderDim, 1.5f);
                var root = fill.parent as RectTransform;   // bordered outer carries the button
                var btn = root.gameObject.AddComponent<Button>();
                btn.targetGraphic = root.GetComponent<Image>();
                ElarionUiKit.StyleButtonColors(btn);
                SoftenButton(btn);
                int idx = i;
                btn.onClick.AddListener(() => OnTab(idx));
                var lbl = ElarionUiKit.Label(fill, labels[i], 0.10f, 0.90f,
                    ElarionUi.Parchment, ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f, bold: true);
                lbl.raycastTarget = false;
                ElarionUiKit.FitSingleLine(lbl);
                // WO-832: thin gold rule along the tab's bottom edge — the "selected"
                // indicator (shown only for the active tab; RestyleTabs toggles it).
                var underGo = ElarionUiKit.AddImage(fill, "SelectedUnderline",
                    new Vector2(0.08f, 0.04f), new Vector2(0.92f, 0.10f), ElarionUi.Gilt);
                var under = underGo.GetComponent<Image>();
                under.raycastTarget = false;
                _tabs.Add(new TabRef { Fill = fill.GetComponent<Image>(), Underline = under, Label = lbl });
            }
            RestyleTabs();
        }

        private void RestyleTabs()
        {
            // WO-832 one-true-button: tabs are NAVIGATION, never the CTA's gold fill.
            // The selected tab reads as a TAB — dark plate + gold underline + gold text.
            // ElarionUi.GoldButton (solid bright fill) is reserved EXCLUSIVELY for the
            // right-pane commit CTA (BuildDetailCta).
            for (int i = 0; i < _tabs.Count; i++)
            {
                bool sel = i == _activeTab;
                if (_tabs[i].Fill != null)
                    _tabs[i].Fill.color = TabDark;
                if (_tabs[i].Underline != null)
                    _tabs[i].Underline.enabled = sel;
                if (_tabs[i].Label != null)
                    _tabs[i].Label.color = sel ? ElarionUi.Gilt : ElarionUi.Parchment;
            }
        }

        private void OnTab(int index)
        {
            _activeTab = Mathf.Clamp(index, 0, Mathf.Max(0, _tabs.Count - 1));
            FlowTrace.Step("UpgradeUI", "tab -> " + (_activeTab == 0 ? "Upgrade" : "Skills"));
            RestyleTabs();
            ApplyTabVisibility();
        }

        private void ApplyTabVisibility()
        {
            if (_upgradePage != null) _upgradePage.SetActive(_activeTab == 0);
            if (_skillsPage  != null) _skillsPage.SetActive(_activeTab == 1);
        }

        // ── Upgrade page — WO-895 ONE full-width column (progress strip + next card) ──

        private GameObject BuildUpgradePage(Transform parent)
        {
            var page = new GameObject("UpgradePage", typeof(RectTransform));
            page.transform.SetParent(parent, false);
            var prt = (RectTransform)page.transform;
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;

            // 1. PROGRESS STRIP — a FIXED-pixel band hung off the page's TOP edge, so it
            //    never scales into an under-height band at a real aspect (WO-832 §4 lesson).
            _progressHost = MakeZone(page.transform, "ProgressStrip", new Vector2(0f, 1f), new Vector2(1f, 1f));
            _progressHost.offsetMin = new Vector2(0f, -ProgressStripPx);
            _progressHost.offsetMax = Vector2.zero;

            // 2. MODEL COLUMN — the left band that holds the live 3D render. Built empty here
            //    and filled (or left unbuilt, and the card widened) by RebuildUpgrade once the
            //    VM says which model resolves.
            _previewHost = MakeZone(page.transform, "ModelBand",
                new Vector2(0f, 0f), new Vector2(PreviewColumnWidth, 1f));
            _previewHost.offsetMax = new Vector2(0f, -(ProgressStripPx + BandGapPx * 2f));

            // 3. NEXT-UPGRADE CARD — everything below the strip, right of the model column.
            //    Full width when no model resolved (the old WO-895 geometry, unchanged).
            _nextCardHost = MakeZone(page.transform, "NextCardHost", Vector2.zero, Vector2.one);
            _nextCardHost.offsetMax = new Vector2(0f, -(ProgressStripPx + BandGapPx * 2f));

            return page;
        }

        // Repaint the whole Upgrade tab from vm.* — WO-895: progress strip + NEXT card only.
        private void RebuildUpgrade()
        {
            if (_vm == null || _progressHost == null || _nextCardHost == null) return;

            // WO-841: the rebuild tears the button down — drop the cached countdown label +
            // fill (the in-progress branch re-stashes fresh ones when still building).
            _ctaCountdownLabel = null;
            _ctaProgressFill = null;
            _ctaCountdownLastSec = -1;

            ClearChildren(_progressHost);
            ClearChildren(_nextCardHost);

            // The model band FIRST: it decides whether the card is full-width or right-column,
            // so the card must be laid out after it (§2 Guarded — a bad prefab logs and the page
            // still renders its text, it never blanks the panel).
            Guard.Try("UpgradeUI", "build model band for '" + _vm.Title + "'", RebuildPreview);

            if (_vm.MaxTier <= 0)
            {
                // A plain anchored label, NOT the layout-group EmptyNote (this host is a raw
                // zone — a LayoutElement-sized note would collapse to zero here and read as a
                // blank panel, the exact "shows nothing" failure §12 forbids).
                var note = ElarionUiKit.Label(_nextCardHost, "This building has no enhancement path yet.",
                    0.42f, 0.58f, ElarionUi.ParchmentDim, ElarionUi.FontBody,
                    TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f);
                note.raycastTarget = false;
                ElarionUiKit.FitBlock(note);
                FlowTrace.Warn("UpgradeUI", "'" + _vm.Title + "' has NO ladder (MaxTier=0) - empty note shown");
                return;
            }

            // §2 Guard: one bad catalog row must log + skip, never blank the whole panel.
            Guard.Try("UpgradeUI", "build progress strip for '" + _vm.Title + "'", BuildProgressStrip);
            Guard.Try("UpgradeUI", "build next-upgrade card for '" + _vm.Title + "'", BuildNextCard);
        }

        // ── 1. PROGRESS STRIP — "Tier N of M" + segmented bar + "Now: <tier name>" ──
        // Colourblind law: a filled segment is a SOLID full-height block, an unowned one a
        // thin hollow rule. The difference is SHAPE + luminance, never hue.

        private void BuildProgressStrip()
        {
            int cur = Mathf.Clamp(_vm.CurrentTier, 0, _vm.MaxTier);
            int max = Mathf.Max(1, _vm.MaxTier);
            string word = _vm.TierWord;

            var head = ElarionUiKit.Label(_progressHost, word + " " + cur + " of " + max, 0.06f, 0.94f,
                ElarionUi.Gilt, ElarionUi.FontBody, TMPro.TextAlignmentOptions.MidlineLeft,
                0.005f, 0.20f, bold: true);
            head.raycastTarget = false;
            ElarionUiKit.FitSingleLine(head);

            // The segmented bar.
            var barZone = MakeZone(_progressHost, "Segments", new Vector2(0.215f, 0.22f), new Vector2(0.63f, 0.78f));
            const float segGap = 0.008f;
            float segW = (1f - segGap * (max - 1)) / max;
            for (int i = 0; i < max; i++)
            {
                float x0 = i * (segW + segGap);
                bool owned = i < cur;
                // Owned = solid full-height block; not-yet = a thin centred rule (shape signal).
                var seg = ElarionUiKit.AddImage(barZone, owned ? "SegOn" : "SegOff",
                    new Vector2(x0, owned ? 0f : 0.38f), new Vector2(x0 + segW, owned ? 1f : 0.62f),
                    owned ? ElarionUi.Gilt : new Color(0.34f, 0.33f, 0.31f, 0.9f));
                var segImg = seg.GetComponent<Image>();
                segImg.raycastTarget = false;
                ElarionUiKit.ApplyRounded(segImg);
            }

            string now = cur <= 0
                ? "Now: not yet enhanced"
                : "Now: " + (!string.IsNullOrEmpty(_vm.CurrentTierName) ? _vm.CurrentTierName : (word + " " + cur));
            var nowLbl = ElarionUiKit.Label(_progressHost, now, 0.06f, 0.94f,
                ElarionUi.ParchmentDim, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.MidlineRight,
                0.645f, 0.995f);
            nowLbl.raycastTarget = false;
            ElarionUiKit.FitSingleLine(nowLbl);

            FlowTrace.Step("UpgradeUI", "progress strip: " + word + " " + cur + "/" + max
                + " now='" + _vm.CurrentTierName + "'");
        }

        // ── 2. THE NEXT-UPGRADE CARD (the body) ───────────────────────────────────
        // Stands alone: a drill-in from the Manage screen lands HERE, so the card carries
        // its own subject (icon + tier + name), what it does, what it costs, and the one
        // button — with no rail around it to supply context.

        private void BuildNextCard()
        {
            RectTransform card = RoundedCard(_nextCardHost, "NextUpgradeCard",
                Vector2.zero, Vector2.one, SubPanelFill, BorderGoldDim, 2f);

            if (!_vm.HasNextUpgrade) { BuildMaxedCard(card); return; }

            // Bottom-up fixed bands: action button, cost row. The BONUS list takes whatever
            // is left between the description and the cost row — it is the content that
            // matters, so it gets the flex (never the header).
            float costBot = ActionBottomPx + ActionBandPx + CostGapPx;
            float bonusBot = costBot + CostBandPx + BandGapPx;

            // -- HEADER: icon + kicker + next tier NAME --------------------------
            var header = MakeZone(card, "NextHeader", new Vector2(0.03f, 1f), new Vector2(0.97f, 1f));
            header.offsetMin = new Vector2(0f, -(BandGapPx + NextHeaderBandPx));
            header.offsetMax = new Vector2(0f, -BandGapPx);

            var art = BuildingArt(_vm.NextTier);
            float textX0 = 0.01f;
            if (art != null)
            {
                var g = new GameObject("NextIcon", typeof(Image));
                g.transform.SetParent(header, false);
                var irt = g.GetComponent<RectTransform>();
                irt.anchorMin = new Vector2(0f, 0.04f); irt.anchorMax = new Vector2(0.075f, 0.96f);
                irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
                var img = g.GetComponent<Image>();
                img.sprite = art; img.preserveAspect = true; img.raycastTarget = false;
                textX0 = 0.095f;
            }

            var kicker = ElarionUiKit.Label(header,
                "NEXT UPGRADE - " + _vm.TierWord.ToUpperInvariant() + " " + _vm.NextTier,
                0.58f, 0.98f, ElarionUi.ParchmentDim, ElarionUi.FontLabel,
                TMPro.TextAlignmentOptions.BottomLeft, textX0, 1f, bold: true);
            kicker.characterSpacing = 5f;
            kicker.raycastTarget = false;
            ElarionUiKit.FitSingleLine(kicker);

            var nameLbl = ElarionUiKit.Label(header,
                !string.IsNullOrEmpty(_vm.NextTierName) ? _vm.NextTierName : (_vm.TierWord + " " + _vm.NextTier),
                0.02f, 0.56f, ElarionUi.Gilt, ElarionUi.FontHead,
                TMPro.TextAlignmentOptions.TopLeft, textX0, 1f, bold: true);
            nameLbl.raycastTarget = false;
            ElarionUiKit.FitBlock(nameLbl);

            // -- DESCRIPTION: a plain sentence, wraps, never truncates -------------
            var desc = ElarionUiKit.Label(card, _vm.NextDescription, 0f, 1f,
                ElarionUi.Parchment, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.TopLeft, 0.03f, 0.97f);
            desc.raycastTarget = false;
            ElarionUiKit.FitBlock(desc);
            PinBandFromTop(desc.rectTransform, BandGapPx * 2f + NextHeaderBandPx, NextDescBandPx);

            // -- BONUSES: one row each (never a single clipped run-on string) ------
            var bonusZone = MakeZone(card, "Bonuses", Vector2.zero, Vector2.one);
            bonusZone.offsetMin = new Vector2(0f, bonusBot);
            bonusZone.offsetMax = new Vector2(0f, -(BandGapPx * 3f + NextHeaderBandPx + NextDescBandPx));
            BuildBonusList(bonusZone);

            // -- COST ROW ----------------------------------------------------------
            var costZone = MakeZone(card, "CostRow", new Vector2(0.03f, 0f), new Vector2(0.97f, 0f));
            costZone.offsetMin = new Vector2(0f, costBot);
            costZone.offsetMax = new Vector2(0f, costBot + CostBandPx);
            BuildNextCostRow(costZone);

            // -- THE ONE ACTION BUTTON --------------------------------------------
            BuildActionButton(card);
        }

        // Max tier — "Fully enhanced". No action button at all (nothing to commit).
        private void BuildMaxedCard(RectTransform card)
        {
            var title = ElarionUiKit.Label(card, "Fully enhanced", 0.56f, 0.80f,
                ElarionUi.Gilt, ElarionUi.FontHead, TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f, bold: true);
            title.raycastTarget = false;
            ElarionUiKit.FitSingleLine(title);

            string body = _vm.Title + " has reached " + _vm.TierWord.ToLowerInvariant() + " "
                        + _vm.MaxTier + " of " + _vm.MaxTier + " - there is nothing left to upgrade here.";
            var note = ElarionUiKit.Label(card, body, 0.30f, 0.54f,
                ElarionUi.Parchment, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Top, 0.08f, 0.92f);
            note.raycastTarget = false;
            ElarionUiKit.FitBlock(note);

            TraceActionState(UpgradeActionState.Maxed);
        }

        // Bonus rows stack top-down in fixed-pixel bands; the loop stops when the next row
        // cannot FULLY seat (a dropped trailing row beats a mid-word cut — WO-832 §4).
        private void BuildBonusList(RectTransform zone)
        {
            var bonuses = _vm.NextBonuses;
            if (bonuses == null || bonuses.Count == 0)
            {
                var none = ElarionUiKit.Label(zone, "No listed bonuses for this tier.", 0f, 1f,
                    ElarionUi.ParchmentDim, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.TopLeft, 0.03f, 0.97f);
                none.raycastTarget = false;
                ElarionUiKit.FitSingleLine(none);
                PinBandFromTop(none.rectTransform, 0f, BonusRow1Px);
                return;
            }

            Canvas.ForceUpdateCanvases();           // settle the zone so rect.height is real
            float zoneH = zone.rect.height;
            if (zoneH < BonusRow1Px) zoneH = BonusRow1Px * 4f;   // pre-layout fallback

            float cursor = 0f;
            int shown = 0;
            for (int i = 0; i < bonuses.Count && shown < BonusMaxRows; i++)
            {
                string text = bonuses[i];
                if (string.IsNullOrEmpty(text)) continue;
                bool twoLine = text.Length > BonusLineChars;
                float rowPx = twoLine ? BonusRow2Px : BonusRow1Px;
                if (cursor + rowPx > zoneH) break;
                BuildBonusRow(zone, cursor, rowPx, twoLine, text);
                cursor += rowPx + BandGapPx * 0.5f;
                shown++;
            }

            if (shown < bonuses.Count)
                FlowTrace.Warn("UpgradeUI", "bonus list showed " + shown + " of " + bonuses.Count
                    + " rows (zone " + zoneH.ToString("0") + "px) - trailing rows dropped rather than clipped");
        }

        // One bonus line: a solid square bullet (SHAPE, not hue) + wrapping text.
        private void BuildBonusRow(RectTransform parent, float topPx, float heightPx, bool twoLine, string text)
        {
            var glyphSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleElement, RpgUiCatalog.ElementToggleBoxOn);
            var g = new GameObject("Bullet", typeof(Image));
            g.transform.SetParent(parent, false);
            var grt = g.GetComponent<RectTransform>();
            grt.anchorMin = new Vector2(0.03f, 1f); grt.anchorMax = new Vector2(0.055f, 1f);
            grt.offsetMin = new Vector2(0f, -(topPx + BonusRow1Px));
            grt.offsetMax = new Vector2(0f, -topPx);
            var gimg = g.GetComponent<Image>();
            if (glyphSprite != null) { gimg.sprite = glyphSprite; gimg.preserveAspect = true; }
            else ElarionUiKit.ApplyRounded(gimg);
            gimg.color = ElarionUi.Gilt;
            gimg.raycastTarget = false;

            var lbl = ElarionUiKit.Label(parent, text, 0f, 1f,
                ElarionUi.Parchment, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.TopLeft, 0.075f, 0.97f);
            lbl.raycastTarget = false;
            if (twoLine) ElarionUiKit.FitBlock(lbl); else ElarionUiKit.FitSingleLine(lbl);
            PinBandFromTop(lbl.rectTransform, topPx, heightPx);
        }

        // ── COST ROW — chips that name the shortfall IN WORDS ─────────────────────
        // Colourblind law: a short resource is NOT signalled by a red tint. Its chip carries
        // an empty-box glyph AND the words "need <n> more" — readable with colour removed.
        private void BuildNextCostRow(RectTransform zone)
        {
            var lines = _vm.NextCostLines;
            if (lines == null || lines.Count == 0)
            {
                var free = ElarionUiKit.Label(zone, "UPGRADE COST: free", 0f, 1f,
                    ElarionUi.ParchmentDim, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.MidlineLeft, 0f, 1f, bold: true);
                free.raycastTarget = false;
                ElarionUiKit.FitSingleLine(free);
                return;
            }

            // Caption on the LEFT of the same band as the chips (a stacked caption cost a
            // whole line the bonus rows needed).
            const float chipsX0 = 0.17f;
            var head = ElarionUiKit.Label(zone, "UPGRADE COST", 0f, 1f,
                ElarionUi.ParchmentDim, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.MidlineLeft, 0f, chipsX0 - 0.015f, bold: true);
            head.characterSpacing = 3f;
            head.raycastTarget = false;
            ElarionUiKit.FitSingleLine(head);

            int n = lines.Count;
            const float gap = 0.015f;
            float span = 1f - chipsX0;
            float cw = (span - gap * (n - 1)) / n;
            for (int i = 0; i < n; i++)
            {
                float x0 = chipsX0 + i * (cw + gap);
                BuildNextCostChip(zone, lines[i], x0, x0 + cw);
            }
        }

        private void BuildNextCostChip(RectTransform zone, BuildingUpgradeVM.UpgradeCostLine line, float x0, float x1)
        {
            RectTransform chip = RoundedCard(zone, "NextCost_" + line.Label,
                new Vector2(x0, 0.10f), new Vector2(x1, 0.90f), PillFill,
                line.Short ? BorderDim : BorderGoldDim, line.Short ? 1.5f : 2f);

            Sprite ic = UiStyle.Icon(line.ConceptId);
            float textX0 = 0.04f;
            if (ic != null)
            {
                var ig = new GameObject("Icon", typeof(Image));
                ig.transform.SetParent(chip, false);
                var irt = ig.GetComponent<RectTransform>();
                irt.anchorMin = new Vector2(0.02f, 0.14f); irt.anchorMax = new Vector2(0.14f, 0.86f);
                irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
                var iimg = ig.GetComponent<Image>();
                iimg.sprite = ic; iimg.preserveAspect = true; iimg.raycastTarget = false;
                textX0 = 0.17f;
            }

            // SHORTFALL MARKER — an EMPTY-BOX glyph at the chip's right edge (shape), paired
            // with the words below. Two colour-free signals; the tint is decoration only.
            float textX1 = 0.97f;
            if (line.Short)
            {
                var mark = RpgUiCatalog.Get(RpgUiCatalog.RoleElement, RpgUiCatalog.ElementToggleBoxOff);
                if (mark != null)
                {
                    var mg = new GameObject("ShortMark", typeof(Image));
                    mg.transform.SetParent(chip, false);
                    var mrt = mg.GetComponent<RectTransform>();
                    mrt.anchorMin = new Vector2(0.87f, 0.18f); mrt.anchorMax = new Vector2(0.98f, 0.82f);
                    mrt.offsetMin = Vector2.zero; mrt.offsetMax = Vector2.zero;
                    var mimg = mg.GetComponent<Image>();
                    mimg.sprite = mark; mimg.preserveAspect = true; mimg.raycastTarget = false;
                    mimg.color = new Color(0.72f, 0.70f, 0.66f, 1f);
                    textX1 = 0.85f;
                }
            }

            // The shortfall is TEXT, never a tint: "1.2k Wood - need 240 more".
            string text = ElarionUi.CompactNumber(line.Amount) + " " + line.Label;
            if (line.Short) text += " - need " + ElarionUi.CompactNumber(line.Missing) + " more";
            var lbl = ElarionUiKit.Label(chip, text, 0f, 1f,
                line.Short ? ElarionUi.ParchmentDim : ElarionUi.Parchment,
                ElarionUi.FontLabel, TMPro.TextAlignmentOptions.MidlineLeft, textX0, textX1, bold: !line.Short);
            lbl.raycastTarget = false;
            ElarionUiKit.FitSingleLine(lbl);
        }

        // ── THE ONE STATEFUL ACTION BUTTON (WO-895 §1b) ───────────────────────────

        private void BuildActionButton(RectTransform card)
        {
            var state = _vm.ActionState;
            int sec = _vm.ActionRemainingSeconds;
            string label = FormatActionLabel(state, sec, _vm.NextTierName,
                                             _vm.BuilderQueueDepth, _vm.BuilderQueueLimit);
            const float x0 = 0.03f, x1 = 0.97f;

            TraceActionState(state);

            // WO-1045 — QUEUE FULL: grey out, explain, offer (the owner's own order).
            if (state == UpgradeActionState.QueueFull) { BuildQueueFullBand(card, label, x0, x1); return; }

            // WO-1037 — MISSING RESOURCES: same shape, same order (dead state, then remedy).
            if (state == UpgradeActionState.MissingResources) { BuildShortfallBand(card, label, x0, x1); return; }

            if (state == UpgradeActionState.Ready)
            {
                // The ONE bright plate on the panel (WO-832) — and the ONLY interactable state.
                var root = BuildGoldButton(card, label, true, x0, x1, 0f, 1f, OnUpgradeTapped);
                AddStateGlyph(root, RpgUiCatalog.ElementArrowBoxOn, ElarionUi.Ink);
                PinActionBand(root);
                return;
            }

            if (state == UpgradeActionState.VillageGated)
            {
                // The next tier is behind the GLOBAL Village Tier. The one button raises THAT
                // (the SOLE VillageTierService caller path) instead of dead-ending the player.
                var root = BuildGoldButton(card, label + " " + (_vm.VillageTierNow + 1), true, x0, x1, 0f, 1f,
                    () =>
                    {
                        FlowTrace.Step("UpgradeUI", "raise-village tapped from " + _vm.Title
                            + " (need " + _vm.NextRequiresVillageTier + ", have " + _vm.VillageTierNow + ")");
                        _vm?.Select(BuildingUpgradeVM.VillageTierRowId);
                    });
                AddStateGlyph(root, RpgUiCatalog.ElementArrowBoxOn, ElarionUi.Ink);
                PinActionBand(root);
                return;
            }

            // Every remaining state is INERT and wears the dark plate. They are told apart by
            // unique TEXT + a unique GLYPH SHAPE (+ a fill bar for In progress) — never by hue.
            string glyph = state == UpgradeActionState.InProgress ? RpgUiCatalog.ElementHandle
                         : state == UpgradeActionState.Queued ? RpgUiCatalog.ElementArrowBox
                         : RpgUiCatalog.ElementToggleBoxOff;
            var lockLbl = BuildLockButton(card, label, x0, x1, 0f, 1f, null, glyph);
            var lockRoot = (RectTransform)lockLbl.transform.parent;
            PinActionBand(lockRoot);

            if (state == UpgradeActionState.InProgress)
            {
                // WO-841 live tick: cache the label so Update() rewrites ONLY its text, and add
                // the growing fill bar — the shape signal that separates it from Queued.
                _ctaCountdownLabel = lockLbl;
                _ctaCountdownLastSec = sec;

                var fill = ElarionUiKit.AddImage(lockRoot, "ProgressFill",
                    new Vector2(0f, 0f), new Vector2(Mathf.Clamp01(_vm.ActionProgress), 0f), ElarionUi.Gilt);
                var frt = (RectTransform)fill.transform;
                frt.offsetMin = Vector2.zero;
                frt.offsetMax = new Vector2(0f, ActionFillPx);
                var fimg = fill.GetComponent<Image>();
                fimg.raycastTarget = false;
                ElarionUiKit.ApplyRounded(fimg);
                _ctaProgressFill = frt;
            }
        }

        // ── WO-1045 — the QUEUE-FULL action band: GREY OUT, EXPLAIN, OFFER ────────
        //
        // THE DEFECT THIS RETIRES (owner, 2026-08-17): with 49k wood against a 108 cost the player
        // tapped "Upgrade to Level 2" and NOTHING happened. Not a disabled button, not a message —
        // a bright plate over a guaranteed refusal. Affordable-but-blocked was invisible, which is
        // the worst outcome available: the player cannot tell a broken game from a rule they have
        // not been told.
        //
        // LAYOUT: the band is ONE fixed CTA height. Splitting it VERTICALLY would halve two
        // controls below the touch minimum, so it splits HORIZONTALLY and reads left-to-right in
        // the owner's own order — the dead state on the left, the live remedy on the right. With no
        // remedy to offer, the dead state takes the full width and the explanation grows into it.
        //
        // COLOURBLIND LAW: the disabled half carries a WORD ("Queue full"), a NUMBER ("5 of 5"),
        // the service's own sentence, and a distinct GLYPH SHAPE. No hue is load-bearing, and no
        // colour accent is added "to help" — the greyscale read is the gate.
        private void BuildQueueFullBand(RectTransform card, string label, float x0, float x1)
        {
            string reason = _vm.ActionBlockedReason;
            bool canBuy = _vm.CanBuyQueueSlot;
            int price = _vm.QueueSlotPrice;
            bool offering = canBuy && price > 0;

            // The CREW line makes the two axes visibly different NUMBERS on one screen: crews are
            // how many run AT ONCE, the queue count is how many may be LINED UP. Without it a
            // player reading "5 of 5" has no way to know a slot purchase is about a different dial.
            string crews = string.Format(Copy(CopyKeyQueueCrews, "{0} of {1} crews working"),
                                         _vm.BuilderCrewsBusy, _vm.BuilderCrewSlots);

            // -- LEFT (or full width): the DEAD state, visibly disabled + explained -------
            // Composed in FULL before the button is built: BuildLockButton runs FitBlock, which
            // sizes the text to the string it is given, so assigning .text afterwards would leave
            // the fit stale and could clip the very explanation this band exists to show.
            float deadX1 = offering ? x0 + (x1 - x0) * 0.56f : x1;
            string dead = label;
            if (!string.IsNullOrEmpty(reason)) dead += "\n" + reason;
            dead += "\n" + crews;

            // With no purchasable remedy, say what WOULD unlock one — the service's Echo-gate
            // sentence ("Awaken a 3rd Echo...") is a real goal, so it goes on screen rather than
            // leaving the player at a wall with nothing to aim at.
            string why = offering ? "" : _vm.QueueSlotLockReason;
            if (!string.IsNullOrEmpty(why)) dead += "\n" + why;

            // onClick: null => Button.interactable stays FALSE. The plate is the dim lock plate,
            // never the gold CTA — the button cannot be mistaken for tappable, and cannot BE tapped.
            var lockLbl = BuildLockButton(card, dead, x0, deadX1, 0f, 1f, null,
                                          RpgUiCatalog.ElementCross);
            PinActionBand((RectTransform)lockLbl.transform.parent);

            if (!offering)
            {
                FlowTrace.Step("UpgradeUI", "queue-full band on '" + _vm.Title + "': no slot offer ("
                    + (string.IsNullOrEmpty(why) ? "no reason given" : why) + "); depth "
                    + _vm.BuilderQueueDepth + "/" + _vm.BuilderQueueLimit + " (DEPTH cap), crews "
                    + _vm.BuilderCrewsBusy + "/" + _vm.BuilderCrewSlots + " (concurrency, not the blocker).");
                return;
            }

            // -- RIGHT: the OFFER. Buying a slot raises the DEPTH limit as well as the crew
            //    count (QueueDepthLimit = authored + bought), so it genuinely unblocks THIS
            //    refusal — it is not an upsell aimed at the wrong axis.
            string offer = string.Format(Copy(CopyKeyQueueSlotOffer, "Buy a builder slot - {0} Crystals"),
                                         ElarionUi.CompactNumber(price));
            var offerRoot = BuildGoldButton(card, offer, true, deadX1 + 0.015f, x1, 0f, 1f,
                                            OnBuyQueueSlotTapped);
            AddStateGlyph(offerRoot, RpgUiCatalog.ElementArrowBoxOn, ElarionUi.Ink);
            PinActionBand(offerRoot);

            FlowTrace.Step("UpgradeUI", "queue-full band on '" + _vm.Title + "': depth "
                + _vm.BuilderQueueDepth + "/" + _vm.BuilderQueueLimit + " (DEPTH cap), crews "
                + _vm.BuilderCrewsBusy + "/" + _vm.BuilderCrewSlots + " (concurrency, not the blocker); "
                + "slot offer shown at " + price + " crystals.");
        }

        // The OFFER tap. Goes through the VM -> BuildTimerService.TryBuySlot, which applies the Echo
        // gate AND the crystal charge. Never GrantSlot/BuySlot (the [Obsolete] free grant).
        private void OnBuyQueueSlotTapped()
        {
            if (_vm == null) return;
            FlowTrace.Step("UpgradeUI", "SLOT-BUY TAP on '" + _vm.Title + "' (depth "
                + _vm.BuilderQueueDepth + "/" + _vm.BuilderQueueLimit + ", ask "
                + _vm.QueueSlotPrice + " crystals)");

            // The VM sets Status either way; Render() toasts a CHANGED status, so a refusal is
            // spoken aloud and can never be a silent no-op (§12). A success widens the depth limit,
            // which flips ActionState off QueueFull -> the signature changes -> this band is
            // replaced by the live Upgrade CTA in the same frame.
            Guard.Try("UpgradeUI", "buy builder slot from the upgrade panel", () => _vm.TryBuyQueueSlot());
        }

        // ── WO-1037 — the SHORTFALL action band: grey out, explain, offer ─────────
        //
        // The player opened an upgrade, read the perks, decided they want it, and hit a wall. That
        // is the highest-intent moment the economy produces, and this band answers the question they
        // are already asking ("how do I get past this?") instead of injecting a new one.
        //
        // THE GUARDRAILS ARE THE LAYOUT, not a comment about the layout (WO-1037 §1):
        //   * The HARVEST path takes the WIDER half and reads first. The pack is the alternative.
        //   * ONE offer, the SMALLEST SUFFICIENT size — chosen by ShortfallPackOffer, never here.
        //     No upsell rail, no "or go bigger" second plate.
        //   * It states exactly what it closes, in words and numbers ("Short 880 Wood").
        //   * DISMISSIBLE, and it stays dismissed for that building for the session.
        //
        // ⛔ THE OFFER PLATE IS INERT AND CANNOT PURCHASE — deliberately (WO-1037 §2 / WO-931).
        //    It is built with BuildLockButton, never BuildGoldButton: the dim lock plate cannot be
        //    mistaken for the gold CTA, and its only action is DISMISS. There is no call to
        //    PackStore.Purchase, no call to ApplyPackContents, and no path from this band to either.
        //    That is not a stub waiting to be finished by wiring the tap up — WO-931 shipped a
        //    tappable Buy over a free-granting stub wallet, and the fix is that the surface has no
        //    purchase route AT ALL until the owner opens FeatureFlags.RealmStorePurchase after a
        //    device wallet test. When that day comes, the routing is a NEW, gated change.
        private void BuildShortfallBand(RectTransform card, string label, float x0, float x1)
        {
            string worstLabel = _vm.WorstShortLabel;
            int worstMissing = _vm.WorstShortMissing;
            var offer = _vm.ShortfallOffer;
            bool dismissed = IsOfferDismissed();
            bool offering = offer.HasOffer && !dismissed;

            // -- LEFT (or full width): the DEAD state + the SHORTFALL IN WORDS + the harvest path.
            // Composed in FULL before the button is built: BuildLockButton runs FitBlock, which
            // sizes to the string it is handed, so a later .text assignment would leave a stale fit.
            float deadX1 = offering ? x0 + (x1 - x0) * 0.60f : x1;
            string dead = label;
            if (worstMissing > 0 && !string.IsNullOrEmpty(worstLabel))
            {
                dead += "\n" + string.Format(Copy(CopyKeyShortDetail, "Short {0} {1}"),
                                             ElarionUi.CompactNumber(worstMissing), worstLabel);
                dead += "\n" + string.Format(Copy(CopyKeyShortHarvest, "Harvest more {0}, or set an Echo to gather it."),
                                             worstLabel.ToLowerInvariant());
            }

            // onClick null => interactable false. The dead half is never tappable.
            var lockLbl = BuildLockButton(card, dead, x0, deadX1, 0f, 1f, null,
                                          RpgUiCatalog.ElementToggleBoxOff);
            PinActionBand((RectTransform)lockLbl.transform.parent);

            if (!offering)
            {
                FlowTrace.Step("UpgradeUI", "shortfall band on '" + _vm.Title + "': short "
                    + worstMissing + " " + worstLabel + "; no offer surfaced ("
                    + (dismissed ? "DISMISSED for this building this session"
                                 : "no impulse-pack family covers this resource") + ").");
                return;
            }

            // -- RIGHT: the OFFER. Information, not a transaction. It names the pack, the amount it
            //    grants and the price, so the player can judge it — and tapping it DISMISSES.
            string offerText = string.Format(
                Copy(CopyKeyShortPackOffer, "{0}\n{1} {2} - {3}"),
                offer.Pack.Name,
                ElarionUi.CompactNumber(offer.Amount),
                worstLabel,
                offer.PriceLabel);
            offerText += "\n" + Copy(CopyKeyShortPackSoon, "Coming soon - tap to dismiss");

            var offerLbl = BuildLockButton(card, offerText, deadX1 + 0.015f, x1, 0f, 1f,
                                           OnDismissShortfallOffer, RpgUiCatalog.ElementArrowBox);
            PinActionBand((RectTransform)offerLbl.transform.parent);

            FlowTrace.Step("UpgradeUI", "shortfall band on '" + _vm.Title + "': short " + worstMissing
                + " " + worstLabel + " -> offering '" + offer.Pack.Sku + "' (" + offer.Amount + " "
                + offer.ResourceKey + ", " + offer.PriceLabel + ", covers=" + offer.CoversShortfall
                + "). Plate is INERT: purchase rail "
                + (offer.Purchasable ? "OPEN but this surface still has NO purchase route (WO-1037 §2)"
                                     : "CLOSED (FeatureFlags.RealmStorePurchase off)") + ".");
        }

        // ── Session-scoped dismissal (WO-1037 §5: "stays dismissed for that upgrade that session") ──
        // Static so it survives the panel being closed and reopened on the same building, which is
        // what "that session" means to a player. Cleared only by a domain reload / a new run — it is
        // deliberately NOT persisted: a dismissal is a mood, not a setting, and burning it into the
        // save would silently hide the feature forever after one tap.
        private static readonly HashSet<string> _dismissedOffers = new HashSet<string>();

        private string OfferDismissKey() => _vm != null ? (_vm.Title ?? "") + "|" + _vm.NextTier : "";

        private bool IsOfferDismissed()
        {
            string k = OfferDismissKey();
            return !string.IsNullOrEmpty(k) && _dismissedOffers.Contains(k);
        }

        private void OnDismissShortfallOffer()
        {
            string k = OfferDismissKey();
            if (string.IsNullOrEmpty(k)) return;
            _dismissedOffers.Add(k);
            FlowTrace.Step("UpgradeUI", "shortfall offer DISMISSED for '" + k
                + "' - it will not resurface for this upgrade this session.");
            // The dismissal is part of the rendered content (ContentSignature reads it), so the
            // next Render tears the band down and rebuilds it full-width. No manual teardown.
            Render();
        }

        // The tap. The button flips IMMEDIATELY because the VM raises Changed -> Render ->
        // the signature (which includes ActionState) changes -> this button is rebuilt in the
        // new state. No dead click, no reopen needed (WO-895 §1b).
        private void OnUpgradeTapped()
        {
            if (_vm == null) return;
            FlowTrace.Step("UpgradeUI", "ACTION TAP on '" + _vm.Title + "' -> start "
                + _vm.TierWord.ToLowerInvariant() + "-" + _vm.NextTier
                + " (affordable=" + _vm.NextAffordable + ")");
            _vm.StartNextUpgrade();
        }

        private static void PinActionBand(RectTransform rt) => PinBandFromBottom(rt, ActionBottomPx, ActionBandPx);

        // A state glyph on the left of the bright CTA (shape signal on the Ready/gated states).
        private static void AddStateGlyph(RectTransform buttonRoot, string elementKey, Color tint)
        {
            var sprite = RpgUiCatalog.Get(RpgUiCatalog.RoleElement, elementKey);
            if (sprite == null || buttonRoot == null) return;
            var g = new GameObject("StateGlyph", typeof(Image));
            g.transform.SetParent(buttonRoot, false);
            var rt = g.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.02f, 0.24f); rt.anchorMax = new Vector2(0.075f, 0.76f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = g.GetComponent<Image>();
            img.sprite = sprite; img.preserveAspect = true; img.raycastTarget = false;
            img.color = tint;
        }

        // §12 — every button-state TRANSITION is traced with the data that decided it, so a
        // capture proves which state was shown and WHY (never re-theorised from code).
        private void TraceActionState(UpgradeActionState state)
        {
            if (_hasRenderedAction && state == _lastActionState) return;
            string why = "affordable=" + _vm.NextAffordable
                       + " villageGate=" + _vm.NextRequiresVillageTier + "/" + _vm.VillageTierNow
                       + " remainSec=" + _vm.ActionRemainingSeconds
                       + " progress=" + _vm.ActionProgress.ToString("0.00")
                       // WO-1045 — BOTH axes on every transition line, always labelled, so a capture
                       // can never leave "which limit did we hit?" to be re-theorised from code.
                       + " queueDepth=" + _vm.BuilderQueueDepth + "/" + _vm.BuilderQueueLimit
                       + " crews=" + _vm.BuilderCrewsBusy + "/" + _vm.BuilderCrewSlots;
            string line = "button '" + _vm.Title + "' " + (_hasRenderedAction ? _lastActionState.ToString() : "<open>")
                        + " -> " + state + " (" + why + ")";
            if (state == UpgradeActionState.MissingResources || state == UpgradeActionState.VillageGated
                || state == UpgradeActionState.QueueFull)
                FlowTrace.Warn("UpgradeUI", line);
            else
                FlowTrace.Step("UpgradeUI", line);
            _lastActionState = state;
            _hasRenderedAction = true;
        }

        // ── WO-832 §4 fixed-pixel band pins (RumorBoard pattern) ──────────────────
        // Re-hang an already-fraction-anchored control on its parent's TOP or BOTTOM
        // edge with a FIXED ref-pixel band. X anchors/offsets are preserved; only the
        // vertical seat changes, so bands never scale (and never under-height) again.

        private static void PinBandFromTop(RectTransform rt, float topPx, float heightPx)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(rt.anchorMin.x, 1f);
            rt.anchorMax = new Vector2(rt.anchorMax.x, 1f);
            rt.offsetMin = new Vector2(rt.offsetMin.x, -(topPx + heightPx));
            rt.offsetMax = new Vector2(rt.offsetMax.x, -topPx);
        }

        private static void PinBandFromBottom(RectTransform rt, float bottomPx, float heightPx)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(rt.anchorMin.x, 0f);
            rt.anchorMax = new Vector2(rt.anchorMax.x, 0f);
            rt.offsetMin = new Vector2(rt.offsetMin.x, bottomPx);
            rt.offsetMax = new Vector2(rt.offsetMax.x, bottomPx + heightPx);
        }

        // ── Shared clean buttons (gold CTA + dim lock) ────────────────────────────

        // Returns the button's root RectTransform so callers can re-pin it (WO-832 §4).
        private RectTransform BuildGoldButton(Transform parent, string label, bool enabled,
            float x0, float x1, float y0, float y1, System.Action onClick)
        {
            var go = ElarionUiKit.AddImage(parent, "GoldBtn", new Vector2(x0, y0), new Vector2(x1, y1),
                enabled ? ElarionUi.GoldButton : new Color(0.30f, 0.28f, 0.22f, 1f));
            var img = go.GetComponent<Image>();
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            ElarionUiKit.StyleButtonColors(btn);
            SoftenButton(btn);
            btn.interactable = enabled && onClick != null;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            var lbl = ElarionUiKit.Label(go.transform, label, 0.06f, 0.94f,
                enabled ? ElarionUi.Ink : ElarionUi.ParchmentDim,
                ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);
            lbl.raycastTarget = false;
            ElarionUiKit.FitSingleLine(lbl);
            return (RectTransform)go.transform;
        }

        // Returns the reason LABEL (a direct child of the button root) — WO-841 caches it
        // for the live countdown tick; callers reach the root via label.transform.parent.
        // WO-895: <paramref name="glyphKey"/> picks the state SHAPE (empty box = missing
        // resources, hollow arrow = queued, handle = in progress) — the colourblind-safe
        // signal that tells the inert states apart without any hue.
        private TMPro.TextMeshProUGUI BuildLockButton(Transform parent, string reason, float x0, float x1, float y0, float y1,
            System.Action onClick, string glyphKey = null)
        {
            var go = ElarionUiKit.AddImage(parent, "LockBtn", new Vector2(x0, y0), new Vector2(x1, y1),
                new Color(0.11f, 0.105f, 0.10f, 1f));
            var img = go.GetComponent<Image>();
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            ElarionUiKit.StyleButtonColors(btn);
            SoftenButton(btn);
            btn.interactable = onClick != null;
            btn.transition = Selectable.Transition.None;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            // Lock glyph (empty toggle box reads "not yet" — colorblind-safe shape) + text.
            var glyph = RpgUiCatalog.Get(RpgUiCatalog.RoleElement,
                string.IsNullOrEmpty(glyphKey) ? RpgUiCatalog.ElementToggleBoxOff : glyphKey);
            float textX0 = 0.06f;
            if (glyph != null)
            {
                var g = new GameObject("LockGlyph", typeof(Image));
                g.transform.SetParent(go.transform, false);
                var rt = g.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.04f, 0.22f); rt.anchorMax = new Vector2(0.16f, 0.78f);
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                var gi = g.GetComponent<Image>();
                gi.sprite = glyph; gi.preserveAspect = true; gi.raycastTarget = false;
                gi.color = new Color(0.62f, 0.60f, 0.56f, 1f);
                textX0 = 0.18f;
            }
            var lbl = ElarionUiKit.Label(go.transform, reason, 0.06f, 0.94f,
                ElarionUi.ParchmentDim, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.MidlineLeft, textX0, 0.96f);
            lbl.raycastTarget = false;
            ElarionUiKit.FitBlock(lbl);
            return lbl;
        }

        // ── Building illustration resolver (per-tier portrait; real art) ──────────
        // The building's Portraits/<slug>[-tier] sprite. Towers carry -2/-3 tier variants;
        // resource/city buildings reuse their single portrait (grown per tier by the card).
        private Sprite BuildingArt(int tierNum)
        {
            string title = _vm != null ? _vm.Title : "";
            if (string.IsNullOrEmpty(title)) return null;
            string t = title.Trim().ToLowerInvariant().Replace("'", "");
            string nospace = t.Replace(" ", "");
            string dash = t.Replace(' ', '-');

            if (tierNum >= 2)
            {
                var v = LoadPortrait(dash + "-" + tierNum);
                if (v == null) v = LoadPortrait(nospace + "-" + tierNum);
                if (v != null) return v;
            }
            var s = LoadPortrait(nospace);
            if (s == null) s = LoadPortrait(dash);
            return s;
        }

        // Portraits import as plain Texture2D (mirror BuildPaletteUI.LoadPortrait) — wrap once, cache.
        private static Sprite LoadPortrait(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            string path = "Portraits/" + key;
            if (_portraitCache.TryGetValue(path, out var cached)) return cached;

            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                var tex = Resources.Load<Texture2D>(path);
                if (tex != null)
                    sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            _portraitCache[path] = sprite;   // cache nulls too — one lookup per miss
            return sprite;
        }

        // ── Skills tab — the per-tier RESEARCH PERKS as a scroll list (unchanged) ──

        private void RebuildSkills()
        {
            if (_vm == null || _skillsContent == null) return;

            ClearChildren(_skillsContent);

            bool anyPerk = false;
            foreach (var item in _vm.Perks)
            {
                if (item.Id != null && item.Id.StartsWith("perk:"))
                {
                    CreateRow(_skillsContent, item);
                    anyPerk = true;
                }
            }

            if (!anyPerk)
                EmptyNote(_skillsContent, "No research skills for this building yet.");

            LayoutRebuilder.ForceRebuildLayoutImmediate(_skillsContent);
        }

        private GameObject BuildScrollPage(Transform parent, string name, out RectTransform content)
        {
            var page = new GameObject(name, typeof(RectTransform));
            page.transform.SetParent(parent, false);
            var prt = (RectTransform)page.transform;
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;

            var viewport = new GameObject("Viewport", typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewport.transform.SetParent(page.transform, false);
            var vr = viewport.GetComponent<RectTransform>();
            vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one;
            vr.offsetMin = Vector2.zero; vr.offsetMax = Vector2.zero;
            var vImg = viewport.GetComponent<Image>();
            vImg.color = new Color(0f, 0f, 0f, 0.001f);
            vImg.raycastTarget = true;   // drag-scroll target

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewport.transform, false);
            var cr = contentGo.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0f, 1f);
            cr.anchorMax = new Vector2(1f, 1f);
            cr.pivot = new Vector2(0.5f, 1f);
            cr.anchoredPosition = Vector2.zero;
            cr.sizeDelta = Vector2.zero;

            var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = RowGapPx;
            vlg.padding = new RectOffset(8, 8, 6, 10);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperCenter;

            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.GetComponent<ScrollRect>();
            scroll.viewport = vr;
            scroll.content = cr;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25f;

            content = cr;
            return page;
        }

        // One full-width perk ROW (Skills tab): icon | name + effect | cost chips + CTA / OWNED / reason.
        private void CreateRow(Transform parent, ItemVM item)
        {
            var row = new GameObject("Row_" + item.Id, typeof(Image), typeof(Button));
            row.transform.SetParent(parent, false);
            var le = row.AddComponent<LayoutElement>();
            le.minHeight = RowHeightPx;
            le.preferredHeight = RowHeightPx;

            var plate = row.GetComponent<Image>();
            DressRowPlate(plate);
            float dim = 1f;
            if (item.Equipped)
            {
                var c = plate.color;
                plate.color = new Color(c.r * 1.12f, c.g * 1.08f, c.b * 0.9f, c.a);
            }
            else if (item.Locked)
            {
                var c = plate.color;
                plate.color = new Color(c.r * 0.52f, c.g * 0.52f, c.b * 0.55f, c.a * 0.8f);
                dim = 0.6f;
            }

            bool purchasable = !item.Locked && !item.Equipped;

            var btn = row.GetComponent<Button>();
            btn.targetGraphic = plate;
            ElarionUiKit.StyleButtonColors(btn);
            SoftenButton(btn);
            btn.interactable = purchasable;
            if (!purchasable) btn.transition = Selectable.Transition.None;
            string id = item.Id;
            btn.onClick.AddListener(() => { FlowTrace.Step("UpgradeUI", "row-tap " + id); _vm?.Select(id); });

            Sprite icon = IconFor(item);
            if (icon != null)
            {
                var iconGo = new GameObject("Icon", typeof(Image));
                iconGo.transform.SetParent(row.transform, false);
                var irt = iconGo.GetComponent<RectTransform>();
                irt.anchorMin = new Vector2(0.015f, 0.14f); irt.anchorMax = new Vector2(0.11f, 0.86f);
                irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
                var iImg = iconGo.GetComponent<Image>();
                iImg.sprite = icon;
                iImg.preserveAspect = true;
                iImg.raycastTarget = false;
                iImg.color = new Color(1f, 1f, 1f, dim);
            }
            else
            {
                var g = ElarionUiKit.Label(row.transform, ElarionUi.CrestGlyph, 0.14f, 0.86f,
                    new Color(ElarionUi.Gilt.r, ElarionUi.Gilt.g, ElarionUi.Gilt.b, dim),
                    ElarionUi.FontTitle, TMPro.TextAlignmentOptions.Center, 0.015f, 0.11f, bold: true);
                g.raycastTarget = false;
                ElarionUiKit.FitSingleLine(g);
            }

            var nameLbl = ElarionUiKit.Label(row.transform, item.Name, 0.50f, 0.90f,
                new Color(ElarionUi.Parchment.r, ElarionUi.Parchment.g, ElarionUi.Parchment.b, dim),
                ElarionUi.FontBody, TMPro.TextAlignmentOptions.MidlineLeft, 0.135f, 0.49f, bold: true);
            nameLbl.raycastTarget = false;
            ElarionUiKit.FitSingleLine(nameLbl);

            string effect = _vm != null ? _vm.EffectFor(item.Id) : "";
            if (!string.IsNullOrEmpty(effect))
            {
                var effLbl = ElarionUiKit.Label(row.transform, effect, 0.12f, 0.47f,
                    new Color(ElarionUi.Gilt.r, ElarionUi.Gilt.g, ElarionUi.Gilt.b, 0.85f * dim),
                    ElarionUi.FontLabel, TMPro.TextAlignmentOptions.MidlineLeft, 0.135f, 0.49f);
                effLbl.raycastTarget = false;
                ElarionUiKit.FitSingleLine(effLbl);
            }

            if (item.Equipped)
            {
                var owned = ElarionUiKit.Label(row.transform, "OWNED", 0.30f, 0.70f,
                    ElarionUi.Gilt, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.66f, 0.985f, bold: true);
                owned.raycastTarget = false;
                ElarionUiKit.FitSingleLine(owned);
            }
            else if (item.Locked)
            {
                string reason = !string.IsNullOrEmpty(item.LockReason) ? item.LockReason : "Locked";
                var req = ElarionUiKit.Label(row.transform, reason, 0.14f, 0.86f,
                    ElarionUi.ParchmentDim, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.50f, 0.985f);
                req.raycastTarget = false;
                ElarionUiKit.FitBlock(req);
            }
            else
            {
                var cost = _vm != null ? _vm.CostPartsFor(item.Id) : System.Array.Empty<CostPart>();
                BuildCostChips(row.transform, cost, 0.50f, 0.795f);
                BuildRowCta(row.transform, "Research", item.Affordable,
                    () => { FlowTrace.Step("UpgradeUI", "research " + id); _vm?.Select(id); });
            }
        }

        // ── Inline cost chips (icon + number) — colorblind-safe ───────────────────

        private void BuildCostChips(Transform parent, IReadOnlyList<CostPart> parts, float x0, float x1)
        {
            int n = parts != null ? parts.Count : 0;
            if (n == 0) return;

            const float gap = 0.02f;
            float span = x1 - x0;
            float cw = (span - gap * (n - 1)) / n;
            if (cw <= 0f) cw = span / n;
            for (int i = 0; i < n; i++)
            {
                float cx0 = x0 + i * (cw + gap);
                BuildCostChip(parent, parts[i], cx0, cx0 + cw);
            }
        }

        private void BuildCostChip(Transform parent, CostPart part, float x0, float x1)
        {
            RectTransform chip = RoundedCard(parent, "CostChip",
                new Vector2(x0, 0.28f), new Vector2(x1, 0.72f), PillFill, BorderDim, 1.5f);

            Sprite ic = UiStyle.Icon(part.ConceptId);
            float textX0 = 0.10f;
            if (ic != null)
            {
                var ig = new GameObject("Icon", typeof(Image));
                ig.transform.SetParent(chip, false);
                var irt = ig.GetComponent<RectTransform>();
                irt.anchorMin = new Vector2(0.06f, 0.16f); irt.anchorMax = new Vector2(0.40f, 0.84f);
                irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
                var iimg = ig.GetComponent<Image>();
                iimg.sprite = ic; iimg.preserveAspect = true; iimg.raycastTarget = false;
                textX0 = 0.44f;
            }

            if (ic == null) FlowTrace.Once("CostFormat", "no-icon-" + part.ConceptId,
                "no icon for concept=" + part.ConceptId + "; using full-word fallback");
            string shown = ic != null ? part.AmountText : part.Word + " " + part.AmountText;
            var lbl = ElarionUiKit.Label(chip, shown, 0f, 1f,
                ElarionUi.Parchment, ElarionUi.FontLabel,
                ic != null ? TMPro.TextAlignmentOptions.MidlineLeft : TMPro.TextAlignmentOptions.Center,
                textX0, 0.94f, bold: true);
            lbl.raycastTarget = false;
            ElarionUiKit.FitSingleLine(lbl);
        }

        // Grey/white kit CTA seated at the row's right edge (Skills rows).
        private void BuildRowCta(Transform parent, string label, bool enabled, System.Action onClick)
        {
            var go = ElarionUiKit.AddImage(parent, "RowCta", new Vector2(0.815f, 0.16f), new Vector2(0.985f, 0.84f),
                new Color(0.13f, 0.125f, 0.115f, 1f));
            var plate = go.GetComponent<Image>();
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = plate;
            ElarionUiKit.StyleButtonColors(btn);
            SoftenButton(btn);
            btn.interactable = enabled;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var lbl = ElarionUiKit.Label(go.transform, label, 0.05f, 0.95f,
                ElarionUi.Parchment, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center,
                0.05f, 0.95f, bold: true);
            lbl.raycastTarget = false;
            ElarionUiKit.FitSingleLine(lbl);
        }

        private void EmptyNote(Transform parent, string text)
        {
            var go = new GameObject("EmptyNote", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = RowHeightPx;
            le.preferredHeight = RowHeightPx;
            var lbl = ElarionUiKit.Label(go.transform, text, 0.30f, 0.70f,
                ElarionUi.ParchmentDim, ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f);
            lbl.raycastTarget = false;
            ElarionUiKit.FitBlock(lbl);
        }

        // ── Icon / string helpers ─────────────────────────────────────────────────

        private Sprite IconFor(ItemVM item)
        {
            if (item.IconRole == BuildingUpgradeVM.IconRolePerk && !string.IsNullOrEmpty(item.IconName))
                return Resources.Load<Sprite>("HudIcons/BuildingUpgrades/" + item.IconName);
            if (item.IconRole == BuildingUpgradeVM.IconRoleTier
                && item.Id != null && item.Id.StartsWith("tier-"))
                return RpgUiCatalog.Get(RpgUiCatalog.RoleCrown, "tier" + Mathf.Clamp(TierNumber(item.Id), 1, 3));
            return null;
        }

        private static int TierNumber(string id)
        {
            int dash = id != null ? id.LastIndexOf('-') : -1;
            if (dash >= 0 && dash < id.Length - 1 && int.TryParse(id.Substring(dash + 1), out int n)) return n;
            return 1;
        }

        private static void DressRowPlate(Image plateImg)
        {
            if (plateImg == null) return;
            var plate = RpgUiCatalog.Get(RpgUiCatalog.RoleSlot, "slot_talent_1");
            if (plate != null)
            {
                plateImg.sprite = plate;
                plateImg.type   = Image.Type.Sliced;
                plateImg.color  = Color.white;
                return;
            }
            plateImg.color = new Color(0.078f, 0.073f, 0.066f, 1f);
            ElarionUiKit.ApplyRounded(plateImg);
        }

        private static void SoftenButton(Button btn)
        {
            if (btn == null || btn.transition != Selectable.Transition.ColorTint) return;
            var colors = btn.colors;
            colors.fadeDuration = ButtonFadeSec;
            btn.colors = colors;
        }

        // ── Shared primitive: a clean rounded card = border image + inset fill image ──
        // Returns the FILL RectTransform (content host); the bordered outer image is its parent.
        private static RectTransform RoundedCard(Transform parent, string name, Vector2 min, Vector2 max,
            Color fill, Color border, float borderPx)
        {
            var b = ElarionUiKit.AddImage(parent, name, min, max, border);   // outer = border ring
            var f = ElarionUiKit.AddImage(b.transform, "Fill", Vector2.zero, Vector2.one, fill);
            var frt = (RectTransform)f.transform;
            frt.offsetMin = new Vector2(borderPx, borderPx);
            frt.offsetMax = new Vector2(-borderPx, -borderPx);
            f.GetComponent<Image>().raycastTarget = false;
            return frt;
        }

        // ── Teardown ──────────────────────────────────────────────────────────────

        private static void ClearChildren(RectTransform host)
        {
            if (host == null) return;
            for (int i = host.childCount - 1; i >= 0; i--)
            {
                var c = host.GetChild(i);
                if (c != null) Destroy(c.gameObject);
            }
        }

        private void Close()
        {
            Unbind();
            _vm?.Dispose();
            _vm = null;
            _pills.Clear();
            _tabs.Clear();
            _lastStatus = null;
            _lastContentSig = null;   // fresh chrome next Open -> force the first rebuild
            _ctaCountdownLabel = null;   // WO-841 — label dies with the chrome
            _ctaProgressFill = null;
            _ctaCountdownLastSec = -1;
            _hasRenderedAction = false;  // WO-895 — next open re-traces the opening state
            // The model rig owns a RenderTexture + an off-screen camera/light/prefab instance:
            // it MUST be disposed with the chrome or every open leaks a rig (RotateModelMenu's
            // lifecycle, mirrored). Disposed BEFORE the canvas dies so the RawImage never holds
            // a released texture for a frame.
            DisposePreviewRig();
            if (_ui != null)
            {
                var fx = _ui.GetComponent<PanelOpenCloseFx>();
                if (fx != null && fx.isActiveAndEnabled) fx.PlayCloseAndDestroy();
                else Destroy(_ui);
            }
            _ui = null;
            _bodyHost = null;
            _upgradePage = null;
            _skillsPage = null;
            _progressHost = null;
            _nextCardHost = null;
            _skillsContent = null;
            _previewHost = null;
            PanelManager.NotifyClosed(_panelHandle);
        }

        // ── THE MODEL BAND — build / spin / dispose ──────────────────────────────
        // Lifecycle copied from the PROVEN uGUI precedent (RotateModelMenu:114-120 +
        // Update's per-frame ApplyRotation): URP will NOT auto-render an off-screen Base
        // camera, so TowerPreviewCamera keeps its camera disabled and renders on demand;
        // every SetRotation call IS the draw. Rebuilt only when the subject changes.
        private void RebuildPreview()
        {
            if (_vm == null || _previewHost == null) return;

            string id = _vm.PreviewId;
            int level = _vm.PreviewLevel;
            string key = (id ?? "") + "@" + level;
            if (key == _previewKey && _previewImage != null) return;   // same subject — keep the rig

            DisposePreviewRig();
            ClearChildren(_previewHost);
            _previewKey = key;

            GameObject prefab = null;
            DeNelle.Core.Catalog.OrientationFix orientation = null;
            bool resolved = Guard.Try("UpgradeUI", "resolve preview model for '" + (id ?? "?") + "'",
                () => StructurePreviewSource.TryResolve(id, level, out prefab, out orientation))
                && prefab != null;

            if (resolved)
            {
                _previewRig = new TowerPreviewCamera();
                // A rig that fails to build leaves Texture null — treated exactly like an
                // unresolved model (2D portrait, full-width card), never a black box.
                if (!Guard.Try("UpgradeUI", "begin preview rig", () => _previewRig.Begin(prefab, orientation))
                    || _previewRig.Texture == null)
                {
                    FlowTrace.Warn("UpgradeUI", "preview rig failed to start for '" + key
                        + "' - falling back to the 2D portrait");
                    DisposePreviewRig();
                    resolved = false;
                }
            }

            if (!resolved)
            {
                // No model column: give the card the full body width (the WO-895 geometry).
                if (_nextCardHost != null) _nextCardHost.anchorMin = new Vector2(0f, _nextCardHost.anchorMin.y);
                return;
            }

            // A framed viewport in the kit's chrome vocabulary (RoundedCard) with ONE RawImage
            // inside it — the same shape the rotate menu uses, no new widget invented.
            RectTransform frame = RoundedCard(_previewHost, "ModelViewport",
                new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f), SubPanelFill, BorderGoldDim, 2f);
            var viewGo = new GameObject("ModelView", typeof(RawImage));
            viewGo.transform.SetParent(frame, false);
            var vrt = (RectTransform)viewGo.transform;
            vrt.anchorMin = new Vector2(0.04f, 0.06f);
            vrt.anchorMax = new Vector2(0.96f, 0.94f);
            vrt.offsetMin = Vector2.zero; vrt.offsetMax = Vector2.zero;
            _previewImage = viewGo.GetComponent<RawImage>();
            _previewImage.texture = _previewRig.Texture;
            _previewImage.color = Color.white;       // show the RT unmodulated
            _previewImage.raycastTarget = false;

            _previewYaw = 0f;
            _previewRig.SetRotation(Quaternion.identity);

            if (_nextCardHost != null)
                _nextCardHost.anchorMin = new Vector2(PreviewColumnWidth, _nextCardHost.anchorMin.y);

            FlowTrace.Step("UpgradeUI", "model band built for '" + key + "'");
        }

        private void DisposePreviewRig()
        {
            if (_previewImage != null) _previewImage.texture = null;
            _previewImage = null;
            if (_previewRig != null) { _previewRig.Dispose(); _previewRig = null; }
            _previewKey = null;
        }

        private static RectTransform MakeZone(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return rt;
        }
    }

    /// <summary>
    /// DEPRECATED private twin (WO-714 P8): the kit now owns this tween as
    /// <c>ElarionUiKit.PanelOpenCloseFx</c> — new code uses the kit version; this copy
    /// is kept only so parallel lanes stay additive, and migrates on-touch. Ease-out
    /// scale 0.92-&gt;1 + fade-in on open (~0.18s); ease-in fade/scale-out then self-destroy
    /// on close (~0.14s). Unscaled time; CanvasGroup blocks input while closing.
    /// </summary>
    internal sealed class PanelOpenCloseFx : MonoBehaviour
    {
        private const float OpenSec  = 0.18f;
        private const float CloseSec = 0.14f;

        private CanvasGroup _group;
        private RectTransform _scaled;
        private bool _closing;

        public void PlayOpen(RectTransform scaleTarget)
        {
            _group = gameObject.GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _scaled = scaleTarget;
            _group.alpha = 0f;
            if (_scaled != null) _scaled.localScale = Vector3.one * 0.92f;
            StartCoroutine(Ease(open: true, OpenSec, onDone: null));
        }

        public void PlayCloseAndDestroy()
        {
            if (_closing) return;
            _closing = true;
            if (_group == null) _group = gameObject.GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;
            StartCoroutine(Ease(open: false, CloseSec, onDone: () => Destroy(gameObject)));
        }

        private IEnumerator Ease(bool open, float duration, System.Action onDone)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float x = Mathf.Clamp01(t / duration);
                float k = open ? 1f - Mathf.Pow(1f - x, 3f) : 1f - Mathf.Pow(x, 3f);
                if (_group != null) _group.alpha = k;
                if (_scaled != null)
                    _scaled.localScale = Vector3.one * Mathf.Lerp(open ? 0.92f : 0.94f, 1f, k);
                yield return null;
            }
            if (_group != null) _group.alpha = open ? 1f : 0f;
            if (_scaled != null && open) _scaled.localScale = Vector3.one;
            onDone?.Invoke();
        }
    }
}
