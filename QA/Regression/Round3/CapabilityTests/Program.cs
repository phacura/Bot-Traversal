using P=BotTraversal.Core.FollowerCapabilityStrategyPolicy;
using D=BotTraversal.Core.FollowerCapabilityDeliveryPolicy;
using R=BotTraversal.Core.FollowerCapabilityRetryPolicy;
using L=BotTraversal.Core.FollowerTraversalLifecycle;
using K=BotTraversal.Core.FollowerCapabilityDeliveryPolicy.Capability;
using System.Text.Json;
var checks=new List<string>();
void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);checks.Add(name);}
// The original five traversal seed contracts, plus the explicit Follow/Recovery contracts.
var traits=new[]{
 (K.WalkThrough,false,true,true,true,true,false),
 (K.Jump,true,false,true,false,false,true),
 (K.DropDown,true,false,true,true,false,true),
 (K.Climb,true,false,true,false,true,false),
 (K.Vault,true,false,true,false,true,false),
 (K.Follow,false,false,false,false,false,false),
 (K.Recovery,false,false,false,false,false,false)};
foreach(var (kind,act,pass,finish,acquire,trans,landing) in traits){
 var t=P.Describe(kind,.12f);
 Check(t.Actuator==act,kind+" actuator requirement");Check(t.PassageCompletion==pass,kind+" passage proof");
 Check(t.FinishOffMesh==finish,kind+" off-mesh finish");Check(t.AcquireOffMesh==acquire,kind+" off-mesh entry");
 Check(t.Transition==trans,kind+" transition");Check(t.Landing==landing,kind+" landing proof");
}
Check(P.Describe(K.Jump,.12f).Lead==.12f,"configured jump lead preserved");
Check(P.Describe(K.Unsupported,.12f).Actuator,"unknown capability conservative");
Check(D.Parse("PhysicalCorridor")==K.WalkThrough,"physical corridor shares passage strategy");
void Select(K kind,P.Executor expected,bool parameters=false,bool direction=true,bool assist=false,bool fresh=true,bool retired=false,bool downward=false){
 var p=P.Select(kind,parameters,direction,assist,fresh,retired,downward);
 Check(p.Executor==expected,$"{kind}: {expected} params={parameters} direction={direction} assist={assist} retired={retired} downward={downward}");
 if(expected==P.Executor.NativeJump||expected==P.Executor.NativeDropDown)Check(p.Native&&!p.Assisted,"native actuator has one executor");
 if(retired)Check(!p.Native&&p.Assisted,"retired primary cannot regain native selection");
}
Select(K.WalkThrough,P.Executor.GroundCorridor);Select(K.Follow,P.Executor.FollowPath);Select(K.Recovery,P.Executor.RecoveryPath);
Select(K.Jump,P.Executor.NativeJump);Select(K.DropDown,P.Executor.NativeDropDown);
Select(K.Jump,P.Executor.Compatibility,assist:true);Select(K.DropDown,P.Executor.Compatibility,assist:true);
Select(K.Jump,P.Executor.NativeJump,assist:true,fresh:false);
Select(K.Jump,P.Executor.Compatibility,downward:true);
Select(K.Jump,P.Executor.Compatibility,retired:true);Select(K.DropDown,P.Executor.Compatibility,retired:true);
foreach(var k in new[]{K.Vault,K.Climb}){
 Select(k,P.Executor.InjectedVault,parameters:true);Select(k,P.Executor.Compatibility);
 Select(k,P.Executor.Compatibility,parameters:true,direction:false);
 Select(k,P.Executor.Compatibility,parameters:true,assist:true);
 Select(k,P.Executor.Compatibility,parameters:true,retired:true);
}
Check(P.StartRolling(K.Climb,P.Executor.InjectedVault,.1f,.5f,3f),"Climb precision entry even inside trigger");
Check(P.StartRolling(K.Vault,P.Executor.Compatibility,.1f,.5f,3f),"compatibility Vault precision entry");
Check(P.StartRolling(K.Jump,P.Executor.NativeJump,.1f,.5f,3f),"Jump keeps native runup");
Check(!P.StartRolling(K.Unsupported,P.Executor.Compatibility,.1f,.5f,3f),"non precision avoids needless runup");
Check(!P.StartRolling(K.Climb,P.Executor.InjectedVault,4f,.5f,3f),"rolling entry bounded");
Check(!P.StartRolling(K.Climb,P.Executor.InjectedVault,float.NaN,.5f,3f),"invalid entry distance rejected");
// Source-level starvation counterexample (not inferred from throttled raid frames):
// 1000 unattempted proposals cannot push an actual attempt deadline forward.
float retry=224.374f;
for(int i=0;i<1000;i++)retry=R.Apply(retry,R.Change.ProposalDeferred,223.924f+i*.02f+.45f);
Check(retry==224.374f,"unattempted proposals cannot starve actual retry");
Check(224.428f>=retry,"later frame becomes eligible after original deadline");
Check(R.Apply(retry,R.Change.AttemptOrExplicitHold,225f)==225f,"real attempt may set retry deadline");
Check(R.Apply(retry,R.Change.Reset,0f)==0f,"existing lifecycle reset supported");
Check(R.Apply(retry,R.Change.AttemptOrExplicitHold,float.NaN)==retry,"NaN cannot poison retry");
Check(R.Apply(retry,R.Change.AttemptOrExplicitHold,float.PositiveInfinity)==retry,"infinite hold rejected");
// Vertical chain fixture joins the actual lifecycle and actual capability policy.
var mission=new L.MissionState();var steps=new[]{new L.StepState(),new L.StepState(),new L.StepState()};
L.Identity Id(int i,long writer=20)=>new("raid-bot",2,131,i,28+i,99+i,24,steps[i].GeometryVersion,writer);
for(int i=0;i<2;i++){
 var id=Id(i);var probe=new L.ProbeState(id,steps[i]);Check(steps[i].Attach(probe),"chain predecessor attached");
 Check(probe.PublishCompletion(id,steps[i],L.ActionOutcome.NativePhysical)==L.CommitResult.Applied,"chain predecessor physical receipt");
 Check(mission.Resolve(steps[i],id,steps[i],id,L.Resolution.Executed,true,true)==L.CommitResult.Applied,"chain predecessor resolved");probe.Retire();
}
var delivery=D.Evaluate(new(K.DropDown,true,false,true,true,true,true,false,false,true,1.00086975f,3f));
Check(delivery.Kind==D.Delivery.PublishNativeGroundPath,"raid ground delivery selected");
Check(steps[2].DebtOutstanding&&mission.Cursor==2,"path proposal never resolves second DropDown");
Check(!mission.CanHandoffToFollow(3,false,true),"path acceptance cannot hand off unfinished chain");
Check(mission.Resolve(steps[2],Id(2),steps[2],Id(2),L.Resolution.Subsumed,false,true)==L.CommitResult.Rejected,"input-only evidence never completes second descent");
Check(steps[2].DebtOutstanding,"failed movement keeps debt");
Check(mission.Resolve(steps[2],Id(2,21),steps[2],Id(2),L.Resolution.Subsumed,true,true)==L.CommitResult.Rejected,"writer handoff cannot borrow old receipt");
Check(mission.Resolve(steps[2],Id(2,21),steps[2],Id(2,21),L.Resolution.Subsumed,true,true)==L.CommitResult.Applied,"new qualified body evidence can finish existing step");
Check(mission.CanHandoffToFollow(3,false,false),"Follow only after qualified physical completion");
File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=true,checks=checks.Count,cases=checks,scope="traits/executor/entry/retry and lifecycle chain; EFT physics not simulated"},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS {checks.Count} compiled capability/retry/chain checks");
