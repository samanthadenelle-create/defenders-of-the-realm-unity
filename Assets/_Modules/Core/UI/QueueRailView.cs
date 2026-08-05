// =============================================================================
// QueueRailView — the ONE CoC-style queue card rail (WO-864). REUSABLE COMPONENT.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.UI
//
// PUBLIC ENTRY POINT (write future hosts against this — nothing else is API):
//
//     QueueRailView rail = QueueRailView.Build(mount, channel, options);
//
//   mount   : any RectTransform. The rail fills its width and takes a FIXED pixel
//             height off the TOP of it. The HOST owns all chrome around it — a
//             header, a frame, a title, a "+slot" button are the host's job, never
//             the rail's. That is why the same rail can sit in the always-on HUD
//             band, in the Work Queue modal, and in a future Manage screen without
//             any of them leaking layout into the others.
//   channel : DeNelle.Core.Jobs.ChannelId — Builder / Train / Research. One rail
//             renders exactly ONE channel; three channels = three Build() calls.
//   options : Options.Default, or a copy with CardHeightPx / CardWidthPx / Gap /
//             Pad / ShowPlate overridden. Nothing here is channel-specific.
//
//   rail.Height  -> the fixed pixel height it occupies (lay the host out with this).
//   rail.Sync()  -> force a repaint; normally self-driving.
//
// It lives in DeNelle.Core because DeNelle.HUD and DeNelle.Village may not
// reference each other (CLAUDE.md §5) and BOTH host it. It reads ONLY the
// presentation-ready Core snapshot (ObsidianQueueGate.Status) — no game state, no
// service call, no clock of its own — so both hosts always show the SAME thing.
//
// ── DESIGN: VERB-FIRST (owner ruling 2026-08-03) ─────────────────────────────
// "For now if no images we can use verbs." So the card is BUILT on the verb and
// GAINS a portrait where one exists:
//
//        +------------------+
//        |      BUILD       |  <- verb band, always populated
//        |   [ portrait ]   |  <- portrait if any, else the name's initial
//        |   Arcane Spire   |  <- name (+ level)
//        |     3m 13s       |  <- timer / QUEUED / --
//        +------------------+
//
// Measured art coverage is ~76% of queueable jobs (see QueueIconResolver's header
// for the per-entry table), so a card that ASSUMED an icon would look broken about
// a quarter of the time. This one looks deliberate at 0% coverage and better at 76%.
//
// ── THE THREE BUGS THIS FIXES (owner's live Seeker capture, 2026-08-03) ──────
//  1. TIMER PRINTED TWICE — the old surface put the soonest countdown in the chip
//     header AND again in the job row. Exactly ONE card owns a countdown now, and
//     the host header carries no timer at all.
//  2. FREE SLOT RENDERED AS BLANK SPACE — 1 of 2 builders busy drew one text row
//     and left a large dark void. Slot count now drives the card count: every idle
//     slot gets a visible "FREE / Open slot" card.
//  3. PANEL OVERSIZED FOR ITS CONTENT — the old rows plate was a FRACTION of its
//     band, so it reserved five rows' worth of dark plate to show one. Every band
//     here is FIXED PIXELS (WO-841/WO-852 fraction-band culling lesson); the rail
//     is exactly as tall as one card plus padding and never claims dead space.
//
// ── CHEAP (WO-864 §4b) ───────────────────────────────────────────────────────
// BuildTimerService republishes every second, so Version alone is NOT a rebuild
// trigger — it moves once a second forever. Cards rebuild only when the queue
// SHAPE changes (SameShape below: job ids / queued flags / stack counts / slot
// count). A plain tick updates the countdown TEXT on the cards that own one and
// touches nothing else. Sprites are cached by QueueIconResolver. No per-frame work
// beyond an int compare; zero cost while the host is inactive.
//
// ASCII ONLY in every TMP string (LiberationSans SDF has no clock glyph -> tofu),
// and state is carried by TEXT ("FREE" / "QUEUED" / the verb), never by colour —
// the owner is red/green colourblind.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeNelle.Core.Jobs;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.UI
{
    /// <summary>
    /// One channel's horizontal rail of queue cards. Built via
    /// <see cref="Build(RectTransform, ChannelId, Options)"/>; self-repainting.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QueueRailView : MonoBehaviour
    {
        // ── FIXED PIXEL GEOMETRY (reference px on a 1080x1920 canvas) ─────────
        // NEVER fractions of the parent. WO-841/WO-852: a text band expressed as a
        // fraction of a short parent resolves to a rect too small for its glyphs and
        // culls to nothing. Each band below is >= its font size * 1.35 so the line
        // box always fits, at any parent height.
        private const float VerbBandPx  = 36f;   // font 26 -> line ~31
        private const float IconBandPx  = 62f;
        private const float NameBandPx  = 36f;   // font 26 -> line ~31
        private const float TimerBandPx = 42f;   // font 30 -> line ~36
        private const float CardPadPx   = 4f;
        private const float CardHeightPx = CardPadPx + VerbBandPx + IconBandPx + NameBandPx + TimerBandPx + CardPadPx; // 184
        private const float CardWidthPx  = 176f;
        private const float BadgeWpx = 52f, BadgeHpx = 30f;

        private const int VerbFont  = 26;
        private const int NameFont  = 26;
        private const int TimerFont = 30;
        private const int BadgeFont = 24;
        private const int InitialFont = 44;

        /// <summary>Host-tunable knobs. Nothing channel-specific ever belongs here.</summary>
        public struct Options
        {
            /// <summary>Card height in reference px (default 184).</summary>
            public float CardHeight;
            /// <summary>Preferred card width in reference px; cards shrink to fit, never below
            /// <see cref="ElarionUiKit.MinTouchPx"/> (default 176).</summary>
            public float CardWidth;
            /// <summary>Gap between cards (default 10).</summary>
            public float Gap;
            /// <summary>Inset around the card row (default 8).</summary>
            public float Pad;
            /// <summary>Draw the shared base-frame plate + gold rail under the cards (default true).</summary>
            public bool ShowPlate;
            /// <summary>Width to assume before the first layout pass resolves (default 440).</summary>
            public float FallbackWidth;

            public static Options Default => new Options
            {
                CardHeight = CardHeightPx,
                CardWidth = CardWidthPx,
                Gap = 10f,
                Pad = 8f,
                ShowPlate = true,
                FallbackWidth = 440f,
            };
        }

        private Options _opts;
        private ChannelId _channel;
        private RectTransform _root;      // the whole rail (plate + cards)
        private RectTransform _cardRow;   // cards are positioned into this by fixed px
        private int _seenVersion = -1;

        // Live cards that own a countdown, so the 1s tick sets ONLY their text.
        private readonly List<TMP_Text> _tickLabels = new List<TMP_Text>();

        // Last rendered shape — the rebuild trigger (Version alone is not one).
        private ObsidianQueueGate.QueueEntry[] _shape;
        private int _shapeSlots = -1;
        // Width the cards were last laid out against. The FIRST Sync runs inside Build(),
        // before any layout pass, so rect.width is still 0 and the fallback width is used.
        // Without this the cards would keep that guessed width forever whenever the queue
        // shape happened not to change afterwards.
        private float _shapeWidth = -1f;

        /// <summary>The fixed pixel height this rail occupies. Lay the host out with it.</summary>
        public float Height => HeightOf(_opts);

        /// <summary>The fixed pixel height a rail with these options WILL occupy — so a host
        /// can size its row BEFORE calling <see cref="Build(RectTransform, ChannelId, Options)"/>.</summary>
        public static float HeightOf(Options o)
        {
            if (o.CardHeight <= 0f) o = Options.Default;
            return o.Pad * 2f + o.CardHeight;
        }

        /// <summary>The channel this rail renders.</summary>
        public ChannelId Channel => _channel;

        // =====================================================================
        //  PUBLIC ENTRY POINT
        // =====================================================================

        /// <summary>
        /// Build a queue card rail for one channel into <paramref name="mount"/>. The rail
        /// pins to the TOP of the mount at a fixed pixel height (<see cref="Height"/>) and
        /// stretches to its width. The host supplies every piece of chrome around it.
        /// </summary>
        public static QueueRailView Build(RectTransform mount, ChannelId channel, Options opts)
        {
            if (mount == null)
            {
                FlowTrace.Fail("QueueUi", "QueueRailView.Build called with a null mount — no rail built.");
                return null;
            }
            if (opts.CardHeight <= 0f) opts = Options.Default;

            var go = new GameObject("QueueRail_" + channel, typeof(RectTransform));
            go.transform.SetParent(mount, false);
            var view = go.AddComponent<QueueRailView>();
            view._opts = opts;
            view._channel = channel;
            view._root = (RectTransform)go.transform;

            // Top-anchored, full width, FIXED pixel height.
            view._root.anchorMin = new Vector2(0f, 1f);
            view._root.anchorMax = new Vector2(1f, 1f);
            view._root.pivot = new Vector2(0.5f, 1f);
            view._root.anchoredPosition = Vector2.zero;
            view._root.sizeDelta = new Vector2(0f, view.Height);

            view.BuildChrome();
            // RESOLVE THE RECTS BEFORE THE FIRST MEASURE. Card width is computed from the
            // card row's real width; freshly parented RectTransforms have not been through a
            // layout pass yet, so without this the first Sync measures garbage and lays out
            // cards far too narrow — the 2026-08-03 headless capture showed exactly that
            // ("+2 MORE" for a 3-card queue, and a timer string spilling outside its card).
            // One synchronous canvas update per rail, at build time only.
            Guard.Try("QueueUi", "resolve rail rects before first layout", Canvas.ForceUpdateCanvases);
            view.Sync();
            FlowTrace.Step("QueueUi", "QueueRailView built for " + channel + " (h=" + view.Height + "px)");
            return view;
        }

        /// <summary>Convenience overload using <see cref="Options.Default"/>.</summary>
        public static QueueRailView Build(RectTransform mount, ChannelId channel)
            => Build(mount, channel, Options.Default);

        /// <summary>Repaint now (rebuilds cards only if the queue shape moved).</summary>
        public void Sync()
        {
            Guard.Try("QueueUi", "sync " + _channel + " rail", () =>
            {
                var st = ObsidianQueueGate.Status;
                _seenVersion = st.Version;
                var wanted = BuildCardModel(st, _channel);
                int slots = st.SlotsOf(_channel);

                if (SameShape(_shape, wanted) && _shapeSlots == slots && _cardRow.childCount > 0 &&
                    Mathf.Abs(MeasuredWidth() - _shapeWidth) < 1f)
                {
                    RefreshTimers(wanted);       // cheap path — text only, no teardown
                    return;
                }
                _shape = wanted;
                _shapeSlots = slots;
                Rebuild(wanted);
            });
        }

        // =====================================================================
        //  Lifecycle — an int compare per frame, a repaint at most once a second.
        // =====================================================================

        private void Update()
        {
            if (_root == null) return;
            int v = ObsidianQueueGate.Status.Version;
            if (v == _seenVersion) return;      // the publisher has not moved
            Sync();
        }

        // =====================================================================
        //  MODEL — snapshot entries + the FREE slots that the snapshot omits.
        // =====================================================================

        // The snapshot lists JOBS. A slot with no job publishes nothing at all, which is
        // exactly how the owner's screen ended up with a large empty dark region where
        // builder #2 should have been. Pad the list out to SlotCount with explicit FREE
        // cards so an idle slot READS as an idle slot.
        private static ObsidianQueueGate.QueueEntry[] BuildCardModel(
            ObsidianQueueGate.WorkQueueStatus st, ChannelId channel)
        {
            var src = st.EntriesOf(channel);
            int slots = Mathf.Max(0, st.SlotsOf(channel));
            int free = Mathf.Max(0, slots - st.BusyOf(channel));
            var outp = new ObsidianQueueGate.QueueEntry[src.Length + free];

            int w = 0;
            for (int i = 0; i < src.Length; i++)
            {
                var e = src[i];
                if (e.StackCount < 1) e.StackCount = 1;
                if (string.IsNullOrEmpty(e.Verb)) e.Verb = "WORK";
                outp[w++] = e;
            }
            for (int i = 0; i < free; i++)
                outp[w++] = new ObsidianQueueGate.QueueEntry
                {
                    Free = true,
                    Verb = "FREE",
                    Label = "Open slot",
                    RemainingSec = -1,
                    StackCount = 1,
                };
            return outp;
        }

        // Shape = everything that changes the CARDS. Deliberately excludes RemainingSec,
        // which moves every second and must never cost a rebuild (WO-864 §4b).
        private static bool SameShape(ObsidianQueueGate.QueueEntry[] a, ObsidianQueueGate.QueueEntry[] b)
        {
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i].Free != b[i].Free) return false;
                if (a[i].Queued != b[i].Queued) return false;
                if (a[i].StackCount != b[i].StackCount) return false;
                if (a[i].TargetTier != b[i].TargetTier) return false;
                if (!string.Equals(a[i].JobId, b[i].JobId, System.StringComparison.Ordinal)) return false;
                if (!string.Equals(a[i].Label, b[i].Label, System.StringComparison.Ordinal)) return false;
                if (!string.Equals(a[i].Verb, b[i].Verb, System.StringComparison.Ordinal)) return false;
            }
            return true;
        }

        // =====================================================================
        //  CHROME — the shared base frame + rail the cards sit on.
        // =====================================================================

        private void BuildChrome()
        {
            if (_opts.ShowPlate)
            {
                var plate = AddImage(_root, "RailPlate", new Color(0.055f, 0.050f, 0.060f, 0.90f));
                ElarionUiKit.ApplyRounded(plate.GetComponent<Image>());

                // The "rail" itself: a thin gold bar pinned to the bottom edge in FIXED px,
                // so the cards read as one connected queue rather than free-floating tiles.
                var bar = new GameObject("RailBar", typeof(Image));
                bar.transform.SetParent(_root, false);
                var brt = (RectTransform)bar.transform;
                brt.anchorMin = new Vector2(0f, 0f);
                brt.anchorMax = new Vector2(1f, 0f);
                brt.pivot = new Vector2(0.5f, 0f);
                brt.sizeDelta = new Vector2(-12f, 3f);
                brt.anchoredPosition = new Vector2(0f, 3f);
                var bimg = bar.GetComponent<Image>();
                bimg.color = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.55f);
                bimg.raycastTarget = false;
            }

            var row = new GameObject("Cards", typeof(RectTransform));
            row.transform.SetParent(_root, false);
            _cardRow = (RectTransform)row.transform;
            _cardRow.anchorMin = Vector2.zero;
            _cardRow.anchorMax = Vector2.one;
            _cardRow.offsetMin = new Vector2(_opts.Pad, _opts.Pad);
            _cardRow.offsetMax = new Vector2(-_opts.Pad, -_opts.Pad);
        }

        // =====================================================================
        //  REBUILD (shape change only)
        // =====================================================================

        private void Rebuild(ObsidianQueueGate.QueueEntry[] model)
        {
            for (int i = _cardRow.childCount - 1; i >= 0; i--)
                Destroy(_cardRow.GetChild(i).gameObject);
            _tickLabels.Clear();

            if (model.Length == 0) return;

            // How many cards actually FIT at a legal touch size? Shrink the card toward
            // MinTouchPx first; only when even that overflows do we drop to a "+N" tail.
            float avail = MeasuredWidth();
            _shapeWidth = avail;

            int n = model.Length;
            bool tail = false;
            float w = CardWidthFor(n, avail);
            while (n > 1 && w < ElarionUiKit.MinTouchPx)
            {
                tail = true; n--;
                w = WidthWithTail(n, avail);      // n cards + one half-width "+N MORE"
            }

            float x = 0f;
            for (int i = 0; i < n; i++)
            {
                BuildCard(model[i], x, w);
                x += w + _opts.Gap;
            }
            if (tail) BuildTailCard(model.Length - n, x, w);
        }

        // The card row's resolved width, or the host's fallback before the first layout pass.
        private float MeasuredWidth()
        {
            float w = _cardRow != null ? _cardRow.rect.width : 0f;
            return w > 1f ? w : _opts.FallbackWidth;
        }

        private float CardWidthFor(int count, float avail)
        {
            if (count <= 0) return _opts.CardWidth;
            float w = (avail - (count - 1) * _opts.Gap) / count;
            return Mathf.Min(_opts.CardWidth, w);
        }

        // n full cards plus a half-width "+N MORE" tail.
        private float WidthWithTail(int count, float avail)
        {
            if (count <= 0) return _opts.CardWidth;
            float w = (avail - count * _opts.Gap) / (count + 0.5f);
            return Mathf.Min(_opts.CardWidth, w);
        }

        // =====================================================================
        //  ONE CARD — verb / portrait / name / timer, every band FIXED PIXELS.
        // =====================================================================

        private void BuildCard(ObsidianQueueGate.QueueEntry e, float x, float w)
        {
            var card = NewCardRoot("Card_" + (e.Free ? "free" : (e.JobId ?? e.Label)), x, w);
            bool dim = e.Free || e.Queued;

            var plateImg = card.GetComponent<Image>();
            plateImg.color = e.Free
                ? new Color(0.10f, 0.10f, 0.12f, 0.55f)     // recessed = nothing here
                : new Color(0.09f, 0.085f, 0.08f, 0.95f);
            var slotSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleSlot, RpgUiCatalog.SlotItem);
            if (slotSprite != null && !e.Free)
            {
                plateImg.sprite = slotSprite;
                plateImg.type = Image.Type.Sliced;
                plateImg.color = Color.white;
            }
            else ElarionUiKit.ApplyRounded(plateImg);

            // 1. VERB — the load-bearing band. Present on EVERY card, icon or not.
            var verb = BandLabel(card, "Verb", CardPadPx, VerbBandPx, VerbFont,
                dim ? ElarionUi.ParchmentDim : ElarionUi.Gilt, bold: true);
            verb.text = Ascii(e.Verb);

            // 2. PORTRAIT — the ENHANCEMENT. Falls back to the name's initial on a plate,
            //    so a missing portrait is a designed state and never a blank card.
            var iconBand = Band(card, "Icon", CardPadPx + VerbBandPx, IconBandPx);
            var art = e.Free ? null : QueueIconResolver.Resolve(e);
            if (art != null)
            {
                var img = new GameObject("Art", typeof(Image));
                img.transform.SetParent(iconBand, false);
                var irt = (RectTransform)img.transform;
                irt.anchorMin = new Vector2(0.5f, 0.5f);
                irt.anchorMax = new Vector2(0.5f, 0.5f);
                irt.pivot = new Vector2(0.5f, 0.5f);
                irt.sizeDelta = new Vector2(IconBandPx, IconBandPx);
                irt.anchoredPosition = Vector2.zero;
                var i2 = img.GetComponent<Image>();
                i2.sprite = art;
                i2.preserveAspect = true;
                i2.raycastTarget = false;
                if (dim) i2.color = new Color(1f, 1f, 1f, 0.55f);
            }
            else
            {
                // ASCII initial (or "+" for an open slot) — no glyph fonts, no tofu.
                var mark = StretchLabel(iconBand, e.Free ? "+" : InitialOf(e.Label),
                    InitialFont, dim ? ElarionUi.ParchmentDim : ElarionUi.Gold, bold: true);
                mark.alignment = TextAlignmentOptions.Center;
            }

            // 3. STACK BADGE — N identical troop trains collapse to ONE card + "xN".
            if (e.StackCount > 1)
            {
                var badge = new GameObject("StackBadge", typeof(Image));
                badge.transform.SetParent(iconBand, false);
                var brt = (RectTransform)badge.transform;
                brt.anchorMin = new Vector2(1f, 0f);
                brt.anchorMax = new Vector2(1f, 0f);
                brt.pivot = new Vector2(1f, 0f);
                brt.sizeDelta = new Vector2(BadgeWpx, BadgeHpx);
                brt.anchoredPosition = Vector2.zero;
                var bimg = badge.GetComponent<Image>();
                bimg.color = new Color(0.04f, 0.04f, 0.05f, 0.95f);
                bimg.raycastTarget = false;
                ElarionUiKit.ApplyRounded(bimg);
                StretchLabel((RectTransform)badge.transform, "x" + e.StackCount,
                    BadgeFont, ElarionUi.Gilt, bold: true).alignment = TextAlignmentOptions.Center;
            }

            // 4. NAME (+ level, already folded into Label by the publisher).
            var name = BandLabel(card, "Name", CardPadPx + VerbBandPx + IconBandPx, NameBandPx,
                NameFont, dim ? ElarionUi.ParchmentDim : ElarionUi.Parchment, bold: false);
            name.text = Ascii(e.Label);
            name.overflowMode = TextOverflowModes.Ellipsis;

            // 5. TIMER — the ONLY place a countdown is printed. The host header carries
            //    none, which is what kills the double-timer (WO-864 bug 1).
            var timer = BandLabel(card, "Timer",
                CardPadPx + VerbBandPx + IconBandPx + NameBandPx, TimerBandPx,
                TimerFont, e.Free ? ElarionUi.ParchmentDim
                                  : (e.Queued ? ElarionUi.ParchmentDim : ElarionUi.Gilt), bold: true);
            timer.text = TimerText(e);

            // Only a RUNNING job's label ticks; queued/free text is static.
            if (!e.Free && !e.Queued && e.RemainingSec >= 0)
                _tickLabels.Add(timer);
        }

        // "+N MORE" — the honest overflow tell when a channel has more slots than the
        // host band can seat at a legal touch size. (Deliberately not a nested scroll
        // view: this rail lives inside the modal's VERTICAL scroll zone and on the
        // always-on HUD, where a horizontal drag surface would fight both.)
        private void BuildTailCard(int more, float x, float w)
        {
            var card = NewCardRoot("Card_more", x, Mathf.Max(ElarionUiKit.MinTouchPx, w * 0.5f));
            var img = card.GetComponent<Image>();
            img.color = new Color(0.10f, 0.10f, 0.12f, 0.55f);
            ElarionUiKit.ApplyRounded(img);
            var lbl = StretchLabel(card, "+" + more + "\nMORE", NameFont, ElarionUi.ParchmentDim, bold: true);
            lbl.alignment = TextAlignmentOptions.Center;
        }

        private RectTransform NewCardRoot(string name, float x, float w)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(_cardRow, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, _opts.CardHeight);
            rt.anchoredPosition = new Vector2(x, 0f);
            go.GetComponent<Image>().raycastTarget = false;   // never eats the host's taps
            return rt;
        }

        // =====================================================================
        //  CHEAP TICK — timer TEXT only. No teardown, no layout, no allocation churn.
        // =====================================================================

        private void RefreshTimers(ObsidianQueueGate.QueueEntry[] model)
        {
            int k = 0;
            for (int i = 0; i < model.Length && k < _tickLabels.Count; i++)
            {
                var e = model[i];
                if (e.Free || e.Queued || e.RemainingSec < 0) continue;
                var lbl = _tickLabels[k];
                if (lbl != null) lbl.text = FormatTime(e.RemainingSec);
                k++;
            }
        }

        // =====================================================================
        //  Band + label primitives — FIXED PIXEL vertical, stretch horizontal.
        // =====================================================================

        private static RectTransform Band(RectTransform card, string name, float yFromTop, float heightPx)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(card, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(-8f, heightPx);          // 4px inset each side
            rt.anchoredPosition = new Vector2(0f, -yFromTop);
            return rt;
        }

        private static TextMeshProUGUI BandLabel(RectTransform card, string name, float yFromTop,
            float heightPx, int font, Color color, bool bold)
        {
            var band = Band(card, name, yFromTop, heightPx);
            return StretchLabel(band, "", font, color, bold);
        }

        private static TextMeshProUGUI StretchLabel(RectTransform parent, string text, int font,
            Color color, bool bold)
        {
            var go = new GameObject("Txt", typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var t = go.GetComponent<TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(t);        // font BEFORE .text — TMP first-generate NRE guard
            t.text = Ascii(text);
            t.fontSize = font;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            t.enableWordWrapping = false;
            // Ellipsis, never Overflow: an over-long string must be clipped INSIDE its card,
            // not painted across the neighbouring card and past the rail's edge.
            t.overflowMode = TextOverflowModes.Ellipsis;
            // ...but SHRINK before truncating. A narrow card (a channel with many slots) was
            // rendering the state word as "QUE..." in the 2026-08-03 capture, which destroys
            // the whole point of encoding state as TEXT rather than colour. Auto-size floors
            // at 60% so the band stays fixed-pixel and the word stays whole.
            t.enableAutoSizing = true;
            t.fontSizeMax = font;
            t.fontSizeMin = Mathf.Max(16f, font * 0.6f);
            if (bold) t.fontStyle = FontStyles.Bold;
            return t;
        }

        // =====================================================================
        //  Pure text helpers (public so the headless oracle can assert them)
        // =====================================================================

        /// <summary>The card's timer band: a countdown, "QUEUED", or "--" for a free slot.
        /// State is spelled OUT — never carried by colour alone (owner is colourblind).</summary>
        public static string TimerText(ObsidianQueueGate.QueueEntry e)
        {
            if (e.Free) return "--";
            if (e.Queued || e.RemainingSec < 0) return "QUEUED";
            return FormatTime(e.RemainingSec);
        }

        /// <summary>ASCII h/m/s countdown ("3m 13s"). No clock glyph — the SDF font has none.</summary>
        public static string FormatTime(int seconds)
        {
            if (seconds < 0) seconds = 0;
            int h = seconds / 3600, m = (seconds % 3600) / 60, s = seconds % 60;
            if (h > 0) return h + "h " + m + "m";
            if (m > 0) return m + "m " + s + "s";
            return s + "s";
        }

        private static string InitialOf(string label)
        {
            if (string.IsNullOrEmpty(label)) return "?";
            foreach (var c in label)
                if (char.IsLetterOrDigit(c)) return char.ToUpperInvariant(c).ToString();
            return "?";
        }

        /// <summary>Strip anything outside printable ASCII — the SDF font tofus the rest.</summary>
        public static string Ascii(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            bool clean = true;
            foreach (var c in s) { if (c > '~' || (c < ' ' && c != '\n')) { clean = false; break; } }
            if (clean) return s;
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (var c in s) if ((c >= ' ' && c <= '~') || c == '\n') sb.Append(c);
            return sb.ToString();
        }

        private static GameObject AddImage(RectTransform parent, string name, Color c)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return go;
        }
    }
}
