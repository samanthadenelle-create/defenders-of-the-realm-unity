// =============================================================================
// HudKitController — assembles the HUD kit: factory widgets in the actionable
// areas, model-bound, posture-occupied from hud-areas.json (P23 HUDKIT).
// (HUD_OBSIDIAN_ARCHITECTURE_2026-07-03 §3.3/§3.4 as A4 area/posture rows.)
// -----------------------------------------------------------------------------
// Assembly: DeNelle.HUD   Namespace: DeNelle.HUD.Kit
//
// MVVM LAW (§5): every widget is built by the ElarionUiKit factory and bound to
// a Core.HudModel model's Changed event — zero raw widget construction, zero
// state pulls, zero .Instance/reflection reads in this file. Commands fire the
// owner VillageHudController's UnityEvents (Village bridges subscribe those) or
// the Core HudCommands sink (Village registers handlers).
//
// THE §0 FELT FIXES delivered here (mechanism in-line at each site):
//   • HP 9/145 renders ~6%  — vitals = BuildNameplate(Player): the §1.1 fill
//     contract (non-null sprite + fillAmount-only) replaces the sprite-less
//     Filled images of BattleHud9Zone.FillBarLeft (:1701) / VillageHudController.
//   • MP live               — bound to HeroVitalsModel (producer follows
//     HeroHealth.Instance); kills the frozen fillAmount=1f (:1878) + SetMana
//     no-op (:2906).
//   • Dead target clears    — BuildTargetFrame.Bind: TargetModel !HasTarget =>
//     total Clear() (fixes the :549 early-return).
//   • Wave chrome law       — waveBlock exists only in the calm(town) row and
//     self-gates to between-waves phases; countdown shows only when real.
//   • No resource flashing  — CurrencyChip.SetAmount count-tweens; no colour
//     flash exists in the component (owner rule enforced by the factory).
//   • 4 round move buttons  — BuildControllerCluster -> HudMoveInput (replaces
//     the square D-pad + VirtualDPadLean).
//   • Talk button appears   — visibility from PostureSignals.TalkAvailable
//     (Core static; the stale one-shot reflection push is retired).
//   • raid-"x"/harvest-"Y"  — not rebuilt (earns-its-place: no verified
//     backing feature surface); Pi sign-in stays off the HUD (Title-gated).
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.HUD;
using DeNelle.Core.HudModel;
using DeNelle.Core.UI;

namespace DeNelle.HUD.Kit
{
    /// <summary>Builds, binds and posture-occupies every kit widget (see header).</summary>
    public sealed class HudKitController : MonoBehaviour
    {
        private HudAreasHost _host;
        private PostureEvaluator _evaluator;
        private HudAreasConfig _config;
        private VillageHudController _owner;   // command events (Village bridges subscribe them)
        private IHudModel _models;

        // widget id -> root (registry the occupancy rows drive).
        private readonly Dictionary<string, GameObject> _widgets =
            new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

        // live handles
        private ElarionUiKit.NameplateHandle _vitals;
        private ElarionUiKit.BarHandle _xpBar;
        private ElarionUiKit.CurrencyChipHandle _wisdomChip;
        private ElarionUiKit.BarHandle _heartBar;
        private ElarionUiKit.TargetFrameHandle _targetFrame;
        private ElarionUiKit.CastBarHandle _castBar;
        private ElarionUiKit.ActionSlotHandle[] _abilitySlots;
        private ElarionUiKit.ActionSlotHandle _potionSlot;
        private ElarionUiKit.ActionSlotHandle _attackSlot;
        private ElarionUiKit.CurrencyChipHandle[] _resChips;      // expanded row
        private ElarionUiKit.CurrencyChipHandle _resGoldOnly;     // collapsed variant
        private GameObject _resExpandedRow;
        private Button _talkButton, _fleeButton, _startWaveButton;
        private TMP_Text _fleeLabel;
        private TMP_Text _waveLabel, _waveCountdown;
        private ElarionUiKit.BarHandle _waveProgress;
        private GameObject _waveBlockRoot;
        private ElarionUiKit.NameplateHandle[] _cycleRows;
        private string[] _cycleIds;
        private ElarionUiKit.ChatDockHandle _chatDock;

        // model subscriptions (for teardown)
        private readonly List<Action> _unsubscribe = new List<Action>();

        private bool _startWaveAvailable;
        private float _chipsExpandUntil;   // collapsed chips: tap-expand window

