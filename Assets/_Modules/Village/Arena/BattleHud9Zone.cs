// =============================================================================
// BattleHud9Zone — the WO-498 mobile battle HUD BONES: a 3x3 tic-tac-toe of
// anchored RectTransform zones over the isolated BattleArena fight.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Arena
//
// SCOPE (owner directive 2026-06-23): this is the BONES/scaffold assembled to the
// WO-498 spec, NOT the creative/finesse pass (the owner tunes look/feel tomorrow).
// FLAG-GATED behind FeatureFlags.BattleHud9Zone (default OFF) so it is safe + togglable.
//
// HP-B2B law (logic / presentation split): this is a self-contained VIEW that PULLS
// read-only state from the existing combat systems every frame (it never mutates them
// beyond firing their own public cast/target intents on a button press):
//   - HeroHealth      (zone 1 HP bar)
//   - HeroAbilities + AbilityCatalog (zone 1 mana pips, zone 8 basic attack, zone 9
//                     ability arc + radial cooldown rings)
//   - HeroTargetIndicator (zone 4 current-target portrait/role, zone 6 quick-focus)
//   - Enemy + EnemyBrain.Role (zone 2 family role overview, zone 4 target)
//
// 3x3 ZONE MAP (the WO-498 tic-tac-toe):
//   1 TL  Knight HP + resource pips        2 TC  enemy family role overview (4 chips)
//   3 TR  timer + pause (+settings/audio)  4 ML  current-target portrait + role
//   5 C   EMPTY (the fight shows through)  6 MR  quick-focus buttons (Healer/Wizard)
//   7 BL  movement joystick (mobile)       8 BC  Basic Attack pill + weapon skill
//   9 BR  4 ability buttons w/ cooldown rings
//
// Code-built uGUI, Canvas Screen Space - Overlay (UXML does NOT ship — CLAUDE.md S8).
// Dark semi-transparent panels (the premium-fantasy backdrop shines through), large
// touch targets, high contrast, landscape-focus. WebGL-safe solid sprites. ASCII logs.
//
// Icons pull from the existing catalogs (owner: don't placeholder) — the staged
// Resources/HudIcons/<Class>/ ability art + Resources/HudIcons/<Role>/<role>.jpg role
// portraits + RpgUiCatalog (gilt frames/icons) + abilities.json per-ability color/glyph.
// Every sprite lookup is null-safe and falls back to a tinted disc + glyph so the bones
// read even when art is absent.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DeNelle.Core;
using DeNelle.Core.UI;
using DeNelle.Core.Combat;

namespace DeNelle.Village.Arena
{
    /// <summary>The 9-zone mobile battle HUD bones (WO-498). Flag-gated, self-contained VIEW.</summary>
    public sealed class BattleHud9Zone : MonoBehaviour
    {
        // ── palette (dark semi-transparent premium fantasy) ───────────────────
        private static readonly Color PanelDark = new Color(0.05f, 0.06f, 0.09f, 0.78f);
        private static readonly Color PanelDim  = new Color(0.05f, 0.06f, 0.09f, 0.55f);
        private static readonly Color Gold      = new Color(0.92f, 0.78f, 0.36f, 1f);
        private static readonly Color Parchment = new Color(0.86f, 0.84f, 0.78f, 1f);
        private static readonly Color HpGreen   = new Color(0.36f, 0.80f, 0.40f, 1f);
        private static readonly Color ManaBlue  = new Color(0.36f, 0.62f, 0.92f, 1f);
        private static readonly Color TrackBg   = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color DeadGrey  = new Color(0.30f, 0.30f, 0.33f, 0.85f);
        private static readonly Color RingTrack = new Color(0f, 0f, 0f, 0.62f);

        // Role colors (WO-498): Tank gray/blue, Healer green, Wizard purple, DPS red.
        private static readonly Color ColTank   = new Color(0.46f, 0.60f, 0.78f, 1f);
        private static readonly Color ColHealer = new Color(0.40f, 0.80f, 0.45f, 1f);
        private static readonly Color ColWizard = new Color(0.66f, 0.45f, 0.92f, 1f);
        private static readonly Color ColDps    = new Color(0.84f, 0.32f, 0.28f, 1f);

