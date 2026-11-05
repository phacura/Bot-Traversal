param([string]$Dll,[string]$Output)
$ErrorActionPreference='Stop'
[Reflection.Assembly]::LoadFrom('D:/Games/SIT_DIR/mod or addon builds/dnSpy-net-win64/bin/dnlib.dll')|Out-Null
$m=[dnlib.DotNet.ModuleDefMD]::Load((Resolve-Path $Dll).Path)
try {
$r=$m.GetTypes()|Where-Object Name -eq 'FollowerTraversalTestRuntime'
$methodLookup=@{};foreach($method in $r.Methods){$key=[string]$method.Name;if(!$methodLookup.ContainsKey($key)){$methodLookup[$key]=$method}}
$callLookup=@{}
function M($n){$methodLookup[$n]}
function C($f,$n){
 if(!$f -or !$f.HasBody){return @()}
 $key=[string]$f.MDToken.Raw
 if(!$callLookup.ContainsKey($key)){
  $calls=@{};foreach($instruction in $f.Body.Instructions){if($instruction.OpCode.Name -in @('call','callvirt')){
   $name=[string]$instruction.Operand.Name;if(!$calls.ContainsKey($name)){$calls[$name]=[System.Collections.Generic.List[object]]::new()};$calls[$name].Add($instruction)
  }};$callLookup[$key]=$calls
 }
 if($callLookup[$key].ContainsKey($n)){@($callLookup[$key][$n].ToArray())}else{@()}
}
$checks=0
function Check($v,$n){$script:checks++;if(!$v){throw $n}}
$f=M 'TryRecoverPlayerValidatedTraversal';$failure=@(C $f 'TryRequestCapabilityFullRouteRetry')[-1]
Check ((C $f 'FarSideRecoveryAccepted')[0].Offset -lt $failure.Offset) 'real far-side completion before failure'
Check ((C $f 'TryProveLearningV441JumpTypedSuccessorFrontier')[0].Offset -lt $failure.Offset) 'typed successor before failure'
Check ((C $f 'TryBeginNativeTraversalRunOut')[0].Offset -lt $failure.Offset) 'physical native run-out before failure'
Check ((C $f 'TryBeginLearningV408JumpPartialCrossingForwardContinuation')[0].Offset -lt $failure.Offset) 'physical partial Jump continuation before failure'
$f=M 'TryBeginAssistedTraversalReplay'
Check ((C $f 'TryDispatchCapabilitySequenceNative')[0].Offset -lt (C $f 'TryBeginLearnedNativeJump')[0].Offset) 'selected executor before legacy semantic re-selection'
Check ((C $f 'TryDispatchCapabilitySequenceNative')[0].Offset -lt (C $f 'IsPlayerValidatedTraversalProbe')[0].Offset) 'exact live strategy re-entry before persistent legacy history veto'
Check ((C $f 'TryRequestCapabilityFullRouteRetry')[0].Offset -lt (C $f 'TryDispatchCapabilitySequenceNative')[0].Offset) 'consumed or rejected entry routes to failure sequence'
$f=M 'UpdateSharedNativeEntryAcquisition';$fields=@($f.Body.Instructions|Where-Object OpCode -ne $null|ForEach-Object{$_.Operand.Name})
Check ($fields -contains 'SkillTemplateEdgeId' -and $fields -contains 'SkillTransferTransient') 'physical template supplies entry frontier'
# The obstacle-specific fast consume branch must occur between ready-candidate logging and native start.
$i=@($f.Body.Instructions);$ready=($i|Where-Object{$_.OpCode.Name -eq 'ldstr' -and $_.Operand -eq 'learningV32NativeEntryReadyCandidate'})[0].Offset
$consume=(C $f 'MarkLearningV32NativeEntryAcquired'|Where-Object Offset -gt $ready|Select-Object -First 1).Offset
Check (@($i|Where-Object{$_.Offset -gt $ready -and $_.Offset -lt $consume -and $_.Operand.Name -eq 'NativeStrategySelected'}).Count -eq 1) 'selected obstacle can consume same ready sample'
$f=M 'TickCapabilityFailureSequence'
Check ((C $f 'TryStartProbeForBot').Count -eq 1) 'selected strategy owns dispatch'
Check ((C $f 'TickCapabilityFailureSequence').Count -eq 1) 'return failure processes terminal recovery in same callback'
foreach($n in @('TryValidateV2CurrentTypedStep','TryReadV2LiveRouteAdmission','TryStartProbeForBot')){Check ((C (M $n) 'IsCapabilityRecoveryPending').Count -eq 1) "$n blocks terminal recovery ingress"}
Check ((C (M 'TickCapabilityFailureSequence') 'TryEnterBoundPhysicalMotion').Count -eq 1) 'post-teleport callback stays in issued origin scope'
$f=M 'DriveLearningV32NativeEntryAcquisition';$ins=@($f.Body.Instructions)
Check (@($ins|Where-Object{$_.Operand.Name -eq 'GetFlatDirection'}).Count -ge 2 -and @($ins|Where-Object{$_.Operand.Name -eq 'get_Position'}).Count -ge 2) 'entry correction steers from body toward actual mover target'
$f=M 'TryRecoverPlayerValidatedTraversal';Check ((C $f 'ContinueExecutedJump')[0].Offset -lt $failure.Offset) 'normal executed Jump continuation precedes terminal failure'
$f=M 'TryRecoverPlayerValidatedTraversal';$policy=(C $f 'ContinueExecutedJump')[0].Offset;$runout=(C $f 'TryBeginNativeTraversalRunOut')[0].Offset
Check (@($f.Body.Instructions|Where-Object{$_.Offset -gt $runout -and $_.Offset -lt $policy -and $_.Operand.Name -eq 'NativeLandingRejected'}).Count -eq 1) 'landing rejection is a policy argument, never an outer prerequisite'
$f=M 'TryStartProbeForBot';Check ((C $f 'TryAdmitCapabilitySequenceProbe')[0].Offset -lt (C $f 'GetFlatDirection')[0].Offset) 'foreign probe blocked before allocation and movement'
$f=M 'TryGetLearnedTraversalFollowTarget';Check ((C $f 'HasCapabilityFailureStrategy')[0].Offset -lt (C $f 'PruneLearnedTraversalEdges')[0].Offset) 'Follow selector yields to selected physical attempt before ordinary selection'
$f=M 'UpdateLearningV375PersistentExecutablePhysicalRouteTransaction';Check ((C $f 'HasCapabilityFailureStrategy')[0].Offset -lt (C $f 'GetOrCreateLearningV375RouteCursor')[0].Offset) 'canonical producer yields before cursor or target refresh'
$f=M 'TryGetLearningV353JumpRouteFunnelTarget';Check ((C $f 'HasCapabilityFailureStrategy').Count -eq 1) 'old semantic Jump preparation cannot steer a selected Vault or Climb'
$f=M 'TryReadV2LiveRouteAdmission';Check ((C $f 'IsCapabilityStrategyPending').Count -eq 1) 'pending strategy denies old route scope in the same frame'
$f=M 'TryAdmitCapabilitySequenceProbe';Check ((C $f 'CanAllocateStrategy').Count -eq 1) 'production allocation consumes tested exact-source policy'
$f=M 'HasCapabilityFailureStrategy';Check ((C $f 'OwnsStrategy').Count -eq 1) 'strategy exclusivity validates semantic current-step identity'
$f=M 'EscalateTraversalFailureMonotonic';Check ((C $f 'TryRequestCapabilityFullRouteRetry')[0].Offset -lt (C $f 'IsPlayerValidatedTraversalProbe')[0].Offset) 'direct monotonic failure enters ladder before legacy branches'
$f=M 'TryTeleportPlayerValidatedTraversalAtExit';Check ((C $f 'TryRequestCapabilityFullRouteRetry')[0].Offset -lt (C $f 'PrepareProbeForTeleportCommit')[0].Offset) 'route failure intercepted before destructive teleport preparation'
$f=M 'TryBeginPlayerRouteContinuationBridge';Check ((C $f 'TryRequestCapabilityFullRouteRetry').Count -eq 1) 'direct route bridge failure enters shared ladder'
$f=M 'UpdateUnifiedTraversalVerify';Check ((C $f 'TryRetainCapabilityObservedJumpBeforePassageBridge')[0].Offset -lt (C $f 'TryBeginPlayerRouteContinuationBridge')[0].Offset) 'retain observed actuator before grounded bridge can consume its work'
$f=M 'TryRetainCapabilityObservedJumpBeforePassageBridge';Check ((C $f 'TryLatchLearningV372WalkThroughPhysicalPassageProof')[0].Offset -lt (C $f 'CanClosePassageBoundary')[0].Offset) 'refresh existing finite WT witness before bounded handoff, never infer proof from NavMesh alone'
$f=M 'TryRetainCapabilityObservedJumpBeforePassageBridge';Check ((C $f 'CanClosePassageBoundary')[0].Offset -lt (C $f 'InsertLifecycleMissionStep')[0].Offset) 'existing finite supported clean passage proof required before inserting Jump debt'
Check ((C $f 'TryFinalizeTraversalChainIntermediate').Count -eq 1 -and (C $f 'CommitUnifiedTraversalSuccess').Count -eq 0) 'existing lifecycle resolver alone commits boundary completion'
$f=M 'TrySelectCapabilitySequenceStrategy';Check ((C $f 'PastObstacleEntry')[0].Offset -lt (C $f 'CalculateStrictPath')[0].Offset) 'already-passed native obstacle entry cannot become alternate'
$f=M 'UpdateSharedNativeEntryAcquisition';Check ((C $f 'EntryBudget')[0].Offset -lt (C $f 'UpdateSharedNativeEntryAcquisition_acquisitionExpired_5')[0].Offset) 'DropDown approach receives bounded initial-distance budget'
Check (@($f.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldstr' -and $_.Operand -eq 'capabilityNativeEntryAdmissionBlocked'}).Count -eq 2) 'EFT entry refusals have explicit evidence telemetry'
$f=M 'DriveLearningV32NativeEntryAcquisition';Check (@($f.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldarg.3'}).Count -eq 0) 'unacquired DropDown no longer uses raw far landing as mover destination'
$f=M 'TryRequestCapabilityFullRouteRetry';Check ((C $f 'IsCancellation').Count -eq 1) 'unavailable or teleport-labelled execution failures no longer silently bypass ladder'
$f=M 'TryStartProbeForBot';Check ((C $f 'TryPrepareCapabilityAutomaticProbeMission')[0].Offset -lt (C $f 'TickCapabilityPortalWait')[0].Offset) 'automatic recorded probe binds mission before queue admission and allocation'
$f=M 'TryPrepareCapabilityAutomaticProbeMission';Check (@($f.Body.Instructions|Where-Object{$_.Operand.Name -eq 'Kind' -and $_.Operand.DeclaringType.Name -eq 'LearnedTraversalCommitment'}).Count -eq 0) 'automatic mission preserves recorded semantic WHAT instead of replacing it with physical HOW'
$f=M 'TryPrepareCapabilityAutomaticProbeMission';Check ((C $f 'IndexOf').Count -eq 0) 'automatic recorded action cannot claim diagnostic exemption by source text'
$f=M 'TryPrepareCapabilityAutomaticProbeMission';Check ((C $f 'RegisterCapabilityLocalMission').Count -eq 1 -and (C $f 'TryRequestCapabilityFullRouteRetry').Count -eq 1) 'typed native admission rejection is a bound explicit failure, never an unbound legacy probe'
$f=M 'TickCapabilityPortalWait';Check ((C $f 'RegisterCapabilityLocalMission')[0].Offset -lt (C $f 'Admit')[0].Offset) 'eligible nearer followers are enrolled before ticket election'
Check ((C $f 'HeadStalled').Count -eq 1) 'pre-native head has a measured finite progress bound'
$f=M 'ReleaseCapabilityPortalWait';Check (@($f.Body.Instructions|Where-Object{$_.Operand.Name -eq 'PendingAt'}).Count -gt 0) 'queue wait suspends selected dispatch timeout rather than recording a false failure'
$f=M 'TickCapabilityFailureSequence';Check ((C $f 'IsCapabilityPortalWaiting')[0].Offset -lt (C $f 'TryStartProbeForBot')[0].Offset) 'selected strategy must respect queue wait before its own deadline/dispatch'
foreach($name in @('TryReadV2LiveRouteAdmission','TryValidateV2CurrentTypedStep','TryGetLearnedTraversalFollowTarget','UpdateLearningV375PersistentExecutablePhysicalRouteTransaction','TryMaintainCapabilityNativeInput','IsCapabilityRetryWalking')){
 Check ((C (M $name) 'IsCapabilityPortalWaiting').Count -gt 0 -or ($name -eq 'TryGetLearnedTraversalFollowTarget' -and (C (M $name) 'TickCapabilityPortalWait').Count -gt 0)) ("queue ownership reaches "+$name)
}
$authority=$m.GetTypes()|Where-Object Name -eq 'FollowerMovementWriterAuthority';$follow=$authority.Methods|Where-Object Name -eq 'TryEnterFollowPhysicalMotion'|Select-Object -First 1
Check ((C $follow 'IsCapabilityPortalWaiting').Count -eq 1) 'ordinary Follow scope cannot bypass queue owner'
$f=M 'DriveLearningV32NativeEntryAcquisition';Check ((C $f 'EntryRequiresPointAim').Count -eq 1) 'angle correction cannot aim at the historical point behind the body'
$f=M 'TryTeleportPlayerValidatedTraversalAtExit';Check ((C $f 'PrepareProbeForTeleportCommit').Count -eq 1) 'quiescence delay no longer destructively prepares its own updater'
$f=M 'RestoreTraversalMovementAuthorityAfterTeleportAttempt';Check ((C $f 'IsActiveProbe')[0].Offset -lt (C $f 'BeginTraversal')[0].Offset) 'retired probe cannot recreate traversal after teleport rejection'
$q=M 'TickCapabilityPortalWait';Check ((C $q 'IsFollowerRouteMissionStepPhysicallyPassed')[0].Offset -lt (C $q 'Admit')[0].Offset) 'completed passage resolves before old portal queue'
$soft=$m.GetTypes()|Where-Object Name -eq 'GenericSoftCollision';$fixed=$soft.Methods|Where-Object Name -eq 'FixedUpdate'
Check ((C $fixed 'TryICharacterControllerMove').Count -eq 1) 'stationary local-player push retained'
$friendly=$m.GetTypes()|Where-Object Name -eq 'FollowerFriendlyAvoidancePatch';Check ((C ($friendly.Methods|Where-Object Name -eq 'SkipFriendlyPair') 'Owns').Count -eq 2) 'friendly mover repulsion still suppressed'
foreach($pair in @(
 @('EscalateTraversalFailureMonotonic','TryRequestCapabilityFullRouteRetry'),
 @('TryTeleportPlayerValidatedTraversalAtExit','TryRequestCapabilityFullRouteRetry'),
 @('TryBeginPlayerRouteContinuationBridge','TryRequestCapabilityFullRouteRetry'),
 @('UpdateUnifiedTraversalVerify','TryRetainCapabilityObservedJumpBeforePassageBridge'),
 @('TryRetainCapabilityObservedJumpBeforePassageBridge','CanClosePassageBoundary'),
 @('TryRetainCapabilityObservedJumpBeforePassageBridge','TryFinalizeTraversalChainIntermediate'),
 @('TrySelectCapabilitySequenceStrategy','PastObstacleEntry'),
 @('UpdateSharedNativeEntryAcquisition','EntryBudget'),
 @('TryRequestCapabilityFullRouteRetry','IsCancellation'),
 @('TryRetainCapabilityObservedJumpBeforePassageBridge','TryLatchLearningV372WalkThroughPhysicalPassageProof'))){
 Check ((C (M $pair[0]) $pair[1]).Count -gt 0) ("required host connection: "+$pair[0]+" -> "+$pair[1])
}
foreach($pair in @(
 @('TryStartProbeForBot','TryPrepareCapabilityAutomaticProbeMission'),
 @('TryPrepareCapabilityAutomaticProbeMission','RegisterCapabilityLocalMission'),
 @('TryPrepareCapabilityAutomaticProbeMission','TryRequestCapabilityFullRouteRetry'),
 @('TickCapabilityPortalWait','RegisterCapabilityLocalMission'),
 @('TickCapabilityPortalWait','HeadStalled'),
 @('TryReadV2LiveRouteAdmission','IsCapabilityPortalWaiting'),
 @('TryValidateV2CurrentTypedStep','IsCapabilityPortalWaiting'),
 @('UpdateLearningV375PersistentExecutablePhysicalRouteTransaction','IsCapabilityPortalWaiting'),
 @('TryMaintainCapabilityNativeInput','IsCapabilityPortalWaiting'),
 @('IsCapabilityRetryWalking','IsCapabilityPortalWaiting'),
 @('DriveLearningV32NativeEntryAcquisition','EntryRequiresPointAim'),
 @('RestoreTraversalMovementAuthorityAfterTeleportAttempt','IsActiveProbe'),
 @('TickCapabilityFailureSequence','IsCapabilityPortalWaiting'))){
 Check ((C (M $pair[0]) $pair[1]).Count -gt 0) ("required queue connection: "+$pair[0]+" -> "+$pair[1])
}
$f=M 'TryAdvanceRouteMission';Check ((C $f 'Mark').Count -eq 1 -and (C $f 'Resolve')[0].Offset -lt (C $f 'Mark')[0].Offset) 'terminal receipt follows sole applied lifecycle resolution'
foreach($name in @('RegisterCapabilityLocalMission','TryPrepareCapabilityAutomaticProbeMission','IsTraversalEpisodeAlreadyConsumed','IsLearningV366RuntimeEpisodeBehindExecutionFrontier')){Check ((C (M $name) 'IsCapabilityDebtResolved').Count -gt 0) ('resolved debt excluded at '+$name)}
$f=M 'TryGetLearnedTraversalFollowTarget';Check ((C $f 'PruneCapabilityResolvedCommitment').Count -eq 1) 'selector clears completed destination before queue'
$f=M 'TickCapabilityPortalWait';foreach($n in @('Owns','InWindow','IsProbeLifecycleCurrent')){Check ((C $f $n).Count -gt 0) ('native ticket and stable window '+$n)}
$bridge=$m.GetTypes()|Where-Object Name -eq 'LegacyOriginalPITFollowBridge';$f=$bridge.Methods|Where-Object Name -eq 'TryRejectOriginalPITFollowWrite'|Select-Object -First 1
foreach($n in @('CanCommitScopedPhysicalMotion','BlockOriginalFollowDestination','OwnsCapabilityLocalMotion','IsOriginalPITFollowCall')){Check ((C $f $n).Count -gt 0) ('selective original Follow guard '+$n)}
$patch=$m.GetTypes()|Where-Object Name -eq 'FollowerMovementWriterAuthorityPatch';foreach($name in @('PatchBotMoverDestination','PatchPointDataDestination')){$f=$patch.Methods|Where-Object Name -eq $name|Select-Object -First 1;Check ((C $f 'TryRejectOriginalPITFollowWrite').Count -eq 1) ('lower destination boundary '+$name)}
$f=$patch.Methods|Where-Object Name -eq 'Apply'|Select-Object -First 1;$strings=@($f.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldstr'}|ForEach-Object{$_.Operand})
foreach($n in @('GoToPointNoWay','SetPointOnWay','SetPoint','UpdateToGo')){Check ($strings -contains $n) ('actual destination hook registration '+$n)}
$f=M 'BeginWalkThroughProbe';Check ((C $f 'TryRequestCapabilityFullRouteRetry').Count -gt 0 -and (C $f 'TryRequestCapabilityFullRouteRetry')[0].Offset -lt (C $f 'ClearLearnedTraversalCommitment')[0].Offset) 'WT hard validation offers bound failure ladder before legacy cleanup'
$f=M 'FinishProbeWithoutReanchor';$call=(C $f 'TryRequestCapabilityFullRouteRetry')[0];$idx=$f.Body.Instructions.IndexOf($call);Check ($idx -gt 0 -and $f.Body.Instructions[$idx-1].OpCode.Name -eq 'ldc.i4.1') 'every bound unpublished terminal routes to failure without string classifier veto'
$f=M 'UpdateWalkThroughEntryConvergence';Check ((C $f 'TryRequestCapabilityFullRouteRetry').Count -gt 0 -and (C $f 'TryRequestCapabilityFullRouteRetry')[0].Offset -lt (C $f 'TryRetireNativeMoverPathPreserveLocomotion')[0].Offset) 'WT escape offers sequence before destroying its mover and mission'
$f=M 'TryAdvanceRouteMission';Check ((C $f 'CommitCapabilityResolvedNavigation').Count -eq 1 -and (C $f 'Resolve')[0].Offset -lt (C $f 'CommitCapabilityResolvedNavigation')[0].Offset) 'navigation reconciliation follows sole applied resolution'
$f=M 'CommitCapabilityResolvedNavigation';Check ((C $f 'ResolvedNavigationFloor').Count -eq 1 -and (C $f 'CancelDelivery').Count -eq 1) 'completed prefix clamps navigation and retires delivery'
foreach($name in @('TryGetLearnedTraversalFollowTarget','UpdateLearningV375PersistentExecutablePhysicalRouteTransaction')){Check ((C (M $name) 'TryGetCapabilityPortalApproachTarget').Count -gt 0) ('one ticket entry approach reaches '+$name)}
$f=M 'TryGetCapabilityPortalApproachTarget';foreach($n in @('Owns','Approach','IsCapabilityDebtResolved')){Check ((C $f $n).Count -eq 1) ('approach stays current and exclusive '+$n)}
$f=M 'TickCapabilityPortalWait';Check ((C $f 'CancelDelivery').Count -gt 0) 'release invalidates stopped delivery cache'
$f=M 'UpdateSharedNativeEntryAcquisition';Check ((C $f 'AngularProgress').Count -eq 1) 'entry acquisition consumes measured angular progress without changing native admission'
$f=M 'TryValidateCapabilityPortalApproachPath';foreach($n in @('TryGetCapabilityPortalApproachTarget','CalculateStrictPath','LocalApproachPath')){Check ((C $f $n).Count -eq 1) ('current ticket delivery preflight '+$n)}
$f=$bridge.Methods|Where-Object Name -eq 'Tick'|Select-Object -First 1;foreach($n in @('TryGetCapabilityPortalApproachTarget','TryValidateCapabilityPortalApproachPath')){Check ((C $f $n).Count -eq 1) ('original bridge ticket handoff '+$n)}
$f=M 'TryGetCapabilityPortalApproachTarget';Check ((C $f 'IsFollowerRouteMissionStepPhysicallyPassed').Count -eq 1 -and (C $f 'IsFollowerRouteMissionStepPhysicallyPassed')[0].Offset -lt (C $f 'Approach')[0].Offset) 'physical passed proof yields to existing resolver before any backward entry approach'
@{passed=$true;checks=$checks;dllSha256=(Get-FileHash $Dll -Algorithm SHA256).Hash;physicsSimulated=$false}|ConvertTo-Json|Set-Content -Encoding UTF8 $Output
Write-Output "PASS $checks RC128 actual DLL sequence checks"

} finally {$m.Dispose()}
