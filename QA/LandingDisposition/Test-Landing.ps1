param([string]$Dll,[string]$Output)
$ErrorActionPreference='Stop'
[Reflection.Assembly]::LoadFrom('D:/Games/SIT_DIR/mod or addon builds/dnSpy-net-win64/bin/dnlib.dll')|Out-Null
$m=[dnlib.DotNet.ModuleDefMD]::Load((Resolve-Path $Dll).Path);$r=$m.GetTypes()|Where-Object Name -eq 'FollowerTraversalTestRuntime'
function M($n){$r.Methods|Where-Object Name -eq $n|Select-Object -First 1}
function C($f,$n){@($f.Body.Instructions|Where-Object{$_.OpCode.Name -in @('call','callvirt') -and $_.Operand.Name -eq $n})}
$checks=0
function Check($v,$n){$script:checks++;if(!$v){throw $n}}
$f=M 'TryRecoverPlayerValidatedTraversal';$failure=@(C $f 'TryRequestCapabilityFullRouteRetry')[-1]
Check ((C $f 'FarSideRecoveryAccepted')[0].Offset -lt $failure.Offset) 'real far-side completion before failure transaction'
Check ((C $f 'TryProveLearningV441JumpTypedSuccessorFrontier')[0].Offset -lt $failure.Offset) 'typed successor before failure transaction'
Check ((C (M 'TickCapabilityFailureSequence') 'TryStartProbeForBot').Count -eq 1) 'selected strategy dispatches its own exact probe'
$q=M 'TickCapabilityPortalWait';Check ((C $q 'IsFollowerRouteMissionStepPhysicallyPassed')[0].Offset -lt (C $q 'Admit')[0].Offset) 'completed passage resolves before old portal queue'
$soft=$m.GetTypes()|Where-Object Name -eq 'GenericSoftCollision';$fixed=$soft.Methods|Where-Object Name -eq 'FixedUpdate'
Check ((C $fixed 'TryICharacterControllerMove').Count -eq 1) 'stationary local-player push restored'
$friendly=$m.GetTypes()|Where-Object Name -eq 'FollowerFriendlyAvoidancePatch';Check ((C ($friendly.Methods|Where-Object Name -eq 'SkipFriendlyPair') 'Owns').Count -eq 2) 'friendly mover repulsion remains suppressed'
@{passed=$true;checks=$checks;dllSha256=(Get-FileHash $Dll -Algorithm SHA256).Hash;physicsSimulated=$false}|ConvertTo-Json|Set-Content -Encoding UTF8 $Output
$m.Dispose();Write-Output "PASS $checks RC122 actual DLL landing/dispatch/queue/player-push checks"