        // ── live system refs (self-resolved; read-only pulls) ─────────────────
        private Canvas _canvas;
        private HeroHealth _health;
        private HeroAbilities _abilities;
        private HeroTargetIndicator _target;

        // ── zone 1 — hero plate ───────────────────────────────────────────────
        private Image _hpFill;
        private Text _hpText;
        private readonly List<Image> _resourcePips = new List<Image>();

        // ── zone 2 — enemy family overview chips ──────────────────────────────
        private sealed class RoleChip
        {
            public EnemyRole Role;
            public GameObject Root;
            public Image Disc;
            public Image HpFill;
            public Text Label;
            public Enemy Tracked;   // representative enemy of this role (for the mini HP bar)
        }
        private readonly List<RoleChip> _chips = new List<RoleChip>();
        private float _nextFamilyScan;

        // ── zone 3 — timer + pause ────────────────────────────────────────────
        private Text _timerText;
        private float _battleStart;

        // ── zone 4 — current target ───────────────────────────────────────────
        private Image _targetPortrait;
        private Image _targetRoleDisc;
        private Text _targetName;
        private Text _targetRole;
        private GameObject _targetGroup;

        // ── zone 9 — ability arc ──────────────────────────────────────────────
        private sealed class AbilityBtn
        {
            public AbilitySlot Slot;
            public Image Disc;
            public Image CdRing;   // radial cooldown overlay (Filled / Radial360)
            public Text CdText;
            public Text Label;
        }
        private readonly AbilityBtn[] _abilityBtns = new AbilityBtn[4];

        // The owner's open battle family is mage/tank/warrior — WO-498 asks the legend to
        // read the canonical 4 roles (Tank/Healer/Wizard/DPS) as the role-designation KEY,
        // and chips dim when that role has no living member. The four chips are always built.
        private static readonly EnemyRole[] LegendRoles =
            { EnemyRole.Tank, EnemyRole.Healer, EnemyRole.Ranged /*=Wizard*/, EnemyRole.DPS };

        // ─────────────────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Build the 9-zone overlay (and an EventSystem if none exists), but ONLY when the
        /// FeatureFlags.BattleHud9Zone gate is ON. Returns null when the flag is OFF so the
        /// caller can no-op cleanly. Mirrors BattleArenaHud.Create.
        /// </summary>
        public static BattleHud9Zone Create()
        {
            if (!FeatureFlags.BattleHud9Zone)
            {
                Debug.Log("[BattleHud9Zone] ff.battlehud9zone OFF - 9-zone HUD not spawned (bones await finesse).");
                return null;
            }
            var go = new GameObject("BattleHud9Zone");
            DontDestroyOnLoad(go);
            var hud = go.AddComponent<BattleHud9Zone>();
            hud.Build();
            Debug.Log("[BattleHud9Zone] spawned 9-zone battle HUD bones (WO-498).");
            return hud;
        }

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
            _canvas.sortingOrder = 5200;  // above BattleArenaHud (5000) so the bones own the screen
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            BuildZone1HeroPlate();
            BuildZone2FamilyOverview();
            BuildZone3TimerPause();
            BuildZone4CurrentTarget();
            // Zone 5 (center) is intentionally EMPTY — the fight shows through.
            BuildZone6QuickFocus();
            BuildZone7Joystick();
            BuildZone8BasicAttack();
            BuildZone9AbilityArc();
        }

        private void Update()
        {
            ResolveSystems();
            PushHeroPlate();
            PushFamilyOverview();
            PushTimer();
            PushCurrentTarget();
            PushAbilityCooldowns();
        }

