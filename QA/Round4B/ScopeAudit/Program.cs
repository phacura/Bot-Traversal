using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;
if(args[0]=="--format") { foreach(var file in args.Skip(1)) File.WriteAllText(file,CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot().NormalizeWhitespace().ToFullString()+"\n",new System.Text.UTF8Encoding(true)); return; }
var root=Path.GetFullPath(args[0]);
var refs=JsonDocument.Parse(File.ReadAllText(args[1])).RootElement.GetProperty("Items").GetProperty("ReferencePath").EnumerateArray().Select(x=>MetadataReference.CreateFromFile(x.GetProperty("Identity").GetString()!)).ToArray();
var trees=Directory.GetFiles(Path.Combine(root,"src"),"*.cs",SearchOption.AllDirectories).Select(f=>CSharpSyntaxTree.ParseText(File.ReadAllText(f),new CSharpParseOptions(LanguageVersion.Latest),f)).ToArray();
var comp=CSharpCompilation.Create("ControlGate",trees,refs,new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,allowUnsafe:true));
var errors=comp.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
if(errors.Length>0){foreach(var e in errors.Take(8))Console.Error.WriteLine(e);throw new Exception($"semantic errors {errors.Length}");}
var sourceTypes=trees.SelectMany(t=>t.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Select(n=>comp.GetSemanticModel(t).GetDeclaredSymbol(n))).OfType<INamedTypeSymbol>().ToArray();var interfaceBindings=new Dictionary<string,string[]>();var literalNames=trees.SelectMany(t=>t.GetRoot().DescendantTokens().Where(t=>t.IsKind(SyntaxKind.StringLiteralToken)).Select(t=>t.ValueText)).ToHashSet();var methodNodes=new List<object>();var methodCalls=new List<object>();var methodGroups=new HashSet<string>();var constantRuleArguments=new List<object>();var scopeProofs=new List<object>();var authorityCalls=new List<object>();var policyErrors=new List<string>();var policyFiles=new HashSet<string>();var observations=new List<object>();var direct=new List<object>();var routed=new List<object>();var writes=new List<object>();var dynamicSites=new List<object>();var backendWrites=new List<object>();
foreach(var tree in trees){var m=comp.GetSemanticModel(tree);foreach(var n in tree.GetRoot().DescendantNodes()){
 string file=Path.GetRelativePath(root,tree.FilePath).Replace('\\','/');
 if(n is MethodDeclarationSyntax declaration&&m.GetDeclaredSymbol(declaration) is IMethodSymbol declared)methodNodes.Add(new{key=declared.OriginalDefinition.ToDisplayString(),name=declared.Name,type=declared.ContainingType.ToDisplayString(),file,isPrivate=declared.DeclaredAccessibility==Accessibility.Private,eligible=(declared.DeclaredAccessibility is Accessibility.Private or Accessibility.Internal||declared.ContainingType.DeclaredAccessibility==Accessibility.Private)&&!declared.IsVirtual&&!declared.IsOverride&&!declared.IsAbstract&&!declared.IsExtern,namedRoot=literalNames.Contains(declared.Name)||new[]{"Awake","Start","Update","LateUpdate","FixedUpdate","OnGUI","OnDestroy","OnEnable","OnDisable","OnDrawGizmos"}.Contains(declared.Name),attributed=declaration.AttributeLists.Count>0});
 if(n is IdentifierNameSyntax reference&&m.GetSymbolInfo(reference).Symbol is IMethodSymbol referenced&&referenced.ContainingAssembly?.Name=="ControlGate"){
  var invoke=reference.Ancestors().OfType<InvocationExpressionSyntax>().FirstOrDefault();
  if(invoke==null||!invoke.Expression.Span.Contains(reference.Span))methodGroups.Add(referenced.OriginalDefinition.ToDisplayString());
 }
 bool backend=file=="src/Core/FollowerControlBackend.Generated.cs"||file=="src/Core/FollowerControlLocomotionBackend.cs";
 if(n is MemberAccessExpressionSyntax group && !(group.Parent is InvocationExpressionSyntax parentCall && parentCall.Expression==group) && m.GetSymbolInfo(group).Symbol is IMethodSymbol gm && External(gm.ContainingAssembly?.Name,gm.ContainingType.ToDisplayString())){
  observations.Add(new{file,type=gm.ContainingType.ToDisplayString(),name=gm.Name,signature=gm.OriginalDefinition.ToDisplayString(),assembly=gm.ContainingAssembly?.Name,code=group.ToString(),line=tree.GetLineSpan(group.Span).StartLinePosition.Line+1});
 }
 if(n is InvocationExpressionSyntax i){var symbol=m.GetSymbolInfo(i).Symbol as IMethodSymbol;
  if(symbol==null){if(m.GetTypeInfo(i.Expression).Type?.TypeKind==TypeKind.Dynamic)dynamicSites.Add(new{file,code=i.ToString()});continue;}
  var type=symbol.ContainingType.ToDisplayString();
  if(symbol.ContainingAssembly?.Name=="ControlGate"){
   var targets=new[]{symbol.OriginalDefinition.ToDisplayString()};
   if(symbol.ContainingType.TypeKind==TypeKind.Interface){
    var id=symbol.OriginalDefinition.ToDisplayString();
    if(!interfaceBindings.TryGetValue(id,out var implementations)){implementations=sourceTypes.Select(t=>t.FindImplementationForInterfaceMember(symbol.OriginalDefinition)).OfType<IMethodSymbol>().Select(x=>x.OriginalDefinition.ToDisplayString()).Distinct().ToArray();interfaceBindings[id]=implementations;}
    targets=targets.Concat(implementations).ToArray();
   }
   foreach(var target in targets)methodCalls.Add(new{target,caller=(m.GetEnclosingSymbol(i.SpanStart) as IMethodSymbol)?.OriginalDefinition.ToDisplayString(),scoped=ScopeProof(i,m),callback=i.Ancestors().TakeWhile(a=>a is not MethodDeclarationSyntax).Any(a=>a is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax),file,line=tree.GetLineSpan(i.Span).StartLinePosition.Line+1});
  }
  if(symbol.ContainingType.Name is "FollowerCapabilityDecisionRules" or "FollowerCapabilityHelperRules" && m.GetOperation(i) is Microsoft.CodeAnalysis.Operations.IInvocationOperation operation)
   foreach(var argument in operation.Arguments.Where(a=>a.Parameter?.Type.SpecialType==SpecialType.System_Boolean))constantRuleArguments.Add(new{method=symbol.ToDisplayString(),parameter=argument.Parameter!.Name,ordinal=argument.Parameter.Ordinal,value=argument.Value.ConstantValue.HasValue?argument.Value.ConstantValue.Value:null});
  if(type=="BotTraversal.Core.FollowerMovementWriterAuthority" && (symbol.Name.StartsWith("TryEnter")||symbol.Name.StartsWith("Begin")||symbol.Name.StartsWith("Acquire")||symbol.Name=="Enter"||symbol.Name=="ContinueTraversal"||symbol.Name=="RetireExactToFollow"||symbol.Name.StartsWith("Transition")||symbol.Name=="Reset"||symbol.Name=="ClearAll"))
   authorityCalls.Add(new{file,method=m.GetEnclosingSymbol(i.SpanStart)?.ToDisplayString(),callee=symbol.Name,arguments=i.ArgumentList.Arguments.Select(a=>a.ToString()).ToArray(),line=tree.GetLineSpan(i.Span).StartLinePosition.Line+1});
  bool pure=Path.GetFileName(file).StartsWith("FollowerCapability")&&(file.EndsWith("Policy.cs")||file.EndsWith("Rules.Generated.cs"));
  if(pure){policyFiles.Add(file);if(External(symbol.ContainingAssembly?.Name,type))policyErrors.Add(file+":"+i.ToString());}

  if(type=="BotTraversal.Core.FollowerControlBackend" && symbol.Name!="EnterPhase" && symbol.Name!="Authorize" && symbol.Name!="TargetBelongsTo" && symbol.Name!="FindSite"){
   var proof=ScopeProof(i,m);if(proof!=null)scopeProofs.Add(new{file,site=i.ArgumentList.Arguments[0].Expression.ToString().Trim('"'),method=m.GetEnclosingSymbol(i.SpanStart)?.ToDisplayString(),proof});
   routed.Add(new{file,enclosing=m.GetEnclosingSymbol(i.SpanStart)?.ToDisplayString(),backend=symbol.Name,code=i.ToString(),arguments=i.ArgumentList.Arguments.Select(a=>a.ToString()).ToArray(),line=tree.GetLineSpan(i.Span).StartLinePosition.Line+1, normalized=i.NormalizeWhitespace().ToFullString()});
  }
  if(External(symbol.ContainingAssembly?.Name,type)){
   var row=new{file,type,name=symbol.Name,signature=symbol.OriginalDefinition.ToDisplayString(),assembly=symbol.ContainingAssembly?.Name,code=i.ToString(),line=tree.GetLineSpan(i.Span).StartLinePosition.Line+1};
   if(backend)direct.Add(row);else observations.Add(row);
  }
 }
 SyntaxNode? lhs=n is AssignmentExpressionSyntax a?a.Left:n is PostfixUnaryExpressionSyntax p && p.IsKind(SyntaxKind.PostIncrementExpression)||n is PostfixUnaryExpressionSyntax q && q.IsKind(SyntaxKind.PostDecrementExpression)?((PostfixUnaryExpressionSyntax)n).Operand:n is PrefixUnaryExpressionSyntax pre && (pre.IsKind(SyntaxKind.PreIncrementExpression)||pre.IsKind(SyntaxKind.PreDecrementExpression))?pre.Operand:null;
 if(lhs!=null){var sym=m.GetSymbolInfo(lhs).Symbol;if(sym!=null&&External(sym.ContainingAssembly?.Name,sym.ContainingType?.ToDisplayString()??"")){
 var receiver=lhs is MemberAccessExpressionSyntax ma ? m.GetSymbolInfo(ma.Expression).Symbol:null;
 var row=new{file,type=sym.ContainingType?.ToDisplayString(),signature=sym.ToDisplayString(),code=n.ToString(),localValue=receiver is ILocalSymbol || receiver is IParameterSymbol, line=tree.GetLineSpan(n.Span).StartLinePosition.Line+1};
 if(backend)backendWrites.Add(row);else writes.Add(row);
}}
}}
File.WriteAllText(args[2],JsonSerializer.Serialize(new{observations,direct,routed,writes,backendWrites,dynamicSites,authorityCalls,policyFiles,policyErrors,scopeProofs,constantRuleArguments,methodNodes,methodCalls,methodGroups,interfaceBindings},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"Resolved all source: external observations={observations.Count}, backend operations={direct.Count}, route calls={routed.Count}, residual assignments={writes.Count}, dynamic={dynamicSites.Count}");
bool External(string? assembly,string type)=>assembly=="Assembly-CSharp"||assembly=="AnimationSystem.Types"||assembly?.StartsWith("UnityEngine")==true||type.StartsWith("System.Reflection.")||type=="System.Delegate";

if(policyErrors.Count>0)throw new Exception("capability policies call engine/reflection: "+string.Join(";",policyErrors));

string? ScopeProof(InvocationExpressionSyntax call,SemanticModel model){
 var factories=new HashSet<string>{"TryEnterBoundPhysicalMotion","TryEnterFollowPhysicalMotion","TryEnterVerifiedRouteIntent","TryEnterV2CurrentIngressScope","TryEnterV2CurrentTypedTraversalScope","TryEnterCapabilityFollowRecoveryScope","TryEnterCapabilityFreshFollowScope","TryEnterCapabilityRouteProducerScope","TryEnterCapabilityRoutePhysicalScope","TryEnterCapabilityClimbIngressScope"};
 bool Form(ExpressionSyntax e,InvocationExpressionSyntax target,bool positive){
  if(e is ParenthesizedExpressionSyntax p)return Form(p.Expression,target,positive);
  if(positive&&e==target)return true;
  if(!positive&&e is PrefixUnaryExpressionSyntax n&&n.IsKind(SyntaxKind.LogicalNotExpression))return Form(n.Operand,target,true);
  if(e is BinaryExpressionSyntax b&&b.IsKind(positive?SyntaxKind.LogicalAndExpression:SyntaxKind.LogicalOrExpression))return Form(b.Left,target,positive)||Form(b.Right,target,positive);
  return false;
 }
 bool Exits(StatementSyntax st)=>st is ReturnStatementSyntax or ThrowStatementSyntax||st is BlockSyntax b&&b.Statements.LastOrDefault() is ReturnStatementSyntax or ThrowStatementSyntax;
 foreach(var use in call.Ancestors().OfType<UsingStatementSyntax>()){
  if(call.Ancestors().TakeWhile(x=>x!=use).Any(x=>x is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))continue;
  var e=use.Expression;if(e==null||model.GetTypeInfo(e).Type?.ToDisplayString()!="BotTraversal.Core.FollowerMovementWriterAuthority.WriteScope")continue;
  if(e is InvocationExpressionSyntax enter&&model.GetSymbolInfo(enter).Symbol is IMethodSymbol em&&em.Name=="Enter"&&em.ContainingType.ToDisplayString()=="BotTraversal.Core.FollowerMovementWriterAuthority")return "explicit authority Enter resource";
  if(model.GetSymbolInfo(e).Symbol is not ILocalSymbol local)continue;
  foreach(var reference in local.DeclaringSyntaxReferences){
   var syntax=reference.GetSyntax();var arg=syntax.AncestorsAndSelf().OfType<ArgumentSyntax>().FirstOrDefault();
   if(arg?.Parent?.Parent is not InvocationExpressionSyntax issue||model.GetSymbolInfo(issue).Symbol is not IMethodSymbol symbol||!factories.Contains(symbol.Name)||(symbol.ContainingType.ToDisplayString()!="BotTraversal.Core.FollowerMovementWriterAuthority"&&symbol.ContainingType.ToDisplayString()!="BotTraversal.Core.FollowerTraversalTestRuntime"))continue;
   var guard=issue.Ancestors().OfType<IfStatementSyntax>().FirstOrDefault();if(guard==null)continue;
    var owner=use.Ancestors().OfType<MethodDeclarationSyntax>().First();
    bool changed=owner.DescendantNodes().Where(n=>n.SpanStart>issue.Span.End&&n.SpanStart<call.SpanStart).Any(n=>
     n is AssignmentExpressionSyntax a&&SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(a.Left).Symbol,local)||
     n is InvocationExpressionSyntax invocation&&invocation.Expression is MemberAccessExpressionSyntax member&&member.Name.Identifier.ValueText=="Dispose"&&SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(member.Expression).Symbol,local)||
     n is ArgumentSyntax argument&&argument.RefKindKeyword.Kind() is SyntaxKind.OutKeyword or SyntaxKind.RefKeyword&&SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(argument.Expression).Symbol,local));
    if(changed)continue;
    if(use.Ancestors().Contains(guard)&&Form(guard.Condition,issue,true))return "positive guarded "+symbol.Name;
    if(guard.Parent==use.Parent&&guard.Span.End<=use.Span.Start&&Exits(guard.Statement)&&Form(guard.Condition,issue,false))return "negative exit guard "+symbol.Name;
  }
 }
 return null;
}