        /// <summary>Build the whole kit under a fresh HudAreasHost.</summary>
        public static HudKitController Create(VillageHudController owner)
        {
            var host = HudAreasHost.Create(owner != null ? owner.transform : null);
            var kit = host.gameObject.AddComponent<HudKitController>();
            kit._owner = owner;
            kit._host = host;
            kit._evaluator = host.gameObject.AddComponent<PostureEvaluator>();
            kit._config = HudAreasConfig.Load();
            kit._models = CoreServices.HudModel;
            kit.BuildWidgets();
            kit.BindModels();
            kit._evaluator.PostureChanged += kit.ApplyPosture;
            kit.ApplyPosture(kit._evaluator.Posture);
            FlowTrace.Step("HudKit", "kit assembled: " + kit._widgets.Count + " widgets, posture " +
                           HudPostureKeys.Key(kit._evaluator.Posture));
            return kit;
        }

        /// <summary>The whole-HUD visibility seam (VillageHudController.SetHudVisible adapter).</summary>
        public void SetHudVisible(bool visible)
        {
            if (_host == null || _host.Group == null) return;
            _host.Group.alpha = visible ? 1f : 0f;
            _host.Group.interactable = visible;
            _host.Group.blocksRaycasts = visible;
        }

        /// <summary>Start-Wave availability push (StartWaveHudBridge -> VillageHudController adapter).</summary>
        public void SetStartWaveAvailable(bool available)
        {
            _startWaveAvailable = available;
            if (_startWaveButton != null) _startWaveButton.gameObject.SetActive(available);
        }

        /// <summary>Repair-prompt push adapter — the shared toast PLUS a factory Repair
        /// button firing RepairConfirmRequested (the WallRepairHudBridge contract; the
        /// old prompt's Cancel is the toast simply expiring).</summary>
        public void ShowRepairToast(string wallLabel, float damagePercent)
        {
            var card = ShowToast(ElarionUiKit.ToastTone.Danger,
                (string.IsNullOrEmpty(wallLabel) ? "A wall" : wallLabel) +
                " is damaged (" + Mathf.RoundToInt(damagePercent) + "%)", lifetime: 6f);
            if (card == null) return;
            ElarionUiKit.BuildObsidianButton(card.transform, "Repair",
                ElarionUiKit.ObsidianButtonStyle.Style2, ElarionUiKit.ObsidianButtonColor.Green,
                new Vector2(0.70f, 0.12f), new Vector2(0.97f, 0.88f), () =>
                {
                    if (_owner != null) _owner.RepairConfirmRequested?.Invoke();
                    Destroy(card);
                });
        }

        /// <summary>Wave-clear push adapter — routes the old no-op banner through the shared toast.</summary>
        public void ShowWaveClearToast(int waveNumber, int enemiesDefeated, string flavourLine)
        {
            string line = "Wave " + waveNumber + " cleared! " +
                          (enemiesDefeated > 0 ? enemiesDefeated + " foes defeated. " : "") +
                          (flavourLine ?? "");
            ShowToast(ElarionUiKit.ToastTone.Confirm, line.TrimEnd());
        }