        // Self-resolve the hero subsystems (the hero is warped in by BattleArena; resolve
        // lazily so a late-spawned hero still binds). Unity null checks are explicit.
        private void ResolveSystems()
        {
            if (_health == null) _health = HeroHealth.Instance;
            if (_abilities == null) _abilities = Object.FindFirstObjectByType<HeroAbilities>();
            if (_target == null) _target = Object.FindFirstObjectByType<HeroTargetIndicator>();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  ZONE 1 — Top-Left: Knight HP + resource pips
        // ─────────────────────────────────────────────────────────────────────
        private void BuildZone1HeroPlate()
        {
            var plate = AddPanel(transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                                 new Vector2(220f, -64f), new Vector2(400f, 108f), PanelDark);
            Frame(plate);

            // Shield emblem (role/class crest).
            var emblem = AddIcon(plate.transform, RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconShield),
                                 new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(56f, 56f), Gold);

            var name = AddText(plate.transform, "Knight", 24, Gold, TextAnchor.UpperLeft);
            Anchor(name.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(78f, -10f), new Vector2(-90f, 28f), new Vector2(0f, 1f));

            // HP bar (big green, gilt track).
            var hpBg = AddPanel(plate.transform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                                new Vector2(40f, -2f), new Vector2(-94f, 26f), TrackBg);
            var hr = hpBg.rectTransform; hr.anchorMin = new Vector2(0f, 0.5f); hr.anchorMax = new Vector2(1f, 0.5f);
            hr.offsetMin = new Vector2(78f, -14f); hr.offsetMax = new Vector2(-16f, 12f);
            _hpFill = AddImage(hpBg.transform, HpGreen);
            FillBarLeft(_hpFill);
            _hpText = AddText(hpBg.transform, "100 / 100", 14, Color.white, TextAnchor.MiddleCenter);
            Stretch(_hpText.rectTransform);

            // Resource pips row (placeholder bones — mana orbs; the owner maps real resources tomorrow).
            var pipRow = new GameObject("ResourcePips");
            pipRow.transform.SetParent(plate.transform, false);
            var prt = pipRow.AddComponent<RectTransform>();
            Anchor(prt, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(78f, 14f), new Vector2(-94f, 16f), new Vector2(0f, 0f));
            for (int i = 0; i < 5; i++)
            {
                var pip = AddImage(pipRow.transform, ManaBlue);
                var pr = pip.rectTransform;
                pr.anchorMin = new Vector2(0f, 0.5f); pr.anchorMax = new Vector2(0f, 0.5f);
                pr.pivot = new Vector2(0f, 0.5f);
                pr.sizeDelta = new Vector2(14f, 14f);
                pr.anchoredPosition = new Vector2(i * 20f, 0f);
                _resourcePips.Add(pip);
            }
        }

