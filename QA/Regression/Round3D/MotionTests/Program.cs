using M=BotTraversal.Core.FollowerCapabilityMotionPolicy;
using G=BotTraversal.Core.FollowerCapabilityDeliveryPolicy;
using L=BotTraversal.Core.FollowerTraversalLifecycle;
using C=BotTraversal.Core.CapabilityMotionCommit;
using K=BotTraversal.Core.CapabilityMotionCommitKind;
using System.Text.Json;
var checks=new List<string>();void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);checks.Add(name);}
M.Plan Plan(G.Capability kind=G.Capability.DropDown,bool exact=true,bool active=false,bool grounded=false,bool walk=true,bool strict=false,bool actuator=true,bool admissible=false,bool matching=false,bool retry=true,float noProgress=1.1f,bool goal=true,bool moving=true,bool aligned=true,bool prior=true,bool missed=true,bool down=true,float distance=1.38625085f,float maximum=5.5f,bool owns=false) =>
 M.Evaluate(new(new(kind,exact,active,grounded,walk,strict,actuator,admissible,matching,retry,noProgress,3f),goal,moving,aligned,prior,missed,down,distance,maximum,owns));
// Recorded geometry: ungrounded Run, NavMesh start absent, 0.7205m beyond entry,
// 0.3810m lateral, existing forward overshoot limit .16 and lane .42.
Check(M.MissedDropEntry(.720476866f,.381011218f,-.096453425f,.16f,.42f,.45f),"recorded already-past-entry body is eligible without pretending grounded");
Check(!M.MissedDropEntry(.1f,.1f,-.1f,.16f,.42f,.45f),"valid native entry is not bypassed");
Check(!M.MissedDropEntry(.8f,.6f,-.1f,.16f,.42f,.45f),"wide lateral body cannot borrow narrow successor continuation");
Check(M.MissedDropEntry(.1f,.1f,-.6f,.16f,.42f,.45f),"prior lower-body ground rejoin remains eligible");
Check(Plan().Kind==M.Delivery.PublishPhysicalWay,"recorded unsupported Run gets native physical consumer");
Check(Plan(grounded:true,strict:true).Kind==M.Delivery.PublishGroundWay,"proven ground route retains its corners");
Check(Plan(active:true).Kind==M.Delivery.PreserveActuator,"active native probe retains body ownership");
Check(Plan(missed:false,admissible:true).Kind==M.Delivery.PreserveActuator,"admissible DropDown entry retained");
Check(Plan(prior:false).Kind==M.Delivery.PreserveActuator,"no predecessor proof cannot create continuation");
Check(Plan(exact:false).Kind==M.Delivery.Blocked,"foreign or stale identity rejected");
Check(Plan(aligned:false).Kind==M.Delivery.Blocked,"delivery cannot substitute a different direction");
Check(Plan(walk:false).Kind==M.Delivery.Blocked,"disabled native locomotion is diagnosed, never forced");
Check(Plan(goal:false).Kind==M.Delivery.PreserveActuator,"no implicit target invented");
Check(Plan(moving:false).Kind==M.Delivery.PreserveActuator,"hold frame cannot create a moving path");
Check(Plan(noProgress:.5f).Kind==M.Delivery.PreserveActuator,"healthy airborne motion not interrupted speculatively");
Check(Plan(retry:false).Kind==M.Delivery.Blocked,"actual retry budget respected");
Check(Plan(distance:6f).Kind==M.Delivery.Blocked,"physical goal limited to existing local horizon");
Check(Plan(distance:float.NaN).Kind==M.Delivery.Blocked,"invalid distance rejected");
Check(Plan(maximum:float.NaN).Kind==M.Delivery.Blocked,"invalid horizon rejected");
Check(Plan(noProgress:float.NaN).Kind==M.Delivery.Blocked,"invalid observed time cannot authorize path");
Check(Plan(down:false).Kind==M.Delivery.PreserveActuator,"unsupported upward move needs its native actuator");
foreach(var kind in new[]{G.Capability.Jump,G.Capability.Climb,G.Capability.Vault})Check(Plan(kind:kind).Kind==M.Delivery.PreserveActuator,kind+" actuator not bypassed by common delivery");
Check(Plan(kind:G.Capability.WalkThrough,actuator:false).Kind==M.Delivery.PublishPhysicalWay,"WT uses same delivery adapter with its explicit goal");
Check(Plan(matching:true,owns:true,noProgress:.2f).Kind==M.Delivery.KeepNativeWay,"progress does not retire native consumer each frame");
var stalled=Plan(matching:true,owns:true,noProgress:4f);Check(stalled.Kind==M.Delivery.Blocked&&stalled.KeepExistingConsumer,"stalled consumer keeps debt and native mover for existing recovery");
var waiting=Plan(matching:false,owns:true,retry:false,noProgress:.2f);Check(waiting.Kind==M.Delivery.Blocked&&waiting.KeepExistingConsumer,"moving native consumer kept during bounded retarget cooldown");
Check(Plan(owns:true,grounded:true,strict:true,noProgress:.2f).Kind==M.Delivery.PublishGroundWay,"existing moving ground consumer may update an explicit goal without new stall clock");
// Delivery acknowledgement cannot be treated as exclusive manual input or progress.
var native=new C(K.NativeWayAccepted,true);Check(native.Accepted&&!native.UsesExclusiveManualInput&&native.NativeMoverOwned,"native receipt cannot trigger caller manual Pause gate");
var manual=C.ManualFrame;Check(manual.Accepted&&manual.UsesExclusiveManualInput&&!manual.NativeMoverOwned,"manual frame retains existing exclusive input behavior");
var blocked=new C(K.Blocked,true);Check(!blocked.Accepted&&!blocked.UsesExclusiveManualInput&&blocked.NativeMoverOwned,"blocked path is not success and not a manual pause request");
Check(!typeof(C).GetMethods().Any(m=>m.Name=="op_Implicit"),"typed acknowledgement has no implicit bool conversion");
L.Identity Id(string bot="b",int raid=1,int mission=198,int step=3,int episode=29,int edge=78,int chain=26,int geometry=4,long writer=16)=>new(bot,raid,mission,step,episode,edge,chain,geometry,writer);
var origin=Id();Check(M.CanRetainConsumer(origin,origin,true,true,false),"exact installed resource can be retained");
foreach(var changed in new[]{Id(bot:"other"),Id(raid:2),Id(mission:199),Id(step:2),Id(episode:30),Id(edge:79),Id(chain:27),Id(geometry:5),Id(writer:17)})Check(!M.CanRetainConsumer(origin,changed,true,true,false),"changed lifecycle identity invalidates resource receipt "+changed.GetHashCode());
Check(!M.CanRetainConsumer(origin,origin,false,true,false),"replaced native path pointer invalidates receipt");
Check(!M.CanRetainConsumer(origin,origin,true,false,false),"completed/retired native path cannot be kept alive");
Check(!M.CanRetainConsumer(origin,origin,true,true,true),"native probe acquisition retires physical delivery receipt");
// Native path admission does not consume DropDown debt.
var mission=new L.MissionState();var step=new L.StepState();var id=Id(step:0);
Check(Plan().Kind==M.Delivery.PublishPhysicalWay&&step.DebtOutstanding&&mission.Cursor==0,"path request cannot resolve route debt");
Check(mission.Resolve(step,id,step,id,L.Resolution.Subsumed,false,true)==L.CommitResult.Rejected,"no body movement remains unqualified evidence");
File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=true,checks=checks.Count,cases=checks,physicsSimulated=false,canWalkInRecordedShapeAssumed=true},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"PASS {checks.Count} compiled motion delivery/lifetime/typed-ack checks");
