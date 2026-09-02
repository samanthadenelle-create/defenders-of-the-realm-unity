using System;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
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
            var button = ElarionUiKit.BuildObsidianButton(grid, spec.Title,
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

            var face = button.GetComponentInChildren<TMP_Text>();
            if (face != null)
            {
                var rt = face.rectTransform;
                rt.anchorMin = new Vector2(illustratedCard != null ? 0.48f : 0.27f, 0.56f);
                rt.anchorMax = new Vector2(0.93f, 0.86f);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                face.alignment = TextAlignmentOptions.Left;
                face.color = available ? ElarionUi.Gold : ElarionUi.ParchmentDim;
                face.fontSize = 34f;
                face.fontStyle = FontStyles.Bold;
                ElarionUiKit.FitSingleLine(face, 22f, 34f);
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
                float badgeX0 = illustratedCard != null ? 0.48f : 0.27f;
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

            var purpose = ElarionUiKit.Label(button.transform,
                available ? spec.Purpose : "Unavailable - complete its requirement first",
                0.16f, 0.52f, available ? ElarionUi.Parchment : ElarionUi.ParchmentDim,
                (int)ElarionUi.FontLabel, TextAlignmentOptions.Left,
                illustratedCard != null ? 0.48f : 0.28f, 0.92f);
            purpose.enableWordWrapping = false;
            purpose.overflowMode = TextOverflowModes.Ellipsis;
            ElarionUiKit.FitSingleLine(purpose, 16f, ElarionUi.FontLabel);
        }

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
            if (x0 <= 1f && y0 <= 1f && (rect.width - x1) <= 1f && (rect.height - y1) <= 1f)
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
            FlowTrace.Step("HUD", "card art fit: '" + key + "' margin L" +
                Mathf.RoundToInt(x0) + " T" + Mathf.RoundToInt(rect.height - y1) +
                " R" + Mathf.RoundToInt(rect.width - x1) + " B" + Mathf.RoundToInt(y0) +
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
                    return new List<Card>
                    {
                        Route("Bag", "Browse every carried item by category", "inventory", PanelId.Inventory, "bag"),
                        Route("Equipment", "Review worn gear on your hero", "armor", PanelId.EquipmentPanel, "equipment"),
                        Route("Skills", "Learn and improve hero talents", "skill", PanelId.HeroSkillTree, "skills"),
                        Route("Loadout", "Choose the abilities equipped for battle", "magic", PanelId.HeroLoadout, "loadout")
                    };
                case PlayerDeckKind.Journey:
                    return new List<Card>
                    {
                        new Card { Title = "Quests", Purpose = "Read active quests and realm rumors",
                            Concept = "quest", ArtKey = "quests",
                            Available = () => PanelRouter.IsRegistered(PanelId.RumorBoard),
                            Open = () => PanelRouter.Open(PanelId.RumorBoard) },
                        new Card { Title = "Raids", Purpose = "Choose a camp and deploy your army", Concept = "raid",
                            ArtKey = "raids", Available = () => true,
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
