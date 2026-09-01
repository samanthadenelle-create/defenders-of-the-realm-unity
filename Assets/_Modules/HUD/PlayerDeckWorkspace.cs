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
                    // The delivered wide-card PNGs include an editor checkerboard in their
                    // outer packaging margin. Seat the authored card bounds inside a native
                    // rectangular mask; do not display or mutate those packaging pixels.
                    cardImage.sprite = null;
                    cardImage.color = Color.clear;
                    if (button.GetComponent<RectMask2D>() == null)
                        button.gameObject.AddComponent<RectMask2D>();
                    var artSurface = ElarionUiKit.AddImage(button.transform, "IllustratedCardSurface",
                        new Vector2(-.036f, -.136f), new Vector2(1.036f, 1.112f),
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

            var purpose = ElarionUiKit.Label(button.transform,
                available ? spec.Purpose : "Unavailable - complete its requirement first",
                0.16f, 0.52f, available ? ElarionUi.Parchment : ElarionUi.ParchmentDim,
                (int)ElarionUi.FontLabel, TextAlignmentOptions.Left,
                illustratedCard != null ? 0.48f : 0.28f, 0.92f);
            purpose.enableWordWrapping = false;
            purpose.overflowMode = TextOverflowModes.Ellipsis;
            ElarionUiKit.FitSingleLine(purpose, 16f, ElarionUi.FontLabel);
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
                        Route("Bag", "Browse every carried item by category", "inventory", PanelId.Inventory),
                        Route("Equipment", "Review worn gear on your hero", "armor", PanelId.EquipmentPanel),
                        Route("Skills", "Learn and improve hero talents", "skill", PanelId.HeroSkillTree),
                        Route("Loadout", "Choose the abilities equipped for battle", "magic", PanelId.HeroLoadout)
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
