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
File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=true,checks=cases.Count,cases,physicsSimulated=false,kind="production DLL decision replay; host wiring checked separately"},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS {cases.Count} RC128 production decision and recorded-order replay checks");
