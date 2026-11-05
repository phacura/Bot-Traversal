namespace BotTraversal.Core
{
    internal static class FollowerCapabilityPortalPolicy
    {
        internal static bool StrategyEntryReady(double distance)
            => !double.IsNaN(distance)&&!double.IsInfinity(distance)&&distance>=0&&distance<=.65;
        internal static bool ForwardApproach(double targetForward,double minimumCornerForward)
            => !double.IsNaN(targetForward)&&!double.IsInfinity(targetForward)&&!double.IsNaN(minimumCornerForward)&&!double.IsInfinity(minimumCornerForward)&&targetForward>=-.14&&minimumCornerForward>=-.14;
        internal static bool MayReleaseResolvedAuthority(bool writerResolved,bool active,bool pending,bool currentWay,bool safe)
            => writerResolved&&!active&&!pending&&!currentWay&&safe;
        internal static bool OwnsApproach(bool passThroughOff,bool ownsTicket,bool boundPendingStrategy)
            => passThroughOff?ownsTicket:boundPendingStrategy;
        internal static bool MayRetireConsumer(bool resolved,bool successorActive,bool currentWay,bool behind)
            => resolved&&!successorActive&&!currentWay&&behind;
        internal static bool MayYieldApproach(bool currentTicket,bool active,bool currentWay,bool safe)
            => currentTicket&&!active&&!currentWay&&safe;
        internal static bool RebaseRescueAtBody(double planar,double vertical)
            => !double.IsNaN(planar)&&!double.IsInfinity(planar)&&!double.IsNaN(vertical)&&!double.IsInfinity(vertical)&&planar>=0&&planar<=.75&&System.Math.Abs(vertical)<=.65;
        internal static bool LocalApproachPath(bool complete,double length,double direct)
            => complete&&!double.IsNaN(length)&&!double.IsInfinity(length)&&!double.IsNaN(direct)&&!double.IsInfinity(direct)&&length>=0&&direct>=0&&length<=System.Math.Max(1,direct*1.8+.5);
        internal static bool AngularProgress(float previous,float current)
            => !float.IsNaN(previous)&&!float.IsInfinity(previous)&&!float.IsNaN(current)&&!float.IsInfinity(current)&&previous>=0&&previous<=180&&current>=0&&current<=180&&current<previous-2;
        internal static int ResolvedNavigationFloor(int previous,int completedEnd,int nextStart)
            => System.Math.Max(previous,nextStart>=0?System.Math.Min(completedEnd,nextStart):completedEnd);
        internal static bool Approach(bool owns,bool active,bool failure,bool safe,double distance,double localWindow)
            => owns&&!active&&!failure&&safe&&!double.IsNaN(distance)&&!double.IsInfinity(distance)&&distance>localWindow&&distance<=7;
        internal static bool BlockOriginalFollowDestination(string kind,bool owns)
            => owns && (kind=="BotOwner.GoToPoint"||kind=="BotOwner.StopMove"||kind=="BotMover.GoToPointNoWay"||kind=="BotMover.SetPointOnWay"||kind=="BotGoToPointData.Destination"||kind=="BotSteering.LookToDirection"||kind=="BotSteering.LookToPoint"||kind=="BotSteering.LookToMovingDirection");
    }
}
