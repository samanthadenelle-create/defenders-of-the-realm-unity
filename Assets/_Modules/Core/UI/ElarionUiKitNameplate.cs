// =============================================================================
// ElarionUiKit (partial) — BuildPartyNameplate: the SHARED code-built HP/MP plate
// modelled on Blink Obsidian's PartyNameplate.prefab (WO-432).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.UI
//
// ONE reusable HP+MP nameplate the whole HUD can drop anywhere — the hero panel,
// the Heart of Elarion bar, future party frames. CODE-BUILT (no prefab
// instantiation), mirroring the prefab's layer table:
//
//   Root  PartyNameplate (Image, party plate sprite)
//   ├─ PlayerName (TMP_Text — white, auto-size, left-aligned, top)
//   └─ StatBars
//      ├─ HealthBackground (Image #1f1f1f) ─ HealthFill (Image, Filled/Horizontal)
//      └─ ManaBackground   (Image #5e5e5e) ─ ManaFill   (Image, Filled/Horizontal)
//
// WEBGL-SAFE SPRITE RESOLUTION (WO-432 flag): the prefab addresses its sprites by
// GUID via AssetDatabase — but AssetDatabase is EDITOR-ONLY and this kit lives in
// the runtime DeNelle.Core assembly (WebGL target). So sprites resolve through the
// SAME committed Resources/RpgUi mirror every other kit widget uses
// (RpgUiCatalog.RoleHud → Resources/RpgUi/hud/*). The prefab's four GUID sprites
// are ALREADY mirrored there by RpgUiImporter:
//   0bf4c931… (root plate)      -> nameplate_party
//   6a8076f6… (shared bar bg)   -> nameplate_bar
//   fd306686… (health fill)     -> nameplate_health
//   4791157a… (mana fill)       -> nameplate_mana
// No new mirroring is required. Any miss -> Debug.LogWarning + solid-colour
// fallback (never a blank plate); fills go through the §1.1 non-null-sprite
// contract so uGUI honours fillAmount (the 9/145 law).
//
// NOTE on the handle name: the kit ALREADY has a sealed class
// ElarionUiKit.NameplateHandle (the §1.10 world/party plate with BarHandle hp/mp).
// A partial class cannot declare that name twice, so this WO's struct is exposed
// as PartyNameplateHandle with the exact fields the spec names (Root / NameLabel /
// HealthFill / ManaFill). Signature also takes Transform (not RectTransform) parent
// to match every other kit builder and avoid a brittle cast at the HUD call sites.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.UI
{
    public static partial class ElarionUiKit
    {
        /// <summary>The live pieces of a <see cref="BuildPartyNameplate"/> plate. Callers drive the
        /// bars by setting <see cref="HealthFill"/>.fillAmount = hp/maxHp and
        /// <see cref="ManaFill"/>.fillAmount = mp/maxMp, and retext <see cref="NameLabel"/>.</summary>
        public struct PartyNameplateHandle
        {
            /// <summary>The root plate RectTransform (parent / reposition via this).</summary>
            public RectTransform Root;
            /// <summary>The name/label TMP text (retext freely).</summary>
            public TMP_Text NameLabel;
            /// <summary>Health bar fill — set fillAmount = hp/maxHp (0..1).</summary>
            public Image HealthFill;
            /// <summary>Mana bar fill — set fillAmount = mp/maxMp (0..1). Its parent (ManaBackground)
            /// can be SetActive(false) to present a single-bar plate (e.g. the Heart of Elarion).</summary>
            public Image ManaFill;
            /// <summary>XP strip fill (gold on a dark track) — set fillAmount = xp/xpToNext (0..1).
            /// Null unless built with <c>withXpStrip: true</c> (owner 07-06: "a bar showing exp").</summary>
            public Image XpFill;
            /// <summary>XP strip row root (track GameObject). Built INACTIVE — the binder activates it
            /// on the first valid xp/xpToNext push, so a missing HeroProgression can never show a
            /// blank/full bar. Null unless built with <c>withXpStrip: true</c>.</summary>
            public GameObject XpRow;
        }

        /// <summary>
        /// Build a shared code-built HP/MP nameplate (WO-432) mirroring Blink's PartyNameplate.prefab.
        /// Anchored by fraction-of-parent (<paramref name="anchorMin"/>/<paramref name="anchorMax"/>)
        /// with optional pixel <paramref name="offsetMin"/>/<paramref name="offsetMax"/> insets, so it
        /// reflows in any HUD area. WebGL-safe (Resources/RpgUi mirror, never AssetDatabase). Every
        /// sprite miss falls back to a solid colour and is logged — the plate can never blank.
        /// </summary>
        public static PartyNameplateHandle BuildPartyNameplate(
            Transform parent,
            string playerName,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin = default, Vector2 offsetMax = default,
            bool withXpStrip = false)
        {
            var h = new PartyNameplateHandle();

            // ── Root plate (PartyNameplate Image) ────────────────────────────
            var rootGo = new GameObject("PartyNameplate", typeof(Image));
            rootGo.transform.SetParent(parent, false);
            var rt = (RectTransform)rootGo.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            h.Root = rt;

            var rootImg = rootGo.GetComponent<Image>();
            rootImg.raycastTarget = false;
            var plateSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleHud, RpgUiCatalog.HudNameplateParty);
            if (plateSprite != null)
            {
                rootImg.sprite = plateSprite;
                rootImg.type = Image.Type.Simple;   // ornate party plate — never slice
                rootImg.color = ChromeTint;
            }
            else
            {
                // Solid-colour fallback (never blank) — the kit's near-black plate.
                Debug.LogWarning("[ElarionUiKit] BuildPartyNameplate: nameplate_party sprite missing " +
                                 "(Resources/RpgUi/hud) — solid-colour plate fallback.");
                rootImg.color = new Color(0.10f, 0.09f, 0.11f, 0.96f);
                ApplyRounded(rootImg);
            }

            // ── PlayerName (TMP, top, left-aligned, auto-size like the prefab) ─
            h.NameLabel = Label(rootGo.transform, playerName ?? "", 0.58f, 1.0f,
                                Color.white, ElarionUi.FontHead, TextAlignmentOptions.MidlineLeft,
                                0.04f, 0.97f, bold: true);
            h.NameLabel.enableAutoSizing = true;
            h.NameLabel.fontSizeMin = 32f;   // mobile floor (was 12, sub-legible on a phone)
            h.NameLabel.fontSizeMax = 72f;   // prefab 18–72 scaled to the kit's reference res
            h.NameLabel.raycastTarget = false;

            // ── StatBars (two stacked horizontal bars) ───────────────────────
            // The prefab uses a fixed-cell (348x31) GridLayoutGroup; a "drop anywhere"
            // shared builder must reflow to any area width, so the two rows are STRETCH-
            // anchored instead (identical visual: two stacked horizontal bars). Health = top
            // row, Mana = bottom row.
            // Owner 07-06 ("can the hp/mp of hero as well as tree stay inside their containers?"):
            // the nameplate_party plate draws Image.Type.Simple and its VISIBLE ornate frame is
            // INSET from the sprite rect — so anchors near the sprite edge still bleed past the
            // frame the player sees. 0.06..0.94 x / 0.08..0.55 y was felt-verified INSUFFICIENT
            // in the built player (owner F8 2026-07-06/07: bars still rendered outside their
            // plates). Margin below is derived from that visible-frame inset — generous, err
            // inward: bars must read as visibly contained INSIDE the box at any resolution.
            var statBars = AddImage(rootGo.transform, "StatBars",
                new Vector2(0.10f, 0.12f), new Vector2(0.88f, 0.54f),
                new Color(0f, 0f, 0f, 0f), rounded: false);
            statBars.GetComponent<Image>().raycastTarget = false;
            // WO-437: clip the bars to the StatBars container so no HP/MP fill can bleed past the
            // nameplate edge. Masking the CONTAINER (not the root plate) keeps the ornate plate
            // border intact while confining every bar row + fill inside it.
            statBars.AddComponent<RectMask2D>();

            // Row layout: two stacked bars; when the XP strip is requested (hero plate only —
            // owner 07-06 "expecting a bar showing exp in relationship to next level") the rows
            // compress upward to free a thin gold strip along the container's bottom.
            Vector2 hpMin = withXpStrip ? new Vector2(0f, 0.62f) : new Vector2(0f, 0.52f);
            Vector2 mpMin = withXpStrip ? new Vector2(0f, 0.22f) : new Vector2(0f, 0f);
            Vector2 mpMax = withXpStrip ? new Vector2(1f, 0.58f) : new Vector2(1f, 0.48f);

            h.HealthFill = BuildNameplateRow(statBars.transform, "Health",
                hpMin, new Vector2(1f, 1f),
                new Color(0.1226f, 0.1226f, 0.1226f, 1f),   // HealthBackground #1f1f1f
                RpgUiCatalog.HudNameplateHealth,
                new Color(0.82f, 0.16f, 0.16f, 1f));        // fallback fill = red

            h.ManaFill = BuildNameplateRow(statBars.transform, "Mana",
                mpMin, mpMax,
                new Color(0.3679f, 0.3679f, 0.3679f, 1f),   // ManaBackground #5e5e5e
                RpgUiCatalog.HudNameplateMana,
                new Color(0.24f, 0.44f, 0.86f, 1f));        // fallback fill = blue

            if (withXpStrip)
            {
                // ── XP strip (owner 07-06): thin gold-on-dark progress line under HP/MP, inside
                // the same masked StatBars container (so it can never bleed either). No text —
                // the adjacent "Lv N" label carries the number; gold (Gilt) is luminance-distinct
                // from the red HP / blue MP rows (colorblind-safe by position + brightness).
                // Procedural (no sprite lookup — no warning spam); FillSpriteChain keeps the
                // §1.1 non-null-sprite contract so uGUI honours fillAmount.
                var xpBg = new GameObject("XpTrack", typeof(Image));
                xpBg.transform.SetParent(statBars.transform, false);
                var xrt = (RectTransform)xpBg.transform;
                // Match the bar rows' 6% right inset (end-cap fix) so the strip shares the
                // rows' horizontal extents and stays inside the visible plate frame.
                xrt.anchorMin = new Vector2(0f, 0f); xrt.anchorMax = new Vector2(0.94f, 0.14f);
                xrt.offsetMin = Vector2.zero; xrt.offsetMax = Vector2.zero;
                var xpBgImg = xpBg.GetComponent<Image>();
                xpBgImg.raycastTarget = false;
                xpBgImg.color = new Color(0f, 0f, 0f, 0.60f);   // dark track
                ApplyRounded(xpBgImg);
                xpBg.AddComponent<RectMask2D>();                 // belt-and-braces like the rows

                var xpFillGo = new GameObject("XpFill", typeof(Image));
                xpFillGo.transform.SetParent(xpBg.transform, false);
                var xfrt = (RectTransform)xpFillGo.transform;
                xfrt.anchorMin = Vector2.zero; xfrt.anchorMax = Vector2.one;
                xfrt.offsetMin = new Vector2(1f, 1f); xfrt.offsetMax = new Vector2(-1f, -1f);
                var xpFillImg = xpFillGo.GetComponent<Image>();
                xpFillImg.sprite = FillSpriteChain(null);        // guaranteed non-null (9/145 law)
                xpFillImg.color = ElarionUi.Gilt;                // gold progress
                xpFillImg.type = Image.Type.Filled;
                xpFillImg.fillMethod = Image.FillMethod.Horizontal;
                xpFillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
                xpFillImg.fillAmount = 0f;
                xpFillImg.raycastTarget = false;

                h.XpFill = xpFillImg;
                h.XpRow = xpBg;
                // Hidden until the binder pushes a real xp/xpToNext — a missing HeroProgression
                // therefore hides the strip (never a blank or stuck-full bar).
                xpBg.SetActive(false);
            }

            return h;
        }

        /// <summary>Build one PartyNameplate bar row: a background Image (shared bar sprite tinted to
        /// <paramref name="bgColor"/>) with a 2px-inset Filled/Horizontal fill child. Returns the fill
        /// Image (fillAmount starts at 1). WebGL-safe; sprite misses fall back to solid colour + log.</summary>
        private static Image BuildNameplateRow(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Color bgColor, string fillSpriteName, Color fillFallback)
        {
            // Background (shared bar plate).
            var bgGo = new GameObject(name + "Background", typeof(Image));
            bgGo.transform.SetParent(parent, false);
            var brt = (RectTransform)bgGo.transform;
            // Owner F8 2026-07-06/07: nameplate_bar's pointed end-cap (drawn Simple; the sprite
            // has NO 9-slice border — spriteBorder 0,0,0,0 — so Sliced can't tuck it) landed at
            // the row's right edge and poked past the plate frame. Inset xMax ~6% so the cap
            // stays inside the visible plate.
            brt.anchorMin = anchorMin;
            brt.anchorMax = new Vector2(anchorMax.x - 0.06f, anchorMax.y);
            brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
            var bgImg = bgGo.GetComponent<Image>();
            bgImg.raycastTarget = false;
            bgImg.preserveAspect = false;   // stretch to the row rect — never overhang it
            // WO-437: also clip each bar's fill to its OWN background container, so a >100% or
            // animating Filled fill can never spill past the bar edge (belt-and-braces with the
            // StatBars mask). The background Image is the mask graphic; the fill child is clipped.
            bgGo.AddComponent<RectMask2D>();
            var barBg = RpgUiCatalog.Get(RpgUiCatalog.RoleHud, RpgUiCatalog.HudNameplateBar);
            if (barBg != null)
            {
                bgImg.sprite = barBg;
                bgImg.type = Image.Type.Simple;
                bgImg.color = bgColor;
            }
            else
            {
                Debug.LogWarning("[ElarionUiKit] BuildPartyNameplate: nameplate_bar sprite missing " +
                                 "(Resources/RpgUi/hud) — solid-colour " + name + " background fallback.");
                bgImg.color = bgColor;
                ApplyRounded(bgImg);
            }

            // Fill (2px inset, Filled/Horizontal, non-null sprite by contract).
            var fillGo = new GameObject(name + "Fill", typeof(Image));
            fillGo.transform.SetParent(bgGo.transform, false);
            var frt = (RectTransform)fillGo.transform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(2f, 2f); frt.offsetMax = new Vector2(-2f, -2f);
            var fillImg = fillGo.GetComponent<Image>();
            var fillSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleHud, fillSpriteName);
            if (fillSprite != null)
            {
                fillImg.sprite = fillSprite;
                fillImg.color = Color.white;   // coloured pack art — untinted
            }
            else
            {
                Debug.LogWarning("[ElarionUiKit] BuildPartyNameplate: fill sprite '" + fillSpriteName +
                                 "' missing (Resources/RpgUi/hud) — solid-colour fill fallback.");
                fillImg.sprite = FillSpriteChain(null);   // guaranteed non-null (9/145 law)
                fillImg.color = fillFallback;
            }
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount = 1f;
            fillImg.raycastTarget = false;
            return fillImg;
        }
    }
}
