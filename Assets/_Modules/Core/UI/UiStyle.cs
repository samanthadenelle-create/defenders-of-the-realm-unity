// =============================================================================
// UiStyle — the ONE style authority every dumb View pulls from (owner's One-Model
// method applied to PRESENTATION: "make a styling-type SINGLETON for ONE UI style
// for EVERYTHING — not piece this and piece that").
//
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.UI
//
// It owns nothing new conceptually — it COMPOSES the three existing primitives
// (RpgUiCatalog sprites + ConceptIconResolver icons + a single UiTheme record of
// values, defaulting to ElarionUi) and exposes them as SEMANTIC TOKENS. Views and
// ElarionUiKit ask UiStyle for "the window frame" / "the slot for this state" /
// "the primary button" / "the locked colour" — never a raw hex, never a
// RpgUiCatalog.PanelX name, never an ff.blinkchrome branch at the call site.
//
// PHASE (a) — pure addition (Obsidian spec docs/UI/OBSIDIAN_UI_DESIGN_*.md §6.4a):
//   Nothing consumes this yet. ElarionUiKit + the panels are migrated to it in
//   later phases (b/c). Introducing it first is zero-risk: it only READS existing
//   Core.UI primitives. Every sprite lookup is null-safe (RpgUiCatalog/ConceptIcon
//   contract) so a missing themed sprite degrades to the kit's procedural fallback
//   — the migration can never blank a screen.
//
// The Style lever (§6.7): UiStyle.Try(Style.Obsidian) swaps the active UiTheme and
// reskins EVERYTHING at once (every screen reads UiStyle). ff.blinkchrome is read
// in ONE place here (UiStyle.Chrome), not branched per call site.
// =============================================================================

using UColor = UnityEngine.Color;
using UnityEngine;

namespace DeNelle.Core.UI
{
    /// <summary>A named, try-able whole-UI look. Each maps to one <see cref="UiTheme"/> record.</summary>
    public enum Style { Default, Obsidian }

    /// <summary>Semantic state of a slot/cell/node plate — the SAME states across inventory + skill tree.</summary>
    public enum SlotState { Empty, Filled, Selected, Equipped, Locked, Owned, Unlockable }

    /// <summary>Semantic button role — resolves to a sprite (chrome-aware) inside <see cref="UiStyle"/>.</summary>
    public enum ButtonRole { Primary, Neutral, Close, Danger }

    /// <summary>Semantic window-frame role.</summary>
    public enum FrameKind { Window, Vendor, Grid, Portrait, Quest }

    /// <summary>A base slot sprite + its semantic tint for one <see cref="SlotState"/>.</summary>
    public readonly struct PlateStyle
    {
        public readonly Sprite Slot;
        public readonly UColor Tint;
        public PlateStyle(Sprite slot, UColor tint) { Slot = slot; Tint = tint; }
        public void Deconstruct(out Sprite slot, out UColor tint) { slot = Slot; tint = Tint; }
    }

    /// <summary>
    /// The ONE swappable record holding EVERY token value: panel/slot/button sprite NAMES
    /// (strings resolved through RpgUiCatalog), semantic colours, font sizes, spacing. Swap the
    /// record (via <see cref="UiStyle.Try"/>) → the whole game reskins. Code-default now;
    /// JSON-backed later (Resources/Data/Canonical/ui-theme.&lt;style&gt;.json) once &gt;1 style
    /// exists — NOT a ScriptableObject (inspector drag-drop is banned; JSON matches the catalog
    /// pattern). Defaults below ARE today's correct per-panel values, named ONCE.
    /// </summary>
    [System.Serializable]
    public sealed class UiTheme
    {
        // ── Panel frame sprite names (RpgUiCatalog RolePanel) ──
        public string WindowPanel   = RpgUiCatalog.PanelWindowDark;
        public string VendorPanel   = RpgUiCatalog.PanelVendor;
        public string GridPanel     = RpgUiCatalog.PanelGrid;
        public string PortraitPanel = RpgUiCatalog.PanelPortrait;
        public string QuestPanel    = RpgUiCatalog.PanelQuest;

        // ── Slot/cell sprite names (RpgUiCatalog RoleSlot) ──
        public string SlotItem   = RpgUiCatalog.SlotItem;   // per-item inventory plate
        public string SlotTalent = "slot_talent";           // skill-node plate (null-safe until mirrored)

