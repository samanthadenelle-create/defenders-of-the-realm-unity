// =============================================================================
// AdminOverlay — owner-only debug controls. Trigger waves, give crystals,
// reset the save, toggle the cold open, etc.
// -----------------------------------------------------------------------------
// Owner-gate: matches the wallet address bound on GameStateService.State
// against AdminOverlay.OwnerWalletAddress. Until the owner's address is
// pasted in (or until the Connect Wallet flow lands in Week 7), the overlay
// is reachable via the debug chord Ctrl+Shift+A.
//
// All actions call through reflection so the HUD asmdef stays decoupled from
// DeNelle.Village / DeNelle.Core.State (which already do reference Core).
// =============================================================================

using System;
using System.Reflection;
using DeNelle.Core.Catalog;
using DeNelle.Core.UI;
using UnityEngine;
using UnityEngine.UIElements;
using PanelMgr = DeNelle.Core.UI.PanelManager;

namespace DeNelle.HUD
{
    [DisallowMultipleComponent]
    public sealed class AdminOverlay : MonoBehaviour
    {
        /// <summary>
        /// Paste the owner's Solana wallet address here (lower-cased). Until
        /// then the overlay is reachable only via the debug chord.
        /// </summary>
        public const string OwnerWalletAddress = ""; // TODO(owner)

        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _overlay;
        private Label _status;
        private bool _bound;

        // DEF-212 modal arbiter handle (same discipline as HelpMenu / the shop panels).
        private PanelHandle _panelHandle;

        // Dev orient tool — catalog id the owner types in.
        private TextField _orientIdField;

        // Reflection handles — resolved lazily on first show.
        private Type _gameStateServiceType;
        private object _gameStateInstance;
        private object _gameStateState;
        private Type _waveManagerType;
        private object _waveManagerInstance;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            if (_document == null) _document = gameObject.AddComponent<UIDocument>();
            if (_document.panelSettings == null)
            {
                foreach (var existing in UnityEngine.Object.FindObjectsByType<UIDocument>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (existing == _document || existing.panelSettings == null) continue;
                    _document.panelSettings = existing.panelSettings;
                    break;
                }
            }
            if (_document.panelSettings == null) { enabled = false; return; }
            // DEF: HelpMenu sits at sortingOrder 2700 (it raised itself over the town
            // mini-map canvas long after this 170 comment was written). At 170 the admin
            // overlay opened BENEATH the Help panel + town HUD, so "Dev tools" appeared to
            // do nothing. Sit just above HelpMenu so the dev panel is actually visible.
            _document.sortingOrder = 2710; // just above HelpMenu (2700)
            BuildUi();
            // Route through the single-modal arbiter (DEF-212) so opening the admin overlay
            // closes any other open panel (incl. Help) and closing clears the slot.
            _panelHandle = PanelMgr.Register("Admin", Close, () => IsOpen);
        }

        private void OnDestroy()
        {
            PanelMgr.NotifyClosed(_panelHandle);
        }

        /// <summary>True while the admin overlay is visible (its backdrop is pickable).</summary>
        public bool IsOpen =>
            _overlay != null && _overlay.style.display != DisplayStyle.None;

        private void Update()
        {
            // Debug chord: Ctrl + Shift + A → toggle overlay. Survives the
            // pre-wallet build state. Uses legacy Input Manager since the HUD
            // asmdef doesn't reference Unity.InputSystem.
            if (Input.GetKeyDown(KeyCode.A) &&
                (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) &&
                (Input.GetKey(KeyCode.LeftShift)   || Input.GetKey(KeyCode.RightShift)))
            {
                Toggle();
            }
        }

        // ── UI ──────────────────────────────────────────────────────────────
        private void BuildUi()
        {
            _root = _document.rootVisualElement;
            _root.Clear();
            _root.pickingMode = PickingMode.Ignore;
            _root.style.position = Position.Absolute;
            _root.style.left = 0; _root.style.right = 0;
            _root.style.top = 0;  _root.style.bottom = 0;

            _overlay = new VisualElement();
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = 0; _overlay.style.right = 0;
            _overlay.style.top = 0;  _overlay.style.bottom = 0;
            _overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.86f);
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            _overlay.style.display = DisplayStyle.None;
            _root.Add(_overlay);

            var card = new VisualElement();
            card.style.minWidth = 420; card.style.maxWidth = 560;
            card.style.paddingTop = 22;  card.style.paddingBottom = 22;
            card.style.paddingLeft = 26; card.style.paddingRight = 26;
            // Stone panel from the shared theme, but with a DANGER-red rim so the
            // owner-only debug overlay reads as "admin / careful", still in-family.
            card.style.backgroundColor = ElarionUi.PanelStoneDark;
            ElarionUi.SetRadius(card, ElarionUi.RadiusLg);
            ElarionUi.SetBorderWidth(card, 2);
            ElarionUi.SetBorderColor(card, new Color(ElarionUi.Danger.r, ElarionUi.Danger.g, ElarionUi.Danger.b, 0.75f));
            _overlay.Add(card);

