using System.Text.Json;
using BotTraversal.Core;
using P=BotTraversal.Core.FollowerCapabilityFollowRecoveryPolicy;
using M=BotTraversal.Core.CapabilityFollowRecoveryMode;
using F=BotTraversal.Core.CapabilityFollowRecoveryFailure;
using D=BotTraversal.Core.FollowerMovementWriterAuthority.WriterDomain;
var checks=new List<string>();void Check(bool ok,string label){if(!ok)throw new Exception("FAIL "+label);checks.Add(label);}
P.Snapshot S(M mode=M.GeneralRecovery,D owner=D.Follow,long origin=7,long live=7,bool actor=true,bool native=false,bool probe=true,bool canonical=false,bool mission=false,bool epoch=false,bool blocked=false)=>new(origin,live,owner,mode,actor,native,probe,canonical,mission,epoch,blocked);
foreach(var mode in Enum.GetValues<M>())foreach(var domain in Enum.GetValues<D>()){
 bool ok=true;
 for(int mask=0;mask<32;mask++){
  var s=S(mode,domain,native:(mask&1)!=0,canonical:(mask&2)!=0,mission:(mask&4)!=0,epoch:(mask&8)!=0,blocked:(mask&16)!=0);
  bool expected=mode switch{
   M.FollowPath=>domain==D.Follow&&(mask is 0 or 8 or 12),
   M.GeneralRecovery=>(domain is D.Follow or D.Recovery or D.PostTraversal)&&(mask is 0 or 8 or 12),
   M.ProbeRecovery=>domain==D.Traversal&&(mask is 1 or 3 or 5 or 7 or 9 or 11 or 13 or 15),
   M.TeleportQuiescence=>domain==D.Teleport,
   _=>false};
  ok&=(P.Evaluate(s)==F.None)==expected;
 }
 Check(ok,"32-context ownership truth table "+mode+":"+domain);
}
foreach(var mode in Enum.GetValues<M>()){
 var owner=mode==M.TeleportQuiescence?D.Teleport:mode==M.ProbeRecovery?D.Traversal:D.Follow;
 Check(P.Evaluate(S(mode,owner,actor:false,native:mode==M.ProbeRecovery))==F.MissingActor,"foreign or dead actor rejected "+mode);
 Check(P.Evaluate(S(mode,owner,origin:0,native:mode==M.ProbeRecovery))==F.MissingOrigin,"zero generation is not a ticket "+mode);
 Check(P.Evaluate(S(mode,owner,origin:6,native:mode==M.ProbeRecovery))==F.StaleOrigin,"newer owner cannot be borrowed "+mode);
}
Check(P.Evaluate(S(M.ProbeRecovery,D.Traversal,native:true,probe:false))==F.StaleProbe,"native repair cannot use replaced probe");
Check(P.Evaluate(S(M.GeneralRecovery,D.Follow,native:true))==F.NativeOwnsMotion,"ordinary repair cannot clear active native flags");
Check(P.Evaluate(S(M.FollowPath,D.Follow,mission:true))==F.MissionOwnsMotion,"leader reprime cannot overwrite pending typed ingress");
Check(P.Evaluate(S(M.FollowPath,D.Follow,mission:true,epoch:true))==F.None,"same lifecycle terminal recovery may navigate unconsumed debt");
Check(P.Evaluate(S(M.FollowPath,D.Follow,canonical:true,epoch:true))==F.CanonicalOwnsMotion,"recovery marker cannot borrow live canonical producer");
Check(P.Evaluate(S((M)900,D.Follow))==F.WrongOwner,"unknown mode cannot gain repair authority");
var intent=S(M.FollowPath,D.Unknown,origin:0,live:0);
Check(P.EvaluateFresh(intent)==F.None&&P.Evaluate(intent)==F.MissingOrigin,"fresh intent approval is not physical scope issuance");
foreach(var owner in Enum.GetValues<D>())Check(P.EvaluateFresh(S(M.FollowPath,owner,origin:0,live:0))==(owner==D.Teleport?F.Preempted:owner is D.Unknown or D.Follow or D.Recovery?F.None:F.WrongOwner),"fresh preflight retains one existing authority issuer "+owner);
Check(P.EvaluateFresh(S(M.FollowPath,D.Follow,native:true))==F.NativeOwnsMotion,"fresh Follow cannot supersede native during acquisition");
Check(P.EvaluateFresh(S(M.FollowPath,D.Follow,canonical:true))==F.CanonicalOwnsMotion,"fresh Follow cannot supersede canonical producer");
Check(P.EvaluateFresh(S(M.FollowPath,D.Follow,mission:true))==F.MissionOwnsMotion,"fresh leader path cannot skip outstanding ingress");
Check(P.EvaluateFresh(S(M.FollowPath,D.Follow,blocked:true))==F.Preempted,"medical/combat/command preemption precedes Follow acquisition");
Check(P.EvaluateFresh(S(M.ProbeRecovery,D.Traversal))==F.WrongOwner,"preflight helper does not authorize another mode");
Check(P.Evaluate(S(M.TeleportQuiescence,D.Teleport,native:true,canonical:true,mission:true,blocked:true))==F.None,"issued teleport may neutralize stale motion even during old native state");
Check(P.Evaluate(S(M.GeneralRecovery,D.Teleport))==F.WrongOwner,"forward repair never runs under Teleport owner");
Check(P.Evaluate(S(M.GeneralRecovery,D.PostTraversal))==F.None,"orphan landing may repair under its existing post-traversal owner");
Check(P.Evaluate(S(M.GeneralRecovery,D.PostTraversal,canonical:true))==F.CanonicalOwnsMotion,"post-traversal owner cannot bypass active canonical producer");
File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=true,checks=checks.Count,cases=checks,permissionCombinations=1024,physicsSimulated=false,scope="typed Follow/native/teleport recovery ownership"},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"PASS {checks.Count} candidate Follow/recovery checks (1024 ownership combinations)");