        private void PushHeroPlate()
        {
            if (_health != null && _hpFill != null)
            {
                float max = _health.MaxHp <= 0f ? 1f : _health.MaxHp;
                float frac = Mathf.Clamp01(_health.Hp / max);
                _hpFill.fillAmount = frac;
                if (_hpText != null) _hpText.text = Mathf.CeilToInt(Mathf.Max(0f, _health.Hp)) + " / " + Mathf.CeilToInt(max);
            }
            // Resource pips = mana (lit up to current mana). Bones; owner remaps to real resources.
            if (_abilities != null && _resourcePips.Count > 0)
            {
                float maxMana = _abilities.MaxMana <= 0f ? _resourcePips.Count : _abilities.MaxMana;
                float per = maxMana / _resourcePips.Count;
                for (int i = 0; i < _resourcePips.Count; i++)
                {
                    bool lit = _abilities.Mana >= (i + 1) * per - 0.001f;
                    _resourcePips[i].color = lit ? ManaBlue : new Color(ManaBlue.r, ManaBlue.g, ManaBlue.b, 0.22f);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  ZONE 2 — Top-Center: enemy family role overview (4 chips, dim-on-death)
        // ─────────────────────────────────────────────────────────────────────
        private void BuildZone2FamilyOverview()
        {
            var bar = AddPanel(transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                               new Vector2(0f, -56f), new Vector2(540f, 92f), PanelDark);
            Frame(bar);

            var legend = AddText(bar.transform, "FAMILY", 12, Parchment, TextAnchor.UpperCenter);
            Anchor(legend.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -4f), new Vector2(0f, 16f), new Vector2(0.5f, 1f));

            float chipW = 124f;
            for (int i = 0; i < LegendRoles.Length; i++)
            {
                var role = LegendRoles[i];
                var chipGo = new GameObject("Chip_" + role);
                chipGo.transform.SetParent(bar.transform, false);
                var crt = chipGo.AddComponent<RectTransform>();
                crt.anchorMin = new Vector2(0.5f, 0.5f); crt.anchorMax = new Vector2(0.5f, 0.5f);
                crt.pivot = new Vector2(0.5f, 0.5f);
                crt.sizeDelta = new Vector2(chipW - 8f, 60f);
                crt.anchoredPosition = new Vector2((i - 1.5f) * chipW, -4f);

                var chip = new RoleChip { Role = role, Root = chipGo };

                // Role disc + icon.
                var disc = AddImage(chipGo.transform, RoleColor(role));
                var dr = disc.rectTransform;
                dr.anchorMin = new Vector2(0f, 0.5f); dr.anchorMax = new Vector2(0f, 0.5f); dr.pivot = new Vector2(0f, 0.5f);
                dr.sizeDelta = new Vector2(34f, 34f); dr.anchoredPosition = new Vector2(2f, 6f);
                var roleSp = RoleIcon(role);
                if (roleSp != null) { disc.sprite = roleSp; disc.color = Color.white; }
                chip.Disc = disc;

                // Label.
                var lbl = AddText(chipGo.transform, RoleLabel(role), 13, Parchment, TextAnchor.MiddleLeft);
                Anchor(lbl.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(42f, 0f), new Vector2(-2f, 24f), new Vector2(0f, 0.5f));
                chip.Label = lbl;

                // Mini HP bar.
                var hpBg = AddImage(chipGo.transform, TrackBg);
                Anchor(hpBg.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(42f, 6f), new Vector2(-2f, 10f), new Vector2(0f, 0f));
                var hpFill = AddImage(hpBg.transform, RoleColor(role));
                FillBarLeft(hpFill);
                chip.HpFill = hpFill;

                _chips.Add(chip);
            }
        }

        private void PushFamilyOverview()
        {
            // Re-scan the live family every ~0.3s (cheap; the family is small) and bind a
            // representative enemy per role so the chip mini-bar + dim-on-death track the fight.
            if (Time.time >= _nextFamilyScan)
            {
                _nextFamilyScan = Time.time + 0.3f;
                RebindFamily();
            }
            for (int i = 0; i < _chips.Count; i++)
            {
                var chip = _chips[i];
                bool alive = chip.Tracked != null && !chip.Tracked.IsDead;
                if (chip.HpFill != null) chip.HpFill.fillAmount = alive ? chip.Tracked.HpFraction : 0f;
                // Dim-on-death: grey the disc + label when no living member of this role.
                if (chip.Disc != null && chip.Disc.sprite == null)
                    chip.Disc.color = alive ? RoleColor(chip.Role) : DeadGrey;
                else if (chip.Disc != null)
                    chip.Disc.color = alive ? Color.white : DeadGrey;
                if (chip.Label != null) chip.Label.color = alive ? Parchment : DeadGrey;
            }
        }

        // Bind each role chip to a living enemy of that role (nearest-with-most-HP is fine for bones).
        private void RebindFamily()
        {
            var enemies = Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);
            for (int c = 0; c < _chips.Count; c++)
            {
                var chip = _chips[c];
                // Keep the current tracked enemy if it's still alive + same role.
                if (chip.Tracked != null && !chip.Tracked.IsDead && RoleOf(chip.Tracked) == chip.Role) continue;
                chip.Tracked = null;
                for (int e = 0; e < enemies.Length; e++)
                {
                    var en = enemies[e];
                    if (en == null || en.IsDead) continue;
                    if (RoleOf(en) == chip.Role) { chip.Tracked = en; break; }
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  ZONE 3 — Top-Right: timer + pause (+ settings/audio, minimal rail)
        // ─────────────────────────────────────────────────────────────────────
        private void BuildZone3TimerPause()
        {
            var box = AddPanel(transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                               new Vector2(-150f, -52f), new Vector2(220f, 72f), PanelDark);
            Frame(box);

            _timerText = AddText(box.transform, "0:00", 26, Gold, TextAnchor.MiddleLeft);
            Anchor(_timerText.rectTransform, new Vector2(0f, 0f), new Vector2(0.55f, 1f), new Vector2(14f, 0f), Vector2.zero, new Vector2(0f, 0.5f));

            // Pause button (bones; wires to Time.timeScale toggle — non-destructive).
            AddIconButton(box.transform, RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconSettings), "II",
                          new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-32f, 0f), new Vector2(44f, 44f),
                          PanelDim, Gold, TogglePause);
            // Settings/audio mini-icon (de-emphasized far-right rail).
            AddIconButton(box.transform, RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconSettings), "*",
                          new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-80f, 0f), new Vector2(40f, 40f),
                          PanelDim, Parchment, null);
        }