            var title = new Label(ElarionUi.CrestGlyph + "  Admin — owner-only");
            title.style.fontSize = ElarionUi.FontTitle;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = ElarionUi.Danger;
            title.style.marginBottom = 6;
            card.Add(title);
            card.Add(ElarionUi.MakeRule());

            // Owner-trimmed (2026-06-11): only the two controls used live remain — a WORKING
            // full-resource grant (through the same EconomyService wallet the shop spends from,
            // so you can actually buy) + the Yarn/tutorial reset. The rest (wave trigger,
            // onboarded toggles, save, reset-save, orient tool) are dropped from the panel; their
            // handlers stay in the file in case they're wanted back.
            card.Add(Button("Load resources (full base)",   OnLoadResources));
            card.Add(Button("Reset Yarn (replay tutorial)", OnReplayTutorial));
            card.Add(Button("Close",                        Toggle));

            _status = new Label(string.Empty);
            _status.style.color = ElarionUi.ParchmentDim;
            _status.style.fontSize = ElarionUi.FontLabel;
            _status.style.marginTop = 8;
            _status.style.whiteSpace = WhiteSpace.Normal;
            card.Add(_status);
        }

        private static Button Button(string label, Action onClick)
        {
            var b = new Button(onClick) { text = label };
            ElarionUi.StyleButton(b, ElarionUi.ButtonKind.Neutral);
            b.style.minHeight = 38;   // compact debug rows (override the 44 default)
            b.style.unityFontStyleAndWeight = FontStyle.Normal;
            return b;
        }

        // ── Dev orient tool row ──────────────────────────────────────────────
        private VisualElement BuildOrientRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems    = Align.Center;
            row.style.marginTop = 8; row.style.marginBottom = 4;

            // Relabelled "crafting id" → "catalog id" — the field takes a CatalogRegistry id.
            var idLabel = new Label("catalog id");
            idLabel.style.color = ElarionUi.ParchmentDim;
            idLabel.style.fontSize = ElarionUi.FontLabel;
            idLabel.style.width = 70;
            row.Add(idLabel);

            _orientIdField = new TextField { value = "" };
            _orientIdField.style.flexGrow = 1;
            _orientIdField.style.marginRight = 6;
            var input = _orientIdField.Q(className: "unity-text-field__input");
            if (input != null)
            {
                input.style.backgroundColor = new Color(0.05f, 0.04f, 0.03f, 1f);
                input.style.color = ElarionUi.Parchment;
            }
            // Placeholder via tooltip (UIElements 2021 has no native placeholder).
            _orientIdField.tooltip = "catalog id, e.g. mill or tower_ground_archer";
            row.Add(_orientIdField);

            var btn = Button("Orient Asset", OnOrientAsset);
            btn.style.marginTop = 0; btn.style.marginBottom = 0;
            btn.style.width = 130;
            row.Add(btn);
            return row;
        }

        private void OnOrientAsset()
        {
            string id = _orientIdField != null ? (_orientIdField.value ?? string.Empty).Trim() : string.Empty;
            if (string.IsNullOrEmpty(id)) { SetStatus("Orient: type a catalog id first."); return; }

            // Resolve the prefab path: prefer the CatalogEntry's visualPrefabPath;
            // fall back to treating the typed id itself as a Resources path.
            string prefabPath = null;
            string displayName = id;
            var entry = CatalogRegistry.Get(id);
            if (entry != null)
            {
                prefabPath  = entry.visualPrefabPath;
                displayName = !string.IsNullOrEmpty(entry.displayName) ? entry.displayName : id;
            }
            if (string.IsNullOrEmpty(prefabPath)) prefabPath = id;   // raw-path fallback

            var prefab = Resources.Load<GameObject>(prefabPath);
            if (prefab == null)
            {
                SetStatus(entry == null
                    ? $"Orient: id '{id}' not in CatalogRegistry and not loadable as a Resources path."
                    : $"Orient: '{id}' found, but Resources.Load failed for '{prefabPath}'.");
                return;
            }

            if (!OpenOrientMenu(id, prefab, displayName))
            {
                SetStatus("Orient: could not open TowerPlacementRotateMenu (DeNelle.Village missing?).");
                return;
            }

            // Hide the admin overlay so the orient panel is unobstructed (route through
            // the arbiter so the modal slot is cleared, not just the display flag).
            Close();
            SetStatus($"Orienting '{id}'. Confirm in the panel — recipe logs to Console ([OrientRecipe]).");
        }

        /// <summary>
        /// Find-or-create the DeNelle.Village TowerPlacementRotateMenu and invoke
        /// its OpenDevOrient(string,GameObject,string) via reflection (HUD asmdef
        /// does not reference DeNelle.Village). Returns false if the type is absent.
        /// </summary>
        private bool OpenOrientMenu(string id, GameObject prefab, string displayName)
        {
            var menuType = Type.GetType("DeNelle.Village.TowerPlacementRotateMenu, DeNelle.Village");
            if (menuType == null) return false;

            var menu = UnityEngine.Object.FindObjectOfType(menuType);
            if (menu == null)
            {
                var go = new GameObject("DevOrientMenu");
                menu = go.AddComponent(menuType);
            }

            var open = menuType.GetMethod("OpenDevOrient",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(string), typeof(GameObject), typeof(string) },
                null);
            if (open == null) return false;

            open.Invoke(menu, new object[] { id, prefab, displayName });
            return true;
        }

        public void Toggle() => SetOpen(!IsOpen);

        /// <summary>Show the admin overlay (Help menu's "Dev tools" routes here).</summary>
        public void Open() => SetOpen(true);

        /// <summary>Hide the admin overlay (Close button + modal-arbiter close).</summary>
        public void Close() => SetOpen(false);

        private void SetOpen(bool open)
        {
            if (_overlay == null) return;
            // Resolve gate on demand.
            if (!IsAuthorised())
            {
                // Owner gate failed — still allow if the debug chord was used
                // (it's the only way to get here at all right now). The chord
                // requires Ctrl+Shift+A so it's not a stranger trigger.
            }
            _overlay.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            _overlay.pickingMode = open ? PickingMode.Position : PickingMode.Ignore;
            // Single-modal arbiter (DEF-212): opening closes any other open panel; closing
            // clears our slot so the invisible-but-pickable backdrop can't trap input.
            if (open) PanelMgr.NotifyOpened(_panelHandle);
            else PanelMgr.NotifyClosed(_panelHandle);
            if (open) SetStatus("Ready.");
        }

        private bool IsAuthorised()
        {
            if (string.IsNullOrEmpty(OwnerWalletAddress)) return false;
            ResolveGameState();
            if (_gameStateState == null) return false;
            var addr = GetMember<string>(_gameStateState, "BoundWallet");
            return addr != null && addr.Equals(OwnerWalletAddress, StringComparison.OrdinalIgnoreCase);
        }

        // ── Reflection helpers ──────────────────────────────────────────────
        private void ResolveGameState()
        {
            if (_gameStateInstance != null && _gameStateState != null) return;
            _gameStateServiceType = Type.GetType("DeNelle.Core.State.GameStateService, DeNelle.Core");
            if (_gameStateServiceType == null) return;
            var instanceProp = _gameStateServiceType.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static);
            _gameStateInstance = instanceProp?.GetValue(null);
            if (_gameStateInstance == null) return;
            var stateProp = _gameStateServiceType.GetProperty("State",
                BindingFlags.Public | BindingFlags.Instance);
            _gameStateState = stateProp?.GetValue(_gameStateInstance);
        }

        private void ResolveWaveManager()
        {
            if (_waveManagerInstance != null) return;
            _waveManagerType = Type.GetType("DeNelle.Village.WaveManager, DeNelle.Village");
            if (_waveManagerType == null) return;
            _waveManagerInstance = UnityEngine.Object.FindObjectOfType(_waveManagerType);
        }

        private static T GetMember<T>(object obj, string name) where T : class
        {
            var t = obj.GetType();
            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (p != null) return p.GetValue(obj) as T;
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            return f?.GetValue(obj) as T;
        }

        private static void SetField(object obj, string name, object value)
        {
            var t = obj.GetType();
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (f != null) { f.SetValue(obj, value); return; }
            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            p?.SetValue(obj, value);
        }

        private void InvokeMethod(object obj, string method, params object[] args)
        {
            if (obj == null) return;
            var m = obj.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.Instance);
            if (m == null) { SetStatus($"Method '{method}' not found."); return; }
            m.Invoke(obj, args);
        }

        // ── Action handlers ─────────────────────────────────────────────────
        private void OnTriggerWave()
        {
            ResolveWaveManager();
            InvokeMethod(_waveManagerInstance, "ForceBeginNextWave");
            SetStatus("Triggered ForceBeginNextWave() — if missing, will need to add the public method.");
        }

        private void OnGiveCrystals(int delta)
        {
            ResolveGameState();
            if (_gameStateInstance == null || _gameStateState == null)
            {
                SetStatus("GameStateService not alive yet.");
                return;
            }
            // State has Resources.Crystals (nested struct) per the SaveSchema; just
            // call an "AddCrystals" if it exists, else log the gap.
            InvokeMethod(_gameStateInstance, "AddCrystals", delta);
            SetStatus($"+{delta} crystals requested (if AddCrystals isn't defined, owner adds it).");
        }

        private void OnSetOnboarded(bool value)
        {
            ResolveGameState();
            if (_gameStateState == null) { SetStatus("State unavailable."); return; }
            SetField(_gameStateState, "Onboarded", value);
            InvokeMethod(_gameStateInstance, "Save");
            SetStatus($"Onboarded set to {value} + saved.");
        }

        private void OnSave()
        {
            ResolveGameState();
            InvokeMethod(_gameStateInstance, "Save");
            SetStatus("Saved.");
        }

        private void OnReset()
        {
            ResolveGameState();
            InvokeMethod(_gameStateInstance, "ResetToNewGame");

            // ResetToNewGame() clears Onboarded + the party roster but NOT the
            // FTUE gate. The intro Yarn (CompanionMeetingTrigger) is gated once
            // per save via PlayerPrefs "yarn.companionMeeting.seen" (matches
            // CompanionMeetingTrigger.SeenKey). If we leave it set, the tutorial
            // Yarn short-circuits, FinishOnboarding never runs, AddToParty never
            // fires, and the player lands with an empty roster / no companion.
            PlayerPrefs.DeleteKey("yarn.companionMeeting.seen");
            PlayerPrefs.Save();

            // The in-place reset doesn't reload the scene, so the trigger's
            // _hostedThisSession latch and TutorialDirector.s_ranThisSession can't
            // re-fire this session. Reload the village scene so a devtools reset
            // behaves like a real New Game -> tutorial runs -> companion joins.
            UnityEngine.SceneManagement.SceneManager.LoadScene("Village2");
            SetStatus("Reset(): new game -> FTUE gate cleared, reloading Village2 (tutorial + companion re-fire).");
        }

        /// <summary>
        /// Grants a full base of SPENDABLE resources through EconomyService.GrantSpendable —
        /// which lands Wood/Iron in BOTH wallets the game keeps: the in-session pool the shop +
        /// HUD bar read AND GameState.Wood/Iron the structure-upgrade flow (ResourceLedger)
        /// spends. Plain Grant only filled the in-session pool, so dev-granted Wood/Iron was
        /// unspendable in the upgrade flow. HUD asmdef can't reference DeNelle.Village, so
        /// EconomyService is reached by reflection (same idiom as the orient menu / wave manager).
        /// </summary>
        private void OnLoadResources()
        {
            var ecoType = Type.GetType("DeNelle.Village.EconomyService, DeNelle.Village");
            var instProp = ecoType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            var eco = instProp?.GetValue(null);
            if (eco == null) { SetStatus("Resources: EconomyService not alive yet."); return; }

            // GrantSpendable(int wood = 0, int food = 0, int iron = 0, int crystals = 0) —
            // mirrors Wood/Iron into GameState so the upgrade flow can spend them too.
            var grant = ecoType.GetMethod("GrantSpendable",
                BindingFlags.Public | BindingFlags.Instance, null,
                new[] { typeof(int), typeof(int), typeof(int), typeof(int) }, null);
            if (grant == null) { SetStatus("Resources: EconomyService.GrantSpendable(int,int,int,int) not found."); return; }

            grant.Invoke(eco, new object[] { 50000, 25000, 50000, 25000 }); // wood, food, iron, crystals
            SetStatus("Loaded: +50k Wood, +50k Iron, +25k Food, +25k Crystals (shop + upgrades) — now buy something.");
        }

        private void OnReplayTutorial()
        {
            // Force the intro Yarn to replay WITHOUT wiping progress (unlike Reset):
            // clear the FTUE gate then reload the village so CompanionMeetingTrigger +
            // TutorialDirector re-fire (their session latches reset on scene load). The
            // companion re-joins on tutorial completion. Key = CompanionMeetingTrigger.SeenKey.
            PlayerPrefs.DeleteKey("yarn.companionMeeting.seen");
            PlayerPrefs.Save();
            // Owner-requested: a Yarn reset drops back to Character Select so the whole
            // onboarding flow (HeroSelect -> PetSelect -> Village -> tutorial) replays.
            DeNelle.Core.SceneRouter.GoHeroSelect();
            SetStatus("Replay Tutorial: FTUE gate cleared — dropping to Character Select (HeroSelect).");
        }

        private void SetStatus(string s)
        {
            if (_status != null) _status.text = s;
        }
    }
}
