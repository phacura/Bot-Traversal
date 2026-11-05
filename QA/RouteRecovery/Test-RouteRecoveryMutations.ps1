param([string]$Dll)
$ErrorActionPreference='Stop'
[Reflection.Assembly]::LoadFrom('D:/Games/SIT_DIR/mod or addon builds/dnSpy-net-win64/bin/dnlib.dll')|Out-Null
$canonical=(Resolve-Path -LiteralPath $Dll).Path;$root=Join-Path $PSScriptRoot 'Mutants';[IO.Directory]::CreateDirectory($root)|Out-Null;$results=@()
foreach($case in @('sideways_borrow_allowed','return_segment_unbounded','missed_jump_no_return','return_drive_disconnected','terminal_failure_skips_root')){
 $m=[dnlib.DotNet.ModuleDefMD]::Load($canonical);$r=$m.GetTypes()|Where-Object FullName -eq 'BotTraversal.Core.FollowerTraversalTestRuntime';$dir=Join-Path $root $case;[IO.Directory]::CreateDirectory($dir)|Out-Null
 if($case -in @('sideways_borrow_allowed','return_segment_unbounded')){
  $name=if($case -eq 'sideways_borrow_allowed'){'BorrowHeading'}else{'ReturnSegment'}
  $method=($m.GetTypes()|Where-Object Name -eq 'FollowerCapabilityRouteRetryPolicy').Methods|Where-Object Name -eq $name
  $method.Body=[dnlib.DotNet.Emit.CilBody]::new();$method.Body.Instructions.Add([dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ldc_I4_1));$method.Body.Instructions.Add([dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ret))
 }elseif($case -eq 'missed_jump_no_return'){
  $method=$r.Methods|Where-Object Name -eq 'TryBeginLearningV450MissedJumpCurrentBodySelfRescue'
  $method.Body=[dnlib.DotNet.Emit.CilBody]::new();$method.Body.Instructions.Add([dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ldc_I4_0));$method.Body.Instructions.Add([dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ret))
 }elseif($case -eq 'terminal_failure_skips_root'){
  $method=$r.Methods|Where-Object Name -eq 'FinalizeOrEscalateTraversalFailure'
  $call=$method.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'call' -and $_.Operand.Name -eq 'TryRequestCapabilityFullRouteRetry'}|Select-Object -First 1
  if(!$call){throw 'missing terminal hook to mutate'}
  $at=$method.Body.Instructions.IndexOf($call);$flag=$method.Body.Instructions[$at-1]
  if(!$flag.IsLdcI4() -or $flag.GetLdcI4Value() -ne 1){throw 'unexpected terminal flag layout'}
  $flag.OpCode=[dnlib.DotNet.Emit.OpCodes]::Ldc_I4_0
  # The valid actual terminal call now passes false, so FailureAdmission refuses it.
 }else{
  $method=$r.Methods|Where-Object Name -eq 'TickCapabilityFullRouteRetry';$replacement=$r.Methods|Where-Object Name -eq 'TryBuildCapabilityReturnCorridor'
  $call=$method.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'call' -and $_.Operand.Name -eq 'TryDriveCapabilityReturnCorridor'}|Select-Object -First 1
  if(!$call){throw 'missing drive call to mutate'};$call.Operand=$replacement
 }
 $target=Join-Path $dir 'BotTraversal.dll';$m.Write($target);$m.Dispose();$ErrorActionPreference='Continue'
 if($case -in @('sideways_borrow_allowed','return_segment_unbounded')){
  $test=Join-Path $dir 'test';[IO.Directory]::CreateDirectory($test)|Out-Null
  Get-ChildItem (Join-Path $PSScriptRoot 'Tests/bin/Release/net10.0') -File|ForEach-Object{Copy-Item -LiteralPath $_.FullName -Destination $test -Force}
  Copy-Item -LiteralPath $target -Destination (Join-Path $test 'BotTraversal.dll') -Force
  dotnet (Join-Path $test 'BotTraversal.Lifecycle.Tests.dll') (Join-Path $dir 'behavior.json') *> (Join-Path $dir 'rejection.log');$status=$LASTEXITCODE;$oracle='actual candidate scalar and lifecycle behavior'
 }else{
  powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Test-RouteRecoveryBoundary.ps1') -Dll $target -Output (Join-Path $dir 'il.json') *> (Join-Path $dir 'rejection.log');$status=$LASTEXITCODE;$oracle='actual candidate integration IL'
 }
 $ErrorActionPreference='Stop';$log=Get-Content (Join-Path $dir 'rejection.log') -Raw
 if(!$status -or $log -notmatch 'Unhandled exception|failures='){throw "new mutant escaped $case"}
 if($log -match 'InvalidProgramException|FileNotFoundException|BadImageFormatException|ParameterBindingException'){throw "invalid mutant setup $case"}
 $results+=@{id=$case;rejected=$true;actualBinaryModified=$true;compiledFromMutantSource=$false;oracle=$oracle};Write-Output "Rejected RC115 actual fault $case"
}
@{passed=$true;cases=$results;physicsSimulated=$false}|ConvertTo-Json -Depth 5|Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'mutation-result.json')
