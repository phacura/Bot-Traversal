using System.Text.Json;
using System.Security.Cryptography;
using BotTraversal.Core;
using F=BotTraversal.Core.FollowerCapabilityFailureSequence;
using S=BotTraversal.Core.FollowerCapabilityFailureSequence.Stage;
using L=BotTraversal.Core.FollowerTraversalLifecycle;
var cases=new List<string>();void Check(bool b,string n){if(!b)throw new Exception(n);cases.Add(n);}
var stage=S.Native;foreach(var next in new[]{S.Alternate,S.SelfRescue,S.ReturnToEntry,S.RetryAlternate,S.RecoveryTeleport})
{stage=F.AfterFailure(stage);Check(stage==next,"one failure advances to "+next);}
Check(F.AfterFailure(stage)==S.RecoveryTeleport,"terminal failure cannot restart Native");
Check(F.AfterFailure((S)99)==S.RecoveryTeleport,"unknown future phase cannot bypass recovery review");
foreach(S s in Enum.GetValues<S>())Check(F.WalkingOnly(s)==(s==S.ReturnToEntry||s==S.RetryAlternate),"walking-only exact phases "+s);
for(int success=0;success<=20;success++)for(int failed=0;failed<=20;failed++)
{
 Check(F.Rank(success+1,failed,.5,1)>F.Rank(success,failed,.5,1),$"more real successes improve rank {success}/{failed}");
 Check(F.Rank(success,failed+1,.5,1)<F.Rank(success,failed,.5,1),$"more failures lower rank {success}/{failed}");
 Check(F.Rank(success,failed,.9,1)>F.Rank(success,failed,.1,1),$"learned evaluation affects rank {success}/{failed}");
}
Check(double.IsFinite(F.Rank(0,100,0,0)),"poor learned history remains ranked for self-rescue, never vetoed");
Check(F.Rank(5,0,.9,1)>F.Rank(0,5,.1,1),"better learned alternative wins equal geometry");
Check(F.Rank(5,1,.5,1)>F.Rank(5,1,.5,5),"shorter local alternative preferred at equal evidence");
Check(double.IsFinite(F.Rank(0,0,double.NaN,1)),"unrated action has finite exploratory prior");
Check(double.IsNegativeInfinity(F.Rank(1,1,.5,double.NaN)),"invalid route distance refused");
Check(F.ForwardCandidate(0,1,0,0),"current-body local rescue does not require reverse approach");
Check(!F.ForwardCandidate(-1,1,0,0),"alternate cannot return behind actual body");
Check(!F.ForwardCandidate(.1,1,1,.1),"completed physical prefix is not reopened");
Check(!F.ForwardCandidate(.1,.1,0,.1),"candidate must make forward progress");
foreach(double v in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity})
 Check(!F.ForwardCandidate(v,1,0,0)&&!F.ForwardCandidate(0,v,0,0)&&!F.ForwardCandidate(0,1,v,0),"nonfinite frontier "+v);
