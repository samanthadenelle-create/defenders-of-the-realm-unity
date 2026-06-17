namespace DeNelle.Core.UI.Mvvm
{
    /// <summary>
    /// The bind point a panel View implements. The View is a dumb skin: in <see cref="Bind"/> it
    /// subscribes to the ViewModel's Changed event and does an initial render; in <see cref="Unbind"/>
    /// it detaches. It populates its widgets ONLY from ViewModel data and routes user input back as
    /// ViewModel commands — never reaching into game state or services (ARCHITECTURE_PRINCIPLES.md §2).
    ///
    /// Because the contract is View-agnostic, our ElarionUiKit panel and a Blink Obsidian prefab can
    /// both implement IPanelView and bind the SAME ViewModel — swap the skin, keep the wires.
    /// Concrete Views downcast the supplied vm to their specific ViewModel type inside Bind.
    /// </summary>
    public interface IPanelView
    {
        /// <summary>Attach to a ViewModel: subscribe to vm.Changed, then render the initial state.</summary>
        void Bind(IPanelViewModel vm);

        /// <summary>Detach from the current ViewModel (unsubscribe).</summary>
        void Unbind();
    }
}
