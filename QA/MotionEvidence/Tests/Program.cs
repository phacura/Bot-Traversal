using System.Text.Json;
using System.Security.Cryptography;
using BotTraversal.Core;
using P=BotTraversal.Core.FollowerCapabilityApproachPolicy;
using R=BotTraversal.Core.FollowerCapabilityRouteRetryPolicy;
var cases=new List<string>();void Check(bool v,string n){if(!v)throw new Exception(n);cases.Add(n);}
bool Accept(float raw,float measured=2.86f,float duration=.069397f,float age=0,float dot=.999f,bool current=true,bool grounded=true,bool run=true,float demo=5.45297956f)=>P.ReconcileJumpVelocity(current,grounded,run,raw,measured,duration,age,dot,demo);
foreach(float raw in new[]{14.7377625f,16.5659122f,18.491024f,18.0303459f}){
 Check(R.UnsafeJumpSpeed(raw,5.45297956f),"recorded raw speed independently unsafe "+raw);
 Check(Accept(raw),"recorded raw value can reconcile only with fresh measured Run "+raw);
 Check(!Accept(raw,raw),"true measured overspeed still rejected "+raw);
}
foreach(var sample in new[]{(14.7377625f,2.86465285f,.069397f),(16.5659122f,2.86084617f,.063559f),(18.491024f,2.89779601f,.074342f)})
 Check(Accept(sample.Item1,sample.Item2,sample.Item3),"three recorded full body intervals reconcile native spikes "+sample.Item1);
Check(!Accept(18.0303459f,12.1591783f,.017469f),"fourth short body burst cannot be accepted as fresh stable Run");
foreach(float v in new[]{1.1f,2.1554f,2.77940321f,2.9231f,3f})Check(Accept(18.49f,v),"walking-mode Run with physical momentum can use observation "+v);
foreach(float v in new[]{0f,.8233013f,1.06019783f,1.099f,float.NaN,float.PositiveInfinity,-1f})Check(!Accept(18.49f,v),"no manufactured momentum from stale slow or invalid reading "+v);
foreach(float v in new[]{0f,2.779f,7.9f,8f,float.NaN,float.PositiveInfinity})Check(!Accept(v),"ordinary native velocity left unchanged "+v);
foreach(float v in new[]{.0099f,.0399f,.121f,float.NaN,float.PositiveInfinity,-1f})Check(!Accept(18.49f,duration:v),"invalid measurement interval refused "+v);
foreach(float v in new[]{-.001f,.1201f,float.NaN,float.PositiveInfinity})Check(!Accept(18.49f,age:v),"stale or future sample refused "+v);
Check(Accept(18.49f,duration:.04f,age:.12f,dot:.94f),"inclusive sample boundaries");
Check(!Accept(18.49f,current:false)&&!Accept(18.49f,grounded:false)&&!Accept(18.49f,run:false),"retired origin airborne and Idle cannot borrow evidence");
Check(!Accept(18.49f,dot:.9399f)&&!Accept(18.49f,dot:-1)&&!Accept(18.49f,dot:float.NaN),"opposing or invalid native direction kept as veto");
Check(!Accept(10f,4.1f),"small discrepancy is not estimator spike");
Check(!Accept(18.49f,demo:0)&&!Accept(18.49f,demo:float.NaN),"missing edge reference cannot reinterpret unsafe speed");
foreach(float side in new[]{0f,.16f,.308956f,.445385f})Check(P.VaultPortalExcess(side,.75f,.55f)==0,"accepted feasible Vault slot does not chase centre "+side);
float portal=MathF.Sqrt(.55f*.55f-.30f*.30f);
Check(MathF.Abs(portal-.460977f)<.000002f,"native .55 circle with .30 forward reserve yields feasible .461 half width");
foreach(float side in new[]{.482465f,.63036263f,.75f,1f}){
 float excess=P.VaultPortalExcess(side,.75f,.55f),correction=P.VaultSideStep(excess);
 Check(excess>0&&correction<=.18f&&correction<=excess,"only outside portal excess gets bounded correction "+side);
 Check(MathF.Abs(side-excess-portal)<.00001f,"correction targets nearest admissible slot rather than centre "+side);
}
Check(P.VaultSideStep(P.VaultPortalExcess(.482465f,.75f,.55f))<.022f,"second recorded Vault needs ~2cm rather than 18cm correction");
Check(MathF.Sqrt(.30f*.30f+portal*portal)<=.550001f,"forward portal remains reachable by unchanged Euclidean native trigger");
Check(MathF.Abs(P.VaultPortalExcess(.30f,.16f,.55f)-.14f)<.000001f,"narrow obstacle still retains original lateral envelope");
foreach(float side in new[]{.16f,.445385f,.482465f,.63f}){
 float c=P.VaultSideStep(P.VaultPortalExcess(side,.75f,.55f));
 Check(c<=P.VaultSideStep(side),"new portal correction never exceeds old centering "+side);
}
Check(P.RotateVelocityAnchor(.06f)&&!P.RotateVelocityAnchor(.0599f)&&!P.RotateVelocityAnchor(float.NaN),"passive observer rotates only after real elapsed .06s");
Check(P.PreviousVelocityAnchor(.017f)&&!P.PreviousVelocityAnchor(.04f)&&!P.PreviousVelocityAnchor(-1),"short frame must include prior body anchor");
foreach(float now in new[]{.069397f,.074342f,.017059f,.017469f}){
 float latest=now<.04f?0f:.06f,previous=now<.04f?-.06f:0f;
 float age=now-latest;float start=P.PreviousVelocityAnchor(age)?previous:latest;
 float duration=now-start;Check(duration>=.04f&&duration<=.12f,"frame alias retains bounded full physical interval "+now);
}
// Reproduce positions/times of the first rejected frame; this is evidence arithmetic, not Unity simulation.
float distance=MathF.Sqrt(MathF.Pow(466.558533f-466.757019f,2)+MathF.Pow(-142.254486f+142.265625f,2));
float actual=distance/(468.483032f-468.413635f);
Check(actual>2.85f&&actual<2.88f&&Accept(14.7377625f,actual),"468.413-468.483 recorded body interval disproves raw14.74 overspeed");
File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=true,checks=cases.Count,cases,dllSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(P).Assembly.Location))),physicsSimulated=false},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS {cases.Count} RC118 actual DLL velocity reconciliation and nearest Vault portal checks");