// Four actors share one real portal, while a different portal remains concurrent.
foreach(var d in new[]{.4,1.2,2.5,4.8})
{
 Check(FollowerPortalAdmission.Closer(d,"z",d+1,"a"),"physical distance wins profile ordering "+d);
 Check(!FollowerPortalAdmission.Closer(d+1,"a",d,"z"),"farther actor cannot take closer ticket "+d);
 Check(FollowerPortalAdmission.Closer(d,"a",d,"z"),"stable equal-distance profile ordering "+d);
}
Check(!FollowerPortalAdmission.Closer(double.NaN,"a",1,"z"),"invalid actor position cannot win nearest admission");
for(int raid=0;raid<25;raid++)
{
 var q=new FollowerPortalAdmission();var live=new Dictionary<string,(int mission,int step)>();
 foreach(var id in new[]{"nearest","second","third","fourth","other"})live[id]=(raid+1,0);
 bool Live(string b,int m,int s)=>live.TryGetValue(b,out var x)&&x==(m,s);
 bool Admit(string b,double now,bool nearest=true,double x=0)=>q.Admit(b,raid+1,live[b].step,x,0,0,1,0,now,nearest,Live);
 Check(!Admit("fourth",0,false),"callback order cannot admit farther follower "+raid);
 Check(Admit("nearest",0),"closest follower obtains exact ticket "+raid);
 Check(!Admit("second",0)&&!Admit("third",0)&&!Admit("fourth",0),"three followers wait behind owner "+raid);
 Check(Admit("nearest",.2,false),"owner retains lease even when no longer nearest "+raid);
 Check(Admit("other",.2,true,10),"independent portal remains parallel "+raid);
 q.Complete("nearest",raid+1,0,2);
 Check(!Admit("second",2.999999),"completion delay is at least one full second "+raid);
 Check(Admit("second",3),"second closest starts exactly after one second "+raid);
 q.Complete("nearest",raid+1,0,3);Check(!Admit("third",4),"duplicate old completion cannot release current owner "+raid);
 q.Complete("second",raid+1,0,4);Check(!Admit("third",4.9)&&Admit("third",5),"third follows second completion delay "+raid);
 q.Cancel("third",5);Check(!Admit("fourth",5.9)&&Admit("fourth",6),"cancelled or teleported actor releases admission "+raid);
 q.Clear();Check(Admit("second",6.01),"pass-through toggle or raid end clears queue state "+raid);
 q.Clear();live.Remove("nearest");
 Check(!q.Admit("nearest",raid+1,0,0,0,0,1,0,7,true,Live),"stale actor or cancelled route cannot get ticket "+raid);
 Check(!q.Admit("fourth",raid+1,0,double.NaN,0,0,1,0,7,true,Live),"invalid physical portal cannot get ticket "+raid);
 Check(!q.Admit("fourth",raid+1,0,0,0,0,0,0,7,true,Live),"invalid portal direction cannot get ticket "+raid);
}
// Replaying a suffix never awards a completed prefix again; cancellation retires old callbacks.
var id0=new L.Identity("b",1,1,0,1,1,1,0,1);var first=new L.StepState();var p0=new L.ProbeState(id0,first);var mission=new L.MissionState();
Check(first.Attach(p0),"WT prefix probe attached");
Check(p0.PublishCompletion(id0,first,L.ActionOutcome.NativePhysical)==L.CommitResult.Applied,"prefix completed by physical evidence");
Check(mission.Resolve(first,id0,first,id0,L.Resolution.Executed,true,true)==L.CommitResult.Applied,"completed WT cursor advances once");
p0.Retire();mission.RequestFullRetry();
Check(mission.Cursor==1&&first.PhysicallyResolved,"return request preserves completed WT semantic result");
var replay=new L.MissionState();Check(replay.Cursor==0,"new suffix has independent cursor, no false old-prefix success");
mission.Retire();Check(mission.Resolve(first,id0,first,id0,L.Resolution.Executed,true,true)==L.CommitResult.Rejected,"teleport cancellation rejects delayed old debt callback");
var rows=File.ReadLines(args[1]).Select(x=>JsonDocument.Parse(x).RootElement.Clone()).ToArray();
var summaries=rows.Where(x=>x.GetProperty("eventType").GetString()=="learningV322TraversalAttemptSummary").ToArray();
Check(rows.Length==58278,"four-bot trace frozen with all 58278 valid rows");
Check(summaries.Length==137,"actual 137 terminal summaries");
Check(summaries.Count(x=>x.GetProperty("payload").GetProperty("executionTruth").GetString()=="NativePhysical")==118,"118 recorded native physical completions");
Check(summaries.Select(x=>x.GetProperty("bot").GetProperty("profileId").GetString()).Distinct().Count()==4,"four actual bot identities retained in evidence");
var returns=rows.Where(x=>x.GetProperty("eventType").GetString()=="capabilityFullRouteRetryRequested").ToArray();
Check(returns.Length==4&&returns.All(x=>x.GetProperty("payload").GetProperty("failedStep").GetInt32()==2),"four old return requests failed at third step");
Check(returns.All(x=>x.GetProperty("payload").GetProperty("rootEdge").GetInt32()==226),"old behavior returned all four to completed WT root226");
File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=true,checks=cases.Count,cases,dllSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(F).Assembly.Location))),physicsSimulated=false},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS {cases.Count} RC121 sequence/ranking/four-bot queue/lifecycle/evidence checks");
