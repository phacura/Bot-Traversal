param([string]$Dll)
$ErrorActionPreference='Stop'
[Reflection.Assembly]::LoadFrom('D:/Games/SIT_DIR/mod or addon builds/dnSpy-net-win64/bin/dnlib.dll')|Out-Null
$canonical=(Resolve-Path -LiteralPath $Dll).Path;$root=Join-Path $PSScriptRoot 'Mutants';[IO.Directory]::CreateDirectory($root)|Out-Null;$results=@()
foreach($case in @('body_observation_disabled','body_observation_unbounded','handoff_identity_bypassed','prefix_identity_bypassed','missed_window_geometry_only','carry_consumer_disconnected','world_input_disconnected','snapshot_raw_after_attempt')){
 $m=[dnlib.DotNet.ModuleDefMD]::Load($canonical);$r=$m.GetTypes()|Where-Object FullName -eq 'BotTraversal.Core.FollowerTraversalTestRuntime';$p=$m.GetTypes()|Where-Object Name -eq 'FollowerCapabilityApproachPolicy';$dir=Join-Path $root $case;[IO.Directory]::CreateDirectory($dir)|Out-Null
 $behavior=$case -notin @('carry_consumer_disconnected','world_input_disconnected','snapshot_raw_after_attempt')
 if($behavior){
  $name=switch($case){'body_observation_disabled'{'BodyVelocityObservation'}'body_observation_unbounded'{'BodyVelocityObservation'}'handoff_identity_bypassed'{'CarryBodyObservation'}'prefix_identity_bypassed'{'WalkPrefixJump'}'missed_window_geometry_only'{'CrossedJumpWindow'}}
  $method=$p.Methods|Where-Object Name -eq $name;$method.Body=[dnlib.DotNet.Emit.CilBody]::new()
  $op=if($case -eq 'body_observation_disabled'){[dnlib.DotNet.Emit.OpCodes]::Ldc_I4_0}else{[dnlib.DotNet.Emit.OpCodes]::Ldc_I4_1}
  $method.Body.Instructions.Add([dnlib.DotNet.Emit.Instruction]::Create($op));$method.Body.Instructions.Add([dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ret))
 }elseif($case -eq 'carry_consumer_disconnected'){
  $method=$r.Methods|Where-Object Name -eq 'TryCarryCapabilityJumpBodyObservation';$method.Body=[dnlib.DotNet.Emit.CilBody]::new();$method.Body.Instructions.Add([dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ret))
 }elseif($case -eq 'world_input_disconnected'){
  $method=$r.Methods|Where-Object Name -eq 'UpdateLearningV331NativeGroundContinuation';$call=$method.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'call' -and $_.Operand.Name -eq 'TryCommitV2ProbePhysicalMotion'}
  if(@($call).Count -ne 1){throw 'unique physical continuation call missing'}
  $idx=$method.Body.Instructions.IndexOf($call);$instruction=$method.Body.Instructions[$idx-1];$instruction.OpCode=[dnlib.DotNet.Emit.OpCodes]::Ldc_I4_0;$instruction.Operand=$null
 }else{
  $method=$r.Methods|Where-Object Name -eq 'TriggerNativeJump';$store=$method.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'stfld' -and $_.Operand.Name -eq 'LearningV414JumpTakeoffPlanarSpeed'}
  $field=($method.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldfld' -and $_.Operand.Name -eq 'Player'}|Select-Object -First 1).Operand
  $getter=$r.Methods|Where-Object Name -eq 'GetLearningV329PlanarSpeed';$idx=$method.Body.Instructions.IndexOf($store)
  $instruction=$method.Body.Instructions[$idx-1];$instruction.OpCode=[dnlib.DotNet.Emit.OpCodes]::Ldarg_0;$instruction.Operand=$null
  $method.Body.Instructions.Insert($idx,[dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ldfld,$field))
  $method.Body.Instructions.Insert($idx+1,[dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Call,$getter))
 }
 $target=Join-Path $dir 'BotTraversal.dll';$m.Write($target);$m.Dispose();$ErrorActionPreference='Continue'
 if($behavior){
  $test=Join-Path $dir 'test';[IO.Directory]::CreateDirectory($test)|Out-Null
  Get-ChildItem (Join-Path $PSScriptRoot 'Tests/bin/Release/net10.0') -File|ForEach-Object{Copy-Item -LiteralPath $_.FullName -Destination $test -Force}
  Copy-Item -LiteralPath $target -Destination (Join-Path $test 'BotTraversal.dll') -Force
  dotnet (Join-Path $test 'BotTraversal.Lifecycle.Tests.dll') (Join-Path $dir 'behavior.json') *> (Join-Path $dir 'rejection.log');$status=$LASTEXITCODE;$oracle='actual DLL pure behavior'
 }else{
  powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Test-JumpBoundaryBoundary.ps1') -Dll $target -Output (Join-Path $dir 'il.json') *> (Join-Path $dir 'rejection.log');$status=$LASTEXITCODE;$oracle='actual DLL integration IL'
 }
 $ErrorActionPreference='Stop';$log=Get-Content (Join-Path $dir 'rejection.log') -Raw
 if(!$status -or $log -notmatch 'Unhandled exception|failures='){throw "RC119 mutant escaped $case"}
 if($log -match 'InvalidProgramException|FileNotFoundException|BadImageFormatException|ParameterBindingException'){throw "invalid mutant setup $case"}
 $results+=@{id=$case;rejected=$true;actualBinaryModified=$true;compiledFromMutantSource=$false;oracle=$oracle};Write-Output "Rejected RC119 actual fault $case"
}
@{passed=$true;cases=$results;physicsSimulated=$false}|ConvertTo-Json -Depth 5|Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'mutation-result.json')
