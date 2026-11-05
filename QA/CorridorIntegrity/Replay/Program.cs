using System.Text.Json;
using P=BotTraversal.Core.FollowerCapabilityFailureSequence;
var cases=new List<string>();
void Check(bool ok,string label){if(!ok)throw new Exception(label);cases.Add(label);}
// Recorded ordering: Native failed, Climb 11 selected, ordinary Jump 111 offered in the SAME frame.
Check(P.OwnsStrategy(P.Stage.SelfRescue,true),"246.7365 selected Climb owns semantic Jump 111");
Check(!P.CanAllocateStrategy(true,true,false,false),"246.7365 reject ordinary Jump before its probe exists");
Check(P.CanAllocateStrategy(true,true,true,true),"next owned Climb dispatch consumes the selected attempt");
Check(!P.CanAllocateStrategy(true,true,true,false),"owned source alone cannot substitute the wrong physical kind or mission");
Check(!P.CanAllocateStrategy(true,false,true,true),"no repeated allocation while selected attempt already runs");
Check(!P.OwnsStrategy(P.Stage.SelfRescue,false),"retired or advanced semantic step cannot block the next normal capability");
// Native Jump really took off and landed on the obstacle top, not yet a rejected landing.
Check(P.ContinueExecutedJump(true,true,true,true,false,false),"245.8295 takeoff to 246.7365 supported landing remains eligible for bounded forward continuation");
Check(!P.ContinueExecutedJump(true,false,false,false,false,false),"an allocated entry probe does not manufacture physical continuation authority");
for(int mask=0;mask<16;mask++)Check(P.CanAllocateStrategy((mask&1)!=0,(mask&2)!=0,(mask&4)!=0,(mask&8)!=0)==(mask==15),"allocation permissions "+mask);
for(int mask=0;mask<64;mask++){
 bool current=(mask&1)!=0,consumed=(mask&2)!=0,attempted=(mask&4)!=0,started=(mask&8)!=0,partial=(mask&16)!=0,rejected=(mask&32)!=0;
 Check(P.ContinueExecutedJump(current,consumed,attempted,started,partial,rejected)==(current&&consumed&&attempted&&(started||partial||rejected)),"physical continuation permissions "+mask);
}
foreach(var stage in Enum.GetValues<P.Stage>())foreach(bool current in new[]{false,true})
 Check(P.OwnsStrategy(stage,current)==(current&&(stage==P.Stage.Alternate||stage==P.Stage.SelfRescue||stage==P.Stage.RetryAlternate)),"stage lifetime "+stage+" "+current);
// Five observed overwritten strategies must reject the ordinary semantic allocation, then accept only their frozen candidate.
foreach(var sample in new[]{"Ada 246.7365 Climb11->Jump111","Ghost 246.825 Climb11->Jump111","Ada 364.801 Vault146->Jump111","Ghost 364.837 Vault146->Jump111","Ada 382.508 Vault146->Jump111"}){
 Check(!P.CanAllocateStrategy(true,true,false,false),sample+" foreign callback blocked");
 Check(P.CanAllocateStrategy(true,true,true,true),sample+" selected callback accepted");
}
// Recorded long Jump: 12.606m observation, completed WT222 goes to a 12m grounded bridge,
// then direct egress timeout must advance the ladder BEFORE the old deferred teleport clears its probe.
Check(!P.IsCancellation("monotonicFailure:egressTimeout:routeNoDirectionalProgress"),"208.909 WT222 egress timeout is a real attempt failure");
Check(!P.IsCancellation("exitRecoveryRequested:compileFix43RecoveryTeleport:egressTimeout"),"teleport-labelled recovery request remains a failure offer");
Check(!P.IsCancellation("sequenceNativeAdmission:EFT component unavailable"),"EFT inability advances explicitly, no unavailable-word silent veto");
foreach(var cancel in new[]{"manualRegroup","consoleAbort","raidOver"})Check(P.IsCancellation(cancel),"explicit cancellation stays excluded "+cancel);
Check(P.CanClosePassageBoundary(true,true,true,true,true,true,true),"202.866 finite WT proof can hand to adjacent recorded retained Jump without completing Jump");
Check(!P.CanClosePassageBoundary(true,false,true,true,true,true,true),"being on a NavMesh island alone cannot fabricate WT proof");
Check(!P.CanClosePassageBoundary(true,true,true,true,true,true,false),"forced egress cannot gain clean Native completion");
for(int mask=0;mask<128;mask++)Check(P.CanClosePassageBoundary((mask&1)!=0,(mask&2)!=0,(mask&4)!=0,(mask&8)!=0,(mask&16)!=0,(mask&32)!=0,(mask&64)!=0)==(mask==127),"finite boundary permissions "+mask);
Check(P.EntryBudget(2.267285f,1.1f,.55f,4.25f)>1.1f,"437.639 bridge entry 2.267m is not limited to the close-range 1.1s acquisition");
Check(P.EntryBudget(2.267285f,1.1f,.55f,4.25f)<=4.25f,"DropDown approach still has an absolute finite budget");
Check(P.EntryBudget(0,1.1f,.55f,4.25f)==1.1f,"on-entry admission keeps the original close-range budget");
foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity,-1f})Check(P.EntryBudget(invalid,1.1f,.55f,4.25f)==1.1f,"invalid entry distances cannot extend wait "+invalid);
Check(P.PastObstacleEntry(true,.13932237f,.1f),"367.303 alternate entry already +.139m is past the unchanged native +.10m maximum");
Check(!P.PastObstacleEntry(true,.099f,.1f),"still-admissible obstacle entry remains eligible");
Check(!P.PastObstacleEntry(false,.139f,.1f),"obstacle-only maximum cannot reject another capability kind");
// Pass-through OFF: exactly one admitted nearest contender, then one second AFTER completion.
{
 var queue=new BotTraversal.Core.FollowerPortalAdmission();
 var live=new HashSet<string>{"Ada","Chris","Ghost","Knight"};
 bool Live(string id,int mission,int step)=>live.Contains(id)&&mission>0&&step==0;
 bool Admit(string id,int m,double now,bool nearest)=>queue.Admit(id,m,0,0,0,0,1,0,now,nearest,Live);
 Check(Admit("Ada",1,10,true),"nearest Ada owns portal first");
 Check(!Admit("Ghost",2,10,true)&&!Admit("Knight",3,10,true)&&!Admit("Chris",4,10,true),"three followers cannot overtake the existing head");
 Check(queue.Inspect(0,0,0,1,0)?.Bot=="Ada","wait telemetry identifies the actual ticket owner");
 queue.Complete("Ada",1,0,10.2);
 Check(!Admit("Ghost",2,11.199,true),"no release before one full second after completed action");
 Check(Admit("Ghost",2,11.201,true),"release after one full second");
 Check(!Admit("Knight",3,11.201,true),"next callback cannot mint another owner");
 queue.Cancel("Ghost",12);
 Check(!Admit("Knight",3,12.99,true)&&Admit("Knight",3,13.001,true),"cancellation also retains one-second spacing");
 live.Remove("Knight");Check(!Admit("Chris",4,13.1,true),"invalid head retires with cooldown, no stale permanent lock");
 Check(Admit("Chris",4,14.101,true),"next live follower proceeds after invalid-head cooldown");
 queue.Clear();Check(Admit("Ghost",2,14.2,true),"pass-through toggle clear removes old portal debt");
}
Check(BotTraversal.Core.FollowerPortalAdmission.Closer(1.0,"Ada",1.5,"Ghost"),"nearest measured candidate wins");
Check(!BotTraversal.Core.FollowerPortalAdmission.Closer(2.0,"Ada",1.5,"Ghost"),"farther candidate does not win");
Check(BotTraversal.Core.FollowerPortalAdmission.Closer(1,"Ada",1,"Ghost"),"equal distance uses stable profile ordering");
Check(!BotTraversal.Core.FollowerPortalAdmission.Closer(double.NaN,"Ada",1,"Ghost"),"invalid distance is not a contender");
Check(BotTraversal.Core.FollowerPortalAdmission.HeadStalled(15.01,10,11,false,false),"no physical progress for four seconds advances head failure");
Check(!BotTraversal.Core.FollowerPortalAdmission.HeadStalled(15.01,10,11,true,false),"queue cannot interrupt active Native body");
Check(!BotTraversal.Core.FollowerPortalAdmission.HeadStalled(15.01,10,11,false,true),"existing failure sequence retains its own bounded timing");
Check(BotTraversal.Core.FollowerPortalAdmission.HeadStalled(18.01,10,18,false,false),"moving target publications cannot renew total head lease forever");
Check(!BotTraversal.Core.FollowerPortalAdmission.HeadStalled(12,10,11,false,false),"healthy head retains dispatch opportunity");
Check(!P.EntryRequiresPointAim(false,.06f>.16f),"177.063 angle159deg lateral.06 must face axis, not turn toward point behind +.12m");
Check(P.EntryRequiresPointAim(false,.75f>.16f),"real lateral deviation still aims at correction point");
Check(P.EntryRequiresPointAim(true,false),"explicit retry staging still aims at rear staging point");
// Four physical gate corners, bot-specific tickets and one-second handoff under repeated callbacks.
for(int portal=0;portal<8;portal++){
 var q2=new BotTraversal.Core.FollowerPortalAdmission();bool Live(string id,int mission,int step)=>true;
 for(int bot=0;bot<4;bot++){
  double now=100+bot*2.1;
  Check(q2.Admit("b"+bot,bot+1,0,portal*3,0,0,1,0,now,true,Live),"serialized entry "+portal+" "+bot);
  for(int other=bot+1;other<4;other++)Check(!q2.Admit("b"+other,other+1,0,portal*3,0,0,1,0,now+.05,true,Live),"no concurrent native owner "+portal+" "+bot+" "+other);
  q2.Complete("b"+bot,bot+1,0,now+.1);
 }
}
// Cooldown separates BOTS; an exact adjacent step of the same bot must keep action continuity.
{
 var q3=new BotTraversal.Core.FollowerPortalAdmission();bool Live(string id,int m,int s)=>true;
 bool A(string id,int mission,int step,double now,bool nearest)=>q3.Admit(id,mission,step,0,0,0,1,0,now,nearest,Live);
 Check(A("a",1,0,1,true),"chain head first step admitted");q3.Complete("a",1,0,2);
 Check(!A("b",2,0,2.1,true),"another bot still waits after first action");
 Check(!A("a",9,1,2.1,true),"new mission cannot borrow predecessor continuation");
 Check(!A("a",1,2,2.1,true),"skipped step cannot borrow predecessor continuation");
 Check(A("a",1,1,2.1,false),"same bot exact adjacent action continues without a forced brake");
 Check(q3.Inspect(0,0,0,1,0)?.LastAdmissionContinued==true,"continuation receipt is explicit in admission telemetry");
 Check(!A("b",2,0,2.2,true),"same resource remains exclusively owned during adjacent native action");
 q3.Complete("a",1,1,2.3);Check(!A("b",2,0,3.299,true)&&A("b",2,0,3.301,true),"next bot waits until same shared resource is physically released plus one second");
}
// Exact terminal receipts survive replacing a mission/cursor, without consuming another bot or a new demonstration.
{
 var b=new BotTraversal.Core.FollowerCapabilityResolvedDebt();
 Check(!b.Contains(1,"Chris",53,172),"receipt absent before lifecycle Applied");
 b.Mark(1,"Chris",53,172);b.Mark(1,"Chris",53,172);
 Check(b.Contains(1,"Chris",53,172),"608.267 completion blocks 610.488 reenrollment of same debt");
 foreach(var x in new[]{(2,"Chris",53,172),(1,"Ghost",53,172),(1,"Chris",54,172),(1,"Chris",53,173)})
  Check(!b.Contains(x.Item1,x.Item2,x.Item3,x.Item4),"receipt identity isolation "+x);
 b.Mark(1,"Chris",49,99);Check(b.Contains(1,"Chris",49,99),"interleaved lower episode is exact, not a numeric frontier");
 b.Mark(0,"Chris",1,1);Check(!b.Contains(0,"Chris",1,1),"invalid raid cannot issue receipt");
 b.Clear();Check(!b.Contains(1,"Chris",53,172),"new raid clears receipts");
}
foreach(double d in new[]{0,4.99,5,5.01,6.99,7,7.01,double.NaN,double.PositiveInfinity,-1})foreach(bool retained in new[]{false,true})
 Check(BotTraversal.Core.FollowerPortalAdmission.InWindow(d,retained)==(!double.IsNaN(d)&&!double.IsInfinity(d)&&d>=0&&d<=(retained?7:5)),"queue boundary hysteresis "+d+" "+retained);
{
 var b=new BotTraversal.Core.FollowerPortalAdmission();bool Live(string id,int m,int s)=>true;
 bool A(double now)=>b.Admit("a",1,0,0,0,0,1,0,now,true,Live);
 Check(A(1),"first claim");var first=b.Inspect(0,0,0,1,0).ClaimSerial;
 Check(b.Owns("a",1,0)&&!b.Owns("a",1,1)&&!b.Owns("b",1,0),"active animation retains only exact owner");
 Check(A(1.1)&&b.Inspect(0,0,0,1,0).ClaimSerial==first,"owned callback cannot reset head deadline");
 b.Complete("a",1,0,2);Check(A(3.01)&&b.Inspect(0,0,0,1,0).ClaimSerial>first,"real reacquisition receives new progress deadline");
 var second=b.Inspect(0,0,0,1,0).ClaimSerial;b.Clear();Check(A(4)&&b.Inspect(0,0,0,1,0).ClaimSerial>second,"passthrough toggle does not reuse claim identity");
}
foreach(string kind in new[]{"BotOwner.GoToPoint","BotOwner.StopMove","BotMover.GoToPointNoWay","BotMover.SetPointOnWay","BotGoToPointData.Destination","Player.Teleport","Player.EnableSprint","unknown"})foreach(bool owns in new[]{false,true})
 Check(BotTraversal.Core.FollowerCapabilityPortalPolicy.BlockOriginalFollowDestination(kind,owns)==(owns&&(kind=="BotOwner.GoToPoint"||kind=="BotOwner.StopMove"||kind=="BotMover.GoToPointNoWay"||kind=="BotMover.SetPointOnWay"||kind=="BotGoToPointData.Destination")),"precise original Follow destination policy "+kind+" "+owns);
