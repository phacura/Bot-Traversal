$ErrorActionPreference='Stop'
$r=Split-Path $PSScriptRoot -Parent;$s=Join-Path $r 'BotTraversal-1.0.1-SOURCE';$q=Join-Path $PSScriptRoot 'CorridorIntegrity';$dll=Join-Path $s 'bin/Release/net472/BotTraversal.dll'
dotnet build (Join-Path $s 'BotTraversal.csproj') -c Release -m:1 -p:UseSharedCompilation=false --nologo;if($LASTEXITCODE){throw 'production'}
dotnet msbuild (Join-Path $r 'Build-QA.proj') -t:Build -m:1 -v:minimal -nologo -p:UseSharedCompilation=false;if($LASTEXITCODE){throw 'retained build'}
$projects=Get-Content (Join-Path $PSScriptRoot 'Round4C/behavior-suites.json') -Raw|ConvertFrom-Json;$checks=0
foreach($p in $projects){$file=Join-Path $r $p;$dir=Join-Path (Split-Path $file -Parent) 'bin/Release/net10.0';$label=$p.Replace('/','_');$out=Join-Path $q ($label+'.json');dotnet (Join-Path $dir 'BotTraversal.Lifecycle.Tests.dll') $out;if($LASTEXITCODE){throw $p};$checks+=(Get-Content $out -Raw|ConvertFrom-Json).checks}
dotnet build (Join-Path $q 'Replay/Replay.csproj') -c Release -m:1 -p:UseSharedCompilation=false --nologo;if($LASTEXITCODE){throw 'replay build'}
dotnet (Join-Path $q 'Replay/bin/Release/net10.0/BotTraversal.Lifecycle.Tests.dll') (Join-Path $q 'replay.json');if($LASTEXITCODE){throw 'replay'}
powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $q 'Test-Ingress.ps1') -Dll $dll -Output (Join-Path $q 'integration.json');if($LASTEXITCODE){throw 'integration'}
powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'CoordinatedTraversal/Test-NativeAvoidanceTransform.ps1') -Dll $dll -Output (Join-Path $q 'native-transform.json');if($LASTEXITCODE){throw 'native transform'}
dotnet build (Join-Path $PSScriptRoot 'Compatibility416/Compatibility416.csproj') -c Release --nologo;if($LASTEXITCODE){throw 'compatibility build'}
dotnet (Join-Path $PSScriptRoot 'Compatibility416/bin/Release/net10.0/Compatibility416.dll') $dll (Join-Path $q 'compatibility416.json');if($LASTEXITCODE){throw 'SPT416/PIT111 metadata compatibility'}
powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $q 'Test-FaultDetection.ps1') -Dll $dll -Output (Join-Path $q 'fault-detection.json');if($LASTEXITCODE){throw 'fault detection'}
dotnet (Join-Path $PSScriptRoot 'Round4B/Fingerprint/bin/Release/net10.0/Fingerprint.dll') $dll (Join-Path $q 'fingerprint.json') --slim;if($LASTEXITCODE){throw 'fingerprint'}
@{passed=$true;retainedChecks=$checks;dllSha256=(Get-FileHash $dll -Algorithm SHA256).Hash;physicsSimulated=$false}|ConvertTo-Json|Set-Content -Encoding UTF8 (Join-Path $q 'result.json')
