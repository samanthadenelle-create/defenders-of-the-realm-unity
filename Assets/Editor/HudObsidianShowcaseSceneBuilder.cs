// =============================================================================
// HudObsidianShowcaseSceneBuilder — DUMMY showcase of the Blink Obsidian HUD
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   Namespace: DeNelle.Editor   (Editor-only)
//
// Builds a NEW, throwaway showcase scene (Assets/Scenes/HUD_Obsidian_Showcase.unity)
// that renders every Obsidian HUD widget in isolation, populated with FAKE data, so
// the owner can SEE the full-depth black+gold HUD without launching the game — and so
// we can read WHICH widgets load the REAL mirrored Blink prefab vs fall back to the
// code-built ("procedural") mode.
//
// It does NOT reinvent any widget: every component is produced by the EXISTING kit
// builders (ElarionUiKit.* from DeNelle.Core.UI — loader-first, so the real prefab
// depth shows) or, for the composed core, by raw-instantiating the mirrored
// Resources/RpgUi/prefabs/*.prefab. The single genuinely-missing widget (buffs/
// debuffs — no builder, no Player_States prefab mirrored) is assembled here from
// existing kit plate + icon primitives and LOGGED as a depth gap.
//
// LOAD-vs-FALLBACK LOGGING: each kit widget is built under its own container; we then
// look for the kit's ElarionUiKit.UiKitPrefabBinder marker (stamped only on prefab-
// mode instances) in that container. Present => REAL prefab (marker.sourcePrefab);
// absent => CONSTRUCTED fallback. Every result is FlowTrace.Step'd + Debug.Log'd and
// a summary block is printed at the end. (The builders' own FlowTrace.Warn lines —
// e.g. "prefab has no *fill* child" — remain the authoritative gap signal.)
//
// Run (Unity editor CLOSED, batchmode):
//   -executeMethod DeNelle.Editor.HudObsidianShowcaseSceneBuilder.Build
// or in-editor: menu  Defenders/UI/Build Obsidian HUD Showcase
// Then open Assets/Scenes/HUD_Obsidian_Showcase.unity to view.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Editor
{
    /// <summary>Editor utility: generates the isolated Obsidian-HUD showcase scene.</summary>
    public static class HudObsidianShowcaseSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/HUD_Obsidian_Showcase.unity";

        // Portrait project standard (ElarionUiKit.BuildModalCanvas / HudAreasHost).
        private static readonly Vector2 RefRes = new Vector2(1080f, 1920f);

        // Collected load-vs-fallback verdicts, printed as one block at the end.
        private static readonly List<string> _verdicts = new List<string>();

        [MenuItem("Defenders/UI/Build Obsidian HUD Showcase")]
        public static void Build()
        {
            FlowTrace.Enabled = true;
            _verdicts.Clear();
            FlowTrace.Step("HudShowcase", "=== Building HUD_Obsidian_Showcase ===");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── Camera (dark, so the black+gold chrome reads) ────────────────
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.06f, 0.08f, 1f);
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0f, 1f, -10f);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            CreateEventSystem();

            // ── Canvas (ScreenSpaceOverlay, 1080x1920 ref, match 0.5) ────────
            var canvasGo = new GameObject("ShowcaseCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = RefRes;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            var canvasT = canvasGo.transform;

            // Dark full-screen backdrop so the chrome reads even where a widget is sparse.
            var bg = ElarionUiKit.AddImage(canvasT, "Backdrop", Vector2.zero, Vector2.one,
                new Color(0.05f, 0.05f, 0.07f, 1f), rounded: false);
            bg.GetComponent<Image>().raycastTarget = false;

            ElarionUiKit.Label(canvasT, "OBSIDIAN HUD SHOWCASE — fake data", 0.955f, 0.995f,
                ElarionUi.Gilt, 34, TextAlignmentOptions.Center, 0.05f, 0.95f, bold: true);

            // ─────────────────────────────────────────────────────────────────
            // 1. PLAYER STAT BARS (health / energy / stamina / exp)
            // ─────────────────────────────────────────────────────────────────
            BuildPlayerBars(canvasT);

            // ─────────────────────────────────────────────────────────────────
            // 2. HUDCore composed prefab (raw-instantiated, if mirrored)
            // ─────────────────────────────────────────────────────────────────
            BuildRawHudCore(canvasT);

            // ─────────────────────────────────────────────────────────────────
            // 3. TARGET NAMEPLATE + boss variant
            // ─────────────────────────────────────────────────────────────────
            BuildTargetSection(canvasT);

            // ─────────────────────────────────────────────────────────────────
            // 4. BUFFS / DEBUFFS  (no kit widget — showcase-assembled, GAP logged)
            // ─────────────────────────────────────────────────────────────────
            BuildBuffsDebuffs(canvasT);

            // ─────────────────────────────────────────────────────────────────
            // 5. ACTION BAR (row of Action_Bar_Slots + keybinds + cooldown/stack)
            // ─────────────────────────────────────────────────────────────────
            BuildActionBar(canvasT);

            // ─────────────────────────────────────────────────────────────────
            // 6. CAST BAR (Fireball mid-cast)
            // ─────────────────────────────────────────────────────────────────
            BuildCastBarSection(canvasT);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            // ── Print the load-vs-fallback ledger ────────────────────────────
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[HudShowcase] ===== LOAD vs FALLBACK LEDGER =====");
            foreach (var v in _verdicts) sb.AppendLine("  " + v);
            sb.AppendLine("[HudShowcase] Scene saved: " + ScenePath);
            Debug.Log(sb.ToString());
            FlowTrace.Step("HudShowcase", "=== DONE — " + _verdicts.Count + " widgets placed ===");
        }

        // =====================================================================
        //  Sections
        // =====================================================================

        private static void BuildPlayerBars(Transform canvasT)
        {
            var content = Section(canvasT, "Player stat bars  (BuildObsidianBar)",
                new Vector2(0.05f, 0.80f), new Vector2(0.62f, 0.945f));

            // Four stacked bars filling the content, each in its own row container.
            var health  = ElarionUiKit.BuildObsidianBar(content, ElarionUiKit.ObsidianBarKind.Health,
                new Vector2(0f, 0.78f), new Vector2(1f, 1.0f), withValue: true);
            health.SetImmediate(720f, 1000f);
            Verdict("PlayerBar/Health", content, "Bar1..7 (candidate 'HealthBar' won't match mirrored names)");

            var energy  = ElarionUiKit.BuildObsidianBar(content, ElarionUiKit.ObsidianBarKind.Energy,
                new Vector2(0f, 0.52f), new Vector2(1f, 0.74f), withValue: true);
            energy.SetImmediate(40f, 100f);

            var stamina = ElarionUiKit.BuildObsidianBar(content, ElarionUiKit.ObsidianBarKind.Stamina,
                new Vector2(0f, 0.26f), new Vector2(1f, 0.48f), withValue: true);
            stamina.SetImmediate(90f, 100f);

            var xp      = ElarionUiKit.BuildObsidianBar(content, ElarionUiKit.ObsidianBarKind.Xp,
                new Vector2(0f, 0.0f), new Vector2(1f, 0.22f), withValue: true);
            xp.SetImmediate(5500f, 10000f);
        }

        private static void BuildRawHudCore(Transform canvasT)
        {
            var content = Section(canvasT, "HUDCore.prefab (raw)",
                new Vector2(0.64f, 0.80f), new Vector2(0.96f, 0.945f));

            var pf = Resources.Load<GameObject>(RpgUiCatalog.PrefabRoot + "HUDCore");
            if (pf != null)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(pf, content);
                var rt = inst.transform as RectTransform ?? inst.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one;
                _verdicts.Add("HUDCore (raw)          : REAL prefab  (Resources/RpgUi/prefabs/HUDCore)");
                FlowTrace.Step("HudShowcase", "HUDCore raw prefab instantiated");
            }
            else
            {
                ElarionUiKit.Label(content, "HUDCore.prefab MISSING", 0.4f, 0.6f,
                    ElarionUi.HpRed, 22, TextAlignmentOptions.Center);
                _verdicts.Add("HUDCore (raw)          : MISSING     (Resources/RpgUi/prefabs/HUDCore not found)");
                FlowTrace.Warn("HudShowcase", "HUDCore.prefab not mirrored");
            }
        }

        private static void BuildTargetSection(Transform canvasT)
        {
            var content = Section(canvasT, "Target nameplate  (BuildTargetFrame)",
                new Vector2(0.05f, 0.62f), new Vector2(0.50f, 0.775f));
            var tf = ElarionUiKit.BuildTargetFrame(content, Vector2.zero, Vector2.one);
            tf.Set("Grommash", "ELITE", 360f, 600f, "LOCKED");   // WO-1232: badge slot = authored word
            Verdict("TargetFrame", content, "TargetNameplate.prefab");

            var bossContent = Section(canvasT, "Boss nameplate  (BuildNameplate.Boss)",
                new Vector2(0.52f, 0.62f), new Vector2(0.96f, 0.775f));
            var boss = ElarionUiKit.BuildNameplate(bossContent, ElarionUiKit.NameplateKind.Boss,
                Vector2.zero, Vector2.one);
            boss.SetName("Grommash, the Ruin");
            if (boss.hp != null) boss.hp.SetImmediate(0.6f, 1f);
            Verdict("Nameplate/Boss", bossContent, "EnemyNameplate.prefab (candidate) else constructed");
        }

        private static void BuildBuffsDebuffs(Transform canvasT)
        {
            var content = Section(canvasT, "Buffs / Debuffs  (NO KIT WIDGET — showcase-assembled, see report)",
                new Vector2(0.05f, 0.475f), new Vector2(0.96f, 0.605f));

            // There is no ElarionUiKit buff/debuff builder and no Player_States prefab mirrored.
            // Assemble showcase pips from EXISTING kit primitives (element_stat plate + our icons +
            // a stack/duration label) so the owner sees the intended depth — and LOG it as a gap.
            string[] buffIcons  = { RpgUiCatalog.IconShield, RpgUiCatalog.IconHeart, RpgUiCatalog.IconSword };
            string[] buffDur    = { "12s", "8s", "3" };
            string[] debuffIcons = { RpgUiCatalog.IconCombat, RpgUiCatalog.IconQuest };
            string[] debuffDur  = { "5s", "2" };

            ElarionUiKit.Label(content, "BUFFS", 0.80f, 1.0f, ElarionUi.Gilt, 16,
                TextAlignmentOptions.MidlineLeft, 0.0f, 0.2f, bold: true);
            for (int i = 0; i < buffIcons.Length; i++)
                BuildStatusPip(content, buffIcons[i], buffDur[i], new Color(0.35f, 0.78f, 0.35f, 1f),
                    0.02f + i * 0.11f, 0.42f, 0.80f);

            ElarionUiKit.Label(content, "DEBUFFS", 0.30f, 0.5f, ElarionUi.Gilt, 16,
                TextAlignmentOptions.MidlineLeft, 0.0f, 0.2f, bold: true);
            for (int i = 0; i < debuffIcons.Length; i++)
                BuildStatusPip(content, debuffIcons[i], debuffDur[i], new Color(0.85f, 0.32f, 0.28f, 1f),
                    0.02f + i * 0.11f, 0.42f, 0.30f);

            _verdicts.Add("Buffs/Debuffs          : GAP — no kit builder & no Player_States prefab mirrored; " +
                          "showcase-assembled from element_stat plate + icons. NEEDS ART IMPORT + a builder.");
            FlowTrace.Warn("HudShowcase", "Buffs/Debuffs: no widget/prefab — assembled from primitives (DEPTH GAP)");
        }

        // One status pip: element_stat plate + icon + duration/stack label (rimmed by tone).
        private static void BuildStatusPip(Transform parent, string iconName, string dur, Color tone,
            float x0, float xW, float yTop)
        {
            var plate = ElarionUiKit.CurrencyChip(parent, ElarionUiKit.CurrencyKind.Wisdom,
                new Vector2(x0, yTop - 0.36f), new Vector2(x0 + xW, yTop));
            if (plate.plate != null) plate.plate.color = new Color(tone.r, tone.g, tone.b, 0.55f);
            if (plate.amount != null) plate.amount.text = "";     // reuse chip plate, replace content
            // Icon over the plate.
            var iconGo = new GameObject("PipIcon", typeof(Image));
            iconGo.transform.SetParent(plate.root.transform, false);
            var irt = (RectTransform)iconGo.transform;
            irt.anchorMin = new Vector2(0.06f, 0.18f); irt.anchorMax = new Vector2(0.52f, 0.90f);
            irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
            var iconImg = iconGo.GetComponent<Image>();
            iconImg.raycastTarget = false;
            iconImg.preserveAspect = true;
            var spr = RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, iconName);
            if (spr != null) iconImg.sprite = spr; else iconGo.SetActive(false);
            // Duration / stack.
            var lbl = ElarionUiKit.Label(plate.root.transform, dur, 0.02f, 0.5f, ElarionUi.Parchment,
                18, TextAlignmentOptions.BottomRight, 0.4f, 0.94f, bold: true);
            lbl.raycastTarget = false;
        }

        private static void BuildActionBar(Transform canvasT)
        {
            var content = Section(canvasT, "Action bar  (BuildActionSlot + keybinds + cooldown/stack)",
                new Vector2(0.05f, 0.31f), new Vector2(0.96f, 0.44f));

            string[] icons = { RpgUiCatalog.IconSword, RpgUiCatalog.IconShield, RpgUiCatalog.IconCombat,
                               RpgUiCatalog.IconHeart, RpgUiCatalog.IconQuest };
            string[] keys  = { "1", "2", "3", "4", "5" };
            int n = icons.Length;
            float gap = 0.02f;
            float w = (1f - gap * (n - 1)) / n;

            for (int i = 0; i < n; i++)
            {
                float x0 = i * (w + gap);
                var slot = ElarionUiKit.BuildActionSlot(content,
                    new Vector2(x0, 0.05f), new Vector2(x0 + w, 0.95f));
                var spr = RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, icons[i]);
                if (spr != null) slot.SetIcon(spr);

                // Keybind badge (top-left of the slot) — showcase decoration, not a widget rewrite.
                var kb = ElarionUiKit.Label(slot.root.transform, keys[i], 0.70f, 0.98f,
                    ElarionUi.Gilt, 18, TextAlignmentOptions.TopLeft, 0.06f, 0.5f, bold: true);
                kb.raycastTarget = false;

                if (i == 2) slot.SetCooldown(3f, 8f);   // mid-cooldown sweep + "3"
                if (i == 4) slot.SetCount(3);           // charge stack
            }
            _verdicts.Add("ActionSlot x5          : " +
                (content.GetComponentInChildren<ElarionUiKit.UiKitPrefabBinder>(true) != null
                    ? "REAL prefab (Action_Bar_Slot)"
                    : "CONSTRUCTED (no Action_Bar_Slot.prefab mirrored — built on slot_action sprite)"));
        }

        private static void BuildCastBarSection(Transform canvasT)
        {
            var content = Section(canvasT, "Cast bar  (BuildCastBar — Fireball mid-cast)",
                new Vector2(0.15f, 0.20f), new Vector2(0.85f, 0.28f));
            var cast = ElarionUiKit.BuildCastBar(content, 1, Vector2.zero, Vector2.one);
            cast.SetCast("Fireball   1.2s", 0.65f);
            if (cast.root != null) cast.root.SetActive(true);
            Verdict("CastBar", content, "CastBar1.prefab");
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        /// <summary>A captioned section container. Returns the CONTENT RectTransform (below the
        /// caption strip) that a widget fills with anchorMin(0,0)/anchorMax(1,1).</summary>
        private static RectTransform Section(Transform canvasT, string caption, Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject("Section", typeof(RectTransform));
            go.transform.SetParent(canvasT, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var cap = ElarionUiKit.Label(go.transform, caption, 0.86f, 1.0f,
                ElarionUi.ParchmentDim, 18, TextAlignmentOptions.MidlineLeft, 0.0f, 1.0f, bold: true);
            cap.raycastTarget = false;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(go.transform, false);
            var crt = (RectTransform)content.transform;
            crt.anchorMin = new Vector2(0f, 0f); crt.anchorMax = new Vector2(1f, 0.83f);
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
            return crt;
        }

        /// <summary>Record whether the kit widget built under <paramref name="container"/> loaded the
        /// REAL Blink prefab (UiKitPrefabBinder marker present) or fell to the CONSTRUCTED mode.</summary>
        private static void Verdict(string label, Transform container, string expectedPrefab)
        {
            var binder = container.GetComponentInChildren<ElarionUiKit.UiKitPrefabBinder>(true);
            string line = label.PadRight(22) + " : " + (binder != null
                ? "REAL prefab  (" + binder.sourcePrefab + ")"
                : "CONSTRUCTED  (fallback; expected " + expectedPrefab + ")");
            _verdicts.Add(line);
            FlowTrace.Step("HudShowcase", line);
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            var moduleType =
                Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem") ??
                Type.GetType("UnityEngine.EventSystems.StandaloneInputModule, UnityEngine.UI");
            if (moduleType != null) go.AddComponent(moduleType);
        }
    }
}
