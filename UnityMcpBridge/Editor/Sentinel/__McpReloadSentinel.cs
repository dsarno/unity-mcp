#if UNITY_EDITOR
namespace MCP.Reload
{
    // Toggling this constant (1 <-> 2) changes IL and guarantees an assembly bump.
    internal static class __McpReloadSentinel
    {
        internal const int Tick = 2;
    }
}
#endif
