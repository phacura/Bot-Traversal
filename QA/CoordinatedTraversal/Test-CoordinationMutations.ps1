param([string]$Dll)
$ErrorActionPreference='Stop'
[Reflection.Assembly]::LoadFrom('D:/Games/SIT_DIR/mod or addon builds/dnSpy-net-win64/bin/dnlib.dll')|Out-Null
$canonical=(Resolve-Path -LiteralPath $Dll).Path;$root=Join-Path $PSScriptRoot 'Mutants';[IO.Directory]::CreateDirectory($root)|Out-Null;$results=@()
foreach($case in @('failure_skips_alternate','ranking_ignores_evidence','queue_always_admits','queue_cooldown_removed','native_first_disconnected','teleport_cleanup_disconnected','walking_guard_disconnected','friendly_filter_disconnected','native_transform_noop')){
 $m=[dnlib.DotNet.ModuleDefMD]::Load($canonical);$types=$m.GetTypes();$runtime=$types|Where-Object FullName -eq 'BotTraversal.Core.FollowerTraversalTestRuntime'
 $dir=Join-Path $root $case;[IO.Directory]::CreateDirectory($dir)|Out-Null
 $behavior=$case -in @('failure_skips_alternate','ranking_ignores_evidence','queue_always_admits','queue_cooldown_removed')
 if($case -eq 'failure_skips_alternate'){$method=($types|Where-Object Name -eq 'FollowerCapabilityFailureSequence').Methods|Where-Object Name -eq 'AfterFailure';$value=5}
 elseif($case -eq 'ranking_ignores_evidence'){$method=($types|Where-Object Name -eq 'FollowerCapabilityFailureSequence').Methods|Where-Object Name -eq 'Rank';$value=0}
 elseif($case -eq 'queue_always_admits'){$method=($types|Where-Object Name -eq 'FollowerPortalAdmission').Methods|Where-Object Name -eq 'Admit';$value=1}
 elseif($case -eq 'queue_cooldown_removed'){
  $method=($types|Where-Object Name -eq 'FollowerPortalAdmission').Methods|Where-Object Name -eq 'Complete'
  $one=@($method.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldc.r8' -and $_.Operand -eq 1.0});if($one.Count -ne 1){throw 'unique actual cooldown constant missing'};$one[0].Operand=[double]0
 }elseif($case -eq 'native_first_disconnected'){$method=$runtime.Methods|Where-Object Name -eq 'TryDispatchCapabilitySequenceNative';$value=0}
 elseif($case -eq 'teleport_cleanup_disconnected'){$method=$runtime.Methods|Where-Object Name -eq 'ClearCapabilityRouteWorkForTeleport'}
 elseif($case -eq 'walking_guard_disconnected'){$method=$runtime.Methods|Where-Object Name -eq 'IsCapabilityRetryWalking';$value=0}
 elseif($case -eq 'friendly_filter_disconnected'){$method=($types|Where-Object Name -eq 'FollowerFriendlyAvoidancePatch').Methods|Where-Object Name -eq 'SkipFriendlyPair';$value=0}
 else{$method=($types|Where-Object Name -eq 'FollowerFriendlyAvoidancePatch').Methods|Where-Object Name -eq 'FilterFriendlyPairs'}
 if($case -ne 'queue_cooldown_removed'){
  $method.Body=[dnlib.DotNet.Emit.CilBody]::new()
  if($case -eq 'native_transform_noop'){$method.Body.Instructions.Add([dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ldarg_0))}
  elseif($case -eq 'ranking_ignores_evidence'){$method.Body.Instructions.Add([dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ldc_R8,[double]0))}
  elseif($case -ne 'teleport_cleanup_disconnected'){
   if($case -eq 'native_first_disconnected'){
    $method.Body.Instructions.Add([dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ldarg_1))
    $method.Body.Instructions.Add([dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ldstr,''))
    $method.Body.Instructions.Add([dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Stind_Ref))
   }
   $method.Body.Instructions.Add([dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ldc_I4,[int]$value))
  }
  $method.Body.Instructions.Add([dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ret))
 }
 $target=Join-Path $dir 'BotTraversal.dll';$m.Write($target);$m.Dispose();$ErrorActionPreference='Continue'
 if($behavior){
  $test=Join-Path $dir 'test';[IO.Directory]::CreateDirectory($test)|Out-Null
  Get-ChildItem (Join-Path $PSScriptRoot 'Tests/bin/Release/net10.0') -File|ForEach-Object{Copy-Item -LiteralPath $_.FullName -Destination $test -Force}
  Copy-Item -LiteralPath $target -Destination (Join-Path $test 'BotTraversal.dll') -Force
  dotnet (Join-Path $test 'BotTraversal.Lifecycle.Tests.dll') (Join-Path $dir 'behavior.json') (Join-Path $PSScriptRoot 'Evidence/raid.jsonl') *> (Join-Path $dir 'rejection.log');$status=$LASTEXITCODE;$oracle='actual DLL behavior'
 }else{
  $boundary=if($case -eq 'native_transform_noop'){'Test-NativeAvoidanceTransform.ps1'}else{'Test-CoordinationBoundary.ps1'}
  powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot $boundary) -Dll $target -Output (Join-Path $dir 'integration.json') *> (Join-Path $dir 'rejection.log');$status=$LASTEXITCODE;$oracle='actual DLL integration/native IL transform'
 }
 $ErrorActionPreference='Stop';$log=Get-Content (Join-Path $dir 'rejection.log') -Raw
 if(!$status -or $log -notmatch 'Unhandled exception|failures=|friendly filter must add exactly'){throw "RC121 mutant escaped $case"}
 if($log -match 'InvalidProgramException|FileNotFoundException|BadImageFormatException|ParameterBindingException'){throw "invalid mutant setup $case"}
 $results+=@{id=$case;rejected=$true;actualBinaryModified=$true;compiledFromMutantSource=$false;oracle=$oracle};Write-Output "Rejected RC121 actual DLL fault $case"
}
@{passed=$true;cases=$results;physicsSimulated=$false;gameLaunched=$false}|ConvertTo-Json -Depth 5|Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'mutation-result.json')
