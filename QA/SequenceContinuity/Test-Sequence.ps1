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
Check (@($ins|Where-Object{$_.Operand.Name -eq 'GetFlatDirection'}).Count -ge 1 -and @($ins|Where-Object{$_.Operand.Name -eq 'get_Position'}).Count -ge 2) 'entry correction steers from body toward actual mover target'
$q=M 'TickCapabilityPortalWait';Check ((C $q 'IsFollowerRouteMissionStepPhysicallyPassed')[0].Offset -lt (C $q 'Admit')[0].Offset) 'completed passage resolves before old portal queue'
$soft=$m.GetTypes()|Where-Object Name -eq 'GenericSoftCollision';$fixed=$soft.Methods|Where-Object Name -eq 'FixedUpdate'
Check ((C $fixed 'TryICharacterControllerMove').Count -eq 1) 'stationary local-player push retained'
$friendly=$m.GetTypes()|Where-Object Name -eq 'FollowerFriendlyAvoidancePatch';Check ((C ($friendly.Methods|Where-Object Name -eq 'SkipFriendlyPair') 'Owns').Count -eq 2) 'friendly mover repulsion still suppressed'
@{passed=$true;checks=$checks;dllSha256=(Get-FileHash $Dll -Algorithm SHA256).Hash;physicsSimulated=$false}|ConvertTo-Json|Set-Content -Encoding UTF8 $Output
$m.Dispose();Write-Output "PASS $checks RC123 actual DLL sequence checks"
