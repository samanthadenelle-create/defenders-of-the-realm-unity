using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core;
using DeNelle.Core.Catalog;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;
using Cysharp.Threading.Tasks;

namespace DeNelle.Village
{
    public sealed class BuildCollectionPage
    {
        public CardCollectionDefinition Collection { get; }
        public bool IsRoot => Collection == null;

        private BuildCollectionPage(CardCollectionDefinition collection) => Collection = collection;
        public static BuildCollectionPage Root() => new BuildCollectionPage(null);
        public static BuildCollectionPage For(CardCollectionDefinition collection) =>
            new BuildCollectionPage(collection ?? throw new ArgumentNullException(nameof(collection)));
    }

    /// <summary>Player-facing, data-authored Build collection browser. It owns no placement logic.</summary>
    public sealed class BuildCollectionBrowser : ObsidianNavigationWorkspace<BuildCollectionPage>
    {
        public const int CardsPerPage = CardCollectionPaging.MaxVisibleCards;
        // The catalog/progression row remains live for saves and unlock bookkeeping, but its
        // unfinished card art must not be advertised as a player-ready build choice.
        private const string HiddenUntilFinishedArtId = "gate_stone";
        private const string MissingImageCopy = "Image coming soon";
        private readonly List<GameObject> _pageObjects = new List<GameObject>();
        private RectTransform _panel;
        private CardCollectionDocument _document;
        private CardCollectionDefinition _collection;
        private CardCollectionModel _remoteCollection;
        private CardCollectionCatalog _catalog;
        private CardCollectionRemoteService _remote;
        private int _page;
        private Action<CatalogEntry> _place;

        protected override string WorkspaceName => "Build Collections";

        protected override string TitleFor(BuildCollectionPage page) =>
            page == null || page.IsRoot ? "Build Collections" : page.Collection.Title;

        protected override string SubtitleFor(BuildCollectionPage page) =>
            page == null || page.IsRoot
                ? (BuildFirstUseGuide.Current == BuildFirstUseGuide.Step.Category
                    ? BuildFirstUseGuide.Copy : "Choose what the realm needs next.")
                : string.Empty; // collection guidance owns a dedicated row below its cards

        protected override void RenderPage(BuildCollectionPage page, RectTransform content)
        {
            _panel = content;
            _collection = page != null ? page.Collection : null;
            if (_collection == null) RenderCategories();
            else RenderCollection();
        }

        public void Show(Action<CatalogEntry> place)
        {
            _place = place;
            BuildFirstUseGuide.BeginSession();
            _catalog = CardCollectionCatalog.CreateDefault(Application.persistentDataPath, Application.version);
            _remote = new CardCollectionRemoteService(_catalog,
                System.IO.Path.Combine(Application.persistentDataPath, "card-collections"));
            _document = _catalog.Resolve();
            _collection = null;
            _page = 0;
            Open(BuildCollectionPage.Root());
        }

        private void OnEnable()
        {
            ProgressionUnlocks.Changed += OnProgressionUnlockChanged;
            StructureSingleton.SingletonResolved += OnFiniteCapacityChanged;
            StructureSingleton.SingletonReleased += OnFiniteCapacityChanged;
        }

        private void OnFiniteCapacityChanged(string itemId, GameObject placed) => OnFiniteCapacityChanged(itemId);
        private void OnFiniteCapacityChanged(string itemId)
        {
            if (IsOpen && _collection == null) Refresh();
        }

        private void OnProgressionUnlockChanged(string catalogId)
        {
            if (!IsOpen || _collection == null ||
                string.IsNullOrEmpty(RewardedProgression.LockReasonFor(catalogId))) return;
            Refresh();
        }

        public override void Close()
        {
            base.Close();
            _collection = null;
            _remoteCollection = null;
            _page = 0;
        }

