param([string]$Dll,[string]$Output=(Join-Path $PSScriptRoot 'route-retry-il.json'))
$ErrorActionPreference='Stop'
[Reflection.Assembly]::LoadFrom('D:/Games/SIT_DIR/mod or addon builds/dnSpy-net-win64/bin/dnlib.dll')|Out-Null
$m=[dnlib.DotNet.ModuleDefMD]::Load((Resolve-Path -LiteralPath $Dll).Path)
$r=$m.GetTypes()|Where-Object FullName -eq 'BotTraversal.Core.FollowerTraversalTestRuntime'
$script:checks=0;$script:errors=[Collections.Generic.List[string]]::new()
function Check($ok,$label){$script:checks++;if(!$ok){$script:errors.Add($label)}}
function Method($name){$r.Methods|Where-Object Name -eq $name|Select-Object -First 1}
function Calls($method,$name){@($method.Body.Instructions|Where-Object{$_.OpCode.Name -in @('call','callvirt','newobj') -and $_.Operand.Name -eq $name})}
function Before($method,$first,$second){$a=@(Calls $method $first);$b=@(Calls $method $second);return $a.Count -gt 0 -and $b.Count -gt 0 -and $a[0].Offset -lt $b[0].Offset}
$tick=Method 'TickCapabilityFullRouteRetry'
foreach($name in @('IsCapabilityRouteRetrySafe','TryEnterFollowPhysicalMotion','Dispose','ForceReleaseTraversalProbe','StagePosition','StageReached','Retire','TryScrubLearningV364NativeMoverEpochAnchor','ArmLearningV364PhysicalNavigationEpochHandoff','TryProveLearningV364PhysicalNavigationEpochFromAcceptedPath','CalculateStrictPath','BotOwnerGoToPoint','HoldCapabilityRouteRetry')){
 Check ((Calls $tick $name).Count -ge 1) ('actual return/replay orchestration '+$name)
}
Check (Before $tick 'TryEnterFollowPhysicalMotion' 'BotOwnerGoToPoint') 'issued Follow scope before return goal'
$stage=@(Calls $tick 'StageReached');$retire=@(Calls $tick 'Retire')
Check ($stage.Count -eq 1 -and $retire.Count -eq 2 -and $stage[0].Offset -lt $retire[-1].Offset) 'actual pose proof before live mission replacement; separate dead-bot cleanup may retire first'
$on=Method 'OnBotOwnerUpdate';Check (Before $on 'TickCapabilityFullRouteRetry' 'UpdateProbe') 'recovery preempts old probe producer on actual bot update'
foreach($name in @('FinalizeUnifiedTraversalFailure','FinishProbeWithoutReanchor')){
 $f=Method $name;$request=@(Calls $f 'TryRequestCapabilityFullRouteRetry');Check ($request.Count -eq 1) ('terminal failure intercepted '+$name)
 $i=$f.Body.Instructions.IndexOf($request[0]);Check ($f.Body.Instructions[$i+1].OpCode.Name -in @('brfalse','brfalse.s') -and $f.Body.Instructions[$i+2].OpCode.Name -eq 'ret') ('accepted retry exits before old floor/alternate behavior '+$name)
}
$f=Method 'TryArmLearningV431EquivalentLocalBarrierSelfRescue';Check ((Calls $f 'TryRequestCapabilityFullRouteRetry').Count -eq 1) 'old alternate-obstacle rescue replaced by whole-route request'
foreach($name in @('TryFindLearningV431EquivalentLocalBarrier','TryBeginLearningV431EquivalentLocalBarrierSelfRescue','AdvanceFollowerRouteResumeFloor','MarkFollowerTraversalEpisodeCompleted','TryAdvanceRouteMission')){Check ((Calls $tick $name).Count -eq 0) ('return/replay cannot manufacture success or alternate obstacle '+$name)}
$clone=Method 'CloneCapabilityReplayStep';Check (@($clone.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'newobj' -and $_.Operand.DeclaringType.Name -eq 'FollowerRouteMissionStep'}).Count -eq 1) 'replay constructs genuinely fresh step object'
foreach($field in @('Lifecycle','LearningV429JumpAdmissionReserved','LearningV455NativeJumpHowObserved','LearningV424ActuatorProbeRequestedAt')){Check (@($clone.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'stfld' -and $_.Operand.Name -eq $field}).Count -eq 0) ('old execution receipt not copied '+$field)}
$bind=Method 'BindProbeLifecycle';Check ((Calls $bind 'CaptureCapabilityRouteRetryPlan').Count -eq 1) 'route root snapshot tied to actually selected probe'
$gate=Method 'TryValidateV2CurrentTypedStep';Check ((Calls $gate 'IsCapabilityRouteRetryReturning').Count -eq 1) 'normal typed producers reject returning mission'
$target=Method 'TryGetPlayerRouteMissionFollowTarget';Check (Before $target 'TickCapabilityFullRouteRetry' 'TryArmOrRefreshFollowerRouteMission') 'pending retry is handled before ordinary follow mission selection'
$active=Method 'TryGetActiveFollowerRouteMission';Check ((Calls $active 'IsCapabilityRouteRetryReturning').Count -eq 1) 'pending mission remains visible across ordinary expiry'
foreach($name in @('GetFollowerRouteResumeFloor','GetFollowerRouteExecutionFloor')){Check ((Calls (Method $name) 'IsCapabilityFullRouteReplay').Count -eq 1) ('fresh replay progress view instead of old global skip floor '+$name)}
$completion=@($r.Methods|Where-Object{$_.Name -eq 'MarkFollowerTraversalEpisodeCompleted' -and $_.MethodSig.Params.Count -eq 3})[0]
Check ((Calls $completion 'IsCapabilityRouteRetryReturning').Count -eq 1) 'old late completion blocked before history mutation'
Check ($null -eq (Method 'TryCaptureCapabilityEntryAlignment')) 'retired normal entry clamp is absent'
$all=@($m.GetTypes()|ForEach-Object{$_.Methods}|Where-Object HasBody);$consumers=@($all|Where-Object{(Calls $_ 'CommitCapabilityNativePlayerInput').Count -gt 0})
Check ($consumers.Count -eq 3) 'exactly two old moving producers and one cleanup producer'
$hold=Method 'CommitCapabilityRouteRetryZeroInput';$move=@(Calls $hold 'CommitCapabilityNativePlayerInput');$at=$hold.Body.Instructions.IndexOf($move[0]);Check ($hold.Body.Instructions[$at-1].Operand.Name -eq 'get_zero') 'added primitive consumer delivers only literal Vector2.zero'
Check (@($all|ForEach-Object{Calls $_ 'TryCommitNativePlayerMove'}).Count -eq 1) 'one existing Player.Move gateway'
$catalog=$m.GetTypes()|Where-Object Name -eq 'FollowerControlBackend';$find=$catalog.Methods|Where-Object Name -eq 'FindSite';$sites=@($find.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldstr' -and $_.Operand -like 'R112-*'})
Check ($sites.Count -eq 7) 'exactly seven reviewed return backend call sites'
foreach($site in $sites){$ins=$find.Body.Instructions;$index=$ins.IndexOf($site);$target=$ins.IndexOf($ins[$index+2].Operand);Check ($ins[$target+1].GetLdcI4Value() -eq 0) ('new return operation requires exact scope '+$site.Operand)}
$algo=[Security.Cryptography.SHA256]::Create();try{$sha=[BitConverter]::ToString($algo.ComputeHash([IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $Dll).Path))).Replace('-','')}finally{$algo.Dispose()}
@{passed=($script:errors.Count -eq 0);checks=$script:checks;errors=@($script:errors);dllSha256=$sha;physicsSimulated=$false}|ConvertTo-Json -Depth 5|Set-Content -Encoding UTF8 -LiteralPath $Output
Write-Output "RC112 RouteRetry IL: checks=$script:checks failures=$($script:errors.Count)";$m.Dispose();if($script:errors.Count){$script:errors;exit 1}
