param([string]$Dll)
$ErrorActionPreference='Stop'
[Reflection.Assembly]::LoadFrom('D:/Games/SIT_DIR/mod or addon builds/dnSpy-net-win64/bin/dnlib.dll')|Out-Null
$canonical=(Resolve-Path -LiteralPath $Dll).Path;$root=Join-Path $PSScriptRoot 'Mutants';[IO.Directory]::CreateDirectory($root)|Out-Null;$results=@()
foreach($case in @('terminal_disconnected','strategy_cleanup_disconnected','lifecycle_guard_bypassed','verified_gate_bypassed','retry_never_admitted')){
 $m=[dnlib.DotNet.ModuleDefMD]::Load($canonical);$r=$m.GetTypes()|Where-Object FullName -eq 'BotTraversal.Core.FollowerTraversalTestRuntime';$dir=Join-Path $root $case;[IO.Directory]::CreateDirectory($dir)|Out-Null
 $method=$r.Methods|Where-Object Name -eq 'TryHandleCapabilityRescueTerminal'
 if($case -in @('terminal_disconnected','strategy_cleanup_disconnected')){
  if($case -eq 'strategy_cleanup_disconnected'){$method=$r.Methods|Where-Object Name -eq 'RetireCapabilityRescueCommitmentForRetry'}
  $method.Body=[dnlib.DotNet.Emit.CilBody]::new()
  if($case -eq 'terminal_disconnected'){$method.Body.Instructions.Add([dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ldc_I4_0))}
  $method.Body.Instructions.Add([dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ret))
 }elseif($case -eq 'verified_gate_bypassed'){
  $i=$method.Body.Instructions[0];if($i.OpCode.Name -ne 'ldarg.1'){throw 'verified gate setup'};$i.OpCode=[dnlib.DotNet.Emit.OpCodes]::Ldc_I4_0;$i.Operand=$null
 }else{
  $name=if($case -eq 'lifecycle_guard_bypassed'){'IsProbeLifecycleCurrent'}else{'TryRequestCapabilityFullRouteRetry'}
  $call=@($method.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'call' -and $_.Operand.Name -eq $name});if($call.Count -ne 1){throw 'fault setup requires unique call'}
  # Replace result after a genuine call, keeping valid evaluation stack and all argument effects.
  $idx=$method.Body.Instructions.IndexOf($call[0]);$method.Body.Instructions.Insert($idx+1,[dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Pop))
  $op=if($case -eq 'lifecycle_guard_bypassed'){[dnlib.DotNet.Emit.OpCodes]::Ldc_I4_1}else{[dnlib.DotNet.Emit.OpCodes]::Ldc_I4_0}
  $method.Body.Instructions.Insert($idx+2,[dnlib.DotNet.Emit.Instruction]::Create($op))
 }
 $target=Join-Path $dir 'BotTraversal.dll';$m.Write($target);$m.Dispose();$ErrorActionPreference='Continue'
 powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Test-RescueDispositionBoundary.ps1') -Dll $target -Output (Join-Path $dir 'il.json') *> (Join-Path $dir 'rejection.log');$status=$LASTEXITCODE
 $ErrorActionPreference='Stop';$log=Get-Content (Join-Path $dir 'rejection.log') -Raw
 if(!$status -or $log -notmatch 'failures='){throw "RC120 mutant escaped $case"}
 if($log -match 'InvalidProgramException|FileNotFoundException|BadImageFormatException|ParameterBindingException'){throw "invalid mutant setup $case"}
 $results+=@{id=$case;rejected=$true;actualBinaryModified=$true;compiledFromMutantSource=$false;oracle='actual DLL integration IL'};Write-Output "Rejected RC120 actual fault $case"
}
@{passed=$true;cases=$results;physicsSimulated=$false}|ConvertTo-Json -Depth 5|Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'mutation-result.json')
