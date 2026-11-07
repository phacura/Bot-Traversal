namespace BotTraversal.Core
{
    // Runtime authority is local to one exact route step; it is never a
    // persistent PlayerValidated/Verified mark or physical success receipt.
    internal static class FollowerCurrentRaidExecutorPolicy
    {
        internal enum Admission { PreserveExisting, NativeDropDown, Reject }
        internal readonly struct Identity
        {
            internal readonly string Bot;
            internal readonly int Raid, Mission, Step, Episode, Edge, Chain;
            internal readonly long Writer;
            internal Identity(string bot, int raid, int mission, int step, int episode,
                int edge, int chain, long writer)
            { Bot=bot; Raid=raid; Mission=mission; Step=step; Episode=episode; Edge=edge; Chain=chain; Writer=writer; }
        }
        internal static bool Matches(Identity origin, Identity current)
        {
            if (origin.Mission <= 0 || origin.Step < 0 || origin.Episode <= 0 ||
                origin.Chain <= 0 || origin.Edge <= 0 || origin.Edge != current.Edge)
                return false;
            // Reuse the V2 typed identity gateway rather than adding another
            // registry or independently reimplementing its epoch/cursor rules.
            var candidate = new FollowerMovementV2IntentGateway.MovementIntent(
                origin.Bot, origin.Raid, origin.Mission, origin.Step, origin.Episode,
                origin.Chain, origin.Writer, FollowerMovementWriterAuthority.WriterDomain.Traversal,
                FollowerMovementV2IntentGateway.MovementIntentKind.NativeActuator, "currentRaidDropDown");
            var live = new FollowerMovementV2IntentGateway.MovementIntent(
                current.Bot, current.Raid, current.Mission, current.Step, current.Episode,
                current.Chain, current.Writer, FollowerMovementWriterAuthority.WriterDomain.Traversal,
                FollowerMovementV2IntentGateway.MovementIntentKind.NativeActuator, "currentRaidDropDown");
            return FollowerMovementV2IntentGateway.CanOffer(candidate, live, out _);
        }
        internal static Admission Select(bool dropDownRequested, bool persistentValidated,
            bool currentRaidAuthority, bool primaryRetired, bool currentStepMatches,
            bool identitiesMatch, bool ticketDomainMatches, bool finiteGeometry)
        {
            if (!dropDownRequested || persistentValidated || !currentRaidAuthority || primaryRetired)
                return Admission.PreserveExisting;
            return currentStepMatches && identitiesMatch && ticketDomainMatches && finiteGeometry
                ? Admission.NativeDropDown : Admission.Reject;
        }
    }
}
