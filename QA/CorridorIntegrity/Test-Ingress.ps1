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
$f=$bridge.Methods|Where-Object Name -eq 'Tick'|Select-Object -First 1
foreach($n in @('TryReleaseCapabilityResolvedAuthority','TryPrepareCapabilityPortalApproachDelivery')){Check ((C $f $n).Count -eq 1 -and (C $f $n)[0].Offset -lt (C $f 'GetCurrentGeneration')[0].Offset) ('owner handoff precedes bridge domain veto '+$n)}
$f=M 'TryPrepareCapabilityPortalApproachDelivery';foreach($n in @('MayYieldApproach','TryValidateCapabilityPortalApproachPath','TryRetireNativeMoverPathPreserveLocomotion','ClearCapabilityNativeDestinationData','EndLearningV375PersistentRouteTransaction','TransitionTraversalToFollowIfIdleExact','TransitionPostTraversalToFollowIfCurrent')){Check ((C $f $n).Count -eq 1) ('queue handoff exact boundary '+$n)}
$f=M 'ReconcileCapabilityRetiredProbeConsumer';foreach($n in @('MayRetireConsumer','TryRetireNativeMoverPathPreserveLocomotion','ClearCapabilityNativeDestinationData')){Check ((C $f $n).Count -eq 1) ('completed physical consumer retirement '+$n)}
$f=M 'UnregisterActiveProbe';Check ((C $f 'ReconcileCapabilityRetiredProbeConsumer').Count -eq 1 -and (C $f 'UninstallLifecycleProbe')[0].Offset -lt (C $f 'ReconcileCapabilityRetiredProbeConsumer')[0].Offset) 'old body consumer retires after uninstall, preserving active successor'
$f=M 'TrySelectCapabilitySequenceStrategy';Check ((C $f 'CapabilitySelfRescueFrontier').Count -eq 2) 'learned and scanner rescue both use actual frontier policy'
$f=M 'CapabilitySelfRescueFrontier';Check ((C $f 'RescueAtBody').Count -eq 1) 'safe grounded actual rescue frontier does not chase old entry'
$f=M 'ClearCapabilityNativeDestinationData';Check ((C $f 'CanCommitScopedPhysicalMotion').Count -eq 1 -and (C $f 'BotGoToPointDataSetPoint').Count -eq 1 -and (C $f 'BotGoToPointDataUpdateToGo').Count -eq 1) 'shared native goal clear reuses two scoped writers'
$f=M 'TryReleaseCapabilityResolvedAuthority';foreach($n in @('HasWriter','MayReleaseResolvedAuthority','HasCapabilityUnresolvedRouteWork','ClearCapabilityNativeDestinationData','TransitionTraversalToFollowIfIdleExact')){Check ((C $f $n).Count -eq 1) ('terminal release exact resolved writer '+$n)}
$f=M 'TryAdvanceRouteMission';Check ((C $f 'MarkWriter').Count -eq 1 -and (C $f 'Resolve')[0].Offset -lt (C $f 'MarkWriter')[0].Offset) 'writer receipt follows applied semantic resolution'
$f=M 'TickCapabilityFailureSequence';Check ((C $f 'DriveCapabilityStrategyApproach').Count -eq 1 -and (C $f 'DriveCapabilityStrategyApproach')[0].Offset -lt (C $f 'TryStartProbeForBot')[0].Offset) 'one selected strategy driver approaches before allocating actual physical attempt'
$f=M 'TryValidateCapabilityPortalApproachPath';Check ((C $f 'PrefixPath').Count -eq 1) 'approach protects the completed prefix instead of forbidding every local backward correction'
$f=M 'ClearCapabilityStrategyTargets';Check ((C $f 'ClearCapabilityNativeDestinationData').Count -eq 1 -and (C $f 'TryRetireNativeMoverPathPreserveLocomotion').Count -eq 1) 'failure stage clears physical destination as well as dictionaries after body ownership is safe'
$f=M 'TickCapabilityFailureSequence';Check ((C $f 'StrategyEntryReady').Count -eq 2 -and (C $f 'StrategyEntryReady')[0].Offset -lt (C $f 'TryStartProbeForBot')[0].Offset) 'no selected probe allocation before actual 3D physical entry'
$f=M 'TryGetCapabilityPortalApproachTarget';Check ((C $f 'Distance').Count -eq 1) 'selected approach includes vertical distance while ordinary native keeps its window'
foreach($name in @('TryStartProbeForBot','TryGetLearnedTraversalFollowTarget','TryGetLearningV353JumpRouteFunnelTarget','TryCaptureCapabilityJumpPreparation','UpdateLearningV375PersistentExecutablePhysicalRouteTransaction')){Check ((C (M $name) 'IsCapabilityReturnInProgress').Count -gt 0) ('return driver exclusive at '+$name)}
$f=$bridge.Methods|Where-Object Name -eq 'Tick'|Select-Object -First 1;Check ((C $f 'IsCapabilityReturnInProgress').Count -eq 1 -and (C $f 'IsCapabilityReturnInProgress')[0].Offset -lt (C $f 'TryGetCapabilityPortalApproachTarget')[0].Offset) 'legacy bridge cannot reissue normal ingress while returning'
$f=M 'IsCapabilityRetryWalking';Check ((C $f 'NeedsCapabilityEntryRun').Count -eq 1) 'Run is scoped to actual entry correction'
$f=M 'OnBotOwnerUpdate';Check ((C $f 'ApplyCapabilitySequenceWalking').Count -eq 1 -and (C $f 'ApplyCapabilitySequenceWalking')[0].Offset -lt (C $f 'TickCapabilityFullRouteRetry')[0].Offset) 'inherited Sprint is neutralized before route processing'
foreach($name in @('PatchBotOwnerIngressSpeed','PatchBotMoverIngressSpeed')){$f=$patch.Methods|Where-Object Name -eq $name|Select-Object -First 1;Check ((C $f 'LimitCapabilityIngressSpeed').Count -eq 1) ('native speed write cannot rearm chase speed '+$name)}
$f=M 'TryGetCapabilityCurrentJumpGait';Check ((C $f 'TryGetCapabilityRouteSprint').Count -eq 1) 'Jump acceleration requires alignment and adequate run-up'
$f=M 'ApplyCollisionActivePhysicalRouteMotion';Check ((C $f 'TryGetCapabilityCurrentJumpGait').Count -eq 1 -and (C $f 'CommitCapabilityRouteRetryZeroInput').Count -eq 3) 'Jump alignment, explicit zero frame and posture acquisition all clear old input'
$f=M 'TickCapabilityFailureSequence';Check ((C $f 'StrategyExpired').Count -eq 1 -and (C $f 'ApplyCapabilitySequenceWalking')[0].Offset -lt (C $f 'DriveCapabilityStrategyApproach')[0].Offset) 'strategy gait precedes approach; only measured progress extends no-progress budget'
$f=M 'TryValidateCapabilityPortalApproachPath';Check ((C $f 'TryGetCapabilityCompletedPrefixBoundary').Count -eq 1 -and (C $f 'PrefixPath').Count -eq 1) 'path preservation uses resolved predecessor geometry'
$f=M 'MaintainCapabilityInputBeforeNativeConsumer';Check ((C $f 'TryDispatchCapabilityJumpAtNativeUpdate').Count -eq 1 -and (C $f 'TryDispatchCapabilityJumpAtNativeUpdate')[0].Offset -lt (C $f 'TryMaintainCapabilityNativeInput')[0].Offset) 'physical Jump dispatch sampled before native movement'
$f=M 'TryDispatchCapabilityPreparedJump';foreach($n in @('TryCaptureCapabilityJumpPreparation','ShouldDeferCapabilityJumpProbe','TryStartProbeForBot')){Check ((C $f $n).Count -eq 1) ('shared render/native Jump dispatch '+$n)}
$f=M 'TickCapabilityPreparationConsumerFrames';Check ((C $f 'TryDispatchCapabilityPreparedJump').Count -eq 1) 'render frame uses same Jump dispatcher'
$nativePatch=$m.GetTypes()|Where-Object Name -eq 'FollowerCapabilityNativeConsumerPatch';$f=$nativePatch.Methods|Where-Object Name -eq 'Prefix'|Select-Object -First 1;Check ($f.MethodSig.RetType.FullName -eq 'System.Boolean' -and (C $f 'KeepNativeMove').Count -eq 1) 'retired native movement state cannot execute after Jump changes state'
$f=$patch.Methods|Where-Object Name -eq 'PatchBotSteeringIntent'|Select-Object -First 1;Check ((C $f 'TryRejectOriginalPITFollowWrite').Count -eq 1) 'original Follow steering cannot fight current route axis'
foreach($name in @('TryGetCapabilityCurrentJumpGait','TryPrepareCapabilityAdjacentJumpRunup')){Check ((C (M $name) 'TryGetCapabilityRouteSprint').Count -eq 1) ('same sprint launch rule reaches '+$name)}
Check ((C (M 'ApplyCapabilitySequenceWalking') 'NeedsCapabilityRunTakeoff').Count -eq 1) 'Run Jump momentum floor remains separate from precise return'
Check ((C (M 'LimitCapabilityIngressSpeed') 'CapabilityIngressSpeed').Count -eq 1) 'speed setter uses distinct approach/retry policy'

$f=M 'TryAdvanceCapabilityFailureSequence';Check ((C $f 'PendingDeadline').Count -eq 1) 'measured pending deadline cannot be swallowed by legacy duplicate failure filter'
$f=M 'PrepareCapabilityAlternateReplay';Check ((C $f 'CapabilitySemanticCommitment').Count -eq 1) 'retry binds original WHAT to fresh actual root rather than previous failed episode'
foreach($name in @('Episode','Edge','ApproachObserved','DeferredProbe','LastFailureFrame')){Check (@($f.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'stfld' -and $_.Operand.Name -eq $name}).Count -eq 1) ('fresh retry resets stale failure state '+$name)}
$f=M 'CapabilityIngressSpeed';foreach($n in @('ApproachSpeed','IsCapabilityReturnInProgress')){Check ((C $f $n).Count -eq 1) ('separate ingress and return speed '+$n)}
$f=M 'TickCapabilityUnstartedNative';foreach($n in @('NativeExpired','TryRequestCapabilityFullRouteRetry','IsCapabilityRouteRetrySafe','IsFollowerRouteMissionStepPhysicallyPassed','HasActiveTraversalProbe','IsCapabilityPortalWaiting')){Check ((C $f $n).Count -eq 1) ('bounded unstarted native ownership '+$n)}
$f=M 'TickCapabilityFullRouteRetry';Check ((C $f 'TickCapabilityUnstartedNative').Count -eq 1 -and (C $f 'TickCapabilityPortalWait')[0].Offset -lt (C $f 'TickCapabilityUnstartedNative')[0].Offset) 'queue first then current unresolved native watchdog on Bot cadence'
$f=M 'CommitCapabilityPortalHold';foreach($n in @('ClearCapabilityNativeDestinationData','CancelDelivery','EndLearningV375PersistentRouteTransaction')){Check ((C $f $n).Count -eq 1) ('queue admission clears dormant old targets '+$n)}
$f=M 'ApplyCollisionActivePhysicalRouteMotion';Check ((C $f 'CommitCapabilityPortalHold').Count -eq 1 -and (C $f 'CommitCapabilityPortalHold')[0].Offset -lt (C $f 'PrepareCapabilityJumpLocomotion')[0].Offset) 'queued body cannot receive stale physical route forward input'
foreach($name in @('ShouldSuppressSetPlayerToNavMesh','ShouldSuppressNativeNavMeshBodyRelink')){Check ((C (M $name) 'ProtectCapabilityLocalBodyProjection').Count -eq 1) ('queue retry projection gate '+$name)}
$f=M 'ProtectCapabilityLocalBodyProjection';Check ((C $f 'ProtectLocalBody').Count -eq 1) 'local projection protection yields to higher priority'
foreach($name in @('TryReleaseCapabilityResolvedAuthority','ClearCapabilityStrategyTargets')){Check ((C (M $name) 'CommitCapabilityRouteRetryZeroInput').Count -eq 1) ('no obsolete input after safe no-actor handoff '+$name)}
$f=M 'OnBotOwnerUpdate';Check ((C $f 'TryReleaseCapabilityResolvedAuthority').Count -eq 1) 'resolved no-receiver cleanup does not depend on Follow polling'


$f=M 'ShouldSuppressNativeNavMeshBodyRelink';Check ((C $f 'RejectCapabilityPostCompletionProjection').Count -eq 1) 'actual native body writer rejects large recent completed-epoch projection'
$f=M 'RejectCapabilityPostCompletionProjection';foreach($n in @('RejectPostCompletionProjection','PeekExactGeneration','HasActiveTraversalProbe','IsCapabilityRouteRetrySafe')){Check ((C $f $n).Count -eq 1) ('completed-epoch body gate '+$n)}


