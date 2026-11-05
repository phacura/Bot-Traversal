using System.Text.Json;
using System.Security.Cryptography;
using BotTraversal.Core;
using P=BotTraversal.Core.FollowerCapabilityRouteRetryPolicy;
using L=BotTraversal.Core.FollowerTraversalLifecycle;
var checks=new List<string>();void Check(bool ok,string why){if(!ok)throw new Exception(why);checks.Add(why);}
var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(args[0])!,"Evidence/confirmed-evidence.json"))).RootElement;
foreach(var sample in fixture.GetProperty("falseFallCases").EnumerateArray()){
 float below=sample.GetProperty("verticalBelowFutureVaultEntry").GetSingle(),distance=sample.GetProperty("distanceToFutureVaultEntry").GetSingle();
 Check(!P.FailureAdmission(false,true,false,true),"recorded normal low ground before unacquired Vault stays in approach "+below);
 Check(Math.Abs(sample.GetProperty("verticalToRecordedGround").GetSingle())<.005f,"recorded grounded player corridor agrees with bot floor "+below);
}
foreach(float low in new[]{.654f,1f,3f,100f})Check(!P.FailureAdmission(false,true,false,true),"height alone cannot fail unacquired entry "+low);
Check(P.FailureAdmission(true,true,false,true),"explicit executor failure requests retry");
Check(!P.FailureAdmission(false,true,false,true),"active native actuator cannot be interrupted");
Check(P.FailureAdmission(true,true,false,true),"explicit WT executor failure can recover");
float budget=P.ReturnBudget(8.5f);Check(budget==29.5f,"return budget captured at actual failure");
foreach(float movedAway in new[]{25.6f,50f,205.58f})Check(!P.PathWithinBudget(74.62f,budget),"drifting away cannot increase frozen budget "+movedAway);
Check(P.PathWithinBudget(20,budget),"safe bounded return admitted");
foreach(float bad in new[]{float.NaN,float.NegativeInfinity,float.PositiveInfinity,-1,float.MaxValue})Check(P.ReturnBudget(bad)==0,"invalid or overflowing failure geometry has no path budget "+bad);
foreach(float bad in new[]{float.NaN,float.NegativeInfinity,float.PositiveInfinity,-1}){Check(!P.PathWithinBudget(bad,29.5f),"invalid native path length "+bad);Check(!P.PathWithinBudget(20,bad),"invalid frozen budget "+bad);}
// Actual production decisions across continuous samples, not a Unity simulation.
// A valid return may initially grow in direct distance while its remaining arc shrinks.
float remaining=74.6187f,time=182.502f,lastProgress=time;int stops=0,replans=0,kept=0;
foreach(var sample in new[]{(182.954f,72.3227f,26.3f),(183.409f,70.6124f,27.6f),(183.866f,68.2803f,28.4f)}){
 Check(P.PathProgress(remaining,sample.Item2,.3f),"recorded return arc progresses while Euclidean grows "+sample.Item3);
 remaining=sample.Item2;lastProgress=sample.Item1;
 var action=P.ReturnDecision(true,sample.Item1-lastProgress,true);if(action==P.ReturnAction.KeepPath)kept++;else{stops++;replans++;}
 Check(action==P.ReturnAction.KeepPath,"live recorded native way is preserved "+sample.Item1);
}
Check(kept==3&&stops==0&&replans==0,"healthy curved path causes zero stop or replacement requests");
Check(P.ReturnDecision(true,2.1f,true)==P.ReturnAction.RequestPath,"actual stalled owned path can recover after existing progress clock");
Check(P.ReturnDecision(false,0,true)==P.ReturnAction.RequestPath,"foreign or replaced path never inherits current receipt");
Check(P.ReturnDecision(false,0,false)==P.ReturnAction.Wait,"existing retry throttle respected");
foreach(float age in new[]{float.NaN,float.PositiveInfinity,-.1f,2f})Check(P.ReturnDecision(true,age,true)==P.ReturnAction.RequestPath,"invalid or expired path progress does not falsely retain "+age);
Check(!P.PathProgress(10,8,0),"resource bookkeeping without real body motion is not progress");
Check(!P.PathProgress(10,11,1),"growing remaining route is not progress");
foreach(float bad in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity}){
 Check(!P.PathProgress(bad,8,1),"invalid prior remaining "+bad);Check(!P.PathProgress(10,bad,1),"invalid remaining "+bad);Check(!P.PathProgress(10,8,bad),"invalid physical body delta "+bad);
}
// Once per held epoch/resource, with zero-input maintenance independent of hard Stop.
bool sameEpoch=false,sameResource=false;int hardStops=0,zeroMaintains=0;
for(int frame=0;frame<120;frame++){if(P.HoldNeedsStop(sameEpoch,sameResource))hardStops++;sameEpoch=true;sameResource=true;zeroMaintains++;}
Check(hardStops==1&&zeroMaintains==120,"blocked hold hard-stops once; native zero intent can be consumed every update");
Check(P.HoldNeedsStop(false,true)&&P.HoldNeedsStop(true,false),"new writer epoch or foreign native resource requires one clean stop");
// Full lifecycle: ordinary approach -> genuine acquired-entry failure -> staging ->
// new physical first-step debt -> cancellation during both return and replay.
for(int reset=0;reset<5;reset++){
 var old=new L.MissionState();var root=new L.StepState();var origin=new L.Identity("bot",2,27+reset,0,1,178,1,0,3+reset);
 var probe=new L.ProbeState(origin,root);Check(root.Attach(probe),"first WT attaches "+reset);
 Check(probe.PublishCompletion(origin,root,L.ActionOutcome.NativePhysical)==L.CommitResult.Applied,"first WT physically completes "+reset);
 Check(old.Resolve(root,origin,root,origin,L.Resolution.Executed,true,false)==L.CommitResult.Applied,"route advances to normal approach "+reset);
 bool fail=P.FailureAdmission(false,true,false,true);if(fail)old.RequestFullRetry();
 Check(old.Status!=L.Phase.Recovering&&old.Cursor==1,"normal low approach retains Vault chance "+reset);
 if(P.FailureAdmission(true,true,false,true))old.RequestFullRetry();Check(old.Status==L.Phase.Recovering,"real failure enters existing mission recovery "+reset);
 Check(!P.StageReached(-.5f,0,0,0,1,true),"path completion before full rear staging cannot restart "+reset);
 Check(P.StageReached(-1.2f,0,0,0,1,true),"actual aligned grounded staging allows fresh attempt "+reset);
 // Explicit teleport/manualRegroup retirement, the same production lifecycle method.
 old.Retire();Check(old.Retired&&!old.FinishedAtRetirement,"manual reset cancels without success "+reset);
 var staleStep=new L.StepState();Check(old.Resolve(staleStep,origin,staleStep,origin,L.Resolution.Subsumed,true,false)==L.CommitResult.Rejected,"old callback cannot revive cancelled route "+reset);
 var fresh=new L.MissionState();var freshStep=new L.StepState();Check(fresh.Cursor==0&&freshStep.DebtOutstanding,"new follow or demonstration starts with fresh debt "+reset);
 fresh.RequestFullRetry();fresh.Retire();Check(fresh.Retired&&!fresh.FinishedAtRetirement,"reset during replay also cancels "+reset);
}
File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=true,checks=checks.Count,cases=checks,dllSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(P).Assembly.Location))),physicsSimulated=false,recordedSamplesAndProductionPolicyLifecycle=true},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"PASS {checks.Count} recovery regression/continuous-policy/lifecycle checks");
