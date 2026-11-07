param([string]$Dll,[string]$Output=(Join-Path $PSScriptRoot 'native-avoidance-transform.json'))
$ErrorActionPreference='Stop'
$game='D:/Games/SIT_DIR/SPT AKI 4.1.6/EscapeFromTarkov_Data/Managed'
$core='D:/Games/SIT_DIR/SPT AKI 4.1.6/BepInEx/core'
# Resolve dependencies in managed code, with a re-entry guard. PowerShell command
# discovery inside AssemblyResolve can recursively load its own cmdlet assemblies.
$resolvedDll=(Resolve-Path -LiteralPath $Dll).Path
$nativeSha=(Get-FileHash -LiteralPath (Join-Path $game 'Assembly-CSharp.dll') -Algorithm SHA256).Hash
if($nativeSha -ne 'EE25CEE1259777B38ED8B3E7841FDC2DB3C98540B1469FA539B1FF183476E436'){throw 'native game build differs from reviewed SPT4.1.6 artifact'}
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
public static class CoordinationMetadataResolver {
 public static string Game,Core;
 public static readonly ResolveEventHandler Handler=Resolve;
 private static readonly HashSet<string> Resolving=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
 public static Assembly Resolve(object sender,ResolveEventArgs args) {
  string name=args.Name.Split(',')[0];
  lock(Resolving){if(!Resolving.Add(name))return null;}
  try {
   foreach(var a in AppDomain.CurrentDomain.GetAssemblies())if(!a.IsDynamic&&a.GetName().Name==name)return a;
   foreach(string root in new[]{Game,Core}){string path=Path.Combine(root,name+".dll");if(File.Exists(path))return Assembly.LoadFrom(path);}
   return null;
  } finally {lock(Resolving)Resolving.Remove(name);}
 }
}
'@
[CoordinationMetadataResolver]::Game=$game;[CoordinationMetadataResolver]::Core=$core
$resolve=[CoordinationMetadataResolver]::Handler
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolve)
try{
 [Reflection.Assembly]::LoadFrom((Join-Path $core '0Harmony.dll'))|Out-Null
 $gameAssembly=[Reflection.Assembly]::LoadFrom((Join-Path $game 'Assembly-CSharp.dll'))
 $native=$gameAssembly.GetType('BotLocalAvoidance').GetMethod('ManualUpdate')
 $original=[HarmonyLib.PatchProcessor]::GetOriginalInstructions($native)
 $old=@($original);$site=-1;$branch=-1
 for($i=0;$i+7 -lt $old.Count;$i++){
  if($old[$i].opcode -eq [Reflection.Emit.OpCodes]::Ldloc_2 -and $old[$i+1].operand.Name -eq 'get_Id' -and $old[$i+2].opcode -eq [Reflection.Emit.OpCodes]::Ldarg_0){
   for($j=$i+3;$j -lt [Math]::Min($i+9,$old.Count);$j++){if($old[$j].opcode -in @([Reflection.Emit.OpCodes]::Beq,[Reflection.Emit.OpCodes]::Beq_S)){$site=$i;$branch=$j;break}}
   if($site -ge 0){break}
  }
 }
 if($site -lt 0){throw 'actual native loop not found'}
 $label=$old[$branch].operand;$labels=@($old[$site].labels)
 $assembly=[Reflection.Assembly]::LoadFrom($resolvedDll)
 $method=$assembly.GetType('BotTraversal.FollowerFriendlyAvoidancePatch').GetMethod('FilterFriendlyPairs',[Reflection.BindingFlags]'Static,NonPublic')
 # Transform actual game instructions with the actual plugin; no game method is invoked or patched.
 $result=@($method.Invoke($null,[object[]]@(,$original)))
 if($result.Count -ne $old.Count+4){throw 'friendly filter must add exactly four stack-balanced instructions'}
 if($result[$site].opcode -ne [Reflection.Emit.OpCodes]::Ldarg_0 -or $result[$site+1].opcode -ne [Reflection.Emit.OpCodes]::Ldloc_2){throw 'friendly owner/pair operands mismatch'}
 if($result[$site+2].operand.Name -ne 'SkipFriendlyPair' -or $result[$site+3].opcode -ne [Reflection.Emit.OpCodes]::Brtrue){throw 'friendly pair branch missing'}
 if(!$result[$site+3].operand.Equals($label)){throw 'friendly pairs must branch to actual native loop continuation'}
 if($result[$site+4].opcode -ne $old[$site].opcode){throw 'native enemy/self loop changed'}
 $file=(Resolve-Path -LiteralPath $Dll).Path
 $sha=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
 @{passed=$true;checks=6;dllSha256=$sha;nativeGameSha256=(Get-FileHash -LiteralPath (Join-Path $game 'Assembly-CSharp.dll') -Algorithm SHA256).Hash;originalInstructions=$old.Count;filteredInstructions=$result.Count;nativeCodeExecuted=$false;gameLaunched=$false;physicsSimulated=$false}|ConvertTo-Json|Set-Content -Encoding UTF8 -LiteralPath $Output
 Write-Output "PASS actual SPT4.1.6 avoidance IL transformed by actual plugin; six invariants; no game method executed"
}finally{[AppDomain]::CurrentDomain.remove_AssemblyResolve($resolve)}
