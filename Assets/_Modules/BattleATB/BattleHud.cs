// =============================================================================
// BattleHud — code-built, data-bound Final-Fantasy party battle HUD (WO-169 P1)
// -----------------------------------------------------------------------------
// Replaces the old fixed 2-card render. This is a DYNAMIC COLLECTION VIEW over
// the live ATBRuntimeState snapshot:
//
//   • LAYOUT (locked FF): ENEMIES on the LEFT column, the PARTY (heroes) on the
//     RIGHT column, facing off. One reusable combatant card per BattleUnit, keyed
//     by unit Id, instantiated on demand and re-bound on every OnBattleChanged —
//     pets + enemies 2..N are now visible (the old HUD only ever showed unit 1).
//   • Per-unit HP + ATB gauges; the active unit's card is highlighted.
//   • COMMAND MENU on the active PLAYER-mode member's turn: Attack / Skills /
//     Item / Defend. AI-mode members resolve without prompting (engine drives).
//   • Skills opens the active member's SPELL LIST (engine HERO_ABILITIES /
//     pet kit for that unit, cost shown, cost/cooldown-gated). Item opens the
//     item list. Both were previously empty / hard-coded.
//   • TARGETING by the chosen ability/item's TargetMode:
//       Single*  → the player PICKS ONE target (cursor highlight on the cards).
//       All*     → AUTO-targets the whole valid set (no pick forced).
//       Self     → the active member.
//
// UI is built entirely in code (no UXML — BattleHUD.uxml does not clone in
// builds, CLAUDE.md §8). The HUD is a passive view: it never touches the engine
// directly — it calls back into the controller, which routes through
// ATBRuntimeState. State/view separation is preserved.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.BattleATB.Engine;
using DeNelle.BattleATB.State;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.BattleATB
{
    /// <summary>
    /// Builds + binds the dynamic, code-built ATB battle HUD. Owns no combat
    /// logic; raises <see cref="OnAction"/> when the player commits a move and
    /// <see cref="OnControlModeToggled"/> when they flip a member Command/Auto.
    /// </summary>
    public sealed class BattleHud
    {
        // ── Callbacks the controller wires to ATBRuntimeState ────────────────

        /// <summary>Raised with a fully-specified player action to submit.</summary>
        public Action<BattleAction> OnAction;

        /// <summary>Raised when the player toggles a member's control mode in the
        /// in-battle party panel. (memberId, newMode).</summary>
        public Action<string, ControlMode> OnControlModeToggled;

        // ── Palette ──────────────────────────────────────────────────────────

        private static readonly Color Gold = new Color(0.97f, 0.85f, 0.45f, 1f);
        private static readonly Color Parchment = new Color(0.97f, 0.92f, 0.74f, 1f);
        private static readonly Color PartyBg = new Color(0.18f, 0.16f, 0.30f, 0.94f);
        private static readonly Color EnemyBg = new Color(0.30f, 0.13f, 0.14f, 0.94f);
        private static readonly Color HpFill = new Color(0.30f, 0.82f, 0.42f);
        private static readonly Color MpFill = new Color(0.40f, 0.62f, 1f);
        private static readonly Color AtbFill = new Color(0.62f, 0.55f, 0.92f);
        private static readonly Color AtbReady = new Color(0.98f, 0.78f, 0.28f);
        private static readonly Color Panel = new Color(0.06f, 0.05f, 0.10f, 0.92f);

        // ── Roots ────────────────────────────────────────────────────────────

        private VisualElement _root;
        private Label _statusBanner;
        private VisualElement _enemyColumn;   // LEFT
        private VisualElement _partyColumn;    // RIGHT
        private ScrollView _battleLog;
        private VisualElement _battleLogContent;
        private VisualElement _commandBar;     // Attack/Skills/Item/Defend
        private VisualElement _overlay;        // skill list / item list / picker prompt
        private VisualElement _vfxLayer;       // WO-170 retro VFX (numbers / flashes), above cards, never blocks input

        // ── Card pool, keyed by unit Id (the dynamic binding) ────────────────

        private sealed class Card
        {
            public VisualElement Box;
            public VisualElement Portrait;   // hero character icon (WO-213); hidden for non-heroes
            public Label Name;
            public VisualElement HpFill;
            public VisualElement MpRow;
            public VisualElement MpFill;
            public VisualElement AtbFill;
            public VisualElement TargetCursor;
            public Button PickButton;   // overlaid during target-pick mode
            public Action PickHandler;  // current tap handler, so it can be detached
        }

        private readonly Dictionary<string, Card> _cards = new Dictionary<string, Card>();

        // ── Hero portrait cache (WO-213) ─────────────────────────────────────
        // Resources.Load is cheap but we re-bind cards every frame; cache the
        // resolved background per hero slug (and remember misses as null) so we
        // don't hit the loader 60×/sec or re-warn endlessly for a missing asset.
        private static readonly Dictionary<string, StyleBackground?> _portraitCache =
            new Dictionary<string, StyleBackground?>();
        private static readonly HashSet<string> _portraitMissWarned = new HashSet<string>();

        // ── Pending-action state for the two-step (choose then target) flow ──

        private enum PickKind { None, Ability, Item }
        private PickKind _pendingKind = PickKind.None;
        private AbilitySlot _pendingSlot;
        private ItemKind _pendingItem;
        private Side _pickSide;                 // which side is a legal target
        private bool _picking;                  // true while awaiting a target tap

        private int _renderedLogCount;

        // ─────────────────────────────────────────────────────────────────────
        // Build — one-time construction of the static frame
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Build the HUD frame into <paramref name="root"/>. Idempotent
        /// per controller — call once after the UIDocument root is live.</summary>
        public void Build(VisualElement root)
        {
            _root = root;
            root.Clear();
            root.style.flexDirection = FlexDirection.Column;
            root.pickingMode = PickingMode.Ignore;

            // Header — title + status banner.
            var header = new VisualElement();
            header.style.alignItems = Align.Center;
            header.style.marginTop = 10;
            root.Add(header);

            var title = new Label("The Last Stand");
            title.style.color = new StyleColor(Parchment);
            title.style.fontSize = 22;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(title);

            _statusBanner = new Label(string.Empty);
            _statusBanner.style.color = new StyleColor(Color.white);
            _statusBanner.style.fontSize = 15;
            _statusBanner.style.marginTop = 2;
            header.Add(_statusBanner);

            // Battlefield — two facing columns. Enemies LEFT, party RIGHT.
            var field = new VisualElement();
            field.style.flexDirection = FlexDirection.Row;
            field.style.justifyContent = Justify.SpaceBetween;
            field.style.flexGrow = 1;
            field.style.marginLeft = 16; field.style.marginRight = 16;
            field.style.marginTop = 10;
            root.Add(field);

            _enemyColumn = MakeColumn("— Enemies —", Align.FlexStart);
            field.Add(_enemyColumn);

            // Spacer keeps the two sides apart on wide screens.
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            field.Add(spacer);

            _partyColumn = MakeColumn("— Party —", Align.FlexEnd);
            field.Add(_partyColumn);

            // Battle log — a slim strip above the command bar.
            _battleLog = new ScrollView(ScrollViewMode.Vertical);
            _battleLog.style.marginLeft = 16; _battleLog.style.marginRight = 16;
            _battleLog.style.height = 96;
            _battleLog.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.45f));
            _battleLog.style.paddingLeft = 8; _battleLog.style.paddingRight = 8;
            root.Add(_battleLog);
            _battleLogContent = new VisualElement();
            _battleLog.Add(_battleLogContent);

            // Command bar — built per turn (varies with the active member).
            _commandBar = new VisualElement();
            _commandBar.style.flexDirection = FlexDirection.Row;
            _commandBar.style.justifyContent = Justify.Center;
            _commandBar.style.marginLeft = 16; _commandBar.style.marginRight = 16;
            _commandBar.style.marginTop = 8; _commandBar.style.marginBottom = 18;
            _commandBar.style.minHeight = 56;
            root.Add(_commandBar);

            // Overlay — skill list / item list / picker prompt. Hidden by default.
            _overlay = new VisualElement();
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = 0; _overlay.style.right = 0;
            _overlay.style.top = 0; _overlay.style.bottom = 0;
            _overlay.style.display = DisplayStyle.None;
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            _overlay.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.55f));
            root.Add(_overlay);

            // WO-170 — retro VFX layer: a full-screen, input-transparent overlay ABOVE
            // the cards + menus, where the BattleVfx presenter draws floating damage
            // numbers, elemental bursts and screen flashes. PickingMode.Ignore so it
            // never eats a tap meant for a command button or a target card.
            _vfxLayer = new VisualElement();
            _vfxLayer.style.position = Position.Absolute;
            _vfxLayer.style.left = 0; _vfxLayer.style.right = 0;
            _vfxLayer.style.top = 0; _vfxLayer.style.bottom = 0;
            _vfxLayer.pickingMode = PickingMode.Ignore;
            root.Add(_vfxLayer);
        }

        // ─────────────────────────────────────────────────────────────────────
        // WO-170 — presentation seams for the retro VFX layer (BattleVfx)
        // These are READ-ONLY view hooks: they never touch combat state, they only
        // expose where to draw effects and which card belongs to a unit id.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>The HUD root (the UIDocument visual root the HUD was built into).</summary>
        public VisualElement Root => _root;

        /// <summary>The input-transparent overlay layer for floating numbers / bursts /
        /// screen flashes. Null until <see cref="Build"/> has run.</summary>
        public VisualElement VfxLayer => _vfxLayer;

        /// <summary>Locate the live combatant-card box for a unit id, so the VFX layer
        /// can anchor a hit-flash / number / burst over the right combatant. Returns
        /// false if the unit has no card this frame.</summary>
        public bool TryGetCardElement(string unitId, out VisualElement cardBox)
        {
            cardBox = null;
            if (string.IsNullOrEmpty(unitId)) return false;
            if (_cards.TryGetValue(unitId, out Card card) && card != null)
            {
                cardBox = card.Box;
                return cardBox != null;
            }
            return false;
        }

        private static VisualElement MakeColumn(string heading, Align align)
        {
            var col = new VisualElement();
            col.style.width = 280;
            col.style.flexDirection = FlexDirection.Column;
            col.style.alignItems = align;

            var label = new Label(heading);
            label.style.color = new StyleColor(Gold);
            label.style.fontSize = 12;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginBottom = 6;
            col.Add(label);

            return col;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Render — data-bound, called on every OnBattleChanged
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Re-bind the whole HUD from a live snapshot. Instantiates a card per unit
        /// (keyed by Id), updates HP/MP/ATB + active highlight, refreshes the log,
        /// and rebuilds the command bar for the active member.
        /// </summary>
        public void Render(ATBRuntimeState runtime)
        {
            BattleState state = runtime != null ? runtime.Battle : null;
            if (_root == null || state == null) return;

            // Cards include the dead (dimmed) so a fallen ally/foe stays on-screen
            // — iterate ALL units per side in engine order, not just the living.
            BindSide(state, Side.Enemy, _enemyColumn, EnemyBg);
            BindSide(state, Side.Party, _partyColumn, PartyBg);

            RenderLog(state);
            RenderStatus(runtime, state);
            RenderCommandBar(runtime, state);
        }

        private void BindSide(BattleState state, Side side, VisualElement column, Color bg)
        {
            // Track which ids are present this frame so removed units' cards drop.
            var present = new HashSet<string>();

            int order = 1; // child 0 is the heading label
            foreach (BattleUnit u in state.Units)
            {
                if (u.Side != side) continue;
                present.Add(u.Id);

                if (!_cards.TryGetValue(u.Id, out Card card))
                {
                    card = BuildCard(bg);
                    _cards[u.Id] = card;
                }
                // Keep the card under the right column, in engine order. Detach-then-
                // insert so the index is correct and never duplicated (UIElements
                // Insert does not de-dupe an existing child).
                if (card.Box.parent != null) card.Box.RemoveFromHierarchy();
                int idx = Mathf.Clamp(order, 0, column.childCount);
                column.Insert(idx, card.Box);
                order++;

                BindCard(card, u, state);
            }

            // Drop cards for units no longer on this side (shouldn't happen mid-
            // battle, but keeps the pool honest if the roster ever changes).
            var stale = new List<string>();
            foreach (var kv in _cards)
            {
                if (kv.Value.Box.parent == column && !present.Contains(kv.Key))
                    stale.Add(kv.Key);
            }
            foreach (string id in stale)
            {
                _cards[id].Box.RemoveFromHierarchy();
                _cards.Remove(id);
            }
        }

        private Card BuildCard(Color bg)
        {
            var card = new Card();

            var box = new VisualElement();
            box.style.width = 240;
            box.style.marginBottom = 8;
            box.style.paddingTop = 8; box.style.paddingBottom = 8;
            box.style.paddingLeft = 10; box.style.paddingRight = 10;
            box.style.backgroundColor = new StyleColor(bg);
            Round(box, 8);
            card.Box = box;

            // Target cursor — a gold left-edge marker shown during pick mode.
            var cursor = new VisualElement();
            cursor.style.position = Position.Absolute;
            cursor.style.left = -6; cursor.style.top = 0; cursor.style.bottom = 0;
            cursor.style.width = 5;
            cursor.style.backgroundColor = new StyleColor(Gold);
            cursor.style.display = DisplayStyle.None;
            box.Add(cursor);
            card.TargetCursor = cursor;

            // Header row — portrait icon (WO-213) on the left, name on the right.
            // The icon is a square that shows the hero's rendered character portrait
            // (Resources/HeroPortraits/<class>). It stays hidden for enemies/pets and
            // for any hero whose portrait asset is missing — the name "pill" then
            // reads exactly as before, so this is a purely additive enhancement.
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            box.Add(headerRow);

            var portrait = new VisualElement();
            portrait.style.width = 36;
            portrait.style.height = 36;
            portrait.style.marginRight = 8;
            portrait.style.flexShrink = 0;
            portrait.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.35f));
            portrait.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
            portrait.style.display = DisplayStyle.None; // shown only when a portrait loads
            Round(portrait, 6);
            headerRow.Add(portrait);
            card.Portrait = portrait;

            var name = new Label("—");
            name.style.color = new StyleColor(Color.white);
            name.style.fontSize = 14;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.flexShrink = 1;
            name.style.whiteSpace = WhiteSpace.Normal;
            headerRow.Add(name);
            card.Name = name;

            card.HpFill = AddBar(box, "HP", HpFill);

            card.MpRow = new VisualElement();
            box.Add(card.MpRow);
            card.MpFill = AddBar(card.MpRow, "MP", MpFill);

            card.AtbFill = AddBar(box, "ATB", AtbFill);

            // Invisible full-card pick button, enabled only during target-pick mode.
            var pick = new Button();
            pick.style.position = Position.Absolute;
            pick.style.left = 0; pick.style.right = 0; pick.style.top = 0; pick.style.bottom = 0;
            pick.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0));
            pick.style.display = DisplayStyle.None;
            box.Add(pick);
            card.PickButton = pick;

            return card;
        }

        private static void BindCard(Card card, BattleUnit u, BattleState state)
        {
            card.Name.text = u.Name;

            BindPortrait(card, u);

            float hpPct = u.MaxHp > 0 ? Mathf.Clamp01((float)u.Hp / u.MaxHp) : 0f;
            SetWidth(card.HpFill, hpPct);

            bool hasMp = u.MaxResource > 0;
            card.MpRow.style.display = hasMp ? DisplayStyle.Flex : DisplayStyle.None;
            if (hasMp) SetWidth(card.MpFill, Mathf.Clamp01((float)u.Resource / u.MaxResource));

            float atbPct = Mathf.Clamp01((float)(u.Atb / Defs.ATB_FULL));
            SetWidth(card.AtbFill, atbPct);
            bool ready = u.Atb >= Defs.ATB_FULL;
            card.AtbFill.style.backgroundColor = new StyleColor(ready ? AtbReady : AtbFill);

            card.Box.style.opacity = u.Alive ? 1f : 0.35f;

            bool isActive = u.Alive && state.ActiveUnitId == u.Id;
            card.Box.style.borderTopWidth = 2; card.Box.style.borderBottomWidth = 2;
            card.Box.style.borderLeftWidth = 2; card.Box.style.borderRightWidth = 2;
            var border = new StyleColor(isActive ? Gold : new Color(0, 0, 0, 0));
            card.Box.style.borderTopColor = border; card.Box.style.borderBottomColor = border;
            card.Box.style.borderLeftColor = border; card.Box.style.borderRightColor = border;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Hero portrait icon (WO-213) — replace the bare name "pill" with the
        // hero's rendered character portrait when one exists. Heroes only; pets
        // and enemies keep the icon hidden. A missing portrait falls back to the
        // existing name-only pill (icon stays collapsed) — never an error/crash.
        // ─────────────────────────────────────────────────────────────────────

        private static void BindPortrait(Card card, BattleUnit u)
        {
            if (card == null || card.Portrait == null) return;

            // Only heroes have rendered portraits. Pets/enemies → no icon.
            if (u == null || u.Kind != UnitKind.Hero || u.HeroClass == null)
            {
                card.Portrait.style.display = DisplayStyle.None;
                return;
            }

            string slug = u.HeroClass.Value.ToString().ToLowerInvariant(); // mage/knight/ranger
            StyleBackground? bg = ResolveHeroPortrait(slug);
            if (bg.HasValue)
            {
                card.Portrait.style.backgroundImage = bg.Value;
                card.Portrait.style.display = DisplayStyle.Flex;
            }
            else
            {
                // Graceful fallback: collapse the icon, keep the name pill as-is.
                card.Portrait.style.display = DisplayStyle.None;
            }
        }

        /// <summary>Load (and cache) the hero portrait background for a class slug.
        /// Mirrors HeroSelectController: try Sprite first, then Texture2D, since the
        /// committed PNGs may be imported either way. Returns null (cached) and warns
        /// once if the asset is absent so the pill fallback engages quietly.</summary>
        private static StyleBackground? ResolveHeroPortrait(string slug)
        {
            if (string.IsNullOrEmpty(slug)) return null;
            if (_portraitCache.TryGetValue(slug, out StyleBackground? cached)) return cached;

            StyleBackground? result = null;
            var sprite = Resources.Load<Sprite>($"HeroPortraits/{slug}");
            if (sprite != null)
            {
                result = new StyleBackground(sprite);
            }
            else
            {
                var tex = Resources.Load<Texture2D>($"HeroPortraits/{slug}");
                if (tex != null)
                    result = new StyleBackground(tex);
            }

            if (result == null && !_portraitMissWarned.Contains(slug))
            {
                _portraitMissWarned.Add(slug);
                Debug.LogWarning($"[BattleHud] Hero portrait 'Resources/HeroPortraits/{slug}' " +
                                 "not found — falling back to the name pill for this hero. " +
                                 "Run Defenders/Heroes/Render Hero Portraits to generate it.");
            }

            _portraitCache[slug] = result;
            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Command bar — Attack / Skills / Item / Defend for the active member
        // ─────────────────────────────────────────────────────────────────────

        private void RenderCommandBar(ATBRuntimeState runtime, BattleState state)
        {
            _commandBar.Clear();

            BattleUnit active = runtime.ActiveUnit();
            bool playerTurn = state.Phase == BattlePhase.AwaitingInput
                              && !runtime.Resolving
                              && active != null
                              && Turn.IsPlayerControlled(active)
                              && runtime.Enemies().Count > 0;

            if (_picking) return; // the picker prompt owns the bar while choosing

            if (!playerTurn)
            {
                var wait = new Label(state.Phase == BattlePhase.Ended ? string.Empty : "…");
                wait.style.color = new StyleColor(new Color(1, 1, 1, 0.6f));
                wait.style.fontSize = 16;
                _commandBar.Add(wait);
                return;
            }

            _commandBar.Add(MakeCommand("Attack", new Color(0.74f, 0.22f, 0.22f),
                () => BeginAttack(runtime)));
            _commandBar.Add(MakeCommand("Skills", new Color(0.24f, 0.40f, 0.74f),
                () => OpenSkillList(runtime, active)));
            _commandBar.Add(MakeCommand("Item", new Color(0.22f, 0.58f, 0.32f),
                () => OpenItemList(runtime, active)));
            _commandBar.Add(MakeCommand("Defend", new Color(0.42f, 0.40f, 0.18f),
                () => Submit(BattleAction.MakeDefend())));
        }

        private void BeginAttack(ATBRuntimeState runtime)
        {
            // Basic attack is single-enemy → let the player pick the foe.
            _pendingKind = PickKind.None; // attack is its own committal below
            BeginTargetPick(runtime, Side.Enemy, targetId =>
                Submit(BattleAction.MakeAttack(targetId)));
        }

        // ─────────────────────────────────────────────────────────────────────
        // Skills — the active member's spell list (engine kit, cost-gated)
        // ─────────────────────────────────────────────────────────────────────

        private void OpenSkillList(ATBRuntimeState runtime, BattleUnit caster)
        {
            // Full kit so the player sees the whole spellbook; un-castable spells
            // (cost/cooldown) are shown disabled with the reason — never an empty menu.
            List<AbilityDef> kit = BattleStateOps.UnitAbilityKit(caster);
            ShowOverlayList("Skills", kit.Count == 0 ? "No abilities." : null, panel =>
            {
                foreach (AbilityDef ab in kit)
                {
                    bool affordable = caster.Resource >= ab.Cost;
                    bool offCooldown = BattleStateOps.CooldownOf(caster, ab.Slot) <= 0;
                    bool usable = affordable && offCooldown;

                    string sub = $"{ab.Cost} MP" + (ab.Damage > 0 ? $" • {ab.Damage} dmg" : "")
                                 + (!offCooldown ? "  (on cooldown)" : (!affordable ? "  (not enough MP)" : ""));
                    AbilityDef captured = ab;
                    panel.Add(MakeListRow($"{ab.Name}", sub, usable, () =>
                    {
                        CloseOverlay();
                        ChooseAbilityTarget(runtime, caster, captured);
                    }));
                }
            });
        }

        /// <summary>Route an ability to targeting by its TargetMode: Single* → pick;
        /// All*/Self → auto-resolve (the engine's ResolveTargets handles the set).</summary>
        private void ChooseAbilityTarget(ATBRuntimeState runtime, BattleUnit caster, AbilityDef ab)
        {
            switch (ab.Target)
            {
                case TargetMode.SingleEnemy:
                    _pendingKind = PickKind.Ability; _pendingSlot = ab.Slot;
                    BeginTargetPick(runtime, Side.Enemy,
                        id => Submit(BattleAction.MakeAbility(ab.Slot, id)));
                    break;
                case TargetMode.SingleAlly:
                    _pendingKind = PickKind.Ability; _pendingSlot = ab.Slot;
                    BeginTargetPick(runtime, Side.Party,
                        id => Submit(BattleAction.MakeAbility(ab.Slot, id)));
                    break;
                case TargetMode.Self:
                    // Self-cast — target is the caster; no pick.
                    Submit(BattleAction.MakeAbility(ab.Slot, caster.Id));
                    break;
                default:
                    // AllEnemies / AllAllies / RandomEnemies → AoE auto-target. The
                    // engine's ResolveTargets selects the whole valid set; we pass no
                    // explicit target id (the engine ignores it for these modes).
                    Submit(BattleAction.MakeAbility(ab.Slot, null));
                    break;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Item — the carried inventory (potion etc.), then ally target
        // ─────────────────────────────────────────────────────────────────────

        private void OpenItemList(ATBRuntimeState runtime, BattleUnit user)
        {
            Dictionary<ItemKind, int> inv = runtime.Battle != null ? runtime.Battle.Inventory : null;
            ShowOverlayList("Items", null, panel =>
            {
                bool any = false;
                if (inv != null)
                {
                    foreach (var kv in inv)
                    {
                        if (kv.Value <= 0) continue;
                        any = true;
                        ItemKind kind = kv.Key;
                        Defs.ITEM_DEFS.TryGetValue(kind, out ItemDef def);
                        string label = def != null ? def.Name : kind.ToString();
                        panel.Add(MakeListRow($"{label} ×{kv.Value}", ItemSub(def), true, () =>
                        {
                            CloseOverlay();
                            // All current items are SingleAlly → pick an ally target.
                            _pendingKind = PickKind.Item; _pendingItem = kind;
                            BeginTargetPick(runtime, Side.Party,
                                id => Submit(BattleAction.MakeItem(kind, id)));
                        }));
                    }
                }
                if (!any)
                    panel.Add(MakeListRow("No items left.", null, false, null));
            });
        }

        private static string ItemSub(ItemDef def)
        {
            if (def == null) return null;
            if (def.Heal != null) return $"Heal {def.Heal}";
            if (def.RestoreResource != null) return $"Restore {def.RestoreResource} MP";
            if (def.Cleanse == true) return "Cleanse statuses";
            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Target picker — highlight legal targets, commit on tap
        // ─────────────────────────────────────────────────────────────────────

        private void BeginTargetPick(ATBRuntimeState runtime, Side side, Action<string> commit)
        {
            _picking = true;
            _pickSide = side;
            CloseOverlay();

            _commandBar.Clear();
            var prompt = new Label(side == Side.Enemy ? "Choose a target." : "Choose an ally.");
            prompt.style.color = new StyleColor(Gold);
            prompt.style.fontSize = 16;
            prompt.style.marginRight = 16;
            _commandBar.Add(prompt);
            var cancel = MakeCommand("Cancel", new Color(0.35f, 0.35f, 0.38f), () =>
            {
                EndTargetPick();
                Render(runtime); // restore the command bar
            });
            cancel.style.flexGrow = 0; cancel.style.width = 110;
            _commandBar.Add(cancel);

            BattleState state = runtime.Battle;
            foreach (var kv in _cards)
            {
                BattleUnit u = BattleStateOps.GetUnit(state, kv.Key);
                bool legal = u != null && u.Alive && u.Side == side;
                Card card = kv.Value;
                card.TargetCursor.style.display = legal ? DisplayStyle.Flex : DisplayStyle.None;
                card.PickButton.style.display = legal ? DisplayStyle.Flex : DisplayStyle.None;

                // Detach any stale handler before attaching the new one (clicked is
                // a real event so -= works; we must NOT null out .clickable).
                if (card.PickHandler != null)
                {
                    card.PickButton.clicked -= card.PickHandler;
                    card.PickHandler = null;
                }
                if (legal)
                {
                    string id = kv.Key;
                    Action handler = () =>
                    {
                        EndTargetPick();
                        commit(id);
                    };
                    card.PickHandler = handler;
                    card.PickButton.clicked += handler;
                }
            }
        }

        private void EndTargetPick()
        {
            _picking = false;
            _pendingKind = PickKind.None;
            foreach (var kv in _cards)
            {
                kv.Value.TargetCursor.style.display = DisplayStyle.None;
                kv.Value.PickButton.style.display = DisplayStyle.None;
                if (kv.Value.PickHandler != null)
                {
                    kv.Value.PickButton.clicked -= kv.Value.PickHandler;
                    kv.Value.PickHandler = null;
                }
            }
        }

        private void Submit(BattleAction action)
        {
            EndTargetPick();
            CloseOverlay();
            OnAction?.Invoke(action);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Overlay list (skill / item menus) + small UI builders
        // ─────────────────────────────────────────────────────────────────────

        private void ShowOverlayList(string heading, string emptyNote, Action<VisualElement> fill)
        {
            _overlay.Clear();
            _overlay.style.display = DisplayStyle.Flex;

            var panel = new VisualElement();
            panel.style.minWidth = 300;
            panel.style.maxWidth = 420;
            panel.style.paddingTop = 12; panel.style.paddingBottom = 12;
            panel.style.paddingLeft = 14; panel.style.paddingRight = 14;
            panel.style.backgroundColor = new StyleColor(Panel);
            Round(panel, 10);
            _overlay.Add(panel);

            var title = new Label(heading);
            title.style.color = new StyleColor(Gold);
            title.style.fontSize = 18;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 8;
            panel.Add(title);

            if (emptyNote != null)
                panel.Add(MakeListRow(emptyNote, null, false, null));
            fill?.Invoke(panel);

            var close = MakeCommand("Back", new Color(0.35f, 0.35f, 0.38f), CloseOverlay);
            close.style.marginTop = 8;
            panel.Add(close);
        }

        private void CloseOverlay()
        {
            if (_overlay == null) return;
            _overlay.Clear();
            _overlay.style.display = DisplayStyle.None;
        }

        private static VisualElement MakeListRow(string title, string sub, bool enabled, Action onClick)
        {
            var btn = new Button { text = string.Empty };
            btn.style.flexDirection = FlexDirection.Column;
            btn.style.alignItems = Align.FlexStart;
            btn.style.marginTop = 4; btn.style.marginBottom = 4;
            btn.style.paddingTop = 8; btn.style.paddingBottom = 8;
            btn.style.paddingLeft = 10; btn.style.paddingRight = 10;
            btn.style.backgroundColor = new StyleColor(enabled
                ? new Color(0.18f, 0.20f, 0.30f, 1f)
                : new Color(0.14f, 0.14f, 0.16f, 1f));
            Round(btn, 6);

            var t = new Label(title);
            t.style.color = new StyleColor(enabled ? Color.white : new Color(1, 1, 1, 0.45f));
            t.style.fontSize = 14;
            t.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.Add(t);

            if (!string.IsNullOrEmpty(sub))
            {
                var s = new Label(sub);
                s.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.85f, enabled ? 1f : 0.4f));
                s.style.fontSize = 11;
                btn.Add(s);
            }

            btn.SetEnabled(enabled);
            if (enabled && onClick != null) btn.clicked += onClick;
            return btn;
        }

        private static Button MakeCommand(string label, Color bg, Action onClick)
        {
            var btn = new Button { text = label };
            var s = btn.style;
            s.flexGrow = 1;
            s.marginLeft = 4; s.marginRight = 4;
            s.height = 50; s.fontSize = 16;
            s.unityFontStyleAndWeight = FontStyle.Bold;
            s.color = new StyleColor(Color.white);
            s.backgroundColor = new StyleColor(bg);
            Round(btn, 8);
            if (onClick != null) btn.clicked += onClick;
            return btn;
        }

        private static VisualElement AddBar(VisualElement parent, string label, Color fillColor)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 4;
            parent.Add(row);

            var lab = new Label(label);
            lab.style.color = new StyleColor(new Color(0.82f, 0.82f, 0.86f, 1f));
            lab.style.fontSize = 9;
            lab.style.width = 24;
            row.Add(lab);

            var track = new VisualElement();
            track.style.flexGrow = 1;
            track.style.height = 10;
            track.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.5f));
            Round(track, 4);
            row.Add(track);

            var fill = new VisualElement();
            fill.style.height = 10;
            fill.style.width = Length.Percent(100f);
            fill.style.backgroundColor = new StyleColor(fillColor);
            Round(fill, 4);
            track.Add(fill);
            return fill;
        }

        private static void SetWidth(VisualElement fill, float pct)
        {
            if (fill != null) fill.style.width = Length.Percent(Mathf.Clamp01(pct) * 100f);
        }

        private static void Round(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Log + status
        // ─────────────────────────────────────────────────────────────────────

        private void RenderLog(BattleState state)
        {
            if (_battleLogContent == null || state.Log == null) return;
            for (int i = _renderedLogCount; i < state.Log.Count; i++)
            {
                BattleLogEntry entry = state.Log[i];
                var line = new Label(entry.Text);
                line.style.color = new StyleColor(LogColor(entry.Event));
                line.style.fontSize = 12;
                line.style.whiteSpace = WhiteSpace.Normal;
                _battleLogContent.Add(line);
            }
            _renderedLogCount = state.Log.Count;
            if (_battleLog != null && _battleLogContent.childCount > 0)
                _battleLog.ScrollTo(_battleLogContent[_battleLogContent.childCount - 1]);
        }

        private static Color LogColor(BattleLogEvent ev)
        {
            switch (ev)
            {
                case BattleLogEvent.BattleStart: return Gold;
                case BattleLogEvent.Victory: return new Color(0.45f, 0.9f, 0.5f);
                case BattleLogEvent.Defeat: return new Color(0.95f, 0.45f, 0.45f);
                case BattleLogEvent.Death: return new Color(0.9f, 0.6f, 0.6f);
                default: return new Color(0.9f, 0.9f, 0.92f);
            }
        }

        private void RenderStatus(ATBRuntimeState runtime, BattleState state)
        {
            if (_statusBanner == null) return;
            switch (state.Phase)
            {
                case BattlePhase.AwaitingInput:
                    BattleUnit actor = runtime.ActiveUnit();
                    _statusBanner.text = actor != null ? $"{actor.Name} — choose an action." : "Choose an action.";
                    break;
                case BattlePhase.Resolving:
                case BattlePhase.Filling:
                    _statusBanner.text = "Resolving…";
                    break;
                case BattlePhase.Ended:
                    _statusBanner.text = state.Outcome == BattleOutcome.Victory
                        ? "Victory — the breach is repelled."
                        : "Defeat — the last stand is lost.";
                    break;
                default:
                    _statusBanner.text = string.Empty;
                    break;
            }
        }

        /// <summary>Reset the append-only log cursor (called when a new battle starts).</summary>
        public void ResetLog() => _renderedLogCount = 0;
    }
}