        private void RenderCategories()
        {
            // Reserve the upper body band for the first-use/category guidance. The
            // device capture proved .96 let row one paint directly through that line.
            var grid = Region("CategoryGrid", new Vector2(.02f, .18f), new Vector2(.98f, .84f));
            var layout = grid.gameObject.AddComponent<HorizontalLayoutGroup>();
            if (_document?.Collections == null) return;
            var visible = new List<CardCollectionDefinition>();
            foreach (var c in _document.Collections)
            {
                if (c == null || !c.Active || !string.Equals(c.Context, "build", StringComparison.OrdinalIgnoreCase)) continue;
                // Category visibility is based on authoritative item eligibility/unlock, never
                // affordability. An unlocked expensive category remains a useful goal; a category
                // whose definitions are all locked/missing/unfinished is not navigation yet.
                if (!CollectionHasVisibleItems(c)) continue;
                visible.Add(c);
            }
            // Category discovery is one glance, not paged navigation. Seven portrait cards fit
            // comfortably across the supported landscape canvas and read as a collection rather
            // than oversized horizontal buttons.
            layout.spacing = 14f;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            for (int index = 0; index < visible.Count; index++)
            {
                var c = visible[index];
                var captured = c;
                // A collection is browseable content, not an action command.  Building it
                // through ButtonBox made the four choices read as enormous Back/Next buttons
                // (and inherited the button label which then had to be hidden).  Give the
                // collection its own card surface; the Button is only the hit target/state
                // carrier layered over that surface.
                var card = BuildCategoryCard(grid, () =>
                {
                    BuildFirstUseGuide.CategorySelected();
                    OpenCollection(captured);
                });
                var icon = ElarionUiKit.AddImage(card.transform, "CategoryArtwork",
                    new Vector2(.10f, .38f), new Vector2(.90f, .91f), Color.white, false);
                var image = icon.GetComponent<Image>(); image.preserveAspect = true; image.raycastTarget = false;
                SetArtworkOrFallback(icon.transform, image, Resources.Load<Sprite>(c.IconKey));
                var divider = ElarionUiKit.AddImage(card.transform, "CardDivider",
                    new Vector2(.08f, .35f), new Vector2(.92f, .355f), ElarionUi.Gold, false);
                divider.GetComponent<Image>().raycastTarget = false;
                var explicitTitle = Label(card.transform, c.Title, 30, TextAlignmentOptions.Center,
                    new Vector2(.07f, .22f), new Vector2(.93f, .34f));
                explicitTitle.color = ElarionUi.Gold;
                explicitTitle.fontStyle = FontStyles.Bold;
                explicitTitle.enableWordWrapping = false;
                explicitTitle.enableAutoSizing = true;
                explicitTitle.fontSizeMin = 20f;
                explicitTitle.overflowMode = TextOverflowModes.Overflow;
                explicitTitle.transform.SetAsLastSibling();
                var subtitle = Label(card.transform, c.Subtitle, 21, TextAlignmentOptions.Top,
                    new Vector2(.08f, .05f), new Vector2(.92f, .21f));
                subtitle.color = ElarionUi.Parchment;
                subtitle.raycastTarget = false;
                ElarionUiKit.FitBlock(subtitle, 18f, 21f);
            }

            // Building new defenses and managing existing defenses are different player
            // intents. The old behavior silently changed the Defenses category into an upgrade
            // shortcut after FTUE, which made the build catalog disappear precisely when an
            // experienced player expected it. Keep Defenses stable and add one explicit card
            // that routes to the authoritative Manage -> Defense destination.
            CardCollectionDefinition defenseCollection = visible.Find(c =>
                string.Equals(c.CollectionId, "build-defenses", StringComparison.OrdinalIgnoreCase));
            var upgradeCard = BuildCategoryCard(grid, () =>
            {
                Close();
                PanelRouter.Open(PanelId.Manage, "Defense");
            });
            upgradeCard.name = "DefenseUpgradeCard";
            var upgradeArt = ElarionUiKit.AddImage(upgradeCard.transform, "CategoryArtwork",
                new Vector2(.10f, .38f), new Vector2(.90f, .91f), Color.white, false);
            var upgradeImage = upgradeArt.GetComponent<Image>();
            upgradeImage.preserveAspect = true;
            upgradeImage.raycastTarget = false;
            Sprite defenseIcon = defenseCollection != null
                ? Resources.Load<Sprite>(defenseCollection.IconKey)
                : Resources.Load<Sprite>("UI/ElarionMedieval/cards/defense");
            SetArtworkOrFallback(upgradeArt.transform, upgradeImage, defenseIcon);
            var upgradeDivider = ElarionUiKit.AddImage(upgradeCard.transform, "CardDivider",
                new Vector2(.08f, .35f), new Vector2(.92f, .355f), ElarionUi.Gold, false);
            upgradeDivider.GetComponent<Image>().raycastTarget = false;
            var upgradeTitle = Label(upgradeCard.transform, "Upgrade Defenses", 30,
                TextAlignmentOptions.Center, new Vector2(.07f, .22f), new Vector2(.93f, .34f));
            upgradeTitle.color = ElarionUi.Gold;
            upgradeTitle.fontStyle = FontStyles.Bold;
            upgradeTitle.enableWordWrapping = false;
            upgradeTitle.enableAutoSizing = true;
            upgradeTitle.fontSizeMin = 15f;
            upgradeTitle.fontSizeMax = 26f;
            upgradeTitle.transform.SetAsLastSibling();
            var upgradeSubtitle = Label(upgradeCard.transform,
                "Manage every defense.", 21, TextAlignmentOptions.Top,
                new Vector2(.08f, .05f), new Vector2(.92f, .21f));
            upgradeSubtitle.color = ElarionUi.Parchment;
            upgradeSubtitle.raycastTarget = false;
            ElarionUiKit.FitBlock(upgradeSubtitle, 18f, 21f);
        }

