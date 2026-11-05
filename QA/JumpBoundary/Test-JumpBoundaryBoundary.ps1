param([string]$Dll,[string]$Output=(Join-Path $PSScriptRoot 'jump-boundary-il.json'))
$ErrorActionPreference='Stop'
[Reflection.Assembly]::LoadFrom('D:/Games/SIT_DIR/mod or addon builds/dnSpy-net-win64/bin/dnlib.dll')|Out-Null
$m=[dnlib.DotNet.ModuleDefMD]::Load((Resolve-Path -LiteralPath $Dll).Path);$r=$m.GetTypes()|Where-Object FullName -eq 'BotTraversal.Core.FollowerTraversalTestRuntime'
$script:n=0;$script:errors=[Collections.Generic.List[string]]::new()
function Check($v,$s){$script:n++;if(!$v){$script:errors.Add($s)}}
function Method($s){$r.Methods|Where-Object Name -eq $s|Select-Object -First 1}
function Calls($f,$s){if(!$f -or !$f.HasBody){return @()};@($f.Body.Instructions|Where-Object{$_.OpCode.Name -in @('call','callvirt') -and $_.Operand.Name -eq $s})}
function Before($f,$a,$b){$x=@(Calls $f $a);$y=@(Calls $f $b);$x.Count -gt 0 -and $y.Count -gt 0 -and $x[0].Offset -lt $y[0].Offset}
$read=Method 'GetCapabilityJumpPlanarVelocity';$body=Method 'TryObserveCapabilityJumpBodyVelocity';$carry=Method 'TryCarryCapabilityJumpBodyObservation';$acquire=Method 'AcquireLearningV375PersistentRouteTransaction'
Check ((Calls $read 'TryObserveCapabilityJumpBodyVelocity').Count -eq 1) 'production velocity uses bounded actual body observation'
Check ((Calls $body 'BodyVelocityObservation').Count -eq 1) 'state-independent measurement policy integrated'
Check ((Calls $read 'ReconcileJumpVelocity').Count -eq 0 -and (Calls $body 'ReconcileJumpVelocity').Count -eq 0) 'RC118 conditional outlier comparison is no longer a live admission source'
Check ((Calls $body 'get_IsGrounded').Count -eq 0 -and (Calls $body 'get_CurrentStateName').Count -eq 0) 'native state does not erase physical observation'
foreach($name in @('TryValidateV2CurrentTypedStep','TryValidateV2AdjacentPhysicalContinuationBoundary','PeekExactGeneration','IsCapabilityRouteRetrySafe')){Check ((Calls $body $name).Count -eq 1) ('body observation retains '+$name)}
Check ((Calls $acquire 'TryCarryCapabilityJumpBodyObservation').Count -eq 1) 'canonical native probe allocation carries passive measurement'
Check (Before $acquire 'BeginTraversal' 'TryCarryCapabilityJumpBodyObservation') 'new writer still issued before passive sample retag'
foreach($name in @('CarryBodyObservation','TryValidateV2CurrentTypedStep','IsCapabilityRouteRetrySafe')){Check ((Calls $carry $name).Count -eq 1) ('handoff requires '+$name)}
Check (@($carry.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'stfld' -and $_.Operand.Name -eq 'CapabilityBodyVelocity'}).Count -eq 1) 'handoff retags only one cursor observation'
foreach($name in @('BeginTraversal','ContinueTraversal','MovementContextTryJump','TryPlayerTeleport','PublishCapabilityCompletion','TryRequestCapabilityFullRouteRetry')){Check ((Calls $carry $name).Count -eq 0 -and (Calls $body $name).Count -eq 0) ('measurement transfer cannot grant writer or native proof '+$name)}
$prefix=Method 'IsCapabilityWalkPrefixForJump';$handoff=Method 'TryHandoffLearningV388PhysicalCorridorToReadySuccessor'
Check ((Calls $handoff 'IsCapabilityWalkPrefixForJump').Count -eq 1) 'existing physical frontier accepts only proven overlapping WT prefix'
foreach($name in @('WalkPrefixJump','TryValidateV2CurrentTypedStep','TryValidateV2AdjacentPhysicalContinuationBoundary','TryGetLearningV460JumpAdmissionGeometry')){Check ((Calls $prefix $name).Count -eq 1) ('WT prefix requires '+$name)}
Check ((Calls $prefix 'MarkFollowerTraversalEpisodeCompleted').Count -eq 0 -and (Calls $prefix 'MovementContextTryJump').Count -eq 0) 'prefix evidence itself cannot resolve debt or trigger Jump'
Check ((Calls $handoff 'IsLearningV449JumpInsidePhysicalTriggerHorizon').Count -eq 1 -and (Calls $handoff 'TryDispatchLearningV378PersistentActuatorFrontierProbe').Count -eq 1) 'existing exact native consumer still owns the handoff horizon'
$miss=Method 'HasCapabilityMeasuredMissedJumpWindow';$defer=Method 'ShouldDeferCapabilityJumpProbe'
foreach($name in @('CrossedJumpWindow','TryObserveCapabilityJumpBodyVelocity','HasActiveTraversalProbe')){Check ((Calls $miss $name).Count -eq 1) ('missed frontier requires real body crossing '+$name)}
Check ((Calls $defer 'HasCapabilityMeasuredMissedJumpWindow').Count -eq 1) 'same preallocation gate classifies actual missed execution'
Check ((Calls $defer 'TryObserveCapabilityJumpBodyVelocity').Count -eq 1) 'unknown speed evidence defers instead of inventing unsafe failure'
$guard=Method 'IsCapabilityJumpTriggerReady';$trigger=Method 'TriggerNativeJump'
Check ((Calls $guard 'get_IsGrounded').Count -eq 1 -and (Calls $guard 'get_CanJump').Count -eq 1) 'native grounding and CanJump remain mandatory'
Check ((Calls $guard 'TryObserveCapabilityJumpBodyVelocity').Count -eq 1) 'final unsafe decision distinguishes unavailable observation'
Check (@($trigger.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldstr' -and $_.Operand -eq 'velocityObservationPending'}).Count -eq 1) 'missing observation does not start full route retry'
$flag=@($trigger.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'stfld' -and $_.Operand.Name -eq 'JumpAttempted'}|Select-Object -First 1)
$speedCalls=@(Calls $trigger 'GetLearningV329PlanarSpeed')
Check ($flag.Count -eq 1 -and $speedCalls.Count -ge 1 -and @($speedCalls|Where-Object{$_.Offset -gt $flag[0].Offset}).Count -eq 0) 'takeoff speed captured before JumpAttempted invalidates pre-native observation'
$snapshot=@($trigger.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'stfld' -and $_.Operand.Name -eq 'LearningV414JumpTakeoffPlanarSpeed'})
$localSnapshot=$false;if($snapshot.Count -eq 1){$idx=$trigger.Body.Instructions.IndexOf($snapshot[0]);$localSnapshot=$trigger.Body.Instructions[$idx-1].OpCode.Name.StartsWith('ldloc')}
Check $localSnapshot 'capability snapshot consumes same captured speed rather than a fresh raw read'
$continue=Method 'UpdateLearningV331NativeGroundContinuation';$worldCalls=@(Calls $continue 'TryCommitV2ProbePhysicalMotion');$world=$false
if($worldCalls.Count -eq 1){$idx=$continue.Body.Instructions.IndexOf($worldCalls[0]);$world=$continue.Body.Instructions[$idx-1].OpCode.Name.StartsWith('ldloc')}
Check $world 'supported native Jump continuation opts into world direction using existing exact input parameter'
Check ((Calls $continue 'TryGetLearningV375FutureSameChainSuccessor').Count -ge 1) 'Jump Dropdown successor still prevents duplicate continuation'
$h=[Security.Cryptography.SHA256]::Create();try{$sha=[BitConverter]::ToString($h.ComputeHash([IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $Dll).Path))).Replace('-','')}finally{$h.Dispose()}
@{passed=($script:errors.Count -eq 0);checks=$script:n;errors=@($script:errors);dllSha256=$sha;physicsSimulated=$false}|ConvertTo-Json -Depth 5|Set-Content -Encoding UTF8 -LiteralPath $Output
Write-Output "RC119 jump boundary: checks=$script:n failures=$($script:errors.Count)";$m.Dispose();if($script:errors.Count){$script:errors;exit 1}
