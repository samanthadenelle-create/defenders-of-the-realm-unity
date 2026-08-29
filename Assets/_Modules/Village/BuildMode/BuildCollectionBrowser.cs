using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core;
using DeNelle.Core.Catalog;
using DeNelle.Core.UI;
using Cysharp.Threading.Tasks;

namespace DeNelle.Village
{
    /// <summary>Player-facing, data-authored Build collection browser. It owns no placement logic.</summary>
    public sealed class BuildCollectionBrowser : MonoBehaviour
    {
        public const int CardsPerPage = CardCollectionPaging.MaxVisibleCards;
        // The catalog/progression row remains live for saves and unlock bookkeeping, but its
        // unfinished card art must not be advertised as a player-ready build choice.
        private const string HiddenUntilFinishedArtId = "gate_stone";
        private const string MissingImageCopy = "Image coming soon";
        private readonly List<GameObject> _pageObjects = new List<GameObject>();
        private FocusedModalHost _focus;
        private GameObject _canvas;
        private RectTransform _panel;
        private CardCollectionDocument _document;
        private CardCollectionDefinition _collection;
        private CardCollectionModel _remoteCollection;
        private CardCollectionCatalog _catalog;
        private CardCollectionRemoteService _remote;
        private int _page;
        private Action<CatalogEntry> _place;

        public bool IsOpen => _focus != null && _focus.IsOpen;

        public void Show(Action<CatalogEntry> place)
        {
            _place = place;
            BuildFirstUseGuide.BeginSession();
            EnsureBuilt();
            _catalog = CardCollectionCatalog.CreateDefault(Application.persistentDataPath, Application.version);
            _remote = new CardCollectionRemoteService(_catalog,
                System.IO.Path.Combine(Application.persistentDataPath, "card-collections"));
            _document = _catalog.Resolve();
            _collection = null;
            _page = 0;
            if (!_focus.Open("Build Collections")) return;
            _canvas.SetActive(true);
            RenderCategories();
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
            if (IsOpen && _collection == null) RenderCategories();
        }

        private void OnProgressionUnlockChanged(string catalogId)
        {
            if (!IsOpen || _collection == null ||
                string.IsNullOrEmpty(RewardedProgression.LockReasonFor(catalogId))) return;
            RenderCollection();
        }

        public void Close()
        {
            if (_canvas != null) _canvas.SetActive(false);
            _focus?.Close();
            _collection = null;
            _page = 0;
        }

        private void EnsureBuilt()
        {
            if (_canvas != null) return;
            _focus = gameObject.GetComponent<FocusedModalHost>();
            if (_focus == null) _focus = gameObject.AddComponent<FocusedModalHost>();
            _canvas = ElarionUiKit.BuildModalCanvas("BuildCollectionsCanvas", 1200);
            _canvas.transform.SetParent(transform, false);
            var scaler = _canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            Stretch(_canvas.GetComponent<RectTransform>());

            var dim = Box("Dim", _canvas.transform, new Color(0.015f, 0.02f, 0.035f, .82f)); Stretch(dim);
            var panelGo = Box("FocusedCollectionPanel", dim, new Color(.055f, .07f, .105f, .98f));
            _panel = panelGo.GetComponent<RectTransform>();
            ApplySafeArea(_panel);
            var outline = panelGo.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(.83f, .66f, .25f, 1f); outline.effectDistance = new Vector2(3, -3);
            _canvas.SetActive(false);
        }

