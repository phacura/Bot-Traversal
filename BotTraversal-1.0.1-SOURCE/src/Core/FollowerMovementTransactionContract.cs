using System;

namespace BotTraversal.Core
{
    /// <summary>
    /// CF16: transaction policy only. These pure predicates were moved intact from
    /// FollowerMovementWriterAuthority (CF15); no owner state, writer generation,
    /// lease timer, Harmony hook or movement producer is created here.
    /// The existing transaction lifecycle remains the single authority owner.
    /// </summary>
    internal static partial class FollowerMovementWriterAuthority
    {
        private static bool IsTransactionalWrite(string writeKind)
        {
            if (string.IsNullOrEmpty(writeKind))
            {
                return false;
            }

            return writeKind.IndexOf("GoToPoint", StringComparison.Ordinal) >= 0 ||
                   writeKind.IndexOf("StopMove", StringComparison.Ordinal) >= 0 ||
                   writeKind.IndexOf("BotMover.Stop", StringComparison.Ordinal) >= 0 ||
                   writeKind.IndexOf("Teleport", StringComparison.Ordinal) >= 0 ||
                   writeKind.IndexOf("Pause=True", StringComparison.Ordinal) >= 0;
        }

    }
}
