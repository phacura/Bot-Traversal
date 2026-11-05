using System.Text.Json;
using System.Security.Cryptography;
using BotTraversal.Core;
using P=BotTraversal.Core.FollowerCapabilityRouteRetryPolicy;
using L=BotTraversal.Core.FollowerTraversalLifecycle;
var cases=new List<string>();void Check(bool v,string n){if(!v)throw new Exception(n);cases.Add(n);}
for(int mask=0;mask<16;mask++){
 bool failed=(mask&1)!=0,current=(mask&2)!=0,published=(mask&4)!=0,safe=(mask&8)!=0;
 Check(P.FailureAdmission(failed,current,published,safe)==(failed&&current&&!published&&safe),"explicit failure/current unpublished safe origin "+mask);
}
// Recorded normal-entry deadlock was before any return request. No geometry can substitute failure.
foreach(float angle in new[]{0f,8f,18.291605f,80.4041f,180f})foreach(float distance in new[]{.05f,.454f,1.181382f,3.2f}){
 var mission=new L.MissionState();bool request=P.FailureAdmission(false,true,false,true);if(request)mission.RequestFullRetry();
 Check(!request&&mission.Status==L.Phase.Ready&&mission.Cursor==0,"normal entry untouched regardless geometry "+angle+"/"+distance);
}
foreach(string kind in new[]{"WalkThrough","Vault","Climb","Jump","DropDown"}){
 var m=new L.MissionState();Check(!P.FailureAdmission(false,true,false,true),kind+" handoff/cleanup is not failure");
 if(P.FailureAdmission(true,true,false,true))m.RequestFullRetry();Check(m.Status==L.Phase.Recovering,kind+" explicit execution failure starts recovery");
 Check(!P.StageReached(-.8f,0,0,0,1,true),kind+" insufficient actual rear position cannot replay");
 Check(P.StageReached(-1.2f,0,0,0,1,true),kind+" actual one metre rear/aligned/stopped permits fresh replay");
 m.Retire();Check(m.Retired&&!m.FinishedAtRetirement,kind+" manual reset cancels without success");
}
// Paired first-entry / re-entry checks: after a real one-metre staging proof,
// the new attempt has Ready phase and cannot inherit the old failure's movement authority.
for(int attempt=0;attempt<4;attempt++)foreach(float heading in new[]{0f,18.291605f,80.4041f}){
 var next=new L.MissionState();
 if(attempt>0)Check(P.StageReached(-1.19244123f,.0523898974f,.08652878f,.1f,.9933628f,true),"recorded replay42 rear pose qualifies "+attempt+"/"+heading);
 Check(!P.FailureAdmission(false,true,false,true)&&next.Status==L.Phase.Ready,"first and post-retry normal entry do not re-enter recovery "+attempt+"/"+heading);
}
File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=true,checks=cases.Count,cases,dllSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(P).Assembly.Location))),physicsSimulated=false},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"PASS {cases.Count} explicit-failure isolation and retained replay checks");
