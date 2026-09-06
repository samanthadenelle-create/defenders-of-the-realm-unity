using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Core.UI
{
    public static partial class ElarionUiKit
    {
        /// <summary>
        /// Paints the shared antique-gold perimeter used by obsidian cards.
        /// Native scalable geometry keeps the bezel intact across card aspect ratios.
        /// </summary>
        public static void GoldPerimeter(Transform host)
        {
            void Edge(string name, Vector2 min, Vector2 max)
            {
                var edge = AddImage(host, name, min, max,
                    new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, .95f), false);
                edge.GetComponent<Image>().raycastTarget = false;
            }

            Edge("GoldTop",    new Vector2(.018f, .982f), new Vector2(.982f, .992f));
            Edge("GoldBottom", new Vector2(.018f, .008f), new Vector2(.982f, .018f));
            Edge("GoldLeft",   new Vector2(.008f, .018f), new Vector2(.018f, .982f));
            Edge("GoldRight",  new Vector2(.982f, .018f), new Vector2(.992f, .982f));
        }
    }
}
