param([string]$Dll,[string]$Output=(Join-Path $PSScriptRoot 'il-result.json'))
$ErrorActionPreference='Stop'
function Sha([string]$Path){$a=[Security.Cryptography.SHA256]::Create();try{[BitConverter]::ToString($a.ComputeHash([IO.File]::ReadAllBytes([IO.Path]::GetFullPath($Path)))).Replace('-','')}finally{$a.Dispose()}}
[Reflection.Assembly]::LoadFrom('D:/Games/SIT_DIR/mod or addon builds/dnSpy-net-win64/bin/dnlib.dll')|Out-Null
$m=[dnlib.DotNet.ModuleDefMD]::Load((Resolve-Path -LiteralPath $Dll).Path);$r=$m.GetTypes()|Where-Object FullName -eq 'BotTraversal.Core.FollowerTraversalTestRuntime'
function CallKey([object[]]$values){[string[]]$normalized=@($values);[Array]::Sort($normalized,[StringComparer]::Ordinal);return ($normalized -join "`n")}
$script:checks=0;$script:errors=[Collections.Generic.List[string]]::new()
function Check($ok,$name){$script:checks++;if(!$ok){$script:errors.Add($name)}}
function Method($name){$r.Methods|Where-Object Name -eq $name|Select-Object -First 1}
function Calls($method,$name){@($method.Body.Instructions|Where-Object{$_.OpCode.Name -in @('call','callvirt') -and $_.Operand.Name -eq $name})}
function Before($method,$first,$second){$a=@(Calls $method $first);$b=@(Calls $method $second);return $a.Count -gt 0 -and $b.Count -gt 0 -and $a[0].Offset -lt $b[0].Offset}
$capture=Method 'CaptureCapabilityFollowRecovery'
foreach($name in @('PeekExactGeneration','IsLiveBot','HasActiveTraversalProbe','IsActiveProbe','IsProbeLifecycleCurrent','TryGetActiveFollowerRouteMission','MatchLifecycleRecovery','TryPeekActiveCommand','IsTeleportTransactionActive','IsUsingMedical','IsHandsInteractionActiveForAuthority','TryGetImmediateSurvivalThreat','HasCombatMovementClaim')){Check ((Calls $capture $name).Count -ge 1) ('exact recovery observation '+$name)}
foreach($field in @('MovementWriterGeneration','LearningV375MovementGeneration','LearningV375TransactionActive','LearningV350TerminalRecoveryEligible')){Check (@($capture.Body.Instructions|Where-Object{$_.OpCode.Name -in @('ldfld','ldflda') -and $_.Operand.Name -eq $field}).Count -gt 0) ('existing origin evidence '+$field)}
$bound=Method 'TryEnterCapabilityFollowRecoveryScope';$observe=Method 'ObserveCapabilityFollowRecovery'
Check ((Calls $observe 'Evaluate').Count -eq 1 -and (Calls $observe 'CaptureCapabilityFollowRecovery').Count -eq 1) 'one typed recovery observation decision'
Check ((Calls $bound 'ObserveCapabilityFollowRecovery').Count -eq 2) 'recovery rechecks after original ticket issuance'
Check ((Calls $bound 'TryEnterBoundPhysicalMotion').Count -eq 1 -and (Before $bound 'ObserveCapabilityFollowRecovery' 'TryEnterBoundPhysicalMotion')) 'existing exact owner-generation gate after typed check'
Check ((Calls $bound 'Dispose').Count -ge 1) 'changed origin disposes issued scope'
$fresh=Method 'TryEnterCapabilityFreshFollowScope'
Check ((Calls $fresh 'EvaluateFresh').Count -eq 1 -and (Before $fresh 'EvaluateFresh' 'TryEnterFollowPhysicalMotion')) 'fresh intent preflight precedes sole existing Follow issuer'
Check ((Calls $fresh 'TryEnterFollowPhysicalMotion').Count -eq 1 -and (Before $fresh 'TryEnterFollowPhysicalMotion' 'ObserveCapabilityFollowRecovery')) 'real issued Follow ticket is checked after acquisition'
Check ((Calls $fresh 'get_IssuedGeneration').Count -eq 1 -and (Calls $fresh 'Dispose').Count -eq 1) 'fresh admission uses actual issued generation and disposes refusal'
foreach($name in @('TryRecoverBoundFollowerLocomotion','TryRepairStuckJumpLanding','TryHardRecoverStuckJumpLanding','TryQuiesceFollowerLocomotionForTeleport','TryApplyLearningV407CollisionActiveLocomotionPresentation')){
 $method=Method $name;$gate=@(Calls $method 'TryEnterCapabilityFollowRecoveryScope');$writes=@($method.Body.Instructions|Where-Object{$_.OpCode.Name -in @('call','callvirt') -and $_.Operand.DeclaringType.Name -eq 'FollowerControlBackend'})
 Check ($gate.Count -eq 1 -and $writes.Count -gt 0 -and @($writes|Where-Object{$_.Offset -le $gate[0].Offset}).Count -eq 0) ($name+' controls only after typed scope')
 Check ((Calls $method 'Dispose').Count -ge 1) ($name+' releases short scope')
}
foreach($name in @('TryCF38ReprimeLostFollowConsumerPath','TryCF45RepairStaleTerminalFollowConsumer')){
 $method=Method $name;Check ((Calls $method 'TryEnterCapabilityFollowRecoveryScope').Count -eq 1 -and (Calls $method 'TryEnterBoundPhysicalMotion').Count -eq 0) ($name+' shares bounded Follow path admission')
}
Check ((Calls (Method 'TryPrimeFreshFollowPathAfterAuthorityReset') 'TryEnterCapabilityFreshFollowScope').Count -eq 1) 'actual fresh Follow publisher uses shared acquisition'
Check ((Calls (Method 'TryRecoverGeneralFollowerLocomotion') 'TryRecoverBoundFollowerLocomotion').Count -eq 1) 'old three-argument API delegates to bounded core'
Check ((Calls (Method 'TryHardRecoverCapabilityProbeLocomotion') 'TryRecoverBoundFollowerLocomotion').Count -eq 1 -and (Calls (Method 'TryHardRecoverCapabilityProbeLocomotion') 'TryHardRecoverStuckJumpLanding').Count -eq 1) 'native terminal caller explicitly supplies its probe'
$neutral=Method 'TryQuiesceFollowerLocomotionForTeleport'
Check ($neutral.Parameters.Count -eq 4) 'teleport cleanup takes explicit issued origin'
$zeroCalls=@(Calls $neutral 'SetPlayerInputDirection');$allZero=$zeroCalls.Count -eq 2
foreach($call in $zeroCalls){$at=$neutral.Body.Instructions.IndexOf($call);$allZero=$allZero -and $neutral.Body.Instructions[$at-1].Operand.Name -eq 'get_zero'}
Check $allZero 'actual teleport cleanup publishes zero input only'
foreach($name in @('TryCrossFadeFollowerLocomotion','BotOwnerSetTargetMoveSpeed','CommitCapabilityNativePlayerInput')){Check ((Calls $neutral $name).Count -eq 0) ('neutral teleport excludes '+$name)}
$hard=Method 'TryHardRecoverFollowerLocomotionAuthority'
Check (Before $hard 'CanCommitScopedPhysicalMotion' 'TryQuiesceFollowerLocomotionForTeleport') 'teleport routing consumes an existing current scope'
Check (Before $hard 'TryQuiesceFollowerLocomotionForTeleport' 'TryRecoverGeneralFollowerLocomotion') 'teleport neutral branch precedes forward recovery'
$safe=$m.GetTypes()|Where-Object Name -eq 'FollowerSafeTeleport'|Select-Object -First 1
$relocate=$safe.Methods|Where-Object Name -eq 'TryTeleportAtOrigin'|Select-Object -First 1
Check ((Calls $relocate 'TryQuiesceFollowerLocomotionForTeleport').Count -eq 1) 'safe teleport passes to explicit-origin cleanup'
$all=@($m.GetTypes()|ForEach-Object{$_.Methods}|Where-Object HasBody)
$crossCallers=@($all|Where-Object{(Calls $_ 'TryCrossFadeFollowerLocomotion').Count -gt 0}|ForEach-Object{[string]$_.Name}|Sort-Object)
Check (($crossCallers -join ',') -eq 'TryApplyLearningV407CollisionActiveLocomotionPresentation,TryHardRecoverStuckJumpLanding,TryRecoverBoundFollowerLocomotion,TryRepairStuckJumpLanding') 'all four legacy animator repair references are inside typed-scope leaves'
$unchanged=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'il-method-baseline.json') -Raw|ConvertFrom-Json
foreach($entry in $unchanged.methods){$method=Method $entry.name;$calls=@($method.Body.Instructions|Where-Object{$_.OpCode.Name -in @('call','callvirt','newobj') -and !$_.Operand.Name.String.StartsWith('<') -and !$_.Operand.DeclaringType.Name.String.StartsWith('<')}|ForEach-Object{[string]$_.Operand.FullName}|Sort-Object);Check ((CallKey $calls) -ceq (CallKey @($entry.calls))) ('unchanged moved calls '+$entry.name)}
$moved=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'moved-methods.json') -Raw|ConvertFrom-Json
foreach($entry in $moved){Check (@($r.Methods|Where-Object Name -eq $entry.name).Count -eq 1) ('single migrated API '+$entry.name)}
Check (@($all|ForEach-Object{Calls $_ 'TryCommitNativePlayerMove'}).Count -eq 1) 'one original native Player.Move gateway'
Check (@($all|ForEach-Object{$_.Body.Instructions}|Where-Object{$_.OpCode.Name -in @('call','callvirt') -and $_.Operand.FullName -eq 'System.Void IAnimator::Play(System.Int32,System.Int32,System.Single)'}).Count -eq 0) 'no normal animation playback writer restored'
@{passed=($script:errors.Count -eq 0);checks=$script:checks;errors=@($script:errors);dllSha256=(Sha $Dll);physicsSimulated=$false}|ConvertTo-Json -Depth 5|Set-Content -Encoding UTF8 -LiteralPath $Output
Write-Output "Follow/recovery IL: checks=$script:checks failures=$($script:errors.Count)";$m.Dispose();if($script:errors.Count){$script:errors;exit 1}
