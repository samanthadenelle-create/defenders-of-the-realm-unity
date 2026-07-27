// =============================================================================
// ElarionUiKit — OBSIDIAN WIDGET FAMILY (HUD_OBSIDIAN_ARCHITECTURE_2026-07-03 §1)
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.UI   (partial of ElarionUiKit)
// SINGLE WRITER: the P1 Kit/Factory team. Consumers call these builders only —
// per-screen widget construction is a review-blocking violation (§5).
//
// TWO-MODE FACTORY (orchestrator amendment 2026-07-03, owner: "why recreate the
// wheel when someone shows us a fully functioning car"):
//   MODE 1 — LOADER+BINDER: try the P0-mirrored Blink uGUI prefab under
//     Resources/RpgUi/prefabs/<Name>; instantiate, resolve named children
//     DEFENSIVELY (missing child => FlowTrace.Warn + fallback, never NRE), and
//     ENFORCE the same contracts on the prefab's parts (§1.1 fill law verified/
//     corrected on bind).
//   MODE 2 — CONSTRUCTED: the code-built widget, which is BOTH the fresh-clone /
//     art-absent fallback (the ff.blinkchrome-OFF design contract — every widget
//     must look right with pack art present AND absent) and the fidelity
//     reference the orchestrator screenshots against the prefab render.
//
// THE ONE FILL-BINDING CONTRACT (§1.1, game law):
//   1. fill.sprite is ALWAYS non-null (bar_stat_fill -> kit sprite -> SolidSprite).
//   2. type = Filled, fillMethod = Horizontal, fillOrigin = Left.
//   3. The ONLY width mutation is fill.fillAmount = cur / Mathf.Max(1, max).
//   4. Updates arrive via VM Changed events; BarHandle.SetValue writes bar +
//      value label ATOMICALLY. This makes the 9/145-renders-full bug
//      (BattleHud9Zone.cs:1701 sprite-less Filled) structurally impossible.
//
// PREFAB PARAMETERS (amendment A1): where the pack already measured a widget,
// we consume Resources "widget-params.json" (P0-generated; loaded null-safe) or
// the doc's measured numbers as authored fallbacks (CastBar 800x56 / 507x22).
// No eyeballed dimensions where the pack measured them.
//
// MOBILE LENS: no per-frame allocations (labels rebuilt only on visible-digit
// change), raycastTarget only on interactive graphics, sliced sprites sized by
// RectTransform (never scaled), >=56px touch targets on the controller cluster.
//
// A3 CHROME TINT: all CHROME art (frames, plates, cores, tracker/toast plates)
// tints by ElarionUiKit.ChromeTint -> UiStyle.Theme.ChromeTint (ONE field; white
// until the owner's palette ruling). Colored button faces / rarity / semantic
// colors are CONTENT, not chrome — never tinted by the hook.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.HudModel;

namespace DeNelle.Core.UI
{
    public static partial class ElarionUiKit
    {
        // =====================================================================
        // A3 — the global chrome-tint hook (routes through UiStyle, ONE field).
        // =====================================================================

        /// <summary>The ONE tint applied to every piece of Blink CHROME art the factory renders.
        /// Routed through <see cref="UiStyle.Theme"/> (amendment A3 + BLINK_OBSIDIAN doc: UiStyle is
        /// the single style authority) — the pending palette ruling lands in UiTheme.ChromeTint.</summary>
        public static Color ChromeTint => UiStyle.Theme.ChromeTint;

        /// <summary>Cooldown shade drawn by action slots' radial sweep.</summary>
        public static readonly Color CdShade = new Color(0f, 0f, 0f, 0.62f);

        // =====================================================================
        // SOLID SPRITE — the guaranteed-non-null fill sprite of last resort.
        // =====================================================================

        private static Sprite _solid;
        private static bool _solidTried;
        /// <summary>A tiny solid-white sprite — the §1.1 fill sprite of last resort so a Filled
        /// Image can NEVER be sprite-less (uGUI ignores fillAmount on a null-sprite Image — the
        /// proven 9/145 root cause). Failure-safe like the kit's other procedural sprites.</summary>
        public static Sprite SolidSprite
        {
            get
            {
                if (!_solidTried)
                {
                    _solidTried = true;
                    try
                    {
                        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                        var px = new Color32[16];
                        for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
                        tex.SetPixels32(px);
                        tex.Apply();
                        _solid = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f),
                                               100f, 0, SpriteMeshType.FullRect);
                    }
                    catch (Exception e)
                    {
                        FlowTrace.Warn("UI", "ElarionUiKit.SolidSprite build failed: " + e.Message);
                        _solid = null;
                    }
                }
                return _solid;
            }
        }

        /// <summary>The §1.1 non-null fill-sprite chain: named pack fill → white stat fill →
        /// SolidSprite → kit rounded sprite. Logs a Fail if literally everything is unavailable
        /// (never observed in practice — SolidSprite is procedural).</summary>
        private static Sprite FillSpriteChain(string packFillName)
        {
            Sprite s = string.IsNullOrEmpty(packFillName)
                ? null : RpgUiCatalog.Get(RpgUiCatalog.RoleHud, packFillName);
            if (s == null) s = RpgUiCatalog.Get(RpgUiCatalog.RoleHud, RpgUiCatalog.HudBarStatFill);
            if (s == null) s = SolidSprite;
            if (s == null) s = RoundedSprite;
            if (s == null) FlowTrace.Fail("UI", "FillSpriteChain: NO fill sprite available (even procedural) — Filled fill would be ignored by uGUI");
            return s;
        }

        // =====================================================================
        // LOADER + BINDER plumbing (amendment: prefab-first, constructed fallback)
        // =====================================================================

        /// <summary>When true (default) each builder first tries the mirrored Blink prefab under
        /// Resources/RpgUi/prefabs/. The kit demo flips this to render the CONSTRUCTED mode of every
        /// widget beside the prefab mode for the screenshot compare.</summary>
        public static bool PrefabMode = true;

        /// <summary>Marker attached to every prefab-mode widget instance (diagnostics: which source
        /// prefab a live widget came from).</summary>
        public sealed class UiKitPrefabBinder : MonoBehaviour
        {
            /// <summary>The Resources prefab name this widget was instantiated from.</summary>
            public string sourcePrefab;
        }

        /// <summary>Try the mirrored Blink prefabs by candidate name; instantiate the first hit under
        /// <paramref name="parent"/> stretched into the given anchor rect. Null when absent /
        /// PrefabMode off (the constructed fallback then runs). Never throws.</summary>
        private static GameObject InstantiateBlinkPrefab(Transform parent, Vector2 anchorMin, Vector2 anchorMax,
                                                         params string[] candidateNames)
        {
            if (!PrefabMode || candidateNames == null) return null;
            for (int i = 0; i < candidateNames.Length; i++)
            {
                string name = candidateNames[i];
                if (string.IsNullOrEmpty(name)) continue;
                GameObject pf = Guard.Try("UI", "load Blink prefab " + name,
                    () => Resources.Load<GameObject>(RpgUiCatalog.PrefabRoot + name), null);
                if (pf == null) continue;

                var inst = UnityEngine.Object.Instantiate(pf, parent, false);
                inst.name = name;
                var rt = inst.transform as RectTransform;
                if (rt == null) rt = inst.AddComponent<RectTransform>();
                rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one;                       // size by rect, never scale (corner law)
                inst.AddComponent<UiKitPrefabBinder>().sourcePrefab = name;
                FlowTrace.Step("UI", "Kit prefab-mode: instantiated '" + name + "'");
                return inst;
            }
            return null;
        }

        /// <summary>Depth-first search for a component on a descendant whose name contains ANY of the
        /// candidate fragments (case/space/underscore-insensitive). The pack names children cleanly
        /// (HealthFill, ManaFill, PlayerName, CastBar1Fill...). Construction-time only.</summary>
        private static T FindDeep<T>(Transform root, params string[] nameFragments) where T : Component
        {
            if (root == null || nameFragments == null || nameFragments.Length == 0) return null;
            var stack = new Stack<Transform>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var t = stack.Pop();
                if (t != root)
                {
                    string norm = t.name.Replace(" ", "").Replace("_", "");
                    for (int i = 0; i < nameFragments.Length; i++)
                    {
                        if (norm.IndexOf(nameFragments[i], StringComparison.OrdinalIgnoreCase) < 0) continue;
                        var c = t.GetComponent<T>();
                        if (c != null) return c;
                        break;
                    }
                }
                for (int i = t.childCount - 1; i >= 0; i--) stack.Push(t.GetChild(i));
            }
            return null;
        }

        /// <summary>ENFORCE the §1.1 fill law on a bound (prefab or constructed) fill Image:
        /// non-null sprite, Filled/Horizontal/Left, non-raycast. Corrects a prefab that shipped
        /// misconfigured; logs when a correction was needed.</summary>
        private static void EnforceFillContract(Image fill)
        {
            if (fill == null) return;
            if (fill.sprite == null)
            {
                fill.sprite = FillSpriteChain(null);
                FlowTrace.Warn("UI", "fill contract: bound fill had a NULL sprite — assigned the fallback fill (9/145 law)");
            }
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.raycastTarget = false;
        }

        // =====================================================================
        // A1 — widget-params.json (P0 prefab-extracted parameters; null-safe)
        // =====================================================================

#pragma warning disable 0649 // fields assigned by JsonUtility
        [Serializable] private class WpFile   { public WpPrefab[] prefabs; }
        [Serializable] private class WpPrefab { public string name; public WpNode[] nodes; }
        [Serializable] private class WpNode
        {
            public string path;
            public float[] anchorMin, anchorMax, anchoredPosition, sizeDelta, pivot;
            public string sprite, imageType, fillMethod;
            public int fillOrigin;
            public float fillAmount;
        }
