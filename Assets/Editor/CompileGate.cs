using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// Headless compile gate. Opening the project in batchmode forces a full
    /// script recompile; if everything compiles, <see cref="Run"/> executes and
    /// prints the marker below. If compilation fails, the marker never appears and
    /// the batch log carries the CS errors instead. CLI uses this as the
    /// authoritative "does the tree compile" check before committing.
    /// </summary>
    public static class CompileGate
    {
        public static void Run()
        {
            Debug.Log("COMPILE_GATE_OK :: scripts compiled clean");
        }
    }
}