        private void RenderCategories()
        {
            ClearPanel();
            Header("BUILD COLLECTIONS", BuildFirstUseGuide.Current == BuildFirstUseGuide.Step.Category
                ? BuildFirstUseGuide.Copy : "Choose what the realm needs next.", "CLOSE", Close);
            var grid = Region("CategoryGrid", new Vector2(.04f, .08f), new Vector2(.96f, .78f));
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount; layout.constraintCount = 4;
            layout.spacing = new Vector2(22, 22); layout.padding = new RectOffset(12, 12, 12, 12);
            Canvas.ForceUpdateCanvases();
            float cellWidth = Mathf.Max(150f, (grid.rect.width - layout.padding.horizontal - layout.spacing.x * 3f) / 4f);
            layout.cellSize = new Vector2(cellWidth, Mathf.Max(190f, (grid.rect.height - layout.padding.vertical - layout.spacing.y) / 2f));
            if (_document?.Collections == null) return;
            foreach (var c in _document.Collections)
            {
                if (c == null || !c.Active || !string.Equals(c.Context, "build", StringComparison.OrdinalIgnoreCase)) continue;
                // Category visibility is based on authoritative item eligibility/unlock, never
                // affordability. An unlocked expensive category remains a useful goal; a category
                // whose definitions are all locked/missing/unfinished is not navigation yet.
                if (!CollectionHasVisibleItems(c)) continue;
                var captured = c;
                var card = ButtonBox(grid, c.Title, () =>
                {
                    BuildFirstUseGuide.CategorySelected();
                    if (BuildFirstUseGuide.IsComplete &&
                        string.Equals(captured.CollectionId, "build-defenses", StringComparison.OrdinalIgnoreCase))
                    {
                        Close();
                        PanelRouter.Open(PanelId.Manage, "Defense");
                        return;
                    }
                    OpenCollection(captured);
                });
                var titleLabel = card.GetComponentInChildren<TextMeshProUGUI>();
                if (titleLabel != null)
                {
                    var tr = titleLabel.rectTransform; tr.anchorMin = new Vector2(.05f, .82f); tr.anchorMax = new Vector2(.95f, .98f);
                    tr.offsetMin = tr.offsetMax = Vector2.zero; titleLabel.fontSize = 27;
                }
                var icon = ElarionUiKit.AddImage(card.transform, "Icon", new Vector2(.32f, .29f), new Vector2(.68f, .80f), Color.white, false);
                var image = icon.GetComponent<Image>(); image.preserveAspect = true; image.raycastTarget = false;
                SetArtworkOrFallback(icon.transform, image, Resources.Load<Sprite>(c.IconKey));
                Label(card.transform, c.Subtitle, 25, TextAlignmentOptions.Center, new Vector2(.06f, .05f), new Vector2(.94f, .28f));
            }
        }

        private async void OpenCollection(CardCollectionDefinition collection)
        {
            _collection = collection; _remoteCollection = null; _page = 0; _focus.Push(); RenderCollection();
            CardCollectionModel loaded = await _remote.ResolveAsync(collection.CollectionId, Application.version);
            if (_collection != collection || !IsOpen) return;
            _remoteCollection = loaded;
            RenderCollection();
        }

