using System.Text.Json;
using System.Security.Cryptography;
using BotTraversal.Core;
using P=BotTraversal.Core.FollowerCapabilityApproachPolicy;
using R=BotTraversal.Core.FollowerCapabilityRouteRetryPolicy;
var cases=new List<string>();void Check(bool v,string n){if(!v)throw new Exception(n);cases.Add(n);}
// Recorded WT->Vault: accepted lane must keep a positive forward target instead of zero-length lateral chase.
foreach(float forward in new[]{-1.4f,-1.2f,-.847f,-.45f,-.2f,-.0325f})foreach(float lateral in new[]{0f,.032f,.2247f,.36f}){
 float lead=P.VaultLead(forward,lateral,.36f);
 Check(lead>0&&lead<=.45f&&lead<=-forward,"Vault lane forward without passing native plane "+forward+"/"+lateral);
 Check(!P.ForwardSample(lead,0,lateral),"coincident or sideways sampled target refused "+forward+"/"+lateral);
 Check(P.ForwardSample(lead,lead,.01f),"positive axis sample kept "+forward+"/"+lateral);
}
foreach(float forward in new[]{-2f,0f,.1f,float.NaN,float.NegativeInfinity})Check(P.VaultLead(forward,.1f,.36f)==0,"outside bounded lane "+forward);
Check(P.VaultLead(-.8f,.37f,.36f)==0,"outside native lateral lane keeps old correction");
Check(!P.ForwardSample(.32f,.20f,.01f)&&!P.ForwardSample(.32f,.32f,.081f),"nav sample may not discard lead or add side turn");
foreach(float speed in new[]{0f,3.2279f,4.76353f,5.62607f,5.67607f,6f,float.NaN}){
 bool expected=speed>=5.67607f;
 Check(P.SprintFloor(speed,2,5.67607f)==expected,"recorded Sprint final consumer speed "+speed);
}
Check(P.SprintFloor(3.1f,0,0),"missing follower evidence adds no invented minimum");
Check(!P.SprintFloor(float.PositiveInfinity,0,0),"nonfinite measured speed refused");
Check(!R.PrecisionReturn(false,2.201f)&&R.PrecisionReturn(false,2.2f),"walk begins before 1.4m former switching distance");
Check(R.PrecisionReturn(true,3.5f),"overshoot or rejected corridor cannot restore sprint");
Check(R.PrecisionReturn(false,1.05384f),"actual successful return within precision region");
foreach(float progress in new[]{-.04f,0f,.17f})Check(P.LowStep(true,true,true,true,true,true,.29f,.99f,.18f,progress),"same low-sill contract "+progress);
for(int failure=0;failure<6;failure++){
 var flags=Enumerable.Repeat(true,6).ToArray();flags[failure]=false;
 Check(!P.LowStep(flags[0],flags[1],flags[2],flags[3],flags[4],flags[5],.29f,.99f,.18f,0),"low-step prerequisite missing "+failure);
}
Check(!P.LowStep(true,true,true,true,true,true,.279f,.99f,.18f,0),"no premature step before measured stall");
foreach(float height in new[]{-.081f,.381f,float.NaN,float.PositiveInfinity})Check(!P.LowStep(true,true,true,true,true,true,.3f,.99f,height,0),"unsupported or too-high crossing refused "+height);
Check(!P.LowStep(true,true,true,true,true,true,.3f,.94f,.18f,0),"wrong body direction cannot climb sideways");
Check(!P.LowStep(true,true,true,true,true,true,.3f,.99f,.18f,.18f),"completed physical step not replayed");
Check(!P.HorizontalProgress(.25f,0,.12f,.05f),"vertical oscillation or lateral drift cannot renew forward lease");
Check(!P.HorizontalProgress(.25f,-.1f,.12f,.05f),"backward motion is not progress");
Check(P.HorizontalProgress(.2f,.18f,.12f,.05f),"real forward movement renews progress");
Check(P.StepSupport(true,false,0,0),"initial step requires observed grounded body");
Check(!P.StepSupport(false,false,0,0),"no airborne step bootstrap");
Check(P.StepSupport(false,true,.1f,.07f),"active collision step may temporarily lift off support");
Check(!P.StepSupport(false,true,.48f,.07f),"owned lift cannot renew its action lifetime");
Check(!P.StepSupport(false,true,.1f,.38f),"owned lift stays within original WT lift cap");
Check(!P.StepSupport(false,true,.1f,-.081f),"step cannot drive uncontrolled falling");
Check(!P.StepSupport(false,true,float.NaN,0),"invalid active age refuses");
Check(!P.StepSupport(false,true,.1f,float.NaN),"invalid active height refuses");
File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=true,checks=cases.Count,cases,dllSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(P).Assembly.Location))),physicsSimulated=false},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS {cases.Count} RC116 actual DLL recorded-geometry/gait/jump/low-step checks");
