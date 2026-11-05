using System.Text.Json;
using System.Security.Cryptography;
using BotTraversal.Core;
using P=BotTraversal.Core.FollowerCapabilityHandoffPolicy;
using A=BotTraversal.Core.FollowerCapabilityAnimationContinuityPolicy;
using I=BotTraversal.Core.FollowerCapabilityNativeInputPolicy;
using D=BotTraversal.Core.FollowerCapabilityNativeInputDriver;
var cases=new List<string>();
void Check(bool ok,string why){if(!ok)throw new Exception(why);cases.Add(why);}
DropDownBoundarySnapshot S(float ahead=.3902811f,float lateral=.0363266431f,bool unowned=true,float request=.10f)=>new(DropDownConsumerPhase.Execute,true,true,unowned,1.6f,.8413824f,ahead,lateral,.003f,1.22f,request,.42f);
// Recorded RC110 first landing: proof candidate at 231.603, arrest at 231.821.
const float dt=.016f;float age=231.821f-231.603f;
float initial=P.RollingLandingBudget(S(),dt,.30f,.08f,.38f,0f,true);
float rolling=P.RollingLandingBudget(S(),dt,.30f,.08f,.38f,age,true);
Check(initial>0&&rolling>initial,"remaining proof clock prevents full-.30s budget restarting every frame");
Check(P.CanRollLandingProof(true,true,true,true,.3902811f,.0363266431f,rolling),"recorded entry .390m ahead rolls within the native frontier instead of StopMove");
Check(!P.CanRollLandingProof(true,true,true,true,.025f,.01f,.001f),"exhausted reserve requires old safety arrest");
Check(!P.CanRollLandingProof(true,true,true,true,.024f,.01f,.001f),"overrun cannot inherit positive rolling budget");
for(int mask=0;mask<16;mask++)Check(P.CanRollLandingProof((mask&1)!=0,(mask&2)!=0,(mask&4)!=0,(mask&8)!=0,.4f,.03f,.01f)==(mask==15),"all current/witness/contact/exact-successor permissions "+mask);
foreach(float bad in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity}){
 Check(!P.CanRollLandingProof(true,true,true,true,bad,.03f,.01f),"reject invalid ahead "+bad);
 Check(!P.CanRollLandingProof(true,true,true,true,.4f,bad,.01f),"reject invalid side "+bad);
 Check(!P.CanRollLandingProof(true,true,true,true,.4f,.03f,bad),"reject invalid budget "+bad);
 Check(P.RollingLandingBudget(S(),dt,.3f,.08f,.38f,bad,true)==P.LandingBudget(S(),dt,.3f,.08f,.38f),"invalid age keeps conservative old budget "+bad);
}
foreach(float step in new[]{.005f,.016f,.033f,.05f,.1f})foreach(float ahead in new[]{.026f,.04f,.15f,.3902811f,.8f,1.5f}){
 float position=ahead,original=ahead;int frame=0;
 for(float t=0;t<=.30f+.0001f;t+=step){
  float budget=P.RollingLandingBudget(S(position,request:.08f),step,.3f,.1f,.38f,t,true);
  Check(budget>=0f&&budget<=.08f+.00001f&&budget<=Math.Max(0f,position-.025f)+.00001f,"no frontier overrun "+step+"/"+ahead+"/"+(frame++));
  position-=budget;
 }
 Check(position>=.025f-.00001f&&position<=original,"trace preserves reserve across proof maturity "+step+"/"+ahead);
}
Check(P.RollingLandingBudget(S(unowned:false),dt,.3f,.08f,.38f,age,true)<0,"started successor cannot borrow predecessor runway");
Check(P.RollingLandingBudget(S(),.11f,.3f,.08f,.38f,age,true)<0,"long native frame declines rolling rather than guessing");
Check(P.RollingLandingBudget(S(),dt,.3f,.08f,.38f,age,false)==P.LandingBudget(S(),dt,.3f,.08f,.38f),"unstable contact retains conservative proof budget");
for(int mask=0;mask<128;mask++){
 bool j=(mask&1)!=0,d=(mask&2)!=0,attempt=(mask&4)!=0,proof=(mask&8)!=0,reject=(mask&16)!=0,partial=(mask&32)!=0,handoff=(mask&64)!=0;
 Check(A.LandingProofKeepsGait(j,d,attempt,proof,reject,partial,handoff)==((j!=d)&&attempt&&proof&&!reject&&!partial&&(j||handoff)),"qualified Jump/Drop gait proof matrix "+mask);
}
Check(A.LandingProofKeepsGait(true,false,true,true,false,false,false),"qualified Jump landing now preserves sprint intent before proof completion");
Check(!A.LandingProofKeepsGait(true,false,true,false,false,false,false),"airborne jump label grants no landing gait protection");
// Actual existing native input driver consumes Idle input without animation playback.
var port=new InputPort();
for(int i=0;i<4;i++){Check(D.Commit(port)==D.Receipt.InputRequested,"Idle delivered on each native update "+i);port.Consumed++;}
Check(port.Inputs==4&&port.Consumed==4,"four native evaluations share no invented frame delay");
port.Y=0;Check(D.Commit(port)==D.Receipt.AwaitingInput&&port.Inputs==4,"explicit stop is not turned into a moving request");
port.Y=1;port.Current=false;Check(D.Commit(port)==D.Receipt.Rejected&&port.Inputs==4,"superseded identity rejects input after current capture");
port.Current=true;port.Accept=false;Check(D.Commit(port)==D.Receipt.Rejected&&port.Inputs==4,"backend refusal has no animation fallback");
string hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(P).Assembly.Location)));
File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=true,checks=cases.Count,cases,dllSha256=hash,physicsSimulated=false,recordedBoundaryReplayOnly=true},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS {cases.Count} continuity policy/driver/recorded-boundary checks");
class InputPort:ICapabilityNativeInputPort {
 public bool Current=true,Accept=true;public float Y=1;public int Inputs,Consumed;
 public I.Snapshot CaptureInput()=>new(Current,true,true,0,Y,false);
 public bool RequestInput(I.Snapshot s){if(!Accept||!Current)return false;Inputs++;return true;}
}