// Queue holding at 3.67m must have an explicit typed entry approach when released.
for(int mask=0;mask<16;mask++)foreach(double d in new[]{0,2.25,2.26,3.67,5.5,7,7.01,double.NaN,double.PositiveInfinity}){
 bool own=(mask&1)!=0,active=(mask&2)!=0,failure=(mask&4)!=0,safe=(mask&8)!=0;
 Check(BotTraversal.Core.FollowerCapabilityPortalPolicy.Approach(own,active,failure,safe,d,2.25)==(own&&!active&&!failure&&safe&&!double.IsNaN(d)&&!double.IsInfinity(d)&&d>2.25&&d<=7),"queue head approach permissions "+mask+" "+d);
}
foreach(int previous in new[]{0,20,80})foreach(int end in new[]{10,40,90})foreach(int next in new[]{-1,15,45,100}){
 int floor=BotTraversal.Core.FollowerCapabilityPortalPolicy.ResolvedNavigationFloor(previous,end,next);
 Check(floor==Math.Max(previous,next>=0?Math.Min(end,next):end),"resolved navigation prefix "+previous+" "+end+" "+next);
 Check(floor>=previous,"navigation never rewinds consumed progress");
 if(previous<=next&&next>=0)Check(floor<=next,"navigation cannot skip the next unresolved entry");
}
Check(BotTraversal.Core.FollowerCapabilityPortalPolicy.ResolvedNavigationFloor(100,140,125)==125,"overlapping WT completion preserves unfinished successor before WT tail");
foreach(float previous in new[]{float.NaN,float.PositiveInfinity,-1,0,2,3,20,115,180,181})foreach(float current in new[]{float.NaN,float.NegativeInfinity,-1,0,2,3,20,115,180,181})
 Check(BotTraversal.Core.FollowerCapabilityPortalPolicy.AngularProgress(previous,current)==(!float.IsNaN(previous)&&!float.IsInfinity(previous)&&!float.IsNaN(current)&&!float.IsInfinity(current)&&previous>=0&&previous<=180&&current>=0&&current<=180&&current<previous-2),"measured angular progress "+previous+" "+current);
Check(BotTraversal.Core.FollowerCapabilityPortalPolicy.AngularProgress(114.9f,5.83f),"321.991 actual turn counts as progress during EFT transition");
Check(!BotTraversal.Core.FollowerCapabilityPortalPolicy.AngularProgress(5.83f,5.5f),"subthreshold jitter cannot renew stall clock");
foreach(bool complete in new[]{false,true})foreach(double direct in new[]{0,1,3,5,double.NaN,double.PositiveInfinity,-1})foreach(double length in new[]{0,1,3,5,10,55.99,double.NaN,double.PositiveInfinity,-1})
 Check(BotTraversal.Core.FollowerCapabilityPortalPolicy.LocalApproachPath(complete,length,direct)==(complete&&!double.IsNaN(length)&&!double.IsInfinity(length)&&!double.IsNaN(direct)&&!double.IsInfinity(direct)&&length>=0&&direct>=0&&length<=Math.Max(1,direct*1.8+.5)),"local ticket path budget "+complete+" "+direct+" "+length);
Check(!BotTraversal.Core.FollowerCapabilityPortalPolicy.LocalApproachPath(true,55.99,3.0),"PathComplete alone never authorizes a 56m detour to a nearby ticket entry");
// Recorded Ghost 148.082 completion retained a 160.49/21.33 entrance goal through 164s.
for(int mask=0;mask<16;mask++){
 bool resolved=(mask&1)!=0,active=(mask&2)!=0,current=(mask&4)!=0,behind=(mask&8)!=0;
 Check(BotTraversal.Core.FollowerCapabilityPortalPolicy.MayRetireConsumer(resolved,active,current,behind)==(resolved&&!active&&!current&&behind),"exact completed consumer retirement "+mask);
 Check(BotTraversal.Core.FollowerCapabilityPortalPolicy.MayYieldApproach(resolved,active,current,behind)==(resolved&&!active&&!current&&behind),"queue owner handoff permission "+mask);
}
for(int mask=0;mask<32;mask++)Check(BotTraversal.Core.FollowerCapabilityPortalPolicy.MayReleaseResolvedAuthority((mask&1)!=0,(mask&2)!=0,(mask&4)!=0,(mask&8)!=0,(mask&16)!=0)==(mask==17),"terminal writer release "+mask);
for(int mask=0;mask<8;mask++){
 bool off=(mask&1)!=0,ticket=(mask&2)!=0,strategy=(mask&4)!=0;
 Check(BotTraversal.Core.FollowerCapabilityPortalPolicy.OwnsApproach(off,ticket,strategy)==(off?ticket:strategy),"pass-through and selected-strategy approach ownership "+mask);
}
foreach(double planar in new[]{0,.5,.75,.751,3.696,7,double.NaN,double.PositiveInfinity,-1})foreach(double vertical in new[]{0,.65,.651,-.65,-.651,4,double.NaN,double.PositiveInfinity})
 Check(BotTraversal.Core.FollowerCapabilityPortalPolicy.RebaseRescueAtBody(planar,vertical)==(!double.IsNaN(planar)&&!double.IsInfinity(planar)&&!double.IsNaN(vertical)&&!double.IsInfinity(vertical)&&planar>=0&&planar<=.75&&Math.Abs(vertical)<=.65),"generalized rescue actual frontier "+planar+" "+vertical);
Check(!BotTraversal.Core.FollowerCapabilityPortalPolicy.RebaseRescueAtBody(3.696,.592),"145.814 Ghost cannot pretend an obstacle exists at his remote body");
{
 var receipts=new BotTraversal.Core.FollowerCapabilityResolvedDebt();receipts.MarkWriter(2,"Ghost",5);
 Check(receipts.HasWriter(2,"Ghost",5),"only applied resolved generation can retire terminal ownership");
 foreach(var x in new[]{(2,"Ghost",6L),(2,"Knight",5L),(3,"Ghost",5L),(0,"Ghost",5L),(2,"Ghost",0L)})Check(!receipts.HasWriter(x.Item1,x.Item2,x.Item3),"newer writer and foreign bot/raid preserved "+x);
 receipts.MarkWriter(0,"Ghost",1);Check(!receipts.HasWriter(0,"Ghost",1),"invalid writer receipt rejected");
 receipts.Clear();Check(!receipts.HasWriter(2,"Ghost",5),"raid clear removes writer receipts");
}
foreach(double target in new[]{-1,-.141,-.14,0,3,double.NaN,double.PositiveInfinity})foreach(double corner in new[]{-1,-.141,-.14,0,3,double.NaN,double.NegativeInfinity})
 Check(BotTraversal.Core.FollowerCapabilityPortalPolicy.ForwardApproach(target,corner)==(!double.IsNaN(target)&&!double.IsInfinity(target)&&!double.IsNaN(corner)&&!double.IsInfinity(corner)&&target>=-.14&&corner>=-.14),"pre-return approach cannot retreat through completed prefix "+target+" "+corner);
foreach(double distance in new[]{0,.3,.65,.6501,1.25,3.696,double.NaN,double.PositiveInfinity,-1})
 Check(BotTraversal.Core.FollowerCapabilityPortalPolicy.StrategyEntryReady(distance)==(!double.IsNaN(distance)&&!double.IsInfinity(distance)&&distance>=0&&distance<=.65),"selected native strategy waits for actual 3D entry "+distance);
// Ada 235.113: physically in Jump window, Facing=false; Sprint must not push her over it.
for(int mask=0;mask<64;mask++)Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.Walk((mask&1)!=0,(mask&2)!=0,(mask&4)!=0,(mask&8)!=0,(mask&16)!=0,(mask&32)!=0)==((mask&1)==0&&(mask&62)!=0),"bounded ingress walking ownership "+mask);
for(int mask=0;mask<16;mask++)foreach(double forward in new[]{-5.5,-1.2,-1.199,-.219,.02,double.NaN,double.PositiveInfinity})
 Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.JumpSprint((mask&1)!=0,(mask&2)!=0,(mask&4)!=0,(mask&8)!=0,forward)==((mask&1)!=0&&(mask&2)!=0&&(mask&8)!=0&&!double.IsNaN(forward)&&!double.IsInfinity(forward)&&((mask&4)!=0||forward<=-1.2)),"aligned Jump acceleration ownership "+mask+" "+forward);
Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.JumpSprint(true,false,true,true,-.107),"235.113 wrong-facing Sprint is not a valid acceleration phase");
Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.JumpSprint(true,true,false,true,-.219),"cannot initiate a new Sprint from stationary at last takeoff centimetres");
Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.JumpSprint(true,true,true,true,-.219),"existing aligned rolling Sprint remains continuous");
foreach(double age in new[]{0,3,3.01,4,9.99,10,10.01,double.NaN,-1})foreach(double stall in new[]{0,2.99,3,3.01,double.PositiveInfinity,-1})
 Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.StrategyExpired(age,stall)==(double.IsNaN(age)||double.IsInfinity(age)||double.IsNaN(stall)||double.IsInfinity(stall)||age<0||stall<0||age>10||stall>3),"strategy measured progress with finite deadline "+age+" "+stall);
Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.StrategyExpired(5,1),"walking progress is not incorrectly expired at old four-second wall clock");
Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.StrategyExpired(10.01,0),"continuous target refresh cannot extend absolute deadline");
foreach(bool prefix in new[]{false,true})foreach(double body in new[]{-3,0,3})foreach(double entry in new[]{-1,0,2})foreach(double target in new[]{-4,-.15,0,2})foreach(double corner in new[]{-4,-.15,0,2})
 Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.PrefixPath(prefix,body,entry,target,corner)==(!prefix||(target>=Math.Min(body,Math.Min(0,entry))-.14&&corner>=Math.Min(body,Math.Min(0,entry))-.14)),"resolved prefix plane, not body monotonicity "+prefix+" "+body+" "+entry+" "+target+" "+corner);
Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.PrefixPath(false,0,0,-.5,-.5),"first entrance may make a short backward alignment correction");
Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.PrefixPath(true,1,0,-1,-1),"cannot retreat through a completed predecessor");
Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.PrefixPath(true,3,0,2,1),"local correction stays in unfinished segment and remains legal");
foreach(float distance in new[]{0,.2,1,3,7,float.NaN})foreach(float requested in new[]{0,.16f,.55f,1,2,float.PositiveInfinity,float.NaN}){
 float capped=BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.WalkSpeed(requested,distance);
 Check(!float.IsNaN(capped)&&capped>=0&&capped<=.55f,"all ingress speed writes remain bounded "+distance+" "+requested);
}
foreach(bool before in new[]{false,true})foreach(bool after in new[]{false,true})
 Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.KeepNativeMove(before,after)==(!before||after),"only a BT-retired current native consumer is suppressed "+before+" "+after);
Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.WalkSpeed(1,1.4f)==.55f,"aligned Run Jump is not throttled to stationary .16 precision speed");
Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.WalkSpeed(0,1.4f)==0,"explicit alignment hold remains zero despite Run momentum allowance");

// RC131 recorded incident: selection at 582.414; no body progress at 585.425 must actually leave SelfRescue.
Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.StrategyExpired(3.011,3.011),"Chris selected approach has expired at585.425");
Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.PendingDeadline("sequenceStrategyApproachNoMeasuredProgress"),"measured expiry bypasses duplicate callback suppression");
Check(P.AfterFailure(P.Stage.SelfRescue)==P.Stage.ReturnToEntry,"expired rescue progresses to unfinished entry return");
Check(P.AfterFailure(P.Stage.RetryAlternate)==P.Stage.RecoveryTeleport,"retry failure exhausts into recovery rather than restarting Native");
foreach(string reason in new[]{"portalQueueHeadNoMeasuredProgress","nativeEntryFailed","arbitrary","",null})
 Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.PendingDeadline(reason),"duplicate or foreign callback does not consume selected chance "+reason);
foreach(bool safe in new[]{false,true})foreach(bool waiting in new[]{false,true})foreach(bool returning in new[]{false,true})foreach(bool selected in new[]{false,true})
 Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.ProtectLocalBody(safe,waiting,returning,selected)==(safe&&(waiting||returning||selected)),"body projection protects local owner only "+safe+waiting+returning+selected);
Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.NativeExpired(5,4.01),"body already far beyond old entry still times out without local queue ticket");
Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.NativeExpired(5,1),"moving toward finite current exit remains eligible");
Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.NativeExpired(30,1,30),"long valid WT not constrained to short entrance wall clock");
Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.NativeExpired(66.01,0,30),"long corridor still has finite absolute budget");
foreach(float d in new[]{0,.2f,.65f,1,1.4f,3,7}){
 float fast=BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.ApproachSpeed(1,d,false,true,false);
 float retry=BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.ApproachSpeed(1,d,true,true,false);
 Check(fast>=.85f&&fast<=1,"aligned normal ground ingress is brisk "+d);
 Check(retry<=.55f&&retry>=.16f,"explicit retry keeps no-Sprint precision "+d);
 Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.ApproachSpeed(1,d,false,true,true)==1,"aligned Run Jump keeps normal takeoff momentum "+d);
 Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.ApproachSpeed(0,d,false,true,true)==0,"explicit zero hold never accelerated "+d);
 Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.ApproachSpeed(1,d,false,false,false)<fast,"lateral/heading error receives braking instead of normal straight speed "+d);
}
foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity,-1})
 Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.ApproachSpeed(1,invalid,false,true,true)==0,"invalid ingress distance cannot arm movement "+invalid);


Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.RejectPostCompletionProjection(true,true,true,4.231,.439580679,0),"448.987 Knight .439m native body snap rejected during exact completed epoch");
Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.RejectPostCompletionProjection(true,true,true,3.733,.3128965,.0016627),"507.264 Chris repeated .312m body snap rejected");
foreach(bool safe in new[]{false,true})foreach(bool exact in new[]{false,true})foreach(bool grounded in new[]{false,true})foreach(double age in new[]{-1,0,4.231,6,6.001,double.NaN})foreach(double delta in new[]{0,.1,.2,.201,.439,double.PositiveInfinity}){
 bool expected=safe&&exact&&grounded&&!double.IsNaN(age)&&age>=0&&age<=6&&!double.IsInfinity(delta)&&delta>.20;
 Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.RejectPostCompletionProjection(safe,exact,grounded,age,delta,0)==expected,"projection ownership and correction budget "+safe+exact+grounded+age+" "+delta);
}
Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.RejectPostCompletionProjection(true,true,true,1,.1,-.198),"small terrain height adjustment remains native");
Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.RejectPostCompletionProjection(true,true,true,1,.1,-.301),"large vertical body correction rejected on supported body");


