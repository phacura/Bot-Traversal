param([string]$Dll)
$ErrorActionPreference='Stop'
[Reflection.Assembly]::LoadFrom('D:/Games/SIT_DIR/mod or addon builds/dnSpy-net-win64/bin/dnlib.dll')|Out-Null
$canonical=(Resolve-Path -LiteralPath $Dll).Path;$root=Join-Path $PSScriptRoot 'Mutants';[IO.Directory]::CreateDirectory($root)|Out-Null;$results=@()
foreach($case in @('cold_start_disabled','reused_trial_allowed','contact_wall_guard_removed','vault_centering_removed','contact_dispatch_disconnected','vault_facing_disconnected')){
 $m=[dnlib.DotNet.ModuleDefMD]::Load($canonical);$r=$m.GetTypes()|Where-Object FullName -eq 'BotTraversal.Core.FollowerTraversalTestRuntime';$dir=Join-Path $root $case;[IO.Directory]::CreateDirectory($dir)|Out-Null
 if($case -notin @('contact_dispatch_disconnected','vault_facing_disconnected')){
  $name=switch($case){'cold_start_disabled'{'ColdRunTrial'}'reused_trial_allowed'{'ColdRunTrial'}'contact_wall_guard_removed'{'JumpContact'}'vault_centering_removed'{'VaultSideStep'}}
  $method=($m.GetTypes()|Where-Object Name -eq 'FollowerCapabilityApproachPolicy').Methods|Where-Object Name -eq $name
  $method.Body=[dnlib.DotNet.Emit.CilBody]::new()
  $op=switch($case){'vault_centering_removed'{[dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ldc_R4,[single]0)}'cold_start_disabled'{[dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ldc_I4_0)}default{[dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ldc_I4_1)}}
  $method.Body.Instructions.Add($op);$method.Body.Instructions.Add([dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ret))
 }else{
  $name=if($case -eq 'contact_dispatch_disconnected'){'TryObserveCapabilityJumpContactWindow'}else{'TryGetCapabilityVaultIngressFacing'}
  $method=$r.Methods|Where-Object Name -eq $name;$method.Body=[dnlib.DotNet.Emit.CilBody]::new()
  $method.Body.Instructions.Add([dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ldc_I4_0));$method.Body.Instructions.Add([dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ret))
 }
 $target=Join-Path $dir 'BotTraversal.dll';$m.Write($target);$m.Dispose();$ErrorActionPreference='Continue'
 if($case -notin @('contact_dispatch_disconnected','vault_facing_disconnected')){
  $test=Join-Path $dir 'test';[IO.Directory]::CreateDirectory($test)|Out-Null
  Get-ChildItem (Join-Path $PSScriptRoot 'Tests/bin/Release/net10.0') -File|ForEach-Object{Copy-Item -LiteralPath $_.FullName -Destination $test -Force}
  Copy-Item -LiteralPath $target -Destination (Join-Path $test 'BotTraversal.dll') -Force
  dotnet (Join-Path $test 'BotTraversal.Lifecycle.Tests.dll') (Join-Path $dir 'behavior.json') *> (Join-Path $dir 'rejection.log');$status=$LASTEXITCODE;$oracle='actual DLL pure behavior'
 }else{
  powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Test-PassageAdmissionBoundary.ps1') -Dll $target -Output (Join-Path $dir 'il.json') *> (Join-Path $dir 'rejection.log');$status=$LASTEXITCODE;$oracle='actual DLL integration IL'
 }
 $ErrorActionPreference='Stop';$log=Get-Content (Join-Path $dir 'rejection.log') -Raw
 if(!$status -or $log -notmatch 'Unhandled exception|failures='){throw "RC117 mutant escaped $case"}
 if($log -match 'InvalidProgramException|FileNotFoundException|BadImageFormatException|ParameterBindingException'){throw "invalid mutant setup $case"}
 $results+=@{id=$case;rejected=$true;actualBinaryModified=$true;compiledFromMutantSource=$false;oracle=$oracle};Write-Output "Rejected RC117 actual fault $case"
}
@{passed=$true;cases=$results;physicsSimulated=$false}|ConvertTo-Json -Depth 5|Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'mutation-result.json')
