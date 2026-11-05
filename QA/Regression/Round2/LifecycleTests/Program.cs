using L = BotTraversal.Core.FollowerTraversalLifecycle;
using System.Text.Json;
using System.Reflection;
var checks=new List<string>();
void Check(bool ok,string name) { if(!ok) throw new Exception("FAIL "+name);checks.Add(name); }
L.Identity Id(int index,L.StepState s,long writer=10,string bot="bot-A",int raid=2,int mission=7,int episode=0,int edge=0,int chain=3) =>
    new(bot,raid,mission,index,episode==0?100+index:episode,edge==0?200+index:edge,chain,s.GeometryVersion,writer);
L.ProbeState Start(L.StepState s,L.Identity id) { var p=new L.ProbeState(id,s);Check(s.Attach(p),"attach exact step");return p; }
void Native(L.MissionState m,L.StepState s,L.Identity id) {
 var p=Start(s,id);
 Check(p.PublishCompletion(id,s,L.ActionOutcome.NativePhysical)==L.CommitResult.Applied,"native action observed once");
 Check(m.Resolve(s,id,s,id,L.Resolution.Executed,true,true)==L.CommitResult.Applied,"native completion consumes current step");
 p.Retire();
}
// Permanent fixture 1: Jump -> DropDown -> distinct DropDown -> Follow.
{
 var m=new L.MissionState();var steps=Enumerable.Range(0,3).Select(_=>new L.StepState()).ToArray();
 for(int i=0;i<3;i++){
  Check(m.Cursor==i,"separate: current exact step "+i);
  Check(!m.CanHandoffToFollow(3,false,true),"separate: no Follow while debt remains "+i);
  Native(m,steps[i],Id(i,steps[i],10+i));
  Check(steps[i].Result==L.Resolution.Executed,"separate: own action resolves own debt "+i);
  if(i<2)Check(steps[i+1].DebtOutstanding,"separate: successor cannot inherit success "+i);
 }
 Check(m.CanHandoffToFollow(3,false,false),"separate: Follow after all three actions");
 Check(!m.HasNonPhysicalRetirement,"separate: no teleport or suppression hidden in success");
}
// Permanent fixture 2: predecessor physically covers second descent, no fake Native attempt.
{
 var m=new L.MissionState();var a=new L.StepState();var b=new L.StepState();var c=new L.StepState();
 Native(m,a,Id(0,a));Native(m,b,Id(1,b,11));var current=Id(2,c,12);
 Check(m.Resolve(c,current,c,current,L.Resolution.Subsumed,true,true)==L.CommitResult.Applied,"covered: exact qualified physical debt resolution");
 Check(c.Result==L.Resolution.Subsumed && c.Probe==null,"covered: distinct Subsumed classification, no invented action");
 Check(m.CanHandoffToFollow(3,false,false),"covered: Follow permitted after qualified coverage");
 Check(m.Resolve(c,current,c,current,L.Resolution.Subsumed,true,true)==L.CommitResult.Repeated,"covered: duplicate proof is idempotent");
 Check(m.Cursor==3,"covered: duplicate cannot consume fourth step");
}
// Permanent fixture 3: body near landing boundary, evidence insufficient.
{
 var m=new L.MissionState();var a=new L.StepState();var b=new L.StepState();var c=new L.StepState();
 Native(m,a,Id(0,a));Native(m,b,Id(1,b,11));var id=Id(2,c,12);
 for(int repeat=0;repeat<8;repeat++){
  Check(m.Resolve(c,id,c,id,L.Resolution.Subsumed,false,true)==L.CommitResult.Rejected,"uncertain: repeated frames cannot manufacture proof "+repeat);
  Check(m.Cursor==2 && c.DebtOutstanding,"uncertain: second descent remains owed "+repeat);
 }
 Check(m.Resolve(c,id,c,id,L.Resolution.Suppressed,true,true)==L.CommitResult.Rejected,"uncertain: failed actuator selector is not success");
 Check(c.Status==L.Phase.Blocked && c.DebtOutstanding,"uncertain: blocker is explicit with debt intact");
 c.Recover();Check(c.Status==L.Phase.Recovering,"uncertain: recovery changes execution phase only");
 Check(!m.CanHandoffToFollow(3,false,true),"uncertain: recovery never completes route");
 Native(m,c,id);Check(m.CanHandoffToFollow(3,false,false),"uncertain: later valid native evidence can finish");
}
// Full birth identity and object identity; delayed callbacks cannot borrow newer state.
{
 var m=new L.MissionState();var step=new L.StepState();var id=Id(0,step);var p=Start(step,id);
 var foreign=new [] {
  new L.Identity("bot-B",2,7,0,100,200,3,0,10), new L.Identity("bot-A",3,7,0,100,200,3,0,10),
  new L.Identity("bot-A",2,8,0,100,200,3,0,10), new L.Identity("bot-A",2,7,1,100,200,3,0,10),
  new L.Identity("bot-A",2,7,0,101,200,3,0,10), new L.Identity("bot-A",2,7,0,100,201,3,0,10),
  new L.Identity("bot-A",2,7,0,100,200,4,0,10), new L.Identity("bot-A",2,7,0,100,200,3,1,10),
  new L.Identity("bot-A",2,7,0,100,200,3,0,11) };
 for(int i=0;i<foreign.Length;i++){
  Check(!p.Matches(foreign[i],step),"foreign identity component rejected "+i);
  Check(p.PublishCompletion(foreign[i],step,L.ActionOutcome.NativePhysical)==L.CommitResult.Rejected,"foreign completion cannot publish "+i);
  Check(m.Resolve(step,foreign[i],step,id,L.Resolution.Executed,true,true)==L.CommitResult.Rejected,"foreign cursor commit rejected "+i);
 }
 var replacement=new L.StepState();Check(!p.Matches(id,replacement),"same numbers on replacement object are not original step");
 Check(m.Resolve(replacement,id,step,id,L.Resolution.Subsumed,true,true)==L.CommitResult.Rejected,"replaced step cannot consume current debt");
 Check(m.Cursor==0,"all stale callbacks leave cursor unchanged");p.Retire();
}
// Geometry revision invalidates old input/landing evidence; retries get an explicit new origin.
{
 var m=new L.MissionState();var step=new L.StepState();var oldId=Id(0,step);var old=Start(step,oldId);
 step.GeometryChanged();var now=Id(0,step,11);
 Check(!old.Matches(now,step),"geometry refresh invalidates active origin");
 Check(old.PublishCompletion(now,step,L.ActionOutcome.NativePhysical)==L.CommitResult.Rejected,"old landing cannot complete new geometry");
 Check(!step.Attach(new L.ProbeState(now,step)),"new probe cannot replace still-active producer");
 old.Retire();var fresh=Start(step,now);old.Retire();Check(ReferenceEquals(step.Probe,fresh),"duplicate old cleanup cannot detach successor");
 Check(fresh.PublishCompletion(now,step,L.ActionOutcome.NativePhysical)==L.CommitResult.Applied,"fresh geometry can receive independent evidence");
 Check(m.Resolve(step,now,step,now,L.Resolution.Executed,true,true)==L.CommitResult.Applied,"fresh exact origin resolves once");fresh.Retire();
}
// Teleport, suppression, cancellation and input publication are separate from physical execution.
{
 var m=new L.MissionState();var step=new L.StepState();var id=Id(0,step);var p=Start(step,id);
 Check(m.Resolve(step,id,step,id,L.Resolution.Executed,true,true)==L.CommitResult.Rejected,"issued input without completion is not step success");
 Check(p.PublishCompletion(id,step,L.ActionOutcome.Unproven)==L.CommitResult.Rejected,"unproven outcome cannot be completed");
 Check(p.PublishCompletion(id,step,L.ActionOutcome.Teleport)==L.CommitResult.Applied,"teleport is explicit action outcome");
 Check(m.Resolve(step,id,step,id,L.Resolution.Executed,true,true)==L.CommitResult.Rejected,"teleport cannot become physical execution");
 Check(m.Resolve(step,id,step,id,L.Resolution.TeleportBypass,true,true)==L.CommitResult.Applied,"verified teleport retires execution debt separately");
 Check(m.HasNonPhysicalRetirement && !step.PhysicallyResolved,"teleport does not produce physical route completion");p.Retire();
}
// Mission retirement and structural edits do not grant completion or reopen a stale producer.
{
 var m=new L.MissionState();var step=new L.StepState();var id=Id(0,step);m.Retire();
 Check(m.Resolve(step,id,step,id,L.Resolution.Subsumed,true,true)==L.CommitResult.Rejected,"retired mission rejects late resolution");
 Check(!m.CanHandoffToFollow(1,false,true),"cancelled mission with debt cannot claim completed Follow handoff");
 var empty=new L.MissionState();Check(empty.SkipEmpty(0),"empty slot explicitly retired");Check(empty.HasNonPhysicalRetirement,"empty slot is not physical success");
 Check(empty.RemovedAt(0,false) && empty.Cursor==0,"removing prior slot preserves current identity, adjusts sole index");
}
// Admission failures cannot leave a phantom executing producer on a step.
{
 var step=new L.StepState();var id=Id(0,step);
 Check(!step.Attach(new L.ProbeState(default,step)),"missing origin cannot register producer");
 Check(!step.Attach(new L.ProbeState(Id(0,step,0),step)),"missing writer ticket cannot register producer");
 Check(!step.Attach(new L.ProbeState(id,new L.StepState())),"producer must name the exact step object");
 var p=Start(step,id);p.Retire();Check(step.DebtOutstanding && step.Status==L.Phase.Recovering,"aborted probe returns unresolved debt to recovery");
 Check(p.Outcome==L.ActionOutcome.Aborted,"abort is not action completion");
}
// Assisted physical execution can resolve a step without becoming Native.
{
 var m=new L.MissionState();var step=new L.StepState();var id=Id(0,step);var p=Start(step,id);
 Check(p.PublishCompletion(id,step,L.ActionOutcome.NonNative)==L.CommitResult.Applied,"assisted outcome remains explicit");
 Check(m.Resolve(step,id,step,id,L.Resolution.Executed,true,true)==L.CommitResult.Applied,"assisted physical crossing may settle semantic debt");
 Check(p.Outcome==L.ActionOutcome.NonNative,"step completion cannot rewrite action outcome to Native");p.Retire();
}
var report=new { passed=true,checks=checks.Count,fixtures=new[]{"JUMP-DROP-DROP-SEPARATE","JUMP-DROP-DROP-SUBSUMED","JUMP-DROP-DROP-UNPROVEN"},assertions=checks };
if(args.Length>0)File.WriteAllText(args[0],JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS {checks.Count} compiled lifecycle state-transition checks; 3 permanent chain fixtures");