foreach(float d in new[]{0,.1f,.3f,.65f,1.2f}){
 Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.ApproachSpeed(1,d,true,true,true)==.55f,"aligned retry Run Jump retains walking takeoff momentum without Sprint "+d);
 Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.ApproachSpeed(0,d,true,true,true)==0,"retry alignment zero hold is never overridden by momentum floor "+d);
}
Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.ApproachSpeed(1,.1f,true,false,true)==.16f,"misaligned retry Jump still uses precision alignment");


foreach(bool selected in new[]{false,true})foreach(bool actuator in new[]{false,true})foreach(bool stable in new[]{false,true})
 Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.ConsumeReadyEntry(selected,actuator,stable)==(stable||selected&&actuator),"ready entry consumes exact sample "+selected+actuator+stable);
Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.ConsumeReadyEntry(true,true,false),"364.734 Jump at+.157 can consume ready sample before364.752+.224 leaves unchanged+.16 limit");
Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.ConsumeReadyEntry(true,false,false),"DropDown still waits for stable native entry");
foreach(double speed in new[]{0,1,3,4.5,6,8}){
 double brake=BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.BrakingDistance(speed);
 Check(brake>=1.2,"measured braking distance has a finite minimum "+speed);
 Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.RouteSprint(false,true,false,true,-10,0,1,1,speed),"long aligned demonstrated corridor starts Sprint "+speed);
 Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.RouteSprint(false,true,true,true,-brake,0,1,1,speed),"brakes before a nonJump entry based on measured speed "+speed);
 Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.RouteSprint(false,true,false,true,-4.9,0,1,1,speed),"braking cannot rearm short Sprint bursts as speed falls "+speed);
 Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.RouteSprint(true,true,true,true,-.1,0,1,1,speed),"ready aligned Jump preserves rolling Sprint into valid native window "+speed);
 Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.RouteSprint(true,true,true,false,-.1,0,1,1,speed),"cannot Sprint into blocked CanJump window "+speed);
 Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.RouteSprint(true,true,true,true,.161,0,1,1,speed),"cannot borrow Sprint past exact Jump frontier "+speed);
}
foreach(bool jump in new[]{false,true})foreach(bool demonstrated in new[]{false,true})foreach(bool current in new[]{false,true})foreach(double lateral in new[]{0,.25,.251,double.NaN})foreach(double heading in new[]{1,.97,.969,double.NaN}){
 bool allowed=BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.RouteSprint(jump,demonstrated,current,true,-10,lateral,heading,1,4.5);
 Check(allowed==(demonstrated&&!double.IsNaN(lateral)&&lateral<=.25&&!double.IsNaN(heading)&&heading>=.97),"demonstrated geometry controls acceleration "+jump+demonstrated+current+lateral+heading);
}
foreach(double tangent in new[]{.979,double.NaN,double.PositiveInfinity})Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.RouteSprint(false,true,true,true,-10,0,1,tangent,4.5),"sharp/invalid route tangent cannot Sprint "+tangent);
foreach(double speed in new[]{-1,8.01,double.NaN,double.PositiveInfinity})Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.RouteSprint(false,true,true,true,-10,0,1,1,speed),"invalid or excessive measured speed enters braking "+speed);
for(int mask=0;mask<128;mask++){
 bool owns=(mask&1)!=0,safe=(mask&2)!=0,active=(mask&4)!=0,failure=(mask&8)!=0,ground=(mask&16)!=0,support=(mask&32)!=0,blocked=(mask&64)!=0;
 Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.Connector(owns,safe,active,failure,ground,2.49,.65,.4,.25,support,blocked)==(owns&&safe&&!active&&!failure&&ground&&support&&!blocked),"queued recorded connector permissions "+mask);
}
foreach(double distance in new[]{2.25,5.001,double.NaN})Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.Connector(true,true,false,false,true,distance,.6,.4,.25,true,false),"connector never changes native start distance or long route "+distance);
foreach(double join in new[]{.851,double.NaN})Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.Connector(true,true,false,false,true,2.49,join,.4,.25,true,false),"off-demonstration body cannot invent a connector "+join);
foreach(double segment in new[]{.15,1.501,double.NaN})Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.Connector(true,true,false,false,true,2.49,.6,segment,.25,true,false),"recorded connector cannot skip a gap "+segment);
Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.Connector(true,true,false,false,true,2.49,.6,.4,.451,true,false),"recorded connector cannot bypass a climb height");


for(int mask=0;mask<256;mask++){
 bool exact=(mask&1)!=0,safe=(mask&2)!=0,ground=(mask&4)!=0,active=(mask&8)!=0,pending=(mask&16)!=0,path=(mask&32)!=0,moving=(mask&64)!=0,input=(mask&128)!=0;
 Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.StopCompletedFollowInput(exact,safe,ground,active,pending,path,moving,input,.73,1.736)==(exact&&safe&&ground&&!active&&!pending&&!path&&!moving&&input),"completed close Follow input ownership "+mask);
}
Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.StopCompletedFollowInput(true,true,true,false,false,false,false,true,.73,1.736),"510.584 Ghost mover stopped close to leader yet input forward one must quiesce");
Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.StopCompletedFollowInput(true,true,true,false,false,false,false,true,15.86,.26),"526.700 Knight still follows leader15.86m away; do not globally stop completed actors");
foreach(double distance in new[]{-1,1.251,double.NaN})Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.StopCompletedFollowInput(true,true,true,false,false,false,false,true,distance,1),"completed stop requires close real distance "+distance);
foreach(double age in new[]{-1,6.001,double.NaN})Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.StopCompletedFollowInput(true,true,true,false,false,false,false,true,.73,age),"old completion cannot govern new Follow input "+age);


foreach(double speed in new[]{0,.5,1.1,3.805,double.NaN})foreach(double heading in new[]{-.5,.94,.955,1,double.NaN})
 Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.ReadyJumpMotion(speed,heading,1.1,.955)==(!double.IsNaN(speed)&&!double.IsNaN(heading)&&speed>=1.1&&heading>=.955),"same-sample Jump requires measured forward motion "+speed+" "+heading);


// RC135 real board entry: .864m off lane was labelled physical merely by crossing the plane.
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.Lane(true,.8644547,.326933),"321.534 Knight never owns board direction while .864m off lane");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.Lane(true,.16245,.10),"aligned Ghost board approach remains eligible");
foreach(bool witness in new[]{false,true})foreach(double side in new[]{-.1,0,.18,.22,.221,.864,double.NaN})foreach(double height in new[]{0,.35,.351,-.351,double.NaN})
 Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.Lane(witness,side,height)==(witness&&!double.IsNaN(side)&&side>=0&&side<=.22&&!double.IsNaN(height)&&Math.Abs(height)<=.35),"finite entry lane "+witness+side+" "+height);
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.Center(true,false,.2),"slight walking deviation corrects exact world direction");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.Center(true,true,.864),"native step-assist actor cannot be replaced by lane centering");
foreach(bool? intent in new bool?[]{null,false,true})foreach(string state in new[]{null,"Run","Sprint","Idle"})foreach(string historical in new[]{"Run","Sprint"})
 Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.SprintIntent(intent,state,historical)==(intent??string.Equals(state??historical,"Sprint",StringComparison.OrdinalIgnoreCase)),"captured intent or backward-compatible fallback "+intent+state+historical);
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.SprintIntent(true,"Run","Run"),"actual Sprint flag survives Run animation state");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.SprintIntent(false,"Sprint","Sprint"),"explicit no-Sprint intent cannot borrow historical Sprint");
foreach(bool walking in new[]{false,true})foreach(bool retired in new[]{false,true})foreach(bool consumed in new[]{false,true})
 Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.RetiredEpisode(walking,retired,consumed)==(walking&&(retired||consumed)),"already-retired recorded action never creates failure debt "+walking+retired+consumed);
foreach(bool native in new[]{false,true})foreach(bool ground in new[]{false,true})foreach(double heading in new[]{-.578,.093,.72,.954,.955,.998,double.NaN})
 Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.HoldLanding(native,ground,heading)==(native&&ground&&(double.IsNaN(heading)||heading<.955)),"supported landing heading before translation "+native+ground+heading);
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.CoveredDropOverlap(true,true,true,.2,.999,.53,-.0415,3.14,.24,2.84,.1),"355.648 nativeDD88 covers overlapping upperWT229; no return upstairs");
for(int mask=0;mask<8;mask++)Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.CoveredDropOverlap((mask&1)!=0,(mask&2)!=0,(mask&4)!=0,.2,.999,.53,-.04,3.14,.24,2.84,.1)==(mask==7),"overlap requires current Native certificate/sequence/ground "+mask);
foreach(var row in new[]{new double[]{.601,.999,.53,-.04,3.14,.24,2.84,.1},new double[]{.2,.979,.53,-.04,3.14,.24,2.84,.1},new double[]{.2,-1,.53,-.04,3.14,.24,2.84,.1},new double[]{.2,.999,.751,-.04,3.14,.24,2.84,.1},new double[]{.2,.999,.53,-.351,3.14,.24,2.84,.1},new double[]{.2,.999,.53,-.04,-.101,.24,2.84,.1},new double[]{.2,.999,.53,-.04,3.14,1.351,2.84,.1},new double[]{.2,.999,.53,-.04,3.14,.24,.499,.1},new double[]{.2,.999,.53,-.04,3.14,.24,2.84,6.001},new double[]{double.NaN,.999,.53,-.04,3.14,.24,2.84,.1}})
 Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.CoveredDropOverlap(true,true,true,row[0],row[1],row[2],row[3],row[4],row[5],row[6],row[7]),"different passage/old proof cannot erase WT "+string.Join(",",row));
{
 var mission=new BotTraversal.Core.FollowerTraversalLifecycle.MissionState();var drop=new BotTraversal.Core.FollowerTraversalLifecycle.StepState();
 var origin=new BotTraversal.Core.FollowerTraversalLifecycle.Identity("Ada",2,616,0,28,88,17,0,16);var probe=new BotTraversal.Core.FollowerTraversalLifecycle.ProbeState(origin,drop);
 Check(drop.Attach(probe),"real Native predecessor binds original identity");
 Check(probe.PublishCompletion(origin,drop,BotTraversal.Core.FollowerTraversalLifecycle.ActionOutcome.NativePhysical)==BotTraversal.Core.FollowerTraversalLifecycle.CommitResult.Applied,"real Native predecessor has physical outcome");
 Check(mission.Resolve(drop,origin,drop,origin,BotTraversal.Core.FollowerTraversalLifecycle.Resolution.Executed,true,true)==BotTraversal.Core.FollowerTraversalLifecycle.CommitResult.Applied,"actual DropDown advances before recorder overlap");
 var wt=new BotTraversal.Core.FollowerTraversalLifecycle.StepState();var wtOrigin=new BotTraversal.Core.FollowerTraversalLifecycle.Identity("Ada",2,616,1,29,229,17,0,16);
 Check(mission.Resolve(wt,wtOrigin,wt,wtOrigin,BotTraversal.Core.FollowerTraversalLifecycle.Resolution.Subsumed,true,false)==BotTraversal.Core.FollowerTraversalLifecycle.CommitResult.Applied,"qualified actor coverage uses existing debt resolution seam");
 Check(mission.Cursor==2&&wt.Probe==null,"coverage does not allocate or fabricate another WT actuator attempt");
}

// RC135: stale native input never acquires another step, actor, path or strategy lifetime.
foreach(bool exact in new[]{false,true})foreach(bool path in new[]{false,true})foreach(bool way in new[]{false,true})foreach(bool returning in new[]{false,true})foreach(bool pending in new[]{false,true})
foreach(double age in new[]{-.001,0,.1,.2,.201,double.NaN,double.PositiveInfinity})foreach(double delta in new[]{0,.000001,.000002,double.NaN})
 Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.FreshInput(exact,path,way,returning,pending,age,delta)==(exact&&!path&&!way&&!returning&&!pending&&!double.IsNaN(age)&&age>=0&&age<=.2&&!double.IsNaN(delta)&&delta>=0&&delta<=.000001),"nonrenewing exact input receipt "+exact+path+way+returning+pending+age+delta);
foreach(bool corridor in new[]{false,true})foreach(bool exact in new[]{false,true})foreach(double forward in new[]{-.1,.099,.1,3,5.4,5.61,double.NaN})foreach(double side in new[]{0,.45,.451,double.NaN})foreach(double height in new[]{0,-.45,.451,double.NaN})
 Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.PassageInFlight(corridor,exact,forward,5.4,side,height)==(corridor&&exact&&!double.IsNaN(forward)&&forward>=.1&&forward<=5.6&&!double.IsNaN(side)&&side>=0&&side<=.45&&!double.IsNaN(height)&&Math.Abs(height)<=.45),"physical corridor progress never re-approaches its own entry "+corridor+exact+forward+side+height);
