param([string]$Dll,[string]$Output=(Join-Path $PSScriptRoot 'route-recovery-il.json'))
$ErrorActionPreference='Stop'
[Reflection.Assembly]::LoadFrom('D:/Games/SIT_DIR/mod or addon builds/dnSpy-net-win64/bin/dnlib.dll')|Out-Null
$m=[dnlib.DotNet.ModuleDefMD]::Load((Resolve-Path -LiteralPath $Dll).Path);$r=$m.GetTypes()|Where-Object FullName -eq 'BotTraversal.Core.FollowerTraversalTestRuntime'
$script:n=0;$script:errors=[Collections.Generic.List[string]]::new()
function Check($v,$s){$script:n++;if(!$v){$script:errors.Add($s)}}
function Method($s){$r.Methods|Where-Object Name -eq $s|Select-Object -First 1}
function Calls($f,$s){if(!$f){return @()};@($f.Body.Instructions|Where-Object{$_.OpCode.Name -in @('call','callvirt','newobj') -and $_.Operand.Name -eq $s})}
function Before($f,$a,$b){$x=@(Calls $f $a);$y=@(Calls $f $b);$x.Count -gt 0 -and $y.Count -gt 0 -and $x[0].Offset -lt $y[0].Offset}
$miss=Method 'TryBeginLearningV450MissedJumpCurrentBodySelfRescue'
Check (Before (Method 'FinalizeOrEscalateTraversalFailure') 'TryRequestCapabilityFullRouteRetry' 'EscalateTraversalFailureMonotonic') 'terminal failure offers full root before legacy Assist/Teleport can supersede its identity'
$terminal=Method 'FinalizeOrEscalateTraversalFailure';$terminalCall=@(Calls $terminal 'TryRequestCapabilityFullRouteRetry');$terminalFlag=$false
if($terminalCall.Count -eq 1){$at=$terminal.Body.Instructions.IndexOf($terminalCall[0]);$flag=$terminal.Body.Instructions[$at-1];$terminalFlag=$flag.IsLdcI4() -and $flag.GetLdcI4Value() -eq 1}
Check $terminalFlag 'actual terminal hook carries explicit execution failure true'
Check ((Calls $miss 'TryRequestCapabilityFullRouteRetry').Count -eq 1) 'missed no-probe Jump actually requests root recovery'
Check (Before $miss 'TryRequestCapabilityFullRouteRetry' 'RetireLearningV446PhysicalStrategy') 'root recovery offered before deleting physical strategy'
$correction=Method 'TryBeginLearningV3NativeEntryCorrection'
Check (Before $correction 'TryRequestCapabilityFullRouteRetry' 'BotMoverStop') 'failed bound convergence returns to root before local correction'
$borrow=Method 'TryConsumeLearningV369DownstreamActuatorPhysicalFrontier'
Check (Before $borrow 'BorrowHeading' 'MarkLearningV32NativeEntryAcquired') 'physical chronology cannot borrow a sideways native actuator'
$dispatch=Method 'ShouldDeferCapabilityJumpProbe'
Check (Before $dispatch 'UnsafeJumpSpeed' 'TryRequestCapabilityFullRouteRetry') 'outlier dispatch is a classified terminal before recovery'
Check ((Calls $dispatch 'MovementContextTryJump').Count -eq 0) 'unsafe dispatch does not call TryJump itself'
$commit=Method 'TryCommitV2ProbePhysicalMotion';Check ($commit.MethodSig.Params[$commit.MethodSig.Params.Count-1].FullName -eq 'System.Boolean') 'probe input has explicit local world frame opt-in'
$publish=Method 'TryPublishLearningV414PathlessNativeInputLease';Check ($publish.MethodSig.Params[$publish.MethodSig.Params.Count-1].FullName -eq 'System.Boolean') 'precision lease carries explicit opt-in'
Check ((Calls $publish 'TryCommitV2ProbePhysicalMotion').Count -eq 1) 'lease uses sole existing probe motion gateway'
$rolling=Method 'UpdateRollingTraversalApproach'
Check ((Calls $rolling 'TryRetireNativeMoverPathPreserveLocomotion').Count -ge 1) 'precision staging retires competing path instead of requiring it to vanish first'
$optIn=$false;foreach($call in (Calls $rolling 'TryPublishLearningV414PathlessNativeInputLease')){$at=$rolling.Body.Instructions.IndexOf($call);if($rolling.Body.Instructions[$at-1].IsLdcI4() -and $rolling.Body.Instructions[$at-1].GetLdcI4Value() -eq 1){$optIn=$true}}
Check $optIn 'actual precision call explicitly enables its own world input frame'
$tick=Method 'TickCapabilityFullRouteRetry'
foreach($name in @('TryDriveCapabilityReturnCorridor','TryBuildCapabilityReturnCorridor','ReturnDecision','StagePosition','StageReached','TryObserveCapabilityReturnRemaining')){Check ((Calls $tick $name).Count -ge 1) ('return dispatcher integration '+$name)}
$drive=Method 'TryDriveCapabilityReturnCorridor';$build=Method 'TryBuildCapabilityReturnCorridor'
foreach($name in @('ReturnLocal','PathProgress','ReturnSpeed','ApplyCollisionActivePhysicalRouteMotion','HoldCapabilityRouteRetry')){Check ((Calls $drive $name).Count -ge 1) ('same recovery consumer uses '+$name)}
foreach($name in @('ReturnSegment','PathWithinBudget','HoldCapabilityRouteRetry')){Check ((Calls $build $name).Count -ge 1) ('recorded return prefix checks '+$name)}
Check (@($build.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldstr' -and $_.Operand -eq 'WalkThrough'}).Count -ge 1) 'reverse return requires WT root'
Check ((Calls $build 'Any').Count -eq 1 -and (Calls $build 'TakeWhile').Count -eq 1) 'all steps before failed actuator checked for reverse eligibility'
foreach($name in @('TryPlayerTeleport','PlayerMove','SetMovementContextLastBlendMotionDelta','SetPlayerPosition','BotOwnerGoToPoint')){Check ((Calls $drive $name).Count -eq 0) ('local return adds no private movement backend '+$name)}
Check ((Calls (Method 'ForceReleaseTraversalProbe') 'TryRequestCapabilityFullRouteRetry').Count -eq 0) 'cleanup remains isolated'
Check (@($tick.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldstr' -and $_.Operand -eq 'capabilityFullRouteRetryExhausted'}).Count -eq 1) 'bounded replay exhaustion diagnostic exists'
$h=[Security.Cryptography.SHA256]::Create();try{$sha=[BitConverter]::ToString($h.ComputeHash([IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $Dll).Path))).Replace('-','')}finally{$h.Dispose()}
@{passed=($script:errors.Count -eq 0);checks=$script:n;errors=@($script:errors);dllSha256=$sha;physicsSimulated=$false}|ConvertTo-Json -Depth 5|Set-Content -Encoding UTF8 -LiteralPath $Output
Write-Output "RC115 route recovery boundary: checks=$script:n failures=$($script:errors.Count)";$m.Dispose();if($script:errors.Count){$script:errors;exit 1}
