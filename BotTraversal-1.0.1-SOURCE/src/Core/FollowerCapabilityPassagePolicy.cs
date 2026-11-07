using System;
namespace BotTraversal.Core
{
    // Values only. Existing mission, completion receipts and movement scopes own all state.
    internal static class FollowerCapabilityPassagePolicy
    {
        private static bool Finite(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v);
        internal static bool ImmediateDispatchKind(string kind)=>kind=="Jump"||kind=="DropDown"||kind=="WalkThrough"||kind=="Vault"||kind=="Climb";
        internal static bool DropInFlight(bool drop,bool exact,double forward,double span,double lateral,double y,double entryY,double exitY)
            => drop&&exact&&Finite(forward)&&Finite(span)&&span>.2&&forward>=.10&&forward<=span+.20&&
                Finite(lateral)&&lateral>=0&&lateral<=.45&&Finite(y)&&Finite(entryY)&&Finite(exitY)&&
                entryY-exitY>.20&&y<=entryY+.35&&y>=exitY-.35;
        internal static bool ClimbUpperPending(bool climb,bool owned,double rise,double remaining)
            => climb&&owned&&Finite(rise)&&rise>.8&&Finite(remaining)&&remaining>.05;
        internal static bool SameWalkingPortal(double entryGap,double exitGap,double axis)
            => Finite(entryGap)&&entryGap>=0&&entryGap<=.65&&Finite(exitGap)&&exitGap>=0&&exitGap<=1.5&&Finite(axis)&&axis>=.94&&axis<=1.001;
        internal static bool WalkingRecoveryKind(bool walkOnly,string kind)=>!walkOnly||kind=="WalkThrough";
        internal static bool WalkingInterior(bool witnessed,double arc,double length,double side,double height)
            => witnessed&&Finite(arc)&&arc>=0&&Finite(length)&&length>.05&&arc<length&&Finite(side)&&side>=0&&side<=.22&&
                Finite(height)&&Math.Abs(height)<=.35;
        internal static bool WalkingRescue(double forward,double span,double side,double height)
            => Finite(forward)&&Finite(span)&&span>=.20&&forward>=-3.5&&forward<span-.20&&
                Finite(side)&&side>=0&&side<=1.5&&Finite(height)&&Math.Abs(height)<=.65;
        internal static bool WalkExitSample(double planar,double vertical,double tolerance,double maxVertical)
            => Finite(planar)&&planar>=0&&Finite(vertical)&&Finite(tolerance)&&tolerance>0&&Finite(maxVertical)&&maxVertical>0&&
                planar<=tolerance&&Math.Abs(vertical)<=maxVertical;
        internal static double AttemptStallAge(bool current,double now,double stalledAt,double requestedAt)
            => !Finite(now)||!Finite(stalledAt)||(current&&!Finite(requestedAt))?0:
                Math.Max(0,now-(current?Math.Max(stalledAt,requestedAt):stalledAt));
        internal static bool TerminalMayYield(bool terminal,bool active)=>terminal&&!active;
        internal static bool FreshInput(bool exact,bool path,bool nativeWay,bool returning,bool pending,double age,double delta)
            => exact&&!path&&!nativeWay&&!returning&&!pending&&Finite(age)&&age>=0&&age<=.20&&Finite(delta)&&delta<=.000001&&delta>=0;
        internal static bool PassageInFlight(bool corridor,bool exact,double forward,double span,double lateral,double height)
            => corridor&&exact&&Finite(forward)&&Finite(span)&&span>.2&&forward>=.10&&forward<=span+.20&&
                Finite(lateral)&&lateral>=0&&lateral<=.45&&Finite(height)&&Math.Abs(height)<=.45;
        internal static bool RescueAtBody(bool safe,bool grounded,bool finite)=>safe&&grounded&&finite;
        internal static bool WorldInput(bool requested,bool route,bool strategy)=>requested||route||strategy;
        internal static double ContinuationBudget(bool jumpOffNav,double remaining,double local,double speed,double padding,double minimum)
        {
            double distance=jumpOffNav?remaining:local;
            if(!Finite(distance)||distance<0||!Finite(speed)||speed<=0||!Finite(padding)||padding<0||!Finite(minimum)||minimum<0)return minimum;
            return Math.Min(jumpOffNav?8:4.25,Math.Max(minimum,distance/Math.Max(.50,speed)+padding));
        }
        internal static bool LandingCorrection(bool nativeOffNav,bool successor,double heading,double distance)
            => nativeOffNav&&!successor&&Finite(heading)&&heading>=.5&&heading<=1.001&&Finite(distance)&&distance>.15;
        internal static bool ExitOverrun(bool jump,bool supported,double forward,double span)
            => jump&&supported&&Finite(forward)&&Finite(span)&&span>.2&&forward>span+.75;
        internal static bool LocalEntryPath(bool hasPath,double endpointGap,double cornerDistance,double entryDistance,double cornerHeading)
            => hasPath&&Finite(endpointGap)&&endpointGap>=0&&endpointGap<=.35&&Finite(cornerDistance)&&cornerDistance>=0&&
                Finite(entryDistance)&&entryDistance>=0&&cornerDistance<=entryDistance+1&&Finite(cornerHeading)&&cornerHeading>=.5&&cornerHeading<=1.001;
        internal static bool ManualEntry(bool current,bool nativeActor,bool healthy,bool lane,double distance)
            => current&&!nativeActor&&Finite(distance)&&distance>=0&&distance<=3.5&&(!healthy||!lane&&distance<=1.25);
        internal static bool ObservedLowerDrop(bool drop,bool witness,bool exact,bool below,double exitDistance)
            => drop&&witness&&exact&&below&&Finite(exitDistance)&&exitDistance>=0&&exitDistance<=1.75;
        internal static double ImmediateEntryDistance(bool observedLowerDrop,double spatial,double planar)
            => observedLowerDrop?planar:spatial;
        internal static bool CanonicalQueue(bool proven,bool walking,double entryGap,double exitGap,double entryHeight,double exitHeight,double axis,double limit)
            => proven&&walking&&Finite(limit)&&limit>0&&Finite(entryGap)&&entryGap>=0&&entryGap<=limit&&
                Finite(exitGap)&&exitGap>=0&&exitGap<=limit&&Finite(entryHeight)&&Math.Abs(entryHeight)<=.65&&
                Finite(exitHeight)&&Math.Abs(exitHeight)<=.65&&Finite(axis)&&axis>=.85&&axis<=1.001;
        internal static bool QueuedDropWitness(bool drop,bool witness,bool active,bool sameMission,bool sameGeneration)
            => drop&&witness&&active&&sameMission&&sameGeneration;
        internal static bool WalkingIdentity(bool sameEdge,bool samePortal)=>sameEdge||samePortal;
        internal static bool SideCenterTurn(bool sideOnly,bool correction,double angle)
            => !sideOnly&&ObstacleCenterTurn(correction,angle);
        internal static bool ForwardCorner(double from,double to,double lateral,double height)
            => Finite(from)&&Finite(to)&&to>from+.02&&to<=from+.65&&Finite(lateral)&&lateral>=0&&lateral<=.22&&Finite(height)&&Math.Abs(height)<=.35;
        internal static bool RestoreSemanticWalkEntry(bool selectedStrategy)=>!selectedStrategy;
        internal static double ObstacleCenterTolerance(double lateral,double radial)
            => Finite(lateral)&&lateral>0&&Finite(radial)&&radial>0?Math.Min(lateral,radial*.5):0;
        internal static bool QueueClaimCurrent(long expected,long actual,bool sameOwner)
            => expected>0&&actual==expected&&sameOwner;
        internal static bool NativeEgressKind(string kind,string requested,bool current)
            => kind=="WalkThrough"||current&&(kind=="DropDown"||string.IsNullOrEmpty(kind)&&requested=="DropDown");
        internal static bool RescueScanRequired(bool transient,bool selectedSequence,bool selfRescue)
            => transient&&(!selectedSequence||selfRescue);
        internal static bool CorridorSegmentLength(double distance,bool polyline)
            => Finite(distance)&&distance>=(polyline?.18:.20);
        internal static bool SupportedEntry(bool grounded,bool heightReady,bool measuredRamp)
            => grounded&&(heightReady||measuredRamp);
        internal static bool EntryHeight(bool ground,double height)=>ground&&Finite(height)&&Math.Abs(height)<=.65;
        internal static bool ObstacleHold(bool obstacle,bool reacquire,double distance,double lateral,double limit,double angle,double angleLimit)
            => obstacle&&!reacquire&&Finite(distance)&&distance>=0&&distance<=.65&&Finite(lateral)&&lateral>=0&&
                Finite(limit)&&limit>=0&&lateral<=limit&&Finite(angle)&&Finite(angleLimit)&&angle>angleLimit;
        internal static bool RescueSettled(double speed,double heading)
            => Finite(speed)&&speed>=0&&speed<=.15&&Finite(heading)&&heading>=.99&&heading<=1.001;
        internal static bool LocalJoin(bool strict,double join,double length,double height)
            => strict&&Finite(join)&&join>.85&&join<=3&&Finite(length)&&length>=0&&length<=Math.Max(1,join*1.8+.5)&&
                Finite(height)&&Math.Abs(height)<=.35;
        internal static bool RelatedPrefix(bool chain,int gap,double heading)
            => chain&&gap>=0&&gap<=8&&Finite(heading)&&heading>=.3&&heading<=1.001;
        internal static bool CoveredLowerDrop(bool exact,bool between,bool ground,bool chain,int gap,int tail,double axis,
            double above,double endHeight,double forward,double lateral,double bodyGap,double age)
            => exact&&between&&ground&&chain&&gap>=0&&gap<=4&&tail>=0&&tail<=12&&Finite(axis)&&axis>=.85&&axis<=1.001&&
                Finite(above)&&above>=.5&&Finite(endHeight)&&Math.Abs(endHeight)<=.45&&Finite(forward)&&forward>=-.25&&forward<=2.25&&
                Finite(lateral)&&lateral>=0&&lateral<=.65&&Finite(bodyGap)&&bodyGap>=0&&bodyGap<=.35&&Finite(age)&&age>=0&&age<=6;
        internal static bool DropLead(bool current,bool ground,double forward,double staging,double lateral,double limit,double height)
            => current&&ground&&Finite(forward)&&Finite(staging)&&staging>0&&forward>=-staging&&forward<-.10&&
                Finite(lateral)&&lateral>=0&&Finite(limit)&&limit>=0&&lateral<=limit&&Finite(height)&&Math.Abs(height)<=.45;
        internal static bool ForwardLaneSample(bool leading,double body,double sample)
            => !leading||Finite(body)&&Finite(sample)&&sample>=body+.05;
        internal static bool TargetAfterFloor(bool generic,int target,int floor)=>!generic||target>Math.Max(0,floor);
        internal static bool StaleCompletedTarget(bool exact,double age,double candidateGap,double candidateLeader,double bodyLeader,double bodyForward,double candidateForward,double leaderForward)
            => exact&&Finite(age)&&age>=0&&age<=6&&Finite(candidateGap)&&candidateGap>=0&&candidateGap<=1&&
                Finite(candidateLeader)&&candidateLeader>2.5&&Finite(bodyLeader)&&bodyLeader>=0&&bodyLeader<=4&&
                Finite(bodyForward)&&bodyForward>1.25&&Finite(candidateForward)&&candidateForward<bodyForward-1&&
                Finite(leaderForward)&&leaderForward>=bodyForward-.25;
        internal static bool HeadEntry(bool current,bool owns,bool ground,double distance,double height)
            => current&&owns&&ground&&Finite(distance)&&distance>.65&&distance<=2.25&&Finite(height)&&Math.Abs(height)<=.65;
        internal static bool SemanticContinuation(bool supported,double height,bool supportedRamp=false)
            => supported&&Finite(height)&&(Math.Abs(height)<=.65||supportedRamp);
        internal static bool RampEnvelope(double height,double distance)
            => Finite(height)&&Math.Abs(height)>.65&&Finite(distance)&&distance>0&&distance<=8;
        internal static bool RampSample(bool hit,double height,double normal,bool blocked)
            => hit&&Finite(height)&&Math.Abs(height)<=.35&&Finite(normal)&&normal>=.65&&normal<=1.001&&!blocked;
        internal static bool CompletedFollowRange(double distance,double initial)
            => Finite(distance)&&distance>=0&&Finite(initial)&&initial>=0&&distance<=Math.Max(7,Math.Min(25,initial+1));
        internal static bool DestinationMatches(bool hasPath,double gap)
            => hasPath&&Finite(gap)&&gap>=0&&gap<=.35;
        internal static bool GroundLead(bool owns,bool safe,bool grounded,bool active,double distance,double height,bool support,bool blocked)
            => owns&&safe&&grounded&&!active&&Finite(distance)&&distance>.65&&distance<=5&&Finite(height)&&Math.Abs(height)<=.65&&support&&!blocked;
        internal static bool ReacquireFollowTarget(double distance)=>Finite(distance)&&distance>2.5;
        internal static bool LiveFollow(bool safe,bool grounded,bool active,bool pending,bool newWork,bool nativeWay,int floor,long generation,bool follow)
            => safe&&grounded&&!active&&!pending&&!newWork&&!nativeWay&&floor>0&&generation>0&&follow;
        internal static bool StaleNativeCorner(bool path,double bodyGap,double remainingGap,double heading)
            => path&&Finite(bodyGap)&&bodyGap>.50&&Finite(remainingGap)&&remainingGap>.35&&Finite(heading)&&heading<-.25&&heading>=-1.001;
        internal static bool JumpDispatch(bool current,bool debt,bool active,bool failure,double distance)
            => current&&debt&&!active&&!failure&&Finite(distance)&&distance>=0&&distance<=2.50;
        internal static bool EntryTurnHold(double heading)=>!Finite(heading)||heading<.95;
        internal static bool ObstacleCenterTurn(bool correction,double angle)=>correction&&(!Finite(angle)||angle>20);
        internal static bool ObstacleNear(double distance)=>Finite(distance)&&distance>=0&&distance<.4;
        internal static double ObstacleEntrySpeed(double distance,double desired)
            => Finite(distance)&&distance>=0&&distance<.4?Math.Min(desired,.35):desired;
        internal static bool PassageLease(bool generic,bool recovery,bool terminal,int target,int floor,double until,double now)
            => generic&&recovery&&!terminal&&target>Math.Max(0,floor)&&Finite(until)&&Finite(now)&&now<=until;
        internal static bool CorridorExtension(int oldEnd,int currentEnd,double entryGap,double previousSpan,double forward,double side,double height)
            => currentEnd>oldEnd&&Finite(entryGap)&&entryGap>=0&&entryGap<=.02&&Finite(previousSpan)&&previousSpan>=.05&&
                Finite(forward)&&forward>=previousSpan&&forward<=8&&Finite(side)&&side>=0&&side<=.35&&Finite(height)&&Math.Abs(height)<=.65;
        internal static bool CursorFloor(bool chronological,bool movement,bool physical)=>!chronological||!movement||physical;
        internal static bool CorridorExit(bool grounded,double length,double projection,double local,double lateralLimit,double height)
            => grounded&&Finite(length)&&length>=.05&&Finite(projection)&&projection>=length&&Finite(local)&&local>=0&&
                Finite(lateralLimit)&&lateralLimit>0&&local<=lateralLimit&&Finite(height)&&Math.Abs(height)<=.65;
        internal static bool NativeFollowReceipt(long issued,long live,bool clean,bool complete,bool path,double gap)
            => issued>0&&issued==live&&clean&&complete&&path&&Finite(gap)&&gap>=0&&gap<=.35;
        internal static bool NativeFollowIdle(bool clean,bool grounded,bool path,bool moving,bool actor,bool input)
            => clean&&grounded&&!path&&!moving&&!actor&&input;
        internal static bool LeftAdmittedIngress(bool admitted,bool active,bool failure,double distance,bool passage=false)
            => admitted&&!active&&!failure&&!passage&&Finite(distance)&&distance>7;
        internal static double LocalAimFraction(double distance)
            => Finite(distance)&&distance>0?Math.Min(1,.60/distance):0;
        internal static bool GroundAim(bool grounded,bool finite,double height,bool support,bool blocked)
            => grounded&&finite&&Finite(height)&&Math.Abs(height)<=.45&&support&&!blocked;
        internal static bool PhysicalWalkEntry(bool lane,bool finite,double gap,double bodyDistance)
            => !lane&&finite&&Finite(gap)&&gap>.05&&Finite(bodyDistance)&&bodyDistance>=0&&bodyDistance<=3.5;
        internal static bool ObstacleNeedsCenter(double side,double maximum)
            => !Finite(side)||!Finite(maximum)||side<0||maximum<=0||side>maximum;
        internal static bool GenericFollowStart(bool safe,bool grounded,bool current,bool pending,bool actor,bool tight,bool path,
            double direct,double length,double limit,bool recent)
            => safe&&grounded&&current&&!pending&&!actor&&tight&&path&&Finite(direct)&&direct>=0&&
                Finite(length)&&length>=0&&length<=Math.Max(direct*1.60,direct+5.5)&&
                Finite(limit)&&limit>0&&(direct<=limit||recent);
        internal static bool GenericFollowCommit(bool ran,bool same,bool path,bool idle,double distance,double limit)
            => ran&&same&&(path||idle&&Finite(distance)&&distance>=0&&Finite(limit)&&limit>0&&distance<=limit);
        internal static bool NormalFormation(bool follow,bool probe,bool debt,bool generic,bool nativeWay)
            => follow&&!probe&&!debt&&!generic&&!nativeWay;
        internal static bool EntryStillAhead(double forward,double lateral,double height)
            => !Finite(forward)||!Finite(lateral)||!Finite(height)||forward<=.10||lateral>.65||Math.Abs(height)>.65;
        internal static double QueueDistance(bool corridor,double entry,double exit)
            => corridor?exit:entry;
        internal static bool UnownedSequenceProbe(bool reserved)=>!reserved;
        internal static bool CaptureJumpEntry(bool current,bool native,bool jump)=>current&&native&&jump;
        internal static bool RescueObstacle(bool grounded,bool hit,double normalY,double opposition,double distance)
            => grounded&&hit&&Finite(normalY)&&normalY>=-1.001&&normalY<.65&&
                Finite(opposition)&&opposition>=.35&&opposition<=1.001&&Finite(distance)&&distance>=0&&distance<=.85;
        internal static bool WalkSeam(bool current,bool safe,bool grounded,bool active,bool nativeActor,double stalled,double speed)
            => current&&safe&&!nativeActor&&(active||grounded&&Finite(stalled)&&stalled>=.28&&Finite(speed)&&speed>=0&&speed<=.20);
        internal static bool WalkSeamSupport(double height)=>Finite(height)&&height>=-.15&&height<=.20;
        internal static bool RecordedRecoveryScope(bool recorded,bool typedDebt)=>recorded||typedDebt;
        internal static bool RecordedFallback(int episode)=>episode>0;
        internal static bool ForeignCommitment(bool live,bool debt,bool strategy,bool exact)=>live&&debt&&!strategy&&!exact;
        internal static bool ClimbSuccessor(bool current,bool consumed,bool observed,bool crossed,bool supported,bool drop,double gap,double nextGap)
            => current&&consumed&&observed&&crossed&&supported&&drop&&Finite(gap)&&gap>=0&&gap<=.35&&Finite(nextGap)&&nextGap>=0&&nextGap<=2.5;
        internal static bool CenterSideOnly(bool correction,double distance)=>correction&&Finite(distance)&&distance>=0&&distance<=.65;
        internal static bool Lane(bool witnessed,double lateral,double vertical)
            => witnessed&&Finite(lateral)&&Finite(vertical)&&lateral>=0&&lateral<=.22&&Math.Abs(vertical)<=.35;
        internal static bool Center(bool walking,bool nativeActor,double lateral)
            => walking&&!nativeActor&&Finite(lateral)&&lateral>.18;
        internal static bool SprintIntent(bool? captured,string state,string fallback)
            => captured??string.Equals(state??fallback,"Sprint",StringComparison.OrdinalIgnoreCase);
        internal static bool RetiredEpisode(bool walking,bool retired,bool consumed)=>walking&&(retired||consumed);
        internal static bool HoldLanding(bool nativeJump,bool grounded,double heading)
            => nativeJump&&grounded&&(!Finite(heading)||heading<.955);
        internal static bool CoveredDropOverlap(bool nativeExact,bool overlap,bool grounded,double startGap,double axisDot,
            double endExtension,double endHeight,double witnessForward,double witnessLateral,double witnessBelow,double age)
            => nativeExact&&overlap&&grounded&&Finite(startGap)&&startGap>=0&&startGap<=.60&&Finite(axisDot)&&axisDot>=.98&&axisDot<=1.001&&
                Finite(endExtension)&&Math.Abs(endExtension)<=.75&&Finite(endHeight)&&Math.Abs(endHeight)<=.35&&
                Finite(witnessForward)&&witnessForward>=-.10&&Finite(witnessLateral)&&witnessLateral>=0&&witnessLateral<=1.35&&
                Finite(witnessBelow)&&witnessBelow>=.50&&Finite(age)&&age>=0&&age<=6;
    }
}
