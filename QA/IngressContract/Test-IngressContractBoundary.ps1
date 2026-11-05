param([string]$Dll,[string]$Output=(Join-Path $PSScriptRoot 'ingress-contract-il.json'))
$ErrorActionPreference='Stop'
[Reflection.Assembly]::LoadFrom('D:/Games/SIT_DIR/mod or addon builds/dnSpy-net-win64/bin/dnlib.dll')|Out-Null
$m=[dnlib.DotNet.ModuleDefMD]::Load((Resolve-Path -LiteralPath $Dll).Path);$r=$m.GetTypes()|Where-Object FullName -eq 'BotTraversal.Core.FollowerTraversalTestRuntime'
$script:n=0;$script:errors=[Collections.Generic.List[string]]::new()
function Check($v,$s){$script:n++;if(!$v){$script:errors.Add($s)}}
function Method($s){$r.Methods|Where-Object Name -eq $s|Select-Object -First 1}
function Calls($f,$s){if(!$f -or !$f.HasBody){return @()};@($f.Body.Instructions|Where-Object{$_.OpCode.Name -in @('call','callvirt') -and $_.Operand.Name -eq $s})}
function Before($f,$a,$b){$x=@(Calls $f $a);$y=@(Calls $f $b);$x.Count -gt 0 -and $y.Count -gt 0 -and $x[0].Offset -lt $y[0].Offset}
$lane=Method 'TryGetLearningV363ActuatorRouteEntryLaneTarget'
Check ((Calls $lane 'VaultLead').Count -eq 1 -and (Calls $lane 'ForwardSample').Count -eq 1) 'Vault forward lane and sample rejection integrated'
$trigger=Method 'TriggerNativeJump';$guard=@(Calls $trigger 'IsCapabilityJumpTriggerReady')
Check (Before $trigger 'IsCapabilityJumpTriggerReady' 'MovementContextTryJump') 'all trigger paths share final live guard'
$positiveBranch=$false
if($guard.Count -eq 1){$at=$trigger.Body.Instructions.IndexOf($guard[0]);$positiveBranch=$trigger.Body.Instructions[$at+1].OpCode.Name -in @('brtrue','brtrue.s')}
Check $positiveBranch 'final consumer true branch alone reaches native Jump'
Check ((Calls $trigger 'TryRequestCapabilityFullRouteRetry').Count -eq 1) 'failed final consumer retains full root debt'
$ready=Method 'IsCapabilityJumpTriggerReady'
Check (@($ready.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldstr' -and $_.Operand -eq 'Jump'}).Count -eq 1 -and (Calls $ready 'Equals').Count -ge 1) 'final Jump capability guard is semantically isolated from Vault or Climb sibling fallback'
foreach($name in @('IsProbeLifecycleCurrent','IsLearningV411JumpTakeoffModeReady','IsLearningV329JumpTakeoffMomentumReady','UnsafeJumpSpeed','SprintFloor')){Check ((Calls $ready $name).Count -eq 1) ('final consumer requires '+$name)}
foreach($consumerName in @('TryContinueLearningV365ContinuousTypedPhysicalRoute','UpdateWalkThroughProbe')){Check ((Calls (Method $consumerName) 'TryPrepareCapabilityAdjacentJumpRunup').Count -eq 1) ('predecessor current consumer prepares Jump '+$consumerName)}
$prep=Method 'TryPrepareCapabilityAdjacentJumpRunup'
foreach($name in @('TryValidateV2CurrentTypedStep','TryValidateV2AdjacentPhysicalContinuationBoundary','TryGetLearningV460JumpAdmissionGeometry','IsCapabilityRouteRetrySafe')){Check ((Calls $prep $name).Count -ge 1) ('adjacent preparation identity and safety '+$name)}
Check (Before $prep 'TryEnterBoundPhysicalMotion' 'BotMoverSprint') 'adjacent preparation acquires its own exact issued scope for active WT too'
Check ((Calls $prep 'MovementContextTryJump').Count -eq 0 -and (Calls $prep 'SetMovementContextSprintSpeed').Count -eq 0) 'preparation does not trigger Jump or assign world speed'
$generic=Method 'TryContinueLearningV365ContinuousGenericPhysicalRoute';$step=Method 'TryDriveCapabilityGenericLowStep'
Check ((Calls $generic 'TryDriveCapabilityGenericLowStep').Count -eq 1) 'generic passage actually invokes low-step contract'
Check ((Calls $generic 'HorizontalProgress').Count -eq 1) 'generic progress uses horizontal forward witness'
foreach($consumerName in @('TryUpdateWalkThroughStepAssist','TryDriveCapabilityGenericLowStep')){Check ((Calls (Method $consumerName) 'TryObserveCapabilityLowStep').Count -eq 1) ('WT and generic share capsule contract '+$consumerName)}
foreach($name in @('StepSupport','TryGetFirstNonSelfRaycastHit','LowStep','TryMovementContextApplyApproachMotion')){Check ((Calls $step $name).Count -eq 1) ('bounded generic step requires '+$name)}
Check (Before $step 'TryEnterBoundPhysicalMotion' 'TryMovementContextApplyApproachMotion') 'generic step rebinds exact live scope before collision motion'
Check (@($step.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldstr' -and $_.Operand -eq 'R116-116A00000006'}).Count -eq 1) 'generic collision step uses reviewed exact control site'
foreach($name in @('TryPlayerTeleport','TryICharacterControllerMove','TriggerNativeJump','PublishCapabilityCompletion','BotOwnerGoToPoint')){Check ((Calls $step $name).Count -eq 0) ('low step cannot mint unrelated action '+$name)}
$tick=Method 'TickCapabilityFullRouteRetry';$walkStores=@($tick.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'stfld' -and $_.Operand.Name -eq 'PrecisionWalking'});$wholeReturnWalk=$false
foreach($store in $walkStores){$idx=$tick.Body.Instructions.IndexOf($store);if($tick.Body.Instructions[$idx-1].IsLdcI4() -and $tick.Body.Instructions[$idx-1].GetLdcI4Value() -eq 1){$wholeReturnWalk=$true}}
Check $wholeReturnWalk 'entire return owns walking latch from its first tick, beyond old near-entry activation'
Check ((Calls (Method 'TryMaintainCapabilityRouteRetryHold') 'ApplyCapabilityReturnWalking').Count -eq 1) 'native input consumer enforces return walk beyond one update'
Check ((Calls (Method 'ApplyCapabilityReturnWalking') 'PlayerEnableSprint').Count -eq 1) 'return walk releases actual native sprint latch'
$h=[Security.Cryptography.SHA256]::Create();try{$sha=[BitConverter]::ToString($h.ComputeHash([IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $Dll).Path))).Replace('-','')}finally{$h.Dispose()}
@{passed=($script:errors.Count -eq 0);checks=$script:n;errors=@($script:errors);dllSha256=$sha;physicsSimulated=$false}|ConvertTo-Json -Depth 5|Set-Content -Encoding UTF8 -LiteralPath $Output
Write-Output "RC116 ingress contract boundary: checks=$script:n failures=$($script:errors.Count)";$m.Dispose();if($script:errors.Count){$script:errors;exit 1}