        private async void OpenCollection(CardCollectionDefinition collection)
        {
            _collection = collection; _remoteCollection = null; _page = 0;
            Push(BuildCollectionPage.For(collection));
            CardCollectionModel loaded = await _remote.ResolveAsync(collection.CollectionId, Application.version);
            if (_collection != collection || !IsOpen) return;
            _remoteCollection = loaded;
            Refresh();
        }

        private void RenderCollection()
        {
            var visibleIds = VisibleItemIds();
            int count = visibleIds.Count;
            int pages = CardCollectionPaging.PageCount(count);
            // Keep the cards in their own upper field. The collection instruction used
            // to occupy the shared shell subtitle at y .78-.84 and painted through the
            // card titles. Seat it below the card row where the screen has deliberate
            // breathing room; paging remains in the footer band beneath it.
            var row = Region("Cards", new Vector2(.02f, .20f), new Vector2(.98f, .96f));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 18; layout.padding = new RectOffset(10, 10, 6, 6); layout.childForceExpandWidth = true; layout.childForceExpandHeight = true;
            int first = CardCollectionPaging.FirstIndex(_page, count);
            int last = Math.Min(first + CardsPerPage, count);
            for (int i = first; i < last; i++)
            {
                BuildItemCard(row, visibleIds[i]);
            }
            string guidance = BuildFirstUseGuide.Current == BuildFirstUseGuide.Step.Item
                ? BuildFirstUseGuide.Copy : _collection.Subtitle;
            var guidanceLabel = Label(_panel, guidance, 28, TextAlignmentOptions.Center,
                new Vector2(.08f, .11f), new Vector2(.92f, .19f));
            guidanceLabel.color = ElarionUi.Parchment;
            guidanceLabel.raycastTarget = false;
            ElarionUiKit.FitSingleLine(guidanceLabel, 22f, 28f);
            if (pages > 1)
            {
                var prev = FooterButton("PREVIOUS", new Vector2(.04f, .035f), new Vector2(.22f, .11f), () => { _page = Math.Max(0, _page - 1); Refresh(); });
                prev.interactable = _page > 0;
                Label(_panel, "PAGE " + (_page + 1) + " / " + pages, 26, TextAlignmentOptions.Center, new Vector2(.42f, .035f), new Vector2(.58f, .11f));
                var next = FooterButton("NEXT", new Vector2(.78f, .035f), new Vector2(.96f, .11f), () => { _page = Math.Min(pages - 1, _page + 1); Refresh(); });
                next.interactable = _page + 1 < pages;
            }
        }