for(int mask=0;mask<8;mask++){
 Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.RescueAtBody((mask&1)!=0,(mask&2)!=0,(mask&4)!=0)==(mask==7),"rescue from safe current grounded body "+mask);
 Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.WorldInput((mask&1)!=0,(mask&2)!=0,(mask&4)!=0)==(mask!=0),"current world intent outranks retired mover frame "+mask);
}
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.ContinuationBudget(true,6.299642,2.750665,1.85,1.14,2)>4.5,"278.748 Chris six-meter debt cannot borrow a 2.7m local-target deadline");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.ContinuationBudget(true,100,2,1.85,1.14,2)==8,"long Jump debt remains bounded to eight seconds");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.ContinuationBudget(false,100,2,1.85,1.14,2)<4.25,"ordinary continuation retains local budget");
foreach(bool native in new[]{false,true})foreach(bool successor in new[]{false,true})foreach(double heading in new[]{-1,.49,.5,.98,1,double.NaN})foreach(double distance in new[]{0,.15,.151,6,double.NaN})
 Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.LandingCorrection(native,successor,heading,distance)==(native&&!successor&&!double.IsNaN(heading)&&heading>=.5&&heading<=1.001&&!double.IsNaN(distance)&&distance>.15),"supported off-Nav landing corrects toward exit without replacing typed successor "+native+successor+heading+distance);
foreach(bool jump in new[]{false,true})foreach(bool ground in new[]{false,true})foreach(double forward in new[]{0,6.94,7.69,7.7,double.NaN})
 Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.ExitOverrun(jump,ground,forward,6.94)==(jump&&ground&&!double.IsNaN(forward)&&forward>6.94+.75),"unproven Jump cannot run indefinitely past real exit "+jump+ground+forward);

// RC135 recorded double-board entry resources and queue join.
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.LocalEntryPath(true,0,11.9,.94,.1),"254.633 correct endpoint cannot validate a corner twelve meters behind Chris");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.LocalEntryPath(true,0,.4,.76,.96),"313.102 local aligned entry route remains reusable");
foreach(bool path in new[]{false,true})foreach(double gap in new[]{0,.35,.351,double.NaN})foreach(double corner in new[]{.4,1.9,11.9,double.NaN})foreach(double heading in new[]{-.5,.49,.5,.96,double.NaN})
 Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.LocalEntryPath(path,gap,corner,.9,heading)==(path&&!double.IsNaN(gap)&&gap>=0&&gap<=.35&&!double.IsNaN(corner)&&corner>=0&&corner<=1.9&&!double.IsNaN(heading)&&heading>=.5&&heading<=1.001),"current local native entry resource "+path+gap+corner+heading);
for(int mask=0;mask<16;mask++)foreach(double d in new[]{-.1,0,.9,1.25,1.26,3.5,3.501,double.NaN})
 Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.ManualEntry((mask&1)!=0,(mask&2)!=0,(mask&4)!=0,(mask&8)!=0,d)==((mask&1)!=0&&(mask&2)==0&&!double.IsNaN(d)&&d>=0&&d<=3.5&&((mask&4)==0||((mask&8)==0&&d<=1.25))),"same probe entry fallback without taking active native actor "+mask+d);
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.LocalJoin(true,1.853535,2.1,.078915),"324.191 Knight can join nearest current demonstration node757 through verified short path");
foreach(bool strict in new[]{false,true})foreach(double join in new[]{.85,.851,1.853535,3,3.001,double.NaN})foreach(double len in new[]{0,2.1,7,double.NaN})foreach(double height in new[]{0,.35,.351,double.NaN})
 Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.LocalJoin(strict,join,len,height)==(strict&&!double.IsNaN(join)&&join>.85&&join<=3&&!double.IsNaN(len)&&len>=0&&len<=Math.Max(1,join*1.8+.5)&&!double.IsNaN(height)&&Math.Abs(height)<=.35),"local join cannot become blind larger radius "+strict+join+len+height);
Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.Connector(true,true,false,false,true,3.22,1.853535,.5,0,true,false),"unverified 1.85m direct join still rejected by original connector");
foreach(bool chain in new[]{false,true})foreach(int gap in new[]{-1,0,8,9,100})foreach(double heading in new[]{-1,.29,.3,.99,double.NaN})
 Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.RelatedPrefix(chain,gap,heading)==(chain&&gap>=0&&gap<=8&&!double.IsNaN(heading)&&heading>=.3&&heading<=1.001),"old reverse demonstration cannot create current prefix authority "+chain+gap+heading);
foreach(bool obstacle in new[]{false,true})foreach(bool retry in new[]{false,true})foreach(double d in new[]{0,.65,.651,double.NaN})foreach(double side in new[]{0,.1,.11,double.NaN})foreach(double angle in new[]{0,8,8.01,73.18497,double.NaN})
 Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.ObstacleHold(obstacle,retry,d,side,.1,angle,8)==(obstacle&&!retry&&!double.IsNaN(d)&&d>=0&&d<=.65&&!double.IsNaN(side)&&side>=0&&side<=.1&&!double.IsNaN(angle)&&angle>8),"native obstacle turns at actual entry before translating "+obstacle+retry+d+side+angle);
foreach(double speed in new[]{0,.15,.151,2.91,double.NaN})foreach(double heading in new[]{-1,.989,.99,1,double.NaN})
 Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.RescueSettled(speed,heading)==(!double.IsNaN(speed)&&speed>=0&&speed<=.15&&!double.IsNaN(heading)&&heading>=.99&&heading<=1.001),"freeze rescue frontier after physical braking and heading "+speed+heading);
for(int mask=0;mask<16;mask++)Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.CoveredLowerDrop((mask&1)!=0,(mask&2)!=0,(mask&4)!=0,(mask&8)!=0,3,9,.98,.75,-.08,.35,.2,0,.01)==(mask==15),"same native receipt, resolved intervening steps, grounded same chain required "+mask);
foreach(double[] v in new[]{new[]{.75,-.08,.35,.2,0,.01},new[]{.75,-.07,.82,.15,0,.01},new[]{.75,.260665,1.72,.21,0,.01}})
 Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.CoveredLowerDrop(true,true,true,true,3,9,.98,v[0],v[1],v[2],v[3],v[4],v[5]),"382.326/406.001/411.856 actual predecessor landing already covers lower drop debt "+string.Join(",",v));
foreach(double[] v in new[]{new[]{.499,-.08,.35,.2,0,.01},new[]{.75,2.81,.35,.2,0,.01},new[]{.75,-.451,.35,.2,0,.01},new[]{.75,-.08,-.251,.2,0,.01},new[]{.75,-.08,2.251,.2,0,.01},new[]{.75,-.08,.35,.651,0,.01},new[]{.75,-.08,.35,.2,.351,.01},new[]{.75,-.08,.35,.2,0,6.001},new[]{double.NaN,-.08,.35,.2,0,.01}})
 Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.CoveredLowerDrop(true,true,true,true,3,9,.98,v[0],v[1],v[2],v[3],v[4],v[5]),"actual upper-platform landing / distant or stale body never retires lower native action "+string.Join(",",v));
{
 var mission=new BotTraversal.Core.FollowerTraversalLifecycle.MissionState();var upper=new BotTraversal.Core.FollowerTraversalLifecycle.StepState();
 var origin=new BotTraversal.Core.FollowerTraversalLifecycle.Identity("Chris",2,385,0,30,88,27,0,41);var actor=new BotTraversal.Core.FollowerTraversalLifecycle.ProbeState(origin,upper);
 Check(upper.Attach(actor),"upper actual native actor owns first drop");
 Check(actor.PublishCompletion(origin,upper,BotTraversal.Core.FollowerTraversalLifecycle.ActionOutcome.NativePhysical)==BotTraversal.Core.FollowerTraversalLifecycle.CommitResult.Applied,"upper actual actor has real Native outcome");
 Check(mission.Resolve(upper,origin,upper,origin,BotTraversal.Core.FollowerTraversalLifecycle.Resolution.Executed,true,true)==BotTraversal.Core.FollowerTraversalLifecycle.CommitResult.Applied,"upper real drop resolved");
 var overlap=new BotTraversal.Core.FollowerTraversalLifecycle.StepState();var overlapOrigin=new BotTraversal.Core.FollowerTraversalLifecycle.Identity("Chris",2,385,1,31,229,27,0,41);
 Check(mission.Resolve(overlap,overlapOrigin,overlap,overlapOrigin,BotTraversal.Core.FollowerTraversalLifecycle.Resolution.Subsumed,true,false)==BotTraversal.Core.FollowerTraversalLifecycle.CommitResult.Applied,"covered recorder WT retires without new WT outcome");
 var lower=new BotTraversal.Core.FollowerTraversalLifecycle.StepState();var lowerOrigin=new BotTraversal.Core.FollowerTraversalLifecycle.Identity("Chris",2,385,2,32,84,27,0,41);
 Check(mission.Resolve(lower,lowerOrigin,lower,lowerOrigin,BotTraversal.Core.FollowerTraversalLifecycle.Resolution.Subsumed,false,true)==BotTraversal.Core.FollowerTraversalLifecycle.CommitResult.Rejected,"lower native action cannot be skipped without qualified coverage");
 Check(mission.Resolve(lower,lowerOrigin,lower,lowerOrigin,BotTraversal.Core.FollowerTraversalLifecycle.Resolution.Subsumed,true,true)==BotTraversal.Core.FollowerTraversalLifecycle.CommitResult.Applied,"actual coverage resolves lower debt through original lifecycle seam");
 Check(mission.Cursor==3&&lower.Probe==null&&lower.Result==BotTraversal.Core.FollowerTraversalLifecycle.Resolution.Subsumed,"no second native actor or second Native success invented");
}
foreach(bool generic in new[]{false,true})foreach(int target in new[]{0,451,452,453,454,700})foreach(int floor in new[]{0,452,453,454})
 Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.TargetAfterFloor(generic,target,floor)==(!generic||target>Math.Max(0,floor)),"generic producer and low-step obey completed chronology "+generic+target+floor);
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.TargetAfterFloor(true,451,453),"238.366 Ghost old low-step target451 cannot reappear after completed floor453");
foreach(bool current in new[]{false,true})foreach(bool ground in new[]{false,true})foreach(double fwd in new[]{-2,-1.2,-.897324741,-.591780543,-.1,0,double.NaN})foreach(double side in new[]{0,.25,.51,double.NaN})
 Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.DropLead(current,ground,fwd,1.2,side,.5,.1)==(current&&ground&&!double.IsNaN(fwd)&&fwd>=-1.2&&fwd<-.1&&!double.IsNaN(side)&&side>=0&&side<=.5),"current supported drop approach does not orbit same longitudinal station "+current+ground+fwd+side);
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.ForwardLaneSample(true,-.897,-.897),"NavMesh cannot snap a forward drop lead back to same body station");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.ForwardLaneSample(true,-.897,-.55),"forward local lead preserved");

// RC135 completed Follow: a new external path back to the old exit after actual arrival is stale.
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.StaleCompletedTarget(true,3.4,.17,8.5,2,7.55,-.1,8.2),"126.9 Ada cached completed-exit target cannot replace near-player Follow");
foreach(bool exact in new[]{false,true})foreach(double age in new[]{0,3.4,6,6.001,double.NaN})foreach(double gap in new[]{.17,1,1.001})foreach(double playerGap in new[]{1,2.5,8.5})
 Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.StaleCompletedTarget(exact,age,gap,playerGap,2,7.55,-.1,8.2)==(exact&&!double.IsNaN(age)&&age>=0&&age<=6&&gap<=1&&playerGap>2.5),"recent exact old target, never legitimate player return "+exact+age+gap+playerGap);
foreach(double[] v in new[]{new[]{4.001,7.55,-.1,8.2},new[]{2,1.25,-.1,8.2},new[]{2,7.55,6.55,8.2},new[]{2,7.55,-.1,7.29},new[]{double.NaN,7.55,-.1,8.2}})
 Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.StaleCompletedTarget(true,3.4,.17,8.5,v[0],v[1],v[2],v[3]),"new phase, real player reversal or no forward departure remains allowed "+string.Join(",",v));

Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.Connector(true,true,false,false,true,1.94444287,.3,.5,.2,true,false,true),"348.628 Ada recorded near-entry lane is driven instead of legacy 50m leader abandonment");
Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.Connector(true,true,false,false,true,1.94444287,.3,.5,.2,true,false),"near-entry continuation is not a global connector admission relaxation");
Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.Connector(true,true,false,false,true,.65,.3,.5,.2,true,false,true),"native near-frontier selector retains final .65m window");

// RC136 raid 353.442: queue head 1.606m away, .3805m below WT entry must own Aligning, not wait for a partial NavMesh path.
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.HeadEntry(true,true,true,1.608,.3805),"Ada doubleboard head transfers to existing WT entry controller");
foreach(var v in new[]{(false,true,true,1.608,.3805),(true,false,true,1.608,.3805),(true,true,false,1.608,.3805),(true,true,true,2.251,.3805),(true,true,true,1.608,.651),(true,true,true,double.NaN,.3805)})
 Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.HeadEntry(v.Item1,v.Item2,v.Item3,v.Item4,v.Item5),"stale/non-head/airborne/far/other floor cannot allocate WT "+v);
