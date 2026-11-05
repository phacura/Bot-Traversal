param([string]$Dll,[string]$Output)
$ErrorActionPreference='Stop'
[Reflection.Assembly]::LoadFrom('D:/Games/SIT_DIR/mod or addon builds/dnSpy-net-win64/bin/dnlib.dll')|Out-Null
$cases=@(
 @('TryRecoverPlayerValidatedTraversal','TryBeginNativeTraversalRunOut'),
 @('TryRecoverPlayerValidatedTraversal','TryBeginLearningV408JumpPartialCrossingForwardContinuation'),
 @('TryBeginAssistedTraversalReplay','TryDispatchCapabilitySequenceNative'),
 @('TickCapabilityFailureSequence','TickCapabilityFailureSequence'),
 @('TryValidateV2CurrentTypedStep','IsCapabilityRecoveryPending'),
 @('TryReadV2LiveRouteAdmission','IsCapabilityRecoveryPending'),
 @('TryStartProbeForBot','IsCapabilityRecoveryPending'),
 @('TickCapabilityFailureSequence','TryEnterBoundPhysicalMotion'),
 @('DriveLearningV32NativeEntryAcquisition','GetFlatDirection'),
 @('TryRecoverPlayerValidatedTraversal','ContinueExecutedJump'),
 @('TryStartProbeForBot','TryAdmitCapabilitySequenceProbe'),
 @('TryGetLearnedTraversalFollowTarget','HasCapabilityFailureStrategy'),
 @('UpdateLearningV375PersistentExecutablePhysicalRouteTransaction','HasCapabilityFailureStrategy'),
 @('TryGetLearningV353JumpRouteFunnelTarget','HasCapabilityFailureStrategy'),
 @('TryReadV2LiveRouteAdmission','IsCapabilityStrategyPending'),
 @('TryAdmitCapabilitySequenceProbe','CanAllocateStrategy'),
 @('HasCapabilityFailureStrategy','OwnsStrategy'),
 @('EscalateTraversalFailureMonotonic','TryRequestCapabilityFullRouteRetry'),
 @('TryTeleportPlayerValidatedTraversalAtExit','TryRequestCapabilityFullRouteRetry'),
 @('TryBeginPlayerRouteContinuationBridge','TryRequestCapabilityFullRouteRetry'),
 @('UpdateUnifiedTraversalVerify','TryRetainCapabilityObservedJumpBeforePassageBridge'),
 @('TryRetainCapabilityObservedJumpBeforePassageBridge','CanClosePassageBoundary'),
 @('TryRetainCapabilityObservedJumpBeforePassageBridge','TryFinalizeTraversalChainIntermediate'),
 @('TrySelectCapabilitySequenceStrategy','PastObstacleEntry'),
 @('UpdateSharedNativeEntryAcquisition','EntryBudget'),
 @('TryRequestCapabilityFullRouteRetry','IsCancellation'),
 @('TryRetainCapabilityObservedJumpBeforePassageBridge','TryLatchLearningV372WalkThroughPhysicalPassageProof'),
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
 @('TickCapabilityFailureSequence','IsCapabilityPortalWaiting'),
 @('TryAdvanceRouteMission','Mark'),
 @('RegisterCapabilityLocalMission','IsCapabilityDebtResolved'),
 @('TryPrepareCapabilityAutomaticProbeMission','IsCapabilityDebtResolved'),
 @('IsTraversalEpisodeAlreadyConsumed','IsCapabilityDebtResolved'),
 @('IsLearningV366RuntimeEpisodeBehindExecutionFrontier','IsCapabilityDebtResolved'),
 @('TryGetLearnedTraversalFollowTarget','PruneCapabilityResolvedCommitment'),
 @('TickCapabilityPortalWait','Owns'),
 @('TickCapabilityPortalWait','InWindow'),
 @('TickCapabilityPortalWait','IsProbeLifecycleCurrent'),
 @('TryRejectOriginalPITFollowWrite','CanCommitScopedPhysicalMotion','LegacyOriginalPITFollowBridge'),
 @('TryRejectOriginalPITFollowWrite','BlockOriginalFollowDestination','LegacyOriginalPITFollowBridge'),
 @('TryRejectOriginalPITFollowWrite','OwnsCapabilityLocalMotion','LegacyOriginalPITFollowBridge'),
 @('TryRejectOriginalPITFollowWrite','IsOriginalPITFollowCall','LegacyOriginalPITFollowBridge'),
 @('PatchBotMoverDestination','TryRejectOriginalPITFollowWrite','FollowerMovementWriterAuthorityPatch'),
 @('PatchPointDataDestination','TryRejectOriginalPITFollowWrite','FollowerMovementWriterAuthorityPatch'))
$d=Join-Path $PSScriptRoot 'Mutants';New-Item -ItemType Directory -Force $d|Out-Null;$results=@()
foreach($case in $cases){
 $m=[dnlib.DotNet.ModuleDefMD]::Load((Resolve-Path $Dll).Path)
 $typeName=if($case.Count -gt 2){$case[2]}else{'FollowerTraversalTestRuntime'};$r=$m.GetTypes()|Where-Object Name -eq $typeName;$f=$r.Methods|Where-Object Name -eq $case[0]|Select-Object -First 1
 $i=$f.Body.Instructions|Where-Object{$_.OpCode.Name -in @('call','callvirt') -and $_.Operand.Name -eq $case[1]}
 if(!$i){throw "mutation target missing $case"};foreach($instruction in $i){$instruction.OpCode=[dnlib.DotNet.Emit.OpCodes]::Nop;$instruction.Operand=$null};$f.Body.KeepOldMaxStack=$true
 $file=Join-Path $d ($case[0]+'-'+$case[1]+'.dll');$m.Write($file);$m.Dispose()
 $result=Join-Path $d 'result.json';$ErrorActionPreference='Continue';powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Test-Terminal.ps1') -Dll $file -Output $result *> (Join-Path $d ($case[1]+'.log'))
 $faultExit=$LASTEXITCODE;$ErrorActionPreference='Stop'
 if($faultExit -eq 0){throw "undetected IL fault $case"};$results+=@{method=$case[0];removedCall=$case[1];detected=$true}
}
@{passed=$true;checks=$results.Count;faults=$results;gameExecuted=$false;kind='structural IL omission detection; mutated DLLs never executed'}|ConvertTo-Json -Depth 5|Set-Content -Encoding UTF8 $Output
Write-Output "PASS $($results.Count) IL omission faults detected"
