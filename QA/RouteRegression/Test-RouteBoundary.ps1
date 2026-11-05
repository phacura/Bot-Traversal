param([string]$Dll,[string]$Output=(Join-Path $PSScriptRoot 'il-result.json'))
$ErrorActionPreference='Stop'
function Sha([string]$Path){$a=[Security.Cryptography.SHA256]::Create();try{[BitConverter]::ToString($a.ComputeHash([IO.File]::ReadAllBytes([IO.Path]::GetFullPath($Path)))).Replace('-','')}finally{$a.Dispose()}}
[Reflection.Assembly]::LoadFrom('D:/Games/SIT_DIR/mod or addon builds/dnSpy-net-win64/bin/dnlib.dll')|Out-Null
$m=[dnlib.DotNet.ModuleDefMD]::Load((Resolve-Path -LiteralPath $Dll).Path)
$r=$m.GetTypes()|Where-Object FullName -eq 'BotTraversal.Core.FollowerTraversalTestRuntime'
function CallKey([object[]]$values){[string[]]$normalized=@($values);[Array]::Sort($normalized,[StringComparer]::Ordinal);return ($normalized -join "`n")}
$script:checks=0;$script:errors=[Collections.Generic.List[string]]::new()
function Check($ok,$name){$script:checks++;if(!$ok){$script:errors.Add($name)}}
function Method($name){$r.Methods|Where-Object Name -eq $name|Select-Object -First 1}
function Calls($method,$name){@($method.Body.Instructions|Where-Object{$_.OpCode.Name -in @('call','callvirt') -and $_.Operand.Name -eq $name})}
function Before($method,$first,$second){$a=@(Calls $method $first);$b=@(Calls $method $second);return $a.Count -gt 0 -and $b.Count -gt 0 -and $a[0].Offset -lt $b[0].Offset}
$observe=Method 'ObserveCapabilityRouteContinuation'
foreach($name in @('PeekExactGeneration','TryValidateV2CurrentTypedStep','IsActiveProbe','IsProbeLifecycleCurrent','HasActiveTraversalProbe','TryPeekActiveCommand','IsTeleportTransactionActive','IsLearningV350TerminalPhysicalRecoveryEligible','IsUsingMedical','IsHandsInteractionActiveForAuthority','TryGetImmediateSurvivalThreat','HasCombatMovementClaim','Evaluate')){Check ((Calls $observe $name).Count -ge 1) ('shared continuation admission '+$name)}
$typed=@(Calls $observe 'TryValidateV2CurrentTypedStep');$nativePhaseAllowed=$false
if($typed.Count -eq 1){$at=$observe.Body.Instructions.IndexOf($typed[0]);$nativePhaseAllowed=$at -ge 2 -and $observe.Body.Instructions[$at-2].OpCode.Name -eq 'ldc.i4.0'}
Check $nativePhaseAllowed 'shared observation validates native current step without pretending pre-native'
foreach($name in @('MovementWriterGeneration','LifecycleMission','LifecycleStep','LearningV375MovementGeneration','LearningV375RouteMissionId')){Check (@($observe.Body.Instructions|Where-Object{$_.OpCode.Name -in @('ldfld','ldflda') -and $_.Operand.Name -eq $name}).Count -ge 1) ('exact producer resource '+$name)}
$route=Method 'TryEnterCapabilityRouteProducerScope'
$physical=Method 'TryEnterCapabilityRoutePhysicalScope'
foreach($method in @($route,$physical)){
 Check ((Calls $method 'ObserveCapabilityRouteContinuation').Count -eq 2) ([string]$method.Name+' reobserves after issuance')
 Check ((Calls $method 'Dispose').Count -ge 1) ([string]$method.Name+' disposes a stale issued scope')
 Check ((Calls $method 'RecordCapabilityRouteContinuationRejection').Count -ge 1) ([string]$method.Name+' reports typed rejection')
}
Check ((Calls $route 'TryEnterVerifiedRouteIntent').Count -eq 1 -and (Before $route 'ObserveCapabilityRouteContinuation' 'TryEnterVerifiedRouteIntent')) 'route retains original independent live-intent authority gate after typed preflight'
Check ((Calls $physical 'TryEnterBoundPhysicalMotion').Count -eq 1 -and (Before $physical 'ObserveCapabilityRouteContinuation' 'TryEnterBoundPhysicalMotion')) 'physical continuation uses original exact generation issuance'
$update=Method 'UpdateLearningV375PersistentExecutablePhysicalRouteTransaction'
Check ((Calls $update 'TryEnterCapabilityRouteProducerScope').Count -eq 1 -and (Calls $update 'TryEnterVerifiedRouteIntent').Count -eq 0) 'route loop has one shared producer admission'
$gate=@(Calls $update 'TryEnterCapabilityRouteProducerScope')
$routed=@($update.Body.Instructions|Where-Object{$_.OpCode.Name -in @('call','callvirt') -and $_.Operand.DeclaringType.Name -eq 'FollowerControlBackend'})
Check ($gate.Count -eq 1 -and $routed.Count -eq 9 -and @($routed|Where-Object{$_.Offset -le $gate[0].Offset}).Count -eq 0) 'all nine original route control sites including heading are after shared admission'
$resolve=@(Calls $update 'ResolveCanonical');$noFallback=$false
if($resolve.Count -eq 1){
 $at=$update.Body.Instructions.IndexOf($resolve[0]);$ins=$update.Body.Instructions
 if($at+2 -lt $ins.Count -and $ins[$at+1].OpCode.Name -eq 'ldc.i4.2' -and $ins[$at+2].OpCode.Name -in @('bne.un','bne.un.s')){
  $end=$ins.IndexOf($ins[$at+2].Operand)
  $block=@($ins[($at+3)..($end-1)])
  $exits=@($block|Where-Object{$_.OpCode.Name -eq 'ret' -or ($_.OpCode.Name -in @('leave','leave.s') -and $_.Operand.OpCode.Name -eq 'ret')})
  $noFallback=$exits.Count -eq 1 -and @($block|Where-Object{$_.OpCode.Name -eq 'call' -and $_.Operand.Name -eq 'RecordCapabilityRouteContinuationRejection'}).Count -eq 1
 }
}
Check $noFallback 'actual rejected canonical branch returns before legacy NavMesh/NoWay selection'
$atomic=Method 'ExecuteLearningV406AtomicCanonicalNonActuatorRouteMovementIntent'
Check ((Calls $atomic 'TryEnterCapabilityRoutePhysicalScope').Count -eq 1 -and (Calls $atomic 'TryEnterBoundPhysicalMotion').Count -eq 0) 'late canonical physical input uses shared lifecycle scope'
Check (Before $atomic 'TryEnterCapabilityRoutePhysicalScope' 'ApplyCollisionActivePhysicalRouteMotion') 'actual physical submission follows typed scope'
$look=Method 'TryRefreshLearningV334DropDownLandingForwardLookahead'
Check ((Calls $look 'TryEnterCapabilityRoutePhysicalScope').Count -eq 1 -and (Calls $look 'TryEnterV2CurrentTypedTraversalScope').Count -eq 0) 'native landing lookahead has its own native lifecycle phase'
$phaseCall=@(Calls $look 'TryEnterCapabilityRoutePhysicalScope');$rightPhase=$false
if($phaseCall.Count -eq 1){$at=$look.Body.Instructions.IndexOf($phaseCall[0]);$rightPhase=$at -ge 3 -and $look.Body.Instructions[$at-3].OpCode.Name -eq 'ldc.i4.1'}
Check $rightPhase 'native landing caller submits NativeLanding phase rather than pre-native route phase'
$lookWrites=@($look.Body.Instructions|Where-Object{$_.OpCode.Name -in @('call','callvirt') -and $_.Operand.DeclaringType.Name -eq 'FollowerControlBackend'});$lookGate=@(Calls $look 'TryEnterCapabilityRoutePhysicalScope')
Check ($lookGate.Count -eq 1 -and $lookWrites.Count -gt 0 -and @($lookWrites|Where-Object{$_.Offset -le $lookGate[0].Offset}).Count -eq 0) 'landing lookahead cannot mutate mover before admission'
Check ((Calls $look 'Dispose').Count -ge 1) 'landing lookahead always disposes its short scope'
$bridge=Method 'TryDriveChainLocalSuccessorBridge';$reserve=@(Calls $bridge 'ReserveLegacyBridge');$reservedReturn=$false
if($reserve.Count -eq 1){
 $ins=$bridge.Body.Instructions;$at=$ins.IndexOf($reserve[0]);$branch=$ins[$at+1]
 if($branch.OpCode.Name -in @('brfalse','brfalse.s')){
  $end=$ins.IndexOf($branch.Operand);$block=@($ins[($at+2)..($end-1)])
  $reservedReturn=@($block|Where-Object{$_.OpCode.Name -eq 'ret'}).Count -eq 1 -and @($block|Where-Object{$_.OpCode.Name -in @('call','callvirt') -and $_.Operand.DeclaringType.Name -eq 'FollowerControlBackend'}).Count -eq 0
 }
}
Check $reservedReturn 'canonical bridge reservation returns handled without native/physical writes'
Check (Before $bridge 'ReserveLegacyBridge' 'BotMoverSprint') 'legacy bridge cannot adjust gait before canonical reservation'
$moved=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'moved-methods.json') -Raw|ConvertFrom-Json
foreach($method in $moved){Check (@($r.Methods|Where-Object Name -eq $method.name).Count -eq 1) ('single migrated implementation '+$method.name)}
$methodBaseline=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'il-method-baseline.json') -Raw|ConvertFrom-Json
foreach($entry in $methodBaseline.methods){
 $method=Method $entry.name
 $current=@($method.Body.Instructions|Where-Object{$_.OpCode.Name -in @('call','callvirt','newobj') -and !$_.Operand.Name.String.StartsWith('<') -and !$_.Operand.DeclaringType.Name.String.StartsWith('<')}|ForEach-Object{[string]$_.Operand.FullName}|Sort-Object)
 $expected=@($entry.calls)
 # RC117 adds the live actor to the SAME shared physical horizon, preserving every other call.
 if($entry.name -eq 'TryConsumeLearningV429AtomicJumpAdmission'){
  $expected=@($expected|ForEach-Object{$_.Replace('::IsLearningV449JumpInsidePhysicalTriggerHorizon(System.Single,System.Single)','::IsLearningV449JumpInsidePhysicalTriggerHorizon(System.Single,System.Single,EFT.BotOwner)')})
 }
 # RC119 adds only the reviewed passive observation transfer; all writer calls remain pinned.
 if($entry.name -eq 'AcquireLearningV375PersistentRouteTransaction'){
  $expected += 'System.Void BotTraversal.Core.FollowerTraversalTestRuntime::TryCarryCapabilityJumpBodyObservation(BotTraversal.Core.FollowerTraversalTestRuntime/ProbeRuntime,BotTraversal.Core.FollowerTraversalTestRuntime/FollowerRouteMission,BotTraversal.Core.FollowerTraversalTestRuntime/FollowerTraversalRouteCursor,System.Int64,System.Int64,BotTraversal.Core.FollowerMovementWriterAuthority/WriterDomain,System.String)'
 }
 # RC114 restores exact pre-RC112 normal atomic admission.
 Check ((CallKey $current) -ceq (CallKey $expected)) ('retained compiled calls with explicit RC112 heading gate '+$entry.name)
}
$all=@($m.GetTypes()|ForEach-Object{$_.Methods}|Where-Object HasBody)
Check (@($all|ForEach-Object{$_.Body.Instructions}|Where-Object{$_.OpCode.Name -in @('call','callvirt') -and $_.Operand.FullName -eq 'System.Void IAnimator::Play(System.Int32,System.Int32,System.Single)'}).Count -eq 0) 'no retired animation playback writer revived'
Check (@($all|ForEach-Object{Calls $_ 'TryCommitNativePlayerMove'}).Count -eq 1) 'same single native input gateway'
$baseline=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'baseline-native-inventory.json') -Raw|ConvertFrom-Json
$backend=$m.GetTypes()|Where-Object Name -eq 'FollowerControlBackend'|Select-Object -First 1
$calls=@($backend.Methods|Where-Object HasBody|ForEach-Object{$_.Body.Instructions}|Where-Object{$_.OpCode.Name -in @('call','callvirt')}|ForEach-Object{$_.Operand.FullName}|Sort-Object -Unique)
$expected=@($baseline.nativeMethods|Where-Object{$_ -ne 'System.Void IAnimator::Play(System.Int32,System.Int32,System.Single)'})
Check ($expected.Count -eq 58 -and @($expected|Where-Object{$_ -notin $calls}).Count -eq 0) '58 original native mutation signatures retained'
@{passed=($script:errors.Count -eq 0);checks=$script:checks;errors=@($script:errors);dllSha256=(Sha $Dll);physicsSimulated=$false}|ConvertTo-Json -Depth 5|Set-Content -Encoding UTF8 -LiteralPath $Output
Write-Output "Route continuation IL: checks=$script:checks failures=$($script:errors.Count)"
$m.Dispose()
if($script:errors.Count){$script:errors;exit 1}
