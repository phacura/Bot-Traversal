using System.Text.Json;
using BotTraversal.Core;
using P=BotTraversal.Core.FollowerCapabilityFrontierPolicy;
using D=BotTraversal.Core.FollowerCapabilityFrontierDriver;
using K=BotTraversal.Core.CapabilityFrontierKind;
var checks=new List<string>();
void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);checks.Add(name);}
bool Near(float a,float b)=>Math.Abs(a-b)<.00001f;
float Clip(K kind,float value,float budget)=>value-P.ForwardRemoval(new(kind,value,budget));
Check(Near(P.PreparationLead(5f,.75f,.10f,.08f,.05f,.15f,.95f),.95f),"existing minimum preparation distance retained");
Check(Near(P.PreparationLead(10f,.75f,.10f,.08f,.05f,.15f,.95f),1f),"measured speed controls original preparation lead");
Check(Near(P.PreparationLead(10f,.75f,0f,.08f,.05f,.15f,.10f),.8f),"missing profile lead uses existing fallback seconds");
Check(Near(P.PreparationLead(10f,.75f,.4f,.08f,.05f,.15f,.10f),1.5f),"existing maximum lead seconds retained");
Check(Near(P.PreparationLead(-1f,.75f,.10f,.08f,.05f,.15f,.01f),.075f),"minimum reference speed retained without inventing velocity");
Check(Near(Clip(K.LandingProof,.08f,.349822521f-.025f),.08f),"recorded upper candidate initially retains motion within frontier");
Check(Near(Clip(K.LandingProof,.08f,.0501470976f-.025f),.0251470976f),"recorded near-entry native motion cannot cross successor hold plane");
Check(Clip(K.LandingProof,.09166656f,0)==0,"retained Transition momentum is constrained at actual movement consumption");
Check(Clip(K.LandingProof,-.08f,0)==-.08f,"retreat motion is not turned into forward movement");
Check(Clip(K.None,.08f,0)==.08f,"no frontier leaves native movement unchanged");
Check(Clip(K.LandingProof,0,0)==0,"no forward request cannot gain motion");
Check(Clip(K.LandingProof,.08f,-.2f)==0,"already reached frontier never creates rewind or negative forward movement");
foreach(float bad in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity}){
 Check(P.ForwardRemoval(new(K.LandingProof,bad,0))==0,"invalid native displacement is not manufactured into a valid request "+bad);
 Check(P.ForwardRemoval(new(K.LandingProof,.08f,bad))==0,"unavailable frontier cannot constrain unrelated motion "+bad);
}
Check(Near(P.JumpBudget(-1.39962137f,.95f,false,true,true),.44962137f),"retry Transition may approach preparation plane while native readiness is pending");
Check(P.JumpBudget(-.1901051f,.95f,false,true,true)==0,"recorded Jump retry Transition cannot drift beyond late current-body frontier");
Check(P.JumpBudget(-.406447619f,.95f,false,true,true)==0,"second recorded retry preserves a forward opportunity without rewind");
Check(float.IsPositiveInfinity(P.JumpBudget(-.19f,.95f,true,true,true)),"real locomotion restores run-up; missing momentum alone cannot create stationary deadlock");
Check(P.JumpBudget(-1.2f,.95f,true,false,true)>.24f,"uncentered actor retains geometric preparation budget");
Check(P.JumpBudget(-.3f,.95f,true,true,false)==0,"facing readiness cannot be replaced by advancing past entry");
Check(float.IsPositiveInfinity(P.JumpBudget(float.NaN,.95f,false,true,true)),"invalid geometry does not invent a hold");
Check(P.DispatchFrame(true,true,true,true,true,true,false),"live legal native window may dispatch independently of planner retry clock");
Check(!P.DispatchFrame(false,true,true,true,true,true,false),"stale mission cannot allocate probe");
Check(!P.DispatchFrame(true,false,true,true,true,true,false),"other capability cannot borrow Jump preparation dispatch");
Check(!P.DispatchFrame(true,true,false,true,true,true,false),"existing probe prevents duplicate native start");
Check(!P.DispatchFrame(true,true,true,false,true,true,false),"foreign commitment cannot allocate native probe");
Check(!P.DispatchFrame(true,true,true,true,false,true,false),"survival or manual authority blocks preparation dispatch");
Check(!P.DispatchFrame(true,true,true,true,true,false,false),"distant route target cannot become physical native admission");
Check(!P.DispatchFrame(true,true,true,true,true,true,true),"existing native admission denial cannot be bypassed");
Check(!D.Commit(null!),"missing frontier adapter rejects");
var p=new Port();Check(D.Commit(p)&&Near(p.Removed,.055f)&&p.Effects==1,"actual consumer receives one restricted forward displacement");
Check(p.Events.SequenceEqual(new[]{"live","observe","live","restrict"}),"full live identity is revalidated before restriction");
p=new Port{Current=false};Check(!D.Commit(p)&&p.Effects==0&&p.Events.Count==1,"initial stale actor cannot observe native frontier");
p=new Port{HasObservation=false};Check(!D.Commit(p)&&p.Effects==0,"unqualified body has no fallback frontier");
p=new Port{InvalidateOnObserve=true};Check(!D.Commit(p)&&p.Effects==0&&p.Unsafe==0,"epoch or native context replacement during observation rejects effect");
p=new Port{Requested=-.08f};Check(!D.Commit(p)&&p.Effects==0,"negative native request remains untouched");
p=new Port{Requested=.01f};Check(!D.Commit(p)&&p.Effects==0,"smaller native movement is never amplified to budget");
p=new Port{Budget=0};for(int i=0;i<4;i++)Check(D.Commit(p),"each real native callback enforces pending frontier "+i);
Check(p.Effects==4,"native physics callbacks are not deduplicated by render frame");
p.Current=false;Check(!D.Commit(p)&&p.Effects==4,"retired predecessor cannot constrain successor or Follow");
foreach(float value in new[]{.001f,.016f,.1f,1f}){
 float first=Clip(K.LandingProof,value,.025f);Check(Near(Clip(K.LandingProof,first,.025f),first),"base/override interception is idempotent "+value);
}
File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=true,checks=checks.Count,cases=checks,physicsSimulated=false},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS {checks.Count} compiled native frontier and preparation-frame checks");
sealed class Port:ICapabilityFrontierPort{
 public bool Current=true,HasObservation=true,InvalidateOnObserve;public float Requested=.08f,Budget=.025f,Removed;
 public int Effects,Unsafe;public List<string> Events=new();
 public bool IsCurrent{get{Events.Add("live");return Current;}}
 public bool TryObserve(out CapabilityFrontierSnapshot s){Events.Add("observe");s=new(CapabilityFrontierKind.LandingProof,Requested,Budget);if(InvalidateOnObserve)Current=false;return HasObservation;}
 public void RestrictForward(float value){Events.Add("restrict");if(!Current)Unsafe++;Removed=value;Effects++;}
}
