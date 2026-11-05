using System.Text.Json;
using System.Security.Cryptography;
using BotTraversal.Core;
using P=BotTraversal.Core.FollowerControlEntryPolicy;
var scan=JsonDocument.Parse(File.ReadAllText(args[1])).RootElement;var cases=new List<string>();int strict=0,compat=0,infra=0;
void Check(bool ok,string label){if(!ok)throw new Exception(label);cases.Add(label);}
foreach(var entry in scan.GetProperty("catalog").EnumerateObject()){
 var mode=(ControlSiteMode)entry.Value.GetProperty("mode").GetInt32();string id=entry.Name;
 Check(P.Decide(false,true,mode,true,false,false,true)==ControlAdmission.Reject,id+": unknown site rejected");
 Check(P.Decide(true,false,mode,true,false,false,true)==ControlAdmission.Reject,id+": operation substitution rejected");
 if(mode==ControlSiteMode.Infrastructure){infra++;Check(P.Decide(true,true,mode,false,false,false,false)==ControlAdmission.Infrastructure,id+": frozen infrastructure only");continue;}
 Check(P.Decide(true,true,mode,false,false,false,true)==ControlAdmission.Reject,id+": foreign actor rejected");
 Check(P.Decide(true,true,mode,true,true,false,true)==ControlAdmission.Reject,id+": stale explicit ticket never falls back to unscoped compatibility");
 Check(P.Decide(true,true,mode,true,true,true,true)==ControlAdmission.ExactOrigin,id+": valid exact origin admitted");
 Check(P.Decide(true,true,mode,true,false,false,false)==ControlAdmission.Reject,id+": denied original authority cannot be borrowed");
 if(mode==ControlSiteMode.ExactScope){strict++;Check(P.Decide(true,true,mode,true,false,false,true)==ControlAdmission.Reject,id+": missing mandatory scope rejected");}
 else if(mode==ControlSiteMode.ReviewedLegacy){compat++;Check(P.Decide(true,true,mode,true,false,false,true)==ControlAdmission.ReviewedCompatibility,id+": original unscoped authority retained with bound actor");}
 else throw new Exception("unreviewed catalog mode");
}
Check(strict==489&&compat==404&&infra==29,"three removed friendly push/reset infrastructure sites replaced by three exact-scoped queue hold sites; all 404 compatibility sites retained");
for(int m=3;m<16;m++)Check(P.Decide(true,true,(ControlSiteMode)m,true,true,true,true)==ControlAdmission.Reject,"unknown future mode must be reviewed "+m);
string hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(P).Assembly.Location)));
if(scan.GetProperty("dllSha256").GetString()!=hash)throw new Exception("catalog and policy must come from same actual DLL");
File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=true,checks=cases.Count,strict,compat,infra,dllSha256=hash,physicsSimulated=false,cases},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS {cases.Count} catalog-bound compatibility refusal checks; strict={strict} compat={compat} infra={infra}");