foreach(double d in new[]{.65,.651,2.25,2.251})Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.HeadEntry(true,true,true,d,.38)==(d>.65&&d<=2.25),"existing final native window and commitment envelope "+d);
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.SemanticContinuation(true,4.04532433-.508864343),"359.847 local Vault on ground cannot chase upper WT semantic exit");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.SemanticContinuation(true,-2.81),"local rescue must not walk blindly into unresolved lower floor");
foreach(double h in new[]{-.65,0,.65})Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.SemanticContinuation(true,h),"same floor local rescue continuation remains available "+h);
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.SemanticContinuation(false,0),"airborne rescue has not acquired walking continuation");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.SemanticContinuation(true,double.NaN),"invalid semantic target cannot become forward input");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.CompletedFollowRange(13.1917906,13.1917906),"Knight258.947 completed current leader path is outside old7m guard");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.CompletedFollowRange(13.19,3),"short formation completion cannot claim distant future work");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.CompletedFollowRange(26,50),"completed follow is bounded even after distant release");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.CompletedFollowRange(double.NaN,13),"invalid follow range rejected");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.DestinationMatches(true,77),"261.659 old77m path is not healthy completed leader delivery");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.DestinationMatches(true,.35),"same accepted endpoint retains continuity");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.DestinationMatches(true,.351),"changed native endpoint cannot reuse prior receipt");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.DestinationMatches(false,0),"lost path cannot reuse prior receipt");
// The existing failure state machine must advance exactly once after real local rescue fails the semantic mission.
Check(P.AfterFailure(P.Stage.SelfRescue)==P.Stage.ReturnToEntry,"local Vault does not replay completed prefix after semantic failure");

Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.SemanticContinuation(true,3.334768,true),"431.829 successful Vault-to-board ascent retained only with actual supported ramp");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.SemanticContinuation(true,-2.81,true),"measured continuously supported downhill ramp is symmetric with uphill");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.SemanticContinuation(true,-2.81,false),"unmeasured lower floor still cannot authorize a drop");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.SemanticContinuation(false,3.334768,true),"ramp cannot substitute current body support");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.RampEnvelope(3.334768,6.775677),"Chris433.196 ascent fits finite ramp observation envelope");
foreach(var v in new[]{(3.3,8.001),(.65,6.7),(double.NaN,6.7),(3.3,double.NaN)})Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.RampEnvelope(v.Item1,v.Item2),"unknown or unbounded ramp cannot continue "+v);
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.RampSample(true,.30,.82,false),"continuous wooden incline can retain walking egress");
foreach(var v in new[]{(true,-1.8,.82,false),(false,0.0,.82,false),(true,.351,.82,false),(true,0.0,.649,false),(true,0.0,.82,true),(true,double.NaN,.82,false)})
 Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.RampSample(v.Item1,v.Item2,v.Item3,v.Item4),"under-board floor, missing support, steep or blocked segment cannot become a walking ramp "+v);

// RC137 actual dead spot is OUTSIDE the unchanged 2.25m Aligning admission.
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.HeadEntry(true,true,true,2.511,.2376),"Ghost351.405 does not fake admission by widening WT probe radius");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.GroundLead(true,true,true,false,2.511,.2376,true,false),"Ghost351.405 can walk a newly sensed one-meter segment to the unchanged entry envelope");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.GroundLead(true,true,true,false,1.3718,.307,true,false),"Ghost359.272 returned WT retries can approach without unavailable StrictPath");
foreach(var v in new[]{(false,true,true,false,true,false),(true,false,true,false,true,false),(true,true,false,false,true,false),(true,true,true,true,true,false),(true,true,true,false,false,false),(true,true,true,false,true,true)})
 Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.GroundLead(v.Item1,v.Item2,v.Item3,v.Item4,2.511,.238,v.Item5,v.Item6),"non-head, unsafe, airborne, actor-owned, unsupported or blocked entry segment denied "+v);
foreach(double d in new[]{.65,5.001,double.NaN})Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.GroundLead(true,true,true,false,d,.238,true,false),"finite grounded entry connector envelope "+d);
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.GroundLead(true,true,true,false,2.511,.651,true,false),"different floor never bypasses Native WT admission");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.LiveFollow(true,true,false,false,false,false,919,35,true),"Knight400 live resolved Follow still checks resources after old five-second diagnostic window");
for(int k=0;k<7;k++)Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.LiveFollow(k!=0,k!=1,k==2,k==3,k==4,k==5,919,35,k!=6),"command/airborne/actor/debt/new work/native way/other writer excludes live Follow guard "+k);
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.LiveFollow(true,true,false,false,false,false,0,35,true),"no completed navigation floor grants no live resolved Follow context");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.LiveFollow(true,true,false,false,false,false,919,0,true),"missing movement generation grants no context");
{
 double bx=349.060455,bz=-129.130219,cx=348.389832,cz=-125.416687,wx=351.20816,wz=-125.218338;
 double vx=bx-348.7344,vz=bz-(-127.089737),dx=wx-bx,dz=wz-bz;
 double heading=(vx*dx+vz*dz)/Math.Sqrt((vx*vx+vz*vz)*(dx*dx+dz*dz));
 double body=Math.Sqrt((bx-cx)*(bx-cx)+(bz-cz)*(bz-cz));
 double gap=Math.Sqrt((wx-cx)*(wx-cx)+(wz-cz)*(wz-cz));
 Check(heading<-.7&&BotTraversal.Core.FollowerCapabilityPassagePolicy.StaleNativeCorner(true,body,gap,heading),"Knight400 measured southward motion contradicts actual northeast path before 69m runaway");
 Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.StaleNativeCorner(true,body,gap,.8),"legitimate forward motion with origin/lookahead corner must retain path");
}
foreach(var v in new[]{(false,4.1,2.8,-.8),(true,.5,2.8,-.8),(true,4.1,.35,-.8),(true,4.1,2.8,-.25),(true,4.1,2.8,double.NaN)})
 Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.StaleNativeCorner(v.Item1,v.Item2,v.Item3,v.Item4),"valid path, near anchor, remaining corner or unproved reverse motion retained "+v);
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.JumpDispatch(true,true,false,false,.835),"Knight211.013 current Jump is selectable at predecessor release before .364s late scan");
foreach(var v in new[]{(false,true,false,false),(true,false,false,false),(true,true,true,false),(true,true,false,true)})Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.JumpDispatch(v.Item1,v.Item2,v.Item3,v.Item4,.835),"immediate Jump remains exact current debt, no actor or failure strategy "+v);
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.JumpDispatch(true,true,false,false,2.501),"early Jump selector does not allocate distant demonstrations");
Check(BotTraversal.Core.FollowerCapabilityRetryPolicy.Apply(211.4f,BotTraversal.Core.FollowerCapabilityRetryPolicy.Change.Reset,0)==0,"applied WT identity advance discards predecessor attempt clock");
Check(BotTraversal.Core.FollowerCapabilityRetryPolicy.Apply(211.4f,BotTraversal.Core.FollowerCapabilityRetryPolicy.Change.ProposalDeferred,0)==211.4f,"polling current Jump cannot erase its own retry clock");

foreach(double d in new[]{0,.732,2.5})Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.ReacquireFollowTarget(d),"nearby stationary player / formation retains native target selection "+d);
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.ReacquireFollowTarget(4.15),"departed follower can reacquire actual leader after stale path retirement");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.ReacquireFollowTarget(double.NaN),"invalid player range does not override Follow target");

// 348.848 Knight entered a reverse WT approach while still facing its previous route.
foreach(double h in new[]{-1,-.6,0,.94,double.NaN})Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.EntryTurnHold(h),"sensed entry direction is acquired before publishing translation "+h);
foreach(double h in new[]{.95,1})Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.EntryTurnHold(h),"already aligned entry retains continuous forward motion "+h);
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.ObstacleCenterTurn(true,35.37),"Ghost494 lateral correction turns toward actual entry point before translating");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.ObstacleCenterTurn(false,35.37),"centered entry keeps existing axis hold admission");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.ObstacleCenterTurn(true,20),"aligned lateral correction does not introduce a stationary extra stage");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.ObstacleEntrySpeed(.0314,.55)==.35,"Ghost494.580 final centimeters use bounded speed before native consumption");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.ObstacleEntrySpeed(.39,0)==0,"entry heading hold cannot acquire a positive speed floor");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.ObstacleEntrySpeed(.4,.82)==.82,"outside final precision envelope preserves normal approach speed");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.ReacquireFollowTarget(.732),"nearby separation is excluded from both repeated path retirement and target override");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.ReacquireFollowTarget(4.15),"true departure remains guarded after player separation range");

Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.ObstacleNear(.399),"last obstacle centimeters disable sprint/preserveSprint before common route governor can override precision speed");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.ObstacleNear(.4),"full approach Sprint remains available outside final precision boundary");

Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.ForeignCommitment(true,true,false,false),"Knight539 old117 commitment cannot outrank current recorded corridor252");
foreach(var x in new[]{(false,true,false,false),(true,false,false,false),(true,true,true,false),(true,true,false,true)})
 Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.ForeignCommitment(x.Item1,x.Item2,x.Item3,x.Item4),"foreign guard preserves absent/resolved/current debt and owned strategy "+x);
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.RecordedFallback(47),"recorded Jump117 timeout belongs to shared ladder not direct legacy teleport");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.RecordedFallback(0),"unrecorded navigation recovery is not a fabricated demonstration");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.RecordedFallback(-1),"missing recorded origin cannot create debt");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.ClimbSuccessor(true,true,true,true,true,true,.146182314,.4),"Ghost401.645 actual native upper landing reaches current Drop successor despite lower NavMesh sample");
for(int k=0;k<6;k++)Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.ClimbSuccessor(k!=0,k!=1,k!=2,k!=3,k!=4,k!=5,.146,.4),"chain closure requires exact live actor, consumed executor, state, passage, support and current Drop "+k);
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.ClimbSuccessor(true,true,true,true,true,true,.351,.4),"local landing still far from authored exit cannot close Climb");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.ClimbSuccessor(true,true,true,true,true,true,.146,2.501),"distant future Drop cannot bypass local continuation");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.ClimbSuccessor(true,true,true,true,true,true,double.NaN,.4),"invalid landing does not supply completion proof");

Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.CenterSideOnly(true,.49),"Chris394 current obstacle entry corrects .46m cross-track without old staging-point retreat");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.CenterSideOnly(false,.49),"already centered obstacle keeps its own forward axis");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.CenterSideOnly(true,.651),"distant staging stays with existing approach controller");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.CenterSideOnly(true,double.NaN),"unknown position cannot select local side correction");

Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.RecordedRecoveryScope(true,false),"unbound recorded timeout first obtains shared route admission");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.RecordedRecoveryScope(false,true),"unrecorded foreign navigation cannot teleport over a live recorded debt");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.RecordedRecoveryScope(false,false),"ordinary unrecorded recovery with no recorded work remains available");
// RC140 straight threshold support is a bounded WT motion, never an actuator substitute.
for(int mask=0;mask<32;mask++){
 bool current=(mask&1)!=0,safe=(mask&2)!=0,ground=(mask&4)!=0,active=(mask&8)!=0,native=(mask&16)!=0;
 Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.WalkSeam(current,safe,ground,active,native,.4,.1)==(current&&safe&&!native&&(active||ground)),"RC140 seam ownership "+mask);
}
foreach(double v in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity,-1,.201})
 Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.WalkSeam(true,true,true,false,false,.4,v),"moving or invalid body cannot start threshold lift "+v);
