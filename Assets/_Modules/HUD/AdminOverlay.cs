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
using DeNelle.Core.Diagnostics;
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

        // True once BuildUi() has actually run (i.e. a PanelSettings was found and the
        // overlay VisualElements exist). When false, Open() would silently no-op — the
        // T-030 "dev tools goes nowhere" failure in scenes (MainCastle_Hall) that ship
        // NO UIDocument/PanelSettings of their own, so the Awake-time borrow finds none.
        private bool _built;

        private void Awake()
        {
            TryBuild(null);
            // Route through the single-modal arbiter (DEF-212) so opening the admin overlay
            // closes any other open panel (incl. Help) and closing clears the slot.
            // Registered even if the UI hasn't built yet — re-registering is harmless and
            // the handle is needed the moment a later TryBuild() succeeds.
            _panelHandle = PanelMgr.Register("Admin", Close, () => IsOpen);
        }

        /// <summary>
        /// Build the overlay UI if it hasn't been built yet. Needs a PanelSettings, which
        /// it borrows from any UIDocument already in the scene; callers may pass an explicit
        /// <paramref name="fallback"/> (e.g. HelpMenu's live PanelSettings) for scenes that
        /// ship no UIDocument of their own. Returns true once the UI exists.
        /// </summary>
        public bool TryBuild(PanelSettings fallback)
        {
            if (_built) return true;
            _document = GetComponent<UIDocument>();
            if (_document == null) _document = gameObject.AddComponent<UIDocument>();
            // OWN PanelSettings (2026-06-13). We USED to BORROW another UIDocument's
            // panelSettings (HelpMenu's, or the onboarding asset) — but a PanelSettings backs
            // only ONE live panel, so HelpMenu's doc + this doc fought over the same asset and
            // AdminOverlay ended up panel=<null>: "Dev tools" opened (display=Flex) but rendered
            // NOTHING ("clicking devtools disappears"). Create our OWN uniquely-named runtime
            // PanelSettings (borrow only the themeStyleSheet for fonts/colors), so the dev panel
            // is independent of HelpMenu's lifecycle. Mirrors the HelpMenu own-PanelSettings fix.
            if (_document.panelSettings == null)
            {
                var ps = ScriptableObject.CreateInstance<PanelSettings>();
                ps.name = "AdminRuntimePanelSettings";
                if (fallback != null && fallback.themeStyleSheet != null)
                {
                    ps.themeStyleSheet = fallback.themeStyleSheet;
                }
                else
                {
                    foreach (var existing in UnityEngine.Object.FindObjectsByType<UIDocument>(
                                 FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        if (existing == _document || existing.panelSettings == null) continue;
                        if (existing.panelSettings.themeStyleSheet != null)
                        {
                            ps.themeStyleSheet = existing.panelSettings.themeStyleSheet;
                            break;
                        }
                    }
                }
                _document.panelSettings = ps;
            }
            // The DEV overlay must sit ABOVE EVERY in-game panel so it is always usable — incl. the
            // vendor/shop modals (uGUI Canvas at sortingOrder 31000 + a full-screen scrim). At the old
            // 2710 the dev panel opened BENEATH an open shop's scrim after talking to a vendor, so its
            // buttons were non-clickable. 32000 keeps it on top of the 31000 shop + everything below.
            _document.sortingOrder = 32000; // topmost — above the 31000 shop/vendor modals
            // FIX (RCA 2026-06-21, data-proven sortOrder=0 at runtime): UIDocument.sortingOrder does NOT
            // reliably propagate to PanelSettings.sortingOrder — and input dispatch reads the PanelSettings.
            // Set it on the (own, runtime) PanelSettings so the layering is REAL (dev panel above the shop).
            if (_document.panelSettings != null) _document.panelSettings.sortingOrder = 32000;
            BuildUi();
            _built = true;
            return true;
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
            // PLAYER-BUILD SAFETY: the admin chord is gated behind the global
            // DevHotkeys kill-switch (default OFF) so it can never pop the owner-only
            // admin overlay in the shipped .exe OR the editor unless a dev opts in
            // (PlayerPrefs ff.devhotkeys=1). The Help menu's "Dev tools" button
            // (AdminOverlay.Open) remains the always-available entry.
            if (!DeNelle.Core.FeatureFlags.DevHotkeys) return;
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
            { var tf = AdminFont(); if (tf != null) title.style.unityFont = tf; }
            card.Add(title);
            card.Add(ElarionUi.MakeRule());

            // Owner-trimmed (2026-06-11): only the two controls used live remain — a WORKING
            // full-resource grant (through the same EconomyService wallet the shop spends from,
            // so you can actually buy) + the Yarn/tutorial reset. The rest (wave trigger,
            // onboarded toggles, save, reset-save, orient tool) are dropped from the panel; their
            // handlers stay in the file in case they're wanted back.
            card.Add(Button("Load resources (full base)",   OnLoadResources));
            // Level shortcuts — same REAL leveling path the F10 DevPanel uses
            // (HeroProgression.AddXp -> ApplyLevelRewards grants Wisdom + skill points),
            // reached by reflection here since the HUD asmdef can't reference DeNelle.Village.
            card.Add(Button("Set Level 5 (+skill pts)",     () => OnSetHeroLevel(5)));
            card.Add(Button("Set Level 10 (+skill pts)",    () => OnSetHeroLevel(10)));
            card.Add(Button("Trigger next wave",            OnTriggerWave));
            card.Add(Button("VFX Parade",                   OnVfxParade));
            card.Add(Button("Reset Yarn (replay tutorial)", OnReplayTutorial));
            card.Add(Button("Close",                        Toggle));
            FlowTrace.Step("UI", "DevPanel (AdminOverlay) UI built — wired 6 buttons");

            _status = new Label(string.Empty);
            _status.style.color = ElarionUi.ParchmentDim;
            _status.style.fontSize = ElarionUi.FontLabel;
            { var sf = AdminFont(); if (sf != null) _status.style.unityFont = sf; }
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
            var f = AdminFont(); if (f != null) b.style.unityFont = f;
            return b;
        }

        // WO-417: explicit font so admin-overlay text renders even when the borrowed
        // PanelSettings theme has no default font (blank rows = backgrounds draw, glyphs don't).
        private static Font _adminFont;
        private static Font AdminFont()
        {
            if (_adminFont == null) _adminFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _adminFont;
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
            { var lf = AdminFont(); if (lf != null) idLabel.style.unityFont = lf; }
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
            FlowTrace.Step("UI", $"DevPanel toggle/click reached (AdminOverlay.SetOpen open={open}, built={_built})");
            if (_overlay == null)
            {
                FlowTrace.Warn("UI", "DevPanel open FAILED — AdminOverlay._overlay is null (UI never built)");
                return;
            }
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
            FlowTrace.Step("UI", $"DevPanel (AdminOverlay) {(open ? "shown" : "hidden")} — " +
                $"display={_overlay.style.display.value} picking={_overlay.pickingMode} timeScale={Time.timeScale}");
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
            // Force a FRESH resolve every click. _waveManagerInstance is held as `object`, so the
            // `!= null` guard in ResolveWaveManager uses reference equality — it does NOT see Unity's
            // fake-null on a DESTROYED WaveManager from a prior scene, so the cached ref goes stale
            // after a scene change and the trigger silently no-ops (WO-327). Re-find the live one.
            _waveManagerInstance = null;
            ResolveWaveManager();
            if (_waveManagerInstance == null)
            {
                SetStatus("No WaveManager in this scene — nothing to trigger.");
                return;
            }
            InvokeMethod(_waveManagerInstance, "ForceSpawnNextWaveNow");
            SetStatus("Jumped to next wave (ForceSpawnNextWaveNow — spawns immediately, skips countdown).");
        }

        /// <summary>
        /// Launches the RUNTIME VFX Parade overlay (VfxParade.VfxParadeRuntime) so the
        /// owner can curate effects in the built exe with no editor open. The HUD asmdef
        /// does not reference VfxParade.Runtime, so the singleton is created + shown via
        /// reflection (same idiom as the orient menu / wave manager). The overlay pauses
        /// time itself and restores it on Close; this just opens it and hides the admin
        /// panel so it is unobstructed.
        /// </summary>
        private void OnVfxParade()
        {
            var runtimeType = Type.GetType("VfxParade.VfxParadeRuntime, VfxParade.Runtime");
            if (runtimeType == null)
            {
                SetStatus("VFX Parade: VfxParade.Runtime assembly/type not found in this build.");
                return;
            }

            var launch = runtimeType.GetMethod("Launch",
                BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (launch == null)
            {
                SetStatus("VFX Parade: VfxParadeRuntime.Launch() not found.");
                return;
            }

            object instance = null;
            try { instance = launch.Invoke(null, null); }
            catch (Exception e)
            {
                SetStatus("VFX Parade: launch threw - " + e.Message);
                return;
            }

            if (instance == null)
            {
                SetStatus("VFX Parade: Launch returned null (no manifest baked? run VfxParadeManifestBuilder.Build).");
                return;
            }

            // Hide the admin overlay so the parade panel is unobstructed (route through the
            // arbiter so the modal slot is cleared, not just the display flag).
            Close();
            SetStatus("VFX Parade opened. Use Next/Prev, tag a moment + note, Bookmark -> vfx-picks.json.");
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

            // Gold/Coins: GrantSpendable has NO coins param, so this dev grant never gave gold.
            // AddCoins (public) tops up the shop/sell wallet (GameState.Resources.Coins) and fires
            // ResourcesChanged so the HUD gold readout updates. Reflected (HUD can't ref Village).
            var addCoins = ecoType.GetMethod("AddCoins",
                BindingFlags.Public | BindingFlags.Instance, null,
                new[] { typeof(int) }, null);
            if (addCoins != null) addCoins.Invoke(eco, new object[] { 50000 });

            // dev-grant-both-wallets fix: log the granted amounts + the resulting BOTH-store
            // totals (in-session pool via Snapshot + persisted GameState.Wood/Iron) so a dev
            // grant is traceable end-to-end — proves Wood/Iron landed in shop/HUD AND upgrade flow.
            LogGrantTrace(eco, ecoType, 50000, 25000, 50000, 25000);

            // GrantSpendable fires EconomyService.OnChanged, which HeartHudBridge uses to
            // refresh the on-screen resource bar. Belt-and-braces: push the wallet straight
            // to the VillageHudController too so the bar populates immediately even if the
            // bridge isn't subscribed yet in this scene (e.g. the castle hub bootstrap race).
            PingResourceBar(eco, ecoType);

            SetStatus("Loaded: +50k Gold, +50k Wood, +50k Iron, +25k Food, +25k Crystals (shop + upgrades) — now buy something.");
        }

        /// <summary>
        /// Forces the on-screen top resource bar to refresh from the EconomyService
        /// wallet immediately after a grant. EconomyService.Snapshot is a struct in
        /// DeNelle.Village (read by reflection — HUD can't reference Village), but the
        /// VillageHudController lives in THIS assembly so its SetResources is called
        /// directly. Guarantees the bar populates even if HeartHudBridge isn't yet
        /// subscribed in the loaded scene.
        /// </summary>
        private void PingResourceBar(object eco, Type ecoType)
        {
            if (eco == null || ecoType == null) return;

            var hud = UnityEngine.Object.FindAnyObjectByType<VillageHudController>();
            if (hud == null) return;

            var snapProp = ecoType.GetProperty("Snapshot", BindingFlags.Public | BindingFlags.Instance);
            if (snapProp == null) return;
            var snap = snapProp.GetValue(eco);
            if (snap == null) return;

            var snapType = snap.GetType();
            int wood     = GetIntField(snapType, snap, "Wood");
            int iron     = GetIntField(snapType, snap, "Iron");
            int food     = GetIntField(snapType, snap, "Food");
            int crystals = GetIntField(snapType, snap, "Crystals");

            // HUD signature: SetResources(wood, iron, food, gems).
            hud.SetResources(wood, iron, food, crystals);
            hud.SetCrystals(crystals);
        }

        private static int GetIntField(Type t, object obj, string name)
        {
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (f != null && f.GetValue(obj) is int vi) return vi;
            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.GetValue(obj) is int pi) return pi;
            return 0;
        }

        /// <summary>
        /// dev-grant-both-wallets fix: emits a FlowTrace("Eco") line with the granted
        /// amounts AND the resulting totals from BOTH stores — the in-session pool
        /// (EconomyService.Snapshot, read by reflection) plus GameState.Wood/Iron (the
        /// upgrade flow's wallet). Confirms a single dev grant filled both wallets.
        /// </summary>
        private void LogGrantTrace(object eco, Type ecoType, int wood, int food, int iron, int crystals)
        {
            int poolWood = 0, poolIron = 0, poolFood = 0, poolCrys = 0;
            var snapProp = ecoType?.GetProperty("Snapshot", BindingFlags.Public | BindingFlags.Instance);
            var snap = snapProp?.GetValue(eco);
            if (snap != null)
            {
                var st = snap.GetType();
                poolWood = GetIntField(st, snap, "Wood");
                poolIron = GetIntField(st, snap, "Iron");
                poolFood = GetIntField(st, snap, "Food");
                poolCrys = GetIntField(st, snap, "Crystals");
            }

            int gsWood = 0, gsIron = 0;
            ResolveGameState();
            if (_gameStateState != null)
            {
                gsWood = GetMember<object>(_gameStateState, "Wood") is int gw ? gw : 0;
                gsIron = GetMember<object>(_gameStateState, "Iron") is int gi ? gi : 0;
            }

            FlowTrace.Step("Eco",
                $"DevGrant (AdminOverlay) +W{wood} F{food} I{iron} C{crystals} -> " +
                $"pool W{poolWood} I{poolIron} F{poolFood} C{poolCrys} | " +
                $"GameState W{gsWood} I{gsIron}");
        }

        /// <summary>
        /// Sets the hero to <paramref name="target"/> through the SAME real leveling path the
        /// F10 DevPanel's "Set Level 5/10" uses (DevPanelController.SetHeroLevelTo): feed XP via
        /// HeroProgression.AddXp(XpToNext + 1) until the target level is reached, so each level
        /// crossed runs ApplyLevelRewards and banks that level's Wisdom (the skill-tree spend
        /// currency) + a skill point. The HUD asmdef can't reference DeNelle.Village, so the
        /// HeroProgression loop + the resulting Wisdom read are done by reflection (same idiom
        /// as OnLoadResources / the orient menu / the wave manager above). No-op if already
        /// at/above the target.
        /// </summary>
        private void OnSetHeroLevel(int target)
        {
            target = Mathf.Max(1, target);

            var hpType = Type.GetType("DeNelle.Village.HeroProgression, DeNelle.Village");
            var hp = hpType != null ? UnityEngine.Object.FindAnyObjectByType(hpType) : null;
            if (hp == null) { SetStatus("Level: HeroProgression not in scene yet."); return; }

            var levelProp = hpType.GetProperty("Level", BindingFlags.Public | BindingFlags.Instance);
            var xpToNextProp = hpType.GetProperty("XpToNext", BindingFlags.Public | BindingFlags.Instance);
            var addXp = hpType.GetMethod("AddXp",
                BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(float) }, null);
            if (levelProp == null || xpToNextProp == null || addXp == null)
            {
                SetStatus("Level: HeroProgression API (Level/XpToNext/AddXp) not found.");
                return;
            }

            int LevelNow() => levelProp.GetValue(hp) is int l ? l : 0;
            float XpToNextNow() => xpToNextProp.GetValue(hp) is float x ? x : 0f;

            // Mirror SetHeroLevelTo: repeated AddXp(XpToNext + 1) until the target level.
            int guard = 0;
            while (LevelNow() < target && guard++ < 500)
                addXp.Invoke(hp, new object[] { XpToNextNow() + 1f });

            int reached = LevelNow();

            // Resulting Wisdom (skill-tree spend currency) — WisdomCurrencyService.Instance.Wisdom.
            int wisdom = 0;
            var wisType = Type.GetType("DeNelle.Village.Talents.WisdomCurrencyService, DeNelle.Village");
            var wisInst = wisType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (wisInst != null)
            {
                var wisProp = wisType.GetProperty("Wisdom", BindingFlags.Public | BindingFlags.Instance);
                if (wisProp?.GetValue(wisInst) is int w) wisdom = w;
            }

            FlowTrace.Step("Hero",
                $"DevPanel (AdminOverlay) set hero -> Lv.{reached} (target {target}), Wisdom {wisdom}");
            SetStatus($"Set hero to Lv.{reached} (target {target}) — {wisdom} Wisdom to spend in the skill tree.");
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
