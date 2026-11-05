param([string]$Dll,[string]$Output)
$ErrorActionPreference='Stop'
[Reflection.Assembly]::LoadFrom('D:/Games/SIT_DIR/mod or addon builds/dnSpy-net-win64/bin/dnlib.dll')|Out-Null
$m=[dnlib.DotNet.ModuleDefMD]::Load((Resolve-Path $Dll).Path);$r=$m.GetTypes()|Where-Object Name -eq 'FollowerTraversalTestRuntime'
function M($n){$r.Methods|Where-Object Name -eq $n|Select-Object -First 1}
function C($f,$n){@($f.Body.Instructions|Where-Object{$_.OpCode.Name -in @('call','callvirt') -and $_.Operand.Name -eq $n})}
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
@{passed=$true;checks=$checks;dllSha256=(Get-FileHash $Dll -Algorithm SHA256).Hash;physicsSimulated=$false}|ConvertTo-Json|Set-Content -Encoding UTF8 $Output
$m.Dispose();Write-Output "PASS $checks RC125 actual DLL sequence checks"
