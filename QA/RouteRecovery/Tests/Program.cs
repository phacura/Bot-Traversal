using System.Text.Json;
using System.Security.Cryptography;
using BotTraversal.Core;
using P=BotTraversal.Core.FollowerCapabilityRouteRetryPolicy;
using L=BotTraversal.Core.FollowerTraversalLifecycle;
var cases=new List<string>();void Check(bool v,string n){if(!v)throw new Exception(n);cases.Add(n);}
foreach(float angle in new[]{0f,1.78f,12f,18f,48.162f,110.76f,114.81588f,180f,float.NaN,float.PositiveInfinity})
 Check(P.BorrowHeading(angle,18f)==(!float.IsNaN(angle)&&angle>=0&&angle<=18),"actual Vault borrow angle "+angle);
foreach(float speed in new[]{0f,2.89f,3.89f,5.45f,8f,10f,35.2501373f,float.NaN})
 Check(P.UnsafeJumpSpeed(speed,5.45297956f)==(speed>Math.Max(8f,5.45297956f*1.75f+1f)),"bounded takeoff outlier "+speed);
foreach(float speed in new[]{2f,5.45f,35.25f})Check(!P.UnsafeJumpSpeed(speed,0),"missing demonstration is not fabricated overspeed "+speed);
foreach(float planar in new[]{0f,.05f,.5f,1.2f,1.5f,1.501f,3f,float.NaN})foreach(float vertical in new[]{0f,.4f,.8f,.801f,float.NaN})
 Check(P.ReturnSegment(planar,vertical)==(planar>=0&&planar<=1.5f&&vertical>=0&&vertical<=.8f),"grounded return segment "+planar+"/"+vertical);
foreach(float age in new[]{0f,1.9f,2f,3f,float.NaN})foreach(float deviation in new[]{0f,.65f,.651f,float.NaN})
 Check(P.ReturnLocal(age,deviation,.3f)==(age>=0&&age<2&&deviation<=.65f),"local measured progress bound "+age+"/"+deviation);
foreach(float distance in new[]{0f,.2f,.7f,1.4f,7f})Check(P.ReturnSpeed(distance)>=.16f&&P.ReturnSpeed(distance)<=.55f,"return has no Sprint request "+distance);
Check(P.ReturnSpeed(float.NaN)==0&&P.ReturnSpeed(-1)==0,"invalid return speed refuses");
foreach(string terminal in new[]{"missedJumpTakeoff","entryConvergenceFailed","unsafeJumpTakeoffSpeed","nativeDropDownTimeout","assistedReplayLandingFailed"})
 foreach(string attempt in new[]{"initial","replay"}){
  var mission=new L.MissionState();Check(mission.Status==L.Phase.Ready,terminal+attempt+" begins normal");
  Check(!P.FailureAdmission(false,true,false,true),terminal+attempt+" geometry alone stays normal");
  mission.RequestFullRetry();Check(mission.Status==L.Phase.Recovering&&mission.Cursor==0,terminal+attempt+" terminal keeps root debt");
  Check(!P.StageReached(-.8f,0,0,0,1,true),terminal+attempt+" must return whole metre");
  Check(P.StageReached(-1.43292522f,.1775098f,.00000334f,.1f,.99999994f,true),terminal+attempt+" recorded real replay pose accepted");
  mission.Retire();Check(mission.Retired&&!mission.FinishedAtRetirement,terminal+attempt+" cancel cannot mint success");
 }
// Concrete second-return failure: native route from upper ramp could not certify body continuity.
Check(P.ReturnSegment(.45f,.28f)&&P.ReturnSegment(.5f,.16f),"upper ramp reverse uses bounded descending segments");
Check(!P.ReturnSegment(3.8f,1.87f),"upper ramp cannot jump to projected NavMesh anchor");
Check(P.PathWithinBudget(7.32f,25.38f)&&!P.PathWithinBudget(205f,25.38f),"recorded root return cannot become long detour");
Check(!P.ReturnLocal(2f,.01f,.01f),"stuck reverse return does not refresh its own clock");
File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=true,checks=cases.Count,cases,dllSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(P).Assembly.Location))),physicsSimulated=false},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS {cases.Count} actual DLL return/terminal/heading checks");
