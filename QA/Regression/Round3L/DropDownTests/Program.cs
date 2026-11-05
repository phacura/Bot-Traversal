using System.Text.Json;
using BotTraversal.Core;
using P=BotTraversal.Core.FollowerCapabilityDropDownTransitionPolicy;
using F=BotTraversal.Core.FollowerCapabilityFrontierPolicy;
using Phase=BotTraversal.Core.DropDownConsumerPhase;
var checks=new List<string>();void Check(bool ok,string label){if(!ok)throw new Exception("FAIL "+label);checks.Add(label);}
bool Near(float a,float b)=>Math.Abs(a-b)<.00001f;
DropDownBoundaryPlan Plan(Phase phase=Phase.Execute,bool kind=true,bool native=true,bool unowned=true,float progress=1.2f,float required=.888273835f,float ahead=.15f,float lateral=.0054f,float vertical=.1245f,float above=1.2f,float requested=.08f,float trigger=.42f)=>P.Plan(new(phase,kind,native,unowned,progress,required,ahead,lateral,vertical,above,requested,trigger));
Check(Near(P.RequiredProjection(.1f,.6f),.20f),"original minimum far-side projection preserved");
Check(Near(P.RequiredProjection(1.4804564f,.6f),.888273835f),"original measured far-side ratio preserved");
foreach(var phase in new[]{Phase.Execute,Phase.Validate,Phase.Commit}){
 var p=Plan(phase);Check(p.Active&&Near(p.ForwardBudget,.125f),"one boundary owns current consumer through "+phase);
 Check(Near(F.ForwardRemoval(new(CapabilityFrontierKind.DropDownBoundary,.2f,p.ForwardBudget)),.075f),"actual native forward restriction follows phase budget "+phase);
}
Check(!Plan(Phase.None).Active,"retired or unrelated stage releases predecessor boundary");
Check(!Plan((Phase)999).Active,"unknown phase cannot acquire native boundary");
Check(!Plan(kind:false).Active,"Jump or Climb cannot borrow dropdown boundary");
Check(!Plan(native:false).Active,"no actual NativeDropDown attempt cannot acquire boundary");
Check(!Plan(unowned:false).Active,"started successor cannot be restrained by predecessor");
Check(!Plan(progress:.88f).Active,"predecessor near side retains original execution movement");
Check(Plan(progress:.888273835f).Active,"far-side movement boundary does not wait for landing candidate");
Check(!Plan(required:.19f).Active,"invalid required projection rejects boundary");
Check(!Plan(ahead:-.319086581f).Active,"recorded candidate already beyond entry is never rewound");
Check(!Plan(ahead:-.043596413f).Active,"recorded pre-candidate overrun cannot be called protected");
Check(Plan(ahead:.025f).ForwardBudget==0,"near-side plane holds only positive native forward motion");
Check(Plan(ahead:-.015f).Active&&Plan(ahead:-.015f).ForwardBudget==0,"original small entry tolerance cannot create reverse displacement");
Check(!Plan(ahead:-.01501f).Active,"entry tolerance is not widened");
Check(Plan(ahead:.5f,requested:.08f).Active,"one actual native request forecasts boundary crossing");
Check(!Plan(ahead:.501f,requested:.08f).Active,"forecast cannot invent a remote control horizon");
Check(!Plan(ahead:.43f,requested:-.08f).Active,"retreat request cannot expand forward forecast");
Check(!Plan(lateral:.2001f).Active,"adjacent lateral envelope is unchanged");
Check(!Plan(vertical:.3001f).Active,"wrong support level cannot borrow upper entry boundary");
Check(!Plan(above:.3999f).Active,"lower exit cannot masquerade as upper entry");
Check(Plan(lateral:.2f,vertical:.3f,above:.4f).Active,"original geometric envelope boundaries retained");
Check(!Plan(lateral:-.1f).Active&&!Plan(vertical:-.1f).Active,"malformed unsigned geometry is rejected");
Check(!Plan(trigger:0).Active,"invalid trigger cannot create a boundary");
foreach(float bad in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity}){
 Check(!Plan(progress:bad).Active&&!Plan(required:bad).Active,"invalid predecessor geometry rejected "+bad);
 Check(!Plan(ahead:bad).Active&&!Plan(lateral:bad).Active&&!Plan(vertical:bad).Active,"invalid adjacent geometry rejected "+bad);
 Check(!Plan(above:bad).Active&&!Plan(requested:bad).Active&&!Plan(trigger:bad).Active,"invalid native request rejected "+bad);
}
Check(F.ForwardRemoval(new(CapabilityFrontierKind.DropDownBoundary,-.08f,0))==0,"native retreat remains unmodified");
Check(F.ForwardRemoval(new(CapabilityFrontierKind.DropDownBoundary,.01f,.125f))==0,"smaller request is never amplified");
Check(!FollowerCapabilityFrontierDriver.Commit(null!),"missing actual native consumer rejects");
var port=new Port();Check(FollowerCapabilityFrontierDriver.Commit(port)&&Near(port.Removed,.075f),"compiled existing driver consumes dropdown transition plan");
port=new Port{Current=false};Check(!FollowerCapabilityFrontierDriver.Commit(port)&&port.Effects==0,"foreign native origin cannot observe boundary");
port=new Port{Invalidate=true};Check(!FollowerCapabilityFrontierDriver.Commit(port)&&port.Effects==0,"writer replacement during observation prevents restriction");
port=new Port();for(int i=0;i<3;i++)FollowerCapabilityFrontierDriver.Commit(port);
Check(port.Effects==3,"every actual native callback is guarded without render-frame dedupe");
port.Current=false;Check(!FollowerCapabilityFrontierDriver.Commit(port)&&port.Effects==3,"completed handoff releases old native origin");
File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=true,checks=checks.Count,cases=checks,physicsSimulated=false},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS {checks.Count} compiled dropdown transition checks");
sealed class Port:ICapabilityFrontierPort{
 public bool Current=true,Invalidate;public int Effects;public float Removed;
 public bool IsCurrent=>Current;
 public bool TryObserve(out CapabilityFrontierSnapshot s){var p=FollowerCapabilityDropDownTransitionPolicy.Plan(new(DropDownConsumerPhase.Commit,true,true,true,1.2f,.888f,.15f,.01f,.1f,1.2f,.2f,.42f));s=new(CapabilityFrontierKind.DropDownBoundary,.2f,p.ForwardBudget);if(Invalidate)Current=false;return p.Active;}
 public void RestrictForward(float removed){Removed=removed;Effects++;}
}
