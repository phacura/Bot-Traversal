using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Security.Cryptography;
using BotTraversal.Core;
var baselineContext=new AssemblyLoadContext("frozen-RC109",true);var baseline=baselineContext.LoadFromAssemblyPath(Path.GetFullPath(args[1]));var current=typeof(FollowerCapabilityHelperRules).Assembly;
var schema=JsonDocument.Parse(File.ReadAllText(args[2])).RootElement.GetProperty("declarations");int comparisons=0,booleanCases=0,boundaryCases=0;var summaries=new List<object>();
foreach(var rule in schema.EnumerateArray()){
 var typeName=rule.GetProperty("type").GetString()!;var name=rule.GetProperty("name").GetString()!;var old=baseline.GetType(typeName,true)!.GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static)!;var revised=current.GetType(typeName,true)!.GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static)!;
 var parameterRows=rule.GetProperty("parameters").EnumerateArray().ToArray();var parameters=old.GetParameters();var frozen=parameterRows.Where(x=>x.GetProperty("retired").GetBoolean()).ToDictionary(x=>x.GetProperty("Ordinal").GetInt32(),x=>(object)x.GetProperty("value").GetBoolean());var retained=Enumerable.Range(0,parameters.Length).Where(i=>!frozen.ContainsKey(i)).ToArray();
 if(revised.GetParameters().Length!=retained.Length)throw new Exception("specialized arity mismatch "+name);
 var booleans=retained.Where(i=>parameters[i].ParameterType==typeof(bool)).ToArray();if(booleans.Length>18)throw new Exception("truth table unexpectedly large");int start=comparisons;
 object? Default(Type t,int n)=>t==typeof(bool)?false:t==typeof(float)?(float)n:t==typeof(int)?n:t==typeof(string)?"WalkThrough":throw new Exception("unexpected pure-rule type "+t);
 object?[] Values(int mask,int number){var values=parameters.Select(p=>Default(p.ParameterType,number)).ToArray();foreach(var pair in frozen)values[pair.Key]=pair.Value;for(int i=0;i<booleans.Length;i++)values[booleans[i]]=(mask&(1<<i))!=0;return values;}
 void Compare(object?[] values){var a=old.Invoke(null,values);var b=revised.Invoke(null,retained.Select(i=>values[i]).ToArray());if(!Equals(a,b))throw new Exception("old/new rule disagreement "+name+" "+JsonSerializer.Serialize(values));comparisons++;}
 for(int n=0;n<=1;n++)for(int mask=0;mask<(1<<booleans.Length);mask++){Compare(Values(mask,n));booleanCases++;}
 var masks=new[]{0,(1<<booleans.Length)-1,0x55555&((1<<booleans.Length)-1),0xAAAAA&((1<<booleans.Length)-1)}.Distinct().ToArray();
 foreach(int index in retained.Where(i=>parameters[i].ParameterType!=typeof(bool))){
  object?[] samples=parameters[index].ParameterType==typeof(float)?new object?[]{float.NaN,float.NegativeInfinity,float.PositiveInfinity,-float.MaxValue,float.MaxValue,-1f,-.001f,0f,float.Epsilon,.001f,.12f,.16f,.3f,.36f,.42f,.75f,.94f,.955f,1f,2f,100f}:
   parameters[index].ParameterType==typeof(int)?new object?[]{int.MinValue,-1,0,1,2,3,4,42,int.MaxValue}:new object?[]{null,"","unknown","WalkThrough","wAlKtHrOuGh","Climb","Vault","Jump","DropDown"};
  foreach(int mask in masks)foreach(var sample in samples){var values=Values(mask,1);values[index]=sample;Compare(values);boundaryCases++;}
 }
 summaries.Add(new{method=name,compared=comparisons-start,retiredParameters=frozen.Count,retainedBooleanParameters=booleans.Length});
}
if(summaries.Count!=53)throw new Exception("incomplete rule oracle");
string Sha(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=true,rules=53,retiredParameters=60,comparisons,booleanCases,boundaryCases,baselineDllSha256=Sha(args[1]),dllSha256=Sha(current.Location),cases=summaries,physicsSimulated=false},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"PASS {comparisons} original/specialized rule comparisons across 53 actual DLL methods");baselineContext.Unload();
