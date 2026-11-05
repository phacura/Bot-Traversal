using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Text.RegularExpressions;
using var module=ModuleDefMD.Load(args[0]);
var methods=new SortedDictionary<string,object>();var fields=new SortedDictionary<string,string>();var catalog=new SortedDictionary<string,object>();
var aliases=module.GetTypes().Where(t=>t.Name.String.StartsWith("<>f__AnonymousType")).ToDictionary(t=>t.FullName,t=>"anon["+string.Join(",",t.Properties.Select(p=>p.Name.String))+"]`"+t.GenericParameters.Count);
var closures=module.GetTypes().Where(t=>t.Name.String.StartsWith("<>c__DisplayClass")).ToDictionary(t=>t.FullName,t=>"closure["+string.Join(",",t.Methods.Select(m=>Regex.Match(m.Name.String,@"^<(.+)>b__")).Where(m=>m.Success).Select(m=>m.Groups[1].Value).Distinct().Order())+"|"+string.Join(",",t.Fields.Select(f=>f.Name.String+":"+Regex.Replace(f.FieldType.FullName,@"DisplayClass\d+_","DisplayClass_")))+"]");
var caches=new Dictionary<string,string>();
foreach(var type in module.GetTypes())foreach(var method in type.Methods){var match=Regex.Match(method.Name.String,@"^<(.+)>b__(\d+)_(\d+)$");if(match.Success)caches[type.FullName+"::<>9__"+match.Groups[2].Value+"_"+match.Groups[3].Value]=type.FullName+"::<>9__"+match.Groups[1].Value+"_"+match.Groups[3].Value;}
var orderedClosures=closures.OrderByDescending(x=>x.Key.Length).ToArray();var orderedCaches=caches.OrderByDescending(x=>x.Key.Length).ToArray();var canonicalCache=new Dictionary<string,string>();
string Canon(string s){
 if(canonicalCache.TryGetValue(s,out var cached))return cached;var original=s;
 s=Regex.Replace(s,@"<>f__AnonymousType\d+`\d+",match=>aliases[match.Value]);
 foreach(var entry in orderedClosures)s=s.Replace(entry.Key,entry.Value);
 foreach(var entry in orderedCaches)s=s.Replace(entry.Key,entry.Value);
 s=Regex.Replace(s,@"<>O::<\d+>__(\w+)","<>O::<cache>__$1");
 s=Regex.Replace(s,@"<([^<>]+)>g__([^|]+)\|\d+_(\d+)","<$1>g__$2|owner_$3");
 var result=Regex.Replace(s,@"<([^<>]+)>b__\d+_(\d+)","<$1>b__owner_$2");canonicalCache[original]=result;return result;
}
string Hash(string s)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
foreach(var type in module.GetTypes()){
 foreach(var field in type.Fields)fields.Add(Canon(field.FullName),field.Attributes+"|"+field.Constant?.Value);
 foreach(var method in type.Methods){
  if(!method.HasBody){methods.Add(method.FullName,new{hash=Hash(method.Attributes+"|"+method.ImplAttributes),calls=Array.Empty<string>(),strings=Array.Empty<string>()});continue;}
  var ins=method.Body.Instructions;var index=ins.Select((v,i)=>(v,i)).ToDictionary(x=>x.v,x=>x.i);
  int At(Instruction? x)=>x==null?ins.Count:index[x];
  string Operand(object? v)=>v switch{
   null=>"",Instruction i=>"@"+At(i),IList<Instruction> branch=>string.Join(",",branch.Select(x=>"@"+At(x))),
   Local local=>"local:"+local.Index,Parameter param=>"arg:"+param.Index,
   IFullName member=>Canon(member.FullName),
   float f=>"f32:"+Convert.ToHexString(BitConverter.GetBytes(f)),double d=>"f64:"+Convert.ToHexString(BitConverter.GetBytes(d)),
   string s=>s.Replace("V2-RC109-R4A","V2-RC108-R3Z").Replace("V2-RC110-R4B","V2-RC108-R3Z").Replace("V2-RC111-R4C","V2-RC108-R3Z").Replace("V2-RC112-RouteRetry","V2-RC108-R3Z").Replace("V2-RC113-RecoveryFix","V2-RC108-R3Z").Replace("V2-RC114-FailureOnly","V2-RC108-R3Z").Replace("V2-RC115-RouteRecovery","V2-RC108-R3Z").Replace("V2-RC116-IngressContract","V2-RC108-R3Z").Replace("V2-RC117-PassageAdmission","V2-RC108-R3Z").Replace("V2-RC118-MotionEvidence","V2-RC108-R3Z").Replace("V2-RC119-JumpBoundary","V2-RC108-R3Z").Replace("V2-RC120-RescueDisposition","V2-RC108-R3Z").Replace("V2-RC121-CoordinatedTraversal","V2-RC108-R3Z"),_=>Convert.ToString(v,CultureInfo.InvariantCulture)??""};
  var lines=new List<string>{method.Attributes+"|"+method.ImplAttributes+"|"+method.Body.InitLocals};
  lines.AddRange(method.Body.Variables.Select(v=>"local:"+Canon(v.Type.FullName)));
  lines.AddRange(ins.Select(i=>i.OpCode.Name+" "+Operand(i.Operand)));
  lines.AddRange(method.Body.ExceptionHandlers.Select(e=>e.HandlerType+"|"+At(e.TryStart)+"|"+At(e.TryEnd)+"|"+At(e.HandlerStart)+"|"+At(e.HandlerEnd)+"|"+At(e.FilterStart)+"|"+e.CatchType?.FullName));
  var strings=ins.Where(x=>x.OpCode.Code==Code.Ldstr).Select(x=>(string)x.Operand).ToArray();
  methods.Add(Canon(method.FullName),new{hash=Hash(string.Join("\n",lines)),calls=args.Contains("--slim")?null:ins.Where(x=>x.OpCode.FlowControl==FlowControl.Call).Select(x=>Operand(x.Operand)).ToArray(),strings=args.Contains("--slim")?strings.Where(x=>Regex.IsMatch(x,@"^R[0-9A-Z]*-[A-F0-9]+$")).ToArray():strings,il=args.Contains("--full")&&type.FullName.StartsWith("BotTraversal")?lines:null});
  if(type.Name=="FollowerControlBackend"&&method.Name=="FindSite")foreach(var literal in ins.Where(x=>x.OpCode.Code==Code.Ldstr&&((string)x.Operand).StartsWith("R")&&((string)x.Operand).Contains('-'))){
   var pos=index[literal];if(pos+2>=ins.Count||ins[pos+2].OpCode.Code is not (Code.Brtrue or Code.Brtrue_S))continue;
   var target=index[(Instruction)ins[pos+2].Operand];if(ins[target].OpCode.Code!=Code.Ldstr||!ins[target+1].IsLdcI4())throw new Exception("catalog shape changed");
   catalog.Add((string)literal.Operand,new{operation=(string)ins[target].Operand,mode=ins[target+1].GetLdcI4Value()});
  }
 }
}
File.WriteAllText(args[1],JsonSerializer.Serialize(new{dllSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[0]))),methods,fields,catalog},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"Fingerprint {methods.Count} methods {fields.Count} fields {catalog.Count} catalog sites");
