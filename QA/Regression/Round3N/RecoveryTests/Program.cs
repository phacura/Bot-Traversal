using System.Text.Json;
using BotTraversal.Core;
using P=BotTraversal.Core.FollowerCapabilityRecoveryPolicy;
using O=BotTraversal.Core.NativeRecoveryObservation;
using T=BotTraversal.Core.NativeRecoveryTerminal;
var checks=new List<string>();void Check(bool ok,string label){if(!ok)throw new Exception("FAIL "+label);checks.Add(label);}
// Literal expected outcomes protect the execution-state boundary, including a
// rejected-landing flag which must carry no authority without a native state.
foreach(var row in new[]{(false,false,O.AwaitingNativeState),(false,true,O.AwaitingNativeState),(true,false,O.ObservedContinuation),(true,true,O.RejectedJumpLanding)})
 Check(P.Observed(row.Item1,row.Item2)==row.Item3,"native observation route "+row);
foreach(var row in new[]{(false,false,false),(false,true,false),(true,false,false),(true,true,true)})
 Check(P.PreserveJumpTrajectory(row.Item1,row.Item2)==row.Item3,"single trajectory guard "+row);
Check(P.CanOfferAssist(false),"unattempted assist remains available");
Check(!P.CanOfferAssist(true),"consumed assist cannot be offered twice");
foreach(var row in new[]{("Climb",true),("Vault",true),("climb",true),("vAuLt",true),("Jump",false),("DropDown",false),("WalkThrough",false),("PhysicalCorridor",false),("Follow",false),("Recovery",false),("",false),("4",false),("VaultExtra",false),(" Vault",false),(null!,false)})
 Check(P.CanCompletePartial(row.Item1)==row.Item2,"partial completion belongs only to Climb/Vault: "+row.Item1);
foreach(var row in new[]{(false,false,true,T.Teleport),(false,true,true,T.FailTransient),(true,false,false,T.Teleport),(true,true,false,T.FailTransient)}){
 var plan=P.Unconsumed(row.Item1,row.Item2);
 Check(plan.TryAssist==row.Item3,"unconsumed assist choice "+row);
 Check(plan.Terminal==row.Item4,"transient instance cannot borrow teleport "+row);
}
// Recovery may try an executor before observing the next facts. Check changes
// between decisions rather than treating an earlier snapshot as completion.
Check(P.Observed(false,false)==O.AwaitingNativeState,"declined run-out without observed state cannot enter continuation");
Check(P.Observed(true,false)==O.ObservedContinuation,"fresh observed state after declined run-out can enter original continuation");
Check(P.CanOfferAssist(false)&&!P.CanOfferAssist(true),"a failed executor which consumes assist closes the later assist gate");
Check(P.PreserveJumpTrajectory(true,true)&&P.Observed(true,true)==O.RejectedJumpLanding,"rejected native Jump retains original forward-only recovery family");
Check(!P.PreserveJumpTrajectory(true,false),"unstarted Jump is not a consumed physical trajectory");
foreach(string kind in new[]{"Climb","Vault"}){
 Check(P.CanCompletePartial(kind),kind+" can ask existing partial-native executor");
 Check(P.Observed(false,false)==O.AwaitingNativeState,kind+" refusal does not manufacture an observed native state");
 Check(P.Observed(true,false)==O.ObservedContinuation,kind+" observed state retains continuation after partial executor declines");
 Check(!P.CanOfferAssist(true),kind+" exhausted native branch cannot repeat assist");
 Check(P.Unconsumed(true,true).Terminal==T.FailTransient,kind+" exhausted transient transfer cannot fall through to teleport");
}
Check(P.Unconsumed(false,true).TryAssist&&P.Unconsumed(false,true).Terminal==T.FailTransient,"new transient candidate gets original assist opportunity but not synthetic recovery");
Check(!P.Unconsumed(true,false).TryAssist&&P.Unconsumed(true,false).Terminal==T.Teleport,"known exhausted candidate reaches existing terminal executor only");
foreach(string kind in new[]{"Jump","DropDown","WalkThrough","Follow","Recovery"})
 Check(!P.CanCompletePartial(kind),kind+" cannot borrow Climb/Vault completion executor");
File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=true,checks=checks.Count,cases=checks,physicsSimulated=false},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"PASS {checks.Count} compiled native recovery decision and sequence checks");