        private void BuildItemCard(RectTransform parent, string itemId)
        {
            var entry = CatalogRegistry.Get(itemId);
            string lockReason = RewardedProgression.LockReasonFor(itemId);
            bool progressionLocked = !string.IsNullOrEmpty(lockReason) && !ProgressionUnlocks.IsUnlocked(itemId);
            StructureCardVM vm = entry != null
                ? new StructureCardVM(entry, EconomyService.Instance,
                    BuildModeController.FreeBuildAvailable(entry), progressionLocked, lockReason)
                : null;
            bool built = entry != null && StructureSingleton.IsSingleton(entry.id) && StructureSingleton.IsBuilt(entry);
            bool locked = entry == null || (vm != null && vm.Locked);
            bool available = vm != null && vm.Affordable && !locked && !built;
            // Shared collection-card law: the action is a sibling footer BELOW the
            // information card. Keeping it out of the card prevents ornate button art
            // from covering cost/status copy as the layout contracts on mobile.
            var slot = new GameObject("BuildCardSlot_" + itemId, typeof(RectTransform));
            slot.transform.SetParent(parent, false);
            var slotRt = slot.GetComponent<RectTransform>();
            slotRt.anchorMin = Vector2.zero; slotRt.anchorMax = Vector2.one;
            slotRt.offsetMin = slotRt.offsetMax = Vector2.zero;
            // WO-1417: the item card is a KIT SURFACE, not a bespoke navy quad. Same obsidian
            // face (ElarionUiKit.ObsidianFill) + antique-gold perimeter the sibling CATEGORY
            // card one page back already uses, so the row reads as the frame's own material.
            // The old flat #171C29 fill plus an Outline whose colour was the ONLY locked/unlocked
            // cue is gone: hue never carries state here (the owner is red/green colourblind) --
            // the state WORD below is the single carrier.
            var card = Box("BuildCard_" + itemId, slot.transform, ElarionUiKit.ObsidianFill);
            card.anchorMin = new Vector2(0f, .18f);
            card.anchorMax = Vector2.one;
            card.offsetMin = card.offsetMax = Vector2.zero;
            AddGoldPerimeter(card.transform);
            Label(card, vm?.DisplayName ?? Humanize(itemId), 32, TextAlignmentOptions.Center, new Vector2(.05f,.86f), new Vector2(.95f,.98f));
            var artGo = ElarionUiKit.AddImage(card, "Artwork", new Vector2(.12f,.54f), new Vector2(.88f,.84f), Color.white, false);
            var image = artGo.GetComponent<Image>(); image.preserveAspect = true; image.raycastTarget = false;
            SetArtworkOrFallback(artGo.transform, image, BuildPaletteUI.ResolveEntryArtPublic(entry));
            Label(card, vm?.Description ?? "Catalog definition unavailable.", 25, TextAlignmentOptions.TopLeft, new Vector2(.07f,.30f), new Vector2(.93f,.53f));
            // WO-1417 cost line. `COST: NO COST` was a label contradicting its own key, and the
            // underlying zero was TRUE, not a defect: lumberyard/silo/foundry are NON-TOWER rows,
            // so BuildModeController.FreeBuildAvailable lane 3 makes the FIRST placement of each
            // distinct id free (BuildModeController.cs:3078-3080), while the catalog prices the
            // paid one at wood+iron (structures-catalog lumberyard repo.cost 800 wood / 320 iron).
            // While the freebie is live the price slot shows NOTHING -- owner ruling WO-1010 D20,
            // recorded verbatim at BuildPaletteUI.cs:1418-1424 ("dont show anything on first build
            // just nothing" / "they dont need to know first is free"), which retired the "FREE"
            // label on the carousel card. This surface now obeys the same ruling instead of
            // inventing a second freebie wording. The paid basket is spelled through the ONE
            // shared formatter (CostFormat.Words) every other cost surface uses.
            string cost = vm == null ? "Cost unavailable"
                        : vm.Freebie ? string.Empty
                        : CostFormat.Words(CostParts(vm.EffectiveCost));
            if (!string.IsNullOrEmpty(cost))
                Label(card, "COST: " + cost, 25, TextAlignmentOptions.Center, new Vector2(.06f,.13f), new Vector2(.94f,.23f));
            // ONE state word. The old string was a bracket glyph plus a synonym pair
            // ("[READY] AVAILABLE"); brackets are console furniture, and two words for one state
            // read as two facts. Every value below is a distinct word, so the state survives
            // greyscale with no hue at all.
            string state = locked ? "Locked" : built ? "Built" : !vm.Affordable ? "Unaffordable" : "Ready";
            Label(card, state, 25, TextAlignmentOptions.Center, new Vector2(.06f,.02f), new Vector2(.94f,.12f));
            FlowTrace.Step("Build", "collection-card id=" + itemId + " plate=ElarionUiKit.ObsidianFill+GoldPerimeter"
                + " cost='" + cost + "' state='" + state + "'");
            // The complete state already lives in the wrapping status band above. Button faces
            // carry only a short action/state word so the kit never ellipsizes required copy.
            string buttonFace = available ? "PLACE" : built ? "BUILT" : !locked ? "NEED RESOURCES" : "UNAVAILABLE";
            var place = ButtonBox(slot.transform, buttonFace, () => Place(entry));
            var pr = place.GetComponent<RectTransform>(); pr.anchorMin = new Vector2(.10f,.01f); pr.anchorMax = new Vector2(.90f,.16f); pr.offsetMin = pr.offsetMax = Vector2.zero;
            place.GetComponent<Button>().interactable = available;
            var buttonLabel = place.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonLabel != null)
            {
                buttonLabel.enableWordWrapping = true;
                buttonLabel.enableAutoSizing = true;
                buttonLabel.fontSizeMin = 20f;
                buttonLabel.fontSizeMax = 32f;
                buttonLabel.overflowMode = TextOverflowModes.Overflow;
            }
        }