foreach(double h in new[]{double.NaN,double.PositiveInfinity,-.151,.201})
 Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.WalkSeamSupport(h),"unsupported threshold does not lift "+h);
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.WalkSeamSupport(.02669549),"391.922 real door sill support accepted");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.WalkSeamSupport(.15),"supported small positive threshold accepted");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.WalkSeam(true,true,true,false,false,.1,0),"no instant threshold claim before measured stall");
Check(!P.CanAllocateStrategy(true,false,false,false),"354.310 retirement callback sees next stage without selected pending attempt");
Check(P.OwnsStrategy(P.AfterFailure(P.Stage.Native),true),"Native failure publishes Alternate ownership before retirement callback");
// RC141: recorded floor949 advanced at door166 while the body was still at its entry.
for(int mask=0;mask<8;mask++)Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.CursorFloor((mask&1)!=0,(mask&2)!=0,(mask&4)!=0)==((mask&1)==0||(mask&2)==0||(mask&4)!=0),"chronology is not physical passage "+mask);
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.CorridorExit(true,.5357,.0693,.0693,.65,.151),"Chris469.217 before door exit cannot consume PhysicalCorridor by floor");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.CorridorExit(true,.5357,.66,4.60,.65,-.02),"Chris474 beside the far wall is not a finite door passage witness");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.CorridorExit(true,.5357,.80,.27,.65,-.15),"supported body beyond the finite doorway can resolve corridor debt");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.CorridorExit(false,.5357,.80,.27,.65,-.15),"airborne height alone cannot complete corridor");
foreach(double v in new[]{double.NaN,double.PositiveInfinity,-1})Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.CorridorExit(true,.5357,v,.27,.65,-.15),"invalid corridor projection "+v);
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.CorridorExtension(949,953,0,.5357,2.31,.06,-.264),"door166 same recorded episode extension outranks earliest mission snapshot");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.CorridorExtension(953,949,0,.5357,2.31,.06,-.264),"corridor endpoint cannot rewind recording");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.CorridorExtension(949,953,.021,.5357,2.31,.06,-.264),"moved entrance is not the same stable recorded extension");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.CorridorExtension(949,953,0,.5357,.4,.06,-.264),"recorded tail cannot shrink unfinished corridor");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.CorridorExtension(949,953,0,.5357,2.31,.351,-.264),"different lateral route cannot replace current corridor");
for(int mask=0;mask<8;mask++)Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.NativeFollowReceipt(23,23,(mask&1)!=0,(mask&2)!=0,(mask&4)!=0,.1)==(mask==7),"native Follow receipt needs live clean accepted actual path "+mask);
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.NativeFollowReceipt(23,24,true,true,true,.1),"new owner cannot acknowledge an old path call");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.NativeFollowReceipt(0,0,true,true,true,.1),"missing path-call origin cannot acknowledge handoff");
foreach(double v in new[]{double.NaN,double.PositiveInfinity,-1,.351})Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.NativeFollowReceipt(23,23,true,true,true,v),"native path actual endpoint must match request "+v);
for(int mask=0;mask<64;mask++)Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.NativeFollowIdle((mask&1)!=0,(mask&2)!=0,(mask&4)!=0,(mask&8)!=0,(mask&16)!=0,(mask&32)!=0)==(mask==35),"native Follow idle input boundary "+mask);
for(int mask=0;mask<8;mask++)Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.PassageLease((mask&1)!=0,(mask&2)!=0,(mask&4)!=0,953,952,12,10)==(mask==3),"live generic passage excludes ordinary Follow "+mask);
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.PassageLease(true,true,false,953,953,12,10),"completed floor cannot keep a stale passage producer alive");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.PassageLease(true,true,false,953,952,9,10),"expired generic lease returns ordinary Follow");
// RC142 frozen raid: shared entry at191.5679 was acquired, but no Jump entry capture existed.
for(int mask=0;mask<8;mask++)Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.CaptureJumpEntry((mask&1)!=0,(mask&2)!=0,(mask&4)!=0)==(mask==7),"shared physical entry issues Jump witness only to exact current native Jump "+mask);
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.UnownedSequenceProbe(true),"retired sequence cannot allocate reserved dispatch without a live selected transaction");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.UnownedSequenceProbe(false),"ordinary native allocation retains existing admission");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.RescueObstacle(true,true,0,1,.35),"current wall admits ranked generalized rescue");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.RescueObstacle(true,false,0,1,.35),"empty board cannot trigger synthetic Vault");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.RescueObstacle(false,true,0,1,.35),"airborne on slope cannot trigger injected rescue");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.RescueObstacle(true,true,.9,.4,.35),"walkable slope is not a clearance obstacle");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.RescueObstacle(true,true,0,.2,.35),"side wall is not on current rescue axis");
foreach(double v in new[]{double.NaN,double.PositiveInfinity,-.01,.86})Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.RescueObstacle(true,true,0,1,v),"invalid or out-of-reach obstacle "+v);
foreach(double v in new[]{double.NaN,double.PositiveInfinity,-1.01,.65})Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.RescueObstacle(true,true,v,1,.35),"invalid obstacle normal "+v);
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.EntryStillAhead(.806,.069,-.04),"Ada264.407 already on board must not turn to entry");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.EntryStillAhead(1.199,.1,-.04),"Ada264.554 cannot reverse after leaving entrance radius");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.EntryStillAhead(-2,.1,0),"near-side queue head can approach entry");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.EntryStillAhead(1,.8,0),"side-of-board body cannot invent an entered corridor");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.EntryStillAhead(1,.1,-1),"body underneath board cannot invent an entered corridor");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.QueueDistance(true,3,1)<BotTraversal.Core.FollowerCapabilityPassagePolicy.QueueDistance(true,1,3),"moving away from entry toward exit is positive queue progress");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.QueueDistance(false,1,3)==1,"obstacle ingress still measures distance to entry");
Check(!BotTraversal.Core.FollowerPortalAdmission.HeadStalled(20,1,19,false,false,true),"long moving WT does not exhaust eight-second entry clock");
Check(BotTraversal.Core.FollowerPortalAdmission.HeadStalled(20,1,15,false,false,true),"real WT no-progress still advances failure ladder after four seconds");
Check(BotTraversal.Core.FollowerPortalAdmission.HeadStalled(20,1,19,false,false),"noncorridor retains original bounded entry window");
Check(!BotTraversal.Core.FollowerPortalAdmission.HeadStalled(20,1,15,true,false,true),"existing native probe owns its own deadline");
Check(!BotTraversal.Core.FollowerPortalAdmission.HeadStalled(20,1,15,false,true,true),"selected failure transaction owns its own deadline");
// RC143: Chris729..736 was pathless, Follow-owned and close while generic motion kept pushing.
for(int mask=0;mask<128;mask++)Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.GenericFollowStart((mask&1)!=0,(mask&2)!=0,(mask&4)!=0,(mask&8)!=0,(mask&16)!=0,(mask&32)!=0,(mask&64)!=0,7.4,7.41,8,true)==(mask==103),"generic-to-native call preflight ownership/support/path "+mask);
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.GenericFollowStart(true,true,true,false,false,true,true,7.4,7.41,8,false),"normal native formation can stop before player contact, no recent motion needed at rest");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.GenericFollowStart(true,true,true,false,false,true,true,11,11,8,false),"CF40 path flags without recent body progress cannot steal far recovery");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.GenericFollowStart(true,true,true,false,false,true,true,11,11,8,true),"moving supported bridge can offer actual native Follow a consumer call");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.GenericFollowStart(true,true,true,false,false,true,true,7,18,8,true),"legal long detour cannot preempt a local active corridor");
foreach(double v in new[]{double.NaN,double.PositiveInfinity,-1})Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.GenericFollowStart(true,true,true,false,false,true,true,v,7,8,true),"invalid physical distance cannot suspend generic writer "+v);
for(int mask=0;mask<16;mask++)foreach(double distance in new[]{.732,11.0}){
 bool expected=(mask&3)==3&&((mask&4)!=0||(mask&8)!=0&&distance<=8);
 Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.GenericFollowCommit((mask&1)!=0,(mask&2)!=0,(mask&4)!=0,(mask&8)!=0,distance,8)==expected,"actual native path or in-range stop acknowledges same synchronous consumer "+mask+":"+distance);
}
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.GenericFollowCommit(false,true,true,true,.732,8),"exception or skipped original call rolls back, even if flags look healthy");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.GenericFollowCommit(true,false,true,true,.732,8),"new mission/lease/epoch must not be overwritten by old handoff");
for(int mask=0;mask<32;mask++)Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.NormalFormation((mask&1)!=0,(mask&2)!=0,(mask&4)!=0,(mask&8)!=0,(mask&16)!=0)==(mask==1),"Follow domain alone is not formation proof "+mask);
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.ObstacleNeedsCenter(.33,.75),"Ghost611.671 learned actuator lane already accepts side.33; do not turn90 to a narrower lane");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.ObstacleNeedsCenter(.33,.22),"obstacle without a wider proven lane still requires correction");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.ObstacleNeedsCenter(.17,.22),"Ada619.741 inside lane should align axis rather than chase side target");
foreach(double v in new[]{double.NaN,double.PositiveInfinity,-1})Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.ObstacleNeedsCenter(v,.22),"invalid side cannot invent centered entry "+v);
for(int mask=0;mask<8;mask++)Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.LeftAdmittedIngress((mask&1)!=0,(mask&2)!=0,(mask&4)!=0,11.99)==(mask==1),"Knight630 admitted queue head outside seven metres must fail current ingress, not disappear "+mask);
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.LeftAdmittedIngress(true,false,false,7),"normal queue window boundary remains legal");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.LeftAdmittedIngress(false,false,false,12),"distant unadmitted follower does not fabricate a Native failure");
// RC144 Ghost194.019: live window/canJump/entry/heading all passed; historical Sprint readiness must not veto final consumer.
Check(BotTraversal.Core.FollowerCapabilityActuationPolicy.LiveJumpAttempt(true,true,true,true,true,true,true,true,true,true,true),"Ghost194.019 Run speed2.770 exceeds live1.1 floor and valid physical window may attempt native Jump");
for(int guard=0;guard<11;guard++){
 var flags=Enumerable.Repeat(true,11).ToArray();flags[guard]=false;
 Check(!BotTraversal.Core.FollowerCapabilityActuationPolicy.LiveJumpAttempt(flags[0],flags[1],flags[2],flags[3],flags[4],flags[5],flags[6],flags[7],flags[8],flags[9],flags[10]),"same live final-consumer guard remains mandatory "+guard);
}
foreach(var fixture in new[]{new[]{6.99899,.26558,-.11168},new[]{7.01195,.28086,-.11207},new[]{6.99841,.26508,-.10629}}){
 bool flight=BotTraversal.Core.FollowerCapabilityPassagePolicy.PassageInFlight(true,true,fixture[0],9.86748,fixture[1],fixture[2]);
 Check(flight,"recorded corridor253 body lies inside finite current passage "+fixture[0]);
 Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.LeftAdmittedIngress(true,false,false,7.0176,flight),"Ada/Ghost/Chris advancing beyond7m cannot become failed ingress");
}
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.LeftAdmittedIngress(true,false,false,11.99,false),"Knight old Vault retreat still fails an actually lost ingress");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.PassageInFlight(true,true,-5.5,9.86748,.1,0),"Knight623 moving backward before corridor is not a protected passage");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.PassageInFlight(false,true,7,9.86748,.1,0),"an actuator outside entry is not exempt as ordinary movement");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.PassageInFlight(true,true,7,9.86748,.8,0),"body beside current finite passage is not an in-flight ticket");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.PhysicalWalkEntry(false,true,.747,.728),"Chris553 Nav entry.085m away still requires actual raw corridor entry.728m away");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.PhysicalWalkEntry(true,true,.747,.728),"already acquired recorded lane retains existing progress");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.PhysicalWalkEntry(false,false,.747,.728),"missing physical target cannot replace navigation");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.PhysicalWalkEntry(false,true,.01,.728),"coincident Nav and physical entry need no alternate approach");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.PhysicalWalkEntry(false,true,.747,3.51),"distant recording cannot acquire local WT physical driver");
double aim=BotTraversal.Core.FollowerCapabilityPassagePolicy.LocalAimFraction(1.43923509);
Check(aim>0&&aim<1&&Math.Abs(aim*1.43923509-.60)<.00001,"retry1.439m path corner uses existing finite local terrain aim");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.GroundAim(true,true,.577*aim,true,false),"Ghost196 supported ramp is not a.577m single vertical step");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.GroundAim(true,true,.577*aim,false,false),"missing ground support still refuses physical approach");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.GroundAim(true,true,.577*aim,true,true),"current wall still refuses physical approach");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.GroundAim(false,true,.1,true,false),"airborne body does not acquire walking approach");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.GroundAim(true,true,.451,true,false),"bounded local aim cannot authorize abrupt vertical step");
foreach(double v in new[]{double.NaN,double.PositiveInfinity,-1,0})Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.LocalAimFraction(v)==0,"invalid aim length cannot produce a move "+v);
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.LocalAimFraction(.30)==1,"near aim retains exact target");
// RC145: all 17 refused attempts were accepted atomic admissions, not shared acquisition.
for(int mask=0;mask<64;mask++)Check(BotTraversal.Core.FollowerCapabilityActuationPolicy.AtomicJumpReceipt(
    (mask&1)!=0,(mask&2)!=0,(mask&4)!=0,(mask&8)!=0,(mask&16)!=0,(mask&32)!=0)==(mask==63),"atomic receipt identity "+mask);
for(int mask=0;mask<16;mask++)Check(BotTraversal.Core.FollowerCapabilityActuationPolicy.JumpTriggerWindow(
    (mask&1)!=0,(mask&2)!=0,(mask&4)!=0,(mask&8)!=0)==((mask&3)!=0||(mask&12)==12),"one-frame forecast restricted to atomic caller "+mask);
