// =============================================================================
// ElarionUiKitDemo — the P1 KIT DEMO overlay (HUD_OBSIDIAN §4 P1 acceptance).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.UI   (Kit-team-owned)
//
// Dev-invocable (DevPanelController "UI Kit demo" action) full-screen scrolling
// canvas that shows EVERY §1 widget at THREE sizes, in BOTH factory modes
// (prefab-loader row + constructed row — amendment: the orchestrator screenshots
// compare them), with LIVE-ANIMATING bars (a SetValue sweep) and — the fill-
// contract proof — a bar PINNED at SetValue(9,145): the screenshot must show
// ~6% fill (fillAmount≈0.062) and the caption prints the live fillAmount +
// sprite-non-null readout, FlowTrace'd at build time so the headless log carries
// the proof line too.
//
// Toggle() rebuilds from scratch each open — the demo is a dev surface, not a
// pooled HUD; it exists to be screenshot and thrown away.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.UI
{
    /// <summary>Dev-invocable showcase of every ElarionUiKit Obsidian widget (P1 acceptance §4).</summary>
    public static class ElarionUiKitDemo
    {
        private static GameObject _canvas;

        /// <summary>Open the demo (or close it when already open).</summary>
        public static void Toggle()
        {
            if (_canvas != null)
            {
                Object.Destroy(_canvas);
                _canvas = null;
                return;
            }
            Guard.Try("UI", "build ElarionUiKitDemo", Build);
        }

        private static void Build()
        {
            _canvas = ElarionUiKit.BuildModalCanvas("UiKitDemoCanvas", 32500);
            ElarionUiKit.Scrim(_canvas.transform, Toggle);

            // Scrolling column: a tall content rect inside a full-screen ScrollRect.
            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(RectMask2D), typeof(Image));
            scrollGo.transform.SetParent(_canvas.transform, false);
            var srt = (RectTransform)scrollGo.transform;
            srt.anchorMin = new Vector2(0.02f, 0.02f); srt.anchorMax = new Vector2(0.98f, 0.93f);
            srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);   // raycast surface for drag

            const float ContentH = 5200f;   // reference px of demo rows
            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(scrollGo.transform, false);
            var crt = (RectTransform)contentGo.transform;
            crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.sizeDelta = new Vector2(0f, ContentH);
            crt.anchoredPosition = Vector2.zero;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.content = crt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            var title = ElarionUiKit.Label(_canvas.transform, "ELARION UI KIT - P1 OBSIDIAN DEMO",
                0.94f, 0.99f, ElarionUiKit.ObsidianTrim, ElarionUi.FontTitle,
                TextAlignmentOptions.Center, 0.05f, 0.95f, spacing: 3f, bold: true);
            ElarionUiKit.EnsureFont(title, ElarionUiKit.FontRole.Title);
            ElarionUiKit.ObsidianCloseButton(_canvas.transform, Toggle);

            var driver = _canvas.AddComponent<KitDemoDriver>();
            driver.Populate(contentGo.transform, ContentH);
        }

        // =====================================================================
        // The demo driver: builds every row, sweeps the live bars in Update.
        // =====================================================================

        /// <summary>Builds the widget rows + animates the sweep bars (demo-only component).</summary>
        private sealed class KitDemoDriver : MonoBehaviour
        {
            private readonly System.Collections.Generic.List<ElarionUiKit.BarHandle> _sweepBars
                = new System.Collections.Generic.List<ElarionUiKit.BarHandle>();
            private readonly System.Collections.Generic.List<ElarionUiKit.ActionSlotHandle> _sweepSlots
                = new System.Collections.Generic.List<ElarionUiKit.ActionSlotHandle>();
            private readonly System.Collections.Generic.List<ElarionUiKit.CastBarHandle> _sweepCasts
                = new System.Collections.Generic.List<ElarionUiKit.CastBarHandle>();
            private ElarionUiKit.CurrencyChipHandle _goldChip;
            private float _chipTimer;
            private long _gold = 1234;

            private float _y;          // running row cursor (reference px from the top)
            private float _height;
            private Transform _root;

            public void Populate(Transform root, float height)
            {
                _root = root;
                _height = height;
                _y = 20f;

                // ── 1) THE FILL-CONTRACT PROOF — a bar PINNED at 9/145 ─────
                Caption("FILL CONTRACT PROOF - BuildObsidianBar(Health).SetValue(9, 145)  [expect ~6% fill]");
                var pinned = ElarionUiKit.BuildObsidianBar(Row(70f), ElarionUiKit.ObsidianBarKind.Health,
                    new Vector2(0.02f, 0f), new Vector2(0.60f, 1f), withValue: true);
                pinned.SetImmediate(9f, 145f);
                bool spriteOk = pinned.fill != null && pinned.fill.sprite != null;
                float fa = pinned.fill != null ? pinned.fill.fillAmount : -1f;
                var proof = "9/145 => fillAmount=" + fa.ToString("F3") + " (expect 0.062), fillSprite=" +
                            (spriteOk ? "NON-NULL OK" : "NULL - CONTRACT BROKEN") +
                            ", type=" + (pinned.fill != null ? pinned.fill.type.ToString() : "?");
                FlowTrace.Step("UI", "KitDemo FILL PROOF: " + proof);
                Caption(proof);

                // ── 2) Bars — every kind, three sizes, live sweep ───────────
                Caption("BuildObsidianBar - all kinds x 3 sizes (live SetValue sweep)");
                var kinds = new[]
                {
                    ElarionUiKit.ObsidianBarKind.Health, ElarionUiKit.ObsidianBarKind.Mana,
                    ElarionUiKit.ObsidianBarKind.Energy, ElarionUiKit.ObsidianBarKind.Stamina,
                    ElarionUiKit.ObsidianBarKind.Xp,     ElarionUiKit.ObsidianBarKind.Heart,
                    ElarionUiKit.ObsidianBarKind.Loading, ElarionUiKit.ObsidianBarKind.Stat,
                };
                foreach (var kind in kinds)
                {
                    var row = Row(64f);
                    ElarionUiKit.Label(row, kind.ToString(), 0f, 1f, ElarionUi.Parchment,
                        ElarionUi.FontLabel, TextAlignmentOptions.MidlineLeft, 0.00f, 0.12f);
                    _sweepBars.Add(ElarionUiKit.BuildObsidianBar(row, kind, new Vector2(0.13f, 0.10f), new Vector2(0.55f, 0.90f), withValue: true));
                    _sweepBars.Add(ElarionUiKit.BuildObsidianBar(row, kind, new Vector2(0.58f, 0.22f), new Vector2(0.82f, 0.78f), withValue: false));
                    _sweepBars.Add(ElarionUiKit.BuildObsidianBar(row, kind, new Vector2(0.85f, 0.32f), new Vector2(0.99f, 0.68f), withValue: false));
                }

                // ── 3) Buttons — the 5x4 family + shim + 3 sizes ────────────
                Caption("BuildObsidianButton - 5 styles x 4 colors (constructed; sprite-first)");
                for (int s = 1; s <= 5; s++)
                {
                    var row = Row(78f);
                    for (int c = 0; c < 4; c++)
                    {
                        float x0 = 0.02f + c * 0.245f, x1 = x0 + 0.225f;
                        ElarionUiKit.BuildObsidianButton(row, "Style" + s,
                            (ElarionUiKit.ObsidianButtonStyle)s, (ElarionUiKit.ObsidianButtonColor)c,
                            new Vector2(x0, 0.08f), new Vector2(x1, 0.92f));
                    }
                }
                Caption("ButtonKind shim (legacy Button() routes into the family) - 3 sizes");
                var shimRow = Row(84f);
                ElarionUiKit.Button(shimRow, "Gold CTA", ElarionUiKit.ButtonKind.Gold, new Vector2(0.02f, 0.05f), new Vector2(0.34f, 0.95f));
                ElarionUiKit.Button(shimRow, "Confirm", ElarionUiKit.ButtonKind.Confirm, new Vector2(0.37f, 0.18f), new Vector2(0.62f, 0.82f));
                ElarionUiKit.Button(shimRow, "Danger", ElarionUiKit.ButtonKind.Danger, new Vector2(0.65f, 0.28f), new Vector2(0.84f, 0.72f));

                // ── 4) Close (3-state) + CurrencyChips ──────────────────────
                Caption("3-state Close (hover=on, press=off) + CurrencyChip (gold primacy, count-tween, NO flash)");
                var chipRow = Row(84f);
                ElarionUiKit.ObsidianCloseButton(chipRow, null, new Vector4(0.02f, 0.15f, 0.10f, 0.85f));
                _goldChip = ElarionUiKit.CurrencyChip(chipRow, ElarionUiKit.CurrencyKind.Gold,
                    new Vector2(0.14f, 0.05f), new Vector2(0.42f, 0.95f), primary: true);
                _goldChip.SetAmount(_gold, animate: false);
                var wood = ElarionUiKit.CurrencyChip(chipRow, ElarionUiKit.CurrencyKind.Wood,
                    new Vector2(0.45f, 0.18f), new Vector2(0.66f, 0.82f));
                wood.SetAmount(842, animate: false);
                var wisdom = ElarionUiKit.CurrencyChip(chipRow, ElarionUiKit.CurrencyKind.Wisdom,
                    new Vector2(0.69f, 0.18f), new Vector2(0.90f, 0.82f));
                wisdom.SetAmount(23, animate: false);

                // ── 5) Toasts ───────────────────────────────────────────────
                Caption("BuildToast (ToastCard restyle) - Gold / Confirm / Danger");
                var toastRow = Row(84f);
                ToastAt(toastRow, ElarionUiKit.ToastTone.Gold,    "Gear granted: Iron Sword", 0.02f, 0.33f);
                ToastAt(toastRow, ElarionUiKit.ToastTone.Confirm, "Wave cleared!",            0.35f, 0.66f);
                ToastAt(toastRow, ElarionUiKit.ToastTone.Danger,  "Not enough gold",          0.68f, 0.99f);

                // ── 6) Action slots (radial cooldown sweep) — 3 sizes ───────
                Caption("BuildActionSlot - radial cooldown sweep (non-null radial sprite) x 3 sizes");
                var slotRow = Row(110f);
                _sweepSlots.Add(ElarionUiKit.BuildActionSlot(slotRow, new Vector2(0.02f, 0.02f), new Vector2(0.14f, 0.98f)));
                _sweepSlots.Add(ElarionUiKit.BuildActionSlot(slotRow, new Vector2(0.17f, 0.14f), new Vector2(0.26f, 0.86f)));
                _sweepSlots.Add(ElarionUiKit.BuildActionSlot(slotRow, new Vector2(0.29f, 0.24f), new Vector2(0.355f, 0.76f)));
                var countSlot = ElarionUiKit.BuildActionSlot(slotRow, new Vector2(0.40f, 0.02f), new Vector2(0.52f, 0.98f));
                countSlot.SetCount(5);

                // ── 7) Cast bars — 3 styles, live sweep ─────────────────────
                Caption("BuildCastBar - styles 1/2/3 (doc-measured 507/800 fill geometry)");
                for (int s = 1; s <= 3; s++)
                {
                    var cb = ElarionUiKit.BuildCastBar(Row(60f), s, new Vector2(0.05f, 0.05f), new Vector2(0.80f, 0.95f));
                    cb.SetCast("Orc Shaman: Fireball", 0.4f);
                    _sweepCasts.Add(cb);
                }

                // ── 8) Tabs / Toggle / Slider / Dropdown ────────────────────
                Caption("BuildTab / BuildToggle / BuildSlider / BuildDropdown");
                var tabRow = Row(70f);
                var tabA = ElarionUiKit.BuildTab(tabRow, "Weapons", new Vector2(0.02f, 0.1f), new Vector2(0.22f, 0.9f));
                tabA.SetSelected(true);
                ElarionUiKit.BuildTab(tabRow, "Armor", new Vector2(0.24f, 0.1f), new Vector2(0.44f, 0.9f));
                ElarionUiKit.BuildToggle(tabRow, true, null, new Vector2(0.48f, 0.15f), new Vector2(0.58f, 0.85f));
                ElarionUiKit.BuildToggle(tabRow, false, null, new Vector2(0.60f, 0.2f), new Vector2(0.68f, 0.8f), checkbox: true);
                var ctlRow = Row(70f);
                ElarionUiKit.BuildSlider(ctlRow, 0f, 100f, 62f, null, new Vector2(0.02f, 0.2f), new Vector2(0.46f, 0.8f));
                ElarionUiKit.BuildDropdown(ctlRow, new[] { "Low", "Medium", "High", "Ultra" }, null,
                    new Vector2(0.52f, 0.1f), new Vector2(0.86f, 0.9f));

                // ── 9) Target frame — bound state + a total-Clear() button ──
                Caption("BuildTargetFrame - Set vs TOTAL Clear() (the No-Target law)");
                var tgtRow = Row(150f);
                var tf = ElarionUiKit.BuildTargetFrame(tgtRow, new Vector2(0.02f, 0.05f), new Vector2(0.45f, 0.95f));
                tf.Set("Orc Warlord", "BOSS", 62f, 180f, "LOCKED");   // WO-1232: an authored WORD, never "Lv N"
                var tfClear = ElarionUiKit.BuildTargetFrame(tgtRow, new Vector2(0.50f, 0.05f), new Vector2(0.93f, 0.95f));
                tfClear.Clear();   // screenshot: fully-empty frame, "No Target", blank value, 0 fill

                // ── 10) Nameplates — every kind ─────────────────────────────
                Caption("BuildNameplate - Player / Party / Enemy / Neutral / Rare / Boss");
                var npKinds = new[]
                {
                    ElarionUiKit.NameplateKind.Player, ElarionUiKit.NameplateKind.Party,
                    ElarionUiKit.NameplateKind.Enemy, ElarionUiKit.NameplateKind.Neutral,
                    ElarionUiKit.NameplateKind.Rare, ElarionUiKit.NameplateKind.Boss,
                };
                foreach (var k in npKinds)
                {
                    var np = ElarionUiKit.BuildNameplate(Row(86f), k, new Vector2(0.05f, 0.05f), new Vector2(0.70f, 0.95f));
                    np.SetName(k.ToString());
                    np.hp.SetImmediate(k == ElarionUiKit.NameplateKind.Boss ? 145f : 62f, 145f);
                    if (np.mp != null) np.mp.SetImmediate(30f, 90f);
                    _sweepBars.Add(np.hp);
                }

                // ── 11) Controller cluster + chat dock ──────────────────────
                Caption("BuildControllerCluster (4 ROUND buttons, >=56px, press squash) + BuildChatDock");
                var padRow = Row(280f);
                ElarionUiKit.BuildControllerCluster(padRow, new Vector2(0.22f, 0.5f), null);
                ElarionUiKit.BuildChatDock(padRow, new Vector2(0.48f, 0.60f), new Vector2(0.95f, 0.90f));

                // ── 12) CombatText spam proof ───────────────────────────────
                Caption("CombatText - tap to SPAM 'PARRY!' (must show ONE stamp with an xN counter)");
                var ctRow = Row(80f);
                ElarionUiKit.Button(ctRow, "Spam PARRY! x5", ElarionUiKit.ButtonKind.Danger,
                    new Vector2(0.02f, 0.1f), new Vector2(0.40f, 0.9f), () =>
                    {
                        for (int i = 0; i < 5; i++)
                            CombatText.Show(CombatTextKind.Parry, "PARRY!",
                                Camera.main != null ? Camera.main.transform.position + Camera.main.transform.forward * 6f : Vector3.zero);
                    });
                ElarionUiKit.Button(ctRow, "Riposte + Block", ElarionUiKit.ButtonKind.Quiet,
                    new Vector2(0.44f, 0.1f), new Vector2(0.80f, 0.9f), () =>
                    {
                        CombatText.Show(CombatTextKind.Riposte, "RIPOSTE!", Vector3.zero);
                        CombatText.Show(CombatTextKind.Block, "BLOCK", Vector3.zero);
                    });

                // ── 13) BOTH FACTORY MODES side by side (amendment) ─────────
                Caption("FACTORY MODES - prefab-loader (left; falls back until P0 prefabs land) vs constructed (right)");
                var modeRow = Row(96f);
                bool saved = ElarionUiKit.PrefabMode;
                ElarionUiKit.PrefabMode = true;
                var pfPlate = ElarionUiKit.BuildNameplate(modeRow, ElarionUiKit.NameplateKind.Party,
                    new Vector2(0.02f, 0.05f), new Vector2(0.46f, 0.95f));
                pfPlate.SetName("prefab mode");
                pfPlate.hp.SetImmediate(9f, 145f);
                ElarionUiKit.PrefabMode = false;
                var conPlate = ElarionUiKit.BuildNameplate(modeRow, ElarionUiKit.NameplateKind.Party,
                    new Vector2(0.52f, 0.05f), new Vector2(0.96f, 0.95f));
                conPlate.SetName("constructed");
                conPlate.hp.SetImmediate(9f, 145f);
                ElarionUiKit.PrefabMode = saved;

                FlowTrace.Step("UI", "KitDemo built: rows to y=" + _y.ToString("F0") + " of " + _height);
            }

            // One demo row: a full-width strip at the current cursor; advances the cursor.
            private Transform Row(float h)
            {
                var go = new GameObject("DemoRow", typeof(RectTransform));
                go.transform.SetParent(_root, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(0f, h);
                rt.anchoredPosition = new Vector2(0f, -_y);
                _y += h + 10f;
                return go.transform;
            }

            private void Caption(string text)
            {
                var row = Row(34f);
                var lbl = ElarionUiKit.Label(row, text, 0f, 1f, ElarionUi.Gilt, ElarionUi.FontLabel,
                    TextAlignmentOptions.MidlineLeft, 0.01f, 0.99f, bold: true);
                lbl.raycastTarget = false;
            }

            private void ToastAt(Transform row, ElarionUiKit.ToastTone tone, string text, float x0, float x1)
            {
                var parts = ElarionUiKit.ToastCard(row, tone);
                var rt = (RectTransform)parts.card.transform;
                rt.anchorMin = new Vector2(x0, 0.08f); rt.anchorMax = new Vector2(x1, 0.92f);
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                parts.label.text = text;
            }

            private void Update()
            {
                // Live sweep: ping-pong 0..145 (SetImmediate — per-frame drive; labels only realloc
                // on integer change per the BarHandle contract).
                float v = Mathf.PingPong(Time.unscaledTime * 36f, 145f);
                for (int i = 0; i < _sweepBars.Count; i++) _sweepBars[i].SetImmediate(v, 145f);

                float cd = Mathf.PingPong(Time.unscaledTime, 3f);
                for (int i = 0; i < _sweepSlots.Count; i++) _sweepSlots[i].SetCooldown(cd, 3f);

                float cast = Mathf.Repeat(Time.unscaledTime * 0.5f, 1f);
                for (int i = 0; i < _sweepCasts.Count; i++) _sweepCasts[i].SetCast("Orc Shaman: Fireball", cast);

                // Gold count-tween fires every 2.5s (proves tween + no flash).
                _chipTimer += Time.unscaledDeltaTime;
                if (_chipTimer >= 2.5f && _goldChip != null)
                {
                    _chipTimer = 0f;
                    _gold += 137;
                    _goldChip.SetAmount(_gold);
                }
            }
        }
    }
}
