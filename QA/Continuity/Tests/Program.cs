using System.Text.Json;
using BotTraversal.Core;
using D=BotTraversal.Core.FollowerCapabilityLocomotionDriver;
using N=BotTraversal.Core.FollowerCapabilityLocomotionPolicy;
using I=BotTraversal.Core.FollowerCapabilityNativeInputPolicy;
using G=BotTraversal.Core.FollowerCapabilityFollowGaitPolicy;
using L=BotTraversal.Core.FollowerTraversalLifecycle;
var cases=new List<string>();
void Check(bool ok,string name){if(!ok)throw new Exception(name);cases.Add(name);}
var p=new Consumer();
// Native Sprint clears Direction after every update. Two updates may share frame 10.
for(int update=0;update<4;update++){
 Check(D.Commit(p)==D.Receipt.Preserved,"current Run/Sprint accepts update "+update);
 Check(p.Direction==1&&p.Inputs==update+1,"input refreshed before EVERY native consumption "+update);
 p.NativeConsume();
}
Check(p.Frame==10&&p.Plays==0&&p.Consumed==4,"same rendered frame never substitutes for four native consumers");
p=new(){Pending=true};Check(D.Commit(p)==D.Receipt.Preserved&&p.Inputs==1,"queued animation receipt cannot suppress input consumption");
p=new(){Locomotion=false,Idle=false};Check(D.Commit(p)==D.Receipt.AwaitingCallback&&p.Inputs==0&&p.Plays==0,"recorded Transition INERT false awaits native graph without Play");
p=new(){Locomotion=false,Idle=true};Check(D.Commit(p)==D.Receipt.NativeInputRequested&&p.Inputs==1&&p.Plays==0,"Idle activates naturally even with committed outgoing stop");
p=new(){X=0,Y=0};Check(D.Commit(p)==D.Receipt.AwaitingInput&&p.Inputs==0&&p.Plays==0,"intentional zero input remains a stop");
p=new(){CanWalk=false};Check(D.Commit(p)==D.Receipt.Rejected&&p.Inputs==0,"CanWalk block cannot be bypassed");
p=new(){Accept=false};Check(D.Commit(p)==D.Receipt.Rejected&&p.Plays==0,"rejected native gateway cannot fall back to playback");
p=new(){Current=false};Check(D.Commit(p)==D.Receipt.Rejected&&p.Inputs==0,"stale writer identity cannot publish");
p=new(){ReplaceAfterCapture=true};Check(D.Commit(p)==D.Receipt.Rejected&&p.Inputs==0,"replaced actor fails second identity check");
foreach(var flag in new[]{"qualified","ground","allowed","override","teleport","survival","animator"}){
 p=new(){Blocked=flag};Check(D.Commit(p)==D.Receipt.Rejected&&p.Inputs==0,"native safe boundary rejects "+flag);
}
foreach(float bad in new[]{float.NaN,float.NegativeInfinity,float.PositiveInfinity,2f,-2f}){
 p=new(){Y=bad};Check(D.Commit(p)==D.Receipt.Rejected&&p.Plays==0,"invalid native input rejects "+bad);
}
Check(I.Evaluate(new(true,true,true,0,1,true))==I.Action.Reject,"ambiguous native consumer rejected");
foreach(bool start in new[]{false,true}){
 bool sprint=start;
 foreach(float distance in new[]{15.027f,15.296f,14.994f,15.188f,15.065f,15.3f}){
  sprint=G.Select(distance>15,sprint,true,true,distance,12);
  Check(sprint==start,"recorded terminal Follow jitter preserves current gait "+start+":"+distance);
 }
}
Check(G.Select(false,false,true,true,16,12),"enter sprint at actual threshold plus one");
Check(!G.Select(true,true,true,true,13,12),"leave sprint at actual threshold minus two");
Check(!G.Select(true,true,true,false,20,12),"native sprint safety always wins eligible Follow");
Check(!G.Select(false,true,false,true,20,12),"combat manual medical route contexts retain original requested false");
Check(G.Select(true,false,false,false,2,12),"ineligible contexts are not intercepted");
foreach(float config in new[]{8f,12f,30f}){
 float threshold=Math.Min(config+3,16);
 Check(G.Select(false,false,true,true,threshold+1,config),"configured enter boundary "+config);
 Check(!G.Select(true,true,true,true,threshold-2,config),"configured exit boundary "+config);
 Check(G.Select(false,true,true,true,threshold,config),"configured hysteresis keeps Sprint "+config);
 Check(!G.Select(true,false,true,true,threshold,config),"configured hysteresis keeps Run "+config);
}
foreach(float bad in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity,-1f})Check(G.Select(true,false,true,true,bad,12),"invalid follow observation preserves requested intent "+bad);
File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=true,checks=cases.Count,cases,physicsSimulated=false,nativeConsumerContractModel=true},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS {cases.Count} candidate DLL native-consumer and Follow gait checks");
class Consumer:ICapabilityLocomotionPort,ICapabilityNativeInputPort{
 internal int Inputs,Plays,Consumed,Frame=10;internal float Direction,X,Y=1;
 internal bool Locomotion=true,Idle,Pending,CanWalk=true,Accept=true,Current=true,ReplaceAfterCapture;
 internal string Blocked="";
 internal L.Identity Id=new("Ghost",2,140,3,28,794238,24,4,19);
 public N.Snapshot Capture(){var s=new N.Snapshot(Id,Id,Blocked!="qualified",Blocked!="ground",Blocked!="allowed",Blocked=="override",Blocked=="teleport",Blocked=="survival",Blocked!="animator",Locomotion,Pending);if(ReplaceAfterCapture)Current=false;return s;}
 public bool IsCurrent(L.Identity id)=>Current&&id.Equals(Id);
 public I.Snapshot CaptureInput()=>new(Current,Idle,CanWalk,X,Y,Locomotion);
 public bool RequestInput(I.Snapshot s){if(!Accept)return false;Inputs++;Direction=s.Y;return true;}
 // Deliberately not an interface callback: any driver fallback must be caught in IL too.
 public bool RequestNativeRun(){Plays++;return true;}
 internal void NativeConsume(){if(Direction==1)Consumed++;Direction=0;}
}
