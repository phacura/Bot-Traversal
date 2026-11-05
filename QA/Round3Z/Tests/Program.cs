using System.Text.Json;
using BotTraversal.Core;
using D=BotTraversal.Core.FollowerMovementWriterAuthority.WriterDomain;
using R=BotTraversal.Core.FollowerCapabilityFollowRecoveryPolicy;
using RM=BotTraversal.Core.CapabilityFollowRecoveryMode;
using RF=BotTraversal.Core.CapabilityFollowRecoveryFailure;
using C=BotTraversal.Core.FollowerCapabilityRouteContinuationPolicy;
using CF=BotTraversal.Core.CapabilityContinuationFailure;
using CP=BotTraversal.Core.CapabilityContinuationPhase;
using L=BotTraversal.Core.FollowerTraversalLifecycle;
using E=BotTraversal.Core.FollowerControlEntryPolicy;
using A=BotTraversal.Core.ControlAdmission;
using SM=BotTraversal.Core.ControlSiteMode;
var checks=new List<string>();
void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);checks.Add(name);}
R.Snapshot Recovery(RM mode,D owner,long origin=10,long live=10,bool native=false,bool probe=true,bool canonical=false,bool mission=false,bool epoch=false,bool blocked=false)=>
 new(origin,live,owner,mode,true,native,probe,canonical,mission,epoch,blocked);
L.Identity Id(int step=0,long writer=10,int mission=7)=>new("bot-A",2,mission,step,100+step,200+step,3,0,writer);
C.Snapshot Route(CP phase,bool native=false,bool blocked=false,long origin=10,long live=10)=>new(Id(writer:origin),Id(writer:live),phase,true,true,true,true,native,blocked);
// Cross-policy invariants are expectations for a common observed situation, not
// a second runtime arbiter. Each existing capability retains its own decision.
foreach(bool active in new[]{false,true})foreach(bool blocked in new[]{false,true}){
 Check((C.Evaluate(Route(CP.CanonicalRoute,active,blocked))==CF.None)==(!active&&!blocked),$"canonical reserves Native body active={active} blocked={blocked}");
 Check((C.Evaluate(Route(CP.NativeLanding,active,blocked))==CF.None)==(active&&!blocked),$"landing requires own live Native body active={active} blocked={blocked}");
 Check((R.Evaluate(Recovery(RM.ProbeRecovery,D.Traversal,native:active,blocked:blocked))==RF.None)==(active&&!blocked),$"explicit probe recovery shares Native reservation active={active} blocked={blocked}");
 Check((R.Evaluate(Recovery(RM.GeneralRecovery,D.Follow,native:active,blocked:blocked))==RF.None)==(!active&&!blocked),$"ordinary recovery never races Native active={active} blocked={blocked}");
 Check((R.EvaluateFresh(Recovery(RM.FollowPath,D.Follow,native:active,blocked:blocked))==RF.None)==(!active&&!blocked),$"fresh Follow never supersedes Native active={active} blocked={blocked}");
}
foreach(long origin in new long[]{0,9,10,11})foreach(long live in new long[]{0,9,10,11}){
 bool expected=origin>0&&origin==live;
 Check((R.Evaluate(Recovery(RM.TeleportQuiescence,D.Teleport,origin,live,native:true,canonical:true,mission:true,blocked:true))==RF.None)==expected,$"teleport exact positive origin {origin}/{live}");
 Check((C.Evaluate(Route(CP.NativeLanding,true,origin:origin,live:live))==CF.None)==expected,$"landing exact positive origin {origin}/{live}");
 Check((R.Evaluate(Recovery(RM.ProbeRecovery,D.Traversal,origin,live,native:true))==RF.None)==expected,$"probe repair exact positive origin {origin}/{live}");
}
foreach(D owner in Enum.GetValues<D>()){
 Check(R.EvaluateFresh(Recovery(RM.FollowPath,owner,mission:true))!=RF.None,"ordinary Follow cannot discard pending mission under "+owner);
 Check(R.Evaluate(Recovery(RM.GeneralRecovery,owner,native:true))!=RF.None,"idle/general cleanup cannot erase active Native under "+owner);
 Check((R.Evaluate(Recovery(RM.GeneralRecovery,owner,mission:true,epoch:true))==RF.None)==(owner is D.Follow or D.Recovery or D.PostTraversal),"terminal recovery remains same-epoch and domain limited "+owner);
}
foreach(int mode in new[]{-1,3,999})foreach(int flags in Enumerable.Range(0,64))
 Check(E.Decide((flags&1)!=0,(flags&2)!=0,(SM)mode,(flags&4)!=0,(flags&8)!=0,(flags&16)!=0,(flags&32)!=0)==A.Reject,$"unknown catalog mode fails closed {mode}:{flags}");
foreach(int flags in Enumerable.Range(0,64)){
 bool known=(flags&1)!=0,operation=(flags&2)!=0,target=(flags&4)!=0,scope=(flags&8)!=0,exact=(flags&16)!=0,compat=(flags&32)!=0;
 Check(E.Decide(known,operation,SM.ExactScope,target,scope,exact,compat)==(known&&operation&&target&&scope&&exact?A.ExactOrigin:A.Reject),"strict site never falls back to compatibility "+flags);
}
// Retry after a real Jump + DropDown + DropDown must preserve three independent
// probes. Every late callback from the previous action is deliberately replayed.
for(int trial=0;trial<3;trial++){
 var mission=new L.MissionState();var steps=Enumerable.Range(0,3).Select(_=>new L.StepState()).ToArray();
 L.ProbeState? previous=null;L.Identity previousId=default;
 for(int index=0;index<3;index++){
  var step=steps[index];var id=Id(index,10+index,7+trial);var probe=new L.ProbeState(id,step);
  Check(step.Attach(probe),$"trial {trial} action {index} independently attaches");
  if(previous!=null){
   Check(previous.PublishCompletion(id,step,L.ActionOutcome.NativePhysical)==L.CommitResult.Rejected,$"late previous Native callback cannot complete successor {trial}:{index}");
   Check(mission.Resolve(step,id,steps[index-1],previousId,L.Resolution.Executed,true,true)==L.CommitResult.Repeated,$"late exact duplicate remains idempotent {trial}:{index}");
   Check(mission.Cursor==index&&step.DebtOutstanding,$"duplicate leaves successor debt intact {trial}:{index}");
  }
  Check(!mission.CanHandoffToFollow(3,true,true),$"no premature Follow handoff {trial}:{index}");
  Check(probe.PublishCompletion(id,step,L.ActionOutcome.NativePhysical)==L.CommitResult.Applied,$"independent physical proof {trial}:{index}");
  Check(mission.Resolve(step,id,step,id,L.Resolution.Executed,true,true)==L.CommitResult.Applied,$"independent debt consumption {trial}:{index}");
  probe.Retire();previous=probe;previousId=id;
 }
 Check(mission.CanHandoffToFollow(3,false,false)&&!mission.HasNonPhysicalRetirement,"full independent chain can release to Follow "+trial);
}
File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=true,checks=checks.Count,cases=checks,physicsSimulated=false,scope="cross-capability ownership, unknown admission, replayed JDD lifecycle"},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"PASS {checks.Count} cross-capability checks");
