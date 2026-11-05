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

File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=true,checks=cases.Count,cases,physicsSimulated=false,kind="production DLL decision replay; host wiring checked separately"},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS {cases.Count} RC132 production decision and recorded-order replay checks");