        private void PushTimer()
        {
            if (_timerText == null) return;
            int s = Mathf.Max(0, Mathf.FloorToInt(Time.time - _battleStart));
            _timerText.text = (s / 60) + ":" + (s % 60).ToString("00");
        }

        private bool _paused;
        private void TogglePause()
        {
            _paused = !_paused;
            Time.timeScale = _paused ? 0f : 1f;
            Debug.Log("[BattleHud9Zone] pause toggled -> " + (_paused ? "PAUSED" : "RUNNING"));
        }

        // ─────────────────────────────────────────────────────────────────────
        //  ZONE 4 — Middle-Left: current-target portrait + role
        // ─────────────────────────────────────────────────────────────────────
        private void BuildZone4CurrentTarget()
        {
            var panel = AddPanel(transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                 new Vector2(130f, 40f), new Vector2(228f, 132f), PanelDark);
            Frame(panel);
            _targetGroup = panel.gameObject;

            // Portrait disc.
            _targetPortrait = AddImage(panel.transform, new Color(0.12f, 0.12f, 0.15f, 1f));
            Anchor(_targetPortrait.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -14f), new Vector2(72f, 72f), new Vector2(0f, 1f));
            // Small role disc badge over the portrait corner.
            _targetRoleDisc = AddImage(panel.transform, ColDps);
            Anchor(_targetRoleDisc.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(64f, -64f), new Vector2(28f, 28f), new Vector2(0f, 1f));

