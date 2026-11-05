using System.Text.Json;
using BotTraversal.Core;
using P=BotTraversal.Core.FollowerCapabilityProbeFramePolicy;
using D=BotTraversal.Core.FollowerCapabilityProbeFrameDriver;
var checks=new List<string>();
void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);checks.Add(name);}
string Trace(Port p)=>string.Join(",",p.Events);
Check(P.CombatAction(true,true,false)==P.Combat.AbortWalkThrough,"WalkThrough combat abort outranks exclusive motion");
Check(P.CombatAction(true,false,true)==P.Combat.AbortWalkThrough,"WalkThrough cannot borrow pre-native combat semantics");
Check(P.CombatAction(false,false,false)==P.Combat.AbortBeforeNative,"combat before native acquisition releases probe");
Check(P.CombatAction(false,false,true)==P.Combat.AbortBeforeNative,"old defer flag cannot preserve an unowned pre-native probe");
Check(P.CombatAction(false,true,false)==P.Combat.DeferUntilLanding,"active exclusive traversal defers combat to landing");
Check(P.CombatAction(false,true,true)==P.Combat.Continue,"already deferred combat does not reissue deferral");
Check(P.StartPendingWalkThrough(true,true,true,"WalkThrough"),"qualified deferred WalkThrough starts");
Check(P.StartPendingWalkThrough(true,true,true,"wAlKtHrOuGh"),"WalkThrough kind preserves ordinal case-insensitive match");
Check(!P.StartPendingWalkThrough(false,true,true,"WalkThrough"),"existing feature disable remains authoritative");
Check(!P.StartPendingWalkThrough(true,false,true,"WalkThrough"),"no pending action cannot manufacture a WalkThrough start");
Check(!P.StartPendingWalkThrough(true,true,false,"WalkThrough"),"pending action cannot restart a later stage");
Check(!P.StartPendingWalkThrough(true,true,true,"Jump"),"Jump cannot use WalkThrough pending start");
Check(!P.StartPendingWalkThrough(true,true,true,null!),"missing kind cannot start pending WalkThrough");
Check(D.Commit(null!)==D.Receipt.Rejected,"missing frame port is rejected");
var p=new Port();
Check(D.Commit(p)==D.Receipt.StageDispatched,"ordinary live probe dispatches one stage");
Check(Trace(p)=="capture,meaningful,passage,threat?,isolate,lease?,combat?,pending?,dispatch:NativeDropDown","live frame preserves observation and effect ordering");
Check(p.Dispatches==1&&p.UnsafeEffects==0,"one live frame has one validated stage effect");
p=new Port{Current=false};Check(D.Commit(p)==D.Receipt.Rejected&&Trace(p)=="reject","stale initial frame cannot capture actors or observe progress");
p=new Port{Threat=true,Expired=true,Combat=true,Pending=true};
Check(D.Commit(p)==D.Receipt.Interrupted,"immediate survival threat preempts lease combat and pending action");
Check(Trace(p)=="capture,meaningful,passage,threat?,interrupt:survival","threat exits before movement isolation and normal stage");
Check(p.Dispatches==0&&p.UnsafeEffects==0,"survival interruption cannot also execute a stage");
p=new Port{Expired=true,Combat=true,Pending=true};
Check(D.Commit(p)==D.Receipt.Expired,"lease expiry preempts combat pending action and dispatch");
Check(Trace(p)=="capture,meaningful,passage,threat?,isolate,lease?,expire","isolation and expiry preserve original order");
p=new Port{Combat=true,WalkThrough=true,Pending=true};
Check(D.Commit(p)==D.Receipt.CombatAborted,"combat aborts a WalkThrough frame");
Check(p.Events.Last()=="abortWT"&&!p.Events.Contains("pending?"),"WalkThrough combat cannot fall through to pending start");
p=new Port{Combat=true,Exclusive=false,Pending=true};
Check(D.Commit(p)==D.Receipt.CombatAborted&&p.Events.Last()=="abortPre","combat before native cannot retain stage authority");
p=new Port{Combat=true};
Check(D.Commit(p)==D.Receipt.StageDispatched,"exclusive native traversal may finish with deferred combat");
Check(p.Deferrals==1&&p.Dispatches==1&&p.Events.IndexOf("defer:combat")<p.Events.IndexOf("pending?"),"combat deferral is committed before pending selection and stage dispatch");
Check(p.AlreadyDeferred,"combat deferral uses the existing probe flag");
p=new Port{Combat=true,AlreadyDeferred=true};
Check(D.Commit(p)==D.Receipt.StageDispatched&&p.Deferrals==0,"already deferred frame performs no second deferral");
p=new Port{Pending=true};
Check(D.Commit(p)==D.Receipt.PendingStarted&&p.PendingStarts==1&&p.Dispatches==0,"pending WalkThrough starts exactly once and owns that frame");
Check(!p.Pending,"pending start consumes the existing flag");
Check(D.Commit(p)==D.Receipt.StageDispatched&&p.PendingStarts==1&&p.Dispatches==1,"following frame proceeds without replaying pending action");
p=new Port{Combat=true,Pending=true};
Check(D.Commit(p)==D.Receipt.PendingStarted&&p.Deferrals==1&&p.Dispatches==0,"combat deferral does not consume pending stage dispatch twice");
foreach(var point in new[]{"capture","meaningful","passage","threat?","isolate","lease?","combat?","pending?"}){
 p=new Port{InvalidateAfter=point};var receipt=D.Commit(p);
 Check(receipt==D.Receipt.Rejected,"lifecycle change during "+point+" rejects remaining frame");
 Check(p.Dispatches==0&&p.UnsafeEffects==0,"no stale stage or effect after "+point);
}
p=new Port{Threat=true,InvalidateAfter="threat?"};
Check(D.Commit(p)==D.Receipt.Rejected&&!p.Events.Any(e=>e.StartsWith("interrupt:")),"threat callback changing epoch cannot interrupt another lifecycle");
p=new Port{Expired=true,InvalidateAfter="lease?"};
Check(D.Commit(p)==D.Receipt.Rejected&&!p.Events.Contains("expire"),"lease query changing epoch cannot expire a replacement lifecycle");
p=new Port{Combat=true,WalkThrough=true,InvalidateAfter="combatPlan"};
Check(D.Commit(p)==D.Receipt.Rejected&&p.UnsafeEffects==0&&!p.Events.Contains("abortWT"),"combat decision cannot mutate after lifecycle changed");
p=new Port{Combat=true,InvalidateAfter="defer:combat"};
Check(D.Commit(p)==D.Receipt.Rejected&&p.Dispatches==0&&p.UnsafeEffects==0,"deferral callback invalidation cannot dispatch next stage");
p=new Port{Pending=true,InvalidateAfter="pending?"};
Check(D.Commit(p)==D.Receipt.Rejected&&p.PendingStarts==0,"pending observation changing epoch cannot consume replacement action");
p=new Port{Combat=true,ChangeStageOnDeferral="Landing"};
Check(D.Commit(p)==D.Receipt.StageDispatched&&p.Events.Last()=="dispatch:Landing","stage is read at dispatch after prior callbacks rather than eagerly cached");
foreach(string stage in new[]{"Aligning","Approaching","RollingApproach","NativeEntryAcquisition","JumpPriming","LearnedJumpTakeoffPriming","Priming","ForwardHop","NativeDropDown","AssistedReplay","NativeGroundContinuation","WalkThrough","Egress","Verify","ExitCommit","WaitingForState","PostLandingCommit","Traversing","Landing","TeleportArm","SafetyHold"}){
 p=new Port{Stage=stage};Check(D.Commit(p)==D.Receipt.StageDispatched&&p.Dispatches==1&&p.Events.Last()=="dispatch:"+stage,"single current-stage dispatch contract: "+stage);
}
p=new Port{Stage="Verify",RetireOnDispatch=true};
Check(D.Commit(p)==D.Receipt.StageDispatched&&p.Dispatches==1&&!p.Current,"terminal stage may retire itself without a second stage in same frame");
Check(D.Commit(p)==D.Receipt.Rejected&&p.Dispatches==1,"retired probe cannot reenter stage on later frame");
File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=true,checks=checks.Count,cases=checks,physicsSimulated=false},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS {checks.Count} compiled probe-frame order/preemption/reentry checks");
sealed class Port:ICapabilityProbeFramePort{
 public readonly List<string> Events=new();
 public bool Current=true,Threat,Expired,Combat,WalkThrough,Exclusive=true,AlreadyDeferred,Pending,RetireOnDispatch;
 public string Stage="NativeDropDown",InvalidateAfter="",ChangeStageOnDeferral="";
 public int Dispatches,Deferrals,PendingStarts,UnsafeEffects;
 void Record(string text,bool effect=false){if(effect&&!Current)UnsafeEffects++;Events.Add(text);if(text==InvalidateAfter)Current=false;}
 public bool IsCurrent=>Current;
 public void RejectLifecycle()=>Record("reject");
 public void CaptureActors()=>Record("capture");
 public void ObserveMeaningfulProgress()=>Record("meaningful");
 public void ObservePhysicalPassage()=>Record("passage");
 public bool TryImmediateThreat(out string reason){Record("threat?");reason="survival";return Threat;}
 public void InterruptForThreat(string reason)=>Record("interrupt:"+reason,true);
 public void IsolateMovement()=>Record("isolate",true);
 public bool LeaseExpired{get{Record("lease?");return Expired;}}
 public void ExpireLease()=>Record("expire",true);
 public bool TryCombatClaim(out string reason){Record("combat?");reason="combat";return Combat;}
 public P.Combat CombatAction{get{Record("combatPlan");return P.CombatAction(WalkThrough,Exclusive,AlreadyDeferred);}}
 public void AbortWalkThroughForCombat()=>Record("abortWT",true);
 public void AbortBeforeNativeForCombat()=>Record("abortPre",true);
 public void DeferCombat(string reason){Record("defer:"+reason,true);AlreadyDeferred=true;Deferrals++;if(ChangeStageOnDeferral!="")Stage=ChangeStageOnDeferral;}
 public bool PendingWalkThrough{get{Record("pending?");return Pending;}}
 public void StartPendingWalkThrough(){Record("beginWT",true);PendingStarts++;Pending=false;}
 public void DispatchStage(){Record("dispatch:"+Stage,true);Dispatches++;if(RetireOnDispatch)Current=false;}
}
