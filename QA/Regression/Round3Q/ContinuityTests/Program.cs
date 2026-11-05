using System.Text.Json;
using BotTraversal.Core;
using P=BotTraversal.Core.FollowerCapabilityChainContinuityPolicy;
var checks=new List<string>();void Check(bool ok,string label){if(!ok)throw new Exception("FAIL "+label);checks.Add(label);}
DropDownBoundarySnapshot S(DropDownConsumerPhase phase=DropDownConsumerPhase.Execute,bool drop=true,bool attempted=true,bool unowned=true,float progress=1.1f,float required=.8853006f,float ahead=.55f,float side=.046f,float height=.442f,float aboveExit=1.8f,float requested=.09f,float trigger=.55f)=>new(phase,drop,attempted,unowned,progress,required,ahead,side,height,aboveExit,requested,trigger);
float B(DropDownBoundarySnapshot? s=null,float dt=.02f,float stable=.3f,float vertical=2.5f)=>P.LandingBudget(s??S(),dt,stable,vertical);
Check(!FollowerCapabilityDropDownTransitionPolicy.Plan(S()).Active,"old hard frontier is still late while body is .442m above successor entry");
Check(B()>0&&B()<.04f,"recorded late brake has an earlier restrictive native delta budget");
Check(B(S(ahead:.0257221f,height:.150724f))<=FollowerCapabilityDropDownTransitionPolicy.Plan(S(ahead:.0257221f,height:.150724f)).ForwardBudget,"new rolling budget never exceeds existing near-side hard border");
Check(B(S(requested:.001f))==.001f,"small native input cannot be increased by the continuity budget");
Check(B(S(ahead:.025f))==0,"original 2.5cm near-side margin remains closed");
Check(B(S(ahead:-.016f))<0,"past-entry body is never rewound");
Check(B(S(ahead:1.551f))<0,"unrelated distant successor cannot constrain current action");
Check(B(S(progress:.884f))<0,"current far-side progress cannot be borrowed from successor");
Check(B(S(unowned:false))<0,"owned successor cannot be controlled by predecessor");
Check(B(S(attempted:false))<0,"pre-native attempt cannot constrain through active-drop policy");
Check(B(S(drop:false))<0,"Jump motion and gravity never borrow DropDown budget");
foreach(var phase in new[]{DropDownConsumerPhase.None,DropDownConsumerPhase.Validate,DropDownConsumerPhase.Commit})Check(B(S(phase:phase))<0,"new rolling restriction stays in actual native drop phase "+phase);
Check(B(S(side:.201f))<0,"different lateral corridor retains existing ownership");
Check(B(S(height:1.351f))<0,"other vertical layer is not controlled");
Check(B(S(aboveExit:.399f))<0,"successor without meaningful remaining fall cannot reserve a runway");
Check(B(S(requested:-.1f))<0,"backward native request is never converted to forward motion");
Check(B(S(required:.19f))<0,"invalid current proof geometry is rejected");
Check(B(dt:0)<0&&B(dt:-.01f)<0&&B(dt:.101f)<0,"invalid or stalled physics frame cannot mint a motion budget");
Check(B(stable:.299f)<0,"original .30s proof duration cannot be shortened");
Check(B(vertical:-1)<0,"negative absolute fall speed is invalid");
foreach(float dt in new[]{.01f,.02f,.05f}){
 float ahead=.55f;int frames=(int)Math.Round(.3f/dt);
 for(int i=0;i<frames;i++){
  var sample=S(ahead:ahead,height:0,progress:1.1f+(.55f-ahead),requested:5f*dt);
  float move=B(sample,dt:dt,vertical:0);Check(move>=0&&move<=5f*dt&&move<=Math.Max(0,ahead-.025f),"bounded modeled delta frame "+dt+"/"+i);ahead-=move;
 }
 Check(ahead>.15f&&ahead<.25f,"simple displacement replay retains runway through unchanged .30s proof "+dt);
 Check(B(S(ahead:ahead,height:0),dt:dt,vertical:0)/dt>.4f,"modeled completion still has forward budget rather than zero border "+dt);
}
bool Resume(bool current=true,bool executed=true,bool pending=true,bool grounded=true,bool idle=true,float ahead=.7f,float side=.05f,float height=.22f,float heading=6)=>P.Resume(current,executed,pending,grounded,idle,ahead,side,height,heading,20);
Check(Resume(height:.7123728f),"recorded post-Jump Idle successor can request existing native Run handoff");
Check(!Resume(current:false),"foreign identity cannot prepare locomotion");
Check(!Resume(executed:false),"unproven or subsumed predecessor cannot qualify new resume");
Check(!Resume(pending:false),"active successor retains native control");
Check(!Resume(grounded:false),"airborne state cannot be changed to Run");
Check(!Resume(idle:false),"JumpLanding or Fall animation cannot be interrupted by new resume");
Check(!Resume(ahead:-.101f),"overrun entry cannot be borrowed for replay");
Check(!Resume(ahead:1.551f),"remote route cannot request successor Run");
Check(!Resume(side:.381f)&&!Resume(height:1.351f),"different corridor or vertical layer rejects resume");
Check(!Resume(heading:20.01f),"original native DropDown facing limit remains necessary");
foreach(float bad in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity}){
 Check(B(S(ahead:bad))<0,"nonfinite runway "+bad);
 Check(B(S(progress:bad))<0,"nonfinite own progress "+bad);
 Check(B(S(requested:bad))<0,"nonfinite requested motion "+bad);
 Check(B(dt:bad)<0,"nonfinite frame time "+bad);
 Check(B(stable:bad)<0,"nonfinite proof duration "+bad);
 Check(B(vertical:bad)<0,"nonfinite falling speed "+bad);
 Check(!Resume(heading:bad),"nonfinite native facing "+bad);
}
File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=true,checks=checks.Count,cases=checks,physicsSimulated=false,modeledDisplacementOnly=true},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"PASS {checks.Count} compiled chain continuity and boundary checks");
