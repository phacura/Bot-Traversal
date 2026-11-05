using System.Text.Json;
using BotTraversal.Core;
using P=BotTraversal.Core.FollowerCapabilityHandoffPolicy;
var cases=new List<string>();void Check(bool ok,string label){if(!ok)throw new Exception("FAIL "+label);cases.Add(label);}
DropDownBoundarySnapshot S(float side=.186147243f,float ahead=.672327161f,float requested=.08f,DropDownConsumerPhase phase=DropDownConsumerPhase.Execute,bool unowned=true)=>new(phase,true,true,unowned,1.52403212f,.9536949f,ahead,side,.09469253f,1.8f,requested,.55f);
float B(float side=.186147243f,float limit=.38f)=>P.LandingBudget(S(side),.016427f,.3f,.6f,limit);
Check(B()>0,"recorded supported first drop keeps a positive bounded runway budget");
Check(B(.201f)>0&&B(.35f)>0&&B(.38f)>0,"new legacy corridor remains controlled across old .20 lateral cutoff");
Check(B(.381f)<0,"outside original .38 lateral corridor cannot be controlled");
Check(B(.39f,.39f)<0,"corridor beyond original .38 cannot be introduced");
Check(FollowerCapabilityChainContinuityPolicy.LandingBudget(S(.201f),.016427f,.3f,.6f)<0,"old Q public decision keeps its reviewed .20 envelope");
Check(B(.19f,.20f)==FollowerCapabilityChainContinuityPolicy.LandingBudget(S(.19f),.016427f,.3f,.6f),"old Q API delegates to same actual budget formula");
Check(P.LandingBudget(S(unowned:false),.016f,.3f,.6f,.38f)<0,"owned successor cannot inherit predecessor motion control");
foreach(var phase in new[]{DropDownConsumerPhase.None,DropDownConsumerPhase.Validate,DropDownConsumerPhase.Commit})Check(P.LandingBudget(S(phase:phase),.016f,.3f,.6f,.38f)<0,"rolling envelope retains original execute phase "+phase);
// Recorded 4240: subtraction reverses Z even though original motion asks -Z.
double x=-.06688778,z=-.0270013325,ax=-.8317221,az=-.555192232,budget=.009628012;
double forward=x*ax+z*az,removed=forward-budget;
double oldZ=z-az*removed;float factor=P.PlanarRetention((float)forward,(float)removed);
Check(oldZ>0&&z<0,"recorded old axis subtraction can reverse requested lateral component");
Check(factor>=0&&factor<=1&&Math.Abs((x*factor)*ax+(z*factor)*az-budget)<.000001,"recorded full planar scaling obeys forward budget");
Check(x*factor<=0&&z*factor<0,"recorded full planar scaling never reverses requested X or Z");
Check(Math.Abs(x*(z*factor)-z*(x*factor))<.00000001,"recorded scaled planar request stays collinear");
Check(Math.Sqrt(x*x+z*z)*factor<=Math.Sqrt(x*x+z*z),"recorded planar magnitude cannot increase");
foreach(float f in new[]{.001f,.02f,.07f,1f})foreach(float fraction in new[]{0f,.1f,.5f,1f,2f}){
 float retention=P.PlanarRetention(f,f*fraction);
 Check(retention>=0&&retention<=1,"retention is monotonic restriction "+f+"/"+fraction);
 Check(f*retention<=Math.Max(0f,f-f*fraction)+.000001f,"scaled projection consumes at most budget "+f+"/"+fraction);
}
bool Carry(bool current=true,bool physical=true,bool grounded=true,bool topology=true,bool sprint=true,bool overridden=false,float distance=22.5661449f,float settle=8)=>P.PreserveTerminalSprint(current,physical,grounded,topology,sprint,overridden,distance,settle);
Check(Carry(),"recorded terminal completed drop and distant complete Follow path preserve existing Sprint");
Check(!Carry(current:false),"foreign terminal identity cannot keep Sprint");
Check(!Carry(physical:false),"missing physical landing proof cannot keep Sprint");
Check(!Carry(grounded:false),"airborne terminal cannot keep Sprint");
Check(!Carry(topology:false),"unconfirmed topology cannot keep Sprint");
Check(!Carry(sprint:false),"terminal carry never starts a new Sprint");
Check(!Carry(overridden:true),"foreign controls retain priority");
Check(!Carry(distance:8)&&!Carry(distance:1),"close leader retains original slowdown");
Check(Carry(distance:8.001f),"existing chase settle boundary separates carry and slowdown");
Check(!Carry(settle:0),"invalid settle envelope is rejected");
for(int mask=0;mask<32;mask++)Check(Carry(current:(mask&1)!=0,physical:(mask&2)!=0,grounded:(mask&4)!=0,topology:(mask&8)!=0,sprint:(mask&16)!=0)==(mask==31),"terminal carry needs all five existing permissions "+mask);
foreach(float bad in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity}){
 Check(B(bad)<0,"nonfinite lateral "+bad);
 Check(B(limit:bad)<0,"nonfinite allowed corridor "+bad);
 Check(!Carry(distance:bad)&&!Carry(settle:bad),"nonfinite terminal distance "+bad);
 Check(P.PlanarRetention(bad,.1f)==1&&P.PlanarRetention(1,bad)==1,"invalid restriction cannot fabricate a direction "+bad);
}
File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=true,checks=cases.Count,cases,physicsSimulated=false,recordedVectorReplayOnly=true},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"PASS {cases.Count} compiled planar and terminal handoff checks");
