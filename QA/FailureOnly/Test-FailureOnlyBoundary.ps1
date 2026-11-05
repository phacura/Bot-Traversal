param([string]$Dll,[string]$Output=(Join-Path $PSScriptRoot 'failure-only-il.json'))
$ErrorActionPreference='Stop'
[Reflection.Assembly]::LoadFrom('D:/Games/SIT_DIR/mod or addon builds/dnSpy-net-win64/bin/dnlib.dll')|Out-Null
$m=[dnlib.DotNet.ModuleDefMD]::Load((Resolve-Path -LiteralPath $Dll).Path);$r=$m.GetTypes()|Where-Object FullName -eq 'BotTraversal.Core.FollowerTraversalTestRuntime'
$script:n=0;$script:errors=[Collections.Generic.List[string]]::new()
function Check($v,$s){$script:n++;if(!$v){$script:errors.Add($s)}}
function Method($s){$r.Methods|Where-Object Name -eq $s|Select-Object -First 1}
function Calls($f,$s){@($f.Body.Instructions|Where-Object{$_.OpCode.Name -in @('call','callvirt') -and $_.Operand.Name -eq $s})}
foreach($name in @('ShouldRestartCapabilityMissedEntry','TryCaptureCapabilityEntryAlignment','IsCapabilityJumpRouteHeadingReady','IsCapabilityJumpLandingHeadingReady')){Check ($null -eq (Method $name)) ('normal-path restriction retired '+$name)}
$kind=$m.GetTypes()|Where-Object Name -eq 'CapabilityFrontierKind';Check (@($kind.Fields|Where-Object Name -eq 'EntryAlignment').Count -eq 0) 'normal pre-native EntryAlignment clamp kind removed'
$port=$r.NestedTypes|Where-Object Name -eq 'CapabilityFrontierPort';$observe=$port.Methods|Where-Object Name -eq 'TryObserve';Check ((Calls $observe 'TryCaptureCapabilityEntryAlignment').Count -eq 0) 'normal native frontier no longer invokes entry freeze'
$probeRequest=$r.Methods|Where-Object{$_.Name -eq 'TryRequestCapabilityFullRouteRetry' -and $_.MethodSig.Params[0].FullName.EndsWith('/ProbeRuntime')}|Select-Object -First 1
Check ($probeRequest.MethodSig.Params.Count -eq 3 -and $probeRequest.MethodSig.Params[2].FullName -eq 'System.Boolean') 'failure request requires explicit failure argument'
Check ((Calls $probeRequest 'FailureAdmission').Count -eq 1) 'actual failure-only admission wired before request'
$cleanup=Method 'ForceReleaseTraversalProbe';Check ((Calls $cleanup 'TryRequestCapabilityFullRouteRetry').Count -eq 0) 'resource cleanup/cancellation cannot initiate retry'
$finish=Method 'FinishProbeWithoutReanchor';$classified=@(Calls $finish 'IsTraversalExecutionFailure');$offered=@(Calls $finish 'TryRequestCapabilityFullRouteRetry')
Check ($classified.Count -ge 1 -and $offered.Count -eq 1 -and $classified[0].Offset -lt $offered[0].Offset) 'unverified finish must classify execution failure before recovery'
$final=Method 'FinalizeUnifiedTraversalFailure';Check ((Calls $final 'TryRequestCapabilityFullRouteRetry').Count -eq 1) 'explicit terminal failure still enters complete retry'
$tick=Method 'TickCapabilityFullRouteRetry';Check ((Calls $tick 'ShouldRestartCapabilityMissedEntry').Count -eq 0) 'normal update has no geometry-only failure watcher'
Check ((Calls $tick 'IsCapabilityFullRouteReplay').Count -eq 1) 'only already-failed replay expiry may start recovery from update'
Check (@($tick.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldstr' -and $_.Operand -eq 'observedMissedEntry'}).Count -eq 0) 'normal geometry cannot manufacture retry reason'
$guard=$false;foreach($call in (Calls $tick 'IsCapabilityRouteRetryReturning')){
 $at=$tick.Body.Instructions.IndexOf($call);$ins=$tick.Body.Instructions
 if($at+3 -lt $ins.Count -and $ins[$at+1].OpCode.Name -in @('brtrue','brtrue.s') -and $ins[$at+2].OpCode.Name -eq 'ldc.i4.0' -and $ins[$at+3].OpCode.Name -eq 'ret'){$guard=$true}
}
Check $guard 'first normal pass returns false before recovery side effects until returning phase exists'
$all=@($m.GetTypes()|ForEach-Object{$_.Methods}|Where-Object HasBody);$callers=@($all|Where-Object{(Calls $_ 'TryRequestCapabilityFullRouteRetry').Count -gt 0}|ForEach-Object{$_.Name.String}|Sort-Object -Unique)
Check (($callers -join ',') -eq 'FinalizeOrEscalateTraversalFailure,FinalizeUnifiedTraversalFailure,FinishProbeWithoutReanchor,ShouldDeferCapabilityJumpProbe,TickCapabilityFailureSequence,TickCapabilityFullRouteRetry,TriggerNativeJump,TryArmLearningV431EquivalentLocalBarrierSelfRescue,TryBeginLearningV3NativeEntryCorrection,TryBeginLearningV450MissedJumpCurrentBodySelfRescue,TryDispatchCapabilitySequenceNative,TryHandleCapabilityRescueTerminal,TryRecoverCapabilityStalledJumpPreparation,TryRecoverPlayerValidatedTraversal,TryRecoverVaultClimbCrossingBeforeEgress,TryRequestCapabilityFullRouteRetry') 'finite failure/expiry caller surface has no normal mover/ingress/input callers'
$p=$m.GetTypes()|Where-Object Name -eq 'FollowerCapabilityRouteRetryPolicy';foreach($name in @('MissedEntry','JumpHeading','HoldEntryAlignment')){Check (@($p.Methods|Where-Object Name -eq $name).Count -eq 0) ('retired unused normal strategy removed '+$name)}
foreach($name in @('StageReached','StagePosition','ReturnDecision','PathProgress','HoldNeedsStop')){Check (@($p.Methods|Where-Object Name -eq $name).Count -eq 1) ('failure-only replay/return requirement retained '+$name)}
$h=[Security.Cryptography.SHA256]::Create();try{$sha=[BitConverter]::ToString($h.ComputeHash([IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $Dll).Path))).Replace('-','')}finally{$h.Dispose()}
@{passed=($script:errors.Count -eq 0);checks=$script:n;errors=@($script:errors);dllSha256=$sha;physicsSimulated=$false}|ConvertTo-Json -Depth 5|Set-Content -Encoding UTF8 -LiteralPath $Output
Write-Output "RC114 failure-only boundary: checks=$script:n failures=$($script:errors.Count)";$m.Dispose();if($script:errors.Count){$script:errors;exit 1}
