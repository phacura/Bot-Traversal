using System.Text.Json;
using System.Security.Cryptography;
using BotTraversal.Core;
using P=BotTraversal.Core.FollowerCapabilityRouteRetryPolicy;
using L=BotTraversal.Core.FollowerTraversalLifecycle;
var cases=new List<string>();
void Check(bool value,string label){if(!value)throw new Exception(label);cases.Add(label);}
Check(P.StageReached(-1.2f,.02f,.04f,.1f,1,true),"actual first-entry rear staging qualifies");
foreach(float forward in new[]{-.9999f,0f,1f,-2f,float.NaN,float.PositiveInfinity,float.NegativeInfinity})
 Check(!P.StageReached(forward,0,0,0,1,true),"at least 1m physically behind, bounded local staging "+forward);
foreach(float side in new[]{-.1f,.181f,1f,float.NaN,float.PositiveInfinity})Check(!P.StageReached(-1.2f,side,0,0,1,true),"rear normal rejects lateral displacement "+side);
foreach(float h in new[]{-.1f,.301f,2f,float.NaN,float.PositiveInfinity})Check(!P.StageReached(-1.2f,0,h,0,1,true),"actual support height required "+h);
foreach(float v in new[]{-.1f,.351f,5f,float.NaN,float.PositiveInfinity})Check(!P.StageReached(-1.2f,0,0,v,1,true),"stop before replay "+v);
foreach(float f in new[]{0f,.990267f,-1f,float.NaN,float.NegativeInfinity})Check(!P.StageReached(-1.2f,0,0,0,f,true),"align before replay "+f);
Check(!P.StageReached(-1.2f,0,0,0,1,false),"airborne arrival cannot restart");
Check(P.StagePosition(-1.2f,0,0,true)&&!P.StageReached(-1.2f,0,0,0,0,true),"arrival position alone grants turning, no replay");
foreach(float f in new[]{-1.5f,-1.4f,-1.2f,-1.0f})foreach(float side in new[]{0f,.1f,.18f})foreach(float angle in new[]{0f,4f,7.9f})
 Check(P.StageReached(f,side,0,.1f,(float)Math.Cos(angle*Math.PI/180),true),"valid local rear pose "+f+"/"+side+"/"+angle);
// Recorded RC111 Vault136 body drops from Y5.99 to Y4.1; do not chase Y6.13 entry.
foreach(float bad in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity}){
}
// Recorded speed7.620 heading15deg vs a 6.94m Jump. Looking forward is insufficient.
// The candidate's actual lifecycle transition methods, not a copied recovery simulator.
L.Identity Id(int mission,int step=0)=>new("test-bot",2,mission,step,12,206,12,0,77);
var old=new L.MissionState();var first=new L.StepState();var probe=new L.ProbeState(Id(98),first);
Check(first.Attach(probe),"old route owns first probe");
Check(probe.PublishCompletion(Id(98),first,L.ActionOutcome.NativePhysical)==L.CommitResult.Applied,"physical old WT can complete");
Check(old.Resolve(first,Id(98),first,Id(98),L.Resolution.Executed,true,false)==L.CommitResult.Applied&&old.Cursor==1,"old first WT consumed before Vault failure");
var failed=new L.StepState();var failedId=new L.Identity("test-bot",2,98,1,16,136,14,0,77);var native=new L.ProbeState(failedId,failed);
Check(failed.Attach(native),"next Vault is outstanding");old.RequestFullRetry();
Check(old.Status==L.Phase.Recovering,"failure belongs to same mission recovery phase");
Check(native.PublishCompletion(failedId,failed,L.ActionOutcome.NativePhysical)==L.CommitResult.Applied,"late callback may exist independently");
Check(old.Resolve(failed,failedId,failed,failedId,L.Resolution.Executed,true,true)==L.CommitResult.Rejected&&old.Cursor==1,"late old completion cannot advance recovering mission");
Check(!old.CanHandoffToFollow(3,false,true),"recovery debt prevents ordinary Follow handoff");
native.Retire();old.Retire();var fresh=new L.MissionState();var replayRoot=new L.StepState();var replayId=Id(99);
Check(fresh.Cursor==0&&replayRoot.DebtOutstanding&&replayRoot.Probe==null,"new retry physically owes old completed first WT again");
Check(fresh.Resolve(replayRoot,replayId,first,Id(98),L.Resolution.Subsumed,true,false)==L.CommitResult.Rejected,"old completed step instance and mission cannot skip replay root");
Check(fresh.Resolve(replayRoot,replayId,replayRoot,Id(98),L.Resolution.Subsumed,true,false)==L.CommitResult.Rejected,"matching edge/episode alone cannot reuse old mission proof");
var physical=new L.ProbeState(replayId,replayRoot);Check(replayRoot.Attach(physical),"replayed root binds fresh physical attempt");
Check(physical.PublishCompletion(replayId,replayRoot,L.ActionOutcome.NativePhysical)==L.CommitResult.Applied,"fresh WT completion evidence");
Check(fresh.Resolve(replayRoot,replayId,replayRoot,replayId,L.Resolution.Executed,true,false)==L.CommitResult.Applied&&fresh.Cursor==1,"only current actual WT advances new whole route");
var manual=new L.MissionState();manual.RequestFullRetry();manual.CancelFullRetry();Check(manual.Status==L.Phase.Ready,"manual abort can release pending recovery");manual.Retire();manual.RequestFullRetry();Check(manual.Retired&&manual.Status==L.Phase.Ready,"retired mission cannot resurrect retry");
File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=true,checks=cases.Count,cases,dllSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(P).Assembly.Location))),physicsSimulated=false},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS {cases.Count} actual DLL route-retry policy/lifecycle/recorded-boundary checks");
