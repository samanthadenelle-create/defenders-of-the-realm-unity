namespace DeNelle.Core.UI.Mvvm
{
    /// <summary>
    /// A pure binding contract for a panel's state + commands. View-AGNOSTIC: it carries NO
    /// UnityEngine UI types (no GameObject/Image/Sprite/RectTransform) so the same ViewModel can
    /// drive ANY View — our ElarionUiKit code-built panel or a Blink Obsidian prefab — and is
    /// unit-testable without a scene (ARCHITECTURE_PRINCIPLES.md §2 / §2c).
    ///
    /// The View binds to this, re-renders on <see cref="Changed"/>, and raises commands; it never
    /// reads game state or calls a service directly. Concrete ViewModels add their own typed data
    /// (e.g. ShopVM.Items) which the View downcasts to in IPanelView.Bind.
    /// </summary>
    public interface IPanelViewModel
    {
        /// <summary>Header/title text for the panel.</summary>
        string Title { get; }

        /// <summary>Raised whenever any bound data changes; the View re-renders in response.</summary>
        event System.Action Changed;

        /// <summary>The universal "dismiss this panel" command.</summary>
        void Close();

        /// <summary>Detach from model/services so no handler leaks (mirror the panels' unsubscribe discipline).</summary>
        void Dispose();
    }
}