        // ── Button sprite names (RpgUiCatalog RoleButton) ──
        public string ButtonPrimaryChrome  = RpgUiCatalog.ButtonConfirm; // chrome ON  → green confirm/buy
        public string ButtonPrimaryDefault = RpgUiCatalog.ButtonGold;    // chrome OFF → gold scroll
        public string ButtonNeutral        = RpgUiCatalog.ButtonFrame;
        public string ButtonClose          = RpgUiCatalog.ButtonExit;

        // ── Semantic colours (named ONCE; defaults = ElarionUi) ──
        public UColor TextPrimary = ElarionUi.Parchment;
        public UColor TextDim     = ElarionUi.ParchmentDim;
        public UColor Accent      = ElarionUi.Gold;
        public UColor Aether      = ElarionUi.Aether;
        public UColor Danger      = ElarionUi.Danger;
        public UColor Affordable  = ElarionUi.Affordable;
        public UColor DisabledCol = ElarionUi.Disabled;
        // State base tints (alpha applied per-state by StatePlate; centralises the old magic alphas)
        // NOTE: seeded from the SAME literal as the kit's Cell token below (warm wood @0.84a). Kept as a
        // direct literal (not `Cell`) to avoid a static-init back-reference now that ElarionUiKit.Cell
        // RESOLVES FROM this record — reading the kit during this record's own construction would NRE.
        public UColor LockedBase     = new UColor(ElarionUi.PanelStone.r, ElarionUi.PanelStone.g, ElarionUi.PanelStone.b, 0.84f);
        public UColor OwnedBase      = ElarionUi.Affordable;
        public UColor UnlockableBase = ElarionUi.Gold;
        public UColor SelectedBase   = ElarionUi.Gold;
        public UColor EquippedBase   = ElarionUi.Affordable;

        // ── Solid panel fill behind the frame (neutralised to alpha-0 when Chrome) ──
        public UColor PanelFillSolid = ElarionUi.PanelStone;

        // ── Kit surface tokens (the dark-glass/wood language ElarionUiKit exposes) ──
        // Seeded with ElarionUiKit's EXACT current literals so routing the kit through this record is a
        // pure no-op visually. The kit's static color tokens now RESOLVE FROM these fields → UiStyle is
        // the single source of truth for the surface language; swap the record to reskin every surface.
        public UColor Glass         = new UColor(ElarionUi.PanelStoneDark.r, ElarionUi.PanelStoneDark.g, ElarionUi.PanelStoneDark.b, 0.66f);
        public UColor GlassDeep     = new UColor(ElarionUi.PanelStoneDark.r, ElarionUi.PanelStoneDark.g, ElarionUi.PanelStoneDark.b, 0.86f);
        public UColor Track         = new UColor(0.0f, 0.0f, 0.0f, 0.45f);
        public UColor Cell          = new UColor(ElarionUi.PanelStone.r, ElarionUi.PanelStone.g, ElarionUi.PanelStone.b, 0.84f);
        // OBSIDIAN CANON (WO-562): neutralised off the old warm-wood literals to dark neutrals so the
        // kit's selected-cell + niche read black, matching the obsidian panel language (the other kit
        // tokens above already follow ElarionUi.PanelStone/Dark which are now obsidian black).
        public UColor CellSelected  = new UColor(0.12f, 0.12f, 0.14f, 0.95f);
        public UColor StoneNiche    = new UColor(0.030f, 0.030f, 0.038f, 0.96f);
        public UColor AccentLine    = new UColor(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.85f);
        public UColor AccentSoft    = new UColor(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.30f);
        public UColor PortraitPlaceholder = new UColor(0.74f, 0.66f, 0.50f, 1f);

        // ── Typography (re-export of ElarionUi ladder — kills magic +4/+2 deltas) ──
        public int FontTitle   = ElarionUi.FontTitle;
        public int FontHeader  = ElarionUi.FontHead;
        public int FontBody    = ElarionUi.FontBody;
        public int FontCaption = ElarionUi.FontLabel;
        public int FontMicro   = ElarionUi.FontMicro;

        // ── Spacing / shape (kills the inline Vector2(78,72) cell literal) ──
        public float PadSm = ElarionUi.PadCard * 0.5f;
        public float PadMd = ElarionUi.PadCard;
        public float PadLg = ElarionUi.PadPanel;
        public float RadiusSm = ElarionUi.RadiusSm;
        public float RadiusMd = ElarionUi.RadiusMd;
        public float RadiusLg = ElarionUi.RadiusLg;
        public float TapTarget = ElarionUi.TapTarget;
        public Vector2 CellSize = new Vector2(78f, 72f);

