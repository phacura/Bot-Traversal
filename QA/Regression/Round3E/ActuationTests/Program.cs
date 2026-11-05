using A=BotTraversal.Core.FollowerCapabilityActuationPolicy;
using M=BotTraversal.Core.FollowerCapabilityMotionPolicy;
using G=BotTraversal.Core.FollowerCapabilityDeliveryPolicy;
using L=BotTraversal.Core.FollowerTraversalLifecycle;
using System.Text.Json;
var checks=new List<string>();void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);checks.Add(name);}
var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(args[0])!,"jump-and-native-drop-fixture.json"))).RootElement;
var j=fixture.GetProperty("jump");float F(string n)=>j.GetProperty(n).GetSingle();
A.JumpIntent Prepare(bool exact=true,bool goal=true,bool active=false,float? forward=null,float? lateral=null,float direction=1f,float? lead=null)=>A.PrepareJump(exact,goal,active,forward??F("forward"),lead??F("preparationLead"),F("maxOvershoot"),lateral??F("lateral"),F("maxLateral"),direction);
var sample=Prepare();Check(sample.AxisFacing&&sample.ForwardMotion,"recorded centered takeoff must retain portal facing AND forward motion");
var window=A.JumpWindow(F("forward"),.95f,.16f,F("lateral"),.36f,F("facing"),.955f,F("heading"),.94f);
Check(window.Inside&&window.Centered&&!window.Facing&&window.Heading,"recorded 0.929 facing remains below unchanged 0.955 native gate");
float goalAdvance=.16f-F("forward");float oldGoalFacing=goalAdvance/MathF.Sqrt(goalAdvance*goalAdvance+F("lateral")*F("lateral"));
Check(oldGoalFacing<.955f,"old short centerline goal itself requests a heading that fails native gate");
Check(Prepare(forward:-.0959911644f,lateral:.127945289f).ForwardMotion,"last pre-boundary frame keeps legal corridor without over-centering");
Check(Prepare(lateral:.42f).AxisFacing&&!Prepare(lateral:.42f).ForwardMotion,"outside legal centerline retains correction while facing portal");
Check(Prepare(lateral:.36f).ForwardMotion,"existing centerline boundary inclusive");
Check(!Prepare(exact:false).AxisFacing,"stale mission or successor cannot affect facing");
Check(!Prepare(goal:false).AxisFacing,"missing selected target cannot invent Jump intent");
Check(!Prepare(active:true).AxisFacing,"preparation cannot override live native probe");
Check(!Prepare(forward:.17f).AxisFacing,"missed takeoff keeps existing fallback, not wider admission");
Check(!Prepare(forward:-2f).AxisFacing,"far approach keeps its existing producer");
Check(!Prepare(lateral:.73f).AxisFacing,"wide out-of-corridor input not borrowed");
Check(!Prepare(direction:-.1f).AxisFacing,"backward intent cannot become unsolicited forward motion");
foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity}){
 Check(!Prepare(forward:invalid).AxisFacing,"invalid forward rejected "+invalid);
 Check(!Prepare(lateral:invalid).AxisFacing,"invalid lateral rejected "+invalid);
 Check(!Prepare(lead:invalid).AxisFacing,"invalid lead rejected "+invalid);
 Check(!Prepare(direction:invalid).AxisFacing,"invalid direction rejected "+invalid);
}
Check(A.RetiredPhysicalStrategy(1,1,100,100,100,"Jump","Jump"),"retired current strategy may still use existing Assist fallback");
Check(!A.RetiredPhysicalStrategy(2,1,100,100,100,"Jump","Jump"),"retirement cannot cross step epoch");
Check(!A.RetiredPhysicalStrategy(1,1,100,99,100,"Jump","Jump"),"retirement cannot cross semantic edge epoch");
Check(!A.RetiredPhysicalStrategy(1,1,100,100,99,"Jump","Jump"),"another retired edge cannot bypass native dispatch");
Check(!A.RetiredPhysicalStrategy(1,1,100,100,100,"Climb","Jump"),"another retired kind cannot bypass native dispatch");
Check(A.CanDispatchNativeJump(true,true,true,true,true,true,true,true),"legal native dispatch retains unchanged actuator checks");
Check(!A.CanDispatchNativeJump(false,true,true,true,true,true,true,true),"recorded local-entry bridge outside trigger horizon cannot acquire probe");
for(int n=0;n<7;n++){
 bool mode=true,ground=true,can=true,center=true,facing=true,heading=true,momentum=true;
 switch(n){case 0:mode=false;break;case 1:ground=false;break;case 2:can=false;break;case 3:center=false;break;case 4:facing=false;break;case 5:heading=false;break;case 6:momentum=false;break;}
 Check(!A.CanDispatchNativeJump(true,mode,ground,can,center,facing,heading,momentum),"native dispatch rejects unavailable physical dimension "+n);
}
Check(A.NativeDropConsumer(true,true,true,true,true,false,false),"admitted exact DropDown owns its native physical consumer");
for(int n=0;n<7;n++){
 bool native=true,attempt=true,current=true,mission=true,step=true,landing=false,far=false;
 switch(n){case 0:native=false;break;case 1:attempt=false;break;case 2:current=false;break;case 3:mission=false;break;case 4:step=false;break;case 5:landing=true;break;case 6:far=true;break;}
 Check(!A.NativeDropConsumer(native,attempt,current,mission,step,landing,far),"native consumer rejects invalid lifecycle/phase dimension "+n);
}
M.Plan Native(bool admitted=true,bool active=true,bool exact=true,bool walk=true,bool goal=true,bool moving=true,bool direction=true,bool retry=true,bool matching=false,float age=0f,float distance=2.24f,G.Capability kind=G.Capability.DropDown,bool grounded=true)=>
 M.Evaluate(new(new(kind,exact,active,grounded,walk,false,true,true,matching,retry,age,1.2f),goal,moving,direction,false,false,true,distance,5.5f,matching,admitted));
