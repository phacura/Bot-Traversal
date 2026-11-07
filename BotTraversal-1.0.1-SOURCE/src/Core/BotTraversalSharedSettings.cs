namespace BotTraversal.Core
{
    // Same setting semantics as the transitional file contract, but owned entirely by Bot Traversal.
    internal static class BotTraversalSharedSettings
    {
        internal static bool Present => BotTraversal.BotTraversalPlugin.DebugLogEnabled != null;
        internal static bool DebugLogEnabled => BotTraversal.BotTraversalPlugin.DebugLogEnabled?.Value == true;
        internal static bool TraversalEnabled => BotTraversal.BotTraversalPlugin.TraversalEnabled?.Value == true;
    }
}