$f=M 'UpdateWalkThroughProbe';Check (@($f.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldstr' -and $_.Operand -eq 'learningV466NearSuccessorWalkThroughExecutionYield'}).Count -eq 0) 'a near successor cannot abort an unproven WT as failure'
Check ((C $f 'TryLatchLearningV372WalkThroughPhysicalPassageProof').Count -gt 0 -and (C $f 'BeginUnifiedTraversalEgressOrVerify').Count -gt 0) 'finite WT proof still controls real handoff'
$f=M 'TryGetCapabilityRouteSprint';foreach($n in @('ExecutionSprint','GetLearningV442JumpAdmissionEntry','GetCurrentWalkThroughCorridorTarget','TryProjectOntoWalkThroughCorridor','IsRouteMissionSameChainImmediateSuccessor','IsCapabilityRouteRetrySafe','IsProbeLifecycleCurrent')){Check ((C $f $n).Count -eq 1) ('one demonstrated physical Sprint plan '+$n)}
$f=M 'ApplyCollisionActivePhysicalRouteMotion';Check ((C $f 'TryGetCapabilityRouteSprint').Count -eq 1 -and (C $f 'TryGetCapabilityRouteSprint')[0].Offset -lt (C $f 'TryApplyMeasuredGroundDelivery')[0].Offset) 'existing physical input uses Sprint plan before delivery selection'
$f=M 'UpdateSharedNativeEntryAcquisition';Check ((C $f 'ConsumeReadyEntry').Count -eq 1 -and (C $f 'ConsumeReadyEntry')[0].Offset -lt (C $f 'MarkLearningV32NativeEntryAcquired'|Select-Object -Last 1).Offset) 'complete ready Jump consumes same sample without extra stability latency'
$f=M 'TryDispatchCapabilitySequenceNative';Check ((C $f 'UpdateSharedNativeEntryAcquisition').Count -eq 1 -and (C $f 'TryBeginLearnedNativeJump')[0].Offset -lt (C $f 'UpdateSharedNativeEntryAcquisition')[0].Offset) 'newly allocated Jump evaluates entry before next native movement'
$f=M 'TryDriveCapabilityRecordedEntryConnector';foreach($n in @('Owns','IsCapabilityRouteRetrySafe','HasActiveTraversalProbe','IsCapabilityReturnInProgress','CalculateStrictPath','LocalApproachPath','TryGetCapabilityCompletedPrefixBoundary','PrefixPath','TryGetFirstNonSelfRaycastHit','TryGetFirstNonSelfCapsuleCastHit','Connector','TryEnterBoundPhysicalMotion','ApplyCollisionActivePhysicalRouteMotion')){Check ((C $f $n).Count -gt 0) ('one current head grounded recorded connector '+$n)}
Check ((C $f 'Connector')[0].Offset -lt (C $f 'TryEnterBoundPhysicalMotion')[0].Offset -and (C $f 'TryEnterBoundPhysicalMotion')[0].Offset -lt (C $f 'ApplyCollisionActivePhysicalRouteMotion')[0].Offset) 'sensor evidence before exact scope and reused input writer'
$f=M 'TickCapabilityFullRouteRetry';Check ((C $f 'TryDriveCapabilityRecordedEntryConnector').Count -eq 1 -and (C $f 'TickCapabilityPortalWait')[0].Offset -lt (C $f 'TryDriveCapabilityRecordedEntryConnector')[0].Offset -and (C $f 'TickCapabilityFailureSequence')[0].Offset -lt (C $f 'TryDriveCapabilityRecordedEntryConnector')[0].Offset) 'connector never steals queue or failure stage'


$f=M 'OnBotOwnerUpdate';Check ((C $f 'QuiesceCapabilityCompletedFollowInput').Count -eq 1) 'completed Follow input cleanup on native Bot cadence'
$f=M 'QuiesceCapabilityCompletedFollowInput';foreach($n in @('StopCompletedFollowInput','HasCapabilityUnresolvedRouteWork','HasCurrentCapabilityNativeWay','TryEnterBoundPhysicalMotion','CommitCapabilityRouteRetryZeroInput')){Check ((C $f $n).Count -eq 1) ('close completed Follow stops stale input only '+$n)}


$f=M 'UpdateSharedNativeEntryAcquisition';Check ((C $f 'HasCapabilityReadyJumpMotion').Count -eq 1 -and (C $f 'HasCapabilityReadyJumpMotion')[0].Offset -lt (C $f 'ConsumeReadyEntry')[0].Offset) 'fast Jump sample requires real direction and velocity'
$f=M 'HasCapabilityReadyJumpMotion';foreach($n in @('TryObserveCapabilityJumpBodyVelocity','ReadyJumpMotion')){Check ((C $f $n).Count -eq 1) ('instant Jump motion qualification '+$n)}


$f=M 'UpdateWalkThroughEntryConvergence';Check ((C $f 'Lane').Count -eq 1) 'known WT also needs a real finite narrow entry lane'
$f=M 'TryPublishLearningV414PathlessNativeInputLease';Check ((C $f 'Center').Count -eq 1) 'off-lane walking input uses exact correction rather than inherited forward frame'
$f=M 'DispatchV2WalkThroughEntryApproach';foreach($n in @('TryPublishLearningV414PathlessNativeInputLease','CommitCapabilityRouteRetryZeroInput')){Check ((C $f $n).Count -eq 1) ('pathless entry centering '+$n)}
$f=M 'TryGetCapabilityRouteSprint';Check ((C $f 'SprintIntent').Count -eq 1) 'explicit demonstration Sprint flag outranks animation state name'
foreach($name in @('CaptureBreadcrumb','CloneRouteMissionBreadcrumb')){$f=M $name;Check (@($f.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'stfld' -and $_.Operand.Name -eq 'Sprinting'}).Count -eq 1) ('Sprint intent survives capture/clone '+$name)}
$f=M 'IsCapabilityEpisodeRetired';Check ((C $f 'RetiredEpisode').Count -eq 1) 'monotonic retirement is distinct from physical admission failure'
foreach($name in @('TryPrepareCapabilityAutomaticProbeMission','RegisterCapabilityLocalMission')){Check ((C (M $name) 'IsCapabilityEpisodeRetired').Count -eq 1) ('retired episode cannot create new debt '+$name)}
$f=M 'TryPrepareCapabilityAutomaticProbeMission';Check ((C $f 'IsCapabilityEpisodeRetired')[0].Offset -lt (C $f 'RegisterCapabilityLocalMission')[0].Offset) 'retired automatic request rejected before birth and failure ladder'
$f=M 'UpdateWalkThroughProbe';Check ((C $f 'TryRequestCapabilityFullRouteRetry'|Where-Object Offset -lt (C $f 'QuarantineFollowerTraversalEdge')[0].Offset).Count -gt 0) 'solid WT blockage offered before quarantine destroys mission identity'
$f=M 'UpdateLearningV331NativeGroundContinuation';Check ((C $f 'HoldLanding').Count -eq 1) 'supported native Jump may face target before translating in wrong frame'
$f=M 'TryRetireCapabilityCoveredDropOverlap';foreach($n in @('CoveredDropOverlap','IsLifecycleCompletionForStep','CaptureRouteDebtObservation','TryAdvanceRouteMission','IsCapabilityRouteRetrySafe')){Check ((C $f $n).Count -eq 1) ('certified actor overlap debt seam '+$n)}
Check ((C $f 'CoveredDropOverlap')[0].Offset -lt (C $f 'TryAdvanceRouteMission')[0].Offset) 'physical Native receipt and bounded geometry precede subsumption'
foreach($name in @('EnsureFollowerRouteMissionCommitment','TickCapabilityFullRouteRetry')){Check ((C (M $name) 'TryRetireCapabilityCoveredDropOverlap').Count -eq 1) ('covered debt cleared before ordinary ingress '+$name)}
$f=M 'TickCapabilityFullRouteRetry';Check ((C $f 'TryRetireCapabilityCoveredDropOverlap')[0].Offset -lt (C $f 'TickCapabilityPortalWait')[0].Offset) 'covered upper WT never joins a new queue after lower landing'
$snaps=@($m.GetTypes()|ForEach-Object{$_.Methods}|Where-Object{$_.Name -like '<RegisterCapabilityLocalMission>*'})
$has32=$false;foreach($lambda in $snaps){if($lambda.HasBody -and @($lambda.Body.Instructions|Where-Object{$_.OpCode.Name -in @('ldc.i4','ldc.i4.s') -and [int]$_.Operand -eq 32}).Count -gt 0){$has32=$true}}
Check $has32 'local queue snapshot contains the same32 upstream nodes used by its connector'

$f=M 'TryMaintainCapabilityNativeInput';foreach($n in @('FreshInput','HasCurrentCapabilityNativeWay','IsCapabilityReturnInProgress')){Check ((C $f $n).Count -gt 0) ('stale input cannot renew path or failure motion '+$n)}
Check ((C $f 'FreshInput')[0].Offset -lt (C $f 'Commit')[0].Offset) 'fresh exact receipt verified before native maintenance'
$f=M 'CommitCapabilityNativePlayerInput';Check (@($f.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'stfld' -and $_.Operand.Name -eq 'CapabilityInputAt'}).Count -eq 1) 'producer alone stamps input deadline'
$port=$m.GetTypes()|Where-Object Name -eq 'CapabilityLocomotionPort';$f=$port.Methods|Where-Object Name -eq 'RequestInput'|Select-Object -First 1
Check ((C $f 'CommitCapabilityNativePlayerInput').Count -eq 1) 'maintenance uses shared native input gateway'
$call=(C $f 'CommitCapabilityNativePlayerInput')[0];$index=$f.Body.Instructions.IndexOf($call);Check ($f.Body.Instructions[$index-1].OpCode.Name -eq 'ldc.i4.0') 'maintenance explicitly cannot refresh its receipt'
$f=M 'HoldForwardInput';Check ((C $f 'CommitCapabilityNativePlayerInput').Count -eq 1) 'probe producer stamps current forward intent'
$f=M 'TryGetCapabilityPortalApproachTarget';Check ((C $f 'IsCapabilityPassageInFlight').Count -eq 1 -and (C $f 'IsCapabilityPassageInFlight')[0].Offset -lt (C $f 'Approach')[0].Offset) 'in-flight corridor is never sent back to its own entry'
$f=M 'IsCapabilityPassageInFlight';foreach($n in @('PassageInFlight','PeekExactGeneration')){Check ((C $f $n).Count -eq 1) ('exact current corridor progress '+$n)}
$f=M 'DriveCapabilityStrategyApproach';foreach($n in @('IsCapabilityRouteRetrySafe','HasActiveTraversalProbe','IsCapabilityPortalWaiting','Owns','LocalApproachPath','PrefixPath','TryEnterBoundPhysicalMotion','ApplyCollisionActivePhysicalRouteMotion','CommitCapabilityRouteRetryZeroInput')){Check ((C $f $n).Count -gt 0) ('selected strategy drives or holds under same mission '+$n)}
$f=$bridge.Methods|Where-Object Name -eq 'Tick'|Select-Object -First 1;Check ((C $f 'HasCapabilityFailureStrategy').Count -eq 1 -and (C $f 'HasCapabilityFailureStrategy')[0].Offset -lt (C $f 'TryGetCapabilityPortalApproachTarget')[0].Offset) 'independent Follow bridge cannot fight pending strategy driver'
$f=M 'ApplyCollisionActivePhysicalRouteMotion';Check ((C $f 'WorldInput').Count -eq 1 -and (C $f 'WorldInput')[0].Offset -lt (C $f 'TurnMove')[0].Offset) 'manual route input uses actual current player frame'
$f=M 'ReconcileCapabilityRetiredProbeConsumer';Check ((C $f 'CommitCapabilityRouteRetryZeroInput').Count -eq 1) 'terminal no-path consumer clears old forward input after actual completion'
$f=M 'TryBeginLearningV331NativeGroundContinuation';Check ((C $f 'ContinuationBudget').Count -eq 1) 'Jump deadline is issued from remaining real exit distance'
$f=M 'UpdateLearningV331NativeGroundContinuation';foreach($n in @('ExitOverrun','LandingCorrection')){Check ((C $f $n).Count -eq 1) ('bounded current landing correction '+$n)}

$f=M 'DispatchV2WalkThroughEntryApproach';Check ((C $f 'TryDriveCapabilityLocalWalkThroughEntry').Count -eq 1 -and (C $f 'TryDriveCapabilityLocalWalkThroughEntry')[0].Offset -lt (C $f 'BotMoverGoToPointNoWay')[0].Offset) 'WT rejects false path before legacy mover target refresh'
$f=M 'TryDriveCapabilityLocalWalkThroughEntry';foreach($n in @('IsProbeLifecycleCurrent','CanCommitScopedPhysicalMotion','LocalEntryPath','ManualEntry','TryRetireNativeMoverPathPreserveLocomotion','EntryHeight','ApplyCollisionActivePhysicalRouteMotion','CommitCapabilityRouteRetryZeroInput')){Check ((C $f $n).Count -gt 0) ('exact active WT owns current entry resource '+$n)}
Check ((C $f 'LocalEntryPath')[0].Offset -lt (C $f 'ApplyCollisionActivePhysicalRouteMotion')[0].Offset) 'entry resource inspected before motion'
$f=M 'DriveLearningV32NativeEntryAcquisition';Check ((C $f 'TryDriveCapabilityObstacleEntry').Count -eq 1 -and (C $f 'TryDriveCapabilityObstacleEntry')[0].Offset -lt (C $f 'BotMoverGoToPointNoWay')[0].Offset) 'obstacle correction cannot install opposing legacy path'
$f=M 'TryDriveCapabilityObstacleEntry';foreach($n in @('IsProbeLifecycleCurrent','TryEnterBoundPhysicalMotion','ObstacleHold','EntryHeight','ApplyCollisionActivePhysicalRouteMotion')){Check ((C $f $n).Count -gt 0) ('same native entry body and pose resource '+$n)}
$f=M 'TickCapabilityFailureSequence';Check ((C $f 'PrepareCapabilityStationaryRescue').Count -eq 1 -and (C $f 'StrategyExpired')[0].Offset -lt (C $f 'PrepareCapabilityStationaryRescue')[0].Offset -and (C $f 'PrepareCapabilityStationaryRescue')[0].Offset -lt (C $f 'TryStartProbeForBot')[0].Offset) 'bounded stationary rescue preparation before native actor birth'
$f=M 'PrepareCapabilityStationaryRescue';foreach($n in @('TryEnterBoundPhysicalMotion','HasActiveTraversalProbe','IsCapabilityPortalWaiting','Owns','ApplyCollisionActivePhysicalRouteMotion','RescueSettled')){Check ((C $f $n).Count -eq 1) ('brake and rebase selected HOW only '+$n)}
$f=M 'TryDriveCapabilityRecordedEntryConnector';Check ((C $f 'LocalJoin').Count -eq 1 -and (C $f 'LocalJoin')[0].Offset -lt (C $f 'ApplyCollisionActivePhysicalRouteMotion')[0].Offset) 'off-trail head requires strict local join before original support and collision checks'
$f=M 'IsCapabilityRelatedPrefix';Check ((C $f 'RelatedPrefix').Count -eq 1) 'foreign reverse demonstration cannot provide current prefix authority'
$f=M 'TryRetireCapabilityCoveredDropOverlap';Check ((C $f 'TryRetireCapabilityCoveredLowerDrop').Count -eq 1) 'covered lower native drop reconciled before normal admission'
$f=M 'TryRetireCapabilityCoveredLowerDrop';foreach($n in @('IsLifecycleCompletionForStep','CoveredLowerDrop','CaptureRouteDebtObservation','TryAdvanceRouteMission','All')){Check ((C $f $n).Count -eq 1) ('only exact physical predecessor covers lower drop '+$n)}
Check ((C $f 'CoveredLowerDrop')[0].Offset -lt (C $f 'TryAdvanceRouteMission')[0].Offset) 'lower actuator debt requires qualified coverage before subsumption'
$f=M 'CapabilityGenericResumeFloor';foreach($n in @('GetFollowerRouteResumeFloor','GetFollowerRouteExecutionFloor')){Check ((C $f $n).Count -eq 1) ('generic chronology uses existing authoritative floor '+$n)}
foreach($name in @('TryAdvanceLearningV365GenericPhysicalTarget','TryDriveCapabilityGenericLowStep','CommitCapabilityResolvedNavigation')){Check ((C (M $name) 'TargetAfterFloor').Count -eq 1) ('obsolete target blocked at producer and actual consumer '+$name)}
$f=M 'TryDriveLearningV349GenericEmpiricalPassageCorridor';Check ((C $f 'CapabilityGenericResumeFloor').Count -eq 1 -and (C $f 'CapabilityGenericResumeFloor')[0].Offset -lt (C $f 'ArmLearningV365ContinuousGenericPhysicalRoute')[0].Offset) 'new generic target cannot be recreated behind completion floor'
$f=M 'TryDriveCapabilityGenericLowStep';Check ((C $f 'TargetAfterFloor')[0].Offset -lt (C $f 'TryMovementContextApplyApproachMotion')[0].Offset) 'actual low-step writer enforces completed target floor'
$f=M 'TryGetLearningV363ActuatorRouteEntryLaneTarget';foreach($n in @('DropLead','ForwardLaneSample')){Check ((C $f $n).Count -eq 1) ('drop lead survives NavMesh snapping without granting admission '+$n)}
$f=M 'ApplyCollisionActivePhysicalRouteMotion';Check ((C $f 'PrepareCapabilityDropIngressLocomotion').Count -eq 1 -and (C $f 'PrepareCapabilityDropIngressLocomotion')[0].Offset -lt (C $f 'TryApplyMeasuredGroundDelivery')[0].Offset) 'Idle pre-drop native locomotion prepared before physical input delivery'
$f=M 'PrepareCapabilityDropIngressLocomotion';foreach($n in @('HasActiveTraversalProbe','IsCapabilityRouteRetrySafe','TryGetActiveFollowerRouteMission','RequestCapabilityLocomotionResume')){Check ((C $f $n).Count -eq 1) ('existing exact locomotion request only for idle current drop ingress '+$n)}

