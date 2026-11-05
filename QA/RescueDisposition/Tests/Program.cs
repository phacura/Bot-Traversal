using System.Text.Json;
using System.Security.Cryptography;
using L=BotTraversal.Core.FollowerTraversalLifecycle;
var cases=new List<string>();void Check(bool v,string n){if(!v)throw new Exception(n);cases.Add(n);}
// Exercise the production lifecycle DLL, with the recorded original semantic birth ticket.
var id=new L.Identity("recordedBot",2,32,0,8,224,8,0,7);
var step=new L.StepState();var probe=new L.ProbeState(id,step);var mission=new L.MissionState();
Check(step.Attach(probe),"original Jump probe attaches to its immutable step");
Check(probe.Matches(id,step),"current original lifecycle remains valid during physical substitution");
var substitute=new L.Identity("recordedBot",2,32,0,0,130,0,0,7);
Check(!probe.Matches(substitute,step),"Vault strategy is not a replacement semantic receipt");
Check(probe.PublishCompletion(id,step,L.ActionOutcome.Unproven)==L.CommitResult.Rejected,"unproven rescue cannot publish completion");
mission.RequestFullRetry();
Check(mission.Status==L.Phase.Recovering && mission.Cursor==0,"existing full retry retains semantic cursor");
Check(step.DebtOutstanding && !probe.CompletionPublished,"retry does not turn rescue into Native Jump success");
Check(mission.Resolve(step,id,step,id,L.Resolution.Subsumed,true,true)==L.CommitResult.Rejected,"late geometry cannot consume debt while returning");
Check(!mission.CanHandoffToFollow(1,true,true),"failed rescue cannot release normal follow");
probe.Retire();
Check(probe.Retired && step.Probe==null && probe.Outcome==L.ActionOutcome.Aborted,"existing retirement detaches failed probe");
Check(step.DebtOutstanding && mission.Cursor==0,"retirement retains unresolved original passage");
Check(probe.PublishCompletion(id,step,L.ActionOutcome.NativePhysical)==L.CommitResult.Rejected,"late old callback cannot award success");
mission.RequestFullRetry();Check(mission.Cursor==0,"duplicate full retry does not advance route");
// Validate the supplied trace as evidence, not as a simulation of the new runtime.
var rows=File.ReadLines(args[1]).Select(x=>JsonDocument.Parse(x).RootElement.Clone()).ToArray();
string Type(JsonElement x)=>x.GetProperty("eventType").GetString()!;
JsonElement P(JsonElement x)=>x.GetProperty("payload");
bool Edge(JsonElement x,int edge)=>P(x).TryGetProperty("edgeId",out var e)&&e.ValueKind==JsonValueKind.Number&&e.GetInt32()==edge;
var summaries=rows.Where(x=>Type(x)=="learningV322TraversalAttemptSummary").ToArray();
Check(rows.Length==6382,"frozen actual raid has 6382 valid events");
Check(summaries.Count(x=>P(x).GetProperty("executionTruth").GetString()=="NativePhysical")==13,"actual thirteen NativePhysical summaries retained as observations");
Check(summaries.Count(x=>P(x).GetProperty("executionTruth").GetString()=="Unproven")==1,"one actual unproven terminal");
var bad=summaries.Single(x=>Edge(x,224));
Check(P(bad).GetProperty("executionTruth").GetString()=="Unproven","original224 rejected rather than successful Jump");
Check(P(bad).GetProperty("kind").GetString()=="Vault","original Jump receipt ended with physical Vault");
Check(!rows.Any(x=>Type(x)=="capabilityFullRouteRetryRequested"),"old raid never entered sanctioned full retry");
Check(rows.Count(x=>Type(x)=="learningV414JumpTakeoffCapabilitySnapshot")==6,"six actual native takeoffs in RC119 trace");
Check(!rows.Any(x=>Type(x)=="learningV414JumpTakeoffCapabilitySnapshot"&&Edge(x,224)),"rescue case did not execute native Jump224");
File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=true,checks=cases.Count,cases,dllSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(L).Assembly.Location))),traceSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[1]))),physicsSimulated=false},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS {cases.Count} RC120 actual lifecycle and frozen evidence checks");