        // ── Default icon concept (when a View has no specific concept) ──
        public string DefaultIconConcept = "inventory";

        /// <summary>Today's parchment/stone values — the neutral default look.</summary>
        public static UiTheme ForDefault() => new UiTheme();

        /// <summary>
        /// The Obsidian dark-fantasy token set (spec §2-§5). Diverges from Default by leaning the
        /// slot plate onto the Obsidian Talent border family and emphasising the gold hero state.
        /// Most frame ids already match today (the project is Obsidian-leaning), so this stays honest
        /// — the lever exists for a future Parchment record to diverge fully.
        /// </summary>
        public static UiTheme ForObsidian()
        {
            return new UiTheme
            {
                WindowPanel = RpgUiCatalog.PanelWindowDark,
                SlotItem    = RpgUiCatalog.SlotItem,
                SlotTalent  = "slot_talent",
            };
        }

        public static UiTheme For(Style style)
        {
            switch (style)
            {
                case Style.Obsidian: return ForObsidian();
                default:             return ForDefault();
            }
        }
    }

    /// <summary>
    /// Static facade — the single style authority. Views + ElarionUiKit call ONLY UiStyle.*; no raw
    /// hex, no RpgUiCatalog.PanelX, no ff.blinkchrome branch survives at a call site.
    /// </summary>
    public static class UiStyle
    {
        // ── The active style + record (set once at boot; swapped by Try) ──
        public static Style Active { get; private set; } = Style.Default;
        public static UiTheme Theme { get; private set; } = UiTheme.ForDefault();

        /// <summary>The ff.blinkchrome gate, read in exactly ONE place in the whole codebase.</summary>
        public static bool Chrome => FeatureFlags.BlinkChrome;

        /// <summary>Raised by <see cref="Try"/>; open panels re-run Render() to repaint, closed panels pick it up on next Open().</summary>
        public static event System.Action Changed;

        /// <summary>
        /// Swap the active style → load that style's UiTheme → set Active → raise Changed.
        /// One call, whole-UI swap. This is the owner's "try() it" lever at the global level.
        /// </summary>
        public static void Try(Style style)
        {
            Active = style;
            Theme = UiTheme.For(style);
            var handler = Changed;
            if (handler != null) handler();
        }

        // ── Frames ──
        public static class Frame
        {
            public static Sprite Window   => RpgUiCatalog.Get(RpgUiCatalog.RolePanel, Theme.WindowPanel);
            public static Sprite Vendor   => RpgUiCatalog.Get(RpgUiCatalog.RolePanel, Theme.VendorPanel);
            public static Sprite Grid     => RpgUiCatalog.Get(RpgUiCatalog.RolePanel, Theme.GridPanel);
            public static Sprite Portrait => RpgUiCatalog.Get(RpgUiCatalog.RolePanel, Theme.PortraitPanel);
            public static Sprite Quest    => RpgUiCatalog.Get(RpgUiCatalog.RolePanel, Theme.QuestPanel);

            public static Sprite Of(FrameKind kind)
            {
                switch (kind)
                {
                    case FrameKind.Vendor:   return Vendor;
                    case FrameKind.Grid:     return Grid;
                    case FrameKind.Portrait: return Portrait;
                    case FrameKind.Quest:    return Quest;
                    default:                 return Window;
                }
            }
        }

        /// <summary>The slot sprite for a state. Pass an explicit slot name (e.g. SlotTalent) when the
        /// surface is a skill node rather than an inventory cell; defaults to the per-item slot.</summary>
        public static Sprite Slot(SlotState state, string slotName = null)
        {
            string name = slotName ?? Theme.SlotItem;
            return RpgUiCatalog.Get(RpgUiCatalog.RoleSlot, name);
        }

        /// <summary>The button sprite for a role — the chrome branch dies HERE.</summary>
        public static Sprite Button(ButtonRole role)
        {
            switch (role)
            {
                case ButtonRole.Primary:
                    return RpgUiCatalog.Get(RpgUiCatalog.RoleButton,
                        Chrome ? Theme.ButtonPrimaryChrome : Theme.ButtonPrimaryDefault);
                case ButtonRole.Close:
                    return RpgUiCatalog.Get(RpgUiCatalog.RoleButton, Theme.ButtonClose);
                case ButtonRole.Danger:
                case ButtonRole.Neutral:
                default:
                    return RpgUiCatalog.Get(RpgUiCatalog.RoleButton, Theme.ButtonNeutral);
            }
        }

