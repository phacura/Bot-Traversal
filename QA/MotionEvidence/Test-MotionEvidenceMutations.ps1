param([string]$Dll)
$ErrorActionPreference='Stop'
[Reflection.Assembly]::LoadFrom('D:/Games/SIT_DIR/mod or addon builds/dnSpy-net-win64/bin/dnlib.dll')|Out-Null
$canonical=(Resolve-Path -LiteralPath $Dll).Path;$root=Join-Path $PSScriptRoot 'Mutants';[IO.Directory]::CreateDirectory($root)|Out-Null;$results=@()
foreach($case in @('velocity_always_allowed','velocity_disabled','portal_always_centred','portal_correction_disabled','geometry_consumer_disconnected','body_observer_disconnected')){
 $m=[dnlib.DotNet.ModuleDefMD]::Load($canonical);$r=$m.GetTypes()|Where-Object FullName -eq 'BotTraversal.Core.FollowerTraversalTestRuntime';$p=$m.GetTypes()|Where-Object Name -eq 'FollowerCapabilityApproachPolicy';$dir=Join-Path $root $case;[IO.Directory]::CreateDirectory($dir)|Out-Null
 if($case -in @('velocity_always_allowed','velocity_disabled','portal_always_centred','portal_correction_disabled')){
  $name=if($case.StartsWith('velocity')){'ReconcileJumpVelocity'}else{'VaultPortalExcess'}
  $method=$p.Methods|Where-Object Name -eq $name;$method.Body=[dnlib.DotNet.Emit.CilBody]::new()
  $op=switch($case){'velocity_always_allowed'{[dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ldc_I4_1)}'velocity_disabled'{[dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ldc_I4_0)}'portal_always_centred'{[dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ldarg_0)}'portal_correction_disabled'{[dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ldc_R4,[single]0)}}
  $method.Body.Instructions.Add($op);$method.Body.Instructions.Add([dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ret))
 }elseif($case -eq 'geometry_consumer_disconnected'){
  $method=$r.Methods|Where-Object Name -eq 'TryGetLearningV460JumpAdmissionGeometry'
  $call=$method.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'call' -and $_.Operand.Name -eq 'GetCapabilityJumpPlanarVelocity'}
  if(@($call).Count -ne 1){throw 'unique consumer missing'}
  $getter=(($r.Methods|Where-Object Name -eq 'GetCapabilityJumpPlanarVelocity').Body.Instructions|Where-Object{$_.OpCode.Name -in @('call','callvirt') -and $_.Operand.Name -eq 'get_Velocity'}).Operand
  $call.OpCode=[dnlib.DotNet.Emit.OpCodes]::Callvirt;$call.Operand=$getter
 }else{
  $method=$r.Methods|Where-Object Name -eq 'ObserveCapabilityPlanarBodyVelocity';$method.Body=[dnlib.DotNet.Emit.CilBody]::new();$method.Body.Instructions.Add([dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ret))
 }
 $target=Join-Path $dir 'BotTraversal.dll';$m.Write($target);$m.Dispose();$ErrorActionPreference='Continue'
 if($case -notin @('geometry_consumer_disconnected','body_observer_disconnected')){
  $test=Join-Path $dir 'test';[IO.Directory]::CreateDirectory($test)|Out-Null
  Get-ChildItem (Join-Path $PSScriptRoot 'Tests/bin/Release/net10.0') -File|ForEach-Object{Copy-Item -LiteralPath $_.FullName -Destination $test -Force}
  Copy-Item -LiteralPath $target -Destination (Join-Path $test 'BotTraversal.dll') -Force
  dotnet (Join-Path $test 'BotTraversal.Lifecycle.Tests.dll') (Join-Path $dir 'behavior.json') *> (Join-Path $dir 'rejection.log');$status=$LASTEXITCODE;$oracle='actual DLL pure behavior'
 }else{
  powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Test-MotionEvidenceBoundary.ps1') -Dll $target -Output (Join-Path $dir 'il.json') *> (Join-Path $dir 'rejection.log');$status=$LASTEXITCODE;$oracle='actual DLL integration IL'
 }
 $ErrorActionPreference='Stop';$log=Get-Content (Join-Path $dir 'rejection.log') -Raw
 if(!$status -or $log -notmatch 'Unhandled exception|failures='){throw "RC118 mutant escaped $case"}
 if($log -match 'InvalidProgramException|FileNotFoundException|BadImageFormatException|ParameterBindingException'){throw "invalid mutant setup $case"}
 $results+=@{id=$case;rejected=$true;actualBinaryModified=$true;compiledFromMutantSource=$false;oracle=$oracle};Write-Output "Rejected RC118 actual fault $case"
}
@{passed=$true;cases=$results;physicsSimulated=$false}|ConvertTo-Json -Depth 5|Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'mutation-result.json')