$f=M 'TryGetCapabilityCleanCompletedFollow';foreach($n in @('IsCapabilityRouteRetrySafe','HasActiveTraversalProbe','HasCapabilityUnresolvedRouteWork','HasCapabilityNewRecordedWork','HasCurrentCapabilityNativeWay','PeekExactGeneration')){Check ((C $f $n).Count -eq 1) ('completed Follow yields to live work and exact generation '+$n)}
$f=M 'TryYieldCapabilityCompletedFollow';foreach($n in @('TryGetCapabilityCleanCompletedFollow','TryGetCapabilityLiveResolvedFollow','CancelDelivery')){Check ((C $f $n).Count -eq 1) ('completed routes yield ordinary formation '+$n)}
$f=$bridge.Methods|Where-Object Name -eq 'Tick'|Select-Object -First 1;Check ((C $f 'TryYieldCapabilityCompletedFollow').Count -eq 1 -and (C $f 'TryYieldCapabilityCompletedFollow')[0].Offset -lt (C $f 'TryGetCapabilityPortalApproachTarget')[0].Offset) 'clean current leader outranks old completed breadcrumbs in existing Follow delivery'
foreach($name in @('PatchBotOwnerGoToPointStatus','PatchBotOwnerGoToPointVoid','PatchBotMoverDestination','PatchPointDataDestination')){
 $f=$patch.Methods|Where-Object Name -eq $name|Select-Object -First 1
 Check ((C $f 'ReadCapabilityDestination').Count -eq 1) ('actual destination inspected at existing native boundary '+$name)
 Check (@($f.Parameters|Where-Object Name -eq '__args').Count -eq 1) ('Harmony target arguments bound at '+$name)
}

$f=M 'TryUseLearnedTraversalCommitment';Check ((C $f 'OwnsCapabilityUnfinishedCommitment').Count -eq 1 -and (C $f 'OwnsCapabilityUnfinishedCommitment')[0].Offset -lt (C $f 'AdvanceFollowerRouteResumeFloor')[0].Offset) 'current typed approach cannot bypass failure ladder by abandoning floor for reachable leader'
$f=M 'OwnsCapabilityUnfinishedCommitment';foreach($n in @('IsLearningV376RouteStepEquivalentToEdgeId','IsCapabilityDebtResolved')){Check ((C $f $n).Count -eq 1) ('legacy fallback yields only exact outstanding mission identity '+$n)}

$f=M 'TickCapabilityFullRouteRetry';Check ((C $f 'TryAdmitCapabilityHeadWalkThroughEntry').Count -eq 1) 'queue head has actual WT allocation path'
$f=M 'TryAdmitCapabilityHeadWalkThroughEntry';foreach($n in @('IsCapabilityRouteRetrySafe','HasActiveTraversalProbe','IsCapabilityDebtResolved','IsLearningV376RouteStepEquivalentToEdgeId','Owns','HeadEntry','TryStartEmpiricalLocalEntryBridge')){Check ((C $f $n).Count -ge 1) ('WT head allocation bound '+$n)}
Check ((C $f 'HeadEntry')[0].Offset -lt (C $f 'TryStartEmpiricalLocalEntryBridge')[0].Offset) 'head permission before probe allocation'
$f=M 'BeginUnifiedTraversalEgressOrVerify';Check ((C $f 'SemanticContinuation').Count -eq 1) 'local rescue checks actual semantic height before selecting movement'
Check ((C $f 'SemanticContinuation')[0].Offset -lt (C $f 'FinalizeOrEscalateTraversalFailure')[-1].Offset) 'unresolved semantic floor offered to shared failure ladder'
$f=M 'TryArmCapabilityStrictReturnCorridor';foreach($n in @('PathWithinBudget','ReturnSegment','TryGetFirstNonSelfRaycastHit','TryGetFirstNonSelfCapsuleCastHit','TryGetCapabilityCompletedPrefixBoundary','PrefixPath','HoldCapabilityRouteRetry')){Check ((C $f $n).Count -ge 1) ('strict return uses existing validated corridor '+$n)}
$f=M 'TickCapabilityFullRouteRetry';Check ((C $f 'TryArmCapabilityStrictReturnCorridor')[0].Offset -lt (C $f 'BotOwnerGoToPoint')[0].Offset) 'supported return corridor before native stale-corner path'
$bt=$m.GetTypes()|Where-Object Name -eq 'LegacyOriginalPITFollowBridge';$f=$bt.Methods|Where-Object Name -eq 'Tick'|Select-Object -First 1

$f=M 'BeginUnifiedTraversalEgressOrVerify';Check ((C $f 'TryValidateCapabilitySemanticWalkingRamp').Count -eq 1) 'height admission observes actual supported ramp before moving'
$f=M 'TryValidateCapabilitySemanticWalkingRamp';foreach($n in @('IsProbeLifecycleCurrent','IsCapabilityRouteRetrySafe','RampEnvelope','TryGetFirstNonSelfRaycastHit','TryGetFirstNonSelfCapsuleCastHit','RampSample')){Check ((C $f $n).Count -ge 1) ('actual ramp resource '+$n)}

$f=M 'TryDriveCapabilityGroundEntryLead';foreach($n in @('Owns','IsCapabilityRouteRetrySafe','HasActiveTraversalProbe','IsCapabilityReturnInProgress','IsCapabilityDebtResolved','TryGetCapabilityCompletedPrefixBoundary','PrefixPath','TryGetFirstNonSelfRaycastHit','TryGetFirstNonSelfCapsuleCastHit','GroundLead','TryEnterBoundPhysicalMotion','ApplyCollisionActivePhysicalRouteMotion')){Check ((C $f $n).Count -gt 0) ('sensed WT lead exact scope/sensor '+$n)}
Check ((C $f 'GroundLead')[0].Offset -lt (C $f 'TryEnterBoundPhysicalMotion')[0].Offset -and (C $f 'TryEnterBoundPhysicalMotion')[0].Offset -lt (C $f 'ApplyCollisionActivePhysicalRouteMotion')[0].Offset) 'ground segment proof precedes physical mutation'
$f=M 'TryDriveCapabilityRecordedEntryConnector';Check ((C $f 'TryDriveCapabilityGroundEntryLead').Count -eq 1) 'normal queue head fills pre-probe dead spot'
$f=M 'DriveCapabilityStrategyApproach';Check ((C $f 'TryDriveCapabilityGroundEntryLead').Count -eq 1 -and (C $f 'TryDriveCapabilityGroundEntryLead')[0].Offset -lt (C $f 'CalculateStrictPath')[0].Offset) 'returned pending WT can use same local ground resource before unavailable NavMesh'
$f=M 'TryGetCapabilityLiveResolvedFollow';foreach($n in @('PeekExactGeneration','IsCapabilityRouteRetrySafe','HasActiveTraversalProbe','HasCapabilityUnresolvedRouteWork','HasCapabilityNewRecordedWork','HasCurrentCapabilityNativeWay','CapabilityGenericResumeFloor','LiveFollow')){Check ((C $f $n).Count -gt 0) ('live Follow excludes other body consumers '+$n)}
$f=M 'OnBotOwnerUpdate';Check ((C $f 'GuardCapabilityResolvedFollowResource').Count -eq 0) 'ordinary native Follow no longer loses paths to recurring corner cleanup'
$f=M 'TryYieldCapabilityCompletedFollow';Check ((C $f 'TryGetCapabilityLiveResolvedFollow').Count -eq 1) 'late clean Follow can reacquire reachable current leader after diagnostic expiry'
$f=M 'TryDispatchCapabilityCurrentAction';foreach($n in @('IsCapabilityRouteRetrySafe','IsCapabilityPortalWaiting','JumpDispatch','Owns','TickCapabilityPortalWait','EnsureFollowerRouteMissionCommitment','IsLearningV376RouteStepEquivalentToEdgeId','TryStartEmpiricalLocalEntryBridge')){Check ((C $f $n).Count -gt 0) ('immediate Jump retains exact admission '+$n)}
Check ((C $f 'Owns')[0].Offset -lt (C $f 'TryStartEmpiricalLocalEntryBridge')[0].Offset) 'queue permission before immediate native allocation'
Check ((C $f 'CommitCapabilityMoveRetry').Count -eq 0) 'per-frame Jump dispatch cannot reset own failure/backoff clock'
$f=M 'TickCapabilityFullRouteRetry';Check ((C $f 'TickCapabilityPortalWait')[0].Offset -lt (C $f 'TryDispatchCapabilityCurrentAction')[0].Offset -and (C $f 'TryDispatchCapabilityCurrentAction')[0].Offset -lt (C $f 'TickCapabilityUnstartedNative')[0].Offset) 'queue then current Jump before unstarted watchdog'
$f=M 'TryFinalizeLearningV423PostCompletionMissionConsumer';Check ((C $f 'UnregisterActiveProbe')[0].Offset -lt (C $f 'TryDispatchCapabilityCurrentAction')[0].Offset) 'predecessor actor detached before successor admission'
$f=M 'TryAdvanceRouteMission';Check ((C $f 'CommitCapabilityMoveRetry').Count -eq 1 -and (C $f 'Resolve')[0].Offset -lt (C $f 'CommitCapabilityMoveRetry')[0].Offset) 'attempt clock reset at committed lifecycle advance only'

$f=M 'TryYieldCapabilityCompletedFollow';Check ((C $f 'CalculateStrictPath').Count -eq 0 -and (C $f 'TryCommitScopedFollowPath').Count -eq 0) 'resolved Follow does not manufacture a player-point chase path'

$f=M 'TryDriveCapabilityGroundEntryLead';Check ((C $f 'EntryTurnHold').Count -eq 1 -and (C $f 'EntryTurnHold')[0].Offset -lt (C $f 'ApplyCollisionActivePhysicalRouteMotion')[0].Offset) 'grounded WT lead chooses hold before physical publication'
$f=M 'TryDriveCapabilityObstacleEntry';foreach($n in @('SideCenterTurn','ObstacleEntrySpeed')){Check ((C $f $n).Count -eq 1 -and (C $f $n)[0].Offset -lt (C $f 'ApplyCollisionActivePhysicalRouteMotion')[0].Offset) ('native obstacle precision before movement '+$n)}
Check ($m.Assembly.Version.ToString() -eq '1.0.1.0') 'Beta assembly version 0.9.0.0'
$plugin=$m.GetTypes()|Where-Object Name -eq 'BotTraversalPlugin';$meta=$plugin.CustomAttributes|Where-Object {$_.AttributeType.FullName -eq 'BepInEx.BepInPlugin'}
Check ($meta.ConstructorArguments[2].Value.ToString() -eq '1.0.1') 'Beta BepInEx/F12 version 0.9.0'
$fileVersion=$m.Assembly.CustomAttributes|Where-Object {$_.AttributeType.Name -eq 'AssemblyFileVersionAttribute'}
Check ($fileVersion.ConstructorArguments[0].Value.ToString() -eq '1.0.1.0') 'Beta Windows DLL file version'
$infoVersion=$m.Assembly.CustomAttributes|Where-Object {$_.AttributeType.Name -eq 'AssemblyInformationalVersionAttribute'}
Check ($infoVersion.ConstructorArguments[0].Value.ToString() -eq '1.0.1+RC158.SPT416') 'Beta release identity retained in assembly metadata'

$f=M 'TryDriveCapabilityObstacleEntry';Check ((C $f 'ObstacleNear').Count -eq 1 -and (C $f 'ObstacleNear')[0].Offset -lt (C $f 'ApplyCollisionActivePhysicalRouteMotion')[0].Offset) 'near entry selects precision before sprint flags and shared physical governor'

$f=M 'TryUseLearnedTraversalCommitment';Check ((C $f 'RejectCapabilityForeignCommitment').Count -eq 1 -and (C $f 'RejectCapabilityForeignCommitment')[0].Offset -lt (C $f 'IsTraversalCommitmentOrientationAllowed')[0].Offset) 'foreign ordinary target rejected before orientation cleanup or stale delivery'
$f=M 'RejectCapabilityForeignCommitment';foreach($n in @('HasActiveTraversalProbe','OwnsCapabilityUnfinishedCommitment','TryPrepareCapabilityAutomaticProbeMission','ForeignCommitment','ClearLearnedTraversalCommitment','CancelDelivery')){Check ((C $f $n).Count -ge 1) ('current typed priority exact binding '+$n)}
$f=M 'TryTeleportMissedLearnedTraversal';Check ((C $f 'HandleCapabilityRecordedTeleportFailure').Count -eq 1 -and (C $f 'HandleCapabilityRecordedTeleportFailure')[0].Offset -lt (C $f 'PrepareForAutomaticRecoveryTeleport')[0].Offset) 'recorded legacy fallback enters common ladder before teleport transaction'
$f=M 'HandleCapabilityRecordedTeleportFailure';foreach($n in @('RecordedFallback','TryPrepareCapabilityAutomaticProbeMission','OwnsCapabilityUnfinishedCommitment','TryRequestCapabilityFullRouteRetry','RejectCapabilityForeignCommitment','ClearLearnedTraversalCommitment')){Check ((C $f $n).Count -ge 1) ('recorded failure adapter '+$n)}
Check ((C $f 'TryTeleportAcrossTraversalExit').Count -eq 0 -and (C $f 'PrepareForAutomaticRecoveryTeleport').Count -eq 0) 'recorded redirect cannot issue a teleport receipt'
$f=M 'TryCloseCapabilityClimbAtSuccessor';foreach($n in @('IsProbeLifecycleCurrent','IsCapabilityRouteRetrySafe','TryGetLearningV375FutureSameChainSuccessor','IsEffectivelySupportedForProbe','ClimbSuccessor','TryFinalizeTraversalChainIntermediate')){Check ((C $f $n).Count -ge 1) ('qualified upper Climb handoff '+$n)}
Check ((C $f 'CommitUnifiedTraversalSuccess').Count -eq 0) 'upper Climb adapter cannot directly publish weakened success'
$f=M 'BeginUnifiedTraversalEgressOrVerify';Check ((C $f 'TryCloseCapabilityClimbAtSuccessor').Count -eq 1 -and (C $f 'TryCloseCapabilityClimbAtSuccessor')[0].Offset -lt (C $f 'TryBeginPlayerRouteContinuationBridge')[-1].Offset) 'same-chain validator gets upper landing before lower-layer route bridge fallback'

