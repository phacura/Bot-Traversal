param([string]$Dll)
$ErrorActionPreference='Stop'
[Reflection.Assembly]::LoadFrom('D:/Games/SIT_DIR/mod or addon builds/dnSpy-net-win64/bin/dnlib.dll')|Out-Null
$canonical=(Resolve-Path -LiteralPath $Dll).Path;$results=@();$root=Join-Path $PSScriptRoot 'Mutants';[IO.Directory]::CreateDirectory($root)|Out-Null
foreach($case in @('rear_pose_always_accepts','retry_phase_disconnected','return_update_disconnected','return_site_legacy_scope')){
 $m=[dnlib.DotNet.ModuleDefMD]::Load($canonical);$dir=Join-Path $root $case;[IO.Directory]::CreateDirectory($dir)|Out-Null
 $policy=$m.GetTypes()|Where-Object Name -eq 'FollowerCapabilityRouteRetryPolicy';$runtime=$m.GetTypes()|Where-Object FullName -eq 'BotTraversal.Core.FollowerTraversalTestRuntime'
 if($case -eq 'return_site_legacy_scope'){
  $type=$m.GetTypes()|Where-Object Name -eq 'FollowerControlBackend';$method=$type.Methods|Where-Object Name -eq 'FindSite';$ins=$method.Body.Instructions;$literal=$ins|Where-Object{$_.OpCode.Name -eq 'ldstr' -and $_.Operand -eq 'R112-112A00000007'}|Select-Object -First 1;$at=$ins.IndexOf($literal);$at=$ins.IndexOf($ins[$at+2].Operand);$ins[$at+1].OpCode=[dnlib.DotNet.Emit.OpCodes]::Ldc_I4_1
 }elseif($case -eq 'jump_heading_guard_disconnected'){
  $method=$runtime.Methods|Where-Object Name -eq 'TriggerNativeJump';$ins=$method.Body.Instructions;$call=$ins|Where-Object{$_.OpCode.Name -eq 'call' -and $_.Operand.Name -eq 'IsCapabilityJumpLandingHeadingReady'}|Select-Object -First 1;$at=$ins.IndexOf($call);$call.OpCode=[dnlib.DotNet.Emit.OpCodes]::Pop;$call.Operand=$null;$ins.Insert($at+1,[dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ldc_I4_1));$method.Body.SimplifyBranches();$method.Body.OptimizeBranches()
 }else{
  $type=$policy;$name=switch($case){'rear_pose_always_accepts'{'StageReached'};'crooked_jump_always_accepts'{'JumpHeading'};'misaligned_entry_never_holds'{'HoldEntryAlignment'};'retry_phase_disconnected'{$type=$m.GetTypes()|Where-Object{$_.Name -eq 'MissionState' -and $_.DeclaringType.Name -eq 'FollowerTraversalLifecycle'};'RequestFullRetry'};default{$type=$runtime;'TickCapabilityFullRouteRetry'}}
  $method=$type.Methods|Where-Object Name -eq $name|Select-Object -First 1;$method.Body=[dnlib.DotNet.Emit.CilBody]::new()
  if($case -ne 'retry_phase_disconnected'){$code=if($case -in @('rear_pose_always_accepts','crooked_jump_always_accepts')){[dnlib.DotNet.Emit.OpCodes]::Ldc_I4_1}else{[dnlib.DotNet.Emit.OpCodes]::Ldc_I4_0};$method.Body.Instructions.Add([dnlib.DotNet.Emit.Instruction]::Create($code))};$method.Body.Instructions.Add([dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ret))
 }
 $target=Join-Path $dir 'BotTraversal.dll';$m.Write($target);$m.Dispose();$ErrorActionPreference='Continue'
 if($case -in @('rear_pose_always_accepts','crooked_jump_always_accepts','misaligned_entry_never_holds','retry_phase_disconnected')){
  $test=Join-Path $dir 'test';[IO.Directory]::CreateDirectory($test)|Out-Null;Get-ChildItem (Join-Path $PSScriptRoot 'Tests/bin/Release/net10.0') -File|ForEach-Object{Copy-Item -LiteralPath $_.FullName -Destination $test -Force};Copy-Item -LiteralPath $target -Destination (Join-Path $test 'BotTraversal.dll') -Force
  dotnet (Join-Path $test 'BotTraversal.Lifecycle.Tests.dll') (Join-Path $dir 'behavior.json') *> (Join-Path $dir 'rejection.log');$status=$LASTEXITCODE;$kind='actual DLL behavior'
 }else{
  powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Test-RouteRetryBoundary.ps1') -Dll $target -Output (Join-Path $dir 'il.json') *> (Join-Path $dir 'rejection.log');$status=$LASTEXITCODE;$kind='actual host IL wiring'
 }
 $ErrorActionPreference='Stop';$log=Get-Content (Join-Path $dir 'rejection.log') -Raw
 if(!$status -or $log -notmatch 'Unhandled exception|failures='){throw "mutant did not fail intended checks: $case"}
 if($log -match 'InvalidProgramException|FileNotFoundException|BadImageFormatException|ParameterBindingException'){throw "mutant failed due invalid test setup: $case"}
 $results+=@{id=$case;rejected=$true;actualBinaryModified=$true;oracle=$kind;exitCode=$status};Write-Output "Rejected route retry actual DLL mutant $case"
}
@{passed=$true;cases=$results;physicsSimulated=$false}|ConvertTo-Json -Depth 5|Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'mutation-result.json')
