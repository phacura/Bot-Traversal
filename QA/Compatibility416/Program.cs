using dnlib.DotNet;
using System.Security.Cryptography;
using System.Text.Json;
var dll=Path.GetFullPath(args[0]);var output=Path.GetFullPath(args[1]);
var game="D:/Games/SIT_DIR/SPT AKI 4.1.6/EscapeFromTarkov_Data/Managed";
var plugins="D:/Games/SIT_DIR/SPT AKI 4.1.6/BepInEx/plugins";
var core="D:/Games/SIT_DIR/SPT AKI 4.1.6/BepInEx/core";
var resolver=new AssemblyResolver {EnableTypeDefCache=true};var ctx=new ModuleContext(resolver);resolver.DefaultModuleContext=ctx;
foreach(var dir in new[]{Path.GetDirectoryName(dll)!,game,core,plugins+"/spt",plugins+"/SAIN",plugins+"/DrakiaXYZ-BigBrain",plugins+"/pitFireTeam"})resolver.PreSearchPaths.Add(dir);
using var module=ModuleDefMD.Load(dll,new ModuleCreationOptions(ctx));
using var g=ModuleDefMD.Load(game+"/Assembly-CSharp.dll",new ModuleCreationOptions(ctx));
using var pit=ModuleDefMD.Load(plugins+"/pitFireTeam/pitFireTeam.dll",new ModuleCreationOptions(ctx));
var checks=new List<string>();void Check(bool ok,string name){if(!ok)throw new Exception(name);checks.Add(name);}
TypeDef T(ModuleDef m,string name)=>m.Find(name,false)??throw new Exception("Missing type "+name);
MethodDef M(TypeDef t,string name,params string[] p)=>t.Methods.SingleOrDefault(m=>m.Name==name&&m.MethodSig.Params.Select(x=>x.FullName).SequenceEqual(p))??throw new Exception("Missing signature "+t.FullName+"::"+name+"("+string.Join(",",p)+")");
Check(pit.Assembly.Version==new Version(1,1,1,0),"PIT assembly 1.1.1.0");
Check(pit.Mvid==new Guid("084ae5d2-4ba7-4f08-ad73-d22b300816e8"),"reviewed PIT module identity");
var plugin=T(pit,"pitTeam.pitFireTeam");var attr=plugin.CustomAttributes.Single(a=>a.AttributeType.FullName=="BepInEx.BepInPlugin");
Check(attr.ConstructorArguments[0].Value.ToString()=="xyz.pit.fireteam"&&attr.ConstructorArguments[2].Value.ToString()=="1.1.1","PIT GUID and plugin version");
Check(pit.Find("pitTeam.Modules.FollowerTraversalTestRuntime",false)==null,"PIT has no competing embedded traversal runtime");
var follow=T(pit,"pitTeam.BigBrain.Actions.FollowAction");
var distance=M(follow,"GetEffectiveFollowDistance");Check(distance.ReturnType.FullName=="System.Single","follow distance getter float");
Check(!distance.Body.Instructions.Any(i=>i.OpCode.Name is "stfld" or "stsfld"),"follow distance getter remains read-only");
Check(distance.Body.Instructions.Where(i=>i.Operand is IMethod).Select(i=>((IMethod)i.Operand).Name.ToString()).All(n=>n is "get_Value" or "Clamp" or ".ctor" or "get_HasValue" or "GetValueOrDefault"),"follow distance getter has only configuration and clamp calls");
M(follow,"Follow","System.Boolean","System.Single");checks.Add("PIT ordinary Follow signature");
M(follow,"ApplyFollowChaseMovementState","System.Boolean");checks.Add("PIT gait signature");
var follower=T(pit,"pitTeam.Components.BotFollowerPlayer");var peek=M(follower,"TryPeekActiveCommand","pitTeam.Components.FollowerCommandType&","UnityEngine.Vector3&","System.Single&");Check(peek.ReturnType.FullName=="System.Boolean","command reader signature and return type");
var lookup=M(T(pit,"pitTeam.Modules.BossPlayers"),"GetFollowerByProfileId","System.String");Check(lookup.IsStatic&&lookup.ReturnType.FullName==follower.FullName,"follower lookup stays authoritative");
var teleport=M(plugin,"_BotTeleport");Check(!teleport.IsStatic&&teleport.ReturnType.FullName=="System.Void","manual regroup instance void boundary");
Check(teleport.Body.Instructions.Any(i=>i.Operand is IMethod m&&m.Name=="Teleport"&&m.DeclaringType.FullName=="EFT.Player"),"PIT F4 still consumes Player.Teleport boundary");
Check(plugin.Fields.Any(f=>f.Name=="teleportKey"&&f.IsStatic),"PIT manual shortcut metadata");
foreach(var x in new[]{("BotMover","ManualFixedUpdate",Array.Empty<string>()),("BotMover","method_12",Array.Empty<string>()),("BotMover","SetPlayerToNavMesh",new[]{"UnityEngine.Vector3"}),("BotMover","CastFromPos",new[]{"UnityEngine.Vector3","System.Boolean"}),("BotMoverImpostor","OnMotionApplied",new[]{"UnityEngine.CollisionFlags","UnityEngine.Vector3"}),("BotLocalAvoidance","ManualUpdate",Array.Empty<string>()),("EFT.MovementContext","ApplyMotion",new[]{"UnityEngine.Vector3","System.Single"}),("EFT.ClientPlayerMovementContext","ApplyMotion",new[]{"UnityEngine.Vector3","System.Single"}),("EFT.MovementState","OnStateMove",new[]{"System.Single"}),("EFT.MovementContext","set_SprintSpeed",new[]{"System.Single"})}){M(T(g,x.Item1),x.Item2,x.Item3);checks.Add("game hook "+x.Item1+"::"+x.Item2);}
foreach(var x in new[]{("BotData","_owner"),("BotMover","_allowTeleport"),("BotMover","_linkedToNavmeshInitially"),("BotMover","_prevPosLinkedTime"),("BotMover","_prevSuccessLinkedFrom"),("PlayerOverlapManager","_headColliderExtrusion"),("PlayerOverlapManager","_cameraColliderExtrusion"),("EFT.MovementContext","_player"),("EFT.Vaulting.VaultingComponent","_isAutoVaultingSettingEnable")})Check(T(g,x.Item1).Fields.Any(f=>f.Name==x.Item2),"game field "+x.Item1+"::"+x.Item2);
Check(T(g,"BotMoverImpostor").BaseType.FullName=="BotMover","motion integrator retains BotMover ownership");
Check(M(T(g,"BotMoverImpostor"),"OnMotionApplied","UnityEngine.CollisionFlags","UnityEngine.Vector3").Body.Instructions.Any(i=>i.Operand is IMethod m&&m.Name=="SetPlayerToNavMesh"),"motion integrator still contains guarded NavMesh projection");
M(T(g,"EFT.MovementContext"),"CalculatePoseLevelValues","System.Single","UnityEngine.Vector3","System.Single","System.Boolean","System.Single&","UnityEngine.Vector3&");checks.Add("read-only native posture capsule formula signature");
var missing=new List<string>();int resolved=0;
foreach(var mr in module.GetMemberRefs()){
 string assembly=mr.DeclaringType.DefinitionAssembly?.Name??"";
 if(assembly!="Assembly-CSharp")continue;
 bool ok=mr.IsMethodRef?mr.ResolveMethod()!=null:mr.ResolveField()!=null;
 if(!ok)missing.Add(mr.FullName);else resolved++;
}
Check(missing.Count==0,"all direct game member references resolve against installed 4.1.6: "+string.Join(";",missing.Take(5)));
string Hash(string p)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)));
File.WriteAllText(output,JsonSerializer.Serialize(new{passed=true,checks=checks.Count,cases=checks,gameMembersResolved=resolved,unresolvedGameMembers=missing,dllSha256=Hash(dll),nativeGameSha256=Hash(game+"/Assembly-CSharp.dll"),pitSha256=Hash(plugins+"/pitFireTeam/pitFireTeam.dll"),pitMvid=pit.Mvid,gameExecuted=false,physicsSimulated=false},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS {checks.Count} installed SPT4.1.6/PIT1.1.1 metadata contracts; {resolved} game members resolved; no game method executed");