        private GameObject ShowToast(ElarionUiKit.ToastTone tone, string text, float lifetime = 3.5f)
        {
            var mount = _host != null ? _host.Mount(HudArea.Feedback) : null;
            if (mount == null) return null;
            var parts = ElarionUiKit.ToastCard(mount, tone, accentLeft: true, align: TextAnchor.MiddleCenter);
            var rt = (RectTransform)parts.card.transform;
            rt.anchorMin = new Vector2(0.28f, 0.90f);
            rt.anchorMax = new Vector2(0.72f, 0.97f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            parts.label.text = text ?? "";
            Destroy(parts.card, lifetime);
            FlowTrace.Step("HudKit", "toast: " + text);
            return parts.card;
        }

        // =====================================================================
        // WIDGET CONSTRUCTION — factory-only (§5).
        // =====================================================================

        private void BuildWidgets()
        {
            Transform pool = transform;   // widgets are reparented into areas on ApplyPosture
            Vector2 z = Vector2.zero, o = Vector2.one;

            // ── vitals: BuildNameplate(Player) — THE 9/145 fix by contract ──
            _vitals = ElarionUiKit.BuildNameplate(pool, ElarionUiKit.NameplateKind.Player,
                new Vector2(0f, 0.35f), new Vector2(1f, 1f));
            _vitals.SetName("Hero");
            Register("playerNameplate", WrapAsWidget("playerNameplate", _vitals.root));

            // xp bar under the plate (thin, no frame).
            _xpBar = ElarionUiKit.BuildObsidianBar(pool, ElarionUiKit.ObsidianBarKind.Xp,
                new Vector2(0.02f, 0.18f), new Vector2(0.86f, 0.30f), withValue: false, framed: true);
            Register("xpBar", WrapAsWidget("xpBar", _xpBar.track.gameObject));

            _wisdomChip = ElarionUiKit.CurrencyChip(pool, ElarionUiKit.CurrencyKind.Wisdom,
                new Vector2(0.02f, 0.00f), new Vector2(0.34f, 0.16f));
            Register("wisdomChip", WrapAsWidget("wisdomChip", _wisdomChip.root));

            // ── status: wave block (calm(town), between waves only) + heart ──
            BuildWaveBlock(pool);
            _heartBar = ElarionUiKit.BuildObsidianBar(pool, ElarionUiKit.ObsidianBarKind.Heart,
                new Vector2(0.15f, 0.02f), new Vector2(0.85f, 0.30f), withValue: true, framed: true);
            Register("heartStatus", WrapAsWidget("heartStatus", _heartBar.track.gameObject));

            // targetCycle: up to 4 compact enemy rows -> HudCommands.CycleSelect.
            BuildTargetCycle(pool);

            // ── system: settings + flee ──
            var settings = ElarionUiKit.BuildObsidianButton(pool, "Menu",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.10f, 0.55f), new Vector2(0.98f, 0.98f), () =>
                {
                    if (!PanelRouter.Open(PanelId.GameGuide))
                        FlowTrace.Warn("HudKit", "settings tapped but no GameGuide opener registered");
                });
            Register("settingsButton", WrapAsWidget("settingsButton", settings.gameObject));

            // Two-tap arm/confirm (anti-misfire, carried from the retired BattleArenaHud
            // flee button): first tap arms for 2s ("Flee?"), second tap inside the window
            // actually flees; the window expiring disarms back to "Flee".
            _fleeButton = ElarionUiKit.BuildObsidianButton(pool, "Flee",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Red,
                new Vector2(0.10f, 0.05f), new Vector2(0.98f, 0.48f), OnFleeTapped);
            _fleeLabel = _fleeButton.GetComponentInChildren<TMP_Text>();
            Register("fleeButton", WrapAsWidget("fleeButton", _fleeButton.gameObject));

            // ── targetInfo: target frame + cast telegraph ──
            _targetFrame = ElarionUiKit.BuildTargetFrame(pool, new Vector2(0f, 0.35f), new Vector2(1f, 1f));
            Register("targetFrame", WrapAsWidget("targetFrame", _targetFrame.root));

            _castBar = ElarionUiKit.BuildCastBar(pool, 1, new Vector2(0.08f, 0.02f), new Vector2(0.92f, 0.30f));
            Register("castBar", WrapAsWidget("castBar", _castBar.root));

            // ── actionBar: 4 ability slots + potion ──
            BuildAbilityRow(pool);
            _potionSlot = ElarionUiKit.BuildActionSlot(pool,
                new Vector2(0.84f, 0.10f), new Vector2(0.99f, 0.95f), HudCommands.Potion);
            var potionIcon = UiStyle.Icon("potion", "consumable", "heal");
            if (potionIcon != null) _potionSlot.SetIcon(potionIcon);
            Register("potionSlot", WrapAsWidget("potionSlot", _potionSlot.root));

            // ── actionRail: the big basic-attack slot ──
            _attackSlot = ElarionUiKit.BuildActionSlot(pool,
                new Vector2(0.22f, 0.02f), new Vector2(0.98f, 0.44f), HudCommands.Attack);
            var atkIcon = UiStyle.Icon("attack", "sword", "melee");
            if (atkIcon != null) _attackSlot.SetIcon(atkIcon);
            Register("attackButton", WrapAsWidget("attackButton", _attackSlot.root));

            // resource chips (expanded row) + collapsed gold-only variant (tap-expand).
            BuildResourceChips(pool);

            // ── town action buttons ──
            var build = ElarionUiKit.BuildObsidianButton(pool, "Build",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Yellow,
                new Vector2(0.00f, 0.10f), new Vector2(0.24f, 0.95f),
                () => { if (_owner != null) _owner.BuildRequested?.Invoke(); });
            // Carry-over (WO-T2 working-tree intent): the tutorial spotlight target.
            TutorialHighlightRegistry.Register("hud.build_button", (RectTransform)build.transform);
            Register("buildButton", WrapAsWidget("buildButton", build.gameObject));

            _talkButton = ElarionUiKit.BuildObsidianButton(pool, "Talk",
                ElarionUiKit.ObsidianButtonStyle.Style2, ElarionUiKit.ObsidianButtonColor.Green,
                new Vector2(0.26f, 0.10f), new Vector2(0.50f, 0.95f), () =>
                {
                    FlowTrace.Step("HudKit", "Talk tapped -> HudCommands.Talk + TalkRequested");
                    HudCommands.Talk();
                    if (_owner != null) _owner.TalkRequested?.Invoke();   // legacy bridge compat
                });
            Register("talkButton", WrapAsWidget("talkButton", _talkButton.gameObject));

            var bag = ElarionUiKit.BuildObsidianButton(pool, "Bag",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.52f, 0.10f), new Vector2(0.74f, 0.95f), () =>
                {
                    if (_owner != null) _owner.InventoryRequested?.Invoke();
                    VillageHudController.RaiseInventoryRequested();
                });
            Register("bagButton", WrapAsWidget("bagButton", bag.gameObject));

