using System.Text.Json;
using System.Security.Cryptography;
using BotTraversal.Core;
using P=BotTraversal.Core.FollowerCapabilityApproachPolicy;
var cases=new List<string>();void Check(bool v,string n){if(!v)throw new Exception(n);cases.Add(n);}
foreach(float speed in new[]{3.38523579f,3.83971f,3.90518379f,4.09581661f})
 Check(P.ColdRunTrial(true,false,true,true,true,-.3915889f,.95f,.04060617f,1,.9999343f,speed),"recorded first Run is legally trialable without previous success "+speed);
foreach(float speed in new[]{0f,.109743f,1.099f,float.NaN,float.PositiveInfinity})
 Check(!P.ColdRunTrial(true,false,true,true,true,-.3f,.95f,.04f,1,1,speed),"no stationary or invalid speed trial "+speed);
for(int i=0;i<5;i++){
 bool current=i!=0,consumed=i==1,run=i!=2,grounded=i!=3,jump=i!=4;
 Check(!P.ColdRunTrial(current,consumed,run,grounded,jump,-.3f,.95f,.04f,1,1,3),"trial identity/once/native-state prerequisite "+i);
}
Check(!P.ColdRunTrial(true,false,true,true,true,.161f,.95f,.04f,1,1,3),"no late jump beyond existing plane");
Check(!P.ColdRunTrial(true,false,true,true,true,-1f,.95f,.04f,1,1,3),"no early unbounded jump");
foreach(float lateral in new[]{.361f,1f,float.NaN})Check(!P.ColdRunTrial(true,false,true,true,true,-.3f,.95f,lateral,1,1,3),"sideways trial refused "+lateral);
Check(!P.ColdRunTrial(true,false,true,true,true,-.3f,.95f,.04f,.954f,1,3),"existing facing guard kept");
Check(!P.ColdRunTrial(true,false,true,true,true,-.3f,.95f,.04f,1,.939f,3),"existing measured heading guard kept");
Check(P.JumpContact(-.386f,.04f,1,.99f,3,.08f,0,1),"recorded reverse jump can take off before body hits obstacle");
Check(!P.JumpContact(-.56f,.04f,1,.99f,3,.08f,0,1),"contact does not create distant jump opportunity");
Check(!P.JumpContact(-.386f,.04f,1,.99f,3,.16f,0,1),"distant obstacle contact not enough");
Check(!P.JumpContact(-.386f,.04f,1,.99f,3,.08f,.8f,1),"ramp ground normal is not opposing wall");
Check(!P.JumpContact(-.386f,.04f,1,.99f,3,.08f,0,.3f),"grazing side contact not an entry blocker");
foreach(float speed in new[]{0f,1.099f,float.NaN})Check(!P.JumpContact(-.386f,.04f,1,.99f,speed,.08f,0,1),"contact cannot manufacture momentum "+speed);
foreach(float side in new[]{0f,.05f,.18f,.63036263f,.75f}){
 var lead=P.VaultSideStep(side);Check(lead>=0&&lead<=.18f&&lead<=side,"bounded Vault centering "+side);
}
Check(P.VaultSideStep(.63036263f)==.18f,"recorded .63m offset closes without radial heading");
Check(P.VaultSideStep(float.NaN)==0&&P.VaultSideStep(-1)==0,"invalid side step refuses");
// Reconstruct the actual first Vault frame: desired travel corrects side error; facing stays on native axis.
var axisX=-.9967f;var axisZ=.081f;var norm=MathF.Sqrt(axisX*axisX+axisZ*axisZ);axisX/=norm;axisZ/=norm;
var oldX=-.437693954f;var oldZ=.899124f;var oldDot=axisX*oldX+axisZ*oldZ;
Check(oldDot<.6f,"recorded radial anchor differs about59 degrees from native heading");
float f=.45f,l=.18f;var correctionAngle=MathF.Atan2(l,f)*180/MathF.PI;
Check(correctionAngle<22&&correctionAngle>21,"bounded world correction is distinct from held body facing");
Check(P.StalledJump(true,2,.149539f,.01354755f,0),"recorded silent hold becomes explicit failed preparation");
Check(!P.StalledJump(false,4,.149539f,.01354755f,0),"no intent is not failed execution");
Check(!P.StalledJump(true,1.999f,.149539f,.01354755f,0),"allow short preparation settle");
Check(!P.StalledJump(true,4,-2,.01354755f,0),"distant approach cannot trigger root replay");
Check(!P.StalledJump(true,4,.149539f,.01354755f,3),"moving legal run never treated as stall");
Check(!P.StalledJump(true,float.NaN,.149539f,.01354755f,0),"invalid clock refuses");
File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=true,checks=cases.Count,cases,dllSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(P).Assembly.Location))),physicsSimulated=false},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS {cases.Count} RC117 actual DLL cold-run/contact/Vault/stall checks");