        private void Place(CatalogEntry entry)
        {
            if (entry == null) return;
            var callback = _place;
            // Done commits the selection, releases the browsing pause, then returns
            // to the existing placement authority. No economy/placement logic moved.
            Done(BuildFirstUseGuide.ItemSelected, () => callback?.Invoke(entry));
        }

        private List<string> VisibleItemIds()
        {
            var result = new List<string>();
            if (_remoteCollection?.Cards != null)
            {
                foreach (var card in _remoteCollection.Cards)
                    if (card != null && IsCollectionItemVisible(card.StableId))
                        result.Add(card.StableId);
            }
            else if (_collection?.Items != null)
            {
                foreach (var item in _collection.Items)
                    if (item != null && IsCollectionItemVisible(item.ItemId))
                        result.Add(item.ItemId);
            }
            return result;
        }

        private static bool IsCollectionItemVisible(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            if (string.Equals(itemId, HiddenUntilFinishedArtId, StringComparison.OrdinalIgnoreCase)) return false;

            // Owner 2026-08-29: hide cards that are not unlocked (Arcane Spire + Catapult were
            // still showing). Mirror BuildPaletteVM WO-964 — lockedIds HIDE until earned; never
            // grey them in the collection browser. Same earn authority: ProgressionUnlocks.
            if (IsBuildCategoryLockedOut(itemId) && !ProgressionUnlocks.IsUnlocked(itemId))
                return false;

            string lockReason = RewardedProgression.LockReasonFor(itemId);
            if (!string.IsNullOrEmpty(lockReason) && !ProgressionUnlocks.IsUnlocked(itemId))
                return false;

            // visibleLockedIds (e.g. Stone Gate) — also hide until unlock; do not tease.
            if (IsBuildCategoryVisibleLocked(itemId) && !ProgressionUnlocks.IsUnlocked(itemId))
                return false;

            return true;
        }

