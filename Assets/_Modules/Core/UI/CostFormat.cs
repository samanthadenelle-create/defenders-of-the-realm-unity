using System;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace DeNelle.Core.UI
{
    public readonly struct CostPart
    {
        public readonly string ConceptId;
        public readonly string Word;
        public readonly int Amount;
        public readonly string AmountText;

        public CostPart(string conceptId, string word, int amount)
        {
            ConceptId = (conceptId ?? string.Empty).Trim().ToLowerInvariant();
            Word = string.IsNullOrWhiteSpace(word) ? "Resource" : word.Trim();
            Amount = amount;
            AmountText = ElarionUi.CompactNumber(amount);
        }
    }

    public static class CostFormat
    {
        public static IReadOnlyList<CostPart> Parts(IEnumerable<(string conceptId, string word, int amount)> raw)
        {
            if (raw == null) return Array.Empty<CostPart>();
            var parts = new List<CostPart>();
            foreach (var item in raw)
                if (item.amount > 0)
                    parts.Add(new CostPart(item.conceptId, item.word, item.amount));
            return parts;
        }

        public static string Words(IReadOnlyList<CostPart> parts)
        {
            if (parts == null || parts.Count == 0) return string.Empty;
            var words = new string[parts.Count];
            for (int i = 0; i < parts.Count; i++)
                words[i] = parts[i].Word + " " + parts[i].AmountText;
            return string.Join("  ", words);
        }

        internal static Sprite IconOrWarn(CostPart part)
        {
            var icon = UiStyle.Icon(part.ConceptId);
            if (icon == null)
                FlowTrace.Once("CostFormat", "no-icon-" + part.ConceptId,
                    "no icon for concept=" + part.ConceptId + "; using full-word fallback");
            return icon;
        }
    }

    public static class CostRowElement
    {
        public static VisualElement Build(IReadOnlyList<CostPart> parts, string prefix = null)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            if (!string.IsNullOrEmpty(prefix)) AddText(row, prefix);
            if (parts == null) return row;
            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                var icon = CostFormat.IconOrWarn(part);
                if (icon != null)
                {
                    var image = new UnityEngine.UIElements.Image { sprite = icon, scaleMode = ScaleMode.ScaleToFit };
                    image.style.width = 22; image.style.height = 22; image.style.marginLeft = 4;
                    row.Add(image);
                    AddText(row, part.AmountText);
                }
                else AddText(row, part.Word + " " + part.AmountText);
            }
            return row;
        }

        private static void AddText(VisualElement row, string text)
        {
            var label = new Label(text);
            label.style.marginLeft = 4;
            label.style.color = ElarionUi.Parchment;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(label);
        }
    }

    public static partial class ElarionUiKit
    {
        public static RectTransform CostRow(Transform parent, IReadOnlyList<CostPart> parts,
            Vector2 anchorMin, Vector2 anchorMax, Color color, string prefix = null)
        {
            var root = new GameObject("CostRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            root.transform.SetParent(parent, false);
            var rt = (RectTransform)root.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var layout = root.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false; layout.childControlHeight = true;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
            layout.spacing = 4;
            if (!string.IsNullOrEmpty(prefix)) AddCostText(root.transform, prefix, color);
            if (parts == null) return rt;
            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                var icon = CostFormat.IconOrWarn(part);
                if (icon == null) AddCostText(root.transform, part.Word + " " + part.AmountText, color);
                else
                {
                    var iconGo = new GameObject("Icon_" + part.ConceptId, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(LayoutElement));
                    iconGo.transform.SetParent(root.transform, false);
                    var image = iconGo.GetComponent<UnityEngine.UI.Image>();
                    image.sprite = icon; image.preserveAspect = true; image.raycastTarget = false;
                    var size = iconGo.GetComponent<LayoutElement>(); size.preferredWidth = 22; size.preferredHeight = 22;
                    AddCostText(root.transform, part.AmountText, color);
                }
            }
            return rt;
        }

        private static void AddCostText(Transform parent, string value, Color color)
        {
            var go = new GameObject("CostText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.text = value; text.fontSize = 13; text.fontStyle = FontStyles.Bold;
            text.color = color; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            var layout = go.GetComponent<LayoutElement>();
            layout.preferredWidth = Math.Max(28, value.Length * 8); layout.preferredHeight = 24;
        }
    }
}