            _targetName = AddText(panel.transform, "No Target", 16, Parchment, TextAnchor.UpperLeft);
            Anchor(_targetName.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(94f, -16f), new Vector2(-8f, 24f), new Vector2(0f, 1f));
            _targetRole = AddText(panel.transform, "", 14, Gold, TextAnchor.UpperLeft);
            Anchor(_targetRole.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(94f, -42f), new Vector2(-8f, 22f), new Vector2(0f, 1f));
        }

        private void PushCurrentTarget()
        {
            if (_target == null || _targetName == null) return;
            var cur = _target.CurrentTarget;
            var curMb = cur as MonoBehaviour;
            var en = (curMb != null) ? curMb.GetComponentInParent<Enemy>() : null;
            if (cur == null || !cur.IsAlive || en == null)
            {
                _targetName.text = "No Target";
                if (_targetRole != null) _targetRole.text = "";
                if (_targetRoleDisc != null) _targetRoleDisc.color = DeadGrey;
                if (_targetPortrait != null) _targetPortrait.color = new Color(0.12f, 0.12f, 0.15f, 0.6f);
                return;
            }
            var role = RoleOf(en);
            _targetName.text = en.name.Replace("(Clone)", "").Trim();
            if (_targetRole != null) _targetRole.text = RoleLabel(role);
            if (_targetRoleDisc != null) _targetRoleDisc.color = RoleColor(role);
            if (_targetPortrait != null)
            {
                var sp = RoleIcon(role);
                if (sp != null) { _targetPortrait.sprite = sp; _targetPortrait.color = Color.white; }
                else _targetPortrait.color = RoleColor(role);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  ZONE 6 — Middle-Right: quick-focus buttons (Focus Healer / Wizard)
        // ─────────────────────────────────────────────────────────────────────
        private void BuildZone6QuickFocus()
        {
            var col = new GameObject("QuickFocus");
            col.transform.SetParent(transform, false);
            var crt = col.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(1f, 0.5f); crt.anchorMax = new Vector2(1f, 0.5f);
            crt.pivot = new Vector2(1f, 0.5f);
            crt.sizeDelta = new Vector2(190f, 140f);
            crt.anchoredPosition = new Vector2(-20f, 40f);

            AddTextButton(col.transform, "Focus Healer", new Vector2(1f, 1f), new Vector2(1f, 1f),
                          new Vector2(0f, -2f), new Vector2(186f, 56f), ColHealer, () => FocusRole(EnemyRole.Healer));
            AddTextButton(col.transform, "Focus Wizard", new Vector2(1f, 1f), new Vector2(1f, 1f),
                          new Vector2(0f, -66f), new Vector2(186f, 56f), ColWizard, () => FocusRole(EnemyRole.Ranged));
        }

        // Quick-focus: lock HeroTargetIndicator onto the nearest living enemy of the role.
        // We reuse the indicator's own target via its public FocusRole-equivalent — but it
        // exposes only CurrentTarget (get). So we drive its lock by directly handing the
        // ability a locked target through the indicator's public surface where available;
        // for the bones we set the aim by selecting the nearest role member and nudging the
        // ability LockedTarget (HeroAbilities.LockedTarget is public). This is a soft focus.
        private void FocusRole(EnemyRole role)
        {
            var enemies = Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);
            Enemy best = null; float bestSq = float.MaxValue;
            Vector3 me = _abilities != null ? _abilities.transform.position : Vector3.zero;
            for (int i = 0; i < enemies.Length; i++)
            {
                var en = enemies[i];
                if (en == null || en.IsDead || RoleOf(en) != role) continue;
                float sq = (en.transform.position - me).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = en; }
            }
            if (best == null) { Debug.Log("[BattleHud9Zone] FocusRole " + role + " - no living member."); return; }
            var dmg = best.GetComponent<IDamageable>();
            if (dmg == null) dmg = best.GetComponentInParent<IDamageable>();
            if (dmg != null && _abilities != null)
            {
                _abilities.LockedTarget = dmg;
                _abilities.AimPointOverride = dmg.WorldPosition;
                Debug.Log("[BattleHud9Zone] FocusRole " + role + " -> locked " + best.name + ".");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  ZONE 7 — Bottom-Left: movement joystick (mobile; desktop keeps WASD)
        // ─────────────────────────────────────────────────────────────────────
        private void BuildZone7Joystick()
        {
            // Bones: a static joystick base + knob. Touch-drag steering wiring is the finesse
            // pass (the owner tunes joystick feel tomorrow); desktop keeps the existing WASD path,
            // so this is a visual placeholder that does not intercept movement input yet.
            var baseImg = AddImage(transform, PanelDim);
            var br = baseImg.rectTransform;
            br.anchorMin = new Vector2(0f, 0f); br.anchorMax = new Vector2(0f, 0f); br.pivot = new Vector2(0f, 0f);
            br.sizeDelta = new Vector2(180f, 180f); br.anchoredPosition = new Vector2(40f, 40f);
            MakeCircle(baseImg);
            Frame(baseImg);

            var knob = AddImage(baseImg.transform, new Color(0.85f, 0.82f, 0.70f, 0.85f));
            var kr = knob.rectTransform;
            kr.anchorMin = new Vector2(0.5f, 0.5f); kr.anchorMax = new Vector2(0.5f, 0.5f); kr.pivot = new Vector2(0.5f, 0.5f);
            kr.sizeDelta = new Vector2(72f, 72f); kr.anchoredPosition = Vector2.zero;
            MakeCircle(knob);

            // Two small round buttons above the stick (profile, menu) — bones per the mockup.
            AddIconButton(transform, null, "P", new Vector2(0f, 0f), new Vector2(0f, 0f),
                          new Vector2(72f, 244f), new Vector2(48f, 48f), PanelDim, Parchment, null);
            AddIconButton(transform, null, "=", new Vector2(0f, 0f), new Vector2(0f, 0f),
                          new Vector2(140f, 244f), new Vector2(48f, 48f), PanelDim, Parchment, null);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  ZONE 8 — Bottom-Center: Basic Attack pill + weapon skill
        // ─────────────────────────────────────────────────────────────────────
        private void BuildZone8BasicAttack()
        {
            // Wide "Basic Attack" pill -> HeroAbilities.TryCast(Q) (the canonical basic attack
            // per abilities.json; we do NOT touch PlayerAttackController).
            var pill = AddTextButton(transform, "Basic Attack", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                     new Vector2(-70f, 60f), new Vector2(300f, 76f), PanelDark, () => Cast(AbilitySlot.Q));
            Frame(pill.targetGraphic as Image);

            // Weapon-skill button (W slot) beside it — uses the per-class ability art.
            var wsSprite = AbilitySprite(AbilitySlot.W);
            var ws = AddIconButton(transform, wsSprite, "W", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                   new Vector2(130f, 60f), new Vector2(76f, 76f), PanelDark, Gold, () => Cast(AbilitySlot.W));
            Frame(ws);
            MakeCircle(ws);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  ZONE 9 — Bottom-Right: 4 ability buttons w/ radial COOLDOWN RINGS
        // ─────────────────────────────────────────────────────────────────────
        private void BuildZone9AbilityArc()
        {
            // Fan four discs in an arc, bottom-right (Dash/Knockback/Taunt/Ultimate are the V1
            // Knight examples; the bar is skill-tree-driven so it reads the active ability set).
            // Arc anchor positions relative to the bottom-right corner (x left of corner, y up).
            var arc = new[]
            {
                new Vector2(-96f,  72f),
                new Vector2(-176f, 116f),
                new Vector2(-236f, 188f),
                new Vector2(-272f, 280f),
            };

            for (int i = 0; i < 4; i++)
            {
                var slot = (AbilitySlot)i;
                var def = AbilityCatalog.Find(HeroClassId(), slot);
                Color disc = AbilityColor(def, i);

                // Disc button.
                var btn = AddIconButton(transform, AbilitySprite(slot), GlyphFor(def, i),
                                        new Vector2(1f, 0f), new Vector2(1f, 0f), arc[i], new Vector2(96f, 96f),
                                        disc, Color.white, () => Cast(slot));
                MakeCircle(btn);
                Frame(btn);

                // Radial cooldown ring overlay (Filled / Radial360, sweeps as cooldown burns down).
                var ring = AddImage(btn.transform, RingTrack);
                Stretch(ring.rectTransform);
                MakeCircle(ring);
                ring.type = Image.Type.Filled;
                ring.fillMethod = Image.FillMethod.Radial360;
                ring.fillOrigin = (int)Image.Origin360.Top;
                ring.fillClockwise = false;
                ring.fillAmount = 0f;
                ring.raycastTarget = false;

                var cdText = AddText(btn.transform, "", 22, Color.white, TextAnchor.MiddleCenter);
                Stretch(cdText.rectTransform);
                cdText.raycastTarget = false;

                var label = AddText(btn.transform, AbilityName(def, i), 12, Parchment, TextAnchor.UpperCenter);
                Anchor(label.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, -18f), new Vector2(0f, 16f), new Vector2(0.5f, 1f));
                label.raycastTarget = false;

                _abilityBtns[i] = new AbilityBtn { Slot = slot, Disc = btn, CdRing = ring, CdText = cdText, Label = label };
            }
        }

        private void PushAbilityCooldowns()
        {
            if (_abilities == null) return;
            string cls = HeroClassId();
            for (int i = 0; i < _abilityBtns.Length; i++)
            {
                var b = _abilityBtns[i];
                if (b == null) continue;
                var def = AbilityCatalog.Find(cls, b.Slot);
                float total = def != null ? def.Cooldown : 0f;
                float remaining = _abilities.CooldownRemaining(b.Slot);
                float frac = (total > 0.001f) ? Mathf.Clamp01(remaining / total) : 0f;
                if (b.CdRing != null) b.CdRing.fillAmount = frac;
                if (b.CdText != null) b.CdText.text = remaining > 0.05f ? Mathf.CeilToInt(remaining).ToString() : "";
                // Dim the disc while on cooldown.
                if (b.Disc != null)
                {
                    var c = b.Disc.color;
                    float a = remaining > 0.05f ? 0.55f : 1f;
                    b.Disc.color = new Color(c.r, c.g, c.b, a);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Cast intent (the only writes — fire the existing public cast path)
        // ─────────────────────────────────────────────────────────────────────
        private void Cast(AbilitySlot slot)
        {
            if (_abilities == null) _abilities = Object.FindFirstObjectByType<HeroAbilities>();
            if (_abilities != null) _abilities.TryCast(slot);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Role / ability helpers
        // ─────────────────────────────────────────────────────────────────────
        private static EnemyRole RoleOf(Enemy e)
        {
            if (e == null) return EnemyRole.DPS;
            var brain = e.GetComponent<EnemyBrain>();
            return brain != null ? brain.Role : EnemyRole.DPS;
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

        private static string RoleLabel(EnemyRole role)
        {
            switch (role)
            {
                case EnemyRole.Tank:   return "Tank";
                case EnemyRole.Healer: return "Healer";
                case EnemyRole.Ranged: return "Wizard";
                case EnemyRole.MiniBoss: return "Boss";
                default:               return "DPS";
            }
        }

        // Role portrait/icon: reuse the staged Resources/HudIcons/<Role>/<role>.jpg art.
        private static Sprite RoleIcon(EnemyRole role)
        {
            string path;
            switch (role)
            {
                case EnemyRole.Tank:   path = "HudIcons/Knight/knight"; break;     // armored stand-in for tank
                case EnemyRole.Healer: path = "HudIcons/Healer/healer"; break;
                case EnemyRole.Ranged: path = "HudIcons/Wizard/wizard"; break;
                default:               path = "HudIcons/Ranger/ranger"; break;     // DPS stand-in
            }
            return SafeLoad(path);
        }

        private string HeroClassId()
        {
            // HeroAbilities.HeroClass is the live class id ("knight"/"mage"/...). Default knight (V1).
            if (_abilities != null && !string.IsNullOrEmpty(_abilities.HeroClass)) return _abilities.HeroClass;
            return "knight";
        }

        // Per-class ability art (reuses the VillageHudController staged map: Resources/HudIcons/<Class>/...).
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
            switch (slot) { case 0: return "Dash"; case 1: return "Knockback"; case 2: return "Taunt"; default: return "Ultimate"; }
        }

        private static string GlyphFor(AbilityDef def, int slot)
        {
            if (def != null && !string.IsNullOrEmpty(def.Icon)) return def.Icon;
            switch (slot) { case 0: return ">>"; case 1: return "<>"; case 2: return "!"; default: return "*"; }
        }

        private static Sprite SafeLoad(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            try { return Resources.Load<Sprite>(path); }
            catch { return null; }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  uGUI builders (solid sprites, WebGL-safe — mirrors BattleArenaHud)
        // ─────────────────────────────────────────────────────────────────────
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

        // Icon image with explicit anchors (sprite optional; tint applies when no sprite).
        private static Image AddIcon(Transform parent, Sprite sprite, Vector2 aMin, Vector2 aMax,
                                     Vector2 pos, Vector2 size, Color tint)
        {
            var img = AddImage(parent, sprite != null ? Color.white : tint);
            if (sprite != null) img.sprite = sprite;
            var rt = img.rectTransform;
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
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

        // A round/disc icon button with an optional glyph fallback.
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
            var t = AddText(panel.transform, label, 20, Gold, TextAnchor.MiddleCenter);
            t.raycastTarget = false;
            Stretch(t.rectTransform);
            return btn;
        }

        // Gilt frame outline (ornate-panel border). Reuses the RpgUi panel sprite when present,
        // else draws a thin gold border via a slightly-larger backing image.
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
            // Fallback: a thin gilt outline behind the panel.
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

        // Make an Image read as a circle by assigning the RpgUi disc/badge sprite if available
        // (bones: a sliced/round frame). When no round sprite exists, the square panel stands in.
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
