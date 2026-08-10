// =============================================================================
// BattleHudMockupBuilder — assembles a VISUAL-ONLY battle-HUD mockup from the
// REAL Blink "Obsidian" UI art, in a "Diablo orbs" layout, into a NEW scene the
// owner can open and drag pieces around, then captures it to a review PNG.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   Namespace: DeNelle.Editor   (Editor-only)
//
// THIS IS A DESIGN MOCKUP. No game logic, no ViewModels, no runtime wiring — it
// never ships. Its only job is to be looked at + screenshotted so the owner can
// design the battle UI by eye; the approved look is then rebuilt in the real
// code-built pipeline.
//
// ACCESSIBILITY (owner is red/green colorblind — BINDING on this mockup):
//   NO information is carried by color alone. Every ability is distinguished by
//   SHAPE (border) + SYMBOL BADGE + POSITION + TEXT LABEL, so meaning survives in
//   greyscale. The two orbs read apart by ICON + POSITION (heart-ish potion on the
//   left HP orb, sword on the right FURY orb) and use RED vs BLUE/CYAN fills
//   (red-vs-blue survives red/green colorblindness — never red-vs-orange). Text
//   fills are high-luminance for brightness/shape contrast.
//
// LAYOUT (mobile landscape, 1920x1080 design):
//   * Target nameplate top-center ("ORC BERSERKER", HP ~0.65) — composed from the
//     Obsidian nameplate sprites for a clean, controllable bar.
//   * Cast bar directly under it (CastBar1 prefab, fill ~0.6, "Arcane Bolt").
//   * Health ORB bottom-LEFT (DiabloHealth prefab, RED fill ~0.7, heart/potion badge).
//   * Fury ORB bottom-RIGHT (DiabloMana prefab, BLUE/CYAN fill ~0.4, sword badge).
//   * 5 ability slots arced above/left of the Fury orb (right-thumb reachable),
//     each = Obsidian slot frame + distinct Talent_Border shape + Knight talent
//     icon + a symbol badge + a radial cooldown wedge (dark pie = recharging).
//   * Center left empty — that's where the live 3D battle would render.
//
// CAPTURE: reuses the proven ObsidianDemoCapture technique — Obsidian UI is
// ScreenSpaceOverlay uGUI (2D sprites, Image.fillAmount bars), which renders
// through NO camera; so for capture we switch the canvas to ScreenSpaceCamera +
// a RenderTexture IN MEMORY, render, read back to PNG. Logs a clear FAIL if the
// render is empty/black (ran under -nographics instead of a graphics session).
//
// Every prefab/sprite load is guarded (null-check + Debug.LogWarning, never error)
// so one missing asset never blanks the whole mockup (no-silent-failure rule).
//
// Output scene -> Assets/Scenes/BattleHUD_Mockup.unity
// Output PNG   -> <repoRoot>/UI_REVIEW/BATTLE_HUD_MOCKUP/battle_hud_diablo_orbs.png
//                 (repo root resolved at runtime from Application.dataPath — it is
//                  MACHINE-DEPENDENT, never hardcode C:\EoA / D:\eoa)
//
// MUST RUN IN A GRAPHICS UNITY SESSION (windowed / in-editor, NOT -nographics).
// In-editor:  menu  Defenders/UI/Build Battle HUD Mockup
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeNelle.Editor
{
    /// <summary>Editor utility: builds the "Diablo orbs" battle-HUD mockup scene + review PNG.</summary>
    public static class BattleHudMockupBuilder
    {
        // ---- Asset paths (real Obsidian vendor art) --------------------------
        private const string ObsidianDir = "Assets/Blink/Art/UI/Obsidian_UI";
        private const string PrefabDir   = ObsidianDir + "/Prefabs_Obsidian";
        private const string ButtonDir   = PrefabDir + "/Buttons_Obsidian";
        private const string HudDir      = ObsidianDir + "/HUD_Obsidian";
        private const string SlotDir     = ObsidianDir + "/Slots_Obsidian";
        private const string IconDir     = ObsidianDir + "/Icons_Obsidian";
        private const string ShapeDir    = ObsidianDir + "/Shapes_Obsidian";
        private const string KnightIcons = "Assets/Resources/Talents/knight";

        private const string ScenePath = "Assets/Scenes/BattleHUD_Mockup.unity";
        // Repo root is MACHINE-DEPENDENT (C:\EoA on one box, D:\eoa on another — owner ruling
        // 2026-08-09, CLAUDE.md §0), so it is resolved at runtime from Unity's own anchor:
        // Application.dataPath == "<repoRoot>/Assets". Relative destination unchanged.
        private static string RepoRoot =>
            Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/');
        private static string OutDir => RepoRoot + "/UI_REVIEW/BATTLE_HUD_MOCKUP/";
        private static string OutPng => OutDir + "battle_hud_diablo_orbs.png";

        private const int DesignW = 1920;
        private const int DesignH = 1080;
        private const int Supersample = 2;

        // Colorblind-safe fills: RED HP (left) vs CYAN resource (right). High luminance.
        private static readonly Color HpRed   = new Color(0.86f, 0.14f, 0.14f, 1f);
        private static readonly Color FuryCyan = new Color(0.20f, 0.68f, 0.95f, 1f);
        private static readonly Color Gold      = new Color(0.86f, 0.72f, 0.36f, 1f);
        private static readonly Color TextHi    = new Color(0.97f, 0.95f, 0.88f, 1f);
        private static readonly Color Backdrop  = new Color(0.055f, 0.055f, 0.075f, 1f);

        private static TMP_FontAsset s_font;

        // ------------------------------------------------------------------
        [MenuItem("Defenders/UI/Build Battle HUD Mockup")]
        public static void Build()
        {
            s_font = ResolveFont();

            // 1. Fresh empty scene (the one we SAVE for the owner to open + edit).
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 2. Overlay HUD canvas (the real, saved render mode; capture flips it in-memory).
            var canvasGo = new GameObject("BattleHUD_Mockup_Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(DesignW, DesignH);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            var canvasRt = (RectTransform)canvasGo.transform;

            // 3. Dark full-screen backdrop (the center reads as the dark arena).
            var bd = MakeImage(canvasRt, "Arena_Backdrop", null, Vector2.zero, Vector2.zero, Backdrop);
            Stretch(bd.rectTransform);

            // 4. Build each region. Each is guarded so one failure never blanks the rest.
            SafeBuild("TargetNameplate", () => BuildTargetBar(canvasRt));
            SafeBuild("CastBar",         () => BuildCastBar(canvasRt));
            SafeBuild("HealthOrb",       () => BuildHealthOrb(canvasRt));
            SafeBuild("FuryOrb",         () => BuildFuryOrb(canvasRt));
            SafeBuild("AbilityCluster",  () => BuildAbilityCluster(canvasRt));
            SafeBuild("MockupWatermark", () => BuildWatermark(canvasRt));

            // 5. Save the scene so the owner can OPEN it and drag pieces around.
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[BattleHudMockup] scene saved={saved} -> {ScenePath}");

            // 6. Capture to review PNG (ScreenSpaceCamera + RT technique).
            CaptureToPng();

            Debug.Log($"[BattleHudMockup] DONE -> scene {ScenePath} , png {OutPng}");
        }

        // ================================================================== //
        //  REGION BUILDERS                                                    //
        // ================================================================== //

        /// <summary>Top-center enemy nameplate, composed cleanly from Obsidian sprites.</summary>
        private static void BuildTargetBar(RectTransform canvas)
        {
            var root = MakeChild(canvas, "TargetBar", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -70f), new Vector2(640f, 120f));

            // Backing frame (nameplate bar art) — stretched to fill the region.
            var frame = MakeImage(root, "Frame", LoadSprite($"{HudDir}/Nameplate_Enemy_Background.png"),
                new Vector2(640f, 120f), Vector2.zero, Color.white);
            Stretch(frame.rectTransform);

            // Enemy HP fill bar (~0.65). FillMethod horizontal so it reads as a bar.
            var barBg = MakeImage(root, "HpBarBg", LoadSprite($"{HudDir}/Nameplate_Bar.png"),
                new Vector2(560f, 34f), new Vector2(0f, -24f), new Color(0.12f, 0.12f, 0.14f, 1f));
            var barBgRt = barBg.rectTransform;
            var fill = MakeImage(barBgRt, "HpFill", LoadSprite($"{HudDir}/Nameplate_Health_Enemy.png"),
                Vector2.zero, Vector2.zero, HpRed);
            Stretch(fill.rectTransform);
            SetFill(fill, Image.FillMethod.Horizontal, 0.65f);

            // Name.
            MakeText(root, "Name", "ORC BERSERKER", 46, new Vector2(0f, 30f),
                new Vector2(600f, 60f), TextHi, TextAlignmentOptions.Center, FontStyles.Bold);
            // HP readout (brightness/shape, not hue).
            MakeText(barBgRt, "HpText", "1,430 / 2,200", 26, Vector2.zero,
                new Vector2(560f, 34f), TextHi, TextAlignmentOptions.Center, FontStyles.Bold);
        }

        /// <summary>Cast bar just under the target bar (real CastBar1 prefab, fill ~0.6).</summary>
        private static void BuildCastBar(RectTransform canvas)
        {
            var root = MakeChild(canvas, "CastBarGroup", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -196f), new Vector2(560f, 70f));

            var inst = InstantiatePrefab($"{PrefabDir}/CastBar1.prefab", root, "CastBar1");
            if (inst != null)
            {
                var rt = (RectTransform)inst.transform;
                rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.localScale = Vector3.one;
                // First filled child = the progress fill.
                var castFill = FindFillImage(inst);
                if (castFill != null) SetFill(castFill, castFill.fillMethod, 0.6f);
                else Debug.LogWarning("[BattleHudMockup] CastBar1 fill image not found — leaving as authored");
            }

            MakeText(root, "SpellName", "Arcane Bolt", 34, new Vector2(0f, 0f),
                new Vector2(520f, 50f), TextHi, TextAlignmentOptions.Center, FontStyles.Bold);
            MakeText(root, "CastLabel", "CASTING", 22, new Vector2(0f, 34f),
                new Vector2(520f, 30f), Gold, TextAlignmentOptions.Center, FontStyles.Normal);
        }

        /// <summary>Bottom-LEFT health orb (DiabloHealth prefab, RED fill ~0.7 + potion/heart badge).</summary>
        private static void BuildHealthOrb(RectTransform canvas)
        {
            var root = MakeChild(canvas, "HealthOrb", new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(160f, 150f), new Vector2(270f, 270f));

            var inst = InstantiatePrefab($"{ButtonDir}/DiabloHealth.prefab", root, "DiabloHealth");
            if (inst != null)
            {
                CenterOrb(inst, 270f);
                var fill = FindFillImage(inst);
                if (fill != null) { fill.color = HpRed; SetFill(fill, fill.fillMethod, 0.7f); }
                else Debug.LogWarning("[BattleHudMockup] DiabloHealth fill image not found");
            }

            // Icon badge (potion reads as health) — distinguishes by SYMBOL, not hue.
            OrbBadge(root, "HpBadge", LoadSprite($"{IconDir}/Health_Potion.png"));
            // Labels (brightness/shape).
            MakeText(root, "HpLabel", "HEALTH", 30, new Vector2(0f, -150f),
                new Vector2(280f, 40f), TextHi, TextAlignmentOptions.Center, FontStyles.Bold);
            MakeText(root, "HpValue", "70%", 40, new Vector2(0f, 0f),
                new Vector2(200f, 60f), TextHi, TextAlignmentOptions.Center, FontStyles.Bold);
        }

        /// <summary>Bottom-RIGHT fury orb (DiabloMana prefab, CYAN fill ~0.4 + sword badge).</summary>
        private static void BuildFuryOrb(RectTransform canvas)
        {
            var root = MakeChild(canvas, "FuryOrb", new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-160f, 150f), new Vector2(270f, 270f));

            var inst = InstantiatePrefab($"{ButtonDir}/DiabloMana.prefab", root, "DiabloMana");
            if (inst != null)
            {
                CenterOrb(inst, 270f);
                var fill = FindFillImage(inst);
                if (fill != null) { fill.color = FuryCyan; SetFill(fill, fill.fillMethod, 0.4f); }
                else Debug.LogWarning("[BattleHudMockup] DiabloMana fill image not found");
            }

            OrbBadge(root, "FuryBadge", LoadSprite($"{IconDir}/Sword.png"));
            MakeText(root, "FuryLabel", "FURY", 30, new Vector2(0f, -150f),
                new Vector2(280f, 40f), TextHi, TextAlignmentOptions.Center, FontStyles.Bold);
            MakeText(root, "FuryValue", "40%", 40, new Vector2(0f, 0f),
                new Vector2(200f, 60f), TextHi, TextAlignmentOptions.Center, FontStyles.Bold);
        }

        // One ability slot's design data. Meaning is carried by SHAPE(border)+BADGE+LABEL+POSITION.
        private struct AbilityDef
        {
            public string label;      // text under the slot
            public string iconPath;   // main ability art (Knight talent icon)
            public string badgePath;  // symbol badge (shape/role signal)
            public string borderPath; // distinct frame-shape border
            public Color rimTint;     // SECONDARY decoration only
            public float cooldown;    // 0 = READY, else fraction of wedge remaining
            public Vector2 pos;       // anchoredPosition from bottom-right
        }

        /// <summary>5 ability slots arced above/left of the Fury orb (right-thumb reach).</summary>
        private static void BuildAbilityCluster(RectTransform canvas)
        {
            var cluster = MakeChild(canvas, "AbilityCluster", new Vector2(1f, 0f), new Vector2(1f, 0f),
                Vector2.zero, new Vector2(10f, 10f));

            var defs = new[]
            {
                new AbilityDef { label = "HEAL",    iconPath = Knight(5),  badgePath = $"{IconDir}/Health_Potion.png",
                                 borderPath = $"{SlotDir}/Talent_Border_1.png", rimTint = new Color(0.45f,0.85f,0.55f,1f),
                                 cooldown = 0f,   pos = new Vector2(-147f, 575f) },
                new AbilityDef { label = "ARCANE BOLT", iconPath = Knight(9), badgePath = $"{IconDir}/Rune_2.png",
                                 borderPath = $"{SlotDir}/Talent_Border_2.png", rimTint = new Color(0.40f,0.70f,0.95f,1f),
                                 cooldown = 0f,   pos = new Vector2(-243f, 543f) },
                new AbilityDef { label = "SHIELD BASH", iconPath = Knight(2), badgePath = $"{IconDir}/Sword.png",
                                 borderPath = $"{SlotDir}/Talent_Border_3.png", rimTint = new Color(0.90f,0.55f,0.30f,1f),
                                 cooldown = 0.5f, pos = new Vector2(-323f, 483f) },
                new AbilityDef { label = "BULWARK CHARGE", iconPath = Knight(3), badgePath = $"{IconDir}/Sword_1.png",
                                 borderPath = $"{SlotDir}/Talent_Border_4.png", rimTint = new Color(0.90f,0.55f,0.30f,1f),
                                 cooldown = 0f,   pos = new Vector2(-383f, 402f) },
                new AbilityDef { label = "IRON RESOLVE", iconPath = Knight(7), badgePath = $"{IconDir}/Helmet.png",
                                 borderPath = $"{SlotDir}/Talent_Border_5.png", rimTint = new Color(0.55f,0.80f,0.85f,1f),
                                 cooldown = 0.8f, pos = new Vector2(-415f, 307f) },
            };

            // A small "CAST" affordance above the arc (uses a rounded button sprite).
            var castBtn = MakeChild(cluster, "CastButton", new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-120f, 620f), new Vector2(150f, 64f));
            MakeImage(castBtn, "Bg", LoadSprite($"{ObsidianDir}/Buttons_Obsidian/Button2_Yellow.png"),
                new Vector2(150f, 64f), Vector2.zero, Color.white);
            MakeText(castBtn, "Label", "CAST", 30, Vector2.zero, new Vector2(150f, 64f),
                Color.black, TextAlignmentOptions.Center, FontStyles.Bold);

            for (int i = 0; i < defs.Length; i++)
                BuildAbilitySlot(cluster, defs[i], i);
        }

        private static void BuildAbilitySlot(RectTransform cluster, AbilityDef def, int index)
        {
            const float slot = 132f;
            var root = MakeChild(cluster, $"Slot{index}_{def.label.Replace(' ', '_')}",
                new Vector2(1f, 0f), new Vector2(1f, 0f), def.pos, new Vector2(slot, slot));

            // Rim border (distinct SHAPE per ability). Tint is secondary decoration only.
            var border = LoadSprite(def.borderPath);
            MakeImage(root, "Rim", border, new Vector2(slot + 20f, slot + 20f), Vector2.zero, def.rimTint);

            // Slot frame.
            MakeImage(root, "Frame", LoadSprite($"{SlotDir}/Action_Bar_Slot.png"),
                new Vector2(slot, slot), Vector2.zero, Color.white);

            // Main ability icon (Knight talent art). Fallback = colored square handled in MakeImage/LoadSprite.
            var icon = LoadSprite(def.iconPath);
            MakeImage(root, "Icon", icon, new Vector2(slot - 30f, slot - 30f), Vector2.zero,
                icon != null ? Color.white : new Color(0.4f, 0.4f, 0.45f, 1f));
            if (icon == null)
                MakeText(root, "IconFallback", def.label, 18, Vector2.zero, new Vector2(slot - 20f, slot - 20f),
                    TextHi, TextAlignmentOptions.Center, FontStyles.Bold);

            // Cooldown wedge — dark radial pie over the icon (reads as "recharging" in greyscale).
            if (def.cooldown > 0.001f)
            {
                var wedge = MakeImage(root, "CooldownWedge", LoadSprite($"{ShapeDir}/Circle.png"),
                    new Vector2(slot - 28f, slot - 28f), Vector2.zero, new Color(0f, 0f, 0f, 0.72f));
                wedge.type = Image.Type.Filled;
                wedge.fillMethod = Image.FillMethod.Radial360;
                wedge.fillOrigin = (int)Image.Origin360.Top;
                wedge.fillClockwise = false;
                wedge.fillAmount = def.cooldown;
                // Remaining-cooldown seconds (text, not hue).
                MakeText(root, "CdText", Mathf.CeilToInt(def.cooldown * 12f) + "s", 34, Vector2.zero,
                    new Vector2(slot, slot), TextHi, TextAlignmentOptions.Center, FontStyles.Bold);
            }
            else
            {
                // READY marker (text, high luminance).
                MakeText(root, "Ready", "READY", 18, new Vector2(0f, -8f), new Vector2(slot, 30f),
                    Gold, TextAlignmentOptions.Center, FontStyles.Bold);
            }

            // Symbol badge, top-left corner, on a dark disc for contrast (SHAPE/role signal).
            var badgeHolder = MakeChild(root, "Badge", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-slot * 0.42f, slot * 0.42f), new Vector2(44f, 44f));
            MakeImage(badgeHolder, "Disc", LoadSprite($"{ShapeDir}/Circle.png"),
                new Vector2(44f, 44f), Vector2.zero, new Color(0.06f, 0.06f, 0.08f, 0.92f));
            var badgeSprite = LoadSprite(def.badgePath);
            if (badgeSprite != null)
                MakeImage(badgeHolder, "Symbol", badgeSprite, new Vector2(34f, 34f), Vector2.zero, TextHi);

            // Ability name under the slot (always readable, carries meaning in greyscale).
            MakeText(root, "Label", def.label, 24, new Vector2(0f, -(slot * 0.5f) - 26f),
                new Vector2(slot + 90f, 34f), TextHi, TextAlignmentOptions.Center, FontStyles.Bold);
        }

        private static void BuildWatermark(RectTransform canvas)
        {
            var tmp = MakeText(canvas, "MockupWatermark", "VISUAL MOCKUP — design reference only, not wired", 22,
                Vector2.zero, new Vector2(900f, 30f), new Color(0.6f, 0.6f, 0.66f, 1f),
                TextAlignmentOptions.Center, FontStyles.Italic);
            var rt = tmp.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 12f);
        }

        // ================================================================== //
        //  UI PRIMITIVES (guarded)                                            //
        // ================================================================== //

        private static string Knight(int n) => $"{KnightIcons}/knight_{n:00}.png";

        /// <summary>Stretch a RectTransform to fully fill its parent.</summary>
        private static void Stretch(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static RectTransform MakeChild(RectTransform parent, string name, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            rt.localScale = Vector3.one;
            return rt;
        }

        private static Image MakeImage(RectTransform parent, string name, Sprite sprite,
            Vector2 size, Vector2 anchoredPos, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            rt.localScale = Vector3.one;
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            if (sprite != null) img.preserveAspect = true;
            return img;
        }

        private static TextMeshProUGUI MakeText(RectTransform parent, string name, string text, float size,
            Vector2 anchoredPos, Vector2 rect, Color color, TextAlignmentOptions align, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = rect;
            rt.anchoredPosition = anchoredPos;
            rt.localScale = Vector3.one;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (s_font != null) tmp.font = s_font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.fontStyle = style;
            tmp.enableWordWrapping = false;
            tmp.raycastTarget = false;
            // High-luminance outline so text reads by brightness/shape on any backdrop.
            tmp.outlineWidth = 0.18f;
            tmp.outlineColor = new Color32(0, 0, 0, 220);
            return tmp;
        }

        /// <summary>Small icon badge centered inside an orb (symbol, not hue).</summary>
        private static void OrbBadge(RectTransform orbRoot, string name, Sprite sprite)
        {
            if (sprite == null) { Debug.LogWarning($"[BattleHudMockup] orb badge '{name}' sprite missing — skipped"); return; }
            var holder = MakeChild(orbRoot, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 62f), new Vector2(60f, 60f));
            MakeImage(holder, "Disc", LoadSprite($"{ShapeDir}/Circle.png"),
                new Vector2(60f, 60f), Vector2.zero, new Color(0.06f, 0.06f, 0.08f, 0.85f));
            MakeImage(holder, "Symbol", sprite, new Vector2(46f, 46f), Vector2.zero, TextHi);
        }

        private static void CenterOrb(GameObject inst, float size)
        {
            var rt = inst.transform as RectTransform;
            if (rt == null) return;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(size, size);
            rt.localScale = Vector3.one;
        }

        private static void SetFill(Image img, Image.FillMethod method, float amount)
        {
            if (img == null) return;
            img.type = Image.Type.Filled;
            img.fillMethod = method;
            img.fillAmount = Mathf.Clamp01(amount);
        }

        /// <summary>First descendant Image whose type is Filled (the bar/orb fill).</summary>
        private static Image FindFillImage(GameObject root)
        {
            if (root == null) return null;
            var imgs = root.GetComponentsInChildren<Image>(true);
            // Prefer a name hint, else the first Filled image.
            foreach (var im in imgs)
                if (im != null && im.type == Image.Type.Filled &&
                    (im.name.IndexOf("Fill", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     im.name.IndexOf("Bar", StringComparison.OrdinalIgnoreCase) >= 0))
                    return im;
            foreach (var im in imgs)
                if (im != null && im.type == Image.Type.Filled)
                    return im;
            return null;
        }

        private static GameObject InstantiatePrefab(string path, RectTransform parent, string label)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[BattleHudMockup] prefab missing '{label}' at {path} — skipped");
                return null;
            }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (inst == null)
                Debug.LogWarning($"[BattleHudMockup] could not instantiate '{label}' at {path}");
            return inst;
        }

        private static Sprite LoadSprite(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp == null)
            {
                // Fallback: first Sprite sub-asset (some PNGs expose the sprite as a sub-asset).
                var subs = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (var s in subs)
                    if (s is Sprite spr) { sp = spr; break; }
            }
            if (sp == null)
                Debug.LogWarning($"[BattleHudMockup] sprite missing at {path} — placeholder used");
            return sp;
        }

        private static TMP_FontAsset ResolveFont()
        {
            try
            {
                // Prefer an Obsidian-shipped TMP font, else any TMP font, else the TMP default.
                var guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
                string best = null;
                foreach (var g in guids)
                {
                    var p = AssetDatabase.GUIDToAssetPath(g);
                    if (string.IsNullOrEmpty(p)) continue;
                    if (p.Replace('\\', '/').Contains("Obsidian")) { best = p; break; }
                    if (best == null) best = p;
                }
                if (best != null)
                {
                    var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(best);
                    if (f != null) { Debug.Log($"[BattleHudMockup] font: {best}"); return f; }
                }
            }
            catch (Exception e) { Debug.LogWarning($"[BattleHudMockup] font resolve failed: {e.Message}"); }
            var def = TMP_Settings.defaultFontAsset;
            if (def == null) Debug.LogWarning("[BattleHudMockup] no TMP font found — TMP will use its runtime default");
            return def;
        }

        private static void SafeBuild(string region, Action a)
        {
            try { a(); }
            catch (Exception e) { Debug.LogWarning($"[BattleHudMockup] region '{region}' failed: {e.Message}\n{e.StackTrace}"); }
        }

        // ================================================================== //
        //  CAPTURE  (ScreenSpaceCamera + RenderTexture — proven technique)    //
        // ================================================================== //

        private static void CaptureToPng()
        {
            Directory.CreateDirectory(OutDir);

            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (canvases == null || canvases.Length == 0)
            {
                Debug.LogError("BATTLE_HUD_MOCKUP_FAIL no active Canvas — nothing to render.");
                return;
            }

            int w = DesignW * Supersample;
            int h = DesignH * Supersample;

            var camGo = new GameObject("__BattleHudCaptureCam");
            var cam = camGo.AddComponent<Camera>();
            camGo.transform.position = new Vector3(0f, 0f, -100f);
            camGo.transform.rotation = Quaternion.identity;
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Backdrop;
            cam.cullingMask = ~0;
            var urpDataType = Type.GetType(
                "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (urpDataType != null && camGo.GetComponent(urpDataType) == null)
                camGo.AddComponent(urpDataType);

            var restore = new List<(Canvas c, RenderMode mode, Camera worldCam, float plane)>();
            foreach (var c in canvases)
            {
                restore.Add((c, c.renderMode, c.worldCamera, c.planeDistance));
                if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    c.renderMode = RenderMode.ScreenSpaceCamera;
                    c.worldCamera = cam;
                    c.planeDistance = 10f;
                }
                else if (c.renderMode == RenderMode.ScreenSpaceCamera && c.worldCamera == null)
                {
                    c.worldCamera = cam;
                    c.planeDistance = 10f;
                }
            }

            Canvas.ForceUpdateCanvases();

            var rtex = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            rtex.Create();
            var prevActive = RenderTexture.active;
            Texture2D full = null;
            byte[] png = null;
            try
            {
                cam.targetTexture = rtex;
                Canvas.ForceUpdateCanvases();
                cam.Render();
                cam.Render(); // URP warm-up pass
                RenderTexture.active = rtex;
                full = new Texture2D(w, h, TextureFormat.RGB24, false);
                full.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                full.Apply();
                png = full.EncodeToPNG();
            }
            finally
            {
                cam.targetTexture = null;
                RenderTexture.active = prevActive;
            }

            if (png != null && png.Length > 0)
            {
                try
                {
                    File.WriteAllBytes(OutPng, png);
                    Debug.Log($"BATTLE_HUD_MOCKUP_OK {OutPng} ({png.Length} bytes) {w}x{h}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"BATTLE_HUD_MOCKUP_FAIL could not write {OutPng}: {e.Message}");
                }
            }
            else
            {
                Debug.LogError("BATTLE_HUD_MOCKUP_FAIL EncodeToPNG produced no data (black/empty render? " +
                               "run in a GRAPHICS session, not -nographics).");
            }

            if (full != null && IsLikelyBlank(full))
                Debug.LogWarning("BATTLE_HUD_MOCKUP_WARN render looks near-empty — if black, the UI did NOT " +
                                 "render; re-run in a WINDOWED graphics session (in-editor is ideal).");

            // Restore canvases (belt-and-braces; the SAVED scene already has overlay mode).
            foreach (var r in restore)
            {
                if (r.c == null) continue;
                r.c.renderMode = r.mode;
                r.c.worldCamera = r.worldCam;
                r.c.planeDistance = r.plane;
            }

            if (full != null) UnityEngine.Object.DestroyImmediate(full);
            rtex.Release();
            UnityEngine.Object.DestroyImmediate(rtex);
            UnityEngine.Object.DestroyImmediate(camGo);
        }

        private static bool IsLikelyBlank(Texture2D tex)
        {
            int hits = 0, samples = 0;
            for (int x = 0; x < tex.width; x += Mathf.Max(1, tex.width / 32))
                for (int y = 0; y < tex.height; y += Mathf.Max(1, tex.height / 32))
                {
                    samples++;
                    Color c = tex.GetPixel(x, y);
                    if (c.r < 0.12f && c.g < 0.12f && c.b < 0.14f) hits++;
                }
            return samples > 0 && hits >= samples - 2;
        }
    }
}
