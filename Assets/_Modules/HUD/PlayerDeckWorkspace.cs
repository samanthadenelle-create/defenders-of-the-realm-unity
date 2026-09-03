using System;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.HudModel;   // WO-1357: PostureSignals.RaidCapable / RaidLock - the ONE raid predicate
using DeNelle.Core.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.HUD
{
    public enum PlayerDeckKind { Realm, Hero, Journey }

    public sealed class PlayerDeckPage
    {
        public PlayerDeckKind Kind { get; }
        public PlayerDeckPage(PlayerDeckKind kind) => Kind = kind;
    }

    /// <summary>One shared card workspace for the three recognition-heavy player domains.</summary>
    public sealed class PlayerDeckWorkspace : ObsidianNavigationWorkspace<PlayerDeckPage>
    {
        private sealed class Card
        {
            public string Title;
            public string Purpose;
            public string Concept;
            public string ArtKey;
            public Func<bool> Available;
            /// <summary>
            /// WO-1357 — the SPECIFIC reason this card is locked, evaluated at render. Null
            /// (or a null/empty return) falls back to the generic "Complete its requirement
            /// first" line below. A locked card that only says LOCKED is a dead end; one that
            /// names its remedy teaches the next goal, which is why this exists.
            /// </summary>
            public Func<string> LockReason;
            public Action Open;
        }

        private static PlayerDeckWorkspace _instance;
        protected override string WorkspaceName => "Player Deck";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_instance != null) return;
            var go = new GameObject("Player Deck Workspace");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<PlayerDeckWorkspace>();
        }

        protected override void Awake()
        {
            base.Awake();
            _instance = this;
            PanelRouter.Register(PanelId.RealmDeck, OpenRealm);
            PanelRouter.Register(PanelId.HeroDeck, OpenHero);
            PanelRouter.Register(PanelId.JourneyDeck, OpenJourney);
        }

        private void OpenRealm() => Open(new PlayerDeckPage(PlayerDeckKind.Realm));
        private void OpenHero() => Open(new PlayerDeckPage(PlayerDeckKind.Hero));
        private void OpenJourney() => Open(new PlayerDeckPage(PlayerDeckKind.Journey));

        protected override string TitleFor(PlayerDeckPage page) => page.Kind.ToString();

        protected override string SubtitleFor(PlayerDeckPage page)
        {
            switch (page.Kind)
            {
                case PlayerDeckKind.Realm: return "Realm services, records, and guidance.";
                case PlayerDeckKind.Hero: return "Your equipment, inventory, skills, and loadout.";
                default: return "Quests and raids.";
            }
        }

        protected override void RenderPage(PlayerDeckPage page, RectTransform content)
        {
            var cards = CardsFor(page.Kind);
            var gridGo = new GameObject(page.Kind + "CardGrid", typeof(RectTransform), typeof(GridLayoutGroup));
            var grid = (RectTransform)gridGo.transform;
            grid.SetParent(content, false);
            grid.anchorMin = new Vector2(0.02f, 0.03f);
            // Reserve the upper body band for the workspace purpose line. The first
            // measured capture proved a .97 top edge let row one cover that line.
            grid.anchorMax = new Vector2(0.98f, 0.82f);
            grid.offsetMin = grid.offsetMax = Vector2.zero;
            var layout = gridGo.GetComponent<GridLayoutGroup>();
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 2;
            layout.spacing = new Vector2(24f, 20f);
            layout.padding = new RectOffset(14, 14, 14, 14);
            Canvas.ForceUpdateCanvases();
            float w = Mathf.Max(1f, grid.rect.width - layout.padding.horizontal - layout.spacing.x);
            float h = Mathf.Max(1f, grid.rect.height - layout.padding.vertical - layout.spacing.y);
            layout.cellSize = new Vector2(w * 0.5f, h * 0.5f);

            for (int i = 0; i < cards.Count; i++) BuildCard(grid, cards[i]);
        }

        private void BuildCard(RectTransform grid, Card spec)
        {
            bool available = spec.Available == null || spec.Available();
            // Uppercased AT CONSTRUCTION, exactly as Manage does it (ManageScreenPanel.cs:587),
            // so the card face has ONE writer. Never re-assign face.text further down - that is
            // how a screen ends up with two producers for one string (WO-1341).
            var button = ElarionUiKit.BuildObsidianButton(grid, spec.Title.ToUpperInvariant(),
                ElarionUiKit.ObsidianButtonStyle.Style1,
                available ? ElarionUiKit.ObsidianButtonColor.Yellow : ElarionUiKit.ObsidianButtonColor.Gray,
                Vector2.zero, Vector2.one, () => OpenCard(spec));
            if (button == null) return;
            button.gameObject.name = "DeckCard_" + spec.Title;
            button.interactable = available;
            MedievalUiSkin.ApplyButton(button, primary: available);
            var cardImage = button.GetComponent<Image>();
            var illustratedCard = string.IsNullOrEmpty(spec.ArtKey) ? null :
                Resources.Load<Sprite>("UI/ElarionMedieval/cards/" + spec.ArtKey);
            var cardFrame = illustratedCard != null ? illustratedCard :
                Resources.Load<Sprite>("UI/ElarionMedieval/frames/card-frame-empty");
            if (cardImage != null && cardFrame != null)
            {
                if (illustratedCard != null)
                {
                    // Some delivered wide-card PNGs carry an editor checkerboard in their outer
                    // packaging margin and some are already trimmed tight. WO-1311 (owner ruling
                    // 2026-09-02, "fix it that way"): the correction is DERIVED PER SPRITE from
                    // that sprite's own opaque bounds - never from a shared constant. A tight
                    // sprite gets NO correction and renders 1:1; a margined one gets exactly its
                    // own margin removed, per edge, seated inside a native rectangular mask so
                    // the packaging pixels are never displayed or mutated.
                    var fit = ResolveArtFit(spec.ArtKey, illustratedCard);
                    cardImage.sprite = null;
                    cardImage.color = Color.clear;
                    if (fit.Corrected && button.GetComponent<RectMask2D>() == null)
                        button.gameObject.AddComponent<RectMask2D>();
                    var artSurface = ElarionUiKit.AddImage(button.transform, "IllustratedCardSurface",
                        fit.AnchorMin, fit.AnchorMax,
                        available ? Color.white : new Color(.48f, .48f, .50f, .82f), false);
                    artSurface.transform.SetAsFirstSibling();
                    var artImage = artSurface.GetComponent<Image>();
                    artImage.sprite = illustratedCard;
                    artImage.type = Image.Type.Simple;
                    artImage.preserveAspect = false;
                    artImage.raycastTarget = false;
                    button.targetGraphic = artImage;
                    // Illustrated destination cards are complete surfaces. Never SpriteSwap
                    // them to a generic/blank button face on hover or controller selection.
                    button.transition = Selectable.Transition.ColorTint;
                    var colors = button.colors;
                    colors.normalColor = Color.white;
                    colors.highlightedColor = new Color(1.08f, 1.04f, .90f, 1f);
                    colors.selectedColor = colors.highlightedColor;
                    colors.pressedColor = new Color(.82f, .76f, .64f, 1f);
                    colors.disabledColor = new Color(.46f, .46f, .48f, .82f);
                    colors.colorMultiplier = 1f;
                    colors.fadeDuration = .08f;
                    button.colors = colors;
                }
                else
                {
                    cardImage.sprite = cardFrame;
                    cardImage.type = Image.Type.Simple;
                    cardImage.color = available ? Color.white : new Color(.48f, .48f, .50f, .82f);
                }
            }

            // WO-1341, owner ruling: "should match font and format of Manage screen". The
            // reference implementation is ManageScreenPanel.BuildLauncherCards
            // (ManageScreenPanel.cs:620-635) and every number below is copied from it rather
            // than invented here, so the two card surfaces cannot drift again:
            //   title  - anchors x TextPlateX0..0.96, y 0.55..0.90, 36px, CENTRED, Gold
            //            (ParchmentDim when locked), FitSingleLine(30, 40)
            //   purpose- y 0.26..0.52, FontMicro, CENTRED, FitSingleLine(24, 30)
            // The old deck format was 34px BOLD LEFT with a 22px floor and a hard
            // TextOverflowModes.Ellipsis - which is what printed the truncated
            // "Choose the abilities equipped for bat..." in the device capture. Manage neither
            // disables wrapping nor sets an overflow mode, so neither do we.
            var face = button.GetComponentInChildren<TMP_Text>();
            if (face != null)
            {
                var rt = face.rectTransform;
                rt.anchorMin = new Vector2(TextPlateX0(illustratedCard != null), 0.55f);
                rt.anchorMax = new Vector2(0.96f, 0.90f);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                face.fontSize = 36f;
                face.alignment = TextAlignmentOptions.Center;
                face.color = available ? ElarionUi.Gold : ElarionUi.ParchmentDim;
                ElarionUiKit.FitSingleLine(face, 30f, 40f);
            }

            if (illustratedCard == null)
            {
                Sprite sprite = ConceptIconResolver.Resolve(spec.Concept);
                var iconFrame = ElarionUiKit.AddImage(button.transform, "IdentityMedallion",
                    new Vector2(0.055f, 0.22f), new Vector2(0.245f, 0.84f), Color.white, false);
                var bezel = iconFrame.GetComponent<Image>();
                bezel.sprite = Resources.Load<Sprite>("UI/ElarionMedieval/frames/circular-bezel-four-point");
                bezel.preserveAspect = true;
                bezel.raycastTarget = false;
                var iconGo = ElarionUiKit.AddImage(iconFrame.transform, "IdentityIcon",
                    new Vector2(0.22f, 0.22f), new Vector2(0.78f, 0.78f), Color.white, false);
                var icon = iconGo.GetComponent<Image>();
                icon.sprite = sprite;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                if (sprite == null)
                {
                    icon.color = new Color(0f, 0f, 0f, 0f);
                    var monogram = ElarionUiKit.Label(iconFrame.transform,
                        string.IsNullOrEmpty(spec.Title) ? "?" : spec.Title.Substring(0, 1).ToUpperInvariant(),
                        0.20f, 0.80f, ElarionUi.Gold, 42, TextAlignmentOptions.Center, 0.20f, 0.80f,
                        bold: true);
                    monogram.raycastTarget = false;
                    monogram.gameObject.name = "IdentityMonogram";
                }
            }

            if (!available)
            {
                // WO-1311 acceptance 3. The gray tint is a COLOUR-ONLY signal and the owner is
                // red/green colourblind, so unavailability also carries a NON-COLOUR partner: a
                // literal word badge on a dark plate. Text reads identically under any hue loss.
                float badgeX0 = TextPlateX0(illustratedCard != null);
                var badgePlate = ElarionUiKit.AddImage(button.transform, "LockedBadgePlate",
                    new Vector2(badgeX0, 0.87f), new Vector2(0.93f, 0.99f),
                    new Color(0f, 0f, 0f, .62f), false);
                var plateImage = badgePlate.GetComponent<Image>();
                if (plateImage != null) plateImage.raycastTarget = false;
                var badge = ElarionUiKit.Label(badgePlate.transform, "[ LOCKED ]", 0.02f, 0.98f,
                    ElarionUi.Parchment, 24, TextAlignmentOptions.Center, 0.02f, 0.98f, 4f, true);
                badge.gameObject.name = "LockedBadge";
                badge.enableWordWrapping = false;
                badge.overflowMode = TextOverflowModes.Ellipsis;
                ElarionUiKit.FitSingleLine(badge, 14f, 24f);
            }

            // WO-1357: when the card is locked, the purpose line becomes the REMEDY. The
            // "[ LOCKED ]" badge above says THAT it is shut; this line says WHY and what to do,
            // in words - the owner is red/green colourblind, so neither the gray face nor the
            // badge alone may be the only carrier of meaning.
            string lockLine = null;
            if (!available && spec.LockReason != null)
                lockLine = Guard.Try("HUD", "resolve deck card lock reason '" + spec.Title + "'",
                                     spec.LockReason, null);
            if (!available)
                FlowTrace.Step("Navigation", "deck card '" + spec.Title + "' LOCKED - " +
                    (string.IsNullOrEmpty(lockLine) ? "generic requirement line (no LockReason supplied)" : lockLine));

            var purpose = ElarionUiKit.Label(button.transform,
                available ? spec.Purpose
                          : (string.IsNullOrEmpty(lockLine) ? "Complete its requirement first" : lockLine),
                0.26f, 0.52f, available ? ElarionUi.Parchment : ElarionUi.ParchmentDim,
                (int)ElarionUi.FontMicro, TextAlignmentOptions.Center,
                TextPlateX0(illustratedCard != null), 0.96f);
            purpose.gameObject.name = "DeckCardPurpose_" + spec.Title;
            ElarionUiKit.FitSingleLine(purpose, 24f, 30f);
        }

        /// <summary>
        /// Left edge of a card's text plate. An illustrated card's art fills its left half, so the
        /// plate starts at Manage's authored 0.49 (ManageScreenPanel.cs:624). A text-free card
        /// carries only the concept medallion (x 0.055..0.245), so its plate starts just clear of
        /// that instead of leaving a dead band. Both are CENTRED inside their own plate, which is
        /// the format half of the owner's "match Manage" ruling.
        /// </summary>
        private static float TextPlateX0(bool illustrated) => illustrated ? 0.49f : 0.27f;

        /// <summary>
        /// The anchor rectangle an illustrated card's art surface must occupy INSIDE its button so
        /// that the sprite's own opaque region - and nothing else - fills the card face.
        /// <see cref="Corrected"/> is false for a sprite that is already trimmed tight, in which
        /// case the surface is exactly 0..1 and the art renders 1:1 with no mask.
        /// </summary>
        private struct CardArtFit
        {
            public Vector2 AnchorMin;
            public Vector2 AnchorMax;
            public bool Corrected;
        }

        // One measurement per ART KEY for the life of the process. BuildCard runs only when a deck
        // page is opened, and the fit is read from this dictionary - there is no texture read and
        // no allocation per card draw, and none at all per frame.
        private static readonly Dictionary<string, CardArtFit> _artFitCache =
            new Dictionary<string, CardArtFit>();

        private static readonly CardArtFit IdentityFit =
            new CardArtFit { AnchorMin = Vector2.zero, AnchorMax = Vector2.one, Corrected = false };

        private static CardArtFit ResolveArtFit(string artKey, Sprite sprite)
        {
            string key = string.IsNullOrEmpty(artKey) ? (sprite != null ? sprite.name : "?") : artKey;
            CardArtFit cached;
            if (_artFitCache.TryGetValue(key, out cached)) return cached;
            CardArtFit measured = MeasureArtFit(key, sprite);
            _artFitCache[key] = measured;
            return measured;
        }

        /// <summary>
        /// One delivered card PNG whose packaging margin is OPAQUE, so the alpha route in
        /// <see cref="MeasureArtFit"/> cannot see it. Margins are in pixels off each edge of the
        /// authored image, measured from that PNG's own pixels.
        /// </summary>
        private struct OpaqueMargin
        {
            public string Key;
            public int Width, Height, Left, Top, Right, Bottom;
        }

        // ── OPAQUE PACKAGING MARGIN (owner F8 2026-09-03, "the journey raids button is wrong") ──
        // WO-1311 derives a card's packaging margin from the sprite's alpha-built tight mesh. That
        // works only while the margin is TRANSPARENT. Two delivered cards were flattened onto the
        // authoring tool's CHECKERBOARD instead of being exported with alpha, so every pixel of
        // their border is opaque: the tight mesh honestly reports "no margin", the card renders 1:1
        // and the pale checkerboard draws as a near-white slab around the ornate frame. That is
        // exactly the Journey RAIDS card in the owner's photo, and the same defect sits unreported
        // on the Realm deck's GAME GUIDE card.
        //
        // Nothing at RUNTIME can tell that apart from real art - these importers ship
        // isReadable:0, so Texture2D.GetPixels is not available to fall back on, and a wrong crop
        // is worse than an uncropped margin. So the margin is AUTHORED here, measured once from
        // each delivered PNG, and it is GUARDED twice so it can never outlive the bad export:
        //
        //   1. It is consulted ONLY when the alpha route found no transparent margin AT ALL. A
        //      re-exported, properly-alpha'd PNG takes the measured route and never reaches this
        //      table (that is why quests.png - the same art family, exported correctly, margins
        //      L47 T62 R47 B74 - is absent from it and renders right today).
        //   2. Each row is keyed to that PNG's exact pixel dimensions. Re-author the card at any
        //      other size and it falls through to 1:1 rather than being cropped by a stale number.
        //
        // ⚠ DO NOT add a row here for a card that merely "looks a bit off". A row is a claim that
        // the PNG's border pixels are packaging, and the only proof of that is opening the file.
        // Re-export raids.png / game-guide.png with a transparent margin and the matching row
        // becomes dead - delete it THEN, not before.
        private static readonly OpaqueMargin[] OpaqueMargins =
        {
            // cards/raids.png      1774x887 - checkerboard border, art bbox (49,63)-(1726,809)
            new OpaqueMargin { Key = "raids", Width = 1774, Height = 887,
                               Left = 49, Top = 63, Right = 48, Bottom = 78 },
            // cards/game-guide.png 1821x864 - checkerboard border, art bbox (53,65)-(1769,776)
            new OpaqueMargin { Key = "game-guide", Width = 1821, Height = 864,
                               Left = 53, Top = 65, Right = 52, Bottom = 88 }
        };

        /// <summary>
        /// Fills the rect-local opaque fractions from <see cref="OpaqueMargins"/> when this art key
        /// is a known opaque-margin delivery AND the sprite still has the exact dimensions that row
        /// was measured against. Returns false (leaving the fractions untouched) otherwise.
        /// </summary>
        private static bool TryOpaqueMargin(string key, Rect rect,
                                            ref float fx0, ref float fx1, ref float fy0, ref float fy1)
        {
            if (string.IsNullOrEmpty(key)) return false;
            for (int i = 0; i < OpaqueMargins.Length; i++)
            {
                var m = OpaqueMargins[i];
                if (!string.Equals(key, m.Key, System.StringComparison.OrdinalIgnoreCase)) continue;
                // quests.png ships at the SAME 1774x887 as raids.png, so the key match above is
                // what keeps a correctly-exported sibling out of this table, not the size check.
                if (Mathf.RoundToInt(rect.width) != m.Width || Mathf.RoundToInt(rect.height) != m.Height)
                {
                    FlowTrace.Warn("HUD", "card art fit: '" + key + "' has an authored opaque margin " +
                        "for " + m.Width + "x" + m.Height + " but the sprite is " +
                        Mathf.RoundToInt(rect.width) + "x" + Mathf.RoundToInt(rect.height) +
                        " - re-authored art, rendering 1:1");
                    return false;
                }
                // Sprite space is bottom-left origin; the measurements above are image space
                // (top-left origin), so Top and Bottom swap on the way in.
                fx0 = Mathf.Clamp01(m.Left / rect.width);
                fx1 = Mathf.Clamp01((rect.width - m.Right) / rect.width);
                fy0 = Mathf.Clamp01(m.Bottom / rect.height);
                fy1 = Mathf.Clamp01((rect.height - m.Top) / rect.height);
                FlowTrace.Step("HUD", "card art fit: '" + key + "' opaque packaging margin (authored) L" +
                    m.Left + " T" + m.Top + " R" + m.Right + " B" + m.Bottom);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Derives the packaging margin from the sprite's OWN geometry.
        /// <para>ROUTE CHOSEN (WO-1311): the sprite's TIGHT MESH, not a pixel read. These card
        /// importers ship <c>isReadable: 0</c>, so <c>Texture2D.GetPixels</c> would throw; flipping
        /// isReadable would keep a second uncompressed copy of ten ~1800x880 textures in memory on
        /// a phone. The importers already set <c>spriteMeshType: 1</c> (Tight) with
        /// <c>alphaIsTransparency: 1</c>, so Unity generated an alpha-derived mesh AT IMPORT TIME
        /// and <c>Sprite.vertices</c> hands us those opaque bounds at runtime for free.</para>
        /// <para>If the bounds cannot be trusted the answer is NO correction and a
        /// <see cref="FlowTrace.Warn"/> naming the card: a wrong crop is worse than an uncropped
        /// margin.</para>
        /// </summary>
        private static CardArtFit MeasureArtFit(string key, Sprite sprite)
        {
            if (sprite == null)
            {
                FlowTrace.Warn("HUD", "card art fit: no sprite for '" + key + "' - rendering 1:1");
                return IdentityFit;
            }

            Rect rect = sprite.rect;
            if (rect.width < 8f || rect.height < 8f)
            {
                FlowTrace.Warn("HUD", "card art fit: '" + key + "' rect too small - rendering 1:1");
                return IdentityFit;
            }

            Vector2[] verts = null;
            Guard.Try("HUD", "read tight mesh bounds for card art '" + key + "'",
                () => { verts = sprite.vertices; });
            if (verts == null || verts.Length < 3)
            {
                FlowTrace.Warn("HUD", "card art fit: '" + key + "' has no tight mesh - rendering 1:1");
                return IdentityFit;
            }

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < verts.Length; i++)
            {
                Vector2 v = verts[i];
                if (v.x < minX) minX = v.x;
                if (v.x > maxX) maxX = v.x;
                if (v.y < minY) minY = v.y;
                if (v.y > maxY) maxY = v.y;
            }

            // Mesh vertices are in local units measured from the sprite pivot; the pivot is in
            // pixels from the sprite rect's bottom-left. Convert back to rect-local pixels.
            float ppu = sprite.pixelsPerUnit;
            if (ppu <= 0.0001f) ppu = 100f;
            Vector2 pivot = sprite.pivot;
            float x0 = pivot.x + minX * ppu;
            float x1 = pivot.x + maxX * ppu;
            float y0 = pivot.y + minY * ppu;
            float y1 = pivot.y + maxY * ppu;

            float fx0 = Mathf.Clamp01(x0 / rect.width);
            float fx1 = Mathf.Clamp01(x1 / rect.width);
            float fy0 = Mathf.Clamp01(y0 / rect.height);
            float fy1 = Mathf.Clamp01(y1 / rect.height);

            // The alpha route above can only see a margin that is TRANSPARENT. If it found none,
            // the card may still be carrying an OPAQUE packaging margin - see the OpaqueMargins table.
            bool alphaSawNoMargin = x0 <= 1f && y0 <= 1f &&
                                    (rect.width - x1) <= 1f && (rect.height - y1) <= 1f;
            bool opaqueMargin = false;
            if (alphaSawNoMargin && TryOpaqueMargin(key, rect, ref fx0, ref fx1, ref fy0, ref fy1))
                opaqueMargin = true;

            float spanX = fx1 - fx0;
            float spanY = fy1 - fy0;

            // A believable packaging margin trims a modest border. Anything that claims to eat
            // half the card is a mesh we do not understand - refuse it rather than crop wrongly.
            if (spanX < 0.5f || spanY < 0.5f)
            {
                FlowTrace.Warn("HUD", "card art fit: '" + key + "' opaque span implausible (" +
                    spanX.ToString("F3") + "x" + spanY.ToString("F3") + ") - rendering 1:1");
                return IdentityFit;
            }

            // Sub-pixel slack on every edge means the art is already tight. Render it 1:1 and add
            // no mask - this is the case the retired fixed offset was cropping for no reason.
            if (alphaSawNoMargin && !opaqueMargin)
            {
                FlowTrace.Step("HUD", "card art fit: '" + key + "' is tight - 1:1, no correction");
                return IdentityFit;
            }

            float scaleX = 1f / spanX;
            float scaleY = 1f / spanY;
            var fit = new CardArtFit
            {
                AnchorMin = new Vector2(-fx0 * scaleX, -fy0 * scaleY),
                AnchorMax = new Vector2(-fx0 * scaleX + scaleX, -fy0 * scaleY + scaleY),
                Corrected = true
            };
            // Report the margin from the FRACTIONS, not from the mesh bounds: on the opaque
            // route the mesh legitimately spans the whole rect, and logging x0/x1 here would
            // print "margin L0 T0 R0 B0" beside a corrected anchor set - a trace that lies.
            FlowTrace.Step("HUD", "card art fit: '" + key + "' margin L" +
                Mathf.RoundToInt(fx0 * rect.width) + " T" + Mathf.RoundToInt((1f - fy1) * rect.height) +
                " R" + Mathf.RoundToInt((1f - fx1) * rect.width) + " B" + Mathf.RoundToInt(fy0 * rect.height) +
                " -> anchors " + fit.AnchorMin.ToString("F3") + ".." + fit.AnchorMax.ToString("F3"));
            return fit;
        }

        private void OpenCard(Card spec)
        {
            if (spec == null || spec.Open == null || (spec.Available != null && !spec.Available())) return;
            string title = spec.Title;
            Close();
            Guard.Try("Navigation", "open deck card '" + title + "'", spec.Open);
            FlowTrace.Step("Navigation", "deck card -> " + title);
        }

        private static Card Route(string title, string purpose, string concept, PanelId target,
                                  string artKey = null) => new Card
        {
            Title = title,
            Purpose = purpose,
            Concept = concept,
            ArtKey = artKey,
            Available = () => PanelRouter.IsRegistered(target),
            Open = () => PanelRouter.Open(target)
        };

        private static List<Card> CardsFor(PlayerDeckKind kind)
        {
            switch (kind)
            {
                case PlayerDeckKind.Hero:
                    // WO-1341. THESE FOUR CARDS DELIBERATELY CARRY NO ArtKey, and that is the
                    // whole fix - do not "restore the missing art" without reading this.
                    //
                    // Every label on the Hero deck rendered TWICE on device (build
                    // 2026.09.03.353742): once as the live TMP text built below, and once as
                    // words BAKED INTO THE PNG. cards/bag.png, cards/equipment.png,
                    // cards/skills.png and cards/loadout.png each have a title and a tagline
                    // painted into the very text-safe plate BuildCard draws into, so the two
                    // copies overlapped in two fonts - and they did not even agree on the words
                    // ("Manage your items" vs "Browse every carried item by category";
                    // "LOAD OUT" vs "Loadout"). One string, two producers.
                    //
                    // The kit standard is EXPLICIT and every other card already follows it -
                    // ManageScreenPanel.cs:606 "the approved kit cards are text-safe layered
                    // faces: illustration and border are art, while title, purpose, count and
                    // interaction remain live". cards/buildings.png (Manage), cards/quests.png
                    // (Journey) and cards/realm-store.png (Realm) are all illustration-left with
                    // an EMPTY plate right. The four Hero PNGs are the only ones in the kit that
                    // break it, so the ART is the duplicate producer and the live text survives.
                    //
                    // Re-authoring those four PNGs text-free is the OWNER'S call, not this
                    // ticket's. When they are re-delivered to the buildings.png standard, add the
                    // art key back as the 5th argument here (one word per line) and nothing else
                    // needs to change. Until then these render through the text-free branch
                    // (card-frame-empty + the concept medallion), which is the same treatment
                    // every other non-illustrated card in the game gets.
                    return new List<Card>
                    {
                        Route("Bag", "Every item you carry", "inventory", PanelId.Inventory),
                        Route("Equipment", "Gear worn by your hero", "armor", PanelId.EquipmentPanel),
                        Route("Skills", "Learn and improve talents", "skill", PanelId.HeroSkillTree),
                        Route("Loadout", "Abilities equipped for battle", "magic", PanelId.HeroLoadout)
                    };
                case PlayerDeckKind.Journey:
                    return new List<Card>
                    {
                        new Card { Title = "Quests", Purpose = "Read active quests and realm rumors",
                            Concept = "quest", ArtKey = "quests",
                            Available = () => PanelRouter.IsRegistered(PanelId.RumorBoard),
                            Open = () => PanelRouter.Open(PanelId.RumorBoard) },
                        // WO-1357 (owner 2026-09-03: "Raid button under journey should fail
                        // gracefully, it works great if there is a barracks but should show
                        // locked if doesnt have one yet or its destroyed").
                        //
                        // This card used to carry `Available = () => true`, so it offered the raid
                        // door unconditionally and dead-ended with no barracks - while the action
                        // bar's Raids face had honoured PostureSignals.RaidCapable since WO-835.
                        // ONE rule, TWO surfaces, one of them ignoring it: the duplicated-state
                        // class this repo keeps getting burned by. The fix is to read the EXISTING
                        // predicate, never to write a second barracks check here - a second check
                        // would drift from the first, and the drift is the actual defect.
                        //
                        // The card stays VISIBLE and locked rather than hidden: WO-1008 already
                        // settled that a raid door which hides itself reads as broken ("I do not
                        // see a way to start a raid"). Locked-with-a-reason teaches the next goal.
                        new Card { Title = "Raids", Purpose = "Choose a camp and deploy your army", Concept = "raid",
                            ArtKey = "raids",
                            Available = () => PostureSignals.RaidCapable,
                            LockReason = () => PostureSignals.RaidLockCopy(PostureSignals.RaidLock),
                            Open = RaidEntryGate.RequestOpen }
                    };
                default:
                    return new List<Card>
                    {
                        Route("Realm Store", "Browse clearly priced realm offers", "store", PanelId.RealmStore, "realm-store"),
                        Route("Defense Report", "Review attacks against your town", "defense", PanelId.DefenseReport, "defense-report"),
                        Route("Monthly Ledger", "Review non-expiring monthly progress", "ledger", PanelId.MonthlyLedger, "monthly-ledger"),
                        Route("Game Guide", "Read controls, systems, and help", "settings", PanelId.GameGuide, "game-guide")
                    };
            }
        }

        protected override void OnDestroy()
        {
            PanelRouter.Unregister(PanelId.RealmDeck, OpenRealm);
            PanelRouter.Unregister(PanelId.HeroDeck, OpenHero);
            PanelRouter.Unregister(PanelId.JourneyDeck, OpenJourney);
            if (_instance == this) _instance = null;
            base.OnDestroy();
        }
    }
}
