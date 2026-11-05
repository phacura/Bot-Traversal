using System.Text.Json;
using BotTraversal.Core;
using P=BotTraversal.Core.FollowerCapabilityAdjacentLandingPolicy;
using D=BotTraversal.Core.FollowerCapabilityAdjacentLandingDriver;
var checks=new List<string>();
void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);checks.Add(name);}
bool Near(float a,float b)=>Math.Abs(a-b)<.00001f;
AdjacentLandingGeometry G(float ahead=.15f,float lateral=.06f,float distance=.16f,float vertical=.34f,float span=2.53f,bool grounded=false,float aboveExit=1.73f)=>new(ahead,lateral,distance,vertical,span,grounded,aboveExit);
var first=G(1.49782956f,.216259986f,1.51336f,.51111845f,grounded:false,aboveExit:1.9009905f);
var near=G(.148411587f,.06542108f,.1622f,.34024796f,grounded:false,aboveExit:1.7301199f);
Check(P.LegacyRegion(first)&&Near(P.MoveSpeed(first),.8840667f),"recorded early next-entry observation preserves original move-speed demand");
Check(P.LegacyRegion(near)&&Near(P.MoveSpeed(near),.55f),"recorded approaching next ledge keeps existing airborne moving floor");
Check(Near(P.Evaluate(new(first,2f)).NativeSprintCap,1.7681334f),"early demand is mapped to actual native SprintSpeed channel");
Check(Near(P.Evaluate(new(near,2f)).NativeSprintCap,1.1f),"recorded .55 demand constrains native full-speed request 2 to 1.1");
var landing=G(-.56f,.032f,.561f,.167f,grounded:true,aboveExit:1.223f);
Check(!P.LegacyRegion(landing),"recorded first landing is already past old -.30 speed region");
Check(P.Evaluate(new(landing,2)).Active&&Near(P.Evaluate(new(landing,2)).NativeSprintCap,.48f),"upper landing overrun retains native bound during existing predecessor proof");
Check(Near(P.MoveSpeed(G(grounded:true)),.3045f),"supported contact has no unsupported moving floor");
Check(Near(P.MoveSpeed(G(-.2f,grounded:true)),.24f),"supported predecessor retains original minimum moving demand");
Check(Near(P.MoveSpeed(G(-.2f,grounded:false,aboveExit:.4f)),.24f),"airborne floor retains strict old .40 height boundary");
Check(Near(P.MoveSpeed(G(-.2f,grounded:false,aboveExit:.4001f)),.55f),"genuinely unsupported upper body retains existing moving floor");
foreach(var (a,inside) in new[]{(-.30001f,false),(-.3f,true),(1.55f,true),(1.55001f,false)})Check(P.LegacyRegion(G(a,distance:.5f))==inside,"legacy intent entry bounds unchanged "+a);
foreach(var (l,inside) in new[]{(.38f,true),(.38001f,false)})Check(P.LegacyRegion(G(lateral:l))==inside,"legacy lateral bound unchanged "+l);
Check(P.LegacyRegion(G(distance:1.65f))&&!P.LegacyRegion(G(distance:1.65001f)),"legacy intent radial bound unchanged");
Check(P.LegacyRegion(G(vertical:1.35f))&&!P.LegacyRegion(G(vertical:1.35001f)),"vertical body envelope remains bounded");
Check(!P.Evaluate(new(G(-2.54f,distance:2.54f),2)).Active,"speed contract cannot extend beyond successor span");
Check(!P.Evaluate(new(G(-.56f,distance:.56f,aboveExit:.4f),2)).Active,"extended speed region releases after leaving upper landing height");
Check(!P.Evaluate(new(G(-.56f,distance:.56f,lateral:.381f),2)).Active,"parallel unrelated landing cannot inherit bound");
Check(!P.Evaluate(new(G(1.56f,distance:1.56f),2)).Active,"distant future ledge cannot restrict current locomotion");
foreach(float value in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity}){
 Check(!P.Evaluate(new(G(ahead:value),2)).Active,"invalid body geometry cannot own a native speed contract "+value);
 Check(!P.Evaluate(new(G(),value)).Active,"invalid native request is not manufactured into an admitted request "+value);
}
Check(Near(P.MoveSpeed(new AdjacentLandingGeometry(-.2f,.06f,.16f,.34f,2.53f,false,1.73f,false)),.24f),"missing movement context cannot manufacture an airborne speed floor");
Check(D.Commit(null!)==D.Receipt.Rejected,"missing envelope adapter cannot constrain native value");
var p=new Port{Geometry=near};Check(D.Commit(p)==D.Receipt.Clamped&&Near(p.Result,1.1f)&&p.Restrictions==1,"native setter receives the admitted cap rather than unchanged full speed");
Check(p.Trace.SequenceEqual(new[]{"live","observe","live","restrict"}),"capture and full revalidation precede parameter restriction");
p=new Port{Current=false};Check(D.Commit(p)==D.Receipt.Rejected&&p.Restrictions==0&&p.Trace.SequenceEqual(new[]{"live"}),"stale initial probe cannot observe or constrain any native setter");
p=new Port{HasPair=false};Check(D.Commit(p)==D.Receipt.Unconstrained&&p.Restrictions==0&&p.Result==2,"standalone DropDown or unrelated capability remains untouched");
p=new Port{InvalidateOnObserve=true};Check(D.Commit(p)==D.Receipt.Rejected&&p.Restrictions==0&&p.Unsafe==0,"writer or native resource changed during observation rejects captured envelope");
p=new Port{Requested=.2f};Check(D.Commit(p)==D.Receipt.Unconstrained&&Near(p.Result,.2f)&&p.Restrictions==0,"native braking below cap is never raised");
p=new Port{Requested=0};Check(D.Commit(p)==D.Receipt.Unconstrained&&p.Result==0,"native zero command remains zero");
p=new Port{Requested=-1};Check(D.Commit(p)==D.Receipt.Unconstrained&&p.Result==-1,"negative native sentinel is never converted into motion");
p=new Port{Geometry=G(2,distance:2)};Check(D.Commit(p)==D.Receipt.Unconstrained&&p.Result==2,"native Sprint outside exact adjacent region remains unchanged");
p=new Port{Geometry=landing};for(int i=0;i<4;i++){
 p.Result=2;Check(D.Commit(p)==D.Receipt.Clamped&&Near(p.Result,.48f),"native full-speed rewrite is constrained without frame dedupe "+i);
}
Check(p.Restrictions==4,"multiple native physics callbacks each receive their own constraint");
p.Current=false;p.Result=2;Check(D.Commit(p)==D.Receipt.Rejected&&p.Result==2,"retired origin cannot leave a cached constraint on Follow or successor");
p=new Port{Geometry=near};D.Commit(p);p.Geometry=G(1.56f,distance:1.56f);p.Result=2;
Check(D.Commit(p)==D.Receipt.Unconstrained&&p.Result==2,"geometry changes are observed anew without a retained envelope registry");
p=new Port{Geometry=near,Requested=1f};Check(D.Commit(p)==D.Receipt.Unconstrained&&p.Result==1,"already slower native request needs no redundant restriction callback");
File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=true,checks=checks.Count,cases=checks,physicsSimulated=false},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS {checks.Count} compiled adjacent landing envelope checks");
sealed class Port:ICapabilityAdjacentLandingPort{
 public bool Current=true,HasPair=true,InvalidateOnObserve;public float Requested=2;private float? result;public float Result{get=>result??Requested;set=>result=value;}
 public AdjacentLandingGeometry Geometry=new(.15f,.06f,.16f,.34f,2.53f,false,1.73f);
 public int Restrictions,Unsafe;public List<string> Trace=new();
 public bool IsCurrent{get{Trace.Add("live");return Current;}}
 public bool TryObserve(out AdjacentLandingSnapshot snapshot){Trace.Add("observe");snapshot=new(Geometry,Requested);if(InvalidateOnObserve)Current=false;return HasPair;}
 public void Restrict(float value){Trace.Add("restrict");if(!Current)Unsafe++;Result=value;Restrictions++;}
}
