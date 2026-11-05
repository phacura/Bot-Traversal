param([string]$Dll,[string]$Output=(Join-Path $PSScriptRoot 'continuity-il-result.json'))
$ErrorActionPreference='Stop'
[Reflection.Assembly]::LoadFrom('D:/Games/SIT_DIR/mod or addon builds/dnSpy-net-win64/bin/dnlib.dll')|Out-Null
$m=[dnlib.DotNet.ModuleDefMD]::Load((Resolve-Path -LiteralPath $Dll).Path)
$script:checks=0;$script:errors=[Collections.Generic.List[string]]::new()
function Check($ok,$name){$script:checks++;if(!$ok){$script:errors.Add($name)}}
function T($name){$m.GetTypes()|Where-Object Name -eq $name|Select-Object -First 1}
function M($type,$name){$type.Methods|Where-Object Name -eq $name|Select-Object -First 1}
function Calls($method,$name){@($method.Body.Instructions|Where-Object{$_.OpCode.Name -in @('call','callvirt') -and $_.Operand.Name -eq $name})}
$r=T 'FollowerTraversalTestRuntime';$roll=M $r 'TryGetCapabilityRollingLandingHandoff'
foreach($name in @('IsProbeLifecycleCurrent','CanCommitScopedPhysicalMotion','TimeExpiredCapabilityPhase','TryPeekActiveCommand','IsLearningV350TerminalPhysicalRecoveryEligible','IsTeleportTransactionActive','TryGetImmediateSurvivalThreat','HasCombatMovementClaim','TryCaptureCapabilityLandingProofMotionFrontier','CanRollLandingProof')){Check ((Calls $roll $name).Count -ge 1) ('rolling authority/frozen-geometry/safety '+$name)}
foreach($field in @('FrozenTraversalCommitment','LifecycleMission','NativeDropDownSawAirborne','NativeDropDownSawDescent','NativeDropDownSawFarSide')){Check (@($roll.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldfld' -and $_.Operand.Name -eq $field}).Count -ge 1) ('rolling observation '+$field)}
$frozen=@($roll.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldfld' -and $_.Operand.Name -eq 'FrozenTraversalCommitment'});$sameFrozen=$false
if($frozen.Count -eq 1){$at=$roll.Body.Instructions.IndexOf($frozen[0]);$sameFrozen=$roll.Body.Instructions[$at+1].OpCode.Name -eq 'ldarg.1' -and $roll.Body.Instructions[$at+2].OpCode.Name -in @('bne.un','bne.un.s')}
Check $sameFrozen 'frozen commitment identity is an actual inequality exit branch'
Check (@($roll.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldfld' -and $_.Operand.Name -eq 'IgnoreDeltaMovement'}).Count -eq 1) 'actual motion override field denies rolling'
$drop=M $r 'UpdateNativeDropDown';Check ((Calls $drop 'TryGetCapabilityRollingLandingHandoff').Count -eq 3) 'same runway decision at update, first contact and final brake dispatch'
$gait=M $r 'ShouldPreserveCapabilityProbeGait';Check ((Calls $gait 'LandingProofKeepsGait').Count -eq 1) 'actual gait host uses qualified landing decision'
foreach($field in @('JumpAttempted','LearningV330NativeJumpQualifiedLandingWitness','NativeLandingRejected','NativePartialCrossingObserved')){Check (@($gait.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldfld' -and $_.Operand.Name -eq $field}).Count -ge 1) ('actual Jump gait proof '+$field)}
$front=M $r 'TryCaptureCapabilityLandingProofMotionFrontier';Check ((Calls $front 'RollingLandingBudget').Count -eq 1) 'native motion frontier consumes remaining proof budget'
foreach($field in @('NativeDropDownLandingStableSince','NativeDropDownAttempted')){Check (@($front.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldfld' -and $_.Operand.Name -eq $field}).Count -ge 1) ('frontier existing proof clock '+$field)}
$port=$r.NestedTypes|Where-Object Name -eq 'CapabilityFrontierPort';Check ((Calls (M $port 'TryObserve') 'TryCaptureCapabilityLandingProofMotionFrontier').Count -eq 1) 'native frontier port uses same source'
$policy=T 'FollowerCapabilityHandoffPolicy';foreach($name in @('RollingLandingBudget','CanRollLandingProof')){Check ($null -ne (M $policy $name)) ('actual compiled policy '+$name)}
# The new rolling decision must have no route cursor/completion or engine-control writer.
foreach($name in @('TryAdvanceRouteMission','PublishProbeLifecycleCompletion','BotOwnerStopMove','BotMoverStop','TryCommitNativeTransitionVelocity','CommitCapabilityNativePlayerInput','Play','ProcessStateEnter')){Check ((Calls $roll $name).Count -eq 0) ('rolling qualification cannot write '+$name)}
$all=@($m.GetTypes()|ForEach-Object{$_.Methods}|Where-Object HasBody)
Check (@($all|ForEach-Object{Calls $_ 'TryCommitNativePlayerMove'}).Count -eq 1) 'one native Player.Move gateway retained'
Check (@($all|ForEach-Object{Calls $_ 'CommitCapabilityNativePlayerInput'}).Count -eq 3) 'two existing input producers plus scoped zero-only failed-route cleanup'
$alg=[Security.Cryptography.SHA256]::Create();try{$hash=[BitConverter]::ToString($alg.ComputeHash([IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $Dll).Path))).Replace('-','')}finally{$alg.Dispose()}
$result=@{dllSha256=$hash;passed=($script:errors.Count -eq 0);checks=$script:checks;errors=@($script:errors);physicsSimulated=$false}
$result|ConvertTo-Json -Depth 5|Set-Content -Encoding UTF8 -LiteralPath $Output
Write-Output "RC111 Continuity IL: checks=$script:checks failures=$($script:errors.Count)"
$m.Dispose();if($script:errors.Count){$script:errors;exit 1}
