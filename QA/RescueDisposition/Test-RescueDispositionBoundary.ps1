param([string]$Dll,[string]$Output=(Join-Path $PSScriptRoot 'rescue-disposition-il.json'))
$ErrorActionPreference='Stop'
[Reflection.Assembly]::LoadFrom('D:/Games/SIT_DIR/mod or addon builds/dnSpy-net-win64/bin/dnlib.dll')|Out-Null
$m=[dnlib.DotNet.ModuleDefMD]::Load((Resolve-Path -LiteralPath $Dll).Path);$r=$m.GetTypes()|Where-Object FullName -eq 'BotTraversal.Core.FollowerTraversalTestRuntime'
$script:n=0;$script:errors=[Collections.Generic.List[string]]::new()
function Check($v,$s){$script:n++;if(!$v){$script:errors.Add($s)}}
function Method($s){$r.Methods|Where-Object Name -eq $s|Select-Object -First 1}
function Calls($f,$s){if(!$f -or !$f.HasBody){return @()};@($f.Body.Instructions|Where-Object{$_.OpCode.Name -in @('call','callvirt') -and $_.Operand.Name -eq $s})}
function Field($f,$s){if(!$f -or !$f.HasBody){return @()};@($f.Body.Instructions|Where-Object{$_.OpCode.Name -in @('ldfld','ldflda') -and $_.Operand.Name -eq $s})}
function Before($f,$a,$b){$x=@(Calls $f $a);$y=@(Calls $f $b);$x.Count -gt 0 -and $y.Count -gt 0 -and $x[0].Offset -lt $y[0].Offset}
$terminal=Method 'TryReanchorAndRelease';$handle=Method 'TryHandleCapabilityRescueTerminal';$retire=Method 'RetireCapabilityRescueCommitmentForRetry'
$request=$r.Methods|Where-Object{$_.Name -eq 'TryRequestCapabilityFullRouteRetry' -and $_.MethodSig.Params.Count -eq 3 -and $_.MethodSig.Params[0].FullName -eq 'EFT.BotOwner'}
Check ((Calls $terminal 'TryHandleCapabilityRescueTerminal').Count -eq 1) 'legacy completion consumes rescue disposition'
foreach($name in @('HasTraversalDownstreamConnectivity','RestoreProbeInput','BotOwnerStopMove','BotOwnerGoToPoint','UnregisterActiveProbe')){Check (Before $terminal 'TryHandleCapabilityRescueTerminal' $name) ('rescue decision precedes '+$name)}
$consume=@(Calls $terminal 'TryHandleCapabilityRescueTerminal');$shortCircuit=$false
if($consume.Count -eq 1){$idx=$terminal.Body.Instructions.IndexOf($consume[0]);$next=$terminal.Body.Instructions[$idx+1];$shortCircuit=$next.OpCode.Name.StartsWith('brfalse') -and $terminal.Body.Instructions[$idx+2].OpCode.Name -eq 'ldc.i4.1' -and $terminal.Body.Instructions[$idx+3].OpCode.Name -eq 'ret'}
Check $shortCircuit 'handled retry or deferred terminal returns without legacy reanchor'
foreach($name in @('IsProbeLifecycleCurrent','get_DebtOutstanding','TryGetValue','IsLearningV433ExactLocalActuatorSubstitution','TryRequestCapabilityFullRouteRetry','TryCommitLearningV433FailedPhysicalActuatorEpoch')){Check ((Calls $handle $name).Count -eq 1) ('rescue terminal requires '+$name)}
$live=@(Calls $handle 'IsProbeLifecycleCurrent');$liveBranch=$false
if($live.Count -eq 1){$idx=$handle.Body.Instructions.IndexOf($live[0]);$liveBranch=$handle.Body.Instructions[$idx+1].OpCode.Name.StartsWith('brfalse')}
Check $liveBranch 'stale lifecycle cannot request or retain rescue terminal'
foreach($name in @('RouteMissionId','RouteMissionStepIndex','Origin','LifecycleMission','LifecycleStep')){Check ((Field $handle $name).Count -ge 1) ('exact semantic origin guard '+$name)}
$proofGate=$handle -and $handle.HasBody -and $handle.Body.Instructions[0].OpCode.Name -eq 'ldarg.1' -and $handle.Body.Instructions[1].OpCode.Name.StartsWith('brtrue')
Check $proofGate 'verified normal completion bypasses failure-only handler'
Check (Before $handle 'TryRequestCapabilityFullRouteRetry' 'TryCommitLearningV433FailedPhysicalActuatorEpoch') 'admitted full retry precedes physical epoch scrub'
$retryCall=@(Calls $handle 'TryRequestCapabilityFullRouteRetry');$retryBranch=$false
if($retryCall.Count -eq 1){$idx=$handle.Body.Instructions.IndexOf($retryCall[0]);$retryBranch=$handle.Body.Instructions[$idx+1].OpCode.Name.StartsWith('brtrue')}
Check $retryBranch 'retry result selects retained probe or admitted physical commit'
Check ((Calls $request 'RetireCapabilityRescueCommitmentForRetry').Count -eq 1) 'common admitted retry retires physical substitute'
Check (Before $request 'RequestFullRetry' 'RetireCapabilityRescueCommitmentForRetry') 'strategy removed only after mission accepts retry'
Check (Before $request 'RetireCapabilityRescueCommitmentForRetry' 'ResetLearningV431EquivalentBarrierSelfRescueState') 'retirement precedes rescue state reset and producer return'
foreach($name in @('IsCapabilityRouteRetryReturning','TryGetValue','IsLearningV433ExactLocalActuatorSubstitution','Remove')){Check ((Calls $retire $name).Count -eq 1) ('exact retry cleanup requires '+$name)}
foreach($name in @('RouteMissionId','RouteMissionStepIndex')){Check ((Field $retire $name).Count -ge 1) ('cleanup cannot remove unrelated strategy '+$name)}
Check ((Calls $retire 'get_CurrentStepIndex').Count -ge 1) 'cleanup cannot remove unrelated strategy CurrentStepIndex'
Check ((Calls $retire 'ClearLearnedTraversalCommitment').Count -eq 0) 'cleanup does not repeat the wrong original-edge filter'
foreach($f in @($handle,$retire)){
 foreach($name in @('MovementContextTryJump','TryPlayerTeleport','MarkLearnedTraversalEdgeSuccess','MarkFollowerTraversalEpisodeCompleted','PublishProbeLifecycleCompletion','BeginTraversal','BotOwnerGoToPoint','UnregisterActiveProbe')){Check ((Calls $f $name).Count -eq 0) ('disposition grants no native success writer release or target '+$name)}
 Check (@($f.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'stfld'}).Count -eq 0) 'disposition adds no mutable probe/cursor state'
}
$h=[Security.Cryptography.SHA256]::Create();try{$sha=[BitConverter]::ToString($h.ComputeHash([IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $Dll).Path))).Replace('-','')}finally{$h.Dispose()}
@{passed=($script:errors.Count -eq 0);checks=$script:n;errors=@($script:errors);dllSha256=$sha;physicsSimulated=$false}|ConvertTo-Json -Depth 5|Set-Content -Encoding UTF8 -LiteralPath $Output
Write-Output "RC120 rescue disposition: checks=$script:n failures=$($script:errors.Count)";$m.Dispose();if($script:errors.Count){$script:errors;exit 1}
