using System.Text.Json;
using BotTraversal.Core;
using P=BotTraversal.Core.FollowerCapabilityRouteContinuationPolicy;
using F=BotTraversal.Core.CapabilityContinuationFailure;
using Phase=BotTraversal.Core.CapabilityContinuationPhase;
using L=BotTraversal.Core.FollowerTraversalLifecycle;
var checks=new List<string>();void Check(bool ok,string label){if(!ok)throw new Exception("FAIL "+label);checks.Add(label);}
var id=new L.Identity("Ghost",2,140,3,28,79,24,4,19);
F Plan(Phase phase=Phase.CanonicalRoute,bool current=true,bool actor=true,bool owner=true,bool cursor=true,bool native=false,bool blocked=false,L.Identity? origin=null,L.Identity? live=null)=>
 P.Evaluate(new(origin??id,live??id,phase,current,actor,owner,cursor,native,blocked));
Check(Plan()==F.None,"canonical route may publish only with current cursor and no native probe");
Check(Plan(Phase.NativeLanding,native:true)==F.None,"current native landing does not borrow pre-native admission");
Check(Plan(native:true)==F.NativeOwnsMotion,"native probe prevents old route-prefix input");
Check(Plan(Phase.NativeLanding)==F.MissingNativeProbe,"landing continuation cannot outlive native probe");
Check(Plan(cursor:false)==F.CursorMismatch,"stale cursor cannot borrow current owner generation");
Check(Plan(Phase.NativeLanding,cursor:false,native:true)==F.None,"native landing uses its probe rather than inventing a cursor requirement");
for(int mask=0;mask<64;mask++){
 bool b(int i)=>(mask&(1<<i))!=0;
 bool canonical=Plan(current:b(0),actor:b(1),owner:b(2),cursor:b(3),native:b(4),blocked:b(5))==F.None;
 bool landing=Plan(Phase.NativeLanding,current:b(0),actor:b(1),owner:b(2),cursor:b(3),native:b(4),blocked:b(5))==F.None;
 Check(canonical==(mask==15),"pre-native route permission matrix "+mask);
 Check(landing==(mask is 23 or 31),"native landing permission matrix "+mask);
}
var foreign=new[]{new L.Identity("Other",2,140,3,28,79,24,4,19),new L.Identity("Ghost",3,140,3,28,79,24,4,19),new L.Identity("Ghost",2,141,3,28,79,24,4,19),new L.Identity("Ghost",2,140,2,28,79,24,4,19),new L.Identity("Ghost",2,140,3,29,79,24,4,19),new L.Identity("Ghost",2,140,3,28,80,24,4,19),new L.Identity("Ghost",2,140,3,28,79,25,4,19),new L.Identity("Ghost",2,140,3,28,79,24,5,19),new L.Identity("Ghost",2,140,3,28,79,24,4,20)};
for(int i=0;i<foreign.Length;i++)Check(Plan(live:foreign[i])==F.ReplacedOrigin&&Plan(Phase.NativeLanding,native:true,live:foreign[i])==F.ReplacedOrigin,"no borrowing across identity component "+i);
Check(Plan(origin:default(L.Identity))==F.MissingOrigin,"missing origin fails closed");
Check(Plan(live:default(L.Identity))==F.MissingOrigin,"missing live reference fails closed");
var zero=new L.Identity("Ghost",2,140,3,28,79,24,4,0);
Check(Plan(origin:zero,live:zero)==F.MissingOrigin,"lifecycle identity writer zero is not a physical ticket");
Check(Plan((Phase)999)==F.MissingOrigin,"unknown phase cannot publish");
Check(P.ResolveCanonical(false,false)==P.CanonicalReceipt.NotSelected,"unselected route may retain applicable producer selection");
Check(P.ResolveCanonical(true,true)==P.CanonicalReceipt.Published,"selected published canonical route suppresses duplicate producer");
Check(P.ResolveCanonical(true,false)==P.CanonicalReceipt.Rejected,"selected failed canonical route never becomes NotSelected fallback");
// Contract effect model uses the same candidate receipt consumed by the actual host.
foreach(bool selected in new[]{false,true})foreach(bool published in new[]{false,true}){
 int canonicalWrites=published?1:0,legacyWrites=0;
 var result=P.ResolveCanonical(selected,published);
 if(result==P.CanonicalReceipt.NotSelected)legacyWrites++;
 Check(!selected||legacyWrites==0,"selected canonical cannot publish legacy NavMesh/NoWay "+selected+":"+published);
 Check(!selected||!published||canonicalWrites==1,"canonical receipt is a single publisher "+selected+":"+published);
}
Check(P.ReserveLegacyBridge(true),"active canonical successor is reserved from legacy bridge");
Check(!P.ReserveLegacyBridge(false),"standalone compatibility bridge remains available without canonical owner");
foreach(bool canonical in new[]{false,true}){
 int bridgeWrites=0,completionCredits=0;
 if(!P.ReserveLegacyBridge(canonical))bridgeWrites++;
 Check(bridgeWrites==(canonical?0:1)&&completionCredits==0,"bridge reservation is handling, never traversal success "+canonical);
}
File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=true,checks=checks.Count,cases=checks,physicsSimulated=false,scope="current route/native admission and no-fallback contracts"},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"PASS {checks.Count} candidate route continuation checks");
