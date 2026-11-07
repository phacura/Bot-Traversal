"""Verify an exact SOURCE extraction; default Run-CorridorQA.ps1 remains the full suite.

Build and rerun behavior, production decisions, actual DLL wiring and native IL
transforms. Compare all normalized methods, fields and writer catalog with the
source package's full-suite fingerprint. Identical IL does not need a second
execution of the same structural omission mutants. No Unity methods execute.
"""
from pathlib import Path
import hashlib, json, subprocess, os

r=Path(__file__).resolve().parent.parent
q=r/'QA/CorridorIntegrity';s=r/'BotTraversal-1.0.1-SOURCE';dll=s/'bin/Release/net472/BotTraversal.dll'
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
expected=read(q/'fingerprint.json');certificate=read(q/'fault-detection.json');original=read(q/'result.json')
assert original['passed'] and certificate['passed']
for n in ['artifact-result','result','fingerprint','replay','integration','native-transform','fault-detection']:
 (q/(n+'.json')).unlink(missing_ok=True)
# Windows PowerShell must discover its own built-in modules when this runner is
# launched through Python from PowerShell 7 (which otherwise leaks its module path).
env={k:v for k,v in os.environ.items() if k.upper()!='PSMODULEPATH'}
def run(*args):subprocess.run([str(a) for a in args],cwd=r,env=env,check=True)
run('dotnet','build',s/'BotTraversal.csproj','-c','Release','-m:1','-p:UseSharedCompilation=false','--nologo')
run('dotnet','msbuild',r/'Build-QA.proj','-t:Build','-m:1','-v:minimal','-nologo','-p:UseSharedCompilation=false')
checks=0
for item in read(r/'QA/Round4C/behavior-suites.json'):
 project=r/item;out=q/(item.replace('/','_')+'.json')
 run('dotnet',project.parent/'bin/Release/net10.0/BotTraversal.Lifecycle.Tests.dll',out)
 checks+=read(out)['checks']
run('dotnet','build',q/'Replay/Replay.csproj','-c','Release','-m:1','-p:UseSharedCompilation=false','--nologo')
run('dotnet',q/'Replay/bin/Release/net10.0/BotTraversal.Lifecycle.Tests.dll',q/'replay.json')
for script,name in [(q/'Test-Ingress.ps1','integration'),(r/'QA/CoordinatedTraversal/Test-NativeAvoidanceTransform.ps1','native-transform')]:
 run('powershell','-NoProfile','-ExecutionPolicy','Bypass','-File',script,'-Dll',dll,'-Output',q/(name+'.json'))
run('dotnet','build',r/'QA/Compatibility416/Compatibility416.csproj','-c','Release','--nologo')
run('dotnet',r/'QA/Compatibility416/bin/Release/net10.0/Compatibility416.dll',dll,q/'compatibility416.json')
run('dotnet',r/'QA/Round4B/Fingerprint/bin/Release/net10.0/Fingerprint.dll',dll,q/'fingerprint.json','--slim')
actual=read(q/'fingerprint.json')
assert checks==original['retainedChecks'], 'retained suite count differs from full-suite reference'
for key in ['methods','fields','catalog']:assert actual[key]==expected[key],key+' differs from full-suite source package'
for key in ['replay','integration','native-transform','compatibility416']:assert read(q/(key+'.json'))['passed']
result=dict(passed=True,retainedChecks=checks,dllSha256=hashlib.sha256(dll.read_bytes()).hexdigest().upper(),
 physicsSimulated=False,faultDetectionRepeated=False,referenceFullSuiteDllSha256=original['dllSha256'],
 inheritedStructuralFaultChecks=certificate['checks'],
 methodFingerprintEqual=len(actual['methods']),fieldFingerprintEqual=len(actual['fields']),catalogEqual=len(actual['catalog']),
 kind='independent SOURCE rebuild and behavioral/wiring verification; identical full IL fingerprint binds original omission-fault certificate')
(q/'artifact-result.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print('PASS independent SOURCE artifact; all normalized methods, fields and writer catalog match; no duplicate omission-fault run')