        private void RenderCollection()
        {
            ClearPanel();
            var visibleIds = VisibleItemIds();
            int count = visibleIds.Count;
            int pages = CardCollectionPaging.PageCount(count);
            Header(_collection?.Title?.ToUpperInvariant() ?? "BUILD",
                BuildFirstUseGuide.Current == BuildFirstUseGuide.Step.Item
                    ? BuildFirstUseGuide.Copy : (_collection?.Subtitle ?? ""), "BACK", Back);
            var row = Region("Cards", new Vector2(.03f, .13f), new Vector2(.97f, .80f));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 18; layout.padding = new RectOffset(10, 10, 6, 6); layout.childForceExpandWidth = true; layout.childForceExpandHeight = true;
            int first = CardCollectionPaging.FirstIndex(_page, count);
            int last = Math.Min(first + CardsPerPage, count);
            for (int i = first; i < last; i++)
            {
                BuildItemCard(row, visibleIds[i]);
            }
            if (pages > 1)
            {
                var prev = FooterButton("PREVIOUS", new Vector2(.04f, .035f), new Vector2(.22f, .11f), () => { _page = Math.Max(0, _page - 1); RenderCollection(); });
                prev.interactable = _page > 0;
                Label(_panel, "PAGE " + (_page + 1) + " / " + pages, 26, TextAlignmentOptions.Center, new Vector2(.42f, .035f), new Vector2(.58f, .11f));
                var next = FooterButton("NEXT", new Vector2(.78f, .035f), new Vector2(.96f, .11f), () => { _page = Math.Min(pages - 1, _page + 1); RenderCollection(); });
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
            var card = Box("BuildCard_" + itemId, parent, new Color(.09f, .11f, .16f, 1f));
            var outline = card.gameObject.AddComponent<Outline>(); outline.effectColor = locked ? new Color(.5f,.5f,.55f,1) : new Color(.5f,.4f,.2f,1);
            Label(card, vm?.DisplayName ?? Humanize(itemId), 32, TextAlignmentOptions.Center, new Vector2(.05f,.86f), new Vector2(.95f,.98f));
            var artGo = ElarionUiKit.AddImage(card, "Artwork", new Vector2(.12f,.54f), new Vector2(.88f,.84f), Color.white, false);
            var image = artGo.GetComponent<Image>(); image.preserveAspect = true; image.raycastTarget = false;
            SetArtworkOrFallback(artGo.transform, image, BuildPaletteUI.ResolveEntryArtPublic(entry));
            Label(card, vm?.Description ?? "Catalog definition unavailable.", 25, TextAlignmentOptions.TopLeft, new Vector2(.07f,.30f), new Vector2(.93f,.53f));
            string cost = vm == null ? "Cost unavailable" : CostWords(vm.EffectiveCost, vm.Freebie);
            Label(card, "COST: " + cost, 25, TextAlignmentOptions.Center, new Vector2(.06f,.20f), new Vector2(.94f,.29f));
            string state = locked ? "LOCKED — unavailable" : built ? "BUILT — one allowed" : !vm.Affordable ? "NEED MORE RESOURCES" : "AVAILABLE";
            if (progressionLocked) state = "Locked - " + lockReason;
            Label(card, (locked ? "[LOCKED] " : "[READY] ") + state, 25, TextAlignmentOptions.Center, new Vector2(.06f,.12f), new Vector2(.94f,.20f));
            // The complete state already lives in the wrapping status band above. Button faces
            // carry only a short action/state word so the kit never ellipsizes required copy.
            string buttonFace = available ? "PLACE" : built ? "BUILT" : !locked ? "NEED RESOURCES" : "UNAVAILABLE";
            var place = ButtonBox(card, buttonFace, () => Place(entry));
            var pr = place.GetComponent<RectTransform>(); pr.anchorMin = new Vector2(.10f,.02f); pr.anchorMax = new Vector2(.90f,.115f); pr.offsetMin = pr.offsetMax = Vector2.zero;
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
            BuildFirstUseGuide.ItemSelected();
            var callback = _place;
            Close(); // release the browsing pause before the existing Arm seam begins placement
            callback?.Invoke(entry);
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

        private void Back() { if (_collection == null) { Close(); return; } _focus.Pop(); _collection = null; _remoteCollection = null; RenderCategories(); }

        private void Header(string title, string subtitle, string closeText, Action close)
        {
            Label(_panel, title, 42, TextAlignmentOptions.Left, new Vector2(.04f,.88f), new Vector2(.72f,.98f));
            Label(_panel, subtitle, 27, TextAlignmentOptions.Left, new Vector2(.04f,.80f), new Vector2(.76f,.89f));
            var b = FooterButton(closeText, new Vector2(.80f,.87f), new Vector2(.96f,.96f), close); b.gameObject.name = "CloseButton";
        }

        private Button FooterButton(string text, Vector2 min, Vector2 max, Action action)
        { var go = ButtonBox(_panel, text, action); var rt = go.GetComponent<RectTransform>(); rt.anchorMin=min;rt.anchorMax=max;rt.offsetMin=rt.offsetMax=Vector2.zero; return go.GetComponent<Button>(); }
        private RectTransform Region(string name, Vector2 min, Vector2 max)
        { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(_panel,false); var rt=go.GetComponent<RectTransform>();rt.anchorMin=min;rt.anchorMax=max;rt.offsetMin=rt.offsetMax=Vector2.zero;return rt; }
        private void ClearPanel() { for (int i=_panel.childCount-1;i>=0;i--) Destroy(_panel.GetChild(i).gameObject); }
        private static GameObject ButtonBox(Transform parent, string text, Action click)
        { return ElarionUiKit.Button(parent,text,ElarionUiKit.ButtonKind.Quiet,Vector2.zero,Vector2.one,click).gameObject; }
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
        private static void Stretch(RectTransform rt) { rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero; }
        private static void ApplySafeArea(RectTransform rt)
        { var s=Screen.safeArea;float w=Math.Max(1,Screen.width);float h=Math.Max(1,Screen.height);var min=new Vector2(s.xMin/w,s.yMin/h);var max=new Vector2(s.xMax/w,s.yMax/h);var d=max-min;rt.anchorMin=min+d*.10f;rt.anchorMax=max-d*.10f;rt.offsetMin=rt.offsetMax=Vector2.zero; }
        private static string Humanize(string id) => string.IsNullOrEmpty(id) ? "Unknown" : id.Replace('_',' ').Replace('-',' ');
        private static string CostWords(DeNelle.Core.Catalog.ResourceCost c,bool free)
        { if(free)return "NO COST";var p=new List<string>();if(c.wood>0)p.Add(c.wood+" Wood");if(c.food>0)p.Add(c.food+" Stone");if(c.iron>0)p.Add(c.iron+" Iron");if(c.crystals>0)p.Add(c.crystals+" Crystals");return p.Count==0?"No resources":string.Join(" | ",p); }
        private void OnDisable()
        {
            ProgressionUnlocks.Changed -= OnProgressionUnlockChanged;
            StructureSingleton.SingletonResolved -= OnFiniteCapacityChanged;
            StructureSingleton.SingletonReleased -= OnFiniteCapacityChanged;
            Close();
        }
        private void OnDestroy()
        {
            ProgressionUnlocks.Changed -= OnProgressionUnlockChanged;
            StructureSingleton.SingletonResolved -= OnFiniteCapacityChanged;
            StructureSingleton.SingletonReleased -= OnFiniteCapacityChanged;
            Close();
        }
    }
}
