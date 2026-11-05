param([string]$Dll,[string]$Output=(Join-Path $PSScriptRoot 'passage-admission-il.json'))
$ErrorActionPreference='Stop'
[Reflection.Assembly]::LoadFrom('D:/Games/SIT_DIR/mod or addon builds/dnSpy-net-win64/bin/dnlib.dll')|Out-Null
$m=[dnlib.DotNet.ModuleDefMD]::Load((Resolve-Path -LiteralPath $Dll).Path);$r=$m.GetTypes()|Where-Object FullName -eq 'BotTraversal.Core.FollowerTraversalTestRuntime'
$script:n=0;$script:errors=[Collections.Generic.List[string]]::new()
function Check($v,$s){$script:n++;if(!$v){$script:errors.Add($s)}}
function Method($s){$r.Methods|Where-Object Name -eq $s|Select-Object -First 1}
function Calls($f,$s){if(!$f -or !$f.HasBody){return @()};@($f.Body.Instructions|Where-Object{$_.OpCode.Name -in @('call','callvirt') -and $_.Operand.Name -eq $s})}
function Before($f,$a,$b){$x=@(Calls $f $a);$y=@(Calls $f $b);$x.Count -gt 0 -and $y.Count -gt 0 -and $x[0].Offset -lt $y[0].Offset}
$lane=Method 'TryGetLearningV363ActuatorRouteEntryLaneTarget'
Check ((Calls $lane 'VaultSideStep').Count -eq 1) 'Vault lane corrects actual lateral error instead of radial-anchor facing'
Check (@($lane.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldstr' -and $_.Operand -eq ':rc117NativeVaultAxis'}).Count -eq 1) 'Vault lane uses exact native entry axis'
$motion=Method 'ApplyCollisionActivePhysicalRouteMotion';$facing=Method 'TryGetCapabilityVaultIngressFacing'
Check ((Calls $motion 'TryGetCapabilityVaultIngressFacing').Count -eq 1) 'sole motion gateway applies independent Vault facing'
foreach($name in @('TryValidateV2CurrentTypedStep','IsCapabilityRouteRetrySafe','GetFlatDirection')){Check ((Calls $facing $name).Count -ge 1) ('Vault facing retains '+$name)}
Check ((Calls $facing 'MovementContextTryJump').Count -eq 0) 'Vault facing cannot execute unrelated Jump'
$trial=Method 'IsCapabilityColdRunJumpTrialReady'
foreach($name in @('ColdRunTrial','TryValidateV2CurrentTypedStep','TryValidateV2AdjacentPhysicalContinuationBoundary','TryGetLearningV460JumpAdmissionGeometry','IsCapabilityRouteRetrySafe','LastOrDefault')){Check ((Calls $trial $name).Count -ge 1) ('cold trial current physical identity '+$name)}
Check (@($trial.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldfld' -and $_.Operand.Name -eq 'LearningV442HasCurrentEpisodeJumpTakeoffFrontier'}).Count -eq 0) 'new Jump trial does not require an already-native-proven frontier flag'
foreach($name in @('TryGetLearningV353JumpRouteFunnelTarget','IsLearningV329JumpTakeoffMomentumReady','IsLearningV411JumpTakeoffModeReady','ShouldLearningV411RequestJumpSprint')){Check ((Calls (Method $name) 'IsCapabilityColdRunJumpTrialReady').Count -eq 1) ('all Run preparation/consumer gates share cold trial '+$name)}
$consume=Method 'TryConsumeCapabilityColdRunJumpTrial';$trigger=Method 'TriggerNativeJump'
Check ((Calls $consume 'IsCapabilityColdRunJumpTrialReady').Count -eq 1) 'trial consumption rechecks live geometry'
Check (@($consume.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'stfld' -and $_.Operand.Name -eq 'CapabilityColdRunTrialConsumed'}).Count -eq 1) 'one attempt consumes route-step token'
Check (Before $trigger 'IsCapabilityJumpTriggerReady' 'TryConsumeCapabilityColdRunJumpTrial') 'final native guard precedes cold token consumption'
Check (Before $trigger 'TryConsumeCapabilityColdRunJumpTrial' 'MovementContextTryJump') 'token consumed only at actual native invocation'
$horizon=Method 'IsLearningV449JumpInsidePhysicalTriggerHorizon';$contact=Method 'TryObserveCapabilityJumpContactWindow'
Check ($horizon.MethodSig.Params.Count -eq 3 -and $horizon.MethodSig.Params[2].FullName -eq 'EFT.BotOwner') 'shared physical horizon accepts real actor for contact'
Check ((Calls $horizon 'TryObserveCapabilityJumpContactWindow').Count -eq 1) 'all physical consumers share collision takeoff horizon'
foreach($name in @('TryGetFirstNonSelfCapsuleCastHit','JumpContact','TryGetLearningV460JumpAdmissionGeometry','TryGetLearnedCommitmentForProbe','IsCapabilityRouteRetrySafe')){Check ((Calls $contact $name).Count -eq 1) ('contact dispatch requires '+$name)}
$all=@($m.GetTypes()|ForEach-Object{$_.Methods}|Where-Object HasBody);$horizonCalls=0;$horizonConsumers=@()
foreach($method in $all){foreach($call in (Calls $method 'IsLearningV449JumpInsidePhysicalTriggerHorizon')){
 $at=$method.Body.Instructions.IndexOf($call);$horizonCalls++;$horizonConsumers+=([string]$method.Name);Check ($method.Body.Instructions[$at-1].OpCode.Name -ne 'ldnull') ('actual contact actor passed by '+$method.Name)
}}
foreach($consumerName in @('ShouldDeferCapabilityJumpProbe','TryConsumeLearningV429AtomicJumpAdmission','IsCf60LegalOneFrameJumpTriggerForecast','TryHandoffLearningV388PhysicalCorridorToReadySuccessor','TryTriggerLearningV354RollingJumpActuatorLead','UpdateProbeJumpPriming','TryBeginLearnedNativeJump','UpdateLearnedJumpTakeoffPriming')){Check ($horizonConsumers -contains $consumerName) ('shared contact horizon consumer '+$consumerName)}
$stall=Method 'TryRecoverCapabilityStalledJumpPreparation'
foreach($name in @('TryCaptureCapabilityJumpPreparation','HorizontalProgress','StalledJump','TryRequestCapabilityFullRouteRetry')){Check ((Calls $stall $name).Count -eq 1) ('measured preparation failure requires '+$name)}
Check (Before $stall 'HorizontalProgress' 'TryRequestCapabilityFullRouteRetry') 'actual forward progress renews clock before failure classification'
Check (Before $stall 'StalledJump' 'TryRequestCapabilityFullRouteRetry') 'normal entry geometry alone cannot request retry'
Check ((Calls (Method 'TickCapabilityPreparationConsumerFrames') 'TryRecoverCapabilityStalledJumpPreparation').Count -eq 1) 'existing frame consumer observes actual stalled attempts'
$priming=Method 'UpdateProbeJumpPriming'
Check ((Calls $priming 'IsLearningV450LiveEftJumpAttemptLegal').Count -eq 1 -and (Calls $priming 'TriggerNativeJump').Count -eq 1) 'legacy counter still requires shared live physical contract'
Check ((Calls $priming 'MovementContextTryJump').Count -eq 0) 'legacy priming cannot bypass final native guard'
$h=[Security.Cryptography.SHA256]::Create();try{$sha=[BitConverter]::ToString($h.ComputeHash([IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $Dll).Path))).Replace('-','')}finally{$h.Dispose()}
@{passed=($script:errors.Count -eq 0);checks=$script:n;errors=@($script:errors);dllSha256=$sha;physicsSimulated=$false}|ConvertTo-Json -Depth 5|Set-Content -Encoding UTF8 -LiteralPath $Output
Write-Output "RC117 passage admission boundary: checks=$script:n failures=$($script:errors.Count)";$m.Dispose();if($script:errors.Count){$script:errors;exit 1}
