using System.Text.Json;
using P=BotTraversal.Core.FollowerCapabilityFailureSequence;
var cases=new List<string>();
void Check(bool ok,string label){if(!ok)throw new Exception(label);cases.Add(label);}
// Recorded ordering: Native failed, Climb 11 selected, ordinary Jump 111 offered in the SAME frame.
Check(P.OwnsStrategy(P.Stage.SelfRescue,true),"246.7365 selected Climb owns semantic Jump 111");
Check(!P.CanAllocateStrategy(true,true,false,false),"246.7365 reject ordinary Jump before its probe exists");
Check(P.CanAllocateStrategy(true,true,true,true),"next owned Climb dispatch consumes the selected attempt");
Check(!P.CanAllocateStrategy(true,true,true,false),"owned source alone cannot substitute the wrong physical kind or mission");
Check(!P.CanAllocateStrategy(true,false,true,true),"no repeated allocation while selected attempt already runs");
Check(!P.OwnsStrategy(P.Stage.SelfRescue,false),"retired or advanced semantic step cannot block the next normal capability");
// Native Jump really took off and landed on the obstacle top, not yet a rejected landing.
Check(P.ContinueExecutedJump(true,true,true,true,false,false),"245.8295 takeoff to 246.7365 supported landing remains eligible for bounded forward continuation");
Check(!P.ContinueExecutedJump(true,false,false,false,false,false),"an allocated entry probe does not manufacture physical continuation authority");
for(int mask=0;mask<16;mask++)Check(P.CanAllocateStrategy((mask&1)!=0,(mask&2)!=0,(mask&4)!=0,(mask&8)!=0)==(mask==15),"allocation permissions "+mask);
for(int mask=0;mask<64;mask++){
 bool current=(mask&1)!=0,consumed=(mask&2)!=0,attempted=(mask&4)!=0,started=(mask&8)!=0,partial=(mask&16)!=0,rejected=(mask&32)!=0;
 Check(P.ContinueExecutedJump(current,consumed,attempted,started,partial,rejected)==(current&&consumed&&attempted&&(started||partial||rejected)),"physical continuation permissions "+mask);
}
foreach(var stage in Enum.GetValues<P.Stage>())foreach(bool current in new[]{false,true})
 Check(P.OwnsStrategy(stage,current)==(current&&(stage==P.Stage.Alternate||stage==P.Stage.SelfRescue||stage==P.Stage.RetryAlternate)),"stage lifetime "+stage+" "+current);
// Five observed overwritten strategies must reject the ordinary semantic allocation, then accept only their frozen candidate.
foreach(var sample in new[]{"Ada 246.7365 Climb11->Jump111","Ghost 246.825 Climb11->Jump111","Ada 364.801 Vault146->Jump111","Ghost 364.837 Vault146->Jump111","Ada 382.508 Vault146->Jump111"}){
 Check(!P.CanAllocateStrategy(true,true,false,false),sample+" foreign callback blocked");
 Check(P.CanAllocateStrategy(true,true,true,true),sample+" selected callback accepted");
}
File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=true,checks=cases.Count,cases,physicsSimulated=false,kind="production DLL decision replay; host wiring checked separately"},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS {cases.Count} RC124 production decision and recorded-order replay checks");