        /// <summary>
        /// Union of every verb's <c>lockedIds</c> from build-categories.json (same rule as
        /// <see cref="BuildPaletteVM.ConfigureGroup"/>). A row gated under ANY verb stays hidden
        /// in collections until <see cref="ProgressionUnlocks"/> flips.
        /// </summary>
        private static bool IsBuildCategoryLockedOut(string itemId)
        {
            foreach (BuildType verb in System.Enum.GetValues(typeof(BuildType)))
            {
                var cat = BuildCategoryRegistry.Get(verb);
                if (cat?.LockedIds != null && cat.LockedIds.Contains(itemId))
                    return true;
            }
            return false;
        }

        private static bool IsBuildCategoryVisibleLocked(string itemId)
        {
            foreach (BuildType verb in System.Enum.GetValues(typeof(BuildType)))
            {
                var cat = BuildCategoryRegistry.Get(verb);
                if (cat?.VisibleLockedReasons != null && cat.VisibleLockedReasons.ContainsKey(itemId))
                    return true;
            }
            return false;
        }

        private static bool CollectionHasVisibleItems(CardCollectionDefinition collection)
        {
            if (collection?.Items == null) return false;
            foreach (var item in collection.Items)
            {
                if (item == null || !IsCollectionItemVisible(item.ItemId)) continue;
                var entry = CatalogRegistry.Get(item.ItemId);
                if (entry == null) continue;
                // Singleton is today's authoritative finite-placement contract. Once its one
                // allowed instance exists it is no longer a build choice; removal raises
                // SingletonReleased and this category projection recomputes automatically.
                if (StructureSingleton.IsSingleton(entry.id) && StructureSingleton.IsBuilt(entry)) continue;
                return true; // repeatable entries remain visible while their definition is eligible
            }
            return false;
        }