$f=M 'TryDriveCapabilityObstacleEntry';Check ((C $f 'CenterSideOnly').Count -eq 1 -and (C $f 'CenterSideOnly')[0].Offset -lt (C $f 'SideCenterTurn')[0].Offset) 'near-entry position correction selects current cross-track before heading and translation'

$f=M 'HandleCapabilityRecordedTeleportFailure';foreach($n in @('RecordedRecoveryScope','HasCapabilityUnresolvedRouteWork')){Check ((C $f $n).Count -eq 1) ('recovery scope cannot borrow foreign movement authority '+$n)}
$f=M 'TryUseLearnedTraversalCommitment';Check ((C $f 'HandleCapabilityRecordedTeleportFailure').Count -eq 2) 'both legacy timeout callers yield the current frame to recorded recovery before restoring an old commitment'
Check ((C $f 'HandleCapabilityRecordedTeleportFailure')[0].Offset -lt (C $f 'TryTeleportMissedLearnedTraversal')[0].Offset -and (C $f 'HandleCapabilityRecordedTeleportFailure')[1].Offset -lt (C $f 'TryTeleportMissedLearnedTraversal')[1].Offset) 'mission and direct legacy fallbacks intercept recorded work before teleport or commitment renewal'
$f=M 'TryAdvanceCapabilityFailureSequence';Check ((C $f 'AfterFailure')[0].Offset -lt (C $f 'ForceReleaseTraversalProbe')[0].Offset) 'publish failure stage before reentrant probe retirement callbacks'
$stageStore=@($f.Body.Instructions|Where-Object {$_.OpCode.Name -eq 'stfld' -and $_.Operand.Name -eq 'Stage'})[0]
Check ($stageStore.Offset -lt (C $f 'ForceReleaseTraversalProbe')[0].Offset) 'new stage becomes visible before old frontier callbacks can allocate'
$f=M 'TryDriveCapabilityWalkThroughSeam';foreach($n in @('IsProbeLifecycleCurrent','IsCapabilityRouteRetrySafe','WalkSeam','TryGetFirstNonSelfRaycastHit','WalkSeamSupport','TryUpdateWalkThroughStepAssist')){Check ((C $f $n).Count -eq 1) ('bounded straight WT step contract '+$n)}
$f=M 'UpdateWalkThroughProbe';Check ((C $f 'TryDriveCapabilityWalkThroughSeam').Count -eq 1 -and (C $f 'TryDriveCapabilityWalkThroughSeam')[0].Offset -lt (C $f 'TryUpdateWalkThroughSharpTurnGate')[0].Offset) 'straight WT seam contract runs before sharp-turn-only handling and ordinary route producers'
$f=M 'TryDriveCapabilityWalkThroughSeam';foreach($n in @('TryBeginInjectedNativeVault','TryBeginLearnedNativeJump','TryTeleportAcrossTraversalExit','CommitUnifiedTraversalSuccess')){Check ((C $f $n).Count -eq 0) ('step contract has no semantic replacement or success writer '+$n)}
$f=$bridge.Methods|Where-Object Name -eq 'Tick'|Select-Object -First 1
Check ((C $f 'TryYieldCapabilityCompletedFollow')[0].Offset -lt (C $f 'BotOwnerGoToPoint')[0].Offset) 'native formation handoff precedes any new bridge path delivery'
$f=$bridge.Methods|Where-Object Name -eq 'TryRejectOriginalPITFollowWrite'|Select-Object -First 1
Check ((C $f 'TryYieldCapabilityCompletedFollow').Count -eq 1 -and (C $f 'TryYieldCapabilityCompletedFollow')[0].Offset -lt (C $f 'TryGetActiveLease')[0].Offset) 'clean native Follow write immediately retires old approach lease'
$f=M 'TryYieldCapabilityCompletedFollow';$cancel=(C $f 'CancelDelivery')[0];$before=$f.Body.Instructions[$f.Body.Instructions.IndexOf($cancel)-1]
Check ($before.OpCode.Name -eq 'ldc.i4.1' -and $cancel.Operand.MethodSig.Params.Count -eq 2) 'repeated clean Follow handoff preserves bridge scan and log cadence'
$f=$bridge.Methods|Where-Object Name -eq 'CancelDelivery'|Select-Object -First 1
Check ((C $f 'Remove').Count -eq 4) 'delivery cancellation retains lease, scan and diagnostic cleanup for ordinary callers'
$cadence=@($f.Body.Instructions|Where-Object {$_.OpCode.Name -eq 'ldarg.1'})
Check ($cadence.Count -eq 1 -and $cadence[0].Offset -lt (C $f 'Remove')[1].Offset) 'completed handoff can return before clearing scan timers'
$f=M 'TryAdvanceRouteMission';Check ((C $f 'CanConsumeCapabilityCursorFloor').Count -eq 1 -and (C $f 'CanConsumeCapabilityCursorFloor')[0].Offset -lt (C $f 'Resolve')[0].Offset) 'all movement floor consumption has real physical evidence before sole lifecycle resolution'
Check ((C $f 'IsLearningV369PhysicalCorridorStep').Count -ge 1 -and (C $f 'IsFollowerRouteMissionStepPhysicallyPassed').Count -ge 1) 'physical-pass callers cannot bypass finite PhysicalCorridor validator'
$f=M 'CanConsumeCapabilityCursorFloor';foreach($n in @('CursorFloor','IsFollowerRouteMissionStepPhysicallyPassed')){Check ((C $f $n).Count -eq 1) ('chronological floor proof '+$n)}
$f=M 'IsFollowerRouteMissionStepPhysicallyPassed';Check ((C $f 'CorridorExit').Count -eq 1 -and (C $f 'GetPlanarDistanceToSegment').Count -ge 1) 'corridor passage proves supported finite terminal plane and locality'
$f=M 'EnsureFollowerRouteMissionCommitment';Check ((C $f 'RefreshCapabilityCurrentPhysicalCorridor')[0].Offset -lt (C $f 'CanConsumeCapabilityCursorFloor')[0].Offset) 'current recorded corridor extension precedes any floor consumption'
$f=M 'RefreshCapabilityCurrentPhysicalCorridor';foreach($n in @('HasActiveTraversalProbe','IsCapabilityFullRouteReplay','CorridorExtension','LastOrDefault','set_RawExit')){Check ((C $f $n).Count -ge 1) ('same recorded corridor refresh '+$n)}
Check (@($f.Body.Instructions|Where-Object {$_.OpCode.Name -eq 'stfld' -and $_.Operand.Name -in @('LastProgressAt','CurrentStepIndex','NextMoveAttemptAt')}).Count -eq 0) 'recording extension neither advances debt nor renews movement deadlines'
$f=M 'PrepareCapabilityOriginalFollowPath';foreach($n in @('CanCommitScopedPhysicalMotion','IsOriginalPITFollowCall','TryGetCapabilityLiveResolvedFollow','TryRetireNativeMoverPathPreserveLocomotion','TryScrubLearningV364NativeMoverEpochAnchor')){Check ((C $f $n).Count -ge 1) ('pending original Follow origin '+$n)}
$f=M 'CompleteCapabilityOriginalFollowPath';foreach($n in @('TryGetCapabilityLiveResolvedFollow','NativeFollowReceipt','TryEnterBoundPhysicalMotion','RecordFollowPathResult')){Check ((C $f $n).Count -eq 1) ('actual native Follow result receipt '+$n)}
Check ((C $f 'NativeFollowReceipt')[0].Offset -lt (C $f 'RecordFollowPathResult')[0].Offset) 'actual accepted path and exact call generation precede existing epoch proof'
$patch=$m.GetTypes()|Where-Object Name -eq 'FollowerMovementWriterAuthorityPatch'
$f=$patch.Methods|Where-Object Name -eq 'PatchBotOwnerGoToPointStatus'|Select-Object -First 1
Check ((C $f 'Allow')[0].Offset -lt (C $f 'PrepareCapabilityOriginalFollowPath')[0].Offset) 'native path prefix preserves rejected writer semantics before rebase'
$f=$patch.Methods|Where-Object Name -eq 'ObserveNativeFollowPathResult'|Select-Object -First 1
Check ((C $f 'CompleteCapabilityOriginalFollowPath').Count -eq 1 -and @($f.Parameters|Where-Object Name -eq '__runOriginal').Count -eq 1 -and @($f.Parameters|Where-Object Name -eq '__state').Count -eq 1) 'Harmony postfix carries original execution result and call-scoped generation'
$f=$patch.Methods|Where-Object Name -eq 'PatchOne'|Select-Object -First 1
Check (@($f.Body.Instructions|Where-Object {$_.OpCode.Name -eq 'ldstr' -and $_.Operand -eq 'ObserveNativeFollowPathResult'}).Count -eq 1) 'actual native destination patch registers result postfix'
$gait=$m.GetTypes()|Where-Object Name -eq 'FollowerCapabilityFollowGaitPatch'
$f=$gait.Methods|Where-Object Name -eq 'FollowPostfix'|Select-Object -First 1
Check ((C $f 'QuiesceCapabilityOriginalFollowIdle').Count -eq 1) 'verified PIT ordinary Follow completion invokes idle input closure'
$f=$gait.Methods|Where-Object Name -eq 'Apply'|Select-Object -First 1
Check (@($f.Body.Instructions|Where-Object {$_.OpCode.Name -eq 'ldstr' -and $_.Operand -eq 'FollowPostfix'}).Count -eq 1 -and (C $f 'Patch').Count -eq 2) 'existing PIT build binding installs gait and ordinary Follow boundary hooks'
$f=M 'QuiesceCapabilityOriginalFollowIdle';foreach($n in @('TryGetCapabilityLiveResolvedFollow','NativeFollowIdle','TryEnterBoundPhysicalMotion','CommitCapabilityRouteRetryZeroInput')){Check ((C $f $n).Count -eq 1) ('ordinary Follow stop shares actual input '+$n)}
foreach($n in @('BotMoverStop','BotOwnerGoToPoint','TryTeleportAcrossTraversalExit','CommitUnifiedTraversalSuccess')){Check ((C $f $n).Count -eq 0) ('idle closure cannot create motion or success '+$n)}
$f=M 'IsCapabilityLowStepProducerCurrent';foreach($n in @('TryGetActiveFollowerRouteMission','HasCapabilityFailureStrategy','IsCapabilityReturnInProgress','IsLearningV369PhysicalCorridorStep')){Check ((C $f $n).Count -eq 1) ('typed low step owns same unfinished corridor '+$n)}
$f=M 'TryContinueLearningV365ContinuousTypedPhysicalRoute';Check ((C $f 'TryDriveCapabilityGenericLowStep').Count -eq 1 -and (C $f 'TryDriveCapabilityGenericLowStep')[0].Offset -lt (C $f 'ApplyCollisionActivePhysicalRouteMotion')[0].Offset) 'typed corridor uses bounded step contract before same-frame ordinary horizontal drive'
$f=M 'TryDriveCapabilityGenericLowStep';Check ((C $f 'IsCapabilityLowStepProducerCurrent').Count -eq 1) 'shared step contract admits only current typed or already-authorized generic producer'
$f=M 'HasCapabilityLivePassageProducer';Check ((C $f 'PassageLease').Count -eq 1) 'generic passage uses existing finite lease and floor, no new owner registry'
foreach($n in @('OwnsCapabilityLocalMotion','TryGetCapabilityLiveResolvedFollow','TryGetCapabilityCleanCompletedFollow')){Check ((C (M $n) 'HasCapabilityLivePassageProducer').Count -eq 1) ('live passage exclusion reaches '+$n)}
$f=M 'MarkLearningV32NativeEntryAcquired';Check ((C $f 'CaptureCapabilityNativeJumpEntry').Count -eq 1) 'shared accepted physical entry supplies the Jump executor witness'
$f=M 'CaptureCapabilityNativeJumpEntry';Check ((C $f 'CaptureJumpEntry').Count -eq 1 -and (C $f 'IsProbeLifecycleCurrent').Count -eq 1) 'Jump witness belongs to exact current native Jump'
Check (@($f.Body.Instructions|Where-Object {$_.OpCode.Name -eq 'stfld' -and $_.Operand.Name -eq 'JumpEntryCaptured'}).Count -eq 1) 'accepted shared entry writes actual Jump capture field'
$f=M 'TryAdmitCapabilitySequenceProbe';Check ((C $f 'UnownedSequenceProbe').Count -eq 1 -and (C $f 'CanAllocateStrategy').Count -eq 1) 'reserved dispatch requires live transaction and exact selected HOW'
$f=M 'TrySelectCapabilitySequenceStrategy';Check ((C $f 'TryObserveCapabilityRescueObstacle').Count -eq 1 -and (C $f 'TryObserveCapabilityRescueObstacle')[0].Offset -lt (C $f 'RankCapabilityLearnedRecovery')[0].Offset) 'live obstacle evidence precedes historical generalized ranking'
$f=M 'TryBeginInjectedNativeVault';Check ((C $f 'TryObserveCapabilityRescueObstacle').Count -eq 1 -and (C $f 'TryObserveCapabilityRescueObstacle')[0].Offset -lt (C $f 'MovementContextPlayerAnimatorSetDoVault')[0].Offset) 'transient obstacle injection revalidates current physical obstacle at final actuator'
$f=M 'TryObserveCapabilityRescueObstacle';foreach($n in @('TryGetFirstNonSelfCapsuleCastHit','RescueObstacle')){Check ((C $f $n).Count -eq 1) ('live physical rescue evidence '+$n)}
foreach($n in @('TryDriveCapabilityGroundEntryLead','TryAdmitCapabilityHeadWalkThroughEntry','TryDriveCapabilityRecordedEntryConnector')){Check ((C (M $n) 'CanApproachCapabilityUnfinishedWalkEntry').Count -eq 1) ('queue entry cannot rewind current corridor '+$n)}
$f=M 'CanApproachCapabilityUnfinishedWalkEntry';Check ((C $f 'EntryStillAhead').Count -eq 1) 'queue ingress distinguishes body already on finite current corridor'
$f=M 'TickCapabilityPortalWait';Check ((C $f 'QueueDistance').Count -eq 1) 'queue progress uses exit distance during WalkThrough'
$f=M 'UpdateLearningV349GenericPhysicalProgressWatchdog';Check ((C $f 'TryRequestCapabilityFullRouteRetry').Count -eq 1 -and (C $f 'TryRequestCapabilityFullRouteRetry')[0].Offset -lt (C $f 'TryArmLearningV336TraversalSelfRescue')[0].Offset) 'current typed stall enters sole failure ladder before legacy recovery'
$f=M 'PrepareCapabilityGenericFollowHandoff';foreach($n in @('GenericFollowStart','CanAcceptFollowPath','SamplePosition','TryEnterBoundPhysicalMotion','TryRetireNativeMoverPathPreserveLocomotion','TryScrubLearningV364NativeMoverEpochAnchor','CancelDelivery')){Check ((C $f $n).Count -eq 1) ('actual original Follow call preflight '+$n)}
Check ((C $f 'GenericFollowStart')[0].Offset -lt (C $f 'TryRetireNativeMoverPathPreserveLocomotion')[0].Offset -and (C $f 'TryScrubLearningV364NativeMoverEpochAnchor')[0].Offset -lt (C $f 'Remove')[0].Offset) 'supported tight native epoch is proven before temporary producer suspension'
foreach($n in @('BotOwnerGoToPoint','CommitUnifiedTraversalSuccess','TryTeleportAcrossTraversalExit')){Check ((C $f $n).Count -eq 0) ('handoff cannot replace native formation with custom target '+$n)}
Check (@($f.Body.Instructions|Where-Object {$_.OpCode.Name -eq 'stfld' -and $_.Operand.Name -in @('LearningV349LastPhysicalProgressAt','LastProgressAt','LearningV349GenericPassageLeaseUntil')}).Count -eq 0) 'native call preparation neither refreshes progress nor consumes generic deadline'
$f=M 'CompleteCapabilityGenericFollowHandoff';foreach($n in @('GenericFollowCommit','PeekExactGeneration')){Check ((C $f $n).Count -gt 0) ('native call receipt bound to existing generation and objects '+$n)}
Check (@($f.Body.Instructions|Where-Object {$_.OpCode.Name -eq 'ceq'}).Count -ge 4) 'native completion compares original bot, recovery and epoch object identity'
Check ((C $f 'set_Item').Count -eq 1) 'refused actual call restores same generic producer once'
Check (@($f.Body.Instructions|Where-Object {$_.OpCode.Name -eq 'ldfld' -and $_.Operand.Name -eq 'FreshConsumerPathGeneration'}).Count -eq 1 -and @($f.Body.Instructions|Where-Object {$_.OpCode.Name -eq 'ldfld' -and $_.Operand.Name -eq 'SynchronizedMovementGeneration'}).Count -eq 1) 'actual accepted native consumer needs fresh same-generation path and anchor receipts'
Check (@($f.Body.Instructions|Where-Object {$_.OpCode.Name -eq 'stfld' -and $_.Operand.Name -in @('LearningV349LastPhysicalProgressAt','LastProgressAt')}).Count -eq 0) 'rollback cannot renew no-progress clock'
$f=M 'HasCapabilityNormalFormationConsumer';foreach($n in @('NormalFormation','HasCapabilityLivePassageProducer','HasCapabilityUnresolvedRouteWork')){Check ((C $f $n).Count -eq 1) ('formation proof excludes competing consumer '+$n)}
$f=M 'UpdatePostTraversalIntegrityWatch';Check ((C $f 'HasCapabilityNormalFormationConsumer').Count -eq 1) 'stable and final follow outcomes use actual formation consumer predicate'
$f=M 'TryDriveCapabilityObstacleEntry';foreach($n in @('GetLearningV357ActuatorEntryLateralTolerance','ObstacleNeedsCenter')){Check ((C $f $n).Count -eq 1) ('side correction uses same physical template lane '+$n)}
$f=M 'TryDriveCapabilityGroundEntryLead';Check ((C $f 'TryRetireNativeMoverPathPreserveLocomotion').Count -eq 1 -and (C $f 'TryRetireNativeMoverPathPreserveLocomotion')[0].Offset -lt (C $f 'ApplyCollisionActivePhysicalRouteMotion')[0].Offset) 'queue manual entry retires native path before physical input'
Check ((C $f 'ClearCapabilityNativeDestinationData').Count -eq 1) 'manual queue handoff clears prior destination debt'
$gait=$m.GetTypes()|Where-Object Name -eq 'FollowerCapabilityFollowGaitPatch'
$f=$gait.Methods|Where-Object Name -eq 'FollowPrefix'|Select-Object -First 1;Check ((C $f 'PrepareCapabilityGenericFollowHandoff').Count -eq 1 -and @($f.Parameters|Where-Object Name -eq '__state').Count -eq 1) 'verified native Follow prefix carries synchronous receipt'
$f=$gait.Methods|Where-Object Name -eq 'FollowPostfix'|Select-Object -First 1;Check ((C $f 'CompleteCapabilityGenericFollowHandoff').Count -eq 1 -and (C $f 'CompleteCapabilityGenericFollowHandoff')[0].Offset -lt (C $f 'QuiesceCapabilityOriginalFollowIdle')[0].Offset -and @($f.Parameters|Where-Object Name -eq '__runOriginal').Count -eq 1) 'native completion acknowledgement precedes idle input closure'
$f=$gait.Methods|Where-Object Name -eq 'FollowFinalizer'|Select-Object -First 1;Check ((C $f 'CompleteCapabilityGenericFollowHandoff').Count -eq 1) 'native exception rolls back same bounded producer'
$f=$gait.Methods|Where-Object Name -eq 'Apply'|Select-Object -First 1;foreach($n in @('FollowPrefix','FollowFinalizer')){Check (@($f.Body.Instructions|Where-Object {$_.OpCode.Name -eq 'ldstr' -and $_.Operand -eq $n}).Count -eq 1) ('reviewed PIT build installs '+$n)}
$f=M 'TickCapabilityPortalWait';Check ((C $f 'LeftAdmittedIngress').Count -eq 1 -and (C $f 'LeftAdmittedIngress')[0].Offset -lt (C $f 'InWindow')[0].Offset) 'admitted ingress escape cannot silently cancel before failure arbitration'
Check ((C $f 'TryRequestCapabilityFullRouteRetry').Count -eq 3) 'queue head escape measured wrong-way motion and stall share existing failure ladder'
$f=M 'IsCapabilityJumpTriggerReady';foreach($n in @('IsProbeLifecycleCurrent','TryGetLearnedCommitmentForProbe','UnsafeJumpSpeed','IsLearningV450LiveEftJumpAttemptLegal','IsLearningV449JumpInsidePhysicalTriggerHorizon')){Check ((C $f $n).Count -eq 1) ('last Jump consumer shares actual live physical contract '+$n)}
foreach($n in @('IsLearningV411JumpTakeoffModeReady','IsLearningV329JumpTakeoffMomentumReady','SprintFloor')){Check ((C $f $n).Count -eq 0) ('historical preparation cannot veto actual legal Jump '+$n)}
$f=M 'IsLearningV450LiveEftJumpAttemptLegal';Check ((C $f 'LiveJumpAttempt').Count -eq 1) 'all existing Jump admission consumers use same tested live policy'
$f=M 'TryDriveCapabilityLocalWalkThroughEntry';Check ((C $f 'PhysicalWalkEntry').Count -eq 1 -and (C $f 'PhysicalWalkEntry')[0].Offset -lt (C $f 'LocalEntryPath')[0].Offset) 'current recorded WT entry outranks displaced Nav projection before resource selection'
Check ((C $f 'TryGetCapabilitySupportedLocalAim').Count -eq 0 -and (C $f 'EntryHeight').Count -eq 1 -and (C $f 'ApplyCollisionActivePhysicalRouteMotion').Count -eq 1) 'projection mismatch uses same grounded collision-active WT entry, not a second forecast-only veto'
$f=M 'TryGetCapabilitySupportedLocalAim';foreach($n in @('LocalAimFraction','GroundAim','TryGetFirstNonSelfRaycastHit','TryGetFirstNonSelfCapsuleCastHit')){Check ((C $f $n).Count -eq 1) ('bounded slope approach has real terrain sensor '+$n)}
foreach($n in @('TryTeleportAcrossTraversalExit','CommitUnifiedTraversalSuccess','BotOwnerGoToPoint')){Check ((C $f $n).Count -eq 0) ('local aim has no body relocation or alternate route writer '+$n)}
$f=M 'DriveCapabilityStrategyApproach';Check ((C $f 'TryGetCapabilitySupportedLocalAim').Count -eq 1 -and (C $f 'TryGetCapabilitySupportedLocalAim')[0].Offset -lt (C $f 'ApplyCollisionActivePhysicalRouteMotion')[0].Offset) 'pending retry samples finite supported ramp before motion'
Check ((C $f 'TryRetireNativeMoverPathPreserveLocomotion').Count -eq 2 -and (C $f 'ClearCapabilityNativeDestinationData').Count -eq 1) 'pending physical approach retires old native target before its own input'
$f=M 'IsCapabilityCurrentPassageInFlight';Check ((C $f 'PassageInFlight').Count -eq 1 -and @($f.Body.Instructions|Where-Object {$_.OpCode.Name -eq 'ldfld' -and $_.Operand.Name -eq 'PortalQueueClaimSerial'}).Count -eq 1) 'passage exemption belongs to already admitted current mission and finite recorded lane'
$f=M 'TickCapabilityPortalWait';Check ((C $f 'IsCapabilityCurrentPassageInFlight').Count -eq 1 -and (C $f 'IsCapabilityCurrentPassageInFlight')[0].Offset -lt (C $f 'LeftAdmittedIngress')[0].Offset) 'finite in-flight passage protects distance cancellation and false failure'
$live=@($m.GetTypes()|ForEach-Object {$_.Methods}|Where-Object {$_.Name -like '*TickCapabilityPortalWait*Live*'})
Check ($live.Count -eq 1 -and (C $live[0] 'IsCapabilityCurrentPassageInFlight').Count -eq 1) 'other bots cannot evict an in-flight ticket as a distant observer'
$f=M 'IsCapabilityJumpTriggerReady';Check ((C $f 'TryGetLearningV459ReservedJumpExactSuccessorContinuity').Count -eq 1 -and (C $f 'IsCf60LegalOneFrameJumpTriggerForecast').Count -eq 1 -and (C $f 'AtomicJumpReceipt').Count -eq 1) 'final consumer requires scoped atomic receipt for one-frame forecast; exact successor reservation retained'
# RC145 integration: protect every real admission-to-trigger path, not just scalar policy.
foreach($method in @('TryConsumeLearningV429AtomicJumpAdmission','TryTriggerLearningV354RollingJumpActuatorLead')){
 $f=M $method;$mark=@(C $f 'MarkLearningV32NativeEntryAcquired');$epoch=@(C $f 'BeginLearningV32NativeExecutionEpoch');$trigger=@(C $f 'TriggerNativeJump')
 Check ($mark.Count -eq 1 -and $epoch.Count -eq 1 -and $trigger.Count -eq 1) ($method+' publishes shared entry witness')
 Check ($mark[0].Offset -lt $epoch[0].Offset -and $epoch[0].Offset -lt $trigger[0].Offset) ($method+' entry publication precedes epoch and actual trigger')
}
$f=M 'MarkLearningV32NativeEntryAcquired';Check ((C $f 'CaptureCapabilityNativeJumpEntry').Count -eq 1) 'atomic publication also captures same Jump entry witness'
$f=M 'TriggerNativeJump';Check ((C $f 'IsCapabilityJumpTriggerReady').Count -eq 1) 'all real native dispatches still consume final gate'
Check ($f.Parameters.Count -eq 2 -and $f.Parameters[1].ParamDef.HasConstant -and !$f.Parameters[1].ParamDef.Constant.Value) 'ordinary trigger callers default to no atomic receipt'
$f=M 'IsCapabilityJumpTriggerReady';foreach($n in @('AtomicJumpReceipt','JumpTriggerWindow','PeekExactGeneration','IsCf60LegalOneFrameJumpTriggerForecast')){Check ((C $f $n).Count -eq 1) ('same-frame forecast revalidates '+$n)}
$f=M 'HasCapabilityMeasuredMissedJumpWindow';Check ((C $f 'TryObserveCapabilityJumpBodyVelocity').Count -eq 1 -and (C $f 'MissedJumpWindow').Count -eq 1) 'missed queue window requires current measured body sample'
$f=M 'TryRecoverCapabilityStalledJumpPreparation';Check ((C $f 'HasCapabilityMeasuredMissedJumpWindow')[0].Offset -lt (C $f 'HorizontalProgress')[0].Offset) 'missed frontier fails before further forward preparation or watchdog refresh'
$f=M 'TryCaptureCapabilityJumpPreparation';Check ((C $f 'TryValidateV2CurrentTypedStep').Count -eq 1) 'queue waiter cannot consume pre-native preparation'
$f=M 'TryValidateV2CurrentTypedStep';Check ((C $f 'IsCapabilityPortalWaiting').Count -eq 1) 'same typed-step guard retains queue exclusivity'
$f=M 'TryConsumeLearningV429AtomicJumpAdmission';$call=@(C $f 'TriggerNativeJump')[0];$ix=$f.Body.Instructions.IndexOf($call)
Check ($f.Body.Instructions[$ix-1].OpCode.Name -eq 'ldc.i4.1') 'only successful atomic reservation supplies stack receipt'
$callers=@($r.Methods|Where-Object {(C $_ 'TriggerNativeJump').Count -gt 0});$trueCount=0;$falseCount=0
foreach($caller in $callers){foreach($call in @(C $caller 'TriggerNativeJump')){$ix=$caller.Body.Instructions.IndexOf($call);$arg=$caller.Body.Instructions[$ix-1].OpCode.Name;if($arg -eq 'ldc.i4.1'){$trueCount++}elseif($arg -eq 'ldc.i4.0'){$falseCount++}else{throw 'unknown atomic receipt producer'}}}
Check ($trueCount -eq 1 -and $falseCount -eq 6) 'all seven actual trigger call sites have explicit receipt provenance'
$f=M 'TickCapabilityFullRouteRetry';Check ((C $f 'ReturnDiverging').Count -eq 1 -and (C $f 'ReturnDiverging')[0].Offset -lt (C $f 'ReturnDecision')[0].Offset) 'measured return divergence retires stale native motor before KeepPath'
$observe=@(C $f 'TryObserveCapabilityReturnRemaining')[-1];$ix=$f.Body.Instructions.IndexOf($observe);$writes=@($f.Body.Instructions|Where-Object {$_.Offset -gt $observe.Offset -and $_.OpCode.Name -eq 'stfld' -and $_.Operand.Name -eq 'LastProgressAt'})
Check ($writes.Count -eq 0) 'native return allocation cannot renew measured progress deadline'
$f=M 'TickCapabilityFullRouteRetry';$observe=@(C $f 'TryObserveCapabilityReturnRemaining')[-1]
$baseline=@($f.Body.Instructions|Where-Object {$_.Offset -gt $observe.Offset -and $_.OpCode.Name -eq 'stfld' -and $_.Operand.Name -eq 'LastProgressPosition'})
Check ($baseline.Count -eq 1) 'replacement return path starts a fresh body-distance baseline without refreshing progress time'
# RC146 preserves WT identity and finite physical proof across every handoff.
$f=M 'TryResolveUnifiedTraversalEgressTarget';Check ((C $f 'TryResolveCapabilityWalkThroughEgress').Count -eq 1 -and (C $f 'TryResolveCapabilityWalkThroughEgress')[0].Offset -lt (C $f 'IsLearningV340DownstreamBridgeKind')[0].Offset) 'current WT egress precedes broad settled/NavMesh projection'
$f=M 'TryResolveCapabilityWalkThroughEgress';foreach($n in @('IsProbeLifecycleCurrent','TrySampleTraversalSettledAnchor','WalkExitSample')){Check ((C $f $n).Count -eq 1) ('WT exit remains exact and locally projected '+$n)}
$f=M 'TryCloseCapabilityProvenWalkThrough';foreach($n in @('IsProbeLifecycleCurrent','TryGetLearnedCommitmentForProbe','TryConsumeLearningV414WalkThroughPhysicalPassBeforeRecovery')){Check ((C $f $n).Count -eq 1) ('WT post-phase closure reuses existing physical authority '+$n)}
foreach($n in @('CommitUnifiedTraversalSuccess','TryTeleportAcrossTraversalExit','MarkFollowerTraversalEpisodeCompleted')){Check ((C $f $n).Count -eq 0) ('adapter cannot mint success '+$n)}
foreach($n in @('BeginExitAuthorityCommit','UpdateExitAuthorityCommit')){$f=M $n;Check ((C $f 'TryCloseCapabilityProvenWalkThrough').Count -eq 1) ($n+' consumes strict WT pass before legacy topology')}
$f=M 'UpdateExitAuthorityCommit';Check ((C $f 'TryCloseCapabilityProvenWalkThrough')[0].Offset -lt (C $f 'UpdateExitAuthorityTopologyRejoin')[0].Offset) 'full finite WT crossing wins before stale linked-anchor topology'
$f=M 'UpdateLearningV349GenericPhysicalProgressWatchdog';Check ((C $f 'AttemptStallAge').Count -eq 1 -and (C $f 'AttemptStallAge')[0].Offset -lt (C $f 'TryRequestCapabilityFullRouteRetry')[0].Offset) 'watchdog failure belongs to current attempt age'
$f=M 'TryYieldLearningV402PersistentRouteTransactionToTerminalRecovery';Check ((C $f 'TerminalMayYield').Count -eq 1 -and (C $f 'HasActiveTraversalProbe').Count -eq 1) 'terminal producer cannot tear down active native probe'
$f=M 'TryPrepareCapabilityAutomaticProbeMission';Check ((C $f 'TrySupersedeLearningV362TerminalRecoveryByFreshRouteEpoch').Count -eq 1) 'old terminal epoch resolved at pre-probe allocation boundary'
$f=M 'GetCapabilityWalkingRecoveryProof';Check ((C $f 'Where').Count -eq 1) 'walking proof comes from learned same-portal candidates'
$proof=@($m.GetTypes()|ForEach-Object {$_.Methods}|Where-Object {$_.Name -like '*GetCapabilityWalkingRecoveryProof*' -and (C $_ 'SameWalkingPortal').Count -eq 1})
Check ($proof.Count -eq 1 -and (C $proof[0] 'IsLearningV3NativeProvenEdge').Count -eq 1) 'walking contract requires actual native-proven WT and geometric identity'
$f=M 'TrySelectCapabilitySequenceStrategy';foreach($n in @('IsCapabilityWalkingRecoveryOnly','TrySelectCapabilityWalkingRescue','WalkingRecoveryKind')){Check ((C $f $n).Count -eq 1) ('alternative and rescue honour WT contract '+$n)}
Check ((C $f 'TrySelectCapabilityWalkingRescue')[0].Offset -lt (C $f 'TryObserveCapabilityRescueObstacle')[0].Offset) 'walking rescue does not require an actuator obstacle or fall through to scanner'
$f=M 'TrySelectCapabilityWalkingRescue';foreach($n in @('GetCapabilityWalkingRecoveryProof','WalkingRescue','CapabilitySemanticCommitment')){Check ((C $f $n).Count -eq 1) ('WT self-rescue remains local and typed '+$n)}
foreach($n in @('TryBeginInjectedNativeVault','TriggerNativeVaultScan','TriggerNativeJump','CommitUnifiedTraversalSuccess')){Check ((C $f $n).Count -eq 0) ('WT rescue cannot dispatch actuator or write success '+$n)}
$f=M 'TryAdmitCapabilitySequenceProbe';Check ((C $f 'CanAllocateCapabilityWalkingKind').Count -eq 1 -and (C $f 'CanAllocateCapabilityWalkingKind')[0].Offset -lt (C $f 'HasCapabilityFailureStrategy')[0].Offset) 'all allocation sources reject non-WT before native/sequence/legacy branch selection'
$f=M 'CanAllocateCapabilityWalkingKind';Check ((C $f 'WalkingRecoveryKind').Count -eq 1 -and (C $f 'IsCapabilityWalkingRecoveryOnly').Count -eq 1) 'final gate binds current unresolved WT debt to the same proven walking contract'
$f=M 'TryDriveCapabilityLocalWalkThroughEntry';Check ((C $f 'EntryHeight')[0].Offset -lt (C $f 'ApplyCollisionActivePhysicalRouteMotion')[0].Offset) 'restored grounded entry still gates collision-active move by height'
$f=M 'PrepareCapabilityStationaryRescue';$wt=@($f.Body.Instructions|Where-Object {$_.OpCode.Name -eq 'ldstr' -and $_.Operand -eq 'WalkThrough'})
Check ($wt.Count -eq 1 -and $wt[0].Offset -lt (C $f 'ApplyCollisionActivePhysicalRouteMotion')[0].Offset) 'walking rescue bypasses actuator stationary braking and geometry rebasing'
$f=M 'IsCapabilityWalkingRecoveryOnly';Check ((C $f 'GetCapabilityWalkingRecoveryProof').Count -eq 1) 'WT-only classification cannot lose its native walking proof consumer'
$f=M 'TryCloseCapabilityProvenWalkThrough';$call=@(C $f 'TryConsumeLearningV414WalkThroughPhysicalPassBeforeRecovery')[0];$ix=$f.Body.Instructions.IndexOf($call)
Check ($f.Body.Instructions[$ix-1].OpCode.Name -eq 'ldc.i4.0') 'ExitCommit closure explicitly disables Egress/Verify restart fallback'
$f=M 'TryConsumeLearningV414WalkThroughPhysicalPassBeforeRecovery';Check ($f.Parameters.Count -eq 2 -and $f.Parameters[1].ParamDef.HasConstant -and $f.Parameters[1].ParamDef.Constant.Value) 'legacy WT consumers retain their explicit original egress fallback'
$call=@(C $f 'BeginUnifiedTraversalEgressOrVerify')[-1];$ix=$f.Body.Instructions.IndexOf($call);$tail=@($f.Body.Instructions|Where-Object {$_.Offset -gt ($call.Offset-24) -and $_.Offset -lt $call.Offset})
Check (@($tail|Where-Object {$_.OpCode.Name -eq 'ldarg.1'}).Count -eq 1 -and @($tail|Where-Object {$_.OpCode.Name -eq 'ret'}).Count -ge 1) 'completion-only failure returns without re-entering a fresh egress epoch'
# RC147 finite episodes, shared gait and observed wall-aware route target.
$f=M 'BeginWalkThroughProbe';Check ((C $f 'TryExtendWalkThroughProbeWithRouteMissionComposite').Count -eq 0) 'one physical WT attempt cannot consume a child episode before root completion'
$f=M 'GetCurrentWalkThroughCorridorTarget';Check ((C $f 'SelectCapabilityWalkThroughCornerTarget').Count -eq 1) 'lookahead follows bounded route chord'
$f=M 'SelectCapabilityWalkThroughCornerTarget';foreach($n in @('ChordDeviation','RejoinChordInside')){Check ((C $f $n).Count -eq 1) ('corner geometry '+$n)}
$f=M 'TryConstrainCapabilityWalkThroughTarget';foreach($n in @('IsProbeLifecycleCurrent','TryGetFirstNonSelfCapsuleCastHit','WallBlocks')){Check ((C $f $n).Count -eq 1) ('current WT wall sensor '+$n)}
$f=M 'TryPublishLearningV414PathlessNativeInputLease';foreach($n in @('TryConstrainCapabilityWalkThroughTarget','TryGetCapabilityRouteSprint')){Check ((C $f $n).Count -eq 1) ('one native WT target/input '+$n)}
Check ((C $f 'TryCommitV2ProbePhysicalMotion').Count -eq 2) 'blocked target holds through existing exact physical writer without legacy fallback'
$f=M 'NeedsCapabilityEntryRun';foreach($n in @('EntryCorrection','IsCapabilityReturnInProgress','IsProbeLifecycleCurrent')){Check ((C $f $n).Count -eq 1) ('entry-only precision '+$n)}
$f=M 'ApplyCapabilityReturnWalking';foreach($n in @('IsCapabilityRetryWalking','TryGetCapabilityRouteSprint')){Check ((C $f $n).Count -eq 1) ('return approach speed '+$n)}
$f=M 'GetTraversalCompletionMode';Check ((C $f 'IndexOf').Count -eq 0) 'reason labels never fabricate teleport or assisted execution truth'
$f=M 'EnforceLearningV368TraversalProducerOwnership';Check ((C $f 'HasCapabilityUnresolvedRouteWork').Count -eq 1 -and (C $f 'HasCapabilityUnresolvedRouteWork')[0].Offset -lt (C $f 'TryRetireNativeMoverPathPreserveLocomotion')[0].Offset) 'live queue strategy and return debt blocks obsolete orphan Follow takeover'
foreach($name in @('UpdateWalkThroughEntryConvergence','TryDriveCapabilityLocalWalkThroughEntry','NeedsCapabilityEntryRun')){Check ((C (M $name) 'GetCapabilityWalkThroughEntryTarget').Count -eq 1) ('same current episode entry target '+$name)}
$f=M 'GetCapabilityWalkThroughEntryTarget';Check (@($f.Body.Instructions|Where-Object{$_.Operand.Name -eq 'LearningV372CurrentEpisodeWalkThroughCorridorAuthority'}).Count -eq 1) 'current episode lane outranks former semantic root'
# RC148 queue head admission owns physical epoch until the actual action takes over.
$f=M 'ProtectCapabilityLocalBodyProjection';Check ((C $f 'OwnsCapabilityAdmittedIngress').Count -eq 1) 'released queue head still rejects stale native body projection'
$f=M 'OwnsCapabilityAdmittedIngress';foreach($n in @('OwnedIngress','Owns')){Check ((C $f $n).Count -eq 1) ('finite exact admitted ingress '+$n)}
$f=M 'TryPrepareCapabilityPortalApproachDelivery';Check ((C $f 'TryScrubLearningV364NativeMoverEpochAnchor').Count -eq 1 -and (C $f 'TryScrubLearningV364NativeMoverEpochAnchor')[0].Offset -lt (C $f 'TransitionTraversalToFollowIfIdleExact')[0].Offset) 'native bookkeeping anchored at current body before admitted approach Follow handoff'
$f=M 'ShouldSuppressNativeNavMeshBodyRelink';Check ((C $f 'OwnsCapabilityAdmittedIngress').Count -eq 1) 'projection rejection logs exact queue-head scope'
$f=M 'ApplyProbeLocomotionIntent';Check ((C $f 'TryGetCapabilityRouteSprint').Count -eq 1 -and (C $f 'TryGetCapabilityRouteSprint')[0].Offset -lt (C $f 'BotMoverSprint')[0].Offset) 'legacy locomotion setter shares physical-input gait decision'
$f=M 'TryGetCapabilityRouteSprint';Check ((C $f 'ExecutionGaitPhase').Count -eq 1) 'entry/execution planner never rearms Sprint in egress verify or exit commit'
# RC149 exit handoff is bookkeeping, never a body relocation or rollback replay.
$f=M 'ArmExitAuthorityRebase';foreach($n in @('BotMoverTeleport','PlayerTeleport')){Check ((C $f $n).Count -eq 0) ('exit body cannot be restored to historical anchor '+$n)}
foreach($n in @('TryRetireNativeMoverPathPreserveLocomotion','TryScrubLearningV364NativeMoverEpochAnchor')){Check ((C $f $n).Count -eq 1) ('body-preserving exit bookkeeping '+$n)}
Check ((C $f 'ShouldPreserveCapabilityProbeGait').Count -eq 2) 'legacy exit speed writes require real qualified gait'
$f=M 'UpdateExitAuthorityCommit';Check ((C $f 'ArmExitAuthorityRebase').Count -eq 0) 'physical regression fails original handoff instead of retrying historical pose'
$f=($m.GetTypes()|Where-Object Name -eq 'FollowerControlBackend').Methods|Where-Object Name -eq 'FindSite';foreach($uid in @('R1-EFB80A0B501C','R1-F154F5DE1591')){Check (@($f.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldstr' -and $_.Operand -eq $uid}).Count -eq 0) ('retired exit body writer absent from catalog '+$uid)}
$f=M 'TryDriveCapabilityObstacleEntry';Check ((C $f 'DriveTraversalPoseAcquisition').Count -eq 1 -and (C $f 'ApplyCollisionActivePhysicalRouteMotion')[0].Offset -lt (C $f 'DriveTraversalPoseAcquisition')[0].Offset) 'entry turn hold has matching bounded pose consumer after physical input'
Check (@($f.Body.Instructions|Where-Object{$_.Operand.Name -eq 'PoseExclusiveOrientationAuthority'}).Count -eq 1) 'entry turn completion retains pose consumer until existing orientation authority releases'
$f=M 'TickCapabilityPortalWait';Check ((C $f 'QueueMovingAway').Count -eq 1) 'owned queue head detects measured wrong-way approach without new watchdog'
$f=M 'UpdateUnifiedTraversalVerify';Check ((C $f 'TryCorrectCapabilityDropVerify').Count -eq 1) 'stationary dropped-back Verify can request bounded local forward correction'
$f=M 'TryCorrectCapabilityDropVerify';foreach($n in @('IsProbeLifecycleCurrent','VerifyExitCorrection','TryGetCapabilitySupportedLocalAim','TryCommitV2ProbePhysicalMotion')){Check ((C $f $n).Count -eq 1) ('Verify correction reuses existing current physical input '+$n)}
foreach($n in @('TraversalVerifyStableSince','StageStartedAt','TraversalVerifyStartedAt','OwnershipLeaseExpiresAt')){Check (@($f.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'stfld' -and $_.Operand.Name -eq $n}).Count -eq 0) ('correction never grants progress or extends proof '+$n)}
# RC150 entry distance cannot cancel an owned typed descent or a qualified upper landing.
$f=M 'TryDispatchCapabilityCurrentAction';Check ((C $f 'ImmediateDispatchKind').Count -eq 1) 'same existing selector dispatches Jump DropDown and WT before ingress scan can overshoot'
$f=M 'IsCapabilityCurrentPassageInFlight';Check ((C $f 'DropInFlight').Count -eq 1) 'finite current DropDown volume protects real descent without granting success'
$f=M 'TickCapabilityPortalWait';Check ((C $f 'QueueDistance').Count -eq 1) 'queue progress still consumes one measured remaining-distance policy'
$f=M 'TryCloseCapabilityClimbAtSuccessor';Check ((C $f 'TryGetLearningV341ImmediateSemanticSuccessor').Count -eq 1) 'current-raid late successor receives existing native proof and same chain validator'
$f=M 'TryBeginPlayerRouteContinuationBridge';Check ((C $f 'TryCloseCapabilityClimbAtSuccessor').Count -eq 1 -and (C $f 'TryCloseCapabilityClimbAtSuccessor')[0].Offset -lt (C $f 'TryGetTraversalDownstreamEvidence')[0].Offset) 'upper landing completion is offered before ground navigation evidence'
$f=M 'TryRecoverPlayerValidatedTraversal';Check ((C $f 'TryCloseCapabilityClimbAtSuccessor').Count -eq 1 -and (C $f 'TryCloseCapabilityClimbAtSuccessor')[0].Offset -lt (C $f 'TryProveLearningV441JumpTypedSuccessorFrontier')[0].Offset) 'real completed Climb checked before another recovery kind can consume its debt'
$f=M 'TryPreserveCf23NativeVaultClimbLandingProgress';Check ((C $f 'ClimbUpperPending').Count -eq 1) 'upper plane uses current Climb geometry even before successor enters mission snapshot'
Check ((C $f 'get_normalized').Count -eq 1) 'Climb native progress uses full 3D authored axis'
Check (@($f.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'stfld' -and $_.Operand.Name -in @('StageStartedAt','OwnershipLeaseExpiresAt')}).Count -eq 0) 'native landing progress never renews its own absolute timeout'
# RC151 actual resource ownership and phase-specific rescue.
$f=M 'TryDriveCapabilityLocalWalkThroughEntry';foreach($n in @('SupportedEntry','TryValidateCapabilitySemanticWalkingRamp')){Check ((C $f $n).Count -eq 1 -and (C $f $n)[0].Offset -lt (C $f 'ApplyCollisionActivePhysicalRouteMotion')[0].Offset) ('WT upstream slope requires grounded measured support '+$n)}
$f=M 'UpdateLearningV375PersistentExecutablePhysicalRouteTransaction';Check ((C $f 'TryValidateCapabilityPortalApproachPath').Count -eq 1 -and (C $f 'TryValidateCapabilityPortalApproachPath')[0].Offset -lt (C $f 'GetOrCreateLearningV375RouteCursor')[0].Offset) 'producer yields to deliverable path rather than semantic target alone'
$f=M 'TryBeginInjectedNativeVault';Check ((C $f 'RescueScanRequired').Count -eq 1 -and (C $f 'RescueScanRequired')[0].Offset -lt (C $f 'TryObserveCapabilityRescueObstacle')[0].Offset) 'alternate template does not inherit generalized rescue ray gate'
Check ((C $f 'HasCapabilityFailureStrategy').Count -eq 1) 'rescue stage filter uses current selected sequence'
$f=M 'TryValidateWalkThroughCorridor';Check ((C $f 'CorridorSegmentLength').Count -eq 1 -and (C $f 'CorridorSegmentLength')[0].Offset -lt (C $f 'TryGetFirstNonSelfCapsuleCastHit')[0].Offset) 'short polyline fragment still runs existing capsule clearance'
$f=M 'TryValidateWalkThroughPolyline';$v=@(C $f 'TryValidateWalkThroughCorridor')[0];$ix=$f.Body.Instructions.IndexOf($v);Check ($f.Body.Instructions[$ix-1].OpCode.Name -eq 'ldc.i4.1') 'polyline alone opts into sampling fragment minimum'
$f=M 'TryDispatchCapabilityCurrentAction';Check ((C $f 'TryGetLearningV424ActuatorExecutorAdmission').Count -eq 1 -and (C $f 'TryGetLearningV424ActuatorExecutorAdmission')[0].Offset -lt (C $f 'TryStartEmpiricalLocalEntryBridge')[0].Offset) 'immediate obstacle cadence preserves exact original admission before probe allocation'
# RC152 selected geometry, ticket identity and one typed egress input producer.
$f=M 'TryDriveCapabilityLocalWalkThroughEntry';Check ((C $f 'RestoreSemanticWalkEntry').Count -eq 1 -and (C $f 'RestoreSemanticWalkEntry')[0].Offset -lt (C $f 'PhysicalWalkEntry')[0].Offset) 'selected WT retry keeps its physical entry rather than semantic original'
Check ((C $f 'HasCapabilityFailureStrategy').Count -eq 1) 'walking entry restore is scoped to current failure strategy'
$f=M 'TryDriveCapabilityObstacleEntry';Check ((C $f 'ObstacleCenterTolerance').Count -eq 1 -and (C $f 'ObstacleCenterTolerance')[0].Offset -lt (C $f 'ObstacleNeedsCenter')[0].Offset) 'centering target derives from native radial envelope and learned lane'
$f=M 'TickCapabilityPortalWait';Check ((C $f 'QueueClaimCurrent').Count -eq 1 -and (C $f 'QueueClaimCurrent')[0].Offset -lt (C $f 'LeftAdmittedIngress')[0].Offset) 'changed entry invalidates old ticket before lost-ingress failure can fire'
$f=M 'UpdateUnifiedTraversalEgress';Check ((C $f 'NativeEgressKind').Count -eq 1 -and (C $f 'NativeEgressKind')[0].Offset -lt (C $f 'HoldForwardInput')[0].Offset) 'typed current DropDown selects native world input before legacy forward hold'
Check ((C $f 'IsProbeLifecycleCurrent').Count -eq 1) 'pre-acquisition DropDown requires exact live probe lifecycle'
# RC154 current-entry start, proof identity, one facing resolver, finite clear-corner and queued observation.
$f=M 'BeginWalkThroughProbe';Check ((C $f 'GetCapabilityWalkThroughEntryTarget').Count -eq 1) 'actual WT Begin uses same current entry as arrival and motion'
$proof=@($m.GetTypes()|ForEach-Object {$_.Methods}|Where-Object {$_.Name -like '*GetCapabilityWalkingRecoveryProof*' -and (C $_ 'WalkingIdentity').Count -eq 1});Check ($proof.Count -eq 1) 'native-proven semantic identity retains WT-only contract'
$f=M 'TryDriveCapabilityObstacleEntry';Check ((C $f 'SideCenterTurn').Count -eq 1) 'lateral center is not blocked by impossible side-facing pose'
$f=M 'ApplyCollisionActivePhysicalRouteMotion';Check ((C $f 'TryGetCapabilityObstacleCenterFacing').Count -eq 1 -and (C $f 'TryGetCapabilityObstacleCenterFacing')[0].Offset -lt (C $f 'BotSteeringLookToDirection')[0].Offset) 'existing steering writer resolves obstacle axis before projecting lateral input'
$f=M 'TryGetCapabilityObstacleCenterFacing';foreach($n in @('IsProbeLifecycleCurrent','ObstacleCenterTolerance','CenterSideOnly','ObstacleNeedsCenter')){Check ((C $f $n).Count -eq 1) ('axis-facing query requires exact local centering '+$n)}
$f=M 'TryConstrainCapabilityWalkThroughTarget';Check ((C $f 'TrySelectCapabilityClearForwardCorner').Count -eq 1) 'blocked long chord may select a collision-validated authored forward target'
$f=M 'TrySelectCapabilityClearForwardCorner';foreach($n in @('TryGetCapabilitySupportedLocalAim','ForwardCorner','SampleWalkThroughCorridorAtDistance')){Check ((C $f $n).Count -eq 1) ('corner selection reuses measured finite corridor '+$n)}
Check (@($f.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'stfld'}).Count -eq 0) 'corner selection cannot advance body, progress, proof or deadline fields'
$f=M 'CommitCapabilityPortalHold';Check ((C $f 'QueuedDropWitness').Count -eq 1 -and (C $f 'QueuedDropWitness')[0].Offset -lt (C $f 'EndLearningV375PersistentRouteTransaction')[0].Offset) 'queue hold suspends existing upper witness rather than deleting its physical epoch'
$f=M 'TickCapabilityPortalWait';Check ((C $f 'TryAdvanceLearningV468ObservedDropDownLandingDebt').Count -eq 1 -and (C $f 'TryAdvanceLearningV468ObservedDropDownLandingDebt')[0].Offset -lt (C $f 'CommitCapabilityPortalHold')[0].Offset) 'queue observes exact natural landing before issuing a further hold'
$f=M 'UpdateLearningV375PersistentExecutablePhysicalRouteTransaction';Check ((C $f 'IsCapabilityPortalWaiting')[0].Offset -lt (C $f 'GetOrCreateLearningV375RouteCursor')[0].Offset) 'retained queue epoch cannot resume a motion producer while waiting'
$f=($m.GetTypes()|Where-Object Name -eq 'FollowerCapabilityPassagePolicy').Methods|Where-Object Name -eq 'SideCenterTurn';Check ((C $f 'ObstacleCenterTurn').Count -eq 1) 'upstream centering retains original bounded pose-turn policy'
$f=M 'TickCapabilityPortalWait';Check ((C $f 'GetCapabilityQueueAnchor').Count -eq 2) 'own and nearest competitor use same canonical portal key'
$f=M 'GetCapabilityQueueAnchor';Check ((C $f 'CanonicalQueue').Count -eq 1 -and (C $f 'IsLearningV3NativeProvenEdge').Count -eq 1) 'stable queue anchor requires bounded native walking proof'
$f=M 'TryDispatchCapabilityCurrentAction';foreach($n in @('ObservedLowerDrop','ImmediateEntryDistance')){Check ((C $f $n).Count -eq 1 -and (C $f $n)[0].Offset -lt (C $f 'JumpDispatch')[0].Offset) ('observed lower continuation reuses existing allocation frontier '+$n)}
Check ((C $f 'PeekExactGeneration').Count -eq 1) 'lower continuation only reads current writer generation, never acquires a fresh proof epoch'
# RC154 exact episode geometry and predictive walking gait use existing producer only.
$f=M 'TryGetCapabilityRouteSprint';Check ((C $f 'NeedsCapabilityWalkThroughPrecision').Count -eq 1) 'all existing execution gait including released queue head previews finite WT bend'
$f=M 'NeedsCapabilityWalkThroughPrecision';foreach($n in @('IsProbeLifecycleCurrent','WalkingTurnPrecision','WalkingLanePrecision','IsRouteMissionSameChainImmediateSuccessor','BrakingDistance')){Check ((C $f $n).Count -ge 1) ('read-only finite lane gait query '+$n)}
Check (@($f.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'stfld'}).Count -eq 0) 'gait preview cannot advance progress proof timers or body fields'
foreach($n in @('TryCommitV2ProbePhysicalMotion','ApplyCollisionActivePhysicalRouteMotion','FinishProbeWithoutReanchor')){Check ((C $f $n).Count -eq 0) ('gait preview cannot become competing controller '+$n)}
$f=M 'BeginWalkThroughProbe';foreach($n in @('TryGetLearningV372CurrentEpisodeWalkThroughCorridorWitness','WalkingInterior')){Check ((C $f $n).Count -eq 1 -and (C $f $n)[0].Offset -lt (C $f 'PrepareWalkThroughCompletionTarget')[0].Offset) ('Begin uses bounded same current episode lane '+$n)}
$f=M 'TrySelectCapabilityWalkingRescue';Check (@($f.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldstr' -and $_.Operand -eq 'capabilityWalkingRescueUnavailable'}).Count -eq 2) 'WT rescue refusal explains resource or finite envelope and preserves WT-only rule'
$f=M 'TickCapabilityPortalWait';foreach($n in @('portalOwnedByOtherBot','oneSecondCompletionCooldown','nearerEligibleBot','admissionNotGranted')){Check (@($f.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldstr' -and $_.Operand -eq $n}).Count -eq 1) ('queue diagnostics distinguish actual admission wait '+$n)}
$f=M 'FinalizeActiveWalkThroughEpisode';Check ((C $f 'ApplyLearningV372CurrentEpisodeWalkThroughExecutionGeometry').Count -eq 2) 'both finalized recorded WT branches publish current physical lane before alias selects old parent geometry'
Check ((C $f 'RegisterTraversalEpisodeForEdge')[0].Offset -lt (C $f 'ApplyLearningV372CurrentEpisodeWalkThroughExecutionGeometry')[0].Offset -and (C $f 'RegisterTraversalEpisodeForEdge')[-1].Offset -lt (C $f 'ApplyLearningV372CurrentEpisodeWalkThroughExecutionGeometry')[-1].Offset) 'current lane binding consumes the actual registered episode identity'
$f=($m.GetTypes()|Where-Object Name -eq 'BotTraversalPlugin').Methods|Where-Object Name -eq 'Awake';Check (@($f.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldstr' -and $_.Operand -eq 'Bot Traversal 1.0.1 loaded: V2-RC158-SPT416'}).Count -eq 1) 'startup human Beta label matches release marker'
$f=M 'TryGetCapabilityRouteSprint';Check ((C $f 'WalkingGaitCorrection').Count -eq 1) 'only entry precision can force Run; in-passage Sprint intent survives bends'
$f=M 'TryPublishLearningV414PathlessNativeInputLease';foreach($n in @('WalkingTurnSpeed','NeedsCapabilityWalkThroughPrecision')){Check ((C $f $n).Count -eq 1 -and (C $f $n)[0].Offset -lt (C $f 'TryCommitV2ProbePhysicalMotion')[-1].Offset) ('predictive bend tapers existing native Sprint speed before same bound writer '+$n)}
$f=M 'ApplyCollisionActivePhysicalRouteMotion';foreach($n in @('WalkingTurnSpeed','NeedsCapabilityWalkThroughPrecision')){Check ((C $f $n).Count -eq 1 -and (C $f $n)[0].Offset -gt (C $f 'TryGetCapabilityRouteSprint')[0].Offset -and (C $f $n)[0].Offset -lt (C $f 'BotOwnerSetTargetMoveSpeed')[0].Offset) ('central Sprint full-speed restoration cannot overwrite finite WT envelope '+$n)}
# RC155 live definition / learning ranking / bounded queue-head ingress.
$f=M 'TrimLearnedTraversalEdgesToLimit';Check (@($f.Body.Instructions|Where-Object{$_.Operand.Name -eq 'CanDiscardCapabilityTraversalEdge'}).Count -eq 1) 'capacity consumes common eligibility including fresh recorder lease'
$f=M 'PruneLearnedTraversalEdges';Check ((C $f 'CanDiscardCapabilityTraversalEdge').Count -eq 1) 'TTL cannot discard a live alias or recovery template'
$f=M 'CanDiscardCapabilityTraversalEdge';foreach($n in @('CanDiscard','IsTraversalEdgeActivelyReferenced','HasCapabilityFreshTraversalDefinition')){Check ((C $f $n).Count -eq 1) ('one shared deletion contract '+$n)}
$f=M 'HasCapabilityFreshTraversalDefinition';foreach($n in @('FreshDemonstration','IsLearningV3NativeProvenEdge')){Check ((C $f $n).Count -eq 1) ('fresh publication retains existing proof semantics '+$n)}
$f=M 'IsTraversalEdgeActivelyReferenced';Check ((C $f 'HasCapabilityRecordedEdgeReference').Count -eq 1) 'reference lookup includes authored aliases and selected templates'
$f=M 'HasCapabilityRecordedEdgeReference';Check (@($f.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'stfld'}).Count -eq 0) 'alias retention is read-only; creates no registry cursor or ownership clock'
$f=M 'UpdateSharedNativeEntryAcquisition';Check ((C $f 'EntryBudget').Count -eq 1) 'obstacle entry shares existing bounded distance budget'
$f=M 'TrySelectCapabilitySequenceStrategy';Check ((C $f 'RankCapabilityLearnedRecovery').Count -eq 1) 'recovery selector consumes current evidence ranking'
$f=M 'RankCapabilityLearnedRecovery';foreach($n in @('RecoveryRank','GetTraversalPhysicalSuccessCount','GetTraversalExperienceEffectiveScore')){Check ((C $f $n).Count -eq 1) ('existing ranking uses compatible current evidence '+$n)}
Check (@($f.Body.Instructions|Where-Object{$_.Operand.Name -eq 'LearningV322SkillEvidence'}).Count -eq 1) 'ranking reads current persistent skills rather than only retired experience'
foreach($n in @('SaveLearningV31NativeProvenStore','SaveLearningV322PersistentSkillEvidenceStore','ShouldSuppressLearningV322PersistentEvidence')){Check ((C $f $n).Count -eq 0) ('selection cannot write proof or silently veto self-rescue '+$n)}
$f=M 'RegisterTraversalEpisodeForEdge';Check (@($f.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldstr' -and $_.Operand -eq 'edgeMissingBeforeEpisodePublication'}).Count -eq 1) 'publication rejects orphan definition with explicit reason'
$f=M 'CreateFollowerRouteMissionStep';Check (@($f.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldstr' -and $_.Operand -eq 'recordedEpisodeDefinitionMissing'}).Count -eq 1) 'failed mission construction reports actual missing definition'
$f=M 'CanDiscardCapabilityTraversalEdge';Check ((C $f 'CanDiscard')[0].Offset -lt (C $f 'IsTraversalEdgeActivelyReferenced')[0].Offset) 'protected native and fresh definitions short-circuit before live mission graph lookup'
# RC156 actual 4.1.6 engine bindings and 1.1.1 original PIT integration.
$pitCompat=($m.GetTypes()|Where-Object Name -eq 'LegacyPITCompatibility');$f=$pitCompat.Methods|Where-Object Name -eq 'TryAttach';Check ((C $f 'HasStandalonePluginIdentity').Count -eq 1 -and (C $f 'HasStandalonePluginIdentity')[0].Offset -lt (C $f 'InstallOriginalPITCommandReader')[0].Offset) 'reviewed standalone PIT identity precedes any command or manual hook'
$f=($m.GetTypes()|Where-Object Name -eq 'FollowerCapabilityFollowGaitPatch').Methods|Where-Object Name -eq 'Apply';Check ((C $f 'ReviewedFollowBuild').Count -eq 1) 'ordinary follow handoff admits actual reviewed PIT1.1.1 MVID'
$identityReaders=@($pitCompat.NestedTypes|ForEach-Object Methods|Where-Object{$_.Name -like '*HasStandalonePluginIdentity*'});Check (@($identityReaders|Where-Object{(C $_ 'SupportedStandalone').Count -eq 1}).Count -eq 1) 'plugin attribute reader uses exact supported version policy'
$commandReaders=@($pitCompat.NestedTypes|ForEach-Object Methods|Where-Object{$_.Name -like '*InstallOriginalPITCommandReader*'});Check (@($commandReaders|Where-Object{(C $_ 'CommandClaim').Count -eq 2}).Count -eq 1) 'explicit and failed PIT command reads both use guarded ownership policy'
$f=($m.GetTypes()|Where-Object Name -eq 'FollowerTraversalTestBotMoverBodyRelinkPatch').Methods|Where-Object Name -eq 'GetTargetMethod';Check (@($f.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldstr' -and $_.Operand -eq 'CastFromPos'}).Count -eq 1) 'body projection barrier targets renamed native CastFromPos'
$f=($m.GetTypes()|Where-Object Name -eq 'FollowerTraversalTestMotionRelinkPatch').Methods|Where-Object Name -eq 'GetTargetMethod';Check (@($f.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldstr' -and $_.Operand -eq 'OnMotionApplied'}).Count -eq 1) 'motion integration barrier targets actual BotMoverImpostor callback'
$f=($m.GetTypes()|Where-Object Name -eq 'FollowerCapabilityFrontierPatch').Methods|Where-Object Name -eq 'Apply';Check (@($f.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldtoken' -and $_.Operand.FullName -eq 'EFT.ClientPlayerMovementContext'}).Count -eq 1) 'frontier includes renamed client movement context override'
$f=($m.GetTypes()|Where-Object Name -eq 'GenericSoftCollision').Methods|Where-Object Name -eq '.cctor';Check (@($f.Body.Instructions|Where-Object{$_.IsLdcI4() -and $_.GetLdcI4Value() -eq 52}).Count -ge 2) 'extrusion discovery covers actual public and historical private collider fields'
$f=($m.GetTypes()|Where-Object Name -eq 'FollowerControlBackend').Methods|Where-Object Name -eq 'SetGClass2679Func_0';Check (@($f.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'stfld' -and $_.Operand.Name -eq '_isAutoVaultingSettingEnable'}).Count -eq 1) 'existing reviewed vault gate writes renamed native auto-vault callback'
$dependency=$plugin.CustomAttributes|Where-Object{$_.AttributeType.FullName -eq 'BepInEx.BepInDependency' -and $_.ConstructorArguments[0].Value.ToString() -eq 'xyz.pit.fireteam'};Check ($null -ne $dependency) 'PIT initializes before optional compatibility hooks'
# RC157 posture contract: production authoring, persistence, native geometry and existing input owner.
$f=M 'CaptureBreadcrumb';Check ((C $f 'Capture').Count -eq 1) 'crouch demonstration is captured independently of NavMesh failure';Check ((C $f 'CompleteCapabilityPostureEpisode').Count -eq 1) 'first standing witness terminates finite crouch episode'
$f=M 'ExtendActiveWalkThroughEpisode';Check ((C $f 'Required').Count -eq 1) 'builder excludes prone and preserves crouch posture';Check ((C $f 'Same').Count -eq 1) 'builder cannot merge standing and crouch posture contracts'
$f=M 'FinalizeActiveWalkThroughEpisode';Check ((C $f 'Crouched').Count -ge 2) 'cheap NavMesh bypass and microterrain filters cannot erase authored crouch'
foreach($n in @('LearnedTraversalEdge','PersistentTraversalLink','PlayerTraversalMotionProfile','WalkThroughEpisodeBuilder')){
 $type=$r.NestedTypes|Where-Object Name -eq $n;$ctor=$type.Methods|Where-Object Name -eq '.ctor'
 Check (@($ctor.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'stfld' -and $_.Operand.Name -eq 'RequiredPose'}).Count -eq 1) ($n+' explicitly defaults old data to standing')
}
foreach($n in @('SaveLearningV31NativeProvenStore','SavePersistentTraversalLinks')){
 $writers=@($r.NestedTypes|ForEach-Object Methods|Where-Object{$_.Name -like ('*'+$n+'*') -and $_.HasBody})
 Check (@($writers|Where-Object{@($_.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'stfld' -and $_.Operand.Name -eq 'RequiredPose'}).Count -eq 1}).Count -eq 1) ($n+' serializes posture with existing proven identity')
}
foreach($n in @('LoadLearningV31NativeProvenStore','LoadPersistentTraversalLinks')){
 $f=M $n;Check (@($f.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'stfld' -and $_.Operand.Name -eq 'RequiredPose'}).Count -eq 1) ($n+' restores sanitized posture')
}
$f=M 'GetCapabilityWalkingPosture';Check ((C $f 'Resolve').Count -eq 1 -and (C $f 'GetCapabilityPostureEdge').Count -eq 1) 'one posture resolver uses existing active origin'
$f=M 'GetCapabilityPostureEdge';Check ((C $f 'IsProbeLifecycleCurrent').Count -eq 1 -and (C $f 'TryGetActiveFollowerRouteMission').Count -eq 1) 'retired probe and route cannot retain crouch ownership'
$f=M 'ApplyCollisionActivePhysicalRouteMotion';Check ((C $f 'GetCapabilityWalkingPosture')[0].Offset -lt (C $f 'BotOwnerSetPose')[0].Offset) 'contract resolves before existing pose writer';Check ((C $f 'BotOwnerSetPose')[0].Offset -lt (C $f 'TryApplyMeasuredGroundDelivery')[0].Offset) 'native path delivery cannot bypass pose application';Check ((C $f 'BotMoverSprint')[0].Offset -lt (C $f 'TryApplyMeasuredGroundDelivery')[0].Offset) 'sprint is cleared before native ground path takes ownership';Check ((C $f 'CommitCapabilityRouteRetryZeroInput').Count -eq 3) 'posture acquisition reuses bounded zero input rather than standing movement'
$f=M 'GetCapabilityCorridorCapsule';Check ((C $f 'CalculatePoseLevelValues').Count -eq 1) 'query capsule uses native EFT pose formula';Check ((C $f 'set_height').Count -eq 0 -and (C $f 'IgnoreCollision').Count -eq 0) 'posture changes query envelope without resizing body or disabling geometry'
$f=M 'TryValidateWalkThroughCorridor';Check ((C $f 'GetCapabilityCorridorCapsule').Count -eq 1) 'WT geometry validates the demonstrated posture'
$f=M 'UpdateWalkThroughProbe';Check ((C $f 'GetCapabilityProbePosture').Count -eq 2) 'live walking pose and sprint paths obey posture'
$f=M 'BeginWalkThroughProbe';Check ((C $f 'GetCapabilityProbePosture').Count -eq 1) 'WT begins with crouch rather than forced stand'
$f=M 'SanitizeTraversalPersistenceAfterLoad';Check ((C $f 'Same').Count -eq 1) 'sanitizer cannot merge different posture identities'
$f=M 'TryCaptureLearningV314ReverseKnownWalkThroughRouteFact';Check (@($f.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'stfld' -and $_.Operand.Name -eq 'RequiredPose'}).Count -eq 1) 'reverse WT retains posture requirement'
$proof=@($r.NestedTypes|ForEach-Object Methods|Where-Object{$_.Name -like '*GetCapabilityWalkingRecoveryProof*'});Check (@($proof|Where-Object{(C $_ 'Crouched').Count -eq 1}).Count -eq 1) 'player crouch forbids actuator rescue without inventing Native proof'
$identity=@($r.NestedTypes|ForEach-Object Methods|Where-Object{$_.Name -like '*RememberLearnedTraversalEdge*'});Check (@($identity|Where-Object{(C $_ 'Same').Count -eq 1}).Count -eq 2) 'fresh and proven matching keep posture identities distinct'
$f=M 'IsLearningV349CurrentRaidOrdinaryWalkThroughDisproof';Check ((C $f 'Crouched').Count -eq 1) 'NavMesh continuity cannot disprove overhead posture'

Check (@($proof|Where-Object{@($_.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldfld' -and $_.Operand.Name -eq 'EdgeId'}).Count -eq 2}).Count -eq 1) 'unproven crouch recovery proof belongs to exact original edge, not a nearby unproven template'
# RC158: the live wall limiter, local fallback and validator share the same native query shape.
foreach($n in @('TryConstrainCapabilityWalkThroughTarget','TryGetCapabilitySupportedLocalAim','TryValidatePostTraversalEgressCorridor')){
 $f=M $n;Check ((C $f 'GetCapabilityCorridorCapsule').Count -eq 1) ($n+' cannot retain a separate standing-only capsule')
}
$f=M 'GetCapabilityCorridorCapsule';Check ((C $f 'QueryCapsule').Count -eq 1) 'native envelope and contact skin use the one value policy'
$f=M 'CloneWalkThroughEpisode';Check ((C $f 'Normalize').Count -eq 1) 'snapshot preserves sanitized posture rather than constructor standing default'
$builder=$r.NestedTypes|Where-Object Name -eq 'WalkThroughEpisodeBuilder'
foreach($field in $builder.Fields){Check (@($f.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'stfld' -and $_.Operand.FullName -eq $field.FullName}).Count -eq 1) ('snapshot copies every builder field including future contracts '+$field.Name)}
$f=M 'MergeRejectedNaturalDropIntoWalkThrough';Check ((C $f 'MergeSnapshot').Count -eq 1) 'rejected airborne movement respects posture boundaries';Check ((C $f 'IsCapabilityWalkThroughSnapshotPublished').Count -eq 1) 'published episode cannot be resurrected by a stale snapshot'
$f=M 'CaptureBreadcrumb';Check ((C $f 'HoldDropBoundary').Count -eq 1) 'pending floor flicker keeps the existing episode until drop disposition'
$f=M 'FinalizeCapabilityWalkThroughSnapshot';Check ((C $f 'IsCapabilityWalkThroughSnapshotPublished').Count -eq 1) 'all terminal snapshot restores share publication guard';Check ((C $f 'CloneWalkThroughEpisode').Count -eq 1) 'terminal snapshot preserves contract';Check ((C $f 'FinalizeActiveWalkThroughEpisode').Count -eq 1) 'terminal snapshot uses existing publication lifecycle'
$f=M 'UpdateNaturalDropDownCandidate';Check ((C $f 'FinalizeCapabilityWalkThroughSnapshot').Count -eq 5) 'explicit action, timeout, known drop, new drop and rejected major drop all use guarded snapshot finalization'

@{passed=$true;checks=$checks;dllSha256=(Get-FileHash $Dll -Algorithm SHA256).Hash;physicsSimulated=$false}|ConvertTo-Json|Set-Content -Encoding UTF8 $Output
Write-Output "PASS $checks 1.0.1 RC158 actual DLL sequence checks"

} finally {$m.Dispose()}

