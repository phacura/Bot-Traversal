param([string]$Dll,[string]$Output=(Join-Path $PSScriptRoot 'il-result.json'))
$ErrorActionPreference='Stop'
function Get-ConsumerSha256([string]$Path){$alg=[Security.Cryptography.SHA256]::Create();try{return [BitConverter]::ToString($alg.ComputeHash([IO.File]::ReadAllBytes([IO.Path]::GetFullPath($Path)))).Replace('-','')}finally{$alg.Dispose()}}
[Reflection.Assembly]::LoadFrom('D:/Games/SIT_DIR/mod or addon builds/dnSpy-net-win64/bin/dnlib.dll')|Out-Null
$m=[dnlib.DotNet.ModuleDefMD]::Load((Resolve-Path -LiteralPath $Dll).Path)
$r=$m.GetTypes()|Where-Object FullName -eq 'BotTraversal.Core.FollowerTraversalTestRuntime'
$port=$r.NestedTypes|Where-Object Name -eq 'CapabilityLocomotionPort'
$script:checks=0;$script:errors=[Collections.Generic.List[string]]::new()
function Check($ok,$name){$script:checks++;if(!$ok){$script:errors.Add($name)}}
function FindType($name){$m.GetTypes()|Where-Object Name -eq $name|Select-Object -First 1}
function Method($type,$name){$type.Methods|Where-Object Name -eq $name|Select-Object -First 1}
function Calls($method,$name){@($method.Body.Instructions|Where-Object{$_.OpCode.Name -in @('call','callvirt') -and $_.Operand.Name -eq $name})}
$driver=Method (FindType 'FollowerCapabilityLocomotionDriver') 'Commit'
Check ((Calls $driver 'Commit').Count -eq 1) 'shared driver calls native input driver'
foreach($name in @('RequestNativeRun','InterruptOutgoingStop','Play','ProcessStateEnter','get_frameCount')){Check ((Calls $driver $name).Count -eq 0) ('normal driver retires '+$name)}
Check ((Calls $driver 'IsCurrent').Count -eq 1) 'driver rechecks captured current identity'
$maintain=Method $r 'TryMaintainCapabilityNativeInput'
$validation=@(Calls $maintain 'TryValidateV2CurrentTypedStep')
$nativeLeaseCorrect=$false
if($validation.Count -eq 2){$at=$maintain.Body.Instructions.IndexOf($validation[0]);$nativeLeaseCorrect=$at -ge 2 -and $maintain.Body.Instructions[$at-2].OpCode.Name -eq 'ldc.i4.0'}
Check $nativeLeaseCorrect 'native input validation explicitly permits current active native probe; pre-native guard stays separate'
foreach($name in @('TryValidateV2CurrentTypedStep','TryGetActiveProbe','IsActiveProbe','IsProbeLifecycleCurrent','TryEnterBoundPhysicalMotion','Commit')){Check ((Calls $maintain $name).Count -ge 1) ('actual native input scope rechecks '+$name)}
foreach($name in @('TryEnterV2CurrentTypedTraversalScope','TryEnterV2CurrentIngressScope','RequestNativeRun')){Check ((Calls $maintain $name).Count -eq 0) ('native scope cannot borrow '+$name)}
foreach($name in @('MovementWriterGeneration','LifecycleMission','LifecycleStep')){Check (@($maintain.Body.Instructions|Where-Object{ $_.OpCode.Name -in @('ldfld','ldflda') -and $_.Operand.Name -eq $name}).Count -ge 1) ('native scope binds '+$name)}
Check ((Calls $maintain 'get_NativeLocomotionPending').Count -eq 0) 'animation receipt cannot short circuit native consumer'
$hook=Method (FindType 'FollowerCapabilityNativeConsumerPatch') 'Prefix'
Check ((Calls $hook 'MaintainCapabilityInputBeforeNativeConsumer').Count -eq 1) 'actual native callback prefix is connected'
$apply=Method (FindType 'FollowerCapabilityNativeConsumerPatch') 'Apply'
Check (@($apply.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldstr' -and $_.Operand -eq 'OnStateMove'}).Count -eq 1 -and (Calls $apply 'Patch').Count -eq 1) 'hook targets real OnStateMove'
$consumer=Method $r 'MaintainCapabilityInputBeforeNativeConsumer'
foreach($name in @('get_MovementContext','get_CurrentState','get_CurrentManagedState','get_OverridenState','get_OverridenControlsState','get_IgnoreDeltaMovement','TryMaintainCapabilityNativeInput')){Check ((Calls $consumer $name).Count -ge 1 -or @($consumer.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldfld' -and $_.Operand.Name -eq $name.Replace('get_','')}).Count -ge 1) ('consumer identity/safety '+$name)}
Check ((Calls $consumer 'get_frameCount').Count -eq 0) 'no rendered frame dedup at consumption'
Check (@($port.Methods|Where-Object Name -eq 'RequestNativeRun').Count -eq 0) 'real locomotion port has no animation replay fallback'
$input=Method $port 'RequestInput'
foreach($name in @('IsUsingMedical','IsHandsInteractionActiveForAuthority','TryPeekActiveCommand','IsTeleportTransactionActive','TryGetImmediateSurvivalThreat','HasCombatMovementClaim','IsCurrent','CommitCapabilityNativePlayerInput')){Check ((Calls $input $name).Count -ge 1) ('input safety '+$name)}
Check (@($input.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'stfld' -and $_.Operand.Name -eq 'LocomotionRequest'}).Count -eq 0) 'input does not mint animation-evaluation receipt'
$gait=Method $r 'SelectCapabilityOrdinaryFollowGait'
foreach($name in @('PeekExactGeneration','HasActiveTraversalProbe','TryGetActiveFollowerRouteMission','IsUsingMedical','IsHandsInteractionActiveForAuthority','TryPeekActiveCommand','HasCombatMovementClaim','Select')){Check ((Calls $gait $name).Count -ge 1) ('ordinary follow boundary '+$name)}
$gaitPrefix=Method (FindType 'FollowerCapabilityFollowGaitPatch') 'Prefix'
Check ((Calls $gaitPrefix 'SelectCapabilityOrdinaryFollowGait').Count -eq 1 -and (Calls $gaitPrefix 'Invoke').Count -eq 1) 'actual PIT gait prefix uses actual config getter'
$register=Method (FindType 'FollowerMovementWriterAuthorityPatch') 'Apply'
# Registration method name may differ; inspect every existing patch method.
$registration=@((FindType 'FollowerMovementWriterAuthorityPatch').Methods|Where-Object HasBody|ForEach-Object{Calls $_ 'Apply'})
foreach($name in @('FollowerCapabilityNativeConsumerPatch','FollowerCapabilityFollowGaitPatch')){Check (@($registration|Where-Object{$_.Operand.DeclaringType.Name -eq $name}).Count -eq 1) ('startup registration '+$name)}
$all=@($m.GetTypes()|ForEach-Object{$_.Methods}|Where-Object HasBody)
Check (@($all|ForEach-Object{Calls $_ 'TryCommitNativePlayerMove'}).Count -eq 1) 'same single native Player.Move gateway'
Check (@($all|ForEach-Object{Calls $_ 'RequestCapabilityLocomotionResume'}).Count -eq 6) 'six original locomotion consumers now use natural path'
Check (@($all|ForEach-Object{Calls $_ 'CommitCapabilityNativePlayerInput'}).Count -eq 3) 'two retained native input producers plus scoped zero-only failed-route cleanup'
Check (@($all|ForEach-Object{$_.Body.Instructions}|Where-Object{$_.OpCode.Name -in @('call','callvirt') -and $_.Operand.FullName -eq 'System.Void IAnimator::Play(System.Int32,System.Int32,System.Single)'}).Count -eq 0) 'retired native Animator.Play writer is absent from actual DLL'
$baseline=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'baseline-native-inventory.json') -Raw|ConvertFrom-Json
$backend=FindType 'FollowerControlBackend'
$backendCalls=@($backend.Methods|Where-Object HasBody|ForEach-Object{$_.Body.Instructions}|Where-Object{$_.OpCode.Name -in @('call','callvirt')}|ForEach-Object{$_.Operand.FullName}|Sort-Object -Unique)
$remaining=@($baseline.nativeMethods|Where-Object{$_ -ne 'System.Void IAnimator::Play(System.Int32,System.Int32,System.Single)'})
Check ($remaining.Count -eq 58 -and @($remaining|Where-Object{$_ -notin $backendCalls}).Count -eq 0) '58 remaining original native mutation signatures retained'
$result=@{passed=($script:errors.Count -eq 0);checks=$script:checks;errors=@($script:errors);dllSha256=(Get-ConsumerSha256 $Dll);physicsSimulated=$false}
$result|ConvertTo-Json -Depth 5|Set-Content -Encoding UTF8 -LiteralPath $Output
Write-Output "Native consumer / Follow gait IL: checks=$script:checks failures=$($script:errors.Count)"
$m.Dispose()
if($script:errors.Count){$script:errors;exit 1}