foreach(bool forecast in new[]{false,true})foreach(string sample in new[]{"Ada238.879","Chris243.947","Knight687.533","Ghost704.696"}){
 bool atomic=BotTraversal.Core.FollowerCapabilityActuationPolicy.AtomicJumpReceipt(true,true,true,true,true,true);
 Check(BotTraversal.Core.FollowerCapabilityActuationPolicy.JumpTriggerWindow(!forecast,false,atomic,forecast),sample+" current admitted horizon survives final consumer");
 Check(BotTraversal.Core.FollowerCapabilityActuationPolicy.LiveJumpAttempt(true,true,true,true,true,true,true,true,true,true,true),sample+" published shared entry witness enables live final gate");
 Check(!BotTraversal.Core.FollowerCapabilityActuationPolicy.LiveJumpAttempt(true,false,false,true,true,true,true,true,true,true,true),sample+" cannot bypass missing entry acquisition");
}
foreach(float side in new[]{.13658f,.53781f,.30524f})Check(BotTraversal.Core.FollowerCapabilityApproachPolicy.MissedJumpWindow(true,.464f,side,.06f),"stationary beyond-plane queue head fails bounded current attempt "+side);
foreach(float f in new[]{float.NaN,float.PositiveInfinity,-.5f,.16f,.751f})Check(!BotTraversal.Core.FollowerCapabilityApproachPolicy.MissedJumpWindow(true,f,.1f,.06f),"not a proven local missed plane "+f);
Check(!BotTraversal.Core.FollowerCapabilityApproachPolicy.MissedJumpWindow(false,.464f,.1f,.06f),"foreign historical step cannot trigger missed-window failure");
Check(!BotTraversal.Core.FollowerCapabilityApproachPolicy.MissedJumpWindow(true,.464f,.721f,.06f),"distant lateral observer does not fail local attempt");
foreach(float duration in new[]{float.NaN,.039f,.121f})Check(!BotTraversal.Core.FollowerCapabilityApproachPolicy.MissedJumpWindow(true,.464f,.1f,duration),"stale or insufficient body observation "+duration);
foreach(var fixture in new[]{new[]{.8f,1.832f,1.5f},new[]{.8f,3.354f,3f},new[]{.72f,1.2f,.6f}})Check(BotTraversal.Core.FollowerCapabilityRouteRetryPolicy.ReturnDiverging(true,fixture[0],fixture[1],fixture[2]),"observed return path moves away despite native resource "+fixture[1]);
Check(!BotTraversal.Core.FollowerCapabilityRouteRetryPolicy.ReturnDiverging(false,.8f,2f,1f),"foreign replaced native path cannot produce divergence receipt");
Check(!BotTraversal.Core.FollowerCapabilityRouteRetryPolicy.ReturnDiverging(true,.8f,1f,.2f),"small terrain/path distance jitter preserves native return");
Check(!BotTraversal.Core.FollowerCapabilityRouteRetryPolicy.ReturnDiverging(true,.8f,2f,.09f),"path metadata change alone is not measured body divergence");
Check(!BotTraversal.Core.FollowerCapabilityRouteRetryPolicy.ReturnDiverging(true,float.MaxValue,2f,1f),"initial native allocation cannot count as lost distance");
Check(!BotTraversal.Core.FollowerCapabilityRouteRetryPolicy.ReturnDiverging(true,float.NaN,2f,1f),"invalid best distance cannot order path recovery");
// RC146 recorded WT entry, exit, inherited watchdog and walking-only recovery.
foreach(var sample in new[]{new[]{.9574,.0384},new[]{1.5522,.0864}}){
 Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.ManualEntry(true,false,false,false,sample[0]),"roughWT recorded entry owns local movement "+sample[0]);
 Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.EntryHeight(true,sample[1]),"roughWT has existing grounded entry height authority "+sample[1]);
}
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.WalkExitSample(1.014,.0117,.22,.35),"Knight/Ghost rawExitNav shifted to adjacent lane cannot replace authored WT exit");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.WalkExitSample(.06,.03,.22,.35),"local WT NavMesh sample remains usable");
foreach(double gap in new[]{double.NaN,double.PositiveInfinity,-1,.221})Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.WalkExitSample(gap,0,.22,.35),"invalid/displaced WT sample "+gap);
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.WalkExitSample(.01,.351,.22,.35),"NavMesh on another floor cannot own WT exit");
Check(Math.Abs(BotTraversal.Core.FollowerCapabilityPassagePolicy.AttemptStallAge(true,576.2479,572.8,576.2321)-.0158)<.0001,"Chris newly issued Vault cannot inherit WT stall at16ms");
Check(Math.Abs(BotTraversal.Core.FollowerCapabilityPassagePolicy.AttemptStallAge(true,569.6455,565,566.205)-3.4405)<.0001,"genuine current WT stall retains its own age");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.AttemptStallAge(false,10,3,9)==7,"untyped Follow watchdog retains original observation age");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.AttemptStallAge(true,10,9,3)==1,"real progress after current attempt resets observation age");
foreach(double now in new[]{double.NaN,double.PositiveInfinity})Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.AttemptStallAge(true,now,3,4)==0,"invalid clock cannot kill an actuator");
for(int mask=0;mask<4;mask++)Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.TerminalMayYield((mask&1)!=0,(mask&2)!=0)==(mask==1),"terminal teardown versus active probe "+mask);
foreach(var sample in new[]{new[]{.147,.212,.999},new[]{.43,.78,.997}})Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.SameWalkingPortal(sample[0],sample[1],sample[2]),"rough180/239 share native walking contract "+sample[0]);
foreach(var kind in new[]{"WalkThrough","Vault","Climb","Jump","DropDown"}){
 Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.WalkingRecoveryKind(true,kind)==(kind=="WalkThrough"),"proven walking rescue never allocates "+kind);
 Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.WalkingRecoveryKind(false,kind),"other semantic route retains cross-kind strategy "+kind);
}
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.SameWalkingPortal(.651,.1,.99),"a distant WT proof cannot lock another portal");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.SameWalkingPortal(.1,1.501,.99),"different exit is not same walking contract");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.SameWalkingPortal(.1,.1,.93),"different direction is not same walking contract");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.WalkingRescue(-.56,2.60,1.45,.086),"Chris before unfinished entry uses original WT entrance");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.WalkingRescue(1,2.60,.10,.086),"inside WT rescue resumes from current body toward same exit");
foreach(double forward in new[]{double.NaN,double.PositiveInfinity,-3.501,2.401,3})Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.WalkingRescue(forward,2.60,.10,.086),"no expired/distant/backward walking rescue "+forward);
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.WalkingRescue(1,2.6,1.501,.1),"unrelated lateral body cannot borrow WT proof");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.WalkingRescue(1,2.6,.1,.651),"another floor cannot borrow WT recovery");
// RC147 production policy: Run only for nearby entry correction, Sprint elsewhere.
for(int mask=0;mask<64;mask++) {
 bool entry=(mask&1)!=0,local=(mask&2)!=0,side=(mask&4)!=0,angle=(mask&8)!=0,demo=(mask&16)!=0,current=(mask&32)!=0;
 bool correct=entry&&local&&(side||angle);
 Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.EntryCorrection(entry,local,side?.4:.1,.22,angle?.7:1,.94)==correct,"entry correction scope "+mask);
 Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.ExecutionSprint(demo,current,true,correct,.1,.2,.9,.9,4)==(!correct&&(demo||current)),"execution gait "+mask);
}
foreach(double f in new[]{-8,-1,0,.3,3})Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.ExecutionSprint(true,false,true,false,f,.1,1,1,3),"aligned execution has no blanket five metre braking ban "+f);
foreach(double invalid in new[]{double.NaN,double.PositiveInfinity,-1,9})Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.ExecutionSprint(true,true,true,false,0,.1,1,1,invalid),"invalid gait speed "+invalid);
Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.ExecutionSprint(true,true,false,false,0,.1,1,1,3),"EFT stamina/pose cannot be bypassed");
Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.ChordDeviation(0,0,1,1,1,0)>.7,"diagonal across right angle cuts route");
Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.ChordInside(.7,.22),"sharp chord rejected");
Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.ChordInside(.10,.22),"within half lane width permitted");
Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.ChordInside(.111,.22),"outside half lane width rejected");
Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.ChordDeviation(0,0,1,0,.5,0)==0,"straight route retains long lookahead");
Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.ChordDeviation(0,0,0,0,.3,.4)==.5,"zero chord returns body distance");
for(int mask=0;mask<8;mask++)Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.WallBlocks((mask&1)!=0,(mask&2)!=0?0:1,(mask&4)!=0?1:0,.4)==(mask==7),"wall versus floor tangent and absent hit "+mask);
foreach(double invalid in new[]{double.NaN,double.PositiveInfinity,-1})Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.WallBlocks(true,0,1,invalid),"invalid cast distance "+invalid);
// RC148 recenter keeps forward pursuit; exact queue claim protects the released head.
for(int mask=0;mask<32;mask++)Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.OwnedIngress((mask&1)!=0,(mask&2)!=0,(mask&4)!=0,(mask&8)!=0,(mask&16)!=0)==(mask==31),"admitted projection authority "+mask);
for(int mask=0;mask<8;mask++)Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.ExecutionGaitPhase((mask&1)!=0,(mask&2)!=0,(mask&4)!=0)==(mask==0),"landing consumer owns its gait "+mask);
foreach(double initial in new[]{0,.1,.25,.55})foreach(double fraction in new[]{0,.25,.5,.75,1}) {
 double limit=.11+initial*(1-fraction);
 Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.RejoinChordInside(limit,.22,initial,fraction),"decaying forward join boundary "+initial+":"+fraction);
 Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.RejoinChordInside(limit+.001,.22,initial,fraction),"new corner deviation cannot exceed join envelope "+initial+":"+fraction);
}
Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.ChordInside(.25,.22)&&BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.RejoinChordInside(.25,.22,.55,0),"recorded off-lane body can pursue forward instead of perpendicular Sprint reversal");
Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.RejoinChordInside(.25,.22,.55,1),"terminal join never inherits initial lane allowance");
foreach(double bad in new[]{double.NaN,double.PositiveInfinity,-1})Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.RejoinChordInside(bad,.22,.2,.5),"invalid rejoin deviation "+bad);
// RC149 measured approach regression and local stationary DropDown exit correction.
for(int mask=0;mask<8;mask++)Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.QueueMovingAway((mask&1)!=0,(mask&2)!=0,(mask&4)!=0,2,3,1,-1)==(mask==0),"queue never interrupts actor strategy or live passage "+mask);
Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.QueueMovingAway(false,false,false,2,2.75,1,-1),"ordinary local distance noise cannot fail queue head");
Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.QueueMovingAway(false,false,false,2,3,0,-1),"stopped queue is original no-progress watchdog responsibility");
Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.QueueMovingAway(false,false,false,2,3,1,1),"actual entryward velocity cannot count as moving away");
for(int mask=0;mask<128;mask++)Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.VerifyExitCorrection((mask&1)!=0,(mask&2)!=0,(mask&4)!=0,(mask&8)!=0,(mask&16)!=0,(mask&32)!=0,(mask&64)!=0,.659,.657,.02,1.65)==(mask==57),"Ada dropped-back grounded exit correction identity "+mask);
foreach(double gap in new[]{double.NaN,double.PositiveInfinity,-1,0,.851})Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.VerifyExitCorrection(true,false,false,true,true,true,false,gap,.657,.02,1.65),"local Verify cannot chase distant or invalid exit "+gap);
Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.VerifyExitCorrection(true,false,false,true,true,true,false,.659,-.657,.02,1.65),"Verify never publishes a backward target");
Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.VerifyExitCorrection(true,false,false,true,true,true,false,.659,.657,.351,1.65),"Verify cannot jump to another floor");
Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.VerifyExitCorrection(true,false,false,true,true,true,false,.659,.657,.02,.1),"near-entry body is not a lower landing correction");
// RC150 actual WT229->DD85 descent was incorrectly classified as leaving the entrance.
foreach(string kind in new[]{"Jump","DropDown","Climb","Vault","WalkThrough","PhysicalCorridor",""})Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.ImmediateDispatchKind(kind)==(kind=="Jump"||kind=="DropDown"||kind=="WalkThrough"||kind=="Vault"||kind=="Climb"),"exact immediate dispatcher kind "+kind);
foreach(var sample in new[]{new[]{.790,3.054,.030},new[]{.831,3.049,.035},new[]{.794,3.085,.040}}){
 bool inFlight=BotTraversal.Core.FollowerCapabilityPassagePolicy.DropInFlight(true,true,sample[0],2.828,sample[2],sample[1],3.530,.828);
 Check(inFlight,"recorded descending body stays in finite DD85 volume "+sample[1]);
 Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.QueueMovingAway(false,false,inFlight,.028,sample[0],1.79,-1),"real descent cannot become moving-away failure "+sample[1]);
}
foreach(double y in new[]{4.0,0.0,double.NaN})Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.DropInFlight(true,true,.79,2.828,.03,y,3.53,.828),"different vertical layer cannot borrow descent "+y);
foreach(double side in new[]{.451,1,double.NaN})Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.DropInFlight(true,true,.79,2.828,side,3.054,3.53,.828),"off-lane body cannot borrow descent "+side);
foreach(double forward in new[]{-.1,3.029,double.NaN})Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.DropInFlight(true,true,forward,2.828,.03,3.054,3.53,.828),"finite entry/exit planes retained "+forward);
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.DropInFlight(true,false,.79,2.828,.03,3.054,3.53,.828),"foreign queue claim cannot borrow flight volume");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.DropInFlight(false,true,.79,2.828,.03,3.054,3.53,.828),"Climb or other action cannot borrow DropDown phase");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.QueueDistance(true,.79,3.02)==3.02,"DropDown watchdog tracks remaining landing distance, not distance behind entry");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.ClimbUpperPending(true,true,1.257,.213),"Knight actual native ascent remains upper-plane pending");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.ClimbUpperPending(true,true,1.257,-.049),"Ada upper-plane completion is offered to successor, not prolonged native landing");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.ClimbSuccessor(true,true,true,true,true,true,.075,.603),"Ada completed native Climb can hand off even if next DropDown arrived after mission snapshot");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.ClimbSuccessor(true,true,false,true,true,true,.075,.603),"Chris unobserved native state cannot become completion");
foreach(double pending in new[]{.05,0,-.2,double.NaN})Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.ClimbUpperPending(true,true,1.257,pending),"actual upper plane ends pending lease "+pending);
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.ClimbUpperPending(false,true,1.257,.213),"Vault does not inherit Climb vertical debt");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.ClimbUpperPending(true,false,1.257,.213),"superseded writer cannot preserve native landing");
// RC151 recorded WT185: 0.966m upstream / 0.775m above body is a supported slope, not a flat entry.
for(int mask=0;mask<8;mask++)Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.SupportedEntry((mask&1)!=0,(mask&2)!=0,(mask&4)!=0)==((mask&1)!=0&&(mask&6)!=0),"ground and measured ramp required, no airborne entry "+mask);
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.EntryHeight(true,.775),"recorded WT185 slope exceeds original flat-entry envelope");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.RampEnvelope(.775,.966),"recorded WT185 slope can request actual floor and clearance samples");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.RampSample(true,.05,.78,false),"continuous walkable slope sample is eligible");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.RampSample(false,0,.78,false),"missing floor never authorizes uphill motion");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.RampSample(true,0,.78,true),"solid slope obstruction remains veto");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.RampSample(true,0,.4,false),"steep wall is not walkable slope");
for(int mask=0;mask<8;mask++)Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.RescueScanRequired((mask&1)!=0,(mask&2)!=0,(mask&4)!=0)==((mask&1)!=0&&((mask&2)==0||(mask&4)!=0)),"alternate template is distinct from generalized rescue "+mask);
foreach(double distance in new[]{.18,.185,.19,.199999,.20,.21}){
 Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.CorridorSegmentLength(distance,true),"polyline sampling fragment remains capsule validated "+distance);
 Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.CorridorSegmentLength(distance,false)==(distance>=.20),"whole corridor minimum retained "+distance);
}
foreach(double bad in new[]{0,.17999,-1,double.NaN,double.PositiveInfinity})Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.CorridorSegmentLength(bad,true),"invalid or sub-resolution fragment "+bad);
// RC152 semantic WHAT must not overwrite the selected walking HOW.
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.RestoreSemanticWalkEntry(false),"normal displaced Nav projection retains semantic raw entry restore");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.RestoreSemanticWalkEntry(true),"Chris 319.697 selected retry entry cannot be replaced by original entry .829m away");
foreach(double side in new[]{.563,.698,.662,.647,.508,.48,.63}){
 double limit=BotTraversal.Core.FollowerCapabilityPassagePolicy.ObstacleCenterTolerance(.75,.55);
 Check(limit<=.2750001&&BotTraversal.Core.FollowerCapabilityPassagePolicy.ObstacleNeedsCenter(side,limit),"recorded Vault142 side error is centered before admission "+side);
}
foreach(double max in new[]{.1,.2,.42,.75})Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.ObstacleCenterTolerance(max,.55)<=max,"centering never widens learned lateral limit "+max);
foreach(double bad in new[]{0,-1,double.NaN,double.PositiveInfinity})Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.ObstacleCenterTolerance(bad,.55)==0,"invalid learned lane does not widen centering "+bad);
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.QueueClaimCurrent(7,0,false),"Chris mission46 old WT238 claim cannot admit refreshed WT230 7.558m away");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.QueueClaimCurrent(7,8,true),"reused portal now belongs to a different claim");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.QueueClaimCurrent(7,7,false),"same serial alone is not bot mission step ownership");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.QueueClaimCurrent(0,0,true),"no ticket is not admission");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.QueueClaimCurrent(7,7,true),"stable entry keeps existing queue progress clock");
foreach(string kind in new string[]{null,"","DropDown","WalkThrough","Jump","Climb","Vault"})foreach(bool current in new[]{false,true}){
 bool expected=kind=="WalkThrough"||current&&(kind=="DropDown"||string.IsNullOrEmpty(kind));
 Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.NativeEgressKind(kind,"DropDown",current)==expected,"Ada pre-acquisition drop egress uses current typed kind "+kind+":"+current);
}
foreach(string kind in new string[]{null,"","Jump","Climb","Vault"})Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.NativeEgressKind(kind,"Jump",true),"other recovery kinds keep original egress contract "+kind);
// RC154 proof identity and exact queued physical observation.
for(int mask=0;mask<4;mask++)Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.WalkingIdentity((mask&1)!=0,(mask&2)!=0)==(mask!=0),"native-proven semantic WT remains WT-only after current demonstration geometry changes "+mask);
for(int mask=0;mask<4;mask++)foreach(double angle in new[]{0,20,72.01585,81.44067,90,double.NaN})Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.SideCenterTurn((mask&1)!=0,(mask&2)!=0,angle)==((mask&1)==0&&(mask&2)!=0&&(double.IsNaN(angle)||angle>20)),"sideways Run does not wait for lateral-facing agreement "+mask+":"+angle);
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.ForwardCorner(1,1.12,.1,.1),"short authored forward corner remains selectable after long chord hits concrete");
foreach(double next in new[]{.9,1,1.02,1.651,double.NaN})Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.ForwardCorner(1,next,.1,.1),"blocked corner cannot select backward or remote route target "+next);
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.ForwardCorner(1,1.12,.221,.1),"corner rejoin cannot leave recorded lane");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.ForwardCorner(1,1.12,.1,.351),"corner rejoin cannot change vertical layer");
for(int mask=0;mask<32;mask++)Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.QueuedDropWitness((mask&1)!=0,(mask&2)!=0,(mask&4)!=0,(mask&8)!=0,(mask&16)!=0)==(mask==31),"queued natural descent requires captured upper witness and exact active epoch "+mask);
// Two WT236 ingress recordings 1.036m apart were incorrectly two simultaneous queues.
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.CanonicalQueue(true,true,1.036,0,.182,0,.99,2.5),"same proven passage shares canonical queue despite upstream recording shift");
for(int mask=0;mask<4;mask++)Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.CanonicalQueue((mask&1)!=0,(mask&2)!=0,1.036,0,.182,0,.99,2.5)==(mask==3),"only native-proven WT can use canonical queue anchor "+mask);
foreach(double gap in new[]{2.501,double.NaN,double.PositiveInfinity})Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.CanonicalQueue(true,true,gap,0,.182,0,.99,2.5),"remote entrance cannot borrow canonical queue "+gap);
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.CanonicalQueue(true,true,1.036,0,.651,0,.99,2.5),"different vertical portal remains independent");
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.CanonicalQueue(true,true,1.036,0,.182,0,.84,2.5),"different approach direction cannot borrow proven anchor");
// Chris has landed .781m after entry, short of the 1.320m physical-exit frontier.
// Existing lower-side Egress must be allocatable, never completed by location alone.
for(int mask=0;mask<16;mask++)Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.ObservedLowerDrop((mask&1)!=0,(mask&2)!=0,(mask&4)!=0,(mask&8)!=0,.8)==(mask==15),"lower continuation needs same-generation upper witness "+mask);
foreach(double bad in new[]{double.NaN,double.PositiveInfinity,-1,1.751})Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.ObservedLowerDrop(true,true,true,true,bad),"lower continuation is local to authored exit "+bad);
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.ObservedLowerDrop(true,true,true,true,.8),"actual lower Chris body may enter original local Egress");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.ImmediateEntryDistance(true,2.893,.781)==.781,"old upper-floor height does not veto observed lower continuation allocation");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.ImmediateEntryDistance(false,2.893,.781)==2.893,"unobserved lower body cannot borrow planar admission");
// RC154 finite signed ramp, predictive bend gait, exact episode interior.
foreach(double sign in new[]{-1d,1d})foreach(double height in new[]{.650001,.775,.85788,1.416,3.334})
{
 Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.RampEnvelope(sign*height,1.975),"both ramp directions request actual terrain proof "+sign+" "+height);
 Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.SemanticContinuation(true,sign*height,true),"verified supported signed slope continues "+sign+" "+height);
 Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.SemanticContinuation(true,sign*height,false),"height alone cannot authorize signed ramp "+sign+" "+height);
 Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.SemanticContinuation(false,sign*height,true),"ramp proof cannot replace current body support "+sign+" "+height);
}
foreach(double speed in new[]{0d,1d,2d,4d,5d,8d})
{
 double brake=BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.BrakingDistance(speed);
 foreach(double dot in new[]{-1d,0d,.5,.939})
 {
  Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.WalkingTurnPrecision(brake,dot,speed),"bend inside measured speed braking envelope "+speed+" "+dot);
  Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.WalkingTurnPrecision(brake+.001,dot,speed),"distant bend does not globally disable Sprint "+speed+" "+dot);
 }
 Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.WalkingTurnPrecision(-.151,0,speed),"already passed bend does not hold precision "+speed);
 Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.WalkingTurnPrecision(.1,1,speed),"straight lane keeps demonstrated Sprint "+speed);
}
foreach(double bad in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity})
{
 Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.WalkingTurnPrecision(bad,0,4),"invalid turn distance cannot masquerade as finite bend "+bad);
 Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.WalkingTurnPrecision(.1,bad,4),"invalid turn heading "+bad);
 Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.WalkingTurnPrecision(.1,0,bad),"invalid measured speed "+bad);
 Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.WalkingLanePrecision(bad,0,1),"invalid lane measurement holds Sprint "+bad);
 Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.WalkingInterior(true,bad,2,.1,.1),"invalid interior arc cannot waive entry distance "+bad);
}
Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.WalkingTurnPrecision(.56,0,4.5),"Chris WT245 90 degree short exit requires precision before body overshoots");
Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.WalkingLanePrecision(1.1799,.191,1),"recorded Chris offlane cannot retain Sprint");
Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.WalkingLanePrecision(.1,.1,0),"turning toward new WT tangent first uses precision");
Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.WalkingLanePrecision(.22,.35,.94),"centered supported aligned lane retains Sprint eligibility");
Check(BotTraversal.Core.FollowerCapabilityPassagePolicy.WalkingInterior(true,1.1,4,.1,.1),"bent current episode interior uses finite arc rather than entry-exit chord");
foreach(var v in new[]{(1.1,4d,.22001,.1),(1.1,4d,.1,.35001),(4d,4d,.1,.1),(-.001,4d,.1,.1)})
 Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.WalkingInterior(true,v.Item1,v.Item2,v.Item3,v.Item4),"outside or ended finite episode cannot waive entry distance "+v);
