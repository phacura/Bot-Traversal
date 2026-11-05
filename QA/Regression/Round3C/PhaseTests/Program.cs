using P=BotTraversal.Core.FollowerCapabilityPhasePolicy;
using R=BotTraversal.Core.FollowerCapabilityDecisionRules;
using L=BotTraversal.Core.FollowerTraversalLifecycle;
using System.Text.Json;
var checks=new List<string>();
void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);checks.Add(name);}
for(int mask=0;mask<8;mask++){
 bool consumed=(mask&1)!=0,shared=(mask&2)!=0,acquired=(mask&4)!=0;
 var plan=P.DropDownEntry(consumed,shared,acquired);
 Check((plan==P.NativeEntry.RejectConsumed)==consumed,"consumed native entry is never reused "+mask);
 Check((plan==P.NativeEntry.AcquireEntry)==(!consumed&&shared&&!acquired),"only missing shared entry requests acquisition "+mask);
}
Check(!P.DirectionValid(false,true,1),"missing entry geometry rejected");
Check(!P.DirectionValid(true,false,1),"missing exit geometry rejected");
Check(!P.DirectionValid(true,true,.49f),"degenerate direction rejected");
Check(P.DirectionValid(true,true,.5f),"existing direction boundary inclusive");
Check(P.DirectionValid(true,true,float.NaN),"migration preserves original NaN branch semantics; no unsolicited tuning");
Check(P.LandingStateReleased(1,2)&&!P.LandingStateReleased(2,2),"landing state release keeps native state authoritative");
Check(P.SoftLandingRepair(10,10,1,2,1,3),"soft repair at original retry deadline");
Check(!P.SoftLandingRepair(9,10,1,2,1,3),"soft repair cooldown not bypassed");
Check(!P.SoftLandingRepair(10,10,2,2,1,3),"soft repair bounded by attempts");
Check(!P.SoftLandingRepair(10,10,1,2,3,3),"hard budget ends soft repair");
Check(P.HardLandingRepair(3,3,1,2,false),"hard budget boundary included");
Check(P.HardLandingRepair(1,3,2,2,false),"fresh post-soft attempt count can request hard recovery same tick");
Check(!P.HardLandingRepair(3,3,2,2,true),"hard recovery at most once");
Check(P.TypedDropAfterScannerRefusal(true,false,"DropDown"),"typed DropDown transfers from incompatible scanner");
Check(!P.TypedDropAfterScannerRefusal(true,true,"DropDown"),"cannot restart consumed DropDown");
Check(!P.TypedDropAfterScannerRefusal(true,false,"Jump"),"scanner refusal does not relabel Jump");
Check(!P.TypedDropAfterScannerRefusal(false,false,"DropDown"),"existing disabled mode preserved");
Check(P.FarSideRecoveryAccepted(true,true,false),"far side plus sampled consumer accepted");
Check(P.FarSideRecoveryAccepted(true,false,true),"off-mesh qualified proof accepted");
Check(!P.FarSideRecoveryAccepted(false,true,true),"NavMesh cannot substitute far-side proof");
Check(!P.FarSideRecoveryAccepted(true,false,false),"far side alone cannot manufacture a consumer");
Check(P.NativeRecoveryConsumed(true,false)&&P.NativeRecoveryConsumed(false,true)&&!P.NativeRecoveryConsumed(false,false),"native consumption snapshot");
Check(P.NativeRecoveryObserved(false,false,true)&&!P.NativeRecoveryObserved(false,false,false),"observed native execution snapshot");
Check(P.JumpLandingRecovery(true,"Jump")&&!P.JumpLandingRecovery(true,"Vault")&&!P.JumpLandingRecovery(false,"Jump"),"Jump landing recovery maintains semantic kind");
// Boundary fixtures execute the migrated production rules, not a copied formula.
Check(R.UpdateProbeJumpPriming_realGroundRunup_1(.5f,.25f,.5f,1f,true),"real runup eligible inside trigger corridor");
Check(!R.UpdateProbeJumpPriming_realGroundRunup_1(.1f,.25f,.5f,1f,true),"input without measured ground runup is insufficient");
Check(!R.UpdateProbeJumpPriming_realGroundRunup_1(.5f,.25f,2f,1f,true),"far approach cannot fire early");
Check(!R.UpdateProbeJumpPriming_realGroundRunup_1(.5f,.25f,.5f,1f,false),"missed physical takeoff window vetoes jump");
Check(!R.UpdateProbeJumpPriming_realGroundRunup_1(float.NaN,.25f,.5f,1f,true),"invalid runup sample is not progress");
Check(R.UpdateNativeDropDown_supportedLandingFallback_4(false,true,true,.1f,.2f),"supported landing has connected downstream consumer");
Check(!R.UpdateNativeDropDown_supportedLandingFallback_4(false,false,true,.1f,.2f),"unsettled NavMesh candidate insufficient");
Check(!R.UpdateNativeDropDown_supportedLandingFallback_4(false,true,false,.1f,.2f),"disconnected landing cannot complete");
Check(!R.UpdateNativeDropDown_supportedLandingFallback_4(false,true,true,.3f,.2f),"fast descent cannot become landing proof");
Check(!R.UpdateNativeDropDown_rc65QualifiedContinuousLandingHandoff_1(.19f,0,.3f),"continuous landing waits for existing proof tail");
Check(R.UpdateNativeDropDown_rc65QualifiedContinuousLandingHandoff_1(.21f,0,.3f),"continuous landing qualifies after proof tail");
// Successful raid fixture: the first descent physically covered the successor.
var mission=new L.MissionState();var steps=Enumerable.Range(0,4).Select(_=>new L.StepState()).ToArray();
L.Identity Id(int index)=>new("raid-fixture",2,245,index,39+index,100+index,36,steps[index].GeometryVersion,23);
Check(mission.Resolve(steps[0],Id(0),steps[0],Id(0),L.Resolution.Subsumed,true,false)==L.CommitResult.Applied,"recorded approach already covered");
for(int i=1;i<=2;i++){
 var id=Id(i);var probe=new L.ProbeState(id,steps[i]);Check(steps[i].Attach(probe),"recorded own action attaches");
 Check(probe.PublishCompletion(id,steps[i],L.ActionOutcome.NativePhysical)==L.CommitResult.Applied,"recorded own native physical action");
 Check(mission.Resolve(steps[i],id,steps[i],id,L.Resolution.Executed,true,true)==L.CommitResult.Applied,"recorded Jump and first descent resolve independently");probe.Retire();
}
Check(steps[3].DebtOutstanding,"predecessor native success does not clear successor");
Check(mission.Resolve(steps[3],Id(3),steps[3],Id(3),L.Resolution.Subsumed,false,true)==L.CommitResult.Rejected,"second descent requires independent physical coverage proof");
Check(mission.Resolve(steps[3],Id(3),steps[3],Id(3),L.Resolution.Subsumed,true,true)==L.CommitResult.Applied,"recorded covered successor uses Subsumed once");
Check(steps[3].Probe==null&&steps[3].Result==L.Resolution.Subsumed,"covered successor never gets a fake native attempt");
Check(mission.CanHandoffToFollow(4,false,false),"recorded four-step mission permits Follow after full resolution");
File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=true,checks=checks.Count,cases=checks,physicsSimulated=false},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS {checks.Count} compiled phase/rule/covered-successor checks");