        // ── Semantic colours ──
        public static class Color
        {
            public static UColor TextPrimary => Theme.TextPrimary;
            public static UColor TextDim     => Theme.TextDim;
            public static UColor Accent      => Theme.Accent;
            public static UColor Aether      => Theme.Aether;
            public static UColor Danger      => Theme.Danger;
            public static UColor Affordable  => Theme.Affordable;
            public static UColor Disabled    => Theme.DisabledCol;

            public static UColor Locked      => Theme.LockedBase;
            public static UColor Owned       => Theme.OwnedBase;
            public static UColor Unlockable  => Theme.UnlockableBase;
            public static UColor Selected    => Theme.SelectedBase;
            public static UColor Equipped    => Theme.EquippedBase;

            /// <summary>
            /// The solid fill painted behind a framed window. Returns alpha-0 when Chrome is ON (so
            /// the bare Obsidian sprite reads clean) and the solid stone fill otherwise. The gate
            /// lives HERE — no panel branches on ff.blinkchrome.
            /// </summary>
            public static UColor PanelFill(bool chromeAware = true)
            {
                var c = Theme.PanelFillSolid;
                if (chromeAware && Chrome) c.a = 0f;
                return c;
            }
        }

        // ── Typography ──
        public static class Font
        {
            public static int Title   => Theme.FontTitle;
            public static int Header  => Theme.FontHeader;
            public static int Body    => Theme.FontBody;
            public static int Caption => Theme.FontCaption;
            public static int Micro   => Theme.FontMicro;
        }

        // ── Spacing / shape ──
        public static class Pad
        {
            public static float Sm => Theme.PadSm;
            public static float Md => Theme.PadMd;
            public static float Lg => Theme.PadLg;
        }

        public static class Radius
        {
            public static float Sm => Theme.RadiusSm;
            public static float Md => Theme.RadiusMd;
            public static float Lg => Theme.RadiusLg;
        }

        public static Vector2 CellSize => Theme.CellSize;
        public static float TapTarget  => Theme.TapTarget;

        /// <summary>Resolve a concept icon (a View asks "icon for concept X", never names a sprite).
        /// Wraps ConceptIconResolver; null-safe (caller keeps its glyph fallback).</summary>
        public static Sprite Icon(string conceptId, params string[] fallbackConcepts)
        {
            if (fallbackConcepts == null || fallbackConcepts.Length == 0)
                return ConceptIconResolver.Resolve(conceptId);

            var ids = new string[fallbackConcepts.Length + 1];
            ids[0] = conceptId;
            for (int i = 0; i < fallbackConcepts.Length; i++) ids[i + 1] = fallbackConcepts[i];
            return ConceptIconResolver.ResolveAny(ids);
        }

        /// <summary>
        /// The node/cell state in ONE call: base slot sprite + the semantic tint (with the canonical
        /// per-state alpha centralised here — the old per-panel magic literals). Rarity etc. blend
        /// OVER this as a data overlay (it is data, not a theme token). Pass slotName=SlotTalent for
        /// skill-tree nodes.
        /// </summary>
        public static PlateStyle StatePlate(SlotState state, string slotName = null)
        {
            Sprite slot = Slot(state, slotName);
            UColor tint;
            switch (state)
            {
                case SlotState.Owned:      tint = WithAlpha(Theme.OwnedBase, 0.22f); break;
                case SlotState.Unlockable: tint = WithAlpha(Theme.UnlockableBase, 1f); break;   // hero state, full
                case SlotState.Selected:   tint = WithAlpha(Theme.SelectedBase, 1f); break;
                case SlotState.Equipped:   tint = WithAlpha(Theme.EquippedBase, 1f); break;
                case SlotState.Locked:     tint = WithAlpha(Theme.LockedBase, 0.40f); break;
                case SlotState.Empty:      tint = WithAlpha(Theme.LockedBase, 0.25f); break;
                case SlotState.Filled:
                default:                   tint = UColor.white; break;
            }
            return new PlateStyle(slot, tint);
        }

        private static UColor WithAlpha(UColor c, float a) { c.a = a; return c; }
    }
}