Check(Native().Kind==M.Delivery.PublishPhysicalWay,"recorded native DropDown gets executable path immediately, before 1.2s timeout");
Check(Native(grounded:false).Kind==M.Delivery.PublishPhysicalWay,"admitted native executor does not depend on NavMesh or grounded fallback proof");
Check(Native(admitted:false).Kind==M.Delivery.PreserveActuator,"arbitrary active probe remains exclusive");
Check(Native(active:false).Kind==M.Delivery.PreserveActuator,"native flag cannot invent an executing probe");
foreach(var k in new[]{G.Capability.Jump,G.Capability.Climb,G.Capability.Vault})Check(Native(kind:k).Kind==M.Delivery.PreserveActuator,"native DropDown path cannot bypass "+k);
Check(Native(exact:false).Kind==M.Delivery.Blocked,"foreign native execution blocked");
Check(Native(walk:false).Kind==M.Delivery.Blocked,"CanWalk false remains diagnosed and blocked");
Check(Native(goal:false).Kind==M.Delivery.PreserveActuator,"no frozen exit cannot publish a path");
Check(Native(moving:false).Kind==M.Delivery.PreserveActuator,"hold frame cannot publish a native path");
Check(Native(direction:false).Kind==M.Delivery.Blocked,"native goal and direction must agree");
Check(Native(retry:false).Kind==M.Delivery.Blocked,"existing move retry respected");
Check(Native(distance:6f).Kind==M.Delivery.Blocked,"existing local horizon respected");
Check(Native(matching:true,age:.1f).Kind==M.Delivery.KeepNativeWay,"native receipt kept without per-frame republish");
var stall=Native(matching:true,age:1.2f);Check(stall.Kind==M.Delivery.Blocked&&stall.KeepExistingConsumer,"existing native timeout not reset or turned into completion");
L.Identity Id(long writer=16)=>new("b",1,232,3,30,77,23,4,writer);var id=Id();
Check(M.CanRetainConsumer(id,id,true,true,true,true),"exact native executor may retain its own path");
Check(!M.CanRetainConsumer(id,id,true,true,true,false),"another probe cannot inherit path receipt");
Check(!M.CanRetainConsumer(id,Id(17),true,true,true,true),"native receipt cannot cross writer generation");
Check(!M.CanRetainConsumer(id,id,false,true,true,true),"replaced native path pointer invalidates execution receipt");
var debt=new L.StepState();Check(Native().Kind==M.Delivery.PublishPhysicalWay&&debt.DebtOutstanding,"native path installation never resolves DropDown debt");
// The extracted window must preserve strict facing, heading, lateral and longitudinal boundaries.
for(int n=0;n<4;n++){
 var w=A.JumpWindow(n==0?-.951f:0f,.95f,.16f,n==1?.361f:0f,.36f,n==2?.954f:1f,.955f,n==3?.939f:1f,.94f);
 Check(!(w.Inside&&w.Centered&&w.Facing&&w.Heading),"unchanged Jump readiness rejects dimension "+n);
}
File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=true,checks=checks.Count,cases=checks,physicsSimulated=false,recordedCanWalkUnknown=true},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"PASS {checks.Count} compiled actuation/heading/native-probe checks");