#pragma warning restore 0649

        private static WpFile _wp;
        private static bool _wpTried;

        /// <summary>The P0-extracted prefab parameters (amendment A1). Loaded once, null-safe: absent
        /// or unparseable ⇒ FlowTrace.Warn + the authored fallback constants are used instead.</summary>
        private static WpFile WidgetParams
        {
            get
            {
                if (_wpTried) return _wp;
                _wpTried = true;
                _wp = Guard.Try("UI", "load widget-params.json", () =>
                {
                    // Primary contract path, then the A1 amendment path — first hit wins.
                    var ta = Resources.Load<TextAsset>("Data/Canonical/widget-params")
                          ?? Resources.Load<TextAsset>("RpgUi/params/widget-params");
                    if (ta == null || string.IsNullOrEmpty(ta.text)) return (WpFile)null;
                    var parsed = JsonUtility.FromJson<WpFile>(ta.text);
                    if (parsed == null || parsed.prefabs == null || parsed.prefabs.Length == 0) return (WpFile)null;
                    FlowTrace.Step("UI", "widget-params loaded: " + parsed.prefabs.Length + " prefabs");
                    return parsed;
                }, null);
                if (_wp == null)
                    FlowTrace.Warn("UI", "widget-params.json absent/empty (P0 not landed yet?) — using authored fallback dimensions");
                return _wp;
            }
        }

        /// <summary>Try the measured rect (as parent-fraction xMin,yMin,xMax,yMax) of a node inside a
        /// measured prefab. Uses the ROOT node's sizeDelta as the reference frame and assumes a
        /// centred child (the pack's convention). False ⇒ caller uses its authored fallback.</summary>
        private static bool TryParamRect(string prefabName, string nodeFragment, out Vector4 frac)
        {
            frac = default;
            var wp = WidgetParams;
            if (wp == null || wp.prefabs == null) return false;
            for (int p = 0; p < wp.prefabs.Length; p++)
            {
                var pf = wp.prefabs[p];
                if (pf == null || pf.nodes == null) continue;
                if (string.IsNullOrEmpty(pf.name) ||
                    pf.name.IndexOf(prefabName, StringComparison.OrdinalIgnoreCase) < 0) continue;

                // Reference frame = the root node's size (path "" or the first node).
                WpNode rootN = null, hit = null;
                for (int n = 0; n < pf.nodes.Length; n++)
                {
                    var nd = pf.nodes[n];
                    if (nd == null) continue;
                    if (rootN == null && (string.IsNullOrEmpty(nd.path) || nd.path == pf.name)) rootN = nd;
                    if (hit == null && !string.IsNullOrEmpty(nd.path) &&
                        nd.path.Replace(" ", "").Replace("_", "")
                          .IndexOf(nodeFragment, StringComparison.OrdinalIgnoreCase) >= 0) hit = nd;
                }
                if (rootN == null && pf.nodes.Length > 0) rootN = pf.nodes[0];
                if (rootN == null || hit == null) return false;
                if (rootN.sizeDelta == null || rootN.sizeDelta.Length < 2 ||
                    hit.sizeDelta == null || hit.sizeDelta.Length < 2) return false;

                float rw = Mathf.Abs(rootN.sizeDelta[0]), rh = Mathf.Abs(rootN.sizeDelta[1]);
                float w = Mathf.Abs(hit.sizeDelta[0]), h = Mathf.Abs(hit.sizeDelta[1]);
                if (rw < 1f || rh < 1f || w < 1f || h < 1f) return false;
                float ox = 0.5f, oy = 0.5f;   // centred-child convention
                if (hit.anchoredPosition != null && hit.anchoredPosition.Length >= 2)
                {
                    ox = 0.5f + hit.anchoredPosition[0] / rw;
                    oy = 0.5f + hit.anchoredPosition[1] / rh;
                }
                float fx = w / rw * 0.5f, fy = h / rh * 0.5f;
                frac = new Vector4(Mathf.Clamp01(ox - fx), Mathf.Clamp01(oy - fy),
                                   Mathf.Clamp01(ox + fx), Mathf.Clamp01(oy + fy));
                return true;
            }
            return false;
        }

        // =====================================================================
        // §1.1 BuildObsidianBar — THE bar.
        // =====================================================================

        /// <summary>Which Obsidian bar a surface is showing — drives frame art + fill tint.</summary>
        public enum ObsidianBarKind { Health, Mana, Energy, Stamina, Cast, Xp, Heart, Loading, Stat }

        /// <summary>Fill tint for a bar kind (only applied when the fill art is the tintable white).</summary>
        public static Color ObsidianBarTint(ObsidianBarKind kind)
        {
            switch (kind)
            {
                case ObsidianBarKind.Health:  return ElarionUi.HpRed;
                case ObsidianBarKind.Mana:    return ElarionUi.ManaBlue;
                case ObsidianBarKind.Energy:  return new Color(0.95f, 0.80f, 0.20f, 1f);
                case ObsidianBarKind.Stamina: return new Color(0.42f, 0.78f, 0.36f, 1f);
                case ObsidianBarKind.Cast:    return ElarionUi.Aether;
                case ObsidianBarKind.Xp:      return new Color(1f, 0.85f, 0.15f, 1f);
                case ObsidianBarKind.Heart:   return ElarionUi.Gold;
                case ObsidianBarKind.Loading: return ElarionUi.Gold;
                default:                      return new Color(0.85f, 0.85f, 0.85f, 1f); // Stat: near-white
            }
        }

        /// <summary>Frame (ornate silhouette) sprite name for a bar kind; null = no dedicated frame.</summary>
        private static string ObsidianBarFrameName(ObsidianBarKind kind)
        {
            switch (kind)
            {
                case ObsidianBarKind.Health:  return RpgUiCatalog.HudBarHealth;
                case ObsidianBarKind.Mana:    return RpgUiCatalog.HudBarMana;
                case ObsidianBarKind.Energy:  return RpgUiCatalog.HudBarEnergy;
                case ObsidianBarKind.Stamina: return RpgUiCatalog.HudBarStamina;
                case ObsidianBarKind.Cast:    return RpgUiCatalog.HudBarCast1;
                case ObsidianBarKind.Xp:      return RpgUiCatalog.HudBarXp;
                case ObsidianBarKind.Heart:   return RpgUiCatalog.HudBarHealth;    // heart rides the health silhouette, gold-tinted fill
                case ObsidianBarKind.Loading: return RpgUiCatalog.ElementLoadingBg; // element role — handled below
                default:                      return RpgUiCatalog.HudBarStatBg;
            }
        }

        /// <summary>Prefab-mode candidates per bar kind (nameplates/cast have their own builders).</summary>
        private static readonly string[] _barPrefabsHealth  = { "HealthBar", "Health_Bar" };
        private static readonly string[] _barPrefabsMana    = { "ManaBar", "Mana_Bar" };
        private static readonly string[] _barPrefabsEnergy  = { "EnergyBar", "Energy_Bar" };
        private static readonly string[] _barPrefabsStamina = { "StaminaBar", "Stamina_Bar", "STaminaBar" };
        private static readonly string[] _barPrefabsXp      = { "XPBar", "XpBar", "hud-xpbar" };
        private static string[] BarPrefabCandidates(ObsidianBarKind kind)
        {
            switch (kind)
            {
                case ObsidianBarKind.Health:  return _barPrefabsHealth;
                case ObsidianBarKind.Mana:    return _barPrefabsMana;
                case ObsidianBarKind.Energy:  return _barPrefabsEnergy;
                case ObsidianBarKind.Stamina: return _barPrefabsStamina;
                case ObsidianBarKind.Xp:      return _barPrefabsXp;
                default:                      return null; // Cast via BuildCastBar; Stat/Heart/Loading constructed
            }
        }

        /// <summary>
        /// THE bar (§1.1). Prefab-mode: binds the mirrored Blink bar prefab's fill (contract
        /// enforced on bind). Constructed mode (fallback + reference): recessed track, a
        /// contract-compliant fill (NON-NULL sprite, Filled/Horizontal/Left), the kind's ornate
        /// frame drawn ABOVE the fill as a non-raycast overlay (the proven DressBar layering),
        /// and an optional value label. Drive it ONLY via the returned handle's SetValue —
        /// which writes bar + label atomically.
        /// </summary>
        public static BarHandle BuildObsidianBar(Transform parent, ObsidianBarKind kind,
            Vector2 anchorMin, Vector2 anchorMax, bool withValue = false, bool framed = true)
        {
            // ── MODE 1: loader+binder ────────────────────────────────────────
            var pf = InstantiateBlinkPrefab(parent, anchorMin, anchorMax, BarPrefabCandidates(kind));
            if (pf != null)
            {
                var pfFill = FindDeep<Image>(pf.transform, "fill");
                if (pfFill != null)
                {
                    EnforceFillContract(pfFill);
                    var h = new BarHandle
                    {
                        track = (RectTransform)pf.transform,
                        fill = pfFill,
                        frame = pf.GetComponent<Image>(),
                    };
                    if (withValue)
                    {
                        var pfLabel = FindDeep<TMP_Text>(pf.transform, "value", "text", "label");
                        h.valueLabel = pfLabel != null
                            ? pfLabel
                            : Label(pf.transform, "", 0f, 1f, ElarionUi.Parchment, ElarionUi.FontLabel,
                                    TextAlignmentOptions.Center, 0f, 1f, bold: true);
                        h.valueLabel.raycastTarget = false;
                        FitSingleLine(h.valueLabel);                       // §1.14 — value never spills the bar
                    }
                    h.SetImmediate(1f, 1f);
                    return h;
                }
                FlowTrace.Warn("UI", "BuildObsidianBar(" + kind + "): prefab '" + pf.name +
                               "' has no *fill* child — destroying it and constructing the fallback bar");
                UnityEngine.Object.Destroy(pf);
            }

            // ── MODE 2: constructed (fallback + fidelity reference) ──────────
            var rootGo = new GameObject("ObsidianBar_" + kind, typeof(RectTransform));
            rootGo.transform.SetParent(parent, false);
            var root = (RectTransform)rootGo.transform;
            root.anchorMin = anchorMin; root.anchorMax = anchorMax;
            root.offsetMin = Vector2.zero; root.offsetMax = Vector2.zero;

            // Recessed track (9-sliced stat background when mirrored, dark well otherwise).
            var trackGo = new GameObject("Track", typeof(Image));
            trackGo.transform.SetParent(rootGo.transform, false);
            var trt = (RectTransform)trackGo.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var trackImg = trackGo.GetComponent<Image>();
            var trackSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleHud, RpgUiCatalog.HudBarStatBg);
            if (trackSprite == null && kind == ObsidianBarKind.Loading)
                trackSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleElement, RpgUiCatalog.ElementLoadingBg);
            if (trackSprite != null)
            {
                trackImg.sprite = trackSprite;
                trackImg.type = Image.Type.Sliced;
                trackImg.fillCenter = true;
                trackImg.color = ChromeTint;                       // chrome
            }
            else
            {
                trackImg.color = Track;
                ApplyRounded(trackImg);
            }
            trackImg.raycastTarget = false;

            // Fill placement: measured (widget-params / doc numbers) for ornate silhouettes;
            // a thin uniform inset over the plain stat track.
            Vector4 fillFrac;
            bool ornate = framed && kind != ObsidianBarKind.Stat && kind != ObsidianBarKind.Loading;
            if (!TryParamRect(BarParamPrefabName(kind), "fill", out fillFrac))
                fillFrac = ornate ? new Vector4(0.075f, 0.24f, 0.925f, 0.76f)  // authored: inside the forged ends
                                  : new Vector4(0.015f, 0.12f, 0.985f, 0.88f);

            var fillGo = new GameObject("Fill", typeof(Image));
            fillGo.transform.SetParent(trackGo.transform, false);
            var frt = (RectTransform)fillGo.transform;
            frt.anchorMin = new Vector2(fillFrac.x, fillFrac.y);
            frt.anchorMax = new Vector2(fillFrac.z, fillFrac.w);
            frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
            var fill = fillGo.GetComponent<Image>();
            fill.sprite = FillSpriteChain(kind == ObsidianBarKind.Cast ? RpgUiCatalog.HudBarCastFill
                          : kind == ObsidianBarKind.Loading ? RpgUiCatalog.ElementLoadingFill : null);
            // The white stat fill / procedural fill takes the kind tint; a coloured pack fill stays white.
            bool tintable = fill.sprite == null
                || fill.sprite == SolidSprite || fill.sprite == RoundedSprite
                || fill.sprite == RpgUiCatalog.Get(RpgUiCatalog.RoleHud, RpgUiCatalog.HudBarStatFill);
            fill.color = tintable ? ObsidianBarTint(kind) : Color.white;
            EnforceFillContract(fill);
            fill.fillAmount = 1f;

            // Ornate frame overlay ABOVE the fill (hollow silhouette; the fill shows through).
            Image frameImg = null;
            if (framed)
            {
                var frameSprite = RpgUiCatalog.Get(
                    kind == ObsidianBarKind.Loading ? RpgUiCatalog.RoleElement : RpgUiCatalog.RoleHud,
                    ObsidianBarFrameName(kind));
                if (frameSprite != null)
                {
                    var fgo = new GameObject("Frame", typeof(Image));
                    fgo.transform.SetParent(rootGo.transform, false);
                    var fgrt = (RectTransform)fgo.transform;
                    fgrt.anchorMin = Vector2.zero; fgrt.anchorMax = Vector2.one;
                    fgrt.offsetMin = Vector2.zero; fgrt.offsetMax = Vector2.zero;
                    frameImg = fgo.GetComponent<Image>();
                    frameImg.sprite = frameSprite;
                    frameImg.type = Image.Type.Simple;   // ornate silhouettes distort under 9-slice (§2 rule)
                    frameImg.color = ChromeTint;         // chrome
                    frameImg.raycastTarget = false;
                    fgo.transform.SetAsLastSibling();
                }
            }

            TMP_Text valueLabel = null;
            if (withValue)
            {
                valueLabel = Label(rootGo.transform, "", 0f, 1f, ElarionUi.Parchment,
                                   ElarionUi.FontLabel, TextAlignmentOptions.Center, 0f, 1f, bold: true);
                valueLabel.outlineColor = new Color32(10, 10, 14, 200);
                valueLabel.outlineWidth = 0.14f;
                valueLabel.raycastTarget = false;
                FitSingleLine(valueLabel);                                 // §1.14 — value never spills the bar
                valueLabel.transform.SetAsLastSibling();
            }

            var handle = new BarHandle { track = root, fill = fill, valueLabel = valueLabel, frame = frameImg };
            handle.SetImmediate(1f, 1f);
            return handle;
        }

        /// <summary>The widget-params prefab name a bar kind's measured fill would live under.</summary>
        private static string BarParamPrefabName(ObsidianBarKind kind)
        {
            switch (kind)
            {
                case ObsidianBarKind.Health:  return "HealthBar";
                case ObsidianBarKind.Mana:    return "ManaBar";
                case ObsidianBarKind.Energy:  return "EnergyBar";
                case ObsidianBarKind.Stamina: return "StaminaBar";
                case ObsidianBarKind.Cast:    return "CastBar1";
                case ObsidianBarKind.Xp:      return "XPBar";
                default:                      return "StatBar";
            }
        }

        // =====================================================================
        // §1.2 BuildObsidianButton — the 5x4 family (+ the ButtonKind shim map).
        // =====================================================================

        /// <summary>The five Obsidian button face styles.</summary>
        public enum ObsidianButtonStyle { Style1 = 1, Style2 = 2, Style3 = 3, Style4 = 4, Style5 = 5 }
        /// <summary>The four Obsidian button colours.</summary>
        public enum ObsidianButtonColor { Gray, Green, Red, Yellow }

        /// <summary>Canonical RpgUiCatalog sprite name for a family member ("button3_green").</summary>
        public static string ObsidianButtonSpriteName(ObsidianButtonStyle style, ObsidianButtonColor color)
        {
            // STANDARDIZED to GREY faces everywhere (owner 2026-07-16: "standardize all to grey and
            // white — easier to read everywhere"; the mixed grey/green/red/brown-gold plates were
            // inconsistent and the gold plates read poorly). One grey face + white label for every
            // button. The color enum is kept for call-site intent + future opt-in, but all colors
            // resolve to the grey sprite here — the single place button faces are chosen.
            return "button" + (int)style + "_gray";
        }

        /// <summary>Button label ink — STANDARDIZED to Parchment (white) on the uniform grey plate
        /// (owner 2026-07-16 "grey and white everywhere"). Luminance still carries all meaning
        /// (light text on a dark plate); lives HERE in the one builder, never per-caller.</summary>
        public static Color ObsidianButtonLabelColor(ObsidianButtonColor color)
        {
            return ElarionUi.Parchment;
        }

        /// <summary>The §1.2 back-compat shim map: legacy ButtonKind → Obsidian (style, color).
        /// Gold→(1,Yellow), Confirm→(2,Green), Danger→(1,Red), Quiet→(1,Gray).</summary>
        internal static void MapButtonKind(ButtonKind kind, out ObsidianButtonStyle style, out ObsidianButtonColor color)
        {
            switch (kind)
            {
                case ButtonKind.Confirm: style = ObsidianButtonStyle.Style2; color = ObsidianButtonColor.Green;  break;
                case ButtonKind.Danger:  style = ObsidianButtonStyle.Style1; color = ObsidianButtonColor.Red;    break;
                case ButtonKind.Quiet:   style = ObsidianButtonStyle.Style1; color = ObsidianButtonColor.Gray;   break;
                default:                 style = ObsidianButtonStyle.Style1; color = ObsidianButtonColor.Yellow; break; // Gold
            }
        }

        /// <summary>Reverse map for the null-art fallback (routes back to the procedural kind look).</summary>
        private static ButtonKind KindFor(ObsidianButtonColor color)
        {
            switch (color)
            {
                case ObsidianButtonColor.Green:  return ButtonKind.Confirm;
                case ObsidianButtonColor.Red:    return ButtonKind.Danger;
                case ObsidianButtonColor.Gray:   return ButtonKind.Quiet;
                default:                         return ButtonKind.Gold;
            }
        }

        /// <summary>
        /// An Obsidian family button (§1.2): the real 9-sliced button art (Fill Center, sized by
        /// rect) with the kit's eased ColorTint feedback and a role-fonted label. Colored faces are
        /// CONTENT colour — not tinted by the A3 chrome hook. Null art ⇒ the procedural kind button
        /// (the dual-state contract's OFF look). Prefab-mode is attempted first.
        /// </summary>
        // ── COMMON spaced button COLUMN (owner 2026-07-16 "fix it in common") ──────────────
        // The root cause of stacked/overlapping menu buttons (pause, help, ...) was every menu
        // HAND-PLACING fraction-anchored buttons: the MinTouchPx(112) floor grows each button, and
        // on a short modal body those grown rects overlap because the fraction slots are smaller
        // than 112px. Fixed ONCE here: BuildButtonColumn lays a VerticalLayoutGroup over the body
        // and AddColumnButton stamps a FIXED-height button into it, so the gap is guaranteed and the
        // rows can never collide at any screen size. Menus call these instead of anchoring by hand.
        /// <summary>Create a spaced vertical button column over a modal body. Add rows with
        /// <see cref="AddColumnButton"/>; spacing + no-overlap are guaranteed regardless of screen.</summary>
        public static RectTransform BuildButtonColumn(Transform body, float gapPx = 18f,
            float sideInset = 0.06f, float topInset = 0.04f, float bottomInset = 0.04f)
        {
            var go = new GameObject("ButtonColumn", typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.transform.SetParent(body, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(sideInset, bottomInset);
            rt.anchorMax = new Vector2(1f - sideInset, 1f - topInset);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var vlg = go.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = gapPx;
            vlg.childControlWidth = true;  vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperCenter;
            return rt;
        }

        /// <summary>Add one fixed-height button to a <see cref="BuildButtonColumn"/> stack (returns the
        /// Button so callers can capture/hide it). Full-stretch anchors; the LayoutElement pins the
        /// height (>= MinTouchPx, default CanonCtaHeight) so the touch-floor can never overflow the row.</summary>
        public static Button AddColumnButton(Transform column, string label,
            ObsidianButtonColor color, Action onClick,
            ObsidianButtonStyle style = ObsidianButtonStyle.Style1, float heightPx = 0f)
        {
            var btn = BuildObsidianButton(column, label, style, color, Vector2.zero, Vector2.one, onClick);
            if (btn == null) return null;
            var le = btn.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = btn.gameObject.AddComponent<LayoutElement>();
            le.minHeight = MinTouchPx;
            le.preferredHeight = heightPx > 0f ? heightPx : CanonCtaHeight;
            le.flexibleHeight = 0f;
            return btn;
        }

        public static Button BuildObsidianButton(Transform parent, string label,
            ObsidianButtonStyle style, ObsidianButtonColor color,
            Vector2 anchorMin, Vector2 anchorMax, Action onClick = null)
        {
            // ── MODE 1: loader+binder (the pack ships assembled button prefabs) ──
            var pf = InstantiateBlinkPrefab(parent, anchorMin, anchorMax,
                "Button" + (int)style + "_" + color, "Button" + (int)style);
            if (pf != null)
            {
                var pfBtn = pf.TryGetComponent<Button>(out var pfb) ? pfb : FindDeep<Button>(pf.transform, "button");
                if (pfBtn != null)
                {
                    var pfLabel = FindDeep<TMP_Text>(pf.transform, "text", "label");
                    if (pfLabel != null)
                    {
                        pfLabel.text = label ?? "";
                        // CONTRAST LAW: the prefab's BAKED label colour (gold) is unreadable on the
                        // yellow/gold face — override it here so every caller inherits the one rule.
                        pfLabel.color = ObsidianButtonLabelColor(color);
                        pfLabel.fontStyle |= FontStyles.Bold;
                        EnsureFont(pfLabel, FontRole.Body);
                        FitSingleLine(pfLabel);                            // §1.14 — button text never clips ("BU SEL")
                    }
                    else
                    {
                        var overlay = Label(pf.transform, label ?? "", 0f, 1f,
                            ObsidianButtonLabelColor(color),
                            ElarionUi.FontBody, TextAlignmentOptions.Center, 0f, 1f, bold: true);
                        overlay.raycastTarget = false;
                        EnsureFont(overlay, FontRole.Body);
                        FitSingleLine(overlay);                            // §1.14
                    }
                    if (onClick != null) pfBtn.onClick.AddListener(() => onClick());
                    ClampMinTouch(pfBtn);   // P0 kit touch floor
                    return pfBtn;
                }
                FlowTrace.Warn("UI", "BuildObsidianButton: prefab '" + pf.name + "' has no Button — constructing fallback");
                UnityEngine.Object.Destroy(pf);
            }

            // ── MODE 2: constructed on the mirrored sprite ────────────────────
            var art = RpgUiCatalog.Get(RpgUiCatalog.RoleButton, ObsidianButtonSpriteName(style, color));
            if (art == null)
                return Button(parent, label, KindFor(color), anchorMin, anchorMax, onClick); // procedural (shim won't loop: it re-checks this same null)

            var go = new GameObject("ObsBtn_" + label, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.sprite = art;
            img.type = Image.Type.Sliced;
            img.fillCenter = true;
            img.color = Color.white;   // the face's colour IS the semantic colour (content, not chrome)

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            StyleButtonColors(btn);    // eased 0.12s brightness feedback on the real art
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var tt = Label(go.transform, label ?? "", 0f, 1f,
                           ObsidianButtonLabelColor(color),
                           ElarionUi.FontBody, TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);
            tt.raycastTarget = false;
            EnsureFont(tt, FontRole.Body);
            FitSingleLine(tt);                                             // §1.14 — button text never clips
            ClampMinTouch(btn);   // P0 kit touch floor
            return btn;
        }

        /// <summary>Toast tone → Notification plate variant (§1.5).</summary>
        private static string ToastPlateName(ToastTone tone)
        {
            switch (tone)
            {
                case ToastTone.Confirm: return RpgUiCatalog.ElementNotif2;
                case ToastTone.Danger:  return RpgUiCatalog.ElementNotif4;
                default:                return RpgUiCatalog.ElementNotif1; // Gold / Info
            }
        }

        // =====================================================================
        // §1.4 CurrencyChip — gold primacy, count-tween, NO flash.
        // =====================================================================

        /// <summary>Game soft-currency kinds shown on chips. NESTED inside the kit (deviation from
        /// the doc's namespace-level enum: DeNelle.Wallet.CurrencyKind already exists and 16 files
        /// import both namespaces — a namespace-level twin would ambiguate every one of them).</summary>
        public enum CurrencyKind { Gold, Crystal, Wood, Iron, Food, Wisdom }

        /// <summary>Live handle of a built currency chip. SetAmount count-tweens the number —
        /// NEVER a red/green flash (owner rule).</summary>
        public sealed class CurrencyChipHandle
        {
            /// <summary>The chip root (plate).</summary>
            public GameObject root;
            /// <summary>The currency icon (OUR game icon — Blink icons are out of scope).</summary>
            public Image icon;
            /// <summary>The amount label.</summary>
            public TMP_Text amount;
            /// <summary>Optional ALWAYS-VISIBLE text identifier ("SKILL", "Wood"…) — null when the
            /// chip was built without one. Colorblind law: a chip must never be a naked number.</summary>
            public TMP_Text tag;
            /// <summary>The plate Image (chrome).</summary>
            public Image plate;

            /// <summary>Fraction of the chip's width the amount band occupies (set by the
            /// builder; feeds the WO-697 content-fit preferred-width sync).</summary>
            internal float amountBand = 0.62f;

            private long _shown = long.MinValue;
            private long _target;

            /// <summary>Set the shown amount. animate:true count-tweens 0.35s eased; no colour flash ever.</summary>
            public void SetAmount(long v, bool animate = true)
            {
                _target = v;
                if (amount == null) return;
                if (!animate || !Application.isPlaying || _shown == long.MinValue)
                {
                    UiKitTween.Cancel(this);
                    WriteAmount(v);
                    return;
                }
                long from = _shown;
                UiKitTween.Value(this, from, v, 0.35f, val => WriteAmount((long)Math.Round(val)));
            }

            private void WriteAmount(long v)
            {
                if (v == _shown) return;   // no per-frame alloc while the tween passes the same int
                _shown = v;
                if (amount == null) return;
                // WO-697 kit law: the chip OWNS currency formatting — CompactNumber, never
                // a grouped verbatim string that a narrow chip must ellipsize/shrink.
                amount.text = ElarionUi.CompactNumber(v);
                SyncPreferredWidth();
            }

            // WO-697 content-fit safety net: when the chip lives in a layout group
            // (resource panel rows carry a LayoutElement), grow the chip's preferred
            // width so the amount band always seats the full compact string — a
            // 7-digit value can never clip, whatever the formatter emits. Anchor-rect
            // consumers (panel footers) have no LayoutElement -> no-op there.
            private void SyncPreferredWidth()
            {
                if (root == null || amount == null) return;
                var le = root.GetComponent<LayoutElement>();
                if (le == null || le.ignoreLayout) return;
                float textW = amount.GetPreferredValues(amount.text).x;
                float needed = textW / Mathf.Max(0.20f, amountBand) + 12f;
                if (needed > le.preferredWidth) le.preferredWidth = needed;
            }
        }

        /// <summary>
        /// The ONE currency chip (§1.4): an element_stat plate (9-sliced, chrome-tinted), one of OUR
        /// currency icons, and a count-tweened amount. <paramref name="primary"/> (Gold) renders a
        /// size class larger with the gold-tinted amount — gold primacy is a property of the
        /// component, never per-screen styling. Every wallet strip/footer consumes this.
        /// <paramref name="tag"/> (optional) renders a small ALWAYS-VISIBLE text identifier
        /// ("SKILL", "Wood"…) left of the amount — shown even when the icon resolves. Owner is
        /// red/green colorblind and the currency icon set is a known art gap (RpgUi/currency/*
        /// missing → icon well hides): a chip must NEVER read as a naked number.
        /// </summary>
        public static CurrencyChipHandle CurrencyChip(Transform parent, CurrencyKind kind,
            Vector2 anchorMin, Vector2 anchorMax, bool primary = false, string tag = null)
        {
            var go = new GameObject("CurrencyChip_" + kind, typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var plate = go.GetComponent<Image>();
            var plateSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleElement, RpgUiCatalog.ElementStat);
            if (plateSprite != null)
            {
                plate.sprite = plateSprite;
                plate.type = Image.Type.Sliced;
                plate.fillCenter = true;
                plate.color = ChromeTint;                          // chrome
            }
            else
            {
                plate.color = Glass;
                ApplyRounded(plate);
            }
            plate.raycastTarget = false;

            // Icon well (left) — OUR icon via the concept resolver; hidden when unresolved
            // (consumers may SetIcon a specific sprite; the icon mandate stays ours).
            var iconGo = new GameObject("Icon", typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            var irt = (RectTransform)iconGo.transform;
            irt.anchorMin = new Vector2(0.04f, 0.14f); irt.anchorMax = new Vector2(0.30f, 0.86f);
            irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
            var icon = iconGo.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var iconSprite = UiStyle.Icon(kind.ToString().ToLowerInvariant());
            // WO-697: mirrored currency art fallback (Resources/RpgUi/currency/currency_*,
            // the WO-675/676 chip grammar set) when the concept resolver comes up empty.
            if (iconSprite == null)
                iconSprite = RpgUiCatalog.Get("currency", "currency_" + kind.ToString().ToLowerInvariant());
            if (iconSprite != null) icon.sprite = iconSprite;
            else iconGo.SetActive(false);

            // Text tag — WO-697 icon-first rule (supersedes the flag_03 always-visible tag):
            // when the currency ICON resolves, the icon alone carries identity (shape identity,
            // colorblind-safe — never color-only) and the text label is DROPPED. The tag renders
            // only as the no-art fallback, so a chip still never reads as a naked number: the
            // caller's tag if provided, else the kind name.
            TMP_Text tagLabel = null;
            bool hasTag = iconSprite == null;
            if (hasTag)
            {
                string tagText = !string.IsNullOrEmpty(tag) ? tag : kind.ToString();
                tagLabel = Label(go.transform, tagText, 0f, 1f,
                    ElarionUi.Parchment, ElarionUi.FontMicro,
                    TextAlignmentOptions.MidlineLeft, 0.33f, 0.58f);
                tagLabel.raycastTarget = false;
                EnsureFont(tagLabel, FontRole.Body);
                FitSingleLine(tagLabel);                                   // §1.14 — tag never spills its slot
            }

            // Amount — gold primacy: primary chip = one size class up + gilt digits.
            // With a tag present the amount cedes the tag's slot (right-aligned, so short
            // wallets never collide). WO-697 kit law: NO FitSingleLine here — a currency
            // value never ellipsizes or auto-shrinks; the chip formats it compact instead
            // (CompactNumber in WriteAmount) and the content-fit sync grows layout chips.
            var amount = Label(go.transform, "0", 0f, 1f,
                primary ? ElarionUi.Gilt : ElarionUi.Parchment,
                primary ? ElarionUi.FontHead : ElarionUi.FontLabel,
                TextAlignmentOptions.MidlineRight, hasTag ? 0.60f : 0.32f, 0.94f, bold: primary);
            amount.raycastTarget = false;
            amount.textWrappingMode = TextWrappingModes.NoWrap;
            EnsureFont(amount, FontRole.Body);

            var handle = new CurrencyChipHandle
            {
                root = go, icon = icon, amount = amount, tag = tagLabel, plate = plate,
                amountBand = (hasTag ? 0.94f - 0.60f : 0.94f - 0.32f),
            };
            handle.SetAmount(0, animate: false);
            return handle;
        }

        // =====================================================================
        // §1.6 BuildActionSlot — Action_Bar_Slot + radial cooldown sweep.
        // =====================================================================

        /// <summary>Live handle of an action slot (§1.6). Serves the battle ability row, the
        /// assignable-extras bar AND the hot-swap town/world bars.</summary>
        public sealed class ActionSlotHandle
        {
            public GameObject root;
            /// <summary>The slot frame (chrome).</summary>
            public Image frame;
            /// <summary>The ability/consumable icon.</summary>
            public Image icon;
            /// <summary>The radial cooldown shade (Filled/Radial360, NON-NULL sprite — §1.1 law).</summary>
            public Image cdRing;
            /// <summary>Seconds-remaining readout over the sweep.</summary>
            public TMP_Text cdText;
            /// <summary>Stack/charge count (bottom-right).</summary>
            public TMP_Text count;
            /// <summary>The tap target.</summary>
            public Button button;
            /// <summary>Optional centered TEXT face (lazy — built on first <see cref="SetLabel"/>);
            /// null on the icon-only slots, which pay nothing for it.</summary>
            public TMP_Text label;

            private int _shownCd = int.MinValue, _shownCount = int.MinValue;

            public void SetIcon(Sprite s)
            {
                if (icon == null) return;
                // WO-611 F1-B (ABILITY_ICON_AUDIT_2026-07-05): a null sprite must NEVER blank an action
                // slot (owner "there must ALWAYS be an image"; HUD_OBSIDIAN §1 "null art can never blank
                // a surface"). Substitute the concept table's catch-all default (icon_combat) so a blank
                // ability/action slot is structurally impossible even when a future concept is unmapped.
                if (s == null) s = ConceptIconResolver.DefaultSprite();
                icon.sprite = s;
                // Text-label mode owns the face while active (SetLabel) — an icon refresh must
                // not re-enable the sprite underneath the words.
                icon.enabled = s != null && (label == null || !label.gameObject.activeSelf);
            }

            /// <summary>TEXT face mode — render centered word(s) on the slot INSTEAD of the icon
            /// sprite (owner placeholder directive 2026-07-11: "instead of the heroic leap image
            /// use word Dodge/Attack"). Text carries the meaning on the standard chrome — no
            /// color-only signalling (colorblind rule). Auto-sizes to fit multi-line inside the
            /// face; built lazily so every icon-only slot pays zero extra objects. Pass
            /// null/empty to leave text mode (then SetIcon restores the sprite face).</summary>
            public void SetLabel(string text)
            {
                bool has = !string.IsNullOrEmpty(text);
                if (!has)
                {
                    if (label != null && label.gameObject.activeSelf)
                    {
                        label.gameObject.SetActive(false);
                        if (icon != null) icon.enabled = icon.sprite != null;
                    }
                    return;
                }
                if (label == null)
                {
                    label = Label(root.transform, "", 0.14f, 0.86f, ElarionUi.Parchment,
                                  ElarionUi.FontLabel, TextAlignmentOptions.Center, 0.08f, 0.92f,
                                  bold: true);
                    label.raycastTarget = false;
                    EnsureFont(label, FontRole.Body);
                    label.enableAutoSizing = true;              // two-line fit inside the round face
                    label.fontSizeMax = ElarionUi.FontLabel;
                    label.fontSizeMin = 6f;
                    // Sit UNDER the cooldown seconds readout so a live sweep count stays on top.
                    if (cdText != null)
                        label.transform.SetSiblingIndex(cdText.transform.GetSiblingIndex());
                }
                label.gameObject.SetActive(true);
                if (label.text != text) label.text = text;
                if (icon != null) icon.enabled = false;         // words replace the sprite face
            }

            /// <summary>Drive the sweep: fillAmount = remaining/total (the only mutation), seconds
            /// label rebuilt only when the visible integer changes (mobile lens).</summary>
            public void SetCooldown(float remaining, float total)
            {
                if (cdRing == null) return;
                bool cooling = remaining > 0f && total > 0f;
                cdRing.fillAmount = cooling ? Mathf.Clamp01(remaining / Mathf.Max(0.01f, total)) : 0f;
                int secs = cooling ? Mathf.CeilToInt(remaining) : 0;
                if (cdText != null && secs != _shownCd)
                {
                    _shownCd = secs;
                    cdText.text = secs > 0 ? secs.ToString() : "";
                }
                if (button != null) button.interactable = !cooling;
            }

            public void SetCount(int n)
            {
                if (count == null || n == _shownCount) return;
                _shownCount = n;
                count.text = n > 1 ? n.ToString() : "";
            }
        }

        /// <summary>An action slot on the real Action_Bar_Slot art (prefab-mode first), with a
        /// radial-360 cooldown sweep whose Image ALWAYS has a sprite (§1.1 law applies to radial
        /// fills too). Null art ⇒ procedural dark cell + rim.</summary>
        public static ActionSlotHandle BuildActionSlot(Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, Action onTap = null)
        {
            var h = new ActionSlotHandle();

            // ── MODE 1: loader+binder ────────────────────────────────────────
            var pf = InstantiateBlinkPrefab(parent, anchorMin, anchorMax, "Action_Bar_Slot", "ActionBarSlot", "ActionSlot");
            if (pf != null)
            {
                h.root = pf;
                h.frame = pf.GetComponent<Image>();
                h.icon = FindDeep<Image>(pf.transform, "icon");
                h.button = pf.TryGetComponent<Button>(out var pfBtnH) ? pfBtnH : pf.AddComponent<Button>();
                if (h.button.targetGraphic == null) h.button.targetGraphic = h.frame;
            }
            else
            {
                // ── MODE 2: constructed ──────────────────────────────────────
                var go = new GameObject("ActionSlot", typeof(Image), typeof(Button));
                go.transform.SetParent(parent, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                h.root = go;
                h.frame = go.GetComponent<Image>();
                var slotSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleSlot, RpgUiCatalog.SlotAction);
                if (slotSprite != null)
                {
                    h.frame.sprite = slotSprite;
                    h.frame.type = Image.Type.Sliced;
                    h.frame.fillCenter = true;
                    h.frame.color = ChromeTint;                    // chrome
                }
                else
                {
                    h.frame.color = Cell;
                    ApplyRounded(h.frame);
                    AddInnerRim(go, AccentSoft);
                }
                h.button = go.GetComponent<Button>();
                h.button.targetGraphic = h.frame;
                StyleButtonColors(h.button);

                var iconGo = new GameObject("Icon", typeof(Image));
                iconGo.transform.SetParent(go.transform, false);
                var irt = (RectTransform)iconGo.transform;
                irt.anchorMin = new Vector2(0.12f, 0.12f); irt.anchorMax = new Vector2(0.88f, 0.88f);
                irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
                h.icon = iconGo.GetComponent<Image>();
                h.icon.preserveAspect = true;
                h.icon.raycastTarget = false;
                h.icon.enabled = false;
            }

            // Cooldown sweep — SHARED between modes (a prefab won't ship our cd semantics).
            var cdGo = new GameObject("CdRing", typeof(Image));
            cdGo.transform.SetParent(h.root.transform, false);
            var crt = (RectTransform)cdGo.transform;
            crt.anchorMin = new Vector2(0.06f, 0.06f); crt.anchorMax = new Vector2(0.94f, 0.94f);
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
            h.cdRing = cdGo.GetComponent<Image>();
            var cdSprite = CircleSprite;                            // §1.1: NON-NULL sprite on a Filled image
            if (cdSprite == null) cdSprite = SolidSprite;
            if (cdSprite == null) cdSprite = RoundedSprite;
            h.cdRing.sprite = cdSprite;
            h.cdRing.type = Image.Type.Filled;
            h.cdRing.fillMethod = Image.FillMethod.Radial360;
            h.cdRing.fillOrigin = (int)Image.Origin360.Top;
            h.cdRing.fillClockwise = false;
            h.cdRing.color = CdShade;
            h.cdRing.fillAmount = 0f;
            h.cdRing.raycastTarget = false;

            h.cdText = Label(h.root.transform, "", 0.18f, 0.82f, ElarionUi.Parchment,
                             ElarionUi.FontHead, TextAlignmentOptions.Center, 0f, 1f, bold: true);
            h.cdText.raycastTarget = false;

            h.count = Label(h.root.transform, "", 0.02f, 0.34f, ElarionUi.Gilt,
                            ElarionUi.FontMicro, TextAlignmentOptions.BottomRight, 0.50f, 0.94f, bold: true);
            h.count.raycastTarget = false;

            if (onTap != null) h.button.onClick.AddListener(() => onTap());
            return h;
        }

        // =====================================================================
        // §1.7 BuildCastBar — the telegraph UI.
        // =====================================================================

        /// <summary>Live handle of a cast bar (§1.7). Bind to a <see cref="CastModel"/> or drive
        /// SetCast/Show/Hide directly. Show/Hide fade eases (0.12s).</summary>
        public sealed class CastBarHandle
        {
            public GameObject root;
            /// <summary>The §1.1-contract fill.</summary>
            public Image fill;
            /// <summary>The "Caster: Ability" readout.</summary>
            public TMP_Text label;
            internal CanvasGroup group;

            private CastModel _bound;
            private Action _onChanged;
            private string _shownName;

            public void SetCast(string name, float t01)
            {
                if (fill != null) fill.fillAmount = Mathf.Clamp01(t01);   // the only mutation (§1.1)
                if (label != null && !string.Equals(name, _shownName, StringComparison.Ordinal))
                {
                    _shownName = name;
                    label.text = name ?? "";
                }
            }

            public void Show()
            {
                if (root == null) return;
                root.SetActive(true);
                if (group != null) UiKitTween.Value(group, group.alpha, 1f, 0.12f, a => { if (group != null) group.alpha = a; });
            }

            public void Hide()
            {
                if (root == null || group == null) { if (root != null) root.SetActive(false); return; }
                UiKitTween.Value(group, group.alpha, 0f, 0.12f,
                    a => { if (group != null) group.alpha = a; },
                    () => { if (root != null) root.SetActive(false); });
            }

            /// <summary>Bind to the Core CastModel: visibility + progress follow its Changed event
            /// (§1.1 rule 4 — VM-event-driven, never per-frame pulls).</summary>
            public void Bind(CastModel model)
            {
                Unbind();
                if (model == null) return;
                _bound = model;
                _onChanged = () =>
                {
                    if (_bound.Visible)
                    {
                        if (root != null && !root.activeSelf) Show();
                        SetCast(string.IsNullOrEmpty(_bound.CasterName)
                                    ? _bound.AbilityName
                                    : _bound.CasterName + ": " + _bound.AbilityName,
                                _bound.Progress01);
                    }
                    else Hide();
                };
                _bound.Changed += _onChanged;
                _onChanged();
            }

            /// <summary>Detach from the bound model (call before destroying the widget).</summary>
            public void Unbind()
            {
                if (_bound != null && _onChanged != null) _bound.Changed -= _onChanged;
                _bound = null; _onChanged = null;
            }
        }

        /// <summary>
        /// A cast/telegraph bar on the Cast_Bar_{1..3} art (§1.7). Prefab-mode ("CastBar1..3") first;
        /// constructed uses the DOC-MEASURED geometry (frame 800x56, fill 507x22 centred — the pack's
        /// own numbers, widget-params overrides when present). Fill obeys the §1.1 contract.
        /// </summary>
        public static CastBarHandle BuildCastBar(Transform parent, int style,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            style = Mathf.Clamp(style, 1, 3);
            var h = new CastBarHandle();

            // ── MODE 1: loader+binder ────────────────────────────────────────
            var pf = InstantiateBlinkPrefab(parent, anchorMin, anchorMax, "CastBar" + style, "Cast_Bar_" + style);
            if (pf != null)
            {
                var pfFill = FindDeep<Image>(pf.transform, "fill");
                if (pfFill != null)
                {
                    EnforceFillContract(pfFill);
                    h.root = pf;
                    h.fill = pfFill;
                    h.label = FindDeep<TMP_Text>(pf.transform, "text", "label", "name")
                              ?? Label(pf.transform, "", 0f, 1f, ElarionUi.Parchment, ElarionUi.FontLabel,
                                       TextAlignmentOptions.Center, 0.05f, 0.95f, bold: true);
                    h.label.raycastTarget = false;
                    FitSingleLine(h.label);                                // §1.14 — "Caster: Ability" never spills
                    h.group = pf.TryGetComponent<CanvasGroup>(out var pfGrp) ? pfGrp : pf.AddComponent<CanvasGroup>();
                    h.SetCast("", 0f);
                    return h;
                }
                FlowTrace.Warn("UI", "BuildCastBar: prefab 'CastBar" + style + "' has no *fill* child — constructing fallback");
                UnityEngine.Object.Destroy(pf);
            }

            // ── MODE 2: constructed (doc-measured geometry) ──────────────────
            var go = new GameObject("CastBar" + style, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            h.root = go;
            h.group = go.GetComponent<CanvasGroup>();

            // Fill geometry: widget-params → the doc's measured 800x56 frame / 507x22 fill (centred).
            Vector4 frac;
            if (!TryParamRect("CastBar" + style, "fill", out frac))
                frac = new Vector4(0.183f, 0.304f, 0.817f, 0.696f);   // (800-507)/2/800 , (56-22)/2/56

            var fillGo = new GameObject("Fill", typeof(Image));
            fillGo.transform.SetParent(go.transform, false);
            var frt = (RectTransform)fillGo.transform;
            frt.anchorMin = new Vector2(frac.x, frac.y);
            frt.anchorMax = new Vector2(frac.z, frac.w);
            frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
            h.fill = fillGo.GetComponent<Image>();
            h.fill.sprite = FillSpriteChain(RpgUiCatalog.HudBarCastFill);
            bool tintable = h.fill.sprite != RpgUiCatalog.Get(RpgUiCatalog.RoleHud, RpgUiCatalog.HudBarCastFill);
            h.fill.color = tintable ? ObsidianBarTint(ObsidianBarKind.Cast) : Color.white;
            EnforceFillContract(h.fill);
            h.fill.fillAmount = 0f;

            // Frame silhouette ABOVE the fill (Simple — ornate art never 9-slices).
            string frameName = style == 1 ? RpgUiCatalog.HudBarCast1
                             : style == 2 ? RpgUiCatalog.HudBarCast2 : RpgUiCatalog.HudBarCast3;
            var frameSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleHud, frameName);
            if (frameSprite != null)
            {
                var fgo = new GameObject("Frame", typeof(Image));
                fgo.transform.SetParent(go.transform, false);
                var fgrt = (RectTransform)fgo.transform;
                fgrt.anchorMin = Vector2.zero; fgrt.anchorMax = Vector2.one;
                fgrt.offsetMin = Vector2.zero; fgrt.offsetMax = Vector2.zero;
                var fimg = fgo.GetComponent<Image>();
                fimg.sprite = frameSprite;
                fimg.type = Image.Type.Simple;
                fimg.color = ChromeTint;                            // chrome
                fimg.raycastTarget = false;
            }
            else
            {
                // Null-art fallback: recessed track behind the fill so the bar still reads.
                var wellGo = Well(go.transform, Vector2.zero, Vector2.one);
                wellGo.transform.SetAsFirstSibling();
            }

            h.label = Label(go.transform, "", 0f, 1f, ElarionUi.Parchment, ElarionUi.FontLabel,
                            TextAlignmentOptions.Center, 0.05f, 0.95f, bold: true);
            h.label.raycastTarget = false;
            FitSingleLine(h.label);                                        // §1.14 — "Caster: Ability" never spills
            h.SetCast("", 0f);
            return h;
        }

        // =====================================================================
        // §1.9 BuildTab / BuildToggle / BuildSlider / BuildDropdown.
        // =====================================================================

        /// <summary>Live handle of a tab (§1.9) — SetSelected drives the arrow_box_on-style highlight.</summary>
        public sealed class TabHandle
        {
            public Button button;
            public TMP_Text label;
            internal GameObject selection;
            /// <summary>True when the selection highlight is the FULL gold arrow_box_on plate
            /// (label must go dark Ink for contrast); false = the gilt-underline fallback on the
            /// dark tab plate (label stays gold). CONTRAST LAW: gold plate ⇒ dark text, dark
            /// plate ⇒ gold/parchment text — luminance, never hue (owner is colorblind).</summary>
            internal bool selectionIsPlate;
            public void SetSelected(bool on)
            {
                if (selection != null) selection.SetActive(on);
                if (label != null)
                    label.color = on
                        ? (selectionIsPlate ? ElarionUi.Ink : ElarionUi.Gilt)
                        : ElarionUi.Parchment;
            }
        }

        /// <summary>A tab on the element_tab plate; selected state = an arrow_box_on-style highlight
        /// overlay (gilt underline fallback).</summary>
        public static TabHandle BuildTab(Transform parent, string label,
            Vector2 anchorMin, Vector2 anchorMax, Action onSelect = null)
        {
            var go = new GameObject("Tab_" + label, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>();
            var tabSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleElement, RpgUiCatalog.ElementTab);
            if (tabSprite != null)
            {
                img.sprite = tabSprite;
                img.type = Image.Type.Sliced;
                img.fillCenter = true;
                img.color = ChromeTint;                             // chrome
            }
            else { img.color = Glass; ApplyRounded(img); }

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            StyleButtonColors(btn);
            if (onSelect != null) btn.onClick.AddListener(() => onSelect());

            // Selection highlight: arrow_box_on plate when mirrored, gilt underline otherwise.
            GameObject sel;
            var selSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleElement, RpgUiCatalog.ElementArrowBoxOn);
            if (selSprite != null)
            {
                sel = new GameObject("Selected", typeof(Image));
                sel.transform.SetParent(go.transform, false);
                var srt = (RectTransform)sel.transform;
                srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
                srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;
                var simg = sel.GetComponent<Image>();
                simg.sprite = selSprite;
                simg.type = Image.Type.Sliced;
                simg.fillCenter = true;
                simg.color = ChromeTint;
                simg.raycastTarget = false;
            }
            else
            {
                sel = AddImage(go.transform, "Selected", new Vector2(0.08f, 0f), new Vector2(0.92f, 0.06f), ObsidianTrim);
                sel.GetComponent<Image>().raycastTarget = false;
            }
            sel.SetActive(false);

            var lbl = Label(go.transform, label ?? "", 0f, 1f, ElarionUi.Parchment,
                            ElarionUi.FontBody, TextAlignmentOptions.Center, 0.05f, 0.95f, bold: true);
            lbl.raycastTarget = false;
            EnsureFont(lbl, FontRole.Body);
            FitSingleLine(lbl);                                            // §1.14 — tab text never truncates mid-word

            return new TabHandle { button = btn, label = lbl, selection = sel,
                                   selectionIsPlate = selSprite != null };
        }

        /// <summary>Live handle of a toggle (§1.9).</summary>
        public sealed class ToggleHandle
        {
            public Button button;
            public bool IsOn { get; private set; }
            internal Image face;
            internal Sprite on, off;
            internal GameObject checkMark;
            internal Action<bool> onChanged;

            public void SetOn(bool value, bool notify = false)
            {
                IsOn = value;
                if (face != null && on != null && off != null) face.sprite = value ? on : off;
                if (checkMark != null) checkMark.SetActive(value);
                if (notify && onChanged != null) onChanged(value);
            }
        }

        /// <summary>A switch on the toggle_on/off art (or a checkbox on togglebox_on/off when
        /// <paramref name="checkbox"/>); procedural check-plate fallback. Fires onChanged on tap.</summary>
        public static ToggleHandle BuildToggle(Transform parent, bool initial, Action<bool> onChanged,
            Vector2 anchorMin, Vector2 anchorMax, bool checkbox = false)
        {
            var h = new ToggleHandle { onChanged = onChanged };
            var go = new GameObject(checkbox ? "Checkbox" : "Toggle", typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            h.face = go.GetComponent<Image>();
            h.on = RpgUiCatalog.Get(checkbox ? RpgUiCatalog.RoleElement : RpgUiCatalog.RoleButton,
                                    checkbox ? RpgUiCatalog.ElementToggleBoxOn : RpgUiCatalog.ButtonToggleOn);
            h.off = RpgUiCatalog.Get(checkbox ? RpgUiCatalog.RoleElement : RpgUiCatalog.RoleButton,
                                     checkbox ? RpgUiCatalog.ElementToggleBoxOff : RpgUiCatalog.ButtonToggleOff);
            if (h.on != null && h.off != null)
            {
                h.face.type = Image.Type.Simple;
                h.face.preserveAspect = true;
                h.face.color = ChromeTint;                          // chrome
            }
            else
            {
                // Procedural fallback: dark plate + gilt check chip.
                h.on = null; h.off = null;
                h.face.color = Cell;
                ApplyRounded(h.face);
                h.checkMark = AddImage(go.transform, "Check",
                    new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.8f), ObsidianTrim);
                h.checkMark.GetComponent<Image>().raycastTarget = false;
            }

            h.button = go.GetComponent<Button>();
            h.button.targetGraphic = h.face;
            StyleButtonColors(h.button);
            h.button.onClick.AddListener(() => h.SetOn(!h.IsOn, notify: true));
            h.SetOn(initial);
            return h;
        }

        /// <summary>A uGUI Slider skinned slider_bg / slider_fill / slider_handle; the fill obeys the
        /// §1.1 contract (Filled + non-null sprite — uGUI's Slider natively drives fillAmount then).</summary>
        public static Slider BuildSlider(Transform parent, float min, float max, float value,
            Action<float> onChanged, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            // Background track.
            var bgGo = new GameObject("Background", typeof(Image));
            bgGo.transform.SetParent(go.transform, false);
            var brt = (RectTransform)bgGo.transform;
            brt.anchorMin = new Vector2(0f, 0.30f); brt.anchorMax = new Vector2(1f, 0.70f);
            brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
            var bg = bgGo.GetComponent<Image>();
            var bgSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleButton, RpgUiCatalog.ButtonSliderBg);
            if (bgSprite != null) { bg.sprite = bgSprite; bg.type = Image.Type.Sliced; bg.fillCenter = true; bg.color = ChromeTint; }
            else { bg.color = Track; ApplyRounded(bg); }
            bg.raycastTarget = false;

            // Fill area + fill (§1.1: Filled + non-null sprite; the Slider drives fillAmount).
            var faGo = new GameObject("FillArea", typeof(RectTransform));
            faGo.transform.SetParent(go.transform, false);
            var fart = (RectTransform)faGo.transform;
            fart.anchorMin = new Vector2(0f, 0.30f); fart.anchorMax = new Vector2(1f, 0.70f);
            fart.offsetMin = Vector2.zero; fart.offsetMax = Vector2.zero;
            var fillGo = new GameObject("Fill", typeof(Image));
            fillGo.transform.SetParent(faGo.transform, false);
            var firt = (RectTransform)fillGo.transform;
            firt.anchorMin = Vector2.zero; firt.anchorMax = Vector2.one;
            firt.offsetMin = Vector2.zero; firt.offsetMax = Vector2.zero;
            var fillImg = fillGo.GetComponent<Image>();
            var fillSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleButton, RpgUiCatalog.ButtonSliderFill);
            fillImg.sprite = fillSprite != null ? fillSprite : FillSpriteChain(null);
            fillImg.color = fillSprite != null ? ChromeTint : ObsidianTrim;
            EnforceFillContract(fillImg);

            // Handle.
            var haGo = new GameObject("HandleArea", typeof(RectTransform));
            haGo.transform.SetParent(go.transform, false);
            var hart = (RectTransform)haGo.transform;
            hart.anchorMin = Vector2.zero; hart.anchorMax = Vector2.one;
            hart.offsetMin = Vector2.zero; hart.offsetMax = Vector2.zero;
            var hGo = new GameObject("Handle", typeof(Image));
            hGo.transform.SetParent(haGo.transform, false);
            var hrt = (RectTransform)hGo.transform;
            hrt.sizeDelta = new Vector2(34f, 0f);
            var hImg = hGo.GetComponent<Image>();
            var hSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleButton, RpgUiCatalog.ButtonSliderHandle);
            if (hSprite != null) { hImg.sprite = hSprite; hImg.type = Image.Type.Simple; hImg.preserveAspect = true; hImg.color = ChromeTint; }
            else { hImg.sprite = CircleSprite; hImg.color = ObsidianTrim; }

            var slider = go.GetComponent<Slider>();
            slider.targetGraphic = hImg;
            slider.fillRect = firt;
            slider.handleRect = hrt;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = Mathf.Clamp(value, min, max);
            slider.transition = Selectable.Transition.ColorTint;
            var cb = slider.colors; cb.fadeDuration = 0.12f; slider.colors = cb;   // eased state change
            if (onChanged != null) slider.onValueChanged.AddListener(v => onChanged(v));
            return slider;
        }

        /// <summary>Live handle of a lightweight code-built dropdown (§1.9).</summary>
        public sealed class DropdownHandle
        {
            public Button button;
            public TMP_Text label;
            internal GameObject listPanel;
            internal string[] options;
            public int Index { get; internal set; }

            public void SetIndex(int i)
            {
                if (options == null || options.Length == 0) return;
                Index = Mathf.Clamp(i, 0, options.Length - 1);
                if (label != null) label.text = options[Index];
            }
        }

        /// <summary>A lightweight dropdown on the dropdown_1..3 art (fallback dark plate): tap to
        /// expand an option list under the control, tap an option to select + collapse. Code-built
        /// (no TMP_Dropdown template dependency — WebGL-proven path).</summary>
        public static DropdownHandle BuildDropdown(Transform parent, string[] options,
            Action<int> onSelect, Vector2 anchorMin, Vector2 anchorMax)
        {
            var h = new DropdownHandle { options = options ?? Array.Empty<string>() };

            var go = new GameObject("Dropdown", typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>();
            var ddSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleButton, RpgUiCatalog.ButtonDropdown1);
            if (ddSprite != null) { img.sprite = ddSprite; img.type = Image.Type.Sliced; img.fillCenter = true; img.color = ChromeTint; }
            else { img.color = Cell; ApplyRounded(img); }

            h.button = go.GetComponent<Button>();
            h.button.targetGraphic = img;
            StyleButtonColors(h.button);

            h.label = Label(go.transform, h.options.Length > 0 ? h.options[0] : "", 0f, 1f,
                            ElarionUi.Parchment, ElarionUi.FontBody, TextAlignmentOptions.MidlineLeft,
                            0.08f, 0.82f, bold: false);
            h.label.raycastTarget = false;
            EnsureFont(h.label, FontRole.Body);
            FitSingleLine(h.label);                                        // §1.14 — option text never clips

            var caret = Label(go.transform, "v", 0f, 1f, ElarionUi.Gilt, ElarionUi.FontLabel,
                              TextAlignmentOptions.Center, 0.84f, 0.98f, bold: true);
            caret.raycastTarget = false;

            h.button.onClick.AddListener(() =>
            {
                if (h.listPanel != null) { UnityEngine.Object.Destroy(h.listPanel); h.listPanel = null; return; }
                if (h.options.Length == 0) return;
                // Expand: an option list stacked directly below the control (sibling, drawn last).
                var list = new GameObject("DropdownList", typeof(Image));
                list.transform.SetParent(go.transform, false);
                var lrt = (RectTransform)list.transform;
                lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(1f, 0f);
                lrt.pivot = new Vector2(0.5f, 1f);
                lrt.sizeDelta = new Vector2(0f, Mathf.Min(5, h.options.Length) * 52f);
                lrt.anchoredPosition = Vector2.zero;
                var limg = list.GetComponent<Image>();
                var popup = RpgUiCatalog.Get(RpgUiCatalog.RoleButton, RpgUiCatalog.ButtonPopup);
                if (popup != null) { limg.sprite = popup; limg.type = Image.Type.Sliced; limg.fillCenter = true; limg.color = ChromeTint; }
                else { limg.color = GlassDeep; ApplyRounded(limg); }
                list.transform.SetAsLastSibling();
                h.listPanel = list;

                int n = h.options.Length;
                for (int i = 0; i < n; i++)
                {
                    int idx = i;
                    float y1 = 1f - (float)i / n, y0 = 1f - (float)(i + 1) / n;
                    Button(list.transform, h.options[i], ButtonKind.Quiet,
                        new Vector2(0.03f, y0 + 0.01f), new Vector2(0.97f, y1 - 0.01f), () =>
                        {
                            h.SetIndex(idx);
                            if (h.listPanel != null) { UnityEngine.Object.Destroy(h.listPanel); h.listPanel = null; }
                            if (onSelect != null) onSelect(idx);
                        });
                }
            });

            h.SetIndex(0);
            return h;
        }

        // =====================================================================
        // §1.10 BuildTargetFrame / BuildNameplate.
        // =====================================================================

        /// <summary>Live handle of the HUD target block (§1.10). <see cref="Clear"/> empties EVERY
        /// field — the contract fix for the dead-bar-under-"No Target" (BattleHud9Zone.cs:549).</summary>
        public sealed class TargetFrameHandle
        {
            public GameObject root;
            public TMP_Text name;
            public TMP_Text level;
            /// <summary>Free-form extra line (threat / role).</summary>
            public TMP_Text extra;
            public BarHandle hp;
            internal Image plate;

            private TargetModel _bound;
            private Action _onChanged;

            public void Set(string targetName, int targetLevel, float cur, float max, string extraText = "")
            {
                if (name != null) name.text = targetName ?? "";
                if (level != null) level.text = targetLevel > 0 ? "Lv " + targetLevel : "";
                if (extra != null) extra.text = extraText ?? "";
                if (hp != null) hp.SetValue(cur, max);
                if (plate != null) plate.color = ChromeTint;
            }

            /// <summary>TOTAL clear (§1.10): name→"No Target", fill→0, level/value/extra→"", tint→neutral.
            /// Nothing of the dead target survives.</summary>
            public void Clear()
            {
                if (name != null) name.text = "No Target";
                if (level != null) level.text = "";
                if (extra != null) extra.text = "";
                if (hp != null)
                {
                    hp.SetImmediate(0f, 1f);
                    hp.ResetLabel();          // the value text goes fully blank, not "0/1"
                }
                if (plate != null)
                {
                    var c = ChromeTint; c.a *= 0.75f;   // neutral dim
                    plate.color = c;
                }
            }

            /// <summary>Bind the Core TargetModel: Set + show on HasTarget; Clear + HIDE otherwise.
            /// Owner F8 2026-07-07 ("the target should not appear if not a target") supersedes the
            /// visible-"No Target" law for model-bound frames: with no target the whole block (frame,
            /// lock badge child, buff row siblings it hosts) deactivates — the HUD shows nothing where
            /// there is nothing. Direct Set/Clear callers (9-zone HUD) keep their own visibility.</summary>
            public void Bind(TargetModel model)
            {
                Unbind();
                if (model == null) return;
                _bound = model;
                _onChanged = () =>
                {
                    if (_bound.HasTarget)
                    {
                        if (root != null && !root.activeSelf) root.SetActive(true);
                        Set(_bound.Name, _bound.Level, _bound.Hp, _bound.MaxHp,
                            _bound.Locked ? "LOCKED" : "");
                    }
                    else
                    {
                        Clear();   // stale content never flashes on the next show
                        if (root != null && root.activeSelf) root.SetActive(false);
                    }
                };
                _bound.Changed += _onChanged;
                _onChanged();
            }

            public void Unbind()
            {
                if (_bound != null && _onChanged != null) _bound.Changed -= _onChanged;
                _bound = null; _onChanged = null;
            }
        }

        /// <summary>The HUD target frame on hud/target_core (§1.10). Prefab-mode ("TargetNameplate")
        /// first; constructed fallback. Starts CLEARED.</summary>
        public static TargetFrameHandle BuildTargetFrame(Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var h = new TargetFrameHandle();

            // ── MODE 1: loader+binder ────────────────────────────────────────
            var pf = InstantiateBlinkPrefab(parent, anchorMin, anchorMax, "TargetNameplate", "Target_Nameplate", "TargetFrame");
            if (pf != null)
            {
                var pfFill = FindDeep<Image>(pf.transform, "healthfill", "hpfill", "fill");
                if (pfFill != null)
                {
                    EnforceFillContract(pfFill);
                    h.root = pf;
                    h.plate = pf.GetComponent<Image>();
                    h.hp = new BarHandle { track = (RectTransform)pfFill.transform.parent, fill = pfFill };
                    h.name = FindDeep<TMP_Text>(pf.transform, "name");
                    if (h.name == null)
                    {
                        h.name = Label(pf.transform, "", 0.55f, 0.95f, ElarionUi.Parchment,
                                       ElarionUi.FontHead, TextAlignmentOptions.Center, 0.05f, 0.95f, bold: true);
                        FlowTrace.Warn("UI", "BuildTargetFrame: prefab has no *Name* text — overlaid a kit label");
                    }
                    h.name.raycastTarget = false;
                    h.level = FindDeep<TMP_Text>(pf.transform, "level");
                    h.extra = null;
                    GuardSpriteNullImages(pf, h.plate);   // no-silent-white law (F8-31)
                    h.Clear();
                    return h;
                }
                FlowTrace.Warn("UI", "BuildTargetFrame: prefab has no *fill* child — constructing fallback");
                UnityEngine.Object.Destroy(pf);
            }

            // ── MODE 2: constructed ──────────────────────────────────────────
            var go = new GameObject("TargetFrame", typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            h.root = go;
            h.plate = go.GetComponent<Image>();
            var core = RpgUiCatalog.Get(RpgUiCatalog.RoleHud, RpgUiCatalog.HudTargetCore);
            if (core != null)
            {
                h.plate.sprite = core;
                h.plate.type = Image.Type.Simple;   // ornate core — never slice
                h.plate.color = ChromeTint;
            }
            else { h.plate.color = GlassDeep; ApplyRounded(h.plate); }
            h.plate.raycastTarget = false;

            h.name = Label(go.transform, "", 0.60f, 0.95f, ElarionUi.Parchment,
                           ElarionUi.FontHead, TextAlignmentOptions.Center, 0.12f, 0.88f, bold: true);
            FitSingleLine(h.name);                                         // §1.14 — long target names ellipsize
            h.level = Label(go.transform, "", 0.60f, 0.95f, ElarionUi.Gilt,
                            ElarionUi.FontLabel, TextAlignmentOptions.MidlineLeft, 0.03f, 0.20f, bold: true);
            h.extra = Label(go.transform, "", 0.02f, 0.24f, ElarionUi.ParchmentDim,
                            ElarionUi.FontMicro, TextAlignmentOptions.Center, 0.12f, 0.88f);
            h.hp = BuildObsidianBar(go.transform, ObsidianBarKind.Health,
                new Vector2(0.10f, 0.28f), new Vector2(0.90f, 0.56f), withValue: true, framed: false);
            h.Clear();
            return h;
        }

        /// <summary>No-silent-white law (F8-31): a mirrored prefab with a dangling sprite GUID
        /// deserializes as sprite==null and uGUI paints the Image as a flat tinted RECTANGLE —
        /// the "white target frame". After binding a prefab, sweep its whole subtree: any Image
        /// left with a null sprite is re-bound from RpgUiCatalog on a role match (the root plate
        /// → hud/target_core) or DISABLED loudly — never rendered as a silent white quad.</summary>
        private static void GuardSpriteNullImages(GameObject root, Image plate)
        {
            if (root == null) return;
            var images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                var img = images[i];
                if (img == null || img.sprite != null) continue;

                // Role match: the root plate gets the committed target-core art.
                if (img == plate)
                {
                    var core = RpgUiCatalog.Get(RpgUiCatalog.RoleHud, RpgUiCatalog.HudTargetCore);
                    if (core != null)
                    {
                        img.sprite = core;
                        img.type = Image.Type.Simple;   // ornate core — never slice
                        FlowTrace.Warn("UiKit", "sprite-null plate Image " + ImagePath(root.transform, img.transform)
                                              + " re-bound to hud/target_core — dangling ref");
                        continue;
                    }
                }

                img.enabled = false;
                FlowTrace.Warn("UiKit", "sprite-null Image " + ImagePath(root.transform, img.transform)
                                      + " disabled — dangling ref");
            }
        }

        /// <summary>Root-relative transform path for guard logs.</summary>
        private static string ImagePath(Transform root, Transform t)
        {
            if (t == null) return "<null>";
            var path = t.name;
            for (var p = t.parent; p != null && p != root.parent; p = p.parent)
                path = p.name + "/" + path;
            return path;
        }

        /// <summary>Which nameplate flavour to build (§1.10).</summary>
        public enum NameplateKind { Player, Party, Enemy, Neutral, Rare, Boss }

        /// <summary>Live handle of a nameplate (world-space FloatingHealthBar restyle + the party rows).</summary>
        public sealed class NameplateHandle
        {
            public GameObject root;
            public Image plate;
            public Image portrait;
            public Image portraitBorder;
            /// <summary>Rare/Boss rank border overlay (null for common kinds).</summary>
            public Image rankBorder;
            public TMP_Text name;
            public BarHandle hp;
            /// <summary>Mana bar — Player/Party kinds only (null otherwise).</summary>
            public BarHandle mp;

            public void SetName(string n) { if (name != null) name.text = n ?? ""; }
        }

        /// <summary>The fill art name a nameplate kind's HP uses (colored pack art, untinted).</summary>
        private static string NameplateHpFill(NameplateKind kind)
        {
            switch (kind)
            {
                case NameplateKind.Enemy:
                case NameplateKind.Rare:
                case NameplateKind.Boss:    return RpgUiCatalog.HudNameplateHealthEnemy;
                case NameplateKind.Neutral: return RpgUiCatalog.HudNameplateHealthNeutral;
                default:                    return RpgUiCatalog.HudNameplateHealth;
            }
        }

        /// <summary>
        /// A nameplate (§1.10): Party/Player = the full HP/MP plate (prefab-mode "PartyNameplate" —
        /// doc-verified as a correctly-Filled plate — first); Enemy/Neutral/Rare/Boss = the compact
        /// world plate on nameplate_bar / nameplate_enemy_bg with the kind's coloured fill; Rare/Boss
        /// add their border overlay. All fills obey §1.1 (converts the FloatingHealthBar sizeDelta
        /// pattern at P3's call sites).
        /// </summary>
        public static NameplateHandle BuildNameplate(Transform parent, NameplateKind kind,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var h = new NameplateHandle();
            bool friendly = kind == NameplateKind.Player || kind == NameplateKind.Party;

            // ── MODE 1: loader+binder (the doc verified PartyNameplate ships complete) ──
            var pf = friendly
                ? InstantiateBlinkPrefab(parent, anchorMin, anchorMax, "PartyNameplate", "Party_Nameplate", "PlayerNameplate")
                : InstantiateBlinkPrefab(parent, anchorMin, anchorMax, "EnemyNameplate", "Enemy_Nameplate", "Nameplate");
            if (pf != null)
            {
                var pfHp = FindDeep<Image>(pf.transform, "healthfill", "hpfill");
                if (pfHp != null)
                {
                    EnforceFillContract(pfHp);
                    h.root = pf;
                    h.plate = pf.GetComponent<Image>();
                    h.hp = new BarHandle { track = (RectTransform)pfHp.transform.parent, fill = pfHp };
                    var pfMp = FindDeep<Image>(pf.transform, "manafill", "mpfill");
                    if (pfMp != null)
                    {
                        EnforceFillContract(pfMp);
                        h.mp = new BarHandle { track = (RectTransform)pfMp.transform.parent, fill = pfMp };
                    }
                    h.name = FindDeep<TMP_Text>(pf.transform, "playername", "name");
                    if (h.name == null)
                    {
                        h.name = Label(pf.transform, "", 0.55f, 0.95f, ElarionUi.Parchment,
                                       ElarionUi.FontHead, TextAlignmentOptions.MidlineLeft, 0.30f, 0.95f, bold: true);
                        FlowTrace.Warn("UI", "BuildNameplate(" + kind + "): prefab has no *Name* text — overlaid a kit label");
                    }
                    h.name.raycastTarget = false;
                    h.portrait = FindDeep<Image>(pf.transform, "portrait");
                    h.hp.SetImmediate(1f, 1f);
                    if (h.mp != null) h.mp.SetImmediate(1f, 1f);
                    return h;
                }
                FlowTrace.Warn("UI", "BuildNameplate(" + kind + "): prefab has no *HealthFill* — constructing fallback");
                UnityEngine.Object.Destroy(pf);
            }

            // ── MODE 2: constructed ──────────────────────────────────────────
            var go = new GameObject("Nameplate_" + kind, typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            h.root = go;
            h.plate = go.GetComponent<Image>();
            var plateSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleHud,
                friendly ? RpgUiCatalog.HudNameplateParty
                         : kind == NameplateKind.Neutral ? RpgUiCatalog.HudNameplateBar
                         : RpgUiCatalog.HudNameplateEnemyBg);
            if (plateSprite == null) plateSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleHud, RpgUiCatalog.HudNameplateBar);
            if (plateSprite != null)
            {
                h.plate.sprite = plateSprite;
                h.plate.type = Image.Type.Simple;   // nameplate silhouettes never slice (§2 rule)
                h.plate.color = ChromeTint;
            }
            else { h.plate.color = new Color(0.10f, 0.09f, 0.11f, 0.96f); ApplyRounded(h.plate); }
            h.plate.raycastTarget = false;

            if (friendly)
            {
                // Portrait socket (left) — nameplate_portrait + portrait_border, kit Portrait fallback.
                var wrap = AddImage(go.transform, "PortraitWrap",
                    new Vector2(0.035f, 0.12f), new Vector2(0.26f, 0.94f), new Color(0f, 0f, 0f, 0f), rounded: false);
                wrap.GetComponent<Image>().raycastTarget = false;
                var portSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleHud, RpgUiCatalog.HudNameplatePortrait);
                var borderSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleHud, RpgUiCatalog.HudPortraitBorder);
                if (portSprite != null)
                {
                    var pgo = new GameObject("Portrait", typeof(Image));
                    pgo.transform.SetParent(wrap.transform, false);
                    var prt = (RectTransform)pgo.transform;
                    prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
                    prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
                    h.portrait = pgo.GetComponent<Image>();
                    h.portrait.sprite = portSprite;
                    h.portrait.preserveAspect = true;
                    h.portrait.raycastTarget = false;
                    if (borderSprite != null)
                    {
                        var bgo = new GameObject("PortraitBorder", typeof(Image));
                        bgo.transform.SetParent(wrap.transform, false);
                        var bprt = (RectTransform)bgo.transform;
                        bprt.anchorMin = Vector2.zero; bprt.anchorMax = Vector2.one;
                        bprt.offsetMin = Vector2.zero; bprt.offsetMax = Vector2.zero;
                        h.portraitBorder = bgo.GetComponent<Image>();
                        h.portraitBorder.sprite = borderSprite;
                        h.portraitBorder.preserveAspect = true;
                        h.portraitBorder.color = ChromeTint;
                        h.portraitBorder.raycastTarget = false;
                    }
                }
                else
                {
                    var port = Portrait(wrap.transform, null, active: kind == NameplateKind.Player);
                    h.portrait = port.image;
                    h.portraitBorder = port.ring;
                }

                h.name = Label(go.transform, "", 0.52f, 0.95f, new Color(0.95f, 0.88f, 0.62f),
                               ElarionUi.FontHead, TextAlignmentOptions.MidlineLeft, 0.31f, 0.97f, bold: true);
                h.name.enableAutoSizing = true; h.name.fontSizeMin = 30f; h.name.fontSizeMax = 64f;  // mobile floor (was 9–15, sub-legible)
                h.name.raycastTarget = false;

                h.hp = BuildObsidianBar(go.transform, ObsidianBarKind.Health,
                    new Vector2(0.31f, 0.30f), new Vector2(0.985f, 0.50f), withValue: true, framed: false);
                h.mp = BuildObsidianBar(go.transform, ObsidianBarKind.Mana,
                    new Vector2(0.31f, 0.07f), new Vector2(0.985f, 0.27f), withValue: false, framed: false);
            }
            else
            {
                // Compact world plate: name over a single HP bar with the kind's coloured fill.
                h.name = Label(go.transform, "", 0.52f, 0.98f, ElarionUi.Parchment,
                               ElarionUi.FontLabel, TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);
                h.name.raycastTarget = false;
                FitSingleLine(h.name);                                     // §1.14 — long enemy titles ellipsize
                h.hp = BuildObsidianBar(go.transform, ObsidianBarKind.Health,
                    new Vector2(0.06f, 0.10f), new Vector2(0.94f, 0.48f), withValue: false, framed: false);
                var kindFill = RpgUiCatalog.Get(RpgUiCatalog.RoleHud, NameplateHpFill(kind));
                if (kindFill != null) { h.hp.fill.sprite = kindFill; h.hp.fill.color = Color.white; }
                else if (kind == NameplateKind.Neutral) h.hp.fill.color = new Color(0.92f, 0.80f, 0.25f, 1f);

                // Rank border overlay (rare/boss).
                string rank = kind == NameplateKind.Rare ? RpgUiCatalog.HudNameplateRare
                            : kind == NameplateKind.Boss ? RpgUiCatalog.HudNameplateBoss : null;
                if (rank != null)
                {
                    var rankSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleHud, rank);
                    if (rankSprite != null)
                    {
                        var rgo = new GameObject("RankBorder", typeof(Image));
                        rgo.transform.SetParent(go.transform, false);
                        var rrt = (RectTransform)rgo.transform;
                        rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one;
                        rrt.offsetMin = Vector2.zero; rrt.offsetMax = Vector2.zero;
                        h.rankBorder = rgo.GetComponent<Image>();
                        h.rankBorder.sprite = rankSprite;
                        h.rankBorder.type = Image.Type.Simple;
                        h.rankBorder.color = ChromeTint;
                        h.rankBorder.raycastTarget = false;
                        rgo.transform.SetAsLastSibling();
                    }
                    else
                    {
                        AddInnerRim(go, kind == NameplateKind.Boss
                            ? new Color(0.85f, 0.25f, 0.20f, 1f)
                            : new Color(0.32f, 0.58f, 0.92f, 1f));
                    }
                }
            }

            h.hp.SetImmediate(1f, 1f);
            if (h.mp != null) h.mp.SetImmediate(1f, 1f);
            return h;
        }

        // =====================================================================
        // §1.11 BuildControllerCluster — FOUR ROUND buttons (owner mockup).
        // =====================================================================

        /// <summary>Live handle of the movement cluster (§1.11).</summary>
        public sealed class ControllerHandle
        {
            public GameObject root;
            public Button up, down, left, right;
            /// <summary>The currently-held direction (zero when released). Also pushed via onMove.</summary>
            public Vector2 Current { get; internal set; }
        }

        /// <summary>Hold-state press behaviour for one cluster button: reports down/up and eases a
        /// press SQUASH (scale 1→0.86→1, quad-eased in Update — alloc-free).</summary>
        private sealed class UiKitHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
        {
            public Action onDown, onUp;
            private float _target = 1f;

            public void OnPointerDown(PointerEventData e) { _target = 0.86f; onDown?.Invoke(); }
            public void OnPointerUp(PointerEventData e)   { _target = 1f;    onUp?.Invoke(); }
            public void OnPointerExit(PointerEventData e) { if (_target < 1f) { _target = 1f; onUp?.Invoke(); } }

            private void Update()
            {
                float s = transform.localScale.x;
                if (Mathf.Approximately(s, _target)) return;
                s = Mathf.Lerp(s, _target, 1f - Mathf.Pow(0.0001f, Time.unscaledDeltaTime)); // eased, framerate-safe
                if (Mathf.Abs(s - _target) < 0.005f) s = _target;
                transform.localScale = new Vector3(s, s, 1f);
            }
        }

        /// <summary>
        /// The FOUR ROUND movement buttons (§1.11 — replaces the BL square D-pad + VirtualDPadLean).
        /// Round Obsidian faces (menu_btn_1/2 → arrow-iconed; kit disc + steel tint fallback, NEVER a
        /// square), diamond layout, ≥56px touch targets (88 reference px per button at the kit's
        /// 1080-wide reference), eased press squash. <paramref name="onMove"/> receives the held
        /// direction vector on every press-state change (zero on release).
        /// </summary>
        public static ControllerHandle BuildControllerCluster(Transform parent, Vector2 anchor, Action<Vector2> onMove)
        {
            const float Btn = 88f;      // reference px — ~2x the 44px minimum, thumb-reach sized
            const float Span = Btn * 2.6f;

            var go = new GameObject("ControllerCluster", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchor; rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(Span, Span);
            rt.anchoredPosition = Vector2.zero;

            var h = new ControllerHandle { root = go };

            Sprite face = RpgUiCatalog.Get(RpgUiCatalog.RoleElement, RpgUiCatalog.ElementMenuBtn1);
            if (face == null) face = RpgUiCatalog.Get(RpgUiCatalog.RoleElement, RpgUiCatalog.ElementMenuBtn2);
            bool packFace = face != null;
            if (face == null) face = CircleSprite;   // round fallback — never square
            Sprite arrow = RpgUiCatalog.Get(RpgUiCatalog.RoleButton, RpgUiCatalog.ButtonArrow);

            Button Make(string name, Vector2 pos, float arrowDeg, string glyph, Vector2 dir)
            {
                var bgo = new GameObject(name, typeof(Image), typeof(Button), typeof(UiKitHoldButton));
                bgo.transform.SetParent(go.transform, false);
                var brt = (RectTransform)bgo.transform;
                brt.anchorMin = new Vector2(0.5f, 0.5f); brt.anchorMax = new Vector2(0.5f, 0.5f);
                brt.sizeDelta = new Vector2(Btn, Btn);
                brt.anchoredPosition = pos;
                var img = bgo.GetComponent<Image>();
                img.sprite = face;
                img.preserveAspect = true;
                img.color = packFace ? ChromeTint : new Color(0.30f, 0.32f, 0.37f, 0.95f); // forged steel
                var btn = bgo.GetComponent<Button>();
                btn.targetGraphic = img;
                StyleButtonColors(btn);
                if (arrow != null)
                {
                    var ago = new GameObject("Arrow", typeof(Image));
                    ago.transform.SetParent(bgo.transform, false);
                    var art = (RectTransform)ago.transform;
                    art.anchorMin = new Vector2(0.24f, 0.24f); art.anchorMax = new Vector2(0.76f, 0.76f);
                    art.offsetMin = Vector2.zero; art.offsetMax = Vector2.zero;
                    art.localRotation = Quaternion.Euler(0f, 0f, arrowDeg);
                    var aimg = ago.GetComponent<Image>();
                    aimg.sprite = arrow;
                    aimg.preserveAspect = true;
                    aimg.color = ChromeTint;
                    aimg.raycastTarget = false;
                }
                else
                {
                    var lbl = Label(bgo.transform, glyph, 0f, 1f, ElarionUi.Parchment,
                                    ElarionUi.FontHead, TextAlignmentOptions.Center, 0f, 1f, bold: true);
                    lbl.raycastTarget = false;
                }
                var hold = bgo.GetComponent<UiKitHoldButton>();
                hold.onDown = () => { h.Current = dir; onMove?.Invoke(dir); };
                hold.onUp   = () => { if (h.Current == dir) { h.Current = Vector2.zero; onMove?.Invoke(Vector2.zero); } };
                return btn;
            }

            float off = Btn * 0.80f;
            h.up    = Make("Up",    new Vector2(0f,  off), 90f,  "^", Vector2.up);
            h.down  = Make("Down",  new Vector2(0f, -off), -90f, "v", Vector2.down);
            h.left  = Make("Left",  new Vector2(-off, 0f), 180f, "<", Vector2.left);
            h.right = Make("Right", new Vector2( off, 0f), 0f,   ">", Vector2.right);
            return h;
        }

        // =====================================================================
        // §1.12 BuildChatDock — collapsible chat/ranks/music dock.
        // =====================================================================

        /// <summary>Live handle of the chat dock (§1.12 — absorbs SocialAccessCluster).</summary>
        public sealed class ChatDockHandle
        {
            public GameObject root;
            /// <summary>The collapse/expand toggle.</summary>
            public Button toggle;
            /// <summary>Chat / Ranks / Music entry buttons.</summary>
            public Button[] entries;
            internal GameObject entriesRow;
            internal Image toggleImg;
            internal Sprite collapseSprite, expandSprite;
            public bool Expanded { get; private set; } = true;

            public void SetExpanded(bool on)
            {
                Expanded = on;
                if (entriesRow != null) entriesRow.SetActive(on);
                if (toggleImg != null && collapseSprite != null && expandSprite != null)
                    toggleImg.sprite = on ? collapseSprite : expandSprite;
            }
        }

        /// <summary>The collapsible dock on hud/chat_core + chat_element buttons (§1.12). Visibility
        /// per space type (hidden in BuildMode) is the consumers' rules-table job, not the widget's.</summary>
        public static ChatDockHandle BuildChatDock(Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Action onChat = null, Action onRanks = null, Action onMusic = null)
        {
            var h = new ChatDockHandle();
            var go = new GameObject("ChatDock", typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            h.root = go;

            var plate = go.GetComponent<Image>();
            var coreSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleHud, RpgUiCatalog.HudChatCore);
            if (coreSprite != null)
            {
                plate.sprite = coreSprite;
                plate.type = Image.Type.Sliced;   // chat_core is 9-sliced (border 48, §2)
                plate.fillCenter = true;
                plate.color = ChromeTint;
            }
            else { plate.color = Glass; ApplyRounded(plate); }
            plate.raycastTarget = false;

            // Entries row (collapsible).
            var row = new GameObject("Entries", typeof(RectTransform));
            row.transform.SetParent(go.transform, false);
            var rrt = (RectTransform)row.transform;
            rrt.anchorMin = new Vector2(0.02f, 0.10f); rrt.anchorMax = new Vector2(0.82f, 0.90f);
            rrt.offsetMin = Vector2.zero; rrt.offsetMax = Vector2.zero;
            h.entriesRow = row;

            Button Entry(int i, string label, string elementName, Action onTap)
            {
                float x0 = i / 3f + 0.01f, x1 = (i + 1) / 3f - 0.01f;
                var art = RpgUiCatalog.Get(RpgUiCatalog.RoleButton, elementName);
                if (art != null)
                {
                    var ego = new GameObject("Entry_" + label, typeof(Image), typeof(Button));
                    ego.transform.SetParent(row.transform, false);
                    var ert = (RectTransform)ego.transform;
                    ert.anchorMin = new Vector2(x0, 0f); ert.anchorMax = new Vector2(x1, 1f);
                    ert.offsetMin = Vector2.zero; ert.offsetMax = Vector2.zero;
                    var eimg = ego.GetComponent<Image>();
                    eimg.sprite = art;
                    eimg.type = Image.Type.Sliced;
                    eimg.fillCenter = true;
                    eimg.color = ChromeTint;
                    var ebtn = ego.GetComponent<Button>();
                    ebtn.targetGraphic = eimg;
                    StyleButtonColors(ebtn);
                    if (onTap != null) ebtn.onClick.AddListener(() => onTap());
                    var lbl = Label(ego.transform, label, 0f, 1f, ElarionUi.Parchment,
                                    ElarionUi.FontMicro, TextAlignmentOptions.Center, 0f, 1f, bold: true);
                    lbl.raycastTarget = false;
                    FitSingleLine(lbl);                                    // §1.14
                    return ebtn;
                }
                return Button(row.transform, label, ButtonKind.Quiet,
                              new Vector2(x0, 0f), new Vector2(x1, 1f), onTap);
            }

            h.entries = new[]
            {
                Entry(0, "Chat",  RpgUiCatalog.ButtonChatElement1, onChat),
                Entry(1, "Ranks", RpgUiCatalog.ButtonChatElement2, onRanks),
                Entry(2, "Music", RpgUiCatalog.ButtonChatElement3, onMusic),
            };

            // Collapse/expand toggle (right edge).
            h.collapseSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleHud, RpgUiCatalog.HudCollapse);
            h.expandSprite   = RpgUiCatalog.Get(RpgUiCatalog.RoleHud, RpgUiCatalog.HudExpand);
            var tgo = new GameObject("Toggle", typeof(Image), typeof(Button));
            tgo.transform.SetParent(go.transform, false);
            var trt = (RectTransform)tgo.transform;
            trt.anchorMin = new Vector2(0.84f, 0.15f); trt.anchorMax = new Vector2(0.98f, 0.85f);
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            h.toggleImg = tgo.GetComponent<Image>();
            if (h.collapseSprite != null)
            {
                h.toggleImg.sprite = h.collapseSprite;
                h.toggleImg.preserveAspect = true;
                h.toggleImg.color = ChromeTint;
            }
            else { h.toggleImg.color = Cell; ApplyRounded(h.toggleImg); }
            h.toggle = tgo.GetComponent<Button>();
            h.toggle.targetGraphic = h.toggleImg;
            StyleButtonColors(h.toggle);
            if (h.collapseSprite == null)
            {
                var lbl = Label(tgo.transform, "<>", 0f, 1f, ElarionUi.Gilt, ElarionUi.FontLabel,
                                TextAlignmentOptions.Center, 0f, 1f, bold: true);
                lbl.raycastTarget = false;
            }
            h.toggle.onClick.AddListener(() => h.SetExpanded(!h.Expanded));

            h.SetExpanded(true);
            return h;
        }

        // =====================================================================
        // §1.13 Role fonts — Merriweather titles / Alata body / Acme stamps.
        // =====================================================================

        /// <summary>Typographic role (§1.13): Title = Merriweather Bold, Body = Alata (the default),
        /// Stamp = Acme (combat text / toast headlines).</summary>
        public enum FontRole { Title, Body, Stamp }

        private static readonly TMP_FontAsset[] _roleFonts = new TMP_FontAsset[3];
        private static readonly bool[] _roleFontTried = new bool[3];

        /// <summary>The TMP asset for a role (BlinkFontImporter-generated, committed under
        /// Resources/RpgUi/font). Null when not generated — callers fall through the default chain.</summary>
        public static TMP_FontAsset FontFor(FontRole role)
        {
            int i = (int)role;
            if (!_roleFontTried[i])
            {
                _roleFontTried[i] = true;
                string name = role == FontRole.Title ? RpgUiCatalog.FontTitleAsset
                            : role == FontRole.Stamp ? RpgUiCatalog.FontStampAsset
                            : RpgUiCatalog.FontBodyAsset;
                _roleFonts[i] = Guard.Try("UI", "load role font " + name,
                    () => Resources.Load<TMP_FontAsset>(RpgUiCatalog.FontRoot + name), null);
                if (_roleFonts[i] == null)
                    FlowTrace.Warn("UI", "role font '" + name + "' not generated yet (BlinkFontImporter, P0) — using the default chain");
            }
            return _roleFonts[i];
        }

        /// <summary>Role overload of EnsureFont (§1.13): assign the role's TMP asset sprite-first;
        /// when absent, preserve the existing fallback chain (TMP default → LiberationSans → warn).</summary>
        public static void EnsureFont(TMP_Text t, FontRole role)
        {
            if (t == null) return;
            var f = FontFor(role);
            if (f != null) { t.font = f; return; }
            var ugui = t as TextMeshProUGUI;
            if (ugui != null) EnsureFont(ugui);   // the proven default chain (ElarionUiKit.EnsureFont)
        }

        // =====================================================================
        // §1.14 TEXT-FIT + FIT-OR-SCROLL (owner F8 flags 2026-07-06, flag_06:
        // "on this size i need scrollable area on all menus" + "needs formatted
        // to text size"). Kit-level overflow protection: every label fits its
        // rect (bounded auto-size, never below the mobile-legibility floor,
        // ellipsis after that) and every list zone can become a vertical
        // scroller with ONE call — no per-screen scroll plumbing.
        // =====================================================================

        /// <summary>The mobile-legibility floor in reference px (1080x1920 canvas). Matches the
        /// proven "mobile floor" used by the nameplate name auto-size (fontSizeMin 30 — the
        /// mobile-legible ladder commit; ElarionUi.FontMicro=32 is the smallest authored role).
        /// Auto-sizing never shrinks text below this — past the floor, single-line labels
        /// ellipsize instead of becoming unreadable or overlapping siblings.</summary>
        public const float FontFloor = 30f;

        /// <summary>The ABSOLUTE last-resort readability floor for the post-layout guard (below).
        /// The guard exists to prevent a whole line being CULLED when a rect is too thin to seat
        /// the 30px target — but its old 12px escape hatch produced sub-legible phone text (F8
        /// 2026-07-08 "text will never be able to be seen on mobile at this size": Sylas floored
        /// 24->13, Affiliation 13->12, Body 17->15). The guard now never relaxes below THIS —
        /// ~20px reference on a 1080-wide phone is the smallest still-readable size; a band that
        /// cannot even seat 20 is a LAYOUT bug and the guard's render assert FlowTrace.Fails it
        /// (rather than silently shrinking to unreadable). Target stays FontFloor(30); this is the
        /// floor of the relaxation range, not the goal.</summary>
        public const float FontHardFloor = 20f;

        /// <summary>
        /// Overflow-protect a SINGLE-LINE label (tab / button / row name / title / price):
        /// no wrap, bounded TMP auto-size [minSize..maxSize], then Ellipsis. Defaults:
        /// maxSize = the label's current fontSize (never grows), minSize = the FontFloor
        /// (clamped to maxSize when the label is already authored smaller). This is the
        /// structural fix for the flag_06 class of bug — "BU SEL" tab clips, titles cut
        /// mid-glyph, "Requires Lv" stacked over item names.
        /// </summary>
        public static void FitSingleLine(TMP_Text t, float minSize = 0f, float maxSize = 0f)
        {
            if (t == null) return;
            if (maxSize <= 0f) maxSize = t.fontSize;
            if (minSize <= 0f) minSize = FontFloor;
            // WO-714 P7 (WO-693 mobile floor, factory-enforced): no caller may auto-shrink text
            // below the FontHardFloor readability floor — an explicit sub-floor minSize is clamped
            // UP here (ellipsis past the floor, never sub-legible phone text).
            if (minSize < FontHardFloor) minSize = FontHardFloor;
            if (minSize > maxSize) minSize = maxSize;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.enableAutoSizing = true;
            t.fontSizeMin = minSize;
            t.fontSizeMax = maxSize;
            ArmFitGuard(t);
        }

        /// <summary>
        /// Overflow-protect a MULTI-LINE block (description / flavour / status copy):
        /// normal wrap, bounded auto-size [minSize..maxSize], then Truncate (never paints
        /// past its rect onto siblings). Same defaults as <see cref="FitSingleLine"/>.
        /// </summary>
        public static void FitBlock(TMP_Text t, float minSize = 0f, float maxSize = 0f)
        {
            if (t == null) return;
            if (maxSize <= 0f) maxSize = t.fontSize;
            if (minSize <= 0f) minSize = FontFloor;
            // WO-714 P7: same factory floor as FitSingleLine — never below FontHardFloor.
            if (minSize < FontHardFloor) minSize = FontHardFloor;
            if (minSize > maxSize) minSize = maxSize;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Truncate;
            t.enableAutoSizing = true;
            t.fontSizeMin = minSize;
            t.fontSizeMax = maxSize;
            ArmFitGuard(t);
        }

        /// <summary>Attach (or re-arm) the §1.14 post-layout guard on a fitted label.</summary>
        private static void ArmFitGuard(TMP_Text t)
        {
            if (t == null || !Application.isPlaying) return;
            var g = t.GetComponent<UiKitTextFitGuard>();
            if (g == null) g = t.gameObject.AddComponent<UiKitTextFitGuard>();
            g.Arm();
        }

        /// <summary>
        /// §1.14 post-layout guard — the "no dead buttons" backstop. PROVEN CAUSE (orchestrator
        /// capture 2026-07-06, panel_PartyShop.png: BUY/SELL + chip strips drew as BARE PLATES):
        /// TMP's Ellipsis overflow CULLS THE ENTIRE LINE when the line height at fontSizeMin
        /// exceeds the label rect height — on a 16:9 landscape window the modal canvas reference
        /// height is ~1080 (match 0.5), so the tab band is ~30px and the 30px FontFloor's ~38px
        /// line renders ZERO glyphs. The rect is unknowable at build time (layout hasn't run),
        /// so this one-shot component checks AFTER the first layout pass: if the floor's line
        /// cannot seat in the band it RELAXES fontSizeMin to fit the height (never below
        /// FontHardFloor — the mobile-readable last resort; a band too thin to seat even that is a
        /// LAYOUT bug the render assert Fails, NOT something to shrink into illegibility), then
        /// asserts visible glyphs and FlowTraces
        /// rect + fontSize + characterCount either way a rescue/failure happened. Disables itself
        /// after one verified pass; FitSingleLine/FitBlock re-arm it on re-fit.
        /// </summary>
        private sealed class UiKitTextFitGuard : MonoBehaviour
        {
            private TMP_Text _t;
            private int _frames;

            private void Awake() { _t = GetComponent<TMP_Text>(); }

            /// <summary>(Re)start the post-layout check.</summary>
            public void Arm() { _frames = 0; enabled = true; }

            private void LateUpdate()
            {
                if (_t == null) { enabled = false; return; }
                if (_frames++ < 1) return;                        // let the first layout pass size the rect
                if (string.IsNullOrEmpty(_t.text) || _t.rectTransform.rect.height <= 0f)
                {
                    if (_frames > 600)
                    {
                        // Round-3 finding: an armed label that never received text/size vanished
                        // from the log sweep SILENTLY — the exact hole that made the empty tab
                        // strips untraceable. A stand-down is itself a finding: log it.
                        FlowTrace.Warn("UI", "TextFitGuard [" + PathOf(_t.transform) + "]: armed but " +
                            (string.IsNullOrEmpty(_t.text) ? "text still EMPTY" : "rect still zero-height") +
                            " after 600 frames — standing down (a blank plate here is a TEXT-NEVER-SET bug, not a fit bug)");
                        enabled = false;
                    }
                    return;
                }

                float h = _t.rectTransform.rect.height;
                // Round-3 item 1: the 1.3x guess ROUNDED INTO THE CULL ZONE (rect 33 -> relaxed 26
                // -> real line ~34px -> still 0 glyphs). Use the font's MEASURED line factor
                // (faceInfo.lineHeight / pointSize), floor the result, take one more off.
                float factor = 1.3f;
                var f = _t.font;
                if (f != null && f.faceInfo.pointSize > 0f)
                    factor = Mathf.Max(1.05f, f.faceInfo.lineHeight / f.faceInfo.pointSize);

                // ── KIT-LEVEL MIN READABLE BAND FLOOR (additive; only GROWS too-short bands) ──
                // Root cause of the recurring "0 visible glyphs" class (F8 2026-07-08, Skip-Tutorial
                // confirm; earlier dialogue + EndState): kit text bands are authored as a FRACTION of
                // panel height (procedural title = Header y 0.92..0.98 = 0.06; FrameCore z.header
                // ~0.072). On a SMALL panel (confirm modal ~0.32 of a ~1080 canvas) that fraction
                // resolves to ~19px — below the height needed to seat the FontHardFloor(20) line
                // (a 20px line needs ~(20+1)*factor px ≈ 27-30px), so the fit-guard's relaxation
                // bottoms out at the floor and Ellipsis still CULLS the whole line. Fix the KIT once,
                // here at the guard the fit path arms on EVERY policed label (title/header/label/
                // message): if the RESOLVED band is too short to ever seat the hard-floor line, grow
                // the label rect to the minimum readable band (symmetric about its center so the text
                // stays put). This is a pure FLOOR — a band already tall enough (h >= minBand) is
                // untouched, so large panels are unchanged; only sub-legible bands grow. When the rect
                // is layout-group-driven the offset write is overridden and h is unchanged — same
                // behaviour as before (no regression), never worse.
                float minBand = (FontHardFloor + 1f) * factor + 2f;   // seats a 20px line: ~27-30px
                if (h < minBand)
                {
                    var brt = _t.rectTransform;
                    float deficit = minBand - h;
                    float half = deficit * 0.5f;
                    brt.offsetMin = new Vector2(brt.offsetMin.x, brt.offsetMin.y - half);
                    brt.offsetMax = new Vector2(brt.offsetMax.x, brt.offsetMax.y + half);
                    float grown = brt.rect.height;                    // honest re-measure (unchanged if driven)
                    FlowTrace.Warn("UI", "TextFitGuard '" + _t.text + "' [" + PathOf(_t.transform) +
                        "]: band too short to seat FontHardFloor line — grew rect " +
                        ((int)h) + "px -> " + ((int)grown) + "px (minBand " + minBand.ToString("F0") +
                        ", lineFactor " + factor.ToString("F2") + ")");
                    h = grown;
                    _t.ForceMeshUpdate();
                }

                float fitMin = Mathf.Max(FontHardFloor, Mathf.Floor(h / factor) - 1f);
                float oldMin = _t.fontSizeMin;
                bool relaxed = false;
                if (_t.fontSizeMin > fitMin)
                {
                    _t.fontSizeMin = fitMin;
                    if (_t.fontSizeMax < fitMin) _t.fontSizeMax = fitMin;
                    relaxed = true;
                }
                _t.ForceMeshUpdate();                              // refresh textInfo for the checks below

                // GUARANTEE-FIT: iterate the floor DOWN, verified by the guard's own post-check,
                // until glyphs actually render or the FontHardFloor mobile-readable floor (never a
                // static one-shot recompute again — the post-check is the truth, not the formula).
                int iter = 0;
                while (Blank(_t) && _t.fontSizeMin > FontHardFloor && iter++ < 10)
                {
                    _t.fontSizeMin = Mathf.Max(FontHardFloor, _t.fontSizeMin - 2f);
                    if (_t.fontSizeMax < _t.fontSizeMin) _t.fontSizeMax = _t.fontSizeMin;
                    _t.ForceMeshUpdate();
                    relaxed = true;
                }

                if (relaxed)
                    FlowTrace.Warn("UI", "TextFitGuard '" + _t.text + "' [" + PathOf(_t.transform) + "]: rect " +
                        ((int)_t.rectTransform.rect.width) + "x" + ((int)h) +
                        " lineFactor " + factor.ToString("F2") +
                        " — floor " + oldMin.ToString("F0") + " -> " + _t.fontSizeMin.ToString("F0") +
                        " (" + iter + " post-check iterations), fontSize now " + _t.fontSize.ToString("F0") +
                        ", chars " + (_t.textInfo != null ? _t.textInfo.characterCount : -1));

                // Render assert (the DumpZoneLayout-style oracle): a fitted label MUST draw glyphs.
                if (Blank(_t))
                    FlowTrace.Fail("UI", "TextFitGuard '" + _t.text + "' [" + PathOf(_t.transform) + "]: STILL renders 0 visible glyphs (rect " +
                        ((int)_t.rectTransform.rect.width) + "x" + ((int)h) +
                        ", fontSize " + _t.fontSize.ToString("F0") +
                        ", min " + _t.fontSizeMin.ToString("F0") + ", max " + _t.fontSizeMax.ToString("F0") +
                        ", overflow " + _t.overflowMode + ") — dead-button law violated, needs a layout fix");
                enabled = false;
            }

            /// <summary>True when the generated mesh has no visible glyph (Ellipsis/Truncate culled).</summary>
            private static bool Blank(TMP_Text t)
            {
                var ti = t.textInfo;
                if (ti == null || ti.characterCount == 0) return true;
                for (int i = 0; i < ti.characterCount; i++)
                    if (ti.characterInfo[i].isVisible) return false;
                return true;
            }

            /// <summary>Short hierarchy path for log lines (panel/strip/button/label).</summary>
            private static string PathOf(Transform t)
            {
                string s = t != null ? t.name : "?";
                int depth = 0;
                while (t != null && t.parent != null && depth++ < 4) { t = t.parent; s = t.name + "/" + s; }
                return s;
            }
        }

        /// <summary>Live handle of a kit scroll zone (§1.14). Parent rows/cards to
        /// <see cref="content"/> — it stacks them (VerticalLayoutGroup) and grows
        /// (ContentSizeFitter); the zone scrolls when they exceed it.</summary>
        public sealed class ScrollZoneHandle
        {
            /// <summary>The ScrollRect (on the zone-filling host).</summary>
            public ScrollRect scroll;
            /// <summary>The masked viewport.</summary>
            public RectTransform viewport;
            /// <summary>Parent your rows HERE (top-anchored, auto-growing).</summary>
            public RectTransform content;
            /// <summary>The auto-hiding vertical scrollbar.</summary>
            public Scrollbar scrollbar;
        }

        /// <summary>
        /// FIT-OR-SCROLL (§1.14): turn any content drop-zone into a vertical scroller —
        /// vertical only (horizontal off), Clamped movement (elastic OFF — no rubber-band
        /// on desktop), auto-hiding slim scrollbar, RectMask2D clipping so overflowing rows
        /// can never paint over the chrome outside the zone. Rows parented to the returned
        /// <c>content</c> are stacked by a VerticalLayoutGroup (childControlWidth/Height ON,
        /// force-expand width) and sized by their LayoutElement — content shorter than the
        /// zone simply fits; longer content scrolls. ONE call per zone; screens add no
        /// scroll plumbing of their own.
        /// </summary>
        /// <summary>§12 layout oracle — dump a zone's child rects one level deep (plus the scroll
        /// chain when present) so a "panel renders empty" capture names the collapsed layer from
        /// data. Cheap, gated on FlowTrace.Enabled; call after a panel build.</summary>
        public static void DumpZoneLayout(Transform zone, string tag)
        {
            if (zone == null || !DeNelle.Core.Diagnostics.FlowTrace.Enabled) return;
            var sb = new System.Text.StringBuilder();
            void Walk(Transform t, int depth)
            {
                if (t == null || depth > 3) return;
                var rt = t as RectTransform;
                sb.Append('\n').Append(new string(' ', depth * 2))
                  .Append(t.name)
                  .Append(rt != null ? $" h={rt.rect.height:0.#} w={rt.rect.width:0.#}" : "")
                  .Append(t.gameObject.activeSelf ? "" : " [INACTIVE]");
                // Recurse into containers that matter for the collapse question.
                if (depth < 3 && (t.GetComponent<UnityEngine.UI.ScrollRect>() != null ||
                                  t.name == "Viewport" || t.name == "Content" || depth == 0))
                    for (int i = 0; i < t.childCount && i < 24; i++) Walk(t.GetChild(i), depth + 1);
            }
            Walk(zone, 0);
            DeNelle.Core.Diagnostics.FlowTrace.Step("UiLayout", $"DumpZoneLayout[{tag}]:{sb}");
        }

        public static ScrollZoneHandle MakeScrollZone(Transform zone, float spacing = 6f, int padding = 6)
        {
            var h = new ScrollZoneHandle();

            // Host (carries the ScrollRect; fills the zone).
            var host = new GameObject("ScrollZone", typeof(RectTransform), typeof(ScrollRect));
            host.transform.SetParent(zone, false);
            var hrt = (RectTransform)host.transform;
            hrt.anchorMin = Vector2.zero; hrt.anchorMax = Vector2.one;
            hrt.offsetMin = Vector2.zero; hrt.offsetMax = Vector2.zero;

            // Viewport — masked; near-invisible Image so drag-to-scroll has a raycast surface.
            var vpGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            vpGo.transform.SetParent(host.transform, false);
            h.viewport = (RectTransform)vpGo.transform;
            h.viewport.anchorMin = Vector2.zero; h.viewport.anchorMax = Vector2.one;
            h.viewport.offsetMin = Vector2.zero; h.viewport.offsetMax = Vector2.zero;
            h.viewport.pivot = new Vector2(0f, 1f);   // ScrollRect viewport convention
            var vImg = vpGo.GetComponent<Image>();
            vImg.color = new Color(0f, 0f, 0f, 0.001f);

            // Content — top-anchored column that grows with its rows.
            var cGo = new GameObject("Content", typeof(RectTransform));
            cGo.transform.SetParent(vpGo.transform, false);
            h.content = (RectTransform)cGo.transform;
            h.content.anchorMin = new Vector2(0f, 1f);
            h.content.anchorMax = new Vector2(1f, 1f);
            h.content.pivot = new Vector2(0.5f, 1f);
            h.content.anchoredPosition = Vector2.zero;
            h.content.sizeDelta = Vector2.zero;
            var vlg = cGo.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.spacing = spacing;
            vlg.padding = new RectOffset(padding, padding, padding, padding);
            vlg.childControlWidth = true;
            // childControlHeight must be FALSE: kit rows are sized by explicit sizeDelta
            // (RowHeightPx cells) with no ILayoutElement, so a height-controlling group reads
            // preferred-height 0 and collapses the whole column — captured 2026-07-06 windowed
            // run: PartyShop resolved 39 items ([Flow:Vendor]) but rendered ZERO rows/tabs.
            // With control off, each child keeps its own height; the fitter sums real heights.
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var fitter = cGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Slim auto-hiding vertical scrollbar (right edge).
            var sbGo = new GameObject("ScrollbarV", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            sbGo.transform.SetParent(host.transform, false);
            var sbrt = (RectTransform)sbGo.transform;
            sbrt.anchorMin = new Vector2(1f, 0f); sbrt.anchorMax = Vector2.one;
            sbrt.pivot = new Vector2(1f, 1f);
            sbrt.offsetMin = new Vector2(-10f, 0f); sbrt.offsetMax = Vector2.zero;
            var sbImg = sbGo.GetComponent<Image>();
            sbImg.color = new Color(0f, 0f, 0f, 0.35f);
            ApplyRounded(sbImg);
            var slideArea = new GameObject("SlidingArea", typeof(RectTransform));
            slideArea.transform.SetParent(sbGo.transform, false);
            var sart = (RectTransform)slideArea.transform;
            sart.anchorMin = Vector2.zero; sart.anchorMax = Vector2.one;
            sart.offsetMin = new Vector2(2f, 2f); sart.offsetMax = new Vector2(-2f, -2f);
            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGo.transform.SetParent(slideArea.transform, false);
            var handleRt = (RectTransform)handleGo.transform;
            handleRt.offsetMin = Vector2.zero; handleRt.offsetMax = Vector2.zero;
            var handleImg = handleGo.GetComponent<Image>();
            handleImg.color = new Color(0.72f, 0.60f, 0.34f, 0.85f);   // gilt thumb (shape+position carry meaning, not colour)
            ApplyRounded(handleImg);
            h.scrollbar = sbGo.GetComponent<Scrollbar>();
            h.scrollbar.handleRect = handleRt;
            h.scrollbar.targetGraphic = handleImg;
            h.scrollbar.direction = Scrollbar.Direction.BottomToTop;

            h.scroll = host.GetComponent<ScrollRect>();
            h.scroll.viewport = h.viewport;
            h.scroll.content = h.content;
            h.scroll.horizontal = false;                                   // hidden horizontal
            h.scroll.vertical = true;
            h.scroll.movementType = ScrollRect.MovementType.Clamped;       // elastic OFF
            h.scroll.scrollSensitivity = 25f;
            h.scroll.verticalScrollbar = h.scrollbar;
            h.scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            return h;
        }
    }
}
