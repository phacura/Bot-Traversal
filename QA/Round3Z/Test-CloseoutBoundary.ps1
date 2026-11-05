param([string]$Dll,[string]$Output=(Join-Path $PSScriptRoot 'il-result.json'))
$ErrorActionPreference='Stop'
function Sha([string]$Path){$a=[Security.Cryptography.SHA256]::Create();try{[BitConverter]::ToString($a.ComputeHash([IO.File]::ReadAllBytes([IO.Path]::GetFullPath($Path)))).Replace('-','')}finally{$a.Dispose()}}
[Reflection.Assembly]::LoadFrom('D:/Games/SIT_DIR/mod or addon builds/dnSpy-net-win64/bin/dnlib.dll')|Out-Null
$m=[dnlib.DotNet.ModuleDefMD]::Load((Resolve-Path -LiteralPath $Dll).Path);$r=$m.GetTypes()|Where-Object FullName -eq 'BotTraversal.Core.FollowerTraversalTestRuntime'
$script:checks=0;$script:errors=[Collections.Generic.List[string]]::new()
function Check($ok,$name){$script:checks++;if(!$ok){$script:errors.Add($name)}}
function Method($name){$r.Methods|Where-Object Name -eq $name|Select-Object -First 1}
function Calls($method,$name){@($method.Body.Instructions|Where-Object{$_.OpCode.Name -in @('call','callvirt') -and $_.Operand.Name -eq $name})}
function Before($method,$first,$second){$a=@(Calls $method $first);$b=@(Calls $method $second);return $a.Count -gt 0 -and $b.Count -gt 0 -and $a[0].Offset -lt $b[0].Offset}
function LocalSlot($instruction){
 if($instruction.OpCode.Name -match '^(ldloc|stloc)\.([0-3])$'){return [int]$Matches[2]}
 if($instruction.OpCode.Name -match '^(ldloc|stloc)(\.s)?$'){return [int]$instruction.Operand.Index};return -1
}
$neutral=Method 'TryQuiesceFollowerLocomotionForTeleport';$origin=$neutral.ParamDefs|Where-Object Name -eq 'issuedTeleportGeneration'
Check ($origin -and !$origin.IsOptional -and !$origin.HasConstant) 'teleport cleanup origin is mandatory in actual metadata'
$all=@($m.GetTypes()|ForEach-Object{$_.Methods}|Where-Object HasBody)
$callers=@($all|Where-Object{(Calls $_ 'TryQuiesceFollowerLocomotionForTeleport').Count -gt 0})
Check ((@($callers|ForEach-Object{[string]$_.Name}|Sort-Object) -join ',') -eq 'PrepareForManualRegroup,ResetFollowerMovementAuthorityForTeleport,TryHardRecoverFollowerLocomotionAuthority,TryTeleportAtOrigin') 'all four teleport cleanup callers are explicitly audited'
foreach($caller in $callers){
 $calls=@(Calls $caller 'TryQuiesceFollowerLocomotionForTeleport');Check ($calls.Count -eq 1) ('one neutral commit '+$caller.Name)
 $at=$caller.Body.Instructions.IndexOf($calls[0]);$load=$caller.Body.Instructions[$at-1];$slot=LocalSlot $load
 Check ($slot -ge 0 -and [string]$caller.Body.Variables[$slot].Type -eq 'System.Int64') ('origin is explicit long local, never default literal '+$caller.Name)
 if($caller.Name -eq 'PrepareForManualRegroup'){
  $issue=@(Calls $caller 'ResetFollowerMovementAuthorityForTeleport');$index=$caller.Body.Instructions.IndexOf($issue[0]);$issuedSlot=LocalSlot $caller.Body.Instructions[$index+1]
  Check ($issue.Count -eq 1 -and $issuedSlot -eq $slot) 'manual cleanup consumes the generation returned by its own reset'
 }
}
$idle=Method 'TryNeutralizeLearningV432StationaryVisualLocomotionResidue'
Check ((Calls $idle 'TryEnterCapabilityFollowRecoveryScope').Count -eq 1 -and (Calls $idle 'TryEnterBoundPhysicalMotion').Count -eq 0) 'stationary neutralization shares Native/mission/preemption gate'
Check ((Calls $idle 'Dispose').Count -ge 1) 'stationary neutralization disposes bounded scope'
$ingress=Method 'TryEnterV2CurrentIngressScope'
Check ((Calls $ingress 'TryValidateV2CurrentTypedStep').Count -eq 2) 'typed ingress revalidates its same step after issuance'
Check (Before $ingress 'TryEnterFollowPhysicalMotion' 'Dispose') 'typed ingress disposes refusal after original Follow issuer'
Check ((Calls $ingress 'Dispose').Count -eq 1) 'typed ingress cannot retain rejected scope'
$leaves=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'exact-scope-leaves.json') -Raw|ConvertFrom-Json
$backend=$m.GetTypes()|Where-Object FullName -eq 'BotTraversal.Core.FollowerControlBackend';$find=$backend.Methods|Where-Object Name -eq 'FindSite'|Select-Object -First 1
foreach($leaf in $leaves){
 $method=Method $leaf.method;$gates=@(Calls $method 'TryEnterCapabilityFollowRecoveryScope');$writes=@($method.Body.Instructions|Where-Object{$_.OpCode.Name -in @('call','callvirt') -and $_.Operand.DeclaringType.Name -eq 'FollowerControlBackend'})
 Check ($gates.Count -eq 1 -and $writes.Count -eq @($leaf.sites).Count -and @($writes|Where-Object{$_.Offset -le $gates[0].Offset}).Count -eq 0) ('typed gate precedes each leaf effect '+$leaf.method)
 Check ((Calls $method 'Dispose').Count -ge 1) ('leaf releases short scope '+$leaf.method)
 foreach($site in $leaf.sites){
  $literal=@($find.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldstr' -and $_.Operand -eq $site});$at=$find.Body.Instructions.IndexOf($literal[0]);$ctor=$null
  $branch=$find.Body.Instructions[$at+2];if($branch.OpCode.Name -in @('brtrue','brtrue.s')){$at=$find.Body.Instructions.IndexOf($branch.Operand)}
  for($n=$at;$n -lt [Math]::Min($at+8,$find.Body.Instructions.Count);$n++){if($find.Body.Instructions[$n].OpCode.Name -eq 'newobj' -and $find.Body.Instructions[$n].Operand.DeclaringType.Name -eq 'ControlSite'){$ctor=$n;break}}
  Check ($literal.Count -eq 1 -and $ctor -and $find.Body.Instructions[$ctor-1].OpCode.Name -eq 'ldc.i4.0') ('actual catalog requires ExactScope '+$site)
 }
}
# Original cross-capability gates remain wired; this is in addition to retained suites.
foreach($binding in @(
 @('TryEnterCapabilityRouteProducerScope','ObserveCapabilityRouteContinuation',2),
 @('TryEnterCapabilityRoutePhysicalScope','ObserveCapabilityRouteContinuation',2),
 @('TryEnterCapabilityFollowRecoveryScope','ObserveCapabilityFollowRecovery',2),
 @('TryEnterCapabilityFreshFollowScope','ObserveCapabilityFollowRecovery',1),
 @('TryMaintainCapabilityNativeInput','TryEnterBoundPhysicalMotion',1),
 @('CommitCapabilityNativePlayerInput','TryCommitNativePlayerMove',1))) {
 Check ((Calls (Method $binding[0]) $binding[1]).Count -eq $binding[2]) ('cross-capability retained binding '+$binding[0]+':'+$binding[1])
}
$policyTypes=@($m.GetTypes()|Where-Object{$_.Name -like 'FollowerCapability*Policy' -or $_.Name -in @('FollowerCapabilityDecisionRules','FollowerCapabilityHelperRules')})
foreach($type in $policyTypes){
 Check (@($type.Fields|Where-Object{$_.IsStatic -and !$_.IsLiteral}).Count -eq 0) ('capability policy has no owned static state '+$type.Name)
 $engineCalls=@($type.Methods|Where-Object HasBody|ForEach-Object{$_.Body.Instructions}|Where-Object{$_.OpCode.Name -in @('call','callvirt','newobj') -and ($_.Operand.DeclaringType.FullName -match '^(EFT\.|UnityEngine\.|System\.Reflection\.)' -or $_.Operand.DeclaringType.FullName -eq 'IAnimator')})
 Check ($engineCalls.Count -eq 0) ('capability policy has no engine/reflection effects '+$type.Name)
}
@{passed=($script:errors.Count -eq 0);checks=$script:checks;errors=@($script:errors);dllSha256=(Sha $Dll);scopeLeaves=$leaves.Count;strictSites=58;purePolicyTypes=$policyTypes.Count;physicsSimulated=$false}|ConvertTo-Json -Depth 5|Set-Content -Encoding UTF8 -LiteralPath $Output
Write-Output "Closeout IL: checks=$script:checks failures=$($script:errors.Count)";$m.Dispose();if($script:errors.Count){$script:errors;exit 1}