Check(!BotTraversal.Core.FollowerCapabilityPassagePolicy.WalkingInterior(false,1,4,.1,.1),"missing finite witness never waives entry distance");
Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.WalkingGaitCorrection(true,true),"entry precision may force Run");
Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.WalkingGaitCorrection(false,true),"in-passage bend precision cannot force Run or restart gait");
Check(!BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.WalkingGaitCorrection(true,false),"straight aligned entry not forced Run");
Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.WalkingTurnSpeed(1,true)==.65,"predictive bend bounds existing native Sprint speed before apex");
Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.WalkingTurnSpeed(.40,true)==.40,"bend precision never speeds up a tighter existing envelope");
Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.WalkingTurnSpeed(1,false)==1,"straight path retains full requested speed");
Check(BotTraversal.Core.FollowerCapabilityIngressMotionPolicy.WalkingTurnSpeed(double.NaN,true).Equals(double.NaN),"invalid speed is not laundered into an executable command");
// RC155 source lifetime: the 256 proven database may not delete a new demonstration before episode publication.
foreach(double age in new[]{0d,.001,19.131,43.2,88.21,120})
{
 bool fresh=BotTraversal.Core.FollowerCapabilityLearningPolicy.FreshDemonstration(true,false,age,120);
 Check(fresh,"current observed route retains its existing 120s candidate lifetime "+age);
 Check(!BotTraversal.Core.FollowerCapabilityLearningPolicy.CanDiscard(false,0,false,fresh),"soft capacity may not destroy a fresh unbound candidate "+age);
}
foreach(double age in new[]{-.001,120.001,double.NaN,double.PositiveInfinity})
 Check(!BotTraversal.Core.FollowerCapabilityLearningPolicy.FreshDemonstration(true,false,age,120),"invalid or expired candidate does not retain capacity "+age);
Check(!BotTraversal.Core.FollowerCapabilityLearningPolicy.FreshDemonstration(false,false,0,120),"unvalidated cache entry gains no player demonstration lease");
Check(!BotTraversal.Core.FollowerCapabilityLearningPolicy.FreshDemonstration(true,true,0,120),"native proof uses original proof protection rather than a candidate lease");
Check(!BotTraversal.Core.FollowerCapabilityLearningPolicy.CanDiscard(true,0,false,false),"historical semantics survive capacity");
Check(!BotTraversal.Core.FollowerCapabilityLearningPolicy.CanDiscard(false,1,false,false),"physical proof survives capacity");
Check(!BotTraversal.Core.FollowerCapabilityLearningPolicy.CanDiscard(false,0,true,false),"queued authored child or selected template survives TTL while consumed");
Check(BotTraversal.Core.FollowerCapabilityLearningPolicy.CanDiscard(false,0,false,false),"expired unused zero-value cache is still reclaimable");
foreach(int provenCount in new[]{255,256,257,512})
{
 var entries=Enumerable.Repeat((proof:1,fresh:false),provenCount).Append((proof:0,fresh:true)).ToList();
 var removable=entries.FindIndex(x=>BotTraversal.Core.FollowerCapabilityLearningPolicy.CanDiscard(false,x.proof,false,x.fresh));
 Check(removable==-1,"all-protected soft-limit pool retains just-recorded action even above 256 "+provenCount);
 entries.Add((0,false));removable=entries.FindIndex(x=>BotTraversal.Core.FollowerCapabilityLearningPolicy.CanDiscard(false,x.proof,false,x.fresh));
 Check(removable==provenCount+1,"old unbound cache removed instead of recorder's new action "+provenCount);
}
Check(BotTraversal.Core.FollowerCapabilityFailureSequence.EntryBudget(2.23444366f,1.1f,.55f,4.25f)==4.25f,"Chris Vault entry keeps finite approach budget instead of two 1.1s expiries");
Check(BotTraversal.Core.FollowerCapabilityFailureSequence.EntryBudget(0,1.1f,.55f,4.25f)==1.1f,"zero-distance entry gains no blanket timeout extension");
foreach(float d in new[]{.1f,.55f,1f,10f,100f})
 Check(BotTraversal.Core.FollowerCapabilityFailureSequence.EntryBudget(d,1.1f,.55f,4.25f)<=4.25f,"finite obstacle ingress maximum retained "+d);
var learnedGood=BotTraversal.Core.FollowerCapabilityLearningPolicy.RecoveryRank(2,0,.5,true,119,111,8,.9999999,1);
var learnedBad=BotTraversal.Core.FollowerCapabilityLearningPolicy.RecoveryRank(2,0,.5,true,119,2,117,.05,1);
Check(learnedGood>learnedBad,"current persisted skill evidence affects recovery ordering after reload");
Check(!double.IsInfinity(learnedBad)&&!double.IsNaN(learnedBad),"historically poor self-rescue stays ranked and executable");
Check(BotTraversal.Core.FollowerCapabilityLearningPolicy.RecoveryRank(3,2,.7,false,100,0,100,0,1)==BotTraversal.Core.FollowerCapabilityFailureSequence.Rank(3,2,.7,1),"wrong-kind or missing evidence keeps existing local ranking");
Check(BotTraversal.Core.FollowerCapabilityLearningPolicy.RecoveryRank(3,2,.7,true,5,3,2,.7,1)==BotTraversal.Core.FollowerCapabilityFailureSequence.Rank(3,2,.7,1),"persisted current counters are not added twice");
Check(BotTraversal.Core.FollowerCapabilityLearningPolicy.RecoveryRank(3,2,.7,true,0,0,0,0,1)==BotTraversal.Core.FollowerCapabilityFailureSequence.Rank(3,2,.7,1),"empty persisted evidence cannot supply an artificial zero evaluation");
Check(!double.IsNaN(BotTraversal.Core.FollowerCapabilityLearningPolicy.RecoveryRank(3,2,.7,true,5,3,2,double.NaN,1)),"corrupt persisted score follows original finite ranking fallback");
File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=true,checks=cases.Count,cases,physicsSimulated=false,kind="production DLL decision replay; host wiring checked separately"},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS {cases.Count} 0.9.0 Beta18 production decision and recorded-order replay checks");
