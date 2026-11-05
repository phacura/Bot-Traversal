param([string]$Dll,[string]$Output=(Join-Path $PSScriptRoot 'recovery-il-new.json'))
$ErrorActionPreference='Stop'
[Reflection.Assembly]::LoadFrom('D:/Games/SIT_DIR/mod or addon builds/dnSpy-net-win64/bin/dnlib.dll')|Out-Null
$m=[dnlib.DotNet.ModuleDefMD]::Load((Resolve-Path -LiteralPath $Dll).Path);$r=$m.GetTypes()|Where-Object FullName -eq 'BotTraversal.Core.FollowerTraversalTestRuntime'
$script:checks=0;$script:errors=[Collections.Generic.List[string]]::new()
function Check($ok,$label){$script:checks++;if(!$ok){$script:errors.Add($label)}}
function Method($name){$r.Methods|Where-Object Name -eq $name|Select-Object -First 1}
function Calls($method,$name){@($method.Body.Instructions|Where-Object{$_.OpCode.Name -in @('call','callvirt','newobj') -and $_.Operand.Name -eq $name})}
function Before($method,$first,$second){$a=@(Calls $method $first);$b=@(Calls $method $second);return $a.Count -gt 0 -and $b.Count -gt 0 -and $a[0].Offset -lt $b[0].Offset}
function Fields($method,$name){@($method.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldfld' -and $_.Operand.Name -eq $name})}
$tick=Method 'TickCapabilityFullRouteRetry'
Check (@($tick.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldstr' -and $_.Operand -eq 'fellBelowUnstartedBarrierEntry'}).Count -eq 0) 'RC112 normal unstarted low-ground false failure removed'
$miss=Method 'ShouldRestartCapabilityMissedEntry';Check ($null -eq $miss) 'proactive normal-path missed-entry detector retired'
foreach($name in @('ReturnDecision','PathProgress','PathWithinBudget','TryObserveCapabilityReturnRemaining')){Check ((Calls $tick $name).Count -ge 1) ('actual return adapter uses shared decision '+$name)}
$request=$r.Methods|Where-Object{$_.Name -eq 'TryRequestCapabilityFullRouteRetry' -and $_.MethodSig.Params.Count -eq 3 -and $_.MethodSig.Params[0].FullName -eq 'EFT.BotOwner'}|Select-Object -First 1
Check ((Calls $request 'ReturnBudget').Count -eq 1) 'return budget captured once at failure request'
$all=@($m.GetTypes()|ForEach-Object{$_.Methods}|Where-Object HasBody)
Check (@($all|ForEach-Object{Calls $_ 'ReturnBudget'}).Count -eq 1) 'no return-budget widening during drift or repath'
$observe=Method 'TryObserveCapabilityReturnRemaining';Check ($null -ne $observe) 'accepted native resource observer exists'
if($observe){
 foreach($name in @('ReturnPath','ReturnGeneration')){Check ((Fields $observe $name).Count -ge 1) ('observer exact receipt '+$name)}
 foreach($name in @('PeekExactGeneration','get_TargetPoint','GetWayPoints','get_HasPathAndNoComplete')){Check ((Calls $observe $name).Count -ge 1) ('native current path verification '+$name)}
 $identityBranch=$false;foreach($field in (Fields $observe 'ReturnPath')){$i=$observe.Body.Instructions.IndexOf($field);if($observe.Body.Instructions[$i+1].OpCode.Name -in @('bne.un','bne.un.s')){$identityBranch=$true}}
 Check $identityBranch 'actual inequality branch rejects a foreign native path resource'
 Check ((Calls $observe 'get_PlayerRemainingDist').Count -eq 0) 'remaining route calculated from current native corners rather than unverified cached float'
}
$hold=Method 'HoldCapabilityRouteRetry';Check ((Calls $hold 'HoldNeedsStop').Count -eq 1) 'hard-stop is conditional on held epoch/resource identity'
Check ((Fields $hold 'HoldPath').Count -ge 1) 'same stopped native resource is not stopped every retry interval'
$zero=Method 'CommitCapabilityRouteRetryZeroInput';Check ($null -ne $zero) 'one scoped zero-intent adapter exists'
if($zero){
 foreach($name in @('SetPlayerInputDirection','SetMovementContextMovementDirection','CommitCapabilityNativePlayerInput')){Check ((Calls $zero $name).Count -eq 1) ('clear actual held input via existing gateway '+$name)}
 Check ((Calls $zero 'get_zero').Count -eq 3) 'all held input is literal zero'
 foreach($name in @('BotMoverStop','BotOwnerStopMove','BotOwnerGoToPoint','ChangeState','TryPlayerTeleport')){Check ((Calls $zero $name).Count -eq 0) ('native zero maintenance cannot hard-stop/repath/teleport '+$name)}
}
$maintain=Method 'TryMaintainCapabilityRouteRetryHold';Check ($null -ne $maintain) 'existing native consumer supports scoped hold'
if($maintain){
 foreach($name in @('TryEnterBoundPhysicalMotion','Dispose','IsCapabilityRouteRetrySafe','HasActiveTraversalProbe','CommitCapabilityRouteRetryZeroInput')){Check ((Calls $maintain $name).Count -ge 1) ('held consumer revalidates '+$name)}
 Check (Before $maintain 'TryEnterBoundPhysicalMotion' 'CommitCapabilityRouteRetryZeroInput') 'held zero input needs its exact current Follow scope'
 Check ((Fields $maintain 'HoldGeneration').Count -ge 2) 'hold generation checked before and after issuance'
}
Check ((Calls (Method 'TryMaintainCapabilityNativeInput') 'TryMaintainCapabilityRouteRetryHold').Count -eq 1) 'existing native consumer actually dispatches zero-hold adapter'
$reset=Method 'ResetFollowerMovementAuthorityForTeleport';Check (Before $reset 'Retire' 'RemoveLifecycleMission') 'explicit teleport reset retires old debt before protected removal'
Check (@($reset.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldstr' -and $_.Operand -eq 'capabilityFullRouteRetryCancelled'}).Count -eq 1) 'explicit cancellation has actual diagnostic event'
$on=Method 'OnBotOwnerUpdate';Check (Before $on 'TickCapabilityFullRouteRetry' 'RecordFollowerSnapshot') 'recovery still reaches common body snapshots'
Check (Before $on 'TickCapabilityFullRouteRetry' 'UpdateWorldSafetyWatchdog') 'common world safety remains after recovery dispatch'
$call=@(Calls $on 'TickCapabilityFullRouteRetry');$at=$on.Body.Instructions.IndexOf($call[0]);$branch=$on.Body.Instructions[$at+1];$physical=@(Calls $on 'UpdatePostTraversalIntegrityWatch');$safety=@(Calls $on 'UpdateWorldSafetyWatchdog')
Check ($branch.OpCode.Name -in @('brtrue','brtrue.s') -and $branch.Operand.Offset -gt $physical[0].Offset -and $branch.Operand.Offset -lt $safety[0].Offset) 'consumed recovery skips competing producers but reaches common safety and snapshots'
$find=($m.GetTypes()|Where-Object Name -eq 'FollowerControlBackend').Methods|Where-Object Name -eq 'FindSite';$sites=@($find.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldstr' -and $_.Operand -like 'R113-*'})
Check ($sites.Count -eq 2) 'only two new scoped zero-input field sites'
foreach($site in $sites){$ins=$find.Body.Instructions;$i=$ins.IndexOf($site);$dest=$ins.IndexOf($ins[$i+2].Operand);Check ($ins[$dest+1].GetLdcI4Value() -eq 0) ('new held input site exact scope '+$site.Operand)}
$algo=[Security.Cryptography.SHA256]::Create();try{$hash=[BitConverter]::ToString($algo.ComputeHash([IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $Dll).Path))).Replace('-','')}finally{$algo.Dispose()}
@{passed=($script:errors.Count -eq 0);checks=$script:checks;errors=@($script:errors);dllSha256=$hash;physicsSimulated=$false}|ConvertTo-Json -Depth 5|Set-Content -Encoding UTF8 -LiteralPath $Output
Write-Output "RC113 recovery integration IL: checks=$script:checks failures=$($script:errors.Count)";$m.Dispose();if($script:errors.Count){$script:errors;exit 1}
