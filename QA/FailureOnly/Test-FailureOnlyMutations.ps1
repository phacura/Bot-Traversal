param([string]$Dll)
$ErrorActionPreference='Stop'
[Reflection.Assembly]::LoadFrom('D:/Games/SIT_DIR/mod or addon builds/dnSpy-net-win64/bin/dnlib.dll')|Out-Null
$canonical=(Resolve-Path -LiteralPath $Dll).Path;$root=Join-Path $PSScriptRoot 'Mutants';[IO.Directory]::CreateDirectory($root)|Out-Null;$results=@()
foreach($case in @('normal_can_enter_recovery','cleanup_recovery_hook_restored')){
 $m=[dnlib.DotNet.ModuleDefMD]::Load($canonical);$r=$m.GetTypes()|Where-Object FullName -eq 'BotTraversal.Core.FollowerTraversalTestRuntime';$dir=Join-Path $root $case;[IO.Directory]::CreateDirectory($dir)|Out-Null
 if($case -eq 'normal_can_enter_recovery'){
  $method=($m.GetTypes()|Where-Object Name -eq 'FollowerCapabilityRouteRetryPolicy').Methods|Where-Object Name -eq 'FailureAdmission';$method.Body=[dnlib.DotNet.Emit.CilBody]::new();$method.Body.Instructions.Add([dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ldc_I4_1));$method.Body.Instructions.Add([dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ret))
 }else{
  $method=$r.Methods|Where-Object Name -eq 'ForceReleaseTraversalProbe';$request=$r.Methods|Where-Object{$_.Name -eq 'TryRequestCapabilityFullRouteRetry' -and $_.MethodSig.Params[0].FullName.EndsWith('/ProbeRuntime')}|Select-Object -First 1
  $ins=$method.Body.Instructions;$prefix=@([dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ldarg_0),[dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ldarg_1),[dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Ldc_I4_1),[dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Call,$request),[dnlib.DotNet.Emit.Instruction]::Create([dnlib.DotNet.Emit.OpCodes]::Pop));for($i=$prefix.Count-1;$i -ge 0;$i--){$ins.Insert(0,$prefix[$i])};$method.Body.SimplifyBranches();$method.Body.OptimizeBranches()
 }
 $target=Join-Path $dir 'BotTraversal.dll';$m.Write($target);$m.Dispose();$ErrorActionPreference='Continue'
 if($case -eq 'normal_can_enter_recovery'){
  $test=Join-Path $dir 'test';[IO.Directory]::CreateDirectory($test)|Out-Null;Get-ChildItem (Join-Path $PSScriptRoot 'Tests/bin/Release/net10.0') -File|ForEach-Object{Copy-Item -LiteralPath $_.FullName -Destination $test -Force};Copy-Item -LiteralPath $target -Destination (Join-Path $test 'BotTraversal.dll') -Force
  dotnet (Join-Path $test 'BotTraversal.Lifecycle.Tests.dll') (Join-Path $dir 'behavior.json') *> (Join-Path $dir 'rejection.log');$status=$LASTEXITCODE;$oracle='actual failure-only paired first/retry behavior'
 }else{
  powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Test-FailureOnlyBoundary.ps1') -Dll $target -Output (Join-Path $dir 'il.json') *> (Join-Path $dir 'rejection.log');$status=$LASTEXITCODE;$oracle='actual cleanup isolation IL'
 }
 $ErrorActionPreference='Stop';$log=Get-Content (Join-Path $dir 'rejection.log') -Raw
 if(!$status -or $log -notmatch 'Unhandled exception|failures='){throw "isolation mutant escaped $case"};if($log -match 'InvalidProgramException|FileNotFoundException|BadImageFormatException|ParameterBindingException'){throw "invalid mutant setup $case"}
 $results+=@{id=$case;rejected=$true;actualBinaryModified=$true;compiledFromMutantSource=$false;oracle=$oracle};Write-Output "Rejected RC114 actual fault $case"
}
@{passed=$true;cases=$results;physicsSimulated=$false}|ConvertTo-Json -Depth 5|Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'mutation-result.json')
