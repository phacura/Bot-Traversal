param([string]$Dll)
$ErrorActionPreference='Stop'
[Reflection.Assembly]::LoadFrom('D:/Games/SIT_DIR/mod or addon builds/dnSpy-net-win64/bin/dnlib.dll')|Out-Null
$canonical=(Resolve-Path -LiteralPath $Dll).Path;$root=Join-Path $PSScriptRoot 'Mutants';[IO.Directory]::CreateDirectory($root)|Out-Null;$results=@()
foreach($case in @('vault_forward_lead_removed','sprint_floor_bypassed','low_step_wall_allowed','return_walk_latch_removed','jump_guard_inverted','low_step_site_disconnected')){
 $m=[dnlib.DotNet.ModuleDefMD]::Load($canonical);$r=$m.GetTypes()|Where-Object FullName -eq 'BotTraversal.Core.FollowerTraversalTestRuntime';$dir=Join-Path $root $case;[IO.Directory]::CreateDirectory($dir)|Out-Null
 if($case -in @('vault_forward_lead_removed','sprint_floor_bypassed','low_step_wall_allowed','return_walk_latch_removed')){
  $typeName=if($case -eq 'return_walk_latch_removed'){'FollowerCapabilityRouteRetryPolicy'}else{'FollowerCapabilityApproachPolicy'}
  $name=switch($case){'vault_forward_lead_removed'{'VaultLead'}'sprint_floor_bypassed'{'SprintFloor'}'low_step_wall_allowed'{'LowStep'}'return_walk_latch_removed'{'PrecisionReturn'}}
  $method=($m.GetTypes()|Where-Object Name -eq $typeName).Methods|Where-Object Name -eq $name
  $method.Body=[dnlib.DotNet.Emit.CilBody]::new()
  $op=switch($case){'vault_forward_lead_removed'{[dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ldc_R4,[single]0)}'return_walk_latch_removed'{[dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ldc_I4_0)}default{[dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ldc_I4_1)}}
  $method.Body.Instructions.Add($op);$method.Body.Instructions.Add([dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ret))
 }elseif($case -eq 'jump_guard_inverted'){
  $method=$r.Methods|Where-Object Name -eq 'TriggerNativeJump'
  $call=$method.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'call' -and $_.Operand.Name -eq 'IsCapabilityJumpTriggerReady'}|Select-Object -First 1
  $at=$method.Body.Instructions.IndexOf($call);$branch=$method.Body.Instructions[$at+1]
  if($branch.OpCode.Name -notin @('brtrue','brtrue.s')){throw 'unexpected final guard layout'}
  $branch.OpCode=if($branch.OpCode.Name -eq 'brtrue'){[dnlib.DotNet.Emit.OpCodes]::Brfalse}else{[dnlib.DotNet.Emit.OpCodes]::Brfalse_S}
 }else{
  $method=$r.Methods|Where-Object Name -eq 'TryDriveCapabilityGenericLowStep'
  $site=$method.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldstr' -and $_.Operand -eq 'R116-116A00000006'}|Select-Object -First 1
  if(!$site){throw 'missing low step backend site'};$site.Operand='RC116-unknown-refused-site'
 }
 $target=Join-Path $dir 'BotTraversal.dll';$m.Write($target);$m.Dispose();$ErrorActionPreference='Continue'
 if($case -notin @('jump_guard_inverted','low_step_site_disconnected')){
  $test=Join-Path $dir 'test';[IO.Directory]::CreateDirectory($test)|Out-Null
  Get-ChildItem (Join-Path $PSScriptRoot 'Tests/bin/Release/net10.0') -File|ForEach-Object{Copy-Item -LiteralPath $_.FullName -Destination $test -Force}
  Copy-Item -LiteralPath $target -Destination (Join-Path $test 'BotTraversal.dll') -Force
  dotnet (Join-Path $test 'BotTraversal.Lifecycle.Tests.dll') (Join-Path $dir 'behavior.json') *> (Join-Path $dir 'rejection.log');$status=$LASTEXITCODE;$oracle='actual DLL pure behavior'
 }else{
  powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Test-IngressContractBoundary.ps1') -Dll $target -Output (Join-Path $dir 'il.json') *> (Join-Path $dir 'rejection.log');$status=$LASTEXITCODE;$oracle='actual DLL integration IL'
 }
 $ErrorActionPreference='Stop';$log=Get-Content (Join-Path $dir 'rejection.log') -Raw
 if(!$status -or $log -notmatch 'Unhandled exception|failures='){throw "RC116 mutant escaped $case"}
 if($log -match 'InvalidProgramException|FileNotFoundException|BadImageFormatException|ParameterBindingException'){throw "invalid mutant setup $case"}
 $results+=@{id=$case;rejected=$true;actualBinaryModified=$true;compiledFromMutantSource=$false;oracle=$oracle};Write-Output "Rejected RC116 actual fault $case"
}
@{passed=$true;cases=$results;physicsSimulated=$false}|ConvertTo-Json -Depth 5|Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'mutation-result.json')