            var quest = ElarionUiKit.BuildObsidianButton(pool, "Quests",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.76f, 0.10f), new Vector2(1.00f, 0.95f), OnContextAction);
            Register("questButton", WrapAsWidget("questButton", quest.gameObject));

            // ── moveCluster: THE FOUR ROUND BUTTONS (§1.11) -> HudMoveInput ──
            var cluster = ElarionUiKit.BuildControllerCluster(pool, new Vector2(0.5f, 0.5f), HudMoveInput.Set);
            Register("moveCluster", WrapAsWidget("moveCluster", cluster.root));

            // ── dock: chat/ranks/music (hidden entirely in build mode via the rows) ──
            _chatDock = ElarionUiKit.BuildChatDock(pool, z, o, OpenClanChat, OpenLeaderboard, OpenJukebox);
            _chatDock.SetExpanded(false);
            Register("chatDock", WrapAsWidget("chatDock", _chatDock.root));

            // ── feedback: the CombatTextLayer marker (its own capped/pooled canvas) ──
            var fb = new GameObject("FeedbackLayerMarker", typeof(RectTransform));
            fb.transform.SetParent(pool, false);
            if (Application.isPlaying) { var _ = CombatTextLayer.Instance; }   // ensure the layer exists
            Register("feedbackLayer", fb);
        }

        // A stretch wrapper so every widget occupies its area mount uniformly.
        private GameObject WrapAsWidget(string id, GameObject content)
        {
            var wrap = new GameObject("Widget_" + id, typeof(RectTransform));
            wrap.transform.SetParent(transform, false);
            var rt = (RectTransform)wrap.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            content.transform.SetParent(wrap.transform, false);
            return wrap;
        }

        private void Register(string id, GameObject root)
        {
            _widgets[id] = root;
            root.SetActive(false);   // occupancy rows switch widgets on
        }

        private void BuildWaveBlock(Transform pool)
        {
            // Plate + labels + progress + Start Wave (all factory pieces).
            _waveBlockRoot = ElarionUiKit.Panel(pool, Vector2.zero, Vector2.one);
            _waveLabel = ElarionUiKit.Label(_waveBlockRoot.transform, "", 0.62f, 0.98f,
                ElarionUi.Parchment, ElarionUi.FontHead, TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);
            _waveCountdown = ElarionUiKit.Label(_waveBlockRoot.transform, "", 0.34f, 0.60f,
                ElarionUi.Gilt, ElarionUi.FontLabel, TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);
            _waveProgress = ElarionUiKit.BuildObsidianBar(_waveBlockRoot.transform,
                ElarionUiKit.ObsidianBarKind.Stat, new Vector2(0.08f, 0.20f), new Vector2(0.92f, 0.32f),
                withValue: false, framed: false);
            _startWaveButton = ElarionUiKit.BuildObsidianButton(_waveBlockRoot.transform, "Start Wave",
                ElarionUiKit.ObsidianButtonStyle.Style2, ElarionUiKit.ObsidianButtonColor.Green,
                new Vector2(0.22f, 0.01f), new Vector2(0.78f, 0.18f),
                () => { if (_owner != null) _owner.StartWaveRequested?.Invoke(); });
            // Carry-over (WO-T2 working-tree intent): the tutorial spotlight target.
            TutorialHighlightRegistry.Register("hud.wave_button", (RectTransform)_startWaveButton.transform);
            _startWaveButton.gameObject.SetActive(false);
            Register("waveBlock", WrapAsWidget("waveBlock", _waveBlockRoot));
        }

        private void BuildAbilityRow(Transform pool)
        {
            var row = new GameObject("AbilityRow", typeof(RectTransform));
            row.transform.SetParent(pool, false);
            var rrt = (RectTransform)row.transform;
            rrt.anchorMin = Vector2.zero; rrt.anchorMax = new Vector2(0.82f, 1f);
            rrt.offsetMin = Vector2.zero; rrt.offsetMax = Vector2.zero;

            _abilitySlots = new ElarionUiKit.ActionSlotHandle[4];
            for (int i = 0; i < 4; i++)
            {
                int slot = i;
                float x0 = i * 0.25f + 0.01f, x1 = (i + 1) * 0.25f - 0.01f;
                _abilitySlots[i] = ElarionUiKit.BuildActionSlot(row.transform,
                    new Vector2(x0, 0.05f), new Vector2(x1, 0.95f),
                    () => { if (_owner != null) _owner.AbilityRequested?.Invoke(slot); });
            }
            Register("abilityRow", WrapAsWidget("abilityRow", row));
        }

        private void BuildTargetCycle(Transform pool)
        {
            var col = new GameObject("TargetCycle", typeof(RectTransform));
            col.transform.SetParent(pool, false);
            var crt = (RectTransform)col.transform;
            crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;

            _cycleRows = new ElarionUiKit.NameplateHandle[4];
            _cycleIds = new string[4];
            for (int i = 0; i < 4; i++)
            {
                float x0 = i * 0.25f + 0.005f, x1 = (i + 1) * 0.25f - 0.005f;
                _cycleRows[i] = ElarionUiKit.BuildNameplate(col.transform, ElarionUiKit.NameplateKind.Enemy,
                    new Vector2(x0, 0.05f), new Vector2(x1, 0.95f));
                int idx = i;
                // Tap-to-select: a Button over the compact plate, firing the Core command.
                // (§5 note, reported: the factory nameplate ships raycast-off with no tap
                // helper — a BuildNameplate onTap / TapTarget kit ask is filed to P1; this
                // AddComponent<Button> is the sanctioned interim, no visuals constructed.)
                var btn = _cycleRows[i].root.GetComponent<Button>();
                if (btn == null) btn = _cycleRows[i].root.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                if (_cycleRows[i].plate != null) _cycleRows[i].plate.raycastTarget = true;
                btn.onClick.AddListener(() =>
                {
                    if (!string.IsNullOrEmpty(_cycleIds[idx])) HudCommands.CycleSelect(_cycleIds[idx]);
                });
                _cycleRows[i].root.SetActive(false);
            }
            Register("targetCycle", WrapAsWidget("targetCycle", col));
        }

        private void BuildResourceChips(Transform pool)
        {
            // Expanded row: Gold (primary) + Wood/Iron/Food/Crystal — count-tween only, NO flash.
            _resExpandedRow = new GameObject("ResourceChips", typeof(RectTransform));
            _resExpandedRow.transform.SetParent(pool, false);
            var rrt = (RectTransform)_resExpandedRow.transform;
            rrt.anchorMin = new Vector2(0f, 0.78f); rrt.anchorMax = Vector2.one;
            rrt.offsetMin = Vector2.zero; rrt.offsetMax = Vector2.zero;

            var kinds = new[]
            {
                ElarionUiKit.CurrencyKind.Gold, ElarionUiKit.CurrencyKind.Wood,
                ElarionUiKit.CurrencyKind.Iron, ElarionUiKit.CurrencyKind.Food,
                ElarionUiKit.CurrencyKind.Crystal,
            };
            _resChips = new ElarionUiKit.CurrencyChipHandle[kinds.Length];
            for (int i = 0; i < kinds.Length; i++)
            {
                float y1 = 1f - i * 0.19f, y0 = y1 - 0.18f;   // vertical stack down the rail
                _resChips[i] = ElarionUiKit.CurrencyChip(_resExpandedRow.transform, kinds[i],
                    new Vector2(0.05f, y0), new Vector2(1f, y1), primary: kinds[i] == ElarionUiKit.CurrencyKind.Gold);
            }
            // Tap anywhere on the row toggles nothing in town (always expanded there).
            Register("resourceChips", WrapAsWidget("resourceChips", _resExpandedRow));

            // Collapsed variant (calm(explore)): gold chip only; TAP expands the row for 6s.
            _resGoldOnly = ElarionUiKit.CurrencyChip(pool, ElarionUiKit.CurrencyKind.Gold,
                new Vector2(0.05f, 0.82f), new Vector2(1f, 1f), primary: true);
            var tapGo = _resGoldOnly.root;
            var tapBtn = tapGo.AddComponent<Button>();
            tapBtn.transition = Selectable.Transition.None;
            tapBtn.onClick.AddListener(() =>
            {
                _chipsExpandUntil = Time.unscaledTime + 6f;
                FlowTrace.Step("HudKit", "resource chips tap-expanded (6s window)");
            });
            _resGoldOnly.plate.raycastTarget = true;   // the chip is the tap target here
            Register("resourceChipsCollapsed", WrapAsWidget("resourceChipsCollapsed", tapGo));
        }

        // =====================================================================
        // MODEL BINDING — VM Changed events only (§1.1 rule 4 / §5 rule 3).
        // =====================================================================

        private void BindModels()
        {
            var m = _models;
            if (m == null)
            {
                // HudModelHost registers after scene load; retry next frame(s).
                FlowTrace.Warn("HudKit", "CoreServices.HudModel not registered yet — binding deferred");
                InvokeRepeating(nameof(TryLateBind), 0.25f, 0.25f);
                return;
            }
            BindAll(m);
        }

        private void TryLateBind()
        {
            var m = CoreServices.HudModel;
            if (m == null) return;
            CancelInvoke(nameof(TryLateBind));
            _models = m;
            BindAll(m);
        }

        private void BindAll(IHudModel m)
        {
            Sub(m.HeroVitals, OnVitals);        OnVitals();
            Sub(m.Economy, OnEconomy);          OnEconomy();
            Sub(m.Wave, OnWave);                OnWave();
            Sub(m.World, OnWorld);              OnWorld();
            Sub(m.Abilities, OnAbilities);      OnAbilities();
            Sub(m.TargetCycle, OnTargetCycle);  OnTargetCycle();
            _targetFrame.Bind(m.Target);
            _castBar.Bind(m.Cast);
            PostureSignals.TalkChanged += OnTalkChanged; _unsubscribe.Add(() => PostureSignals.TalkChanged -= OnTalkChanged);
            OnTalkChanged();
            FlowTrace.Step("HudKit", "models bound (vitals/economy/wave/world/abilities/cycle/target/cast)");
        }

        private void Sub(HeroVitalsModel m, Action h)    { m.Changed += h; _unsubscribe.Add(() => m.Changed -= h); }
        private void Sub(EconomyModel m, Action h)       { m.Changed += h; _unsubscribe.Add(() => m.Changed -= h); }
        private void Sub(WaveModel m, Action h)          { m.Changed += h; _unsubscribe.Add(() => m.Changed -= h); }
        private void Sub(WorldMetricsModel m, Action h)  { m.Changed += h; _unsubscribe.Add(() => m.Changed -= h); }
        private void Sub(AbilityLoadoutModel m, Action h){ m.Changed += h; _unsubscribe.Add(() => m.Changed -= h); }
        private void Sub(TargetCycleModel m, Action h)   { m.Changed += h; _unsubscribe.Add(() => m.Changed -= h); }

        private void OnVitals()
        {
            var v = _models != null ? _models.HeroVitals : null;
            if (v == null) return;
            // §1.1: fillAmount-only via BarHandle.SetValue — bar + "9/145" label atomic.
            _vitals.hp.SetValue(v.Hp, v.MaxHp);
            if (_vitals.mp != null) _vitals.mp.SetValue(v.Mana, v.MaxMana);   // MP LIVE (§0 fix)
            _vitals.SetName((string.IsNullOrEmpty(v.ClassId) ? "Hero" : Cap(v.ClassId)) + "  Lv " + Mathf.Max(1, v.Level));
            _xpBar.SetValue(v.Xp, Mathf.Max(1, v.XpToNext));
            _wisdomChip.SetAmount(v.Wisdom);
        }

        private void OnEconomy()
        {
            var e = _models != null ? _models.Economy : null;
            if (e == null || _resChips == null) return;
            // Count-tween only — the no-flash law lives in CurrencyChip.SetAmount.
            _resChips[0].SetAmount(e.Gold);
            _resChips[1].SetAmount(e.Wood);
            _resChips[2].SetAmount(e.Iron);
            _resChips[3].SetAmount(e.Food);
            _resChips[4].SetAmount(e.Crystals);
            _resGoldOnly.SetAmount(e.Gold);
        }

        private void OnWave()
        {
            var w = _models != null ? _models.Wave : null;
            if (w == null || _waveBlockRoot == null) return;

            // WAVE-CHROME LAW (§0): the block lives only in the calm(town) row (occupancy)
            // AND self-gates to BETWEEN-waves phases. Countdown shows ONLY when real.
            bool betweenWaves = w.Phase == WavePhase.Idle || w.Phase == WavePhase.Countdown ||
                                w.Phase == WavePhase.Cleared;
            _waveBlockRoot.SetActive(betweenWaves);
            if (!betweenWaves) return;

            _waveLabel.text = w.Number > 0 ? "Wave " + w.Number : "The village rests";
            bool realCountdown = w.Phase == WavePhase.Countdown && w.CountdownRemaining > 0f;
            _waveCountdown.text = realCountdown
                ? "Next wave in " + Mathf.CeilToInt(w.CountdownRemaining) + "s" : "";
            _waveProgress.SetValue(w.EnemiesTotal - w.EnemiesLive, Mathf.Max(1, w.EnemiesTotal));
            _waveProgress.track.gameObject.SetActive(w.EnemiesTotal > 0);
            if (_startWaveButton != null)
                _startWaveButton.gameObject.SetActive(_startWaveAvailable && w.Phase != WavePhase.Countdown);
        }

        private void OnWorld()
        {
            var wm = _models != null ? _models.World : null;
            if (wm == null) return;
            _heartBar.SetValue(wm.HeartHp, Mathf.Max(1, wm.HeartMaxHp));
        }

        private void OnAbilities()
        {
            var a = _models != null ? _models.Abilities : null;
            if (a == null || _abilitySlots == null) return;
            for (int i = 0; i < _abilitySlots.Length; i++)
            {
                var h = _abilitySlots[i];
                if (i >= a.Slots.Count) { h.root.SetActive(false); continue; }
                var s = a.Slots[i];
                h.root.SetActive(s.Equipped);
                if (!s.Equipped) continue;
                h.SetIcon(string.IsNullOrEmpty(s.IconKey) ? null : UiStyle.Icon(s.IconKey));
                h.SetCooldown(s.CooldownRemaining, s.CooldownTotal);
            }
        }

        private void OnTargetCycle()
        {
            var tc = _models != null ? _models.TargetCycle : null;
            if (tc == null || _cycleRows == null) return;
            for (int i = 0; i < _cycleRows.Length; i++)
            {
                bool has = i < tc.Targets.Count;
                _cycleRows[i].root.SetActive(has);
                _cycleIds[i] = has ? tc.Targets[i].Id : null;
                if (!has) continue;
                var t = tc.Targets[i];
                _cycleRows[i].SetName(t.Name);
                _cycleRows[i].hp.SetValue(t.HpFraction, 1f);
            }
        }

        // TALK §0 FIX: visibility follows the Core signal — never a stale reflection push.
        private void OnTalkChanged()
        {
            if (_talkButton == null) return;
            _talkButton.interactable = PostureSignals.TalkAvailable;
            if (!_talkButton.TryGetComponent(out CanvasGroup cg))
                cg = _talkButton.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = PostureSignals.TalkAvailable ? 1f : 0.45f;
        }

        // Two-tap flee (see BuildWidgets) — arm, confirm-inside-window, or disarm.
        private float _fleeArmedUntil;
        private void OnFleeTapped()
        {
            if (Time.unscaledTime < _fleeArmedUntil)
            {
                _fleeArmedUntil = 0f;
                if (_fleeLabel != null) _fleeLabel.text = "Flee";
                HudCommands.Flee();
                return;
            }
            _fleeArmedUntil = Time.unscaledTime + 2f;
            if (_fleeLabel != null) _fleeLabel.text = "Flee?";
            FlowTrace.Step("HudKit", "flee armed (tap again within 2s to confirm)");
        }

        // Context action (carried over from the old HUD, Core-only): focused upgradable
        // building -> Upgrade; else the Quest/Rumor board.
        private void OnContextAction()
        {
            string id = HudBuildingFocus.CurrentBuildingId;
            var custom = HudBuildingFocus.CurrentUpgradeAction;
            if (custom != null) { FlowTrace.Step("HudKit", "context action -> custom upgrade"); custom(); }
            else if (!string.IsNullOrEmpty(id)) PanelRouter.Open(PanelId.BuildingUpgrade, id);
            else if (!PanelRouter.Open(PanelId.RumorBoard))
                FlowTrace.Warn("HudKit", "RumorBoard opener not registered — quest board unreachable");
        }

        // ── dock intents (parity with the retired SocialAccessCluster) ──────
        private void OpenClanChat()
        {
            var p = FindAnyObjectByType<ClanChatPanel>(FindObjectsInactive.Include);
            if (p != null) p.Toggle();
            else FlowTrace.Warn("HudKit", "dock: ClanChatPanel not present");
        }

        private void OpenLeaderboard()
        {
            var p = FindAnyObjectByType<LeaderboardPanel>(FindObjectsInactive.Include);
            if (p != null) p.Toggle();
            else FlowTrace.Warn("HudKit", "dock: LeaderboardPanel not present");
        }

        private void OpenJukebox()
        {
            // MusicSelectionPanel lives in DeNelle.Audio — the same loose-reflection toggle
            // the retired SocialAccessCluster used (HUD -> Core only, no Audio asmdef edge).
            Guard.Try("HudKit", "dock jukebox toggle", () =>
            {
                var t = Type.GetType("DeNelle.Audio.MusicSelectionPanel, DeNelle.Audio");
                if (t == null) return false;
                var panel = FindAnyObjectByType(t) as MonoBehaviour;
                if (panel == null) return false;
                var toggle = t.GetMethod("Toggle");
                if (toggle == null) return false;
                toggle.Invoke(panel, null);
                return true;
            }, false);
        }

        // =====================================================================
        // POSTURE OCCUPANCY — data rows drive everything (A4).
        // =====================================================================

        private void ApplyPosture(HudPosture posture)
        {
            var occupancy = _config.Occupancy(posture);
            int shown = 0;
            foreach (var kv in _widgets)
            {
                HudArea area;
                bool present = occupancy.TryGetValue(kv.Key, out area);
                if (present)
                {
                    var mount = _host.Mount(area);
                    if (mount != null && kv.Value.transform.parent != mount)
                        kv.Value.transform.SetParent(mount, false);
                    if (!kv.Value.activeSelf) kv.Value.SetActive(true);
                    shown++;
                }
                else if (kv.Value.activeSelf) kv.Value.SetActive(false);
            }

            // Dynamic gates on top of the rows (availability, never layout):
            if (_widgets.TryGetValue("fleeButton", out var flee) && flee.activeSelf)
                flee.SetActive(HudCommands.HasFlee);
            if (_widgets.TryGetValue("potionSlot", out var pot) && pot.activeSelf)
                pot.SetActive(HudCommands.HasPotion);
            OnTalkChanged();
            OnWave();   // wave block phase gate re-evaluates with the posture

            FlowTrace.Step("HudKit", "occupancy applied: posture " + HudPostureKeys.Key(posture) +
                           " -> " + shown + " widgets live");
        }

        private void Update()
        {
            // Cheap availability polls (no model event exists for these Core statics).
            if (_widgets.TryGetValue("fleeButton", out var flee))
            {
                bool want = HudCommands.HasFlee &&
                            _config.Occupancy(_evaluator.Posture).ContainsKey("fleeButton");
                if (flee.activeSelf != want) flee.SetActive(want);
            }
            if (_widgets.TryGetValue("potionSlot", out var pot))
            {
                bool want = HudCommands.HasPotion &&
                            _config.Occupancy(_evaluator.Posture).ContainsKey("potionSlot");
                if (pot.activeSelf != want) pot.SetActive(want);
            }
            // Collapsed chips: the tap-expand window temporarily shows the full row.
            if (_widgets.TryGetValue("resourceChipsCollapsed", out var col) && col.activeSelf &&
                _widgets.TryGetValue("resourceChips", out var row))
            {
                bool expand = Time.unscaledTime < _chipsExpandUntil;
                if (row.activeSelf != expand)
                {
                    if (expand)
                    {
                        var mount = _host.Mount(HudArea.ActionRail);
                        if (mount != null && row.transform.parent != mount) row.transform.SetParent(mount, false);
                    }
                    row.SetActive(expand);
                }
            }
        }

        private static string Cap(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        private void OnDestroy()
        {
            foreach (var u in _unsubscribe) { try { u(); } catch { /* teardown */ } }
            _unsubscribe.Clear();
            if (_targetFrame != null) _targetFrame.Unbind();
            if (_castBar != null) _castBar.Unbind();
            if (_evaluator != null) _evaluator.PostureChanged -= ApplyPosture;
            HudMoveInput.Set(Vector2.zero);
        }
    }
}
