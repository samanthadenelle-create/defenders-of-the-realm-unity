// =============================================================================
// BattleHud9Zone - the WO-507 AUTHORITATIVE-MOCKUP battle HUD: nine edge-anchored
// zones hugging the screen corners/edges, the CENTER left clear so the fight shows
// through. Restructured to the owner's 2026-06-24 sketch (supersedes the WO-498
// tic-tac-toe legend; the layout below is the spec).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Arena
//
// SCOPE (owner directive): BONES to the mockup - build the LAYOUT/STRUCTURE exactly,
// wire the systems that already have data, placeholder the not-yet-built systems with
// just the UI bones + a label. Exact colors/sizes are TUNABLE CONSTS the owner dials
// live (every position/size/color below is a clearly-named const = a one-line tweak).
// FLAG-GATED behind FeatureFlags.BattleHud9Zone (default OFF).
//
// HP-B2B law (logic / presentation split): self-contained VIEW that PULLS read-only
// state each frame and fires only the existing public intents on a press:
//   - HeroHealth          (Top-Left HP bar)
//   - HeroTargetIndicator (Top-Center target name + lock state + toggle; target cycle)
//   - HeroLoadout + HeroAbilities + AbilityCatalog (Bottom-Center ability ROW: skill-tree
//                     equipped W/E/R + radial cooldown rings; SLASH anchor = basic attack Q)
//   - Enemy + EnemyBrain.Role (target-cycle rows, Top-Center target)
//   - BattleStarRating + the battle timer (star conditions + stars + time-to-keep-star)
//   - BattleArena.Flee()  (Top-Right FLEE)
//
// THE LAYOUT (render-authoritative, each an edge-anchored RectTransform):
//   Top-Left   : SQUARE hero portrait + name + GREEN HP + BLUE resource + small ability/buff
//                icon row; the TARGET CYCLE list sits directly UNDER it (class-square + name +
//                HP + ">" chevron per enemy)
//   Top-Center : prominent ENEMY TARGET block - name + LEVEL + HP value + HP bar + role/threat;
//                ATK/DEF gated behind ShowEnemyDeepStats; lock state + toggle. Star-conditions
//                + live countdown sit just below it (tunable anchor).
//   Top-Right  : Settings gears + FLEE (Flee lives here now)
//   Mid-Center : EMPTY (the fight shows through)
//   Mid-Right  : FOCUS AREA - heal-toggle + Attack + mode switch (placeholder buttons)
//   Bottom-Left: virtual D-PAD (cross of 4 arrows + center dot)
//   Bottom-Ctr : horizontal ROW of the loadout-equipped ability icons (empty slot = hidden) +
//                Potion / Rapid Heal / Desperate WS (placeholder) + Stars + keep-star timer
//   Bottom-Rgt : 1 big SLASH anchor (basic attack) + a cluster of 3 round utility buttons
//                (target-lock / dash / aim), NOT a fanned arc
//
// Code-built uGUI, Canvas Screen Space - Overlay (UXML does NOT ship - CLAUDE.md S8).
// Dark semi-transparent panels, anchored TIGHT, NO overlap. ASCII logs.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DeNelle.Core;
using DeNelle.Core.UI;
using DeNelle.Core.Combat;
using DeNelle.Core.HudModel;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.Arena
{
    /// <summary>The 9-zone mobile battle HUD to the owner's authoritative mockup (WO-507).</summary>
    public sealed class BattleHud9Zone : MonoBehaviour
    {
        // =====================================================================
        //  TUNABLE CONSTS - the owner dials these live ("tighter / left / bigger").
        //  Every position/size/color a zone uses is named here, one line each.
        // =====================================================================

        // -- palette (dark semi-transparent premium fantasy) -------------------
        private static readonly Color PanelDark = new Color(0.05f, 0.06f, 0.09f, 0.80f);
        private static readonly Color PanelDim  = new Color(0.05f, 0.06f, 0.09f, 0.55f);
        private static readonly Color Gold      = new Color(0.92f, 0.78f, 0.36f, 1f);
        private static readonly Color Parchment = new Color(0.86f, 0.84f, 0.78f, 1f);
        private static readonly Color HpGreen   = new Color(0.30f, 0.78f, 0.34f, 1f);   // mockup: PLAYER HP is GREEN
        private static readonly Color HpRed     = new Color(0.82f, 0.26f, 0.24f, 1f);   // mockup: ENEMY/TARGET HP is RED
        private static readonly Color Bar2Blue  = new Color(0.36f, 0.62f, 0.92f, 1f);   // 2nd bar (resource/level)
        private static readonly Color TrackBg   = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color DeadGrey  = new Color(0.30f, 0.30f, 0.33f, 0.85f);
        private static readonly Color RingTrack = new Color(0f, 0f, 0f, 0.62f);
        private static readonly Color BuffSlot  = new Color(0.16f, 0.18f, 0.22f, 0.85f);
        private static readonly Color LockOn    = new Color(0.85f, 0.30f, 0.26f, 1f);   // Locked = red
        private static readonly Color LockOff   = new Color(0.45f, 0.48f, 0.52f, 1f);   // Unlocked = grey
        // WO-512 slice 2: LOUD lock confirmation - the whole top-center target panel goes a
        // saturated dark-red while LOCKED (vs the neutral PanelDark when free-look), so the
        // lock is unmistakable even when the camera frames the orc and hides the small reticle.
        private static readonly Color PanelLocked = new Color(0.34f, 0.07f, 0.07f, 0.92f);

        // Role colors: Tank gray/blue, Healer green, Wizard purple, DPS red.
        private static readonly Color ColTank   = new Color(0.46f, 0.60f, 0.78f, 1f);
        private static readonly Color ColHealer = new Color(0.40f, 0.80f, 0.45f, 1f);
        private static readonly Color ColWizard = new Color(0.66f, 0.45f, 0.92f, 1f);
        private static readonly Color ColDps    = new Color(0.84f, 0.32f, 0.28f, 1f);

        // GATE: when false, the top-center enemy block shows name/HP/role/level only.
        // When true it ALSO shows ATK/DEF (a future "scout/inspect" skill unlocks this).
        private const bool ShowEnemyDeepStats = false;

        // -- Top-Left: hero portrait + name + HP(green) + resource(blue) + ability/buff icons --
        private static readonly Vector2 TL_Pos      = new Vector2(232f, -58f); // from top-left
        private static readonly Vector2 TL_Size     = new Vector2(420f, 100f);
        private const float TL_PortraitSize = 72f;   // SQUARE hero portrait
        private const int   TL_BuffSlots   = 4;      // small ability/buff icon row
        private const float TL_BuffSize    = 26f;
        private const float TL_BuffGap     = 30f;

        // -- Top-Center: target + lock -----------------------------------------
        // Top-Center = the PROMINENT enemy-target focal block (name + HP value + HP bar
        // + role/threat row). Bigger than a thin label so it reads as the focal element.
        private static readonly Vector2 TC_Pos  = new Vector2(0f, -84f);  // from top-center
        private static readonly Vector2 TC_Size = new Vector2(440f, 150f);

        // Star-conditions + live countdown readout. Anchored UNDER the top-center enemy
        // block by default; the owner can move it (e.g. top-right near settings) by dialing
        // SC_Anchor*/SC_Pos. SC_Anchor is the screen anchor (0.5,1 = top-center).
        private static readonly Vector2 SC_Anchor = new Vector2(0.5f, 1f);  // top-center; (1,1)=top-right
        private static readonly Vector2 SC_Pos    = new Vector2(0f, -150f); // below the enemy block
        private static readonly Vector2 SC_Size   = new Vector2(420f, 40f);

        // -- Top-Right: settings + FLEE ----------------------------------------
        private static readonly Vector2 TR_Pos  = new Vector2(-150f, -48f); // from top-right
        private static readonly Vector2 TR_Size = new Vector2(220f, 64f);

        // -- Mid-Left: TARGET CYCLE / enemy-roster list (owner 2026-06-24: moved here from the
        //    top-left, where it overlapped the player-stats plate). MID-LEFT zone of the 9-grid:
        //    left edge, vertically CENTERED. Anchor + pivot = left-middle (0, 0.5); ML_Pos.x is
        //    the inset from the screen's left edge, ML_Pos.y a vertical nudge about screen center.
        private static readonly Vector2 ML_Pos     = new Vector2(232f, 0f);  // from MID-LEFT (left edge, centered)
        private static readonly Vector2 ML_Size    = new Vector2(420f, 300f);
        private const int   ML_MaxRows = 4;         // Tank/DPS/DPS/Healer family
        private const float ML_RowH    = 56f;
        private const float ML_RowGap  = 4f;

        // -- Mid-Right: focus area (placeholder) -------------------------------
        private static readonly Vector2 MR_Pos  = new Vector2(-18f, 0f); // from mid-right
        private static readonly Vector2 MR_Size = new Vector2(200f, 300f);

        // -- Bottom-Left: D-PAD (cross of 4 arrows + center dot) ---------------
        private static readonly Vector2 BL_PadCenter = new Vector2(150f, 140f); // d-pad center from bottom-left
        private const float BL_PadBtn  = 56f;   // each directional button
        private const float BL_PadGap  = 60f;   // center-to-arrow offset
        private const float BL_PadDot  = 34f;   // center dot

        // -- Bottom-Center: horizontal ROW of the unlocked ability icons (Q/W/E/R) --
        private static readonly Vector2 BC_Pos  = new Vector2(-60f, 70f);  // row centered slightly left of center-bottom
        private const float BC_AbilitySize = 84f;
        private const float BC_AbilityGap  = 96f;
        // Consumables (Potion/Rapid Heal/Desperate WS) + stars/timer tuck to the right of the row.
        private static readonly Vector2 BC_UtilPos = new Vector2(260f, 70f); // from bottom-center
        private const float BC_ConsumeSize = 52f;
        private const float BC_ConsumeGap  = 60f;

        // -- Bottom-Right: 1 big SLASH anchor + a cluster of ~3 round utility buttons --
        private static readonly Vector2 BR_SlashPos = new Vector2(-104f, 104f); // big basic-attack anchor from bottom-right
        private const float BR_SlashSize = 128f;
        private const float BR_UtilSize  = 60f;
        // The 3 small utility discs cluster UP-LEFT of the SLASH anchor (lock / dash / aim).
        private static readonly Vector2[] BR_UtilCluster =
        {
            new Vector2(-196f,  86f),   // target-lock
            new Vector2(-214f, 168f),   // dash
            new Vector2(-150f, 200f),   // aim/move
        };

        // Owner 2026-06-27 (F8 layout correction): the bottom-RIGHT holds the 4 STATIC DEFAULT
        // hero abilities (thrust/parry/heal/charge) — the big one (THRUST = slot Q at BR_SlashPos)
        // with the other three (PARRY/HEAL/CHARGE = W/E/R) fanned in a ROUNDED ARC around it
        // (curving up-and-left, away from the screen corner). These render from the live loadout
        // (class-kit default when a slot is unequipped) so all four are ALWAYS present.
        //   The player-ASSIGNABLE skill-tree EXTRAS live in the bottom-MIDDLE (separate bar +
        //   store; BC_Pos / BC_AbilitySize / BC_AbilityGap). The two bars are distinct.
        private const float BR_SatSize    = 72f;    // the 3 arc satellites (parry/heal/charge)
        private const float BR_ArcRadius  = 135f;   // arc radius around the big thrust button's centre
        // Arc angles (degrees, standard math: 0 = +x/right, 90 = +y/up). Upper-left fan so the
        // satellites curve up-and-left away from the bottom-right corner. Tunable.
        private static readonly float[] BR_ArcAnglesDeg = { 120f, 160f, 200f };

        // -- live system refs (self-resolved; read-only pulls) -----------------
        private Canvas _canvas;
        // WO-541 Stage 3a: context gate. This battle HUD is BATTLE-only; today it relies on
        // spawn lifecycle and can linger into Town/Overworld. We read the live Core
        // HudContextModel (CoreServices.HudModel, registered by HudModelHost AFTER scene load —
        // may be null when this view spawns) and show our canvas ONLY when Context == Battle.
        private IHudModel _hookedModel;
        private HeroHealth _health;
        private HeroAbilities _abilities;
        private HeroTargetIndicator _target;
        private float _battleStart;

        // -- Top-Left ----------------------------------------------------------
        private Image _hpFill;
        private Text  _hpText;
        private Image _bar2Fill;
        private Image _heroPortrait;

        // -- Top-Center (prominent enemy-target focal block) -------------------
        private Text  _targetName;
        private Text  _targetHpValue;   // numeric HP (e.g. "1300")
        private Image _targetHpFill;    // enemy HP bar
        private Text  _targetThreat;    // role + threat row
        private Text  _targetLevel;     // enemy LEVEL
        private Text  _targetDeepStats; // ATK/DEF (gated by ShowEnemyDeepStats)
        private Text  _lockState;
        private Image _lockBtnBg;
        private Image _targetPanelBg;   // WO-512 slice 2: top-center panel bg, tinted red on LOCK

        // -- Star conditions + live countdown readout --------------------------
        private Text _starCondText;

        // -- Mid-Left target-cycle rows ----------------------------------------
        private sealed class CycleRow
        {
            public GameObject Root;
            public Image Portrait;
            public Text  Name;
            public Image HpFill;
            public Enemy Tracked;
        }
        private readonly List<CycleRow> _cycleRows = new List<CycleRow>();
        private float _nextFamilyScan;

        // -- Bottom-Center -----------------------------------------------------
        private Text _starsText;
        private Text _keepTimerText;

        // -- Bottom-Center ability row (skill-tree / loadout driven) -----------
        private sealed class AbilityBtn
        {
            public AbilitySlot Slot;
            public GameObject Root;   // hidden when the slot is empty (W/E/R unequipped)
            public Image Disc;
            public Image Icon;        // the equipped ability's icon (re-bound on loadout change)
            public Image CdRing;
            public Text  CdText;
            public Text  Label;
            public Text  Plus;        // "+" placeholder shown when an assignable W/E/R slot is empty
            public string BoundId;    // the abilityId currently shown (to detect a loadout swap)
        }
        private readonly AbilityBtn[] _abilityBtns = new AbilityBtn[4];

        // -- Bottom-Center ASSIGNABLE EXTRA bar (skill-tree extras, separate store) -----
        // The player fills these from the Skill Tree (HeroLoadout-pattern persistence via
        // AssignableSkillBar). Distinct from the 4 STATIC defaults (bottom-right). Empty =
        // a "+" placeholder; filled = the assigned skill's glyph + name. Data-driven each
        // frame from AssignableSkillBarAccess.Current (PushExtraBar).
        private sealed class ExtraBtn
        {
            public int   Slot;
            public Image Disc;
            public Image Icon;    // DATA-driven concept icon (ConceptIconResolver); transparent when none resolves
            public Text  Glyph;   // assigned ability glyph/initials — fallback when no concept icon resolves
            public Text  Plus;    // "+" when the slot is empty
            public Text  Label;   // ability name under the disc
            public string BoundId;
        }
        private readonly ExtraBtn[] _extraBtns = new ExtraBtn[AssignableSkillBar.SlotCount];

        // Optional external flee handler; defaults to BattleArena.Existing.Flee().
        private System.Action _onFlee;

        // ---------------------------------------------------------------------
        //  Lifecycle
        // ---------------------------------------------------------------------

        /// <summary>
        /// Build the 9-zone overlay (and an EventSystem if none exists) only when the
        /// FeatureFlags.BattleHud9Zone gate is ON. Returns null when OFF so the caller
        /// no-ops cleanly. Mirrors BattleArenaHud.Create.
        /// </summary>
        public static BattleHud9Zone Create()
        {
            if (!FeatureFlags.BattleHud9Zone)
            {
                Debug.Log("[BattleHud9Zone] ff.battlehud9zone OFF - 9-zone HUD not spawned.");
                return null;
            }
            var go = new GameObject("BattleHud9Zone");
            DontDestroyOnLoad(go);
            var hud = go.AddComponent<BattleHud9Zone>();
            hud.Build();
            Debug.Log("[BattleHud9Zone] spawned 9-zone battle HUD (WO-507 mockup).");
            return hud;
        }

        /// <summary>Override the Top-Right FLEE handler. Defaults to BattleArena.Existing.Flee().</summary>
        public void SetFleeHandler(System.Action onFlee) => _onFlee = onFlee;

        public void Close()
        {
            if (this != null && gameObject != null) Destroy(gameObject);
        }

        private void Build()
        {
            EnsureEventSystem();
            _battleStart = Time.time;

            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 5200;  // above BattleArenaHud (5000)
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            BuildTopLeftPlayerStats();
            BuildTargetCycleList();        // MID-LEFT zone (left edge, vertically centered)
            BuildTopCenterTarget();
            BuildTopRightSettingsFlee();
            // Mid-Center is intentionally EMPTY - the fight shows through.
            BuildMidRightFocusArea();
            BuildBottomLeftDPad();
            BuildBottomCenterAbilityRow();    // bottom-RIGHT: 4 STATIC defaults (thrust big + parry/heal/charge arc)
            BuildBottomCenterTemplate();      // bottom-MIDDLE: player-ASSIGNABLE skill-tree extras ("+" until filled)
            // BuildBottomRightSlashCluster();  // RETIRED: the old SLASH was a duplicate Q; the bottom-right is
            //   now the 4 default abilities (big thrust + arc), built in BuildBottomCenterAbilityRow.
        }

        private void Update()
        {
            HookContext();   // WO-541 Stage 3a: subscribe to the Core HUD context once available
            ResolveSystems();
            PushPlayerStats();
            PushTarget();
            PushTargetCycle();
            PushStarConditions();
            PushBottomCenter();
            PushAbilityCooldowns();
            PushExtraBar();
        }

        // WO-541 Stage 3a: CoreServices.HudModel registers AFTER scene load; this HUD may
        // spawn first. Poll until available, then subscribe + apply once. Re-hook if the
        // model instance is ever replaced (host re-spawn). Cheap (one ref compare/frame).
        private void HookContext()
        {
            var hm = CoreServices.HudModel;
            if (hm != null && !ReferenceEquals(hm, _hookedModel))
            {
                if (_hookedModel != null) _hookedModel.Context.Changed -= OnContextChanged;
                _hookedModel = hm;
                _hookedModel.Context.Changed += OnContextChanged;
                ApplyContextGate();   // apply-immediately so a mid-battle spawn gates on frame one
            }
        }

        private void OnContextChanged() => ApplyContextGate();

        // Show this battle HUD ONLY in the Battle context; hide (canvas off, widget alive)
        // in Town/Overworld/Modal so it can no longer linger after a fight. Degrades to the
        // old spawn-lifecycle behaviour (canvas stays on) when the model is unavailable.
        private void ApplyContextGate()
        {
            var hm = CoreServices.HudModel;
            if (hm == null || _canvas == null) return;
            bool show = hm.Context.Context == HudContext.Battle;
            if (_canvas.enabled != show) _canvas.enabled = show;
            FlowTrace.Step("HUD", $"BattleHud9Zone gate context={hm.Context.Context} show={show}");
        }

        private void OnDestroy()
        {
            if (_hookedModel != null)
            {
                _hookedModel.Context.Changed -= OnContextChanged;
                _hookedModel = null;
            }
        }

        private void ResolveSystems()
        {
            if (_health == null) _health = HeroHealth.Instance;
            if (_abilities == null) _abilities = Object.FindFirstObjectByType<HeroAbilities>();
            if (_target == null) _target = Object.FindFirstObjectByType<HeroTargetIndicator>();
        }

        // ---------------------------------------------------------------------
        //  TOP-LEFT - player HP (red) + 2nd bar + BUFFS row (placeholder)
        // ---------------------------------------------------------------------
        private void BuildTopLeftPlayerStats()
        {
            var plate = AddPanel(transform, new Vector2(0f, 1f), new Vector2(0f, 1f), TL_Pos, TL_Size, PanelDark);
            Frame(plate);

            // SQUARE hero portrait (left).
            _heroPortrait = AddImage(plate.transform, new Color(0.12f, 0.12f, 0.15f, 1f));
            Anchor(_heroPortrait.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -14f),
                   new Vector2(TL_PortraitSize, TL_PortraitSize), new Vector2(0f, 1f));
            var pSp = RoleIcon(EnemyRole.Tank);   // "knight" armored portrait stand-in
            if (pSp != null) { _heroPortrait.sprite = pSp; _heroPortrait.color = Color.white; }

            float bx = 14f + TL_PortraitSize + 12f;  // x where the bars/name start (right of portrait)
            var name = AddText(plate.transform, "Knight", 20, Gold, TextAnchor.UpperLeft);
            Anchor(name.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(bx, -8f), new Vector2(-bx - 8f, 24f), new Vector2(0f, 1f));

            // HP bar (GREEN per the render).
            var hpBg = AddImage(plate.transform, TrackBg);
            Anchor(hpBg.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(bx, -38f), new Vector2(-bx - 12f, 16f), new Vector2(0f, 1f));
            _hpFill = AddImage(hpBg.transform, HpGreen);
            FillBarLeft(_hpFill);
            _hpText = AddText(hpBg.transform, "100 / 100", 12, Color.white, TextAnchor.MiddleCenter);
            Stretch(_hpText.rectTransform);

            // Resource bar (BLUE).
            var bar2Bg = AddImage(plate.transform, TrackBg);
            Anchor(bar2Bg.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(bx, -58f), new Vector2(-bx - 12f, 12f), new Vector2(0f, 1f));
            _bar2Fill = AddImage(bar2Bg.transform, Bar2Blue);
            FillBarLeft(_bar2Fill);

            // Small ability/buff icon row under the bars (placeholder slots - new system).
            for (int i = 0; i < TL_BuffSlots; i++)
            {
                var slot = AddImage(plate.transform, BuffSlot);
                var sr = slot.rectTransform;
                sr.anchorMin = new Vector2(0f, 0f); sr.anchorMax = new Vector2(0f, 0f); sr.pivot = new Vector2(0f, 0f);
                sr.sizeDelta = new Vector2(TL_BuffSize, TL_BuffSize);
                sr.anchoredPosition = new Vector2(bx + i * TL_BuffGap, 8f);
                MakeCircle(slot);
            }
        }

        private void PushPlayerStats()
        {
            if (_health != null && _hpFill != null)
            {
                float max = _health.MaxHp <= 0f ? 1f : _health.MaxHp;
                float frac = Mathf.Clamp01(_health.Hp / max);
                _hpFill.fillAmount = frac;
                if (_hpText != null) _hpText.text = Mathf.CeilToInt(Mathf.Max(0f, _health.Hp)) + " / " + Mathf.CeilToInt(max);
            }
            // Resource bar tracks mana when a real pool exists; otherwise a static bone.
            if (_abilities != null && _bar2Fill != null && _abilities.MaxMana > 0f)
                _bar2Fill.fillAmount = Mathf.Clamp01(_abilities.Mana / _abilities.MaxMana);
        }

        // ---------------------------------------------------------------------
        //  TOP-CENTER - the PROMINENT enemy-target focal block (owner refinement):
        //  enemy NAME + numeric HP value + HP bar + role/threat row + lock state/toggle.
        // ---------------------------------------------------------------------
        private void BuildTopCenterTarget()
        {
            var panel = AddPanel(transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), TC_Pos, TC_Size, PanelDark);
            Frame(panel);
            _targetPanelBg = panel;   // WO-512 slice 2: tint red while LOCKED

            var hdr = AddText(panel.transform, "TARGET", 11, Parchment, TextAnchor.UpperLeft);
            Anchor(hdr.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -6f), new Vector2(-12f, 14f), new Vector2(0f, 1f));

            // Enemy NAME (large, the focal line) + LEVEL chip + numeric HP value to its right.
            _targetName = AddText(panel.transform, "No Target", 24, Gold, TextAnchor.UpperLeft);
            Anchor(_targetName.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -22f), new Vector2(-200f, 30f), new Vector2(0f, 1f));
            _targetLevel = AddText(panel.transform, "", 14, Parchment, TextAnchor.UpperRight);
            Anchor(_targetLevel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-58f, -24f), new Vector2(120f, 22f), new Vector2(1f, 1f));
            _targetHpValue = AddText(panel.transform, "", 20, HpRed, TextAnchor.UpperRight);
            Anchor(_targetHpValue.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-58f, -2f), new Vector2(120f, 24f), new Vector2(1f, 1f));

            // Enemy HP bar.
            var hpBg = AddImage(panel.transform, TrackBg);
            Anchor(hpBg.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(16f, -6f), new Vector2(-32f, 14f), new Vector2(0f, 0.5f));
            _targetHpFill = AddImage(hpBg.transform, HpRed);
            FillBarLeft(_targetHpFill);

            // Role + threat row (role label + star-style threat glyphs).
            _targetThreat = AddText(panel.transform, "", 13, Parchment, TextAnchor.LowerLeft);
            Anchor(_targetThreat.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(16f, 24f), new Vector2(-180f, 18f), new Vector2(0f, 0f));

            // ATK/DEF deep stats (gated behind the future scout/inspect skill).
            _targetDeepStats = AddText(panel.transform, "", 12, new Color(0.78f, 0.80f, 0.86f, 1f), TextAnchor.LowerLeft);
            Anchor(_targetDeepStats.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(16f, 6f), new Vector2(-180f, 16f), new Vector2(0f, 0f));

            // Lock state + lock-toggle button (bottom-right of the block).
            _lockState = AddText(panel.transform, "Unlocked", 12, LockOff, TextAnchor.LowerRight);
            Anchor(_lockState.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-58f, 6f), new Vector2(96f, 18f), new Vector2(1f, 0f));
            _lockBtnBg = AddIconButton(panel.transform, RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconShield), "L",
                                       new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-28f, 0f), new Vector2(44f, 44f),
                                       PanelDim, Gold, ToggleLock);
            MakeCircle(_lockBtnBg);

            BuildStarConditions();
        }

        // Star-conditions + live countdown readout (owner refinement: "add star conditions
        // somewhere" in the top area). Shows the time-box thresholds from BattleStarRating
        // + the live elapsed clock. Anchor is a tunable const (SC_Anchor/SC_Pos) so the owner
        // can park it under the enemy block (default) or top-right near settings.
        private void BuildStarConditions()
        {
            var panel = AddPanel(transform, SC_Anchor, SC_Anchor, SC_Pos, SC_Size, PanelDim);
            Frame(panel);
            _starCondText = AddText(panel.transform, "", 13, Gold, TextAnchor.MiddleCenter);
            Stretch(_starCondText.rectTransform);
        }

        // Star CONDITIONS (the time-box goal): show the duration thresholds that earn 3/2/1
        // stars (from BattleStarRating) + the LIVE elapsed clock ticking, so the player sees
        // what to beat. Thresholds are the BattleStarRating consts; the clock is the battle timer.
        private void PushStarConditions()
        {
            if (_starCondText == null) return;
            int s = Mathf.Max(0, Mathf.FloorToInt(Time.time - _battleStart));
            string clock = (s / 60) + ":" + (s % 60).ToString("00");
            _starCondText.text = "3* <" + Mathf.RoundToInt(BattleStarRating.ThreeStarSeconds) + "s  |  "
                               + "2* <" + Mathf.RoundToInt(BattleStarRating.TwoStarSeconds) + "s  |  "
                               + clock;
        }

        private void PushTarget()
        {
            if (_targetName == null) return;
            if (_target == null) { _targetName.text = "No Target"; return; }
            var cur = _target.CurrentTarget;
            var curMb = cur as MonoBehaviour;
            var en = (curMb != null) ? curMb.GetComponentInParent<Enemy>() : null;
            if (cur == null || !cur.IsAlive || en == null)
            {
                _targetName.text = "No Target";
                if (_targetHpValue != null) _targetHpValue.text = "";
                if (_targetLevel != null) _targetLevel.text = "";
                if (_targetHpFill != null) _targetHpFill.fillAmount = 0f;
                if (_targetThreat != null) _targetThreat.text = "";
                if (_targetDeepStats != null) _targetDeepStats.text = "";
                if (_lockState != null) { _lockState.text = "Unlocked"; _lockState.color = LockOff; }
                if (_lockBtnBg != null) _lockBtnBg.color = PanelDim;
                // WO-512 slice 2: no target -> revert the loud LOCK styling to neutral.
                if (_targetPanelBg != null) _targetPanelBg.color = PanelDark;
                if (_targetName != null) _targetName.color = Gold;
                return;
            }
            // Role first so the friendly name can fold the role token ("Orc Tank").
            var role = RoleOf(en);
            // WO-512 slice 2: show a FRIENDLY label, not the raw "ArenaEnemy_orc-tank_1".
            _targetName.text = FriendlyTargetName(en, role);
            if (_targetHpValue != null) _targetHpValue.text = Mathf.CeilToInt(Mathf.Max(0f, en.Hp)).ToString();
            if (_targetHpFill != null) _targetHpFill.fillAmount = en.HpFraction;

            // LEVEL: Enemy exposes no public Level field; derive a readable stub from max-HP
            // (cheap, monotone) until an EnemyDef.Level surfaces. Stubs gracefully ("Lv ?").
            if (_targetLevel != null) _targetLevel.text = "Lv " + EnemyLevelStub(en);

            // Role + threat row: role label + threat glyphs scaled by remaining HP fraction.
            if (_targetThreat != null)
            {
                int threat = 1 + Mathf.Clamp(Mathf.FloorToInt(en.HpFraction * 3f), 0, 2); // 1..3 stars
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < 3; i++) sb.Append(i < threat ? "* " : "- ");
                _targetThreat.text = RoleName(role) + "   " + sb.ToString().TrimEnd();
                _targetThreat.color = RoleColor(role);
            }

            // ATK/DEF deep stats - only when the scout/inspect gate is on. Enemy exposes no
            // public ATK/DEF; stub gracefully ("-") so the bones read without touching Enemy.cs.
            if (_targetDeepStats != null)
                _targetDeepStats.text = ShowEnemyDeepStats ? "ATK -   DEF -" : "";

            // WO-512 slice 2: LOUD, guaranteed-visible lock confirmation (no camera/reticle
            // dependency). When the single lock owner reports LockEngaged we shout it on the
            // top-center panel: "LOCKED" in red, the target NAME reddens, and the whole panel
            // background tints dark-red - unmissable even when the camera frames the orc.
            // Gated by FeatureFlags.LockOn so flag-off is byte-identical to today (neutral).
            bool locked = DeNelle.Core.FeatureFlags.LockOn && _target != null && _target.LockEngaged;
            if (_lockState != null)
            {
                _lockState.text = locked ? "LOCKED" : "Unlocked";
                _lockState.color = locked ? LockOn : LockOff;
            }
            if (_lockBtnBg != null) _lockBtnBg.color = locked ? LockOn : PanelDim;
            if (_targetPanelBg != null) _targetPanelBg.color = locked ? PanelLocked : PanelDark;
            if (_targetName != null) _targetName.color = locked ? LockOn : Gold;
        }

        // Lock toggle (WO-512): route ALL lock intent through the single owner, HeroTargetIndicator.
        // ON engages the lock onto the current target (the indicator owns the per-frame
        // AimPointOverride/LockedTarget writes now — NO duplicate aim writes here); OFF releases to
        // auto-nearest free-look. The Locked/Unlocked label READS HeroTargetIndicator.LockEngaged.
        private void ToggleLock()
        {
            if (_target == null) return;
            if (_target.LockEngaged) _target.ReleaseLock();
            else _target.EngageLock(_target.CurrentTarget);
            Debug.Log("[BattleHud9Zone] lock toggled -> " + (_target.LockEngaged ? "LOCKED" : "UNLOCKED"));
        }

        // ---------------------------------------------------------------------
        //  TOP-RIGHT - Settings gear + FLEE (Flee lives here now)
        // ---------------------------------------------------------------------
        private void BuildTopRightSettingsFlee()
        {
            var box = AddPanel(transform, new Vector2(1f, 1f), new Vector2(1f, 1f), TR_Pos, TR_Size, PanelDark);
            Frame(box);

            // Settings gear (left of the FLEE button).
            AddIconButton(box.transform, RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconSettings), "*",
                          new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(48f, 48f),
                          PanelDim, Parchment, null);

            // FLEE (de-emphasised retreat; reuses BattleArena.Flee).
            AddTextButton(box.transform, "FLEE", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                          new Vector2(-78f, 0f), new Vector2(120f, 48f), new Color(0.42f, 0.20f, 0.20f, 0.85f), OnFlee);
        }

        private void OnFlee()
        {
            if (_onFlee != null) { _onFlee(); return; }
            var arena = BattleArena.Existing;
            if (arena != null) arena.Flee();
            else Debug.Log("[BattleHud9Zone] FLEE - no BattleArena to flee from.");
        }

        // ---------------------------------------------------------------------
        //  TARGET CYCLE - vertical enemy-roster list in the MID-LEFT zone (owner
        //  2026-06-24: moved off the top-left so it no longer overlaps the player-
        //  stats plate). Anchored left-edge / vertically centered (0, 0.5).
        //  (render: each enemy = class-colored SQUARE icon + name + HP + a ">" chevron).
        // ---------------------------------------------------------------------
        private void BuildTargetCycleList()
        {
            var panel = AddPanel(transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), ML_Pos, ML_Size, PanelDark);
            Frame(panel);

            var hdr = AddText(panel.transform, "TARGET CYCLE", 11, Parchment, TextAnchor.UpperLeft);
            Anchor(hdr.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -6f), new Vector2(-12f, 16f), new Vector2(0f, 1f));

            for (int i = 0; i < ML_MaxRows; i++)
            {
                var rowGo = new GameObject("CycleRow_" + i);
                rowGo.transform.SetParent(panel.transform, false);
                var rrt = rowGo.AddComponent<RectTransform>();
                rrt.anchorMin = new Vector2(0f, 1f); rrt.anchorMax = new Vector2(1f, 1f); rrt.pivot = new Vector2(0.5f, 1f);
                rrt.offsetMin = new Vector2(8f, 0f); rrt.offsetMax = new Vector2(-8f, 0f);
                rrt.sizeDelta = new Vector2(rrt.sizeDelta.x, ML_RowH);
                rrt.anchoredPosition = new Vector2(0f, -26f - i * (ML_RowH + ML_RowGap));

                // Tappable row (selects/cycles to this enemy).
                var rowBtn = rowGo.AddComponent<Image>();
                rowBtn.color = PanelDim;
                var btn = rowGo.AddComponent<Button>();
                btn.targetGraphic = rowBtn;
                int idx = i;
                btn.onClick.AddListener(() => SelectCycleRow(idx));

                var row = new CycleRow { Root = rowGo };

                // Class-colored SQUARE icon (no MakeCircle - render shows squares).
                row.Portrait = AddImage(rowGo.transform, new Color(0.12f, 0.12f, 0.15f, 1f));
                Anchor(row.Portrait.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(40f, 40f), new Vector2(0f, 0.5f));

                row.Name = AddText(rowGo.transform, "-", 14, Parchment, TextAnchor.UpperLeft);
                Anchor(row.Name.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(56f, -6f), new Vector2(-30f, 20f), new Vector2(0f, 1f));

                var hpBg = AddImage(rowGo.transform, TrackBg);
                Anchor(hpBg.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(56f, 8f), new Vector2(-30f, 10f), new Vector2(0f, 0f));
                row.HpFill = AddImage(hpBg.transform, ColDps);
                FillBarLeft(row.HpFill);

                // ">" chevron (cycle affordance, far right).
                var chev = AddText(rowGo.transform, ">", 20, Gold, TextAnchor.MiddleRight);
                Anchor(chev.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(20f, 28f), new Vector2(1f, 0.5f));
                chev.raycastTarget = false;

                _cycleRows.Add(row);
            }
        }

        private void PushTargetCycle()
        {
            if (Time.time >= _nextFamilyScan)
            {
                _nextFamilyScan = Time.time + 0.3f;
                RebindFamily();
            }
            for (int i = 0; i < _cycleRows.Count; i++)
            {
                var row = _cycleRows[i];
                bool alive = row.Tracked != null && !row.Tracked.IsDead;
                if (row.Root != null) row.Root.SetActive(row.Tracked != null);
                if (!alive)
                {
                    if (row.HpFill != null) row.HpFill.fillAmount = 0f;
                    if (row.Name != null) row.Name.color = DeadGrey;
                    continue;
                }
                var role = RoleOf(row.Tracked);
                if (row.Name != null)
                {
                    row.Name.text = row.Tracked.name.Replace("(Clone)", "").Trim();
                    row.Name.color = Parchment;
                }
                if (row.HpFill != null)
                {
                    row.HpFill.fillAmount = row.Tracked.HpFraction;
                    row.HpFill.color = RoleColor(role);
                }
                if (row.Portrait != null)
                {
                    var sp = RoleIcon(role);
                    if (sp != null) { row.Portrait.sprite = sp; row.Portrait.color = Color.white; }
                    else row.Portrait.color = RoleColor(role);
                }
            }
        }

        // Bind each row to a living family member (one row per enemy, nearest-first).
        private void RebindFamily()
        {
            var enemies = Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);
            // Sort by distance to the hero for a stable, readable list.
            Vector3 me = _abilities != null ? _abilities.transform.position : Vector3.zero;
            System.Array.Sort(enemies, (a, b) =>
            {
                if (a == null) return 1; if (b == null) return -1;
                return (a.transform.position - me).sqrMagnitude.CompareTo((b.transform.position - me).sqrMagnitude);
            });
            int r = 0;
            for (int e = 0; e < enemies.Length && r < _cycleRows.Count; e++)
            {
                var en = enemies[e];
                if (en == null || en.IsDead) continue;
                _cycleRows[r].Tracked = en;
                r++;
            }
            for (; r < _cycleRows.Count; r++) _cycleRows[r].Tracked = null;
        }

        // Row tap -> engage the soft lock-on onto that enemy (WO-512): route through the single lock
        // owner, HeroTargetIndicator, which owns the per-frame aim writes (no direct _abilities writes).
        private void SelectCycleRow(int idx)
        {
            if (idx < 0 || idx >= _cycleRows.Count) return;
            var en = _cycleRows[idx].Tracked;
            if (en == null || en.IsDead) return;
            var dmg = en.GetComponent<IDamageable>() ?? en.GetComponentInParent<IDamageable>();
            if (dmg != null && _target != null)
            {
                _target.EngageLock(dmg);
                Debug.Log("[BattleHud9Zone] target-cycle select -> " + en.name + ".");
            }
        }

        // ---------------------------------------------------------------------
        //  MID-RIGHT - FOCUS AREA: heal-toggle + Attack + mode switch (PLACEHOLDER)
        // ---------------------------------------------------------------------
        private void BuildMidRightFocusArea()
        {
            var panel = AddPanel(transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), MR_Pos, MR_Size, PanelDark);
            Frame(panel);

            var hdr = AddText(panel.transform, "FOCUS", 11, Parchment, TextAnchor.UpperCenter);
            Anchor(hdr.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -6f), new Vector2(-12f, 16f), new Vector2(0.5f, 1f));

            // Heal-toggle (placeholder - toggles a heal-focus mode later).
            AddTextButton(panel.transform, "Heal: Off", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                          new Vector2(0f, -34f), new Vector2(168f, 48f), ColHealer, () => Debug.Log("[BattleHud9Zone] focus heal-toggle (placeholder)."));

            // Attack (placeholder - a quick-focus attack press later).
            AddTextButton(panel.transform, "Attack", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                          new Vector2(0f, -90f), new Vector2(168f, 48f), ColDps, () => Debug.Log("[BattleHud9Zone] focus attack (placeholder)."));

            // Mode switch (attack / ranged / spell - placeholder cycle).
            AddTextButton(panel.transform, "Mode: Attack", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                          new Vector2(0f, -146f), new Vector2(168f, 48f), ColWizard, () => Debug.Log("[BattleHud9Zone] focus mode-switch (placeholder)."));
        }

        // ---------------------------------------------------------------------
        //  BOTTOM-LEFT - virtual D-PAD: a CROSS of 4 directional arrows + center dot
        // ---------------------------------------------------------------------
        private void BuildBottomLeftDPad()
        {
            // Bones: 4 arrow buttons in a cross + a center dot. Touch-drive of locomotion is a
            // finesse pass; desktop keeps WASD, so these are visual + log-only for now.
            // up / down / left / right offsets from the d-pad center.
            var dirs = new[]
            {
                new Vector2(0f,  BL_PadGap),   // up
                new Vector2(0f, -BL_PadGap),   // down
                new Vector2(-BL_PadGap, 0f),   // left
                new Vector2( BL_PadGap, 0f),   // right
            };
            string[] glyph = { "^", "v", "<", ">" };
            for (int i = 0; i < 4; i++)
            {
                AddIconButton(transform, null, glyph[i], new Vector2(0f, 0f), new Vector2(0f, 0f),
                              BL_PadCenter + dirs[i], new Vector2(BL_PadBtn, BL_PadBtn), PanelDim, Parchment,
                              null);
            }
            // Center dot.
            var dot = AddImage(transform, new Color(0.85f, 0.82f, 0.70f, 0.55f));
            var dr = dot.rectTransform;
            dr.anchorMin = new Vector2(0f, 0f); dr.anchorMax = new Vector2(0f, 0f); dr.pivot = new Vector2(0.5f, 0.5f);
            dr.sizeDelta = new Vector2(BL_PadDot, BL_PadDot); dr.anchoredPosition = BL_PadCenter;
            MakeCircle(dot);
        }

        // ---------------------------------------------------------------------
        //  BOTTOM-CENTER - horizontal ROW of the unlocked ability icons (Q/W/E/R)
        //  + the placeholder consumables + stars/keep-timer tucked to the right.
        // ---------------------------------------------------------------------
        private void BuildBottomCenterAbilityRow()
        {
            // The ability row is SKILL-TREE / LOADOUT driven (NOT hardcoded Q/W/E/R):
            //   Q     = the class BASIC ATTACK (always present).
            //   W/E/R = whatever the player has EQUIPPED in the skill-tree loadout; an
            //           unequipped slot renders EMPTY (the button hides).
            // We build all four slot bones here; PushAbilityCooldowns() re-reads the live
            // HeroLoadout each frame and (re)binds icon/label/visibility, so a weapon-skill
            // swap updates the bar without a rebuild.
            for (int i = 0; i < 4; i++)
            {
                var slot = (AbilitySlot)i;
                // Owner 2026-06-27 (F8 correction): the 4 STATIC DEFAULTS sit in the BOTTOM-RIGHT.
                // Slot 0 (Q = THRUST) is the BIG button at BR_SlashPos; slots 1..3 (PARRY/HEAL/CHARGE
                // = W/E/R) fan in a rounded ARC around it (BR_ArcRadius / BR_ArcAnglesDeg), all
                // anchored to the bottom-right corner. Always present (class-kit default fallback in
                // ResolveSlotDef), so no "+" ever shows here — that's the bottom-MIDDLE bar's job.
                bool big = (i == 0);
                Vector2 anchor = new Vector2(1f, 0f);   // all four hug the bottom-right corner
                Vector2 pos;
                float   size;
                if (big)
                {
                    pos = BR_SlashPos; size = BR_SlashSize;
                }
                else
                {
                    float ang = BR_ArcAnglesDeg[i - 1] * Mathf.Deg2Rad;
                    pos  = BR_SlashPos + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * BR_ArcRadius;
                    size = BR_SatSize;
                }

                var btn = AddPanel(transform, anchor, anchor, pos,
                                   new Vector2(size, size), big ? PanelDark : PanelDim);
                var b = btn.gameObject.AddComponent<Button>();
                b.targetGraphic = btn;
                b.onClick.AddListener(() => Cast(slot));
                MakeCircle(btn);
                Frame(btn);

                // Icon child (re-bound per frame to the equipped ability).
                var icon = AddImage(btn.transform, new Color(1f, 1f, 1f, 0f));
                var ir = icon.rectTransform;
                ir.anchorMin = new Vector2(0.5f, 0.5f); ir.anchorMax = new Vector2(0.5f, 0.5f); ir.pivot = new Vector2(0.5f, 0.5f);
                ir.sizeDelta = new Vector2(size, size) * 0.74f; ir.anchoredPosition = Vector2.zero;
                icon.raycastTarget = false;

                var ring = AddImage(btn.transform, RingTrack);
                Stretch(ring.rectTransform);
                MakeCircle(ring);
                ring.type = Image.Type.Filled;
                ring.fillMethod = Image.FillMethod.Radial360;
                ring.fillOrigin = (int)Image.Origin360.Top;
                ring.fillClockwise = false;
                ring.fillAmount = 0f;
                ring.raycastTarget = false;

                var cdText = AddText(btn.transform, "", 20, Color.white, TextAnchor.MiddleCenter);
                Stretch(cdText.rectTransform);
                cdText.raycastTarget = false;

                var label = AddText(btn.transform, "", 11, Parchment, TextAnchor.UpperCenter);
                Anchor(label.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, -16f), new Vector2(0f, 14f), new Vector2(0.5f, 1f));
                label.raycastTarget = false;

                // "+" placeholder for an empty assignable W/E/R slot (the bottom-middle bar). Q is
                // always equipped so its plus never shows. Toggled by PushAbilityCooldowns.
                var plus = AddText(btn.transform, "+", 30, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleCenter);
                Stretch(plus.rectTransform);
                plus.raycastTarget = false;
                plus.gameObject.SetActive(false);

                _abilityBtns[i] = new AbilityBtn { Slot = slot, Root = btn.gameObject, Disc = btn,
                                                   Icon = icon, CdRing = ring, CdText = cdText, Label = label,
                                                   Plus = plus, BoundId = null };
            }

            // Placeholder consumables (Potion / Rapid Heal / Desperate WS) to the right of the row.
            string[] cons = { "Potion", "Rapid Heal", "Desperate WS" };
            Sprite potionSp = RpgUiCatalog.Get(RpgUiCatalog.RolePotion, RpgUiCatalog.PotionHealth);
            for (int i = 0; i < cons.Length; i++)
            {
                Vector2 pos = BC_UtilPos + new Vector2(i * BC_ConsumeGap, 14f);
                var c = AddIconButton(transform, i == 0 ? potionSp : null, i == 0 ? "" : (i == 1 ? "+" : "!"),
                                      new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), pos,
                                      new Vector2(BC_ConsumeSize, BC_ConsumeSize), PanelDim, Gold,
                                      () => Debug.Log("[BattleHud9Zone] consumable (placeholder)."));
                MakeCircle(c);
                var lbl = AddText(transform, cons[i], 9, Parchment, TextAnchor.LowerCenter);
                var lr = lbl.rectTransform;
                lr.anchorMin = new Vector2(0.5f, 0f); lr.anchorMax = new Vector2(0.5f, 0f); lr.pivot = new Vector2(0.5f, 1f);
                lr.sizeDelta = new Vector2(BC_ConsumeGap, 12f); lr.anchoredPosition = pos + new Vector2(0f, -32f);
            }

            // Stars earned + keep-star timer (wired to BattleStarRating + the battle clock).
            _starsText = AddText(transform, "- - -", 24, Gold, TextAnchor.MiddleCenter);
            Anchor(_starsText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), BC_UtilPos + new Vector2(BC_ConsumeGap * 3f + 30f, 24f), new Vector2(120f, 32f), new Vector2(0.5f, 0f));
            _keepTimerText = AddText(transform, "0:00", 13, Parchment, TextAnchor.MiddleCenter);
            Anchor(_keepTimerText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), BC_UtilPos + new Vector2(BC_ConsumeGap * 3f + 30f, 2f), new Vector2(120f, 16f), new Vector2(0.5f, 0f));
        }

        // Owner 2026-06-27 (F8 correction): the bottom-MIDDLE is the player-ASSIGNABLE skill bar —
        // a row of slots the player fills from the Skill Tree with EXTRA (non-default) skills. Each
        // cell is an empty "+" placeholder until assigned; when filled it shows the assigned skill's
        // glyph + name. Backed by AssignableSkillBar (its OWN persisted, battle-locked store —
        // SEPARATE from the 4 static defaults in the bottom-right). PushExtraBar binds it each frame.
        private void BuildBottomCenterTemplate()
        {
            int n = _extraBtns.Length;
            for (int i = 0; i < n; i++)
            {
                Vector2 pos = BC_Pos + new Vector2((i - (n - 1) / 2f) * BC_AbilityGap, 0f);
                var cell = AddPanel(transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), pos,
                                    new Vector2(BC_AbilitySize, BC_AbilitySize), PanelDim);
                MakeCircle(cell);
                Frame(cell);
                if (cell.TryGetComponent<UnityEngine.UI.Image>(out var cimg))
                    cimg.color = new Color(cimg.color.r, cimg.color.g, cimg.color.b, 0.40f);

                // DATA-driven concept icon: transparent until PushExtraBar resolves one from
                // concept-icons.json. When it resolves, the glyph is cleared and this shows instead.
                var icon = AddImage(cell.transform, new Color(1f, 1f, 1f, 0f));
                var ir = icon.rectTransform;
                ir.anchorMin = new Vector2(0.18f, 0.18f); ir.anchorMax = new Vector2(0.82f, 0.82f);
                ir.offsetMin = Vector2.zero; ir.offsetMax = Vector2.zero;
                icon.raycastTarget = false;

                var glyph = AddText(cell.transform, "", 22, Color.white, TextAnchor.MiddleCenter);
                Stretch(glyph.rectTransform);
                glyph.raycastTarget = false;

                var plus = AddText(cell.transform, "+", 30, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleCenter);
                Stretch(plus.rectTransform);
                plus.raycastTarget = false;

                var label = AddText(cell.transform, "", 10, Parchment, TextAnchor.UpperCenter);
                Anchor(label.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, -14f), new Vector2(0f, 12f), new Vector2(0.5f, 1f));
                label.raycastTarget = false;

                int idx = i;
                var b = cell.gameObject.AddComponent<Button>();
                b.targetGraphic = cell;
                b.onClick.AddListener(() => OnExtraTapped(idx));

                _extraBtns[i] = new ExtraBtn { Slot = i, Disc = cell, Icon = icon, Glyph = glyph, Plus = plus, Label = label, BoundId = null };
            }

            var cap = AddText(transform, "SKILL TREE", 11, Parchment, TextAnchor.UpperCenter);
            Anchor(cap.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                   BC_Pos + new Vector2(0f, -18f), new Vector2(n * BC_AbilityGap, 14f), new Vector2(0.5f, 1f));
            cap.raycastTarget = false;
        }

        // Bind the assignable EXTRA bar to its persisted store each frame (dumb copy: store -> widget).
        private void PushExtraBar()
        {
            var bar = AssignableSkillBarAccess.Current;
            for (int i = 0; i < _extraBtns.Length; i++)
            {
                var b = _extraBtns[i];
                if (b == null) continue;
                string id = bar != null ? bar.AbilityIdForSlot(b.Slot) : null;
                bool filled = !string.IsNullOrEmpty(id);

                if (b.Plus != null && b.Plus.gameObject.activeSelf == filled) b.Plus.gameObject.SetActive(!filled);
                if (!filled)
                {
                    if (b.BoundId != null) FlowTrace.Step("Hud", "skill bar slot " + b.Slot + " rendered EMPTY (+)");
                    b.BoundId = null;
                    if (b.Icon != null) { b.Icon.sprite = null; b.Icon.color = new Color(1f, 1f, 1f, 0f); }
                    if (b.Glyph != null) b.Glyph.text = "";
                    if (b.Label != null) b.Label.text = "";
                    if (b.Disc != null) { var c = b.Disc.color; b.Disc.color = new Color(c.r, c.g, c.b, 0.40f); }
                    continue;
                }

                if (!string.Equals(b.BoundId, id, System.StringComparison.OrdinalIgnoreCase))
                {
                    b.BoundId = id;
                    var def = AbilityCatalog.FindById(id);
                    if (b.Disc != null)
                    {
                        Color c = AbilityColor(def, b.Slot);
                        b.Disc.color = new Color(c.r, c.g, c.b, 0.85f);
                    }
                    // DATA-FIRST icon: resolve a real sprite from concept-icons.json by the def's own
                    // ids; only fall back to the letter-glyph when nothing maps / the art is absent.
                    var sp = ConceptIconResolver.ResolveAny(ConceptIdsFor(def, id));
                    if (b.Icon != null)
                    {
                        if (sp != null) { b.Icon.sprite = sp; b.Icon.color = Color.white; }
                        else { b.Icon.sprite = null; b.Icon.color = new Color(1f, 1f, 1f, 0f); }
                    }
                    if (b.Glyph != null) b.Glyph.text = sp != null ? "" : ExtraGlyph(def, id);
                    if (b.Label != null) b.Label.text = def != null && !string.IsNullOrEmpty(def.Name) ? def.Name : id;
                    FlowTrace.Step("Hud", "skill bar slot " + b.Slot + " rendered id=" + id +
                        (sp != null ? " (icon)" : " (glyph)"));
                }
            }
        }

        // Tap an assignable slot: a filled extra would CAST, but HeroAbilities is a 4-slot engine
        // (Q/W/E/R) — casting a 5th+ assigned skill is not yet wired, so we trace the intent. An
        // empty slot cues the player to assign from the Skill Tree (assignment is out-of-battle).
        private void OnExtraTapped(int idx)
        {
            if (idx < 0 || idx >= _extraBtns.Length) return;
            var b = _extraBtns[idx];
            if (b != null && !string.IsNullOrEmpty(b.BoundId))
                FlowTrace.Step("Hud", "assignable skill slot " + idx + " tapped (cast for id=" + b.BoundId + " not yet wired — 4-slot engine)");
            else
                FlowTrace.Step("Hud", "assignable skill slot " + idx + " tapped EMPTY — assign from the Skill Tree (out of battle)");
        }

        // A compact display token for an assigned extra ability (no abilityId->sprite map exists; the
        // default-bar icons are class/slot-keyed). Prefer the def's HUD glyph, else the name initials.
        private static string ExtraGlyph(AbilityDef def, string id)
        {
            if (def != null && !string.IsNullOrEmpty(def.Icon)) return def.Icon;
            string src = def != null && !string.IsNullOrEmpty(def.Name) ? def.Name : (id ?? "");
            src = src.Trim();
            if (src.Length == 0) return "?";
            // First letter of up to the first two words (e.g. "Snare Arrow" -> "SA").
            var parts = src.Split(new[] { ' ', '-', '_', '.' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2) return ("" + char.ToUpperInvariant(parts[0][0]) + char.ToUpperInvariant(parts[1][0]));
            return char.ToUpperInvariant(src[0]).ToString();
        }

        // Build the ordered concept-id candidates for an ability from its OWN data — the unique
        // id, the bound key, the gameplay effect, then the first name word. NO icon names appear
        // here; concept-icons.json maps whichever candidate it knows (ConceptIconResolver picks the
        // first that resolves to real art). This is how the HUD stays dumb + data-driven.
        private static string[] ConceptIdsFor(AbilityDef def, string boundId)
        {
            string nameTok = null;
            if (def != null && !string.IsNullOrEmpty(def.Name))
            {
                var parts = def.Name.Split(new[] { ' ', '-', '_', '.' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0) nameTok = parts[0];
            }
            return new[]
            {
                def != null ? def.Id : null,
                boundId,
                def != null ? def.Effect : null,
                nameTok,
            };
        }

        private void PushBottomCenter()
        {
            float elapsed = Time.time - _battleStart;
            // Live stars: the rating the player would earn if the battle ended NOW.
            int stars = BattleStarRating.StarsForDuration(elapsed);
            if (_starsText != null)
            {
                var sb = new System.Text.StringBuilder(BattleStarRating.MaxStars * 2);
                for (int i = 0; i < BattleStarRating.MaxStars; i++) sb.Append(i < stars ? "* " : "- ");
                _starsText.text = sb.ToString().TrimEnd();
            }
            // Time-to-keep-current-star: seconds until the NEXT threshold drops a star.
            if (_keepTimerText != null)
            {
                float nextDrop = elapsed <= BattleStarRating.ThreeStarSeconds ? BattleStarRating.ThreeStarSeconds
                               : elapsed <= BattleStarRating.TwoStarSeconds ? BattleStarRating.TwoStarSeconds
                               : -1f;
                if (nextDrop < 0f) _keepTimerText.text = "1*";
                else
                {
                    int rem = Mathf.Max(0, Mathf.CeilToInt(nextDrop - elapsed));
                    _keepTimerText.text = (rem / 60) + ":" + (rem % 60).ToString("00");
                }
            }
        }

        // ---------------------------------------------------------------------
        //  BOTTOM-RIGHT - 1 big SLASH anchor (basic attack) + a cluster of 3 round
        //  utility buttons (target-lock / dash / aim) up-left of it. NOT a fanned arc.
        // ---------------------------------------------------------------------
        private void BuildBottomRightSlashCluster()
        {
            // SLASH = the big basic-attack thumb anchor (basic attack = Q per abilities.json).
            var slash = AddIconButton(transform, AbilitySprite(AbilitySlot.Q), "",
                                      new Vector2(1f, 0f), new Vector2(1f, 0f), BR_SlashPos,
                                      new Vector2(BR_SlashSize, BR_SlashSize), PanelDark, Gold, () => Cast(AbilitySlot.Q));
            MakeCircle(slash);
            Frame(slash);
            var sLbl = AddText(slash.transform, "SLASH", 13, Parchment, TextAnchor.MiddleCenter);
            Stretch(sLbl.rectTransform);
            sLbl.raycastTarget = false;

            // 3 small round utility buttons clustered up-left of the SLASH anchor.
            // [0] target-lock -> toggles the lock (same intent as the top-center lock toggle)
            // [1] dash        -> placeholder (a future dash skill)
            // [2] aim/move    -> placeholder (a future aim-assist toggle)
            var uLock = AddIconButton(transform, RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconShield), "O",
                          new Vector2(1f, 0f), new Vector2(1f, 0f), BR_UtilCluster[0],
                          new Vector2(BR_UtilSize, BR_UtilSize), PanelDim, Gold, ToggleLock);
            var uDash = AddIconButton(transform, null, ">>",
                          new Vector2(1f, 0f), new Vector2(1f, 0f), BR_UtilCluster[1],
                          new Vector2(BR_UtilSize, BR_UtilSize), PanelDim, Parchment,
                          () => Debug.Log("[BattleHud9Zone] dash (placeholder)."));
            var uAim = AddIconButton(transform, null, "+",
                          new Vector2(1f, 0f), new Vector2(1f, 0f), BR_UtilCluster[2],
                          new Vector2(BR_UtilSize, BR_UtilSize), PanelDim, Parchment,
                          () => Debug.Log("[BattleHud9Zone] aim/move (placeholder)."));
            MakeCircle(uLock); MakeCircle(uDash); MakeCircle(uAim);
        }

        private void PushAbilityCooldowns()
        {
            if (_abilities == null) return;
            for (int i = 0; i < _abilityBtns.Length; i++)
            {
                var b = _abilityBtns[i];
                if (b == null) continue;

                // LIVE LOADOUT: resolve THIS slot's equipped def each frame. Q = the class
                // basic attack (always present); W/E/R = the equipped skill-tree ability, or
                // null when the slot is unequipped (the button then hides).
                var def = ResolveSlotDef(b.Slot, out string boundId);
                bool equipped = def != null;

                // Assignable W/E/R slots stay VISIBLE: an empty slot shows the "+" placeholder (the
                // player fills it from the Skill Tree) rather than vanishing. Q is always equipped.
                if (b.Root != null && !b.Root.activeSelf) b.Root.SetActive(true);
                if (b.Plus != null && b.Plus.gameObject.activeSelf == equipped) b.Plus.gameObject.SetActive(!equipped);
                if (!equipped)
                {
                    // Only trace the bound -> empty transition (a clear/swap), not every frame.
                    if (b.BoundId != null) FlowTrace.Step("Hud", "battle bar slot " + b.Slot + " rendered EMPTY (+)");
                    b.BoundId = null;
                    if (b.Icon != null) b.Icon.color = new Color(1f, 1f, 1f, 0f);
                    if (b.CdRing != null) b.CdRing.fillAmount = 0f;
                    if (b.CdText != null) b.CdText.text = "";
                    if (b.Label != null) b.Label.text = "";
                    if (b.Disc != null) { var c = b.Disc.color; b.Disc.color = new Color(c.r, c.g, c.b, 0.4f); }
                    continue;
                }

                // Re-bind icon/label/disc-color only when the equipped ability CHANGED (a swap).
                if (!string.Equals(b.BoundId, boundId, System.StringComparison.OrdinalIgnoreCase))
                {
                    b.BoundId = boundId;
                    Color disc = AbilityColor(def, (int)b.Slot);
                    if (b.Disc != null) b.Disc.color = disc;
                    if (b.Icon != null)
                    {
                        // Static-defaults priority (all DATA-decided; the HUD picks NO icon name):
                        //   1) override  — a concept the owner flagged override:true FORCES its Obsidian icon
                        //   2) class art — else our rich per-class slot art (HudIcons/Knight_*) wins
                        //   3) gap-fill  — else the concept->icon table fills a gap when a class has no art
                        //   4) invisible
                        var ids = ConceptIdsFor(def, boundId);
                        var sp = ConceptIconResolver.ResolveAnyOverride(ids);
                        if (sp == null) sp = AbilitySprite(b.Slot);
                        if (sp == null) sp = ConceptIconResolver.ResolveAny(ids);
                        if (sp != null) { b.Icon.sprite = sp; b.Icon.color = Color.white; }
                        else { b.Icon.sprite = null; b.Icon.color = new Color(1f, 1f, 1f, 0f); }
                    }
                    if (b.Label != null) b.Label.text = AbilityName(def, (int)b.Slot);
                    // §12 instrument-first: prove the saved loadout reached the rendered widget.
                    FlowTrace.Step("Hud", "battle bar slot " + b.Slot + " rendered id=" + boundId + " (icon bound)");
                }

                float total = def.Cooldown;
                float remaining = _abilities.CooldownRemaining(b.Slot);
                float frac = (total > 0.001f) ? Mathf.Clamp01(remaining / total) : 0f;
                if (b.CdRing != null) b.CdRing.fillAmount = frac;
                if (b.CdText != null) b.CdText.text = remaining > 0.05f ? Mathf.CeilToInt(remaining).ToString() : "";
                if (b.Disc != null)
                {
                    var c = b.Disc.color;
                    float a = remaining > 0.05f ? 0.55f : 1f;
                    b.Disc.color = new Color(c.r, c.g, c.b, a);
                }
            }
        }

        // Resolve the AbilityDef the player has in a slot RIGHT NOW from the live loadout.
        //   Q     -> the class basic attack (AbilityCatalog.Find(class, Q)); always present.
        //   W/E/R -> HeroLoadout.AbilityIdForSlot -> AbilityCatalog.FindById; null when the
        //            slot is unequipped (caller hides the button). boundId is the id we matched
        //            (the class+slot key for Q, the abilityId for an equipped W/E/R) so the
        //            caller can detect a swap and re-bind only then.
        private AbilityDef ResolveSlotDef(AbilitySlot slot, out string boundId)
        {
            if (slot == AbilitySlot.Q)
            {
                boundId = "Q:" + HeroClassId();
                return AbilityCatalog.Find(HeroClassId(), slot);
            }
            var lo = HeroLoadoutAccess.Current;
            string id = lo != null ? lo.AbilityIdForSlot(slot) : null;
            if (!string.IsNullOrEmpty(id))
            {
                boundId = id;
                return AbilityCatalog.FindById(id);
            }
            // STATIC DEFAULT: the class-kit ability for this slot. The bottom-right defaults bar is
            // always-present (4 abilities), so an unequipped W/E/R shows the class default, never empty.
            boundId = "default:" + HeroClassId() + ":" + slot;
            return AbilityCatalog.Find(HeroClassId(), slot);
        }

        // ---------------------------------------------------------------------
        //  Cast intent (the only writes - fire the existing public cast path)
        // ---------------------------------------------------------------------
        private void Cast(AbilitySlot slot)
        {
            if (_abilities == null) _abilities = Object.FindFirstObjectByType<HeroAbilities>();
            if (_abilities != null) _abilities.TryCast(slot);
        }

        // ---------------------------------------------------------------------
        //  Role / ability helpers
        // ---------------------------------------------------------------------
        private static EnemyRole RoleOf(Enemy e)
        {
            if (e == null) return EnemyRole.DPS;
            var brain = e.GetComponent<EnemyBrain>();
            return brain != null ? brain.Role : EnemyRole.DPS;
        }

        // WO-512 slice 2: turn the raw GameObject name ("ArenaEnemy_orc-tank_1(Clone)")
        // into a friendly target label ("Orc Tank"). Strips the ArenaEnemy_/encounter_/
        // Enemy_ spawn prefixes + the trailing _N index, splits on -/_ , Title-Cases each
        // word, and folds the redundant role token (a name ending in "tank" + a Tank role
        // would read "Orc Tank Tank") so the role suffix isn't doubled. Falls back to the
        // role name alone when nothing readable survives. ASCII-only.
        private static string FriendlyTargetName(Enemy en, EnemyRole role)
        {
            string raw = en != null ? en.name : "";
            if (string.IsNullOrEmpty(raw)) return RoleName(role);
            raw = raw.Replace("(Clone)", "").Trim();

            // Drop known spawn prefixes (case-insensitive, longest first).
            string[] prefixes = { "ArenaEnemy_", "encounter-", "encounter_", "Enemy_", "Enemy-" };
            foreach (var pre in prefixes)
            {
                if (raw.Length >= pre.Length &&
                    raw.Substring(0, pre.Length).ToLowerInvariant() == pre.ToLowerInvariant())
                {
                    raw = raw.Substring(pre.Length);
                    break;
                }
            }

            // Split on - and _ , drop a purely-numeric trailing index, Title-Case words,
            // and skip a word that just repeats the role (avoid "Orc Tank Tank").
            string roleLower = RoleName(role).ToLowerInvariant();
            var parts = raw.Split(new[] { '-', '_', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                string w = parts[i];
                int dummy;
                if (int.TryParse(w, out dummy)) continue;          // trailing _N index
                if (w.ToLowerInvariant() == roleLower) continue;   // role added explicitly below
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(char.ToUpperInvariant(w[0]));
                if (w.Length > 1) sb.Append(w.Substring(1).ToLowerInvariant());
            }

            string family = sb.ToString().Trim();
            string roleName = RoleName(role);
            if (string.IsNullOrEmpty(family)) return roleName;     // nothing readable -> role only
            return family + " " + roleName;                        // e.g. "Orc Tank"
        }

        private static string RoleName(EnemyRole role)
        {
            switch (role)
            {
                case EnemyRole.Tank:     return "Tank";
                case EnemyRole.Healer:   return "Healer";
                case EnemyRole.Ranged:   return "Wizard";
                case EnemyRole.MiniBoss: return "Boss";
                default:                 return "DPS";
            }
        }

        // Derive a readable LEVEL stub from the enemy's max HP (no public Level field exists).
        // Monotone + cheap; graceful "?" when HP is unknown. Replace when EnemyDef.Level lands.
        private static int EnemyLevelStub(Enemy e)
        {
            if (e == null) return 1;
            float maxHp = e.HpFraction > 0.001f ? e.Hp / e.HpFraction : e.Hp;
            return Mathf.Max(1, Mathf.RoundToInt(maxHp / 25f));
        }

        private static Color RoleColor(EnemyRole role)
        {
            switch (role)
            {
                case EnemyRole.Tank:   return ColTank;
                case EnemyRole.Healer: return ColHealer;
                case EnemyRole.Ranged: return ColWizard;   // Wizard = Ranged caster
                default:               return ColDps;       // DPS / MiniBoss
            }
        }

        private static Sprite RoleIcon(EnemyRole role)
        {
            string path;
            switch (role)
            {
                case EnemyRole.Tank:   path = "HudIcons/Knight/knight"; break;
                case EnemyRole.Healer: path = "HudIcons/Healer/healer"; break;
                case EnemyRole.Ranged: path = "HudIcons/Wizard/wizard"; break;
                default:               path = "HudIcons/Ranger/ranger"; break;
            }
            return SafeLoad(path);
        }

        private string HeroClassId()
        {
            if (_abilities != null && !string.IsNullOrEmpty(_abilities.HeroClass)) return _abilities.HeroClass;
            return "knight";
        }

        private Sprite AbilitySprite(AbilitySlot slot)
        {
            string cls = HeroClassId();
            int i = (int)slot;
            string sub;
            switch ((cls ?? "knight").ToLowerInvariant())
            {
                case "mage":
                    sub = i == 0 ? "Wizard/Wizard_Plasma" : i == 1 ? "Wizard/Wizard_Fireball"
                        : i == 2 ? "Wizard/Wizard_Lightining" : "Wizard/Wizard_Meteor";
                    break;
                case "ranger":
                    sub = i == 0 ? "Ranger/Ranger_Ranged_Attack" : i == 1 ? "Ranger/Ranger_Barrage"
                        : i == 2 ? "Ranger/Ranger_Poison_Arrow" : "Ranger/ranger_rapid_fire";
                    break;
                case "cleric":
                    sub = i == 0 ? "Healer/Healer_Heal" : i == 1 ? "Healer/Healer_Group_Heal"
                        : i == 2 ? "Healer/Healer_Holy" : "Healer/Healer_Smite";
                    break;
                default: // knight
                    sub = i == 0 ? "Knight/Knight_Charge" : i == 1 ? "Knight/knight_parry"
                        : i == 2 ? "Knight/Knight_Cleave" : "Knight/knight_thrust";
                    break;
            }
            return SafeLoad("HudIcons/" + sub);
        }

        private static Color AbilityColor(AbilityDef def, int slot)
        {
            if (def != null && !string.IsNullOrEmpty(def.Color) &&
                ColorUtility.TryParseHtmlString(def.Color, out var c)) return new Color(c.r, c.g, c.b, 1f);
            switch (slot)
            {
                case 0: return new Color(0.70f, 0.55f, 0.95f, 1f); // arcane
                case 1: return new Color(0.49f, 0.83f, 0.99f, 1f); // frost
                case 2: return new Color(1f, 0.82f, 0.48f, 1f);    // heal/gold
                default: return new Color(1f, 0.44f, 0.26f, 1f);   // fire/ult
            }
        }

        private static string AbilityName(AbilityDef def, int slot)
        {
            if (def != null && !string.IsNullOrEmpty(def.Name)) return def.Name;
            switch (slot) { case 0: return "Q"; case 1: return "W"; case 2: return "E"; default: return "R"; }
        }

        private static Sprite SafeLoad(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            try { return Resources.Load<Sprite>(path); }
            catch { return null; }
        }

        // ---------------------------------------------------------------------
        //  uGUI builders (solid sprites, WebGL-safe - mirrors BattleArenaHud)
        // ---------------------------------------------------------------------
        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
                DontDestroyOnLoad(es);
            }
        }

        private static Image AddPanel(Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Color col)
        {
            var go = new GameObject("Panel");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = col;
            var rt = img.rectTransform;
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return img;
        }

        private static Image AddImage(Transform parent, Color col)
        {
            var go = new GameObject("Img");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = col;
            return img;
        }

        private static Text AddText(Transform parent, string s, int size, Color col, TextAnchor anchor)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.text = s; t.fontSize = size; t.color = col; t.alignment = anchor;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                  ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private static Image AddIconButton(Transform parent, Sprite sprite, string glyph, Vector2 aMin, Vector2 aMax,
                                           Vector2 pos, Vector2 size, Color bg, Color glyphCol, System.Action onClick)
        {
            var panel = AddPanel(parent, aMin, aMax, pos, size, bg);
            var btn = panel.gameObject.AddComponent<Button>();
            btn.targetGraphic = panel;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            if (sprite != null)
            {
                var icon = AddImage(panel.transform, Color.white);
                icon.sprite = sprite; icon.raycastTarget = false;
                var ir = icon.rectTransform;
                ir.anchorMin = new Vector2(0.5f, 0.5f); ir.anchorMax = new Vector2(0.5f, 0.5f); ir.pivot = new Vector2(0.5f, 0.5f);
                ir.sizeDelta = size * 0.74f; ir.anchoredPosition = Vector2.zero;
            }
            else if (!string.IsNullOrEmpty(glyph))
            {
                var t = AddText(panel.transform, glyph, Mathf.RoundToInt(size.y * 0.42f), glyphCol, TextAnchor.MiddleCenter);
                t.raycastTarget = false;
                Stretch(t.rectTransform);
            }
            return panel;
        }

        private static Button AddTextButton(Transform parent, string label, Vector2 aMin, Vector2 aMax,
                                             Vector2 pos, Vector2 size, Color bg, System.Action onClick)
        {
            var panel = AddPanel(parent, aMin, aMax, pos, size, bg);
            var btn = panel.gameObject.AddComponent<Button>();
            btn.targetGraphic = panel;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            var t = AddText(panel.transform, label, 18, Gold, TextAnchor.MiddleCenter);
            t.raycastTarget = false;
            Stretch(t.rectTransform);
            return btn;
        }

        private static void Frame(Image panel)
        {
            if (panel == null) return;
            var frameSprite = RpgUiCatalog.Get(RpgUiCatalog.RolePanel, RpgUiCatalog.PanelDefault);
            if (frameSprite != null)
            {
                panel.sprite = frameSprite;
                panel.type = Image.Type.Sliced;
                return;
            }
            var outline = new GameObject("Frame");
            outline.transform.SetParent(panel.transform, false);
            outline.transform.SetAsFirstSibling();
            var img = outline.AddComponent<Image>();
            img.color = new Color(Gold.r, Gold.g, Gold.b, 0.5f);
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-2f, -2f); rt.offsetMax = new Vector2(2f, 2f);
        }

        private static void MakeCircle(Image img)
        {
            if (img == null) return;
            var disc = RpgUiCatalog.Get(RpgUiCatalog.RoleBadge);
            if (disc != null) { img.sprite = disc; img.type = Image.Type.Simple; img.preserveAspect = true; }
        }

        private static void FillBarLeft(Image fill)
        {
            Stretch(fill.rectTransform);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
        }

        private static void Anchor(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 offset, Vector2 size, Vector2 pivot)
        {
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
            rt.anchoredPosition = offset; rt.sizeDelta = size;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
    }
}