        /// <summary>Every collection image resolves to art or an intentional neutral placeholder.</summary>
        private static void SetArtworkOrFallback(Transform host, Image image, Sprite sprite)
        {
            if (image == null) return;
            image.sprite = sprite;
            if (sprite != null) { image.color = Color.white; return; }

            image.color = new Color(.10f, .11f, .14f, 1f);
            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.48f, .43f, .32f, 1f);
            outline.effectDistance = new Vector2(2f, -2f);
            var fallback = Label(host, MissingImageCopy, 23, TextAlignmentOptions.Center,
                new Vector2(.08f, .12f), new Vector2(.92f, .88f));
            fallback.color = new Color(.82f, .79f, .70f, 1f);
            fallback.raycastTarget = false;
        }

        private Button FooterButton(string text, Vector2 min, Vector2 max, Action action)
        {
            var go = ButtonBox(_panel, text, action);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin=min;rt.anchorMax=max;rt.offsetMin=rt.offsetMax=Vector2.zero;
            var button = go.GetComponent<Button>();
            MedievalUiSkin.ApplyButton(button, primary: text == "NEXT");
            return button;
        }
        private RectTransform Region(string name, Vector2 min, Vector2 max)
        { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(_panel,false); var rt=go.GetComponent<RectTransform>();rt.anchorMin=min;rt.anchorMax=max;rt.offsetMin=rt.offsetMax=Vector2.zero;return rt; }
        private static GameObject ButtonBox(Transform parent, string text, Action click)
        { return ElarionUiKit.Button(parent,text,ElarionUiKit.ButtonKind.Quiet,Vector2.zero,Vector2.one,click).gameObject; }

        private static GameObject BuildCategoryCard(Transform parent, Action click)
        {
            var go = new GameObject("BuildCollectionCard", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var face = go.GetComponent<Image>();
            // The supplied horizontal content-panel bitmap has a large transparent tail below
            // its visible border. Stretching it into a portrait card leaves the footer copy
            // outside the painted surface. Use native scalable geometry for this vertical
            // component: one continuous obsidian face plus an explicit antique-gold perimeter.
            face.sprite = null;
            face.color = new Color(.025f, .024f, .023f, .985f);
            AddGoldPerimeter(go.transform);
            var button = go.GetComponent<Button>();
            button.targetGraphic = face;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(1f, .92f, .72f, 1f),
                pressedColor = new Color(.82f, .66f, .36f, 1f),
                selectedColor = new Color(1f, .92f, .72f, 1f),
                disabledColor = new Color(.42f, .42f, .42f, .75f),
                colorMultiplier = 1f,
                fadeDuration = .08f
            };
            if (click != null) button.onClick.AddListener(() => click());
            return go;
        }
        /// <summary>The kit BEZEL: an explicit antique-gold perimeter over an obsidian face, in
        /// native scalable geometry (the horizontal frame bitmap does not survive a portrait
        /// stretch -- see BuildCategoryCard's own note). WO-1417 lifted these four edges out of
        /// BuildCategoryCard verbatim so the CATEGORY card and the ITEM card are one material
        /// written once; no new primitive was authored.</summary>
        private static void AddGoldPerimeter(Transform host)
        {
            void Edge(string name, Vector2 min, Vector2 max)
            {
                var edge = ElarionUiKit.AddImage(host, name, min, max,
                    new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, .95f), false);
                edge.GetComponent<Image>().raycastTarget = false;
            }
            Edge("GoldTop",    new Vector2(.018f, .982f), new Vector2(.982f, .992f));
            Edge("GoldBottom", new Vector2(.018f, .008f), new Vector2(.982f, .018f));
            Edge("GoldLeft",   new Vector2(.008f, .018f), new Vector2(.018f, .982f));
            Edge("GoldRight",  new Vector2(.982f, .018f), new Vector2(.992f, .982f));
        }

        private static RectTransform Box(string name, Transform parent, Color color)
        { return ElarionUiKit.AddImage(parent,name,Vector2.zero,Vector2.one,color,true).GetComponent<RectTransform>(); }
        private static TextMeshProUGUI Label(Transform parent,string text,float size,TextAlignmentOptions align,Vector2 min,Vector2 max)
        {
            var t=ElarionUiKit.Label(parent,text,min.y,max.y,Color.white,Mathf.RoundToInt(size),align,min.x,max.x);
            t.enableWordWrapping=true;
            t.enableAutoSizing=true;
            t.fontSizeMin=Mathf.Min(20f,size);
            t.fontSizeMax=size;
            t.overflowMode=TextOverflowModes.Overflow;
            return t;
        }
        private static string Humanize(string id) => string.IsNullOrEmpty(id) ? "Unknown" : id.Replace('_',' ').Replace('-',' ');
        /// <summary>WO-1417: the collection card's basket now runs through the ONE shared cost
        /// formatter (CostFormat.Parts/Words) instead of a private second formatter that owned its
        /// own separator and its own "NO COST" wording. Same concept ids + words BuildPaletteUI
        /// authors, so the two build surfaces can never disagree about a price.</summary>
        private static IReadOnlyList<CostPart> CostParts(DeNelle.Core.Catalog.ResourceCost c)
        {
            return CostFormat.Parts(new[]
            {
                ("wood", "Wood", c.wood), ("stone", "Stone", c.food),
                ("iron", "Iron", c.iron), ("crystal", "Crystals", c.crystals)
            });
        }
        protected override void OnDisable()
        {
            ProgressionUnlocks.Changed -= OnProgressionUnlockChanged;
            StructureSingleton.SingletonResolved -= OnFiniteCapacityChanged;
            StructureSingleton.SingletonReleased -= OnFiniteCapacityChanged;
            base.OnDisable();
        }
        protected override void OnDestroy()
        {
            ProgressionUnlocks.Changed -= OnProgressionUnlockChanged;
            StructureSingleton.SingletonResolved -= OnFiniteCapacityChanged;
            StructureSingleton.SingletonReleased -= OnFiniteCapacityChanged;
            base.OnDestroy();
        }
    }
}
