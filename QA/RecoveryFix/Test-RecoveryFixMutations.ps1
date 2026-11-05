param([string]$Dll)
$ErrorActionPreference='Stop'
[Reflection.Assembly]::LoadFrom('D:/Games/SIT_DIR/mod or addon builds/dnSpy-net-win64/bin/dnlib.dll')|Out-Null
$canonical=(Resolve-Path -LiteralPath $Dll).Path;$root=Join-Path $PSScriptRoot 'Mutants';[IO.Directory]::CreateDirectory($root)|Out-Null;$results=@()
foreach($case in @('healthy_return_forced_repath','hold_always_hard_stops','path_budget_always_accepts','manual_reset_retirement_removed','native_hold_adapter_disconnected','recovery_skips_observations')){
 $m=[dnlib.DotNet.ModuleDefMD]::Load($canonical);$r=$m.GetTypes()|Where-Object FullName -eq 'BotTraversal.Core.FollowerTraversalTestRuntime';$p=$m.GetTypes()|Where-Object Name -eq 'FollowerCapabilityRouteRetryPolicy';$dir=Join-Path $root $case;[IO.Directory]::CreateDirectory($dir)|Out-Null
 if($case -in @('healthy_return_forced_repath','hold_always_hard_stops','path_budget_always_accepts','native_hold_adapter_disconnected')){
  $type=if($case -eq 'native_hold_adapter_disconnected'){$r}else{$p};$name=switch($case){'healthy_return_forced_repath'{'ReturnDecision'};'hold_always_hard_stops'{'HoldNeedsStop'};'path_budget_always_accepts'{'PathWithinBudget'};default{'TryMaintainCapabilityRouteRetryHold'}}
  $method=$type.Methods|Where-Object Name -eq $name;$method.Body=[dnlib.DotNet.Emit.CilBody]::new();$code=switch($case){'healthy_return_forced_repath'{[dnlib.DotNet.Emit.OpCodes]::Ldc_I4_2};'native_hold_adapter_disconnected'{[dnlib.DotNet.Emit.OpCodes]::Ldc_I4_0};default{[dnlib.DotNet.Emit.OpCodes]::Ldc_I4_1}}
  $method.Body.Instructions.Add([dnlib.DotNet.Emit.Instruction]::Create($code));$method.Body.Instructions.Add([dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ret))
 }elseif($case -eq 'entry_witness_disconnected'){
  $method=$r.Methods|Where-Object Name -eq 'ShouldRestartCapabilityMissedEntry';$ins=$method.Body.Instructions;$field=@($ins|Where-Object{$_.OpCode.Name -eq 'ldfld' -and $_.Operand.Name -eq 'LearningV32NativeEntryAcquired'});if($field.Count -ne 1){throw 'entry witness shape changed'};$at=$ins.IndexOf($field[0]);$field[0].OpCode=[dnlib.DotNet.Emit.OpCodes]::Pop;$field[0].Operand=$null;$ins.Insert($at+1,[dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ldc_I4_1));$method.Body.SimplifyBranches();$method.Body.OptimizeBranches()
 }elseif($case -eq 'manual_reset_retirement_removed'){
  $method=$r.Methods|Where-Object Name -eq 'ResetFollowerMovementAuthorityForTeleport';$ins=$method.Body.Instructions;$call=@($ins|Where-Object{$_.OpCode.Name -eq 'callvirt' -and $_.Operand.Name -eq 'Retire'});if($call.Count -ne 1){throw 'retirement shape changed'};$at=$ins.IndexOf($call[0]);$call[0].OpCode=[dnlib.DotNet.Emit.OpCodes]::Pop;$call[0].Operand=$null;$ins.Insert($at+1,[dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Pop));$method.Body.SimplifyBranches();$method.Body.OptimizeBranches()
 }else{
  $method=$r.Methods|Where-Object Name -eq 'OnBotOwnerUpdate';$ins=$method.Body.Instructions;$call=$ins|Where-Object{$_.OpCode.Name -eq 'call' -and $_.Operand.Name -eq 'TickCapabilityFullRouteRetry'}|Select-Object -First 1;$at=$ins.IndexOf($call);$branch=$ins[$at+1];if($branch.OpCode.Name -notin @('brtrue','brtrue.s')){throw 'recovery branch changed'};$exit=$ins|Where-Object{$_.OpCode.Name -in @('leave','leave.s')}|Select-Object -First 1;$branch.Operand=$exit;$method.Body.SimplifyBranches();$method.Body.OptimizeBranches()
 }
 $target=Join-Path $dir 'BotTraversal.dll';$m.Write($target);$m.Dispose();$ErrorActionPreference='Continue'
 if($case -in @('healthy_return_forced_repath','hold_always_hard_stops','path_budget_always_accepts')){
  $test=Join-Path $dir 'test';[IO.Directory]::CreateDirectory($test)|Out-Null;Get-ChildItem (Join-Path $PSScriptRoot 'Tests/bin/Release/net10.0') -File|ForEach-Object{Copy-Item -LiteralPath $_.FullName -Destination $test -Force};Copy-Item -LiteralPath $target -Destination (Join-Path $test 'BotTraversal.dll') -Force
  [IO.Directory]::CreateDirectory((Join-Path $dir 'Evidence'))|Out-Null;Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Evidence/confirmed-evidence.json') -Destination (Join-Path $dir 'Evidence/confirmed-evidence.json') -Force
  dotnet (Join-Path $test 'BotTraversal.Lifecycle.Tests.dll') (Join-Path $dir 'behavior.json') *> (Join-Path $dir 'rejection.log');$status=$LASTEXITCODE;$oracle='actual continuous policy/lifecycle tests'
 }else{
  powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Test-RecoveryFixBoundary.ps1') -Dll $target -Output (Join-Path $dir 'il.json') *> (Join-Path $dir 'rejection.log');$status=$LASTEXITCODE;$oracle='actual native adapter/reset/update IL'
 }
 $ErrorActionPreference='Stop';$log=Get-Content (Join-Path $dir 'rejection.log') -Raw
 if(!$status -or $log -notmatch 'Unhandled exception|failures='){throw "regression mutant escaped $case"}
 if($log -match 'InvalidProgramException|FileNotFoundException|BadImageFormatException|ParameterBindingException'){throw "invalid mutant/test setup $case"}
 $results+=@{id=$case;rejected=$true;actualBinaryModified=$true;compiledFromMutantSource=$false;oracle=$oracle;exitCode=$status};Write-Output "Rejected RC113 actual binary fault $case"
}
@{passed=$true;cases=$results;physicsSimulated=$false}|ConvertTo-Json -Depth 5|Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'mutation-result.json')
