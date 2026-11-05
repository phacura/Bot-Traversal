param([string]$Dll,[string]$Output=(Join-Path $PSScriptRoot 'coordination-il.json'))
$ErrorActionPreference='Stop'
[Reflection.Assembly]::LoadFrom('D:/Games/SIT_DIR/mod or addon builds/dnSpy-net-win64/bin/dnlib.dll')|Out-Null
$m=[dnlib.DotNet.ModuleDefMD]::Load((Resolve-Path -LiteralPath $Dll).Path);$r=$m.GetTypes()|Where-Object FullName -eq 'BotTraversal.Core.FollowerTraversalTestRuntime'
$script:n=0;$script:errors=[Collections.Generic.List[string]]::new()
function Check($v,$s){$script:n++;if(!$v){$script:errors.Add($s)}}
function Method($s){$r.Methods|Where-Object Name -eq $s|Select-Object -First 1}
function Calls($f,$s){if(!$f -or !$f.HasBody){return @()};@($f.Body.Instructions|Where-Object{$_.OpCode.Name -in @('call','callvirt') -and $_.Operand.Name -eq $s})}
function Before($f,$a,$b){$x=@(Calls $f $a);$y=@(Calls $f $b);$x.Count -gt 0 -and $y.Count -gt 0 -and $x[0].Offset -lt $y[0].Offset}
function Fields($f,$name,$op='ldfld'){if(!$f -or !$f.HasBody){return @()};@($f.Body.Instructions|Where-Object{$_.OpCode.Name -eq $op -and $_.Operand.Name -eq $name})}
$request=$r.Methods|Where-Object{$_.Name -eq 'TryRequestCapabilityFullRouteRetry' -and $_.MethodSig.Params[0].FullName -eq 'EFT.BotOwner'}
Check (Before $request 'TryAdvanceCapabilityFailureSequence' 'RequestFullRetry') 'ladder precedes return transaction; no immediate root rollback'
$advance=Method 'TryAdvanceCapabilityFailureSequence';$select=Method 'TrySelectCapabilitySequenceStrategy';$tick=Method 'TickCapabilityFailureSequence';$native=Method 'TryDispatchCapabilitySequenceNative';$start=Method 'TryStartProbeForBot'
foreach($name in @('AfterFailure','ClearCapabilityStrategyTargets','TrySelectCapabilitySequenceStrategy','ForceReleaseTraversalProbe','IsProbeLifecycleCurrent')){Check ((Calls $advance $name).Count -ge 1) ('monotonic failure transition requires '+$name)}
Check (Before $advance 'ForceReleaseTraversalProbe' 'ClearCapabilityStrategyTargets') 'old active probe retires before physical strategy replacement'
Check (Before $advance 'ClearCapabilityStrategyTargets' 'AfterFailure') 'old target conflict removed before next stage'
Check ((Fields $advance 'RouteRetry' 'stfld').Count -eq 1) 'return plan rebuilt from current unresolved suffix'
foreach($name in @('Rank','ForwardCandidate','CalculateStrictPath','GetTraversalExperienceEffectiveScore','GetTraversalPhysicalSuccessCount')){Check ((Calls $select $name).Count -ge 1) ('learned route selection consumes '+$name)}
foreach($name in @('ShouldSuppressTraversalByExperience','IsTraversalEdgeQuarantinedForFollower','ShouldPreferAssistedExecutor')){Check ((Calls $select $name).Count -eq 0) ('self rescue not silently vetoed by '+$name)}
Check ((Fields $select 'EdgeId' 'stfld').Count -eq 0) 'physical candidate never overwrites semantic edge birth identity'
Check ((Fields $select 'SkillTemplateEdgeId' 'stfld').Count -eq 1) 'physical candidate stored as separate skill template'
Check ((Calls $select 'CapabilitySemanticCommitment').Count -ge 1) 'candidate binds current semantic mission step'
Check ((Calls $start 'TryDispatchCapabilitySequenceNative').Count -eq 1) 'all route-backed physical primary actions reach native-first dispatcher'
Check (Before $start 'TryDispatchCapabilitySequenceNative' 'ShouldPreferAssistedExecutor') 'learned assist preference cannot precede requested Native attempt'
Check ((Calls $native 'IsProbeLifecycleCurrent').Count -eq 1) 'native-first dispatcher requires current original lifecycle'
$guard=@(Calls $native 'IsProbeLifecycleCurrent');$guardBranch=$false
if($guard.Count -eq 1){$idx=$native.Body.Instructions.IndexOf($guard[0]);$guardBranch=$native.Body.Instructions[$idx+1].OpCode.Name.StartsWith('brtrue')}
Check $guardBranch 'stale native receipt rejected before execution'
foreach($name in @('TryBeginLearnedNativeJump','TryBeginInjectedNativeVault','TriggerNativeVaultScan','TryRequestCapabilityFullRouteRetry')){Check ((Calls $native $name).Count -eq 1) ('native execution and refusal use existing consumer '+$name)}
Check (Before $native 'TryEnterBoundPhysicalMotion' 'TryBeginLearnedNativeJump') 'native-first consumer owns exact issued probe scope before actuation'
Check ((Calls $native 'TryCommitCapabilityJumpAtDispatch').Count -eq 1) 'RC119 same-frame Jump opportunity preserved'
Check ((Calls $native 'TryBeginNativeDropDown').Count -eq 1) 'alternate DropDown uses existing typed physical consumer'
Check ((Calls (Method 'TryBeginLearningV32NativeEntryReacquire') 'IsCapabilityForwardFailurePhase').Count -eq 1) 'forward-only phases cannot stage behind the current body'
Check ((Calls (Method 'TryRecoverVaultClimbCrossingBeforeEgress') 'TryRequestCapabilityFullRouteRetry').Count -eq 1) 'first failed native crossing reaches sequence before old repeated recovery'
$recover=Method 'TryRecoverPlayerValidatedTraversal';Check (Before $recover 'TryRequestCapabilityFullRouteRetry' 'IsPlayerValidatedTraversalProbe') 'native refusal enters ladder before old player-validation/assist veto'
$capture=Method 'CaptureCapabilityRouteRetryPlan';Check ((Calls $capture 'get_CurrentStepIndex').Count -ge 1 -and (Calls $capture 'Skip').Count -ge 1) 'completed prefix excluded from retry snapshot'
$return=Method 'TickCapabilityFullRouteRetry';Check ((Calls $return 'PrepareCapabilityAlternateReplay').Count -eq 1) 'staged return issues one alternate replay phase'
Check ((Calls $tick 'PrepareForAutomaticRecoveryTeleport').Count -eq 1 -and (Calls $tick 'TryTeleport').Count -eq 1) 'last failure phase uses existing safe recovery teleport'
Check ((Calls $tick 'IsCapabilityRouteRetrySafe').Count -ge 1) 'commands combat and medical retain priority'
$walking=Method 'IsCapabilityRetryWalking';Check ((Calls $walking 'WalkingOnly').Count -eq 1) 'whole return and alternate replay share one walking phase policy'
$backend=$m.GetTypes()|Where-Object FullName -eq 'BotTraversal.Core.FollowerControlBackend'
foreach($name in @('BotMoverSprint','PlayerEnableSprint','SetBotMoverSprinting')){
 $f=$backend.Methods|Where-Object Name -eq $name
 Check ((Calls $f 'IsCapabilityRetryWalking').Count -eq 1) ('every routed sprint setter clamps retry '+$name)
 Check ((Calls $f 'Authorize').Count -eq 1) ('sprint clamp does not bypass origin authorization '+$name)
}
$nativeAuthority=$m.GetTypes()|Where-Object Name -eq 'FollowerMovementWriterAuthorityPatch'
foreach($name in @('PatchBotOwnerSprint','PatchBotMoverSprint','PatchPlayerEnableSprint')){
 $f=$nativeAuthority.Methods|Where-Object Name -eq $name
 Check (Before $f 'IsCapabilityRetryWalking' 'ShouldHardBlockSprintDisable') ('native/PIT sprint callbacks cannot re-arm old lease '+$name)
 Check ((Calls $f 'Allow').Count -eq 2) ('native walking disable still requires original authority '+$name)
}
Check ((Calls $walking 'IsCapabilityRouteRetrySafe').Count -eq 1) 'walking-only movement restriction yields to higher priority medical combat or explicit commands'
$queue=Method 'TickCapabilityPortalWait';$hold=Method 'CommitCapabilityPortalHold'
foreach($name in @('Admit','get_RegisteredFollowers','IsCapabilityRouteRetrySafe','TryEnterBoundPhysicalMotion','TryEnterFollowPhysicalMotion')){Check ((Calls $queue $name).Count -ge 1) ('portal queue requires '+$name)}
Check ((Calls $queue 'Closer').Count -eq 2) 'armed and unarmed followers share actual tested nearest policy'
Check ((Calls $start 'TickCapabilityPortalWait').Count -eq 1) 'native and WT allocation cannot bypass queue'
$update=Method 'OnBotOwnerUpdate';Check (Before $update 'TickCapabilityFullRouteRetry' 'UpdateProbe') 'single coordinator entry precedes physical consumers'
Check (Before $return 'TickCapabilityPortalWait' 'ForceReleaseTraversalProbe') 'queue admission checked before the existing return execution'
Check ((Calls $return 'TickCapabilityFailureSequence').Count -eq 1) 'same coordinator arbitrates staged failure'
Check ((Calls (Method 'DeferCapabilitySequenceFailure') 'ForceReleaseTraversalProbe').Count -eq 0) 'preempted failure cannot retire or restart a movement writer'
$resolve=Method 'TryAdvanceRouteMission';Check ((Calls $resolve 'Complete').Count -eq 1) 'one-second queue interval published at actual semantic step resolution'
Check ((Fields $hold 'PortalQueueHoldGeneration' 'ldfld').Count -eq 1 -and (Calls $hold 'get_CurPath').Count -ge 1) 'queue stop bound to actual held generation and path'
Check ((Calls (Method 'ClearLifecycleMissions') 'Clear').Count -ge 1) 'raid end clears portal admission state'
$clean=Method 'ClearCapabilityRouteWorkForTeleport';$reset=Method 'ResetFollowerMovementAuthorityForTeleport';$safe=$m.GetTypes()|Where-Object Name -eq 'FollowerSafeTeleport';$origin=$safe.Methods|Where-Object Name -eq 'TryTeleportAtOrigin'
Check (Before $reset 'ClearCapabilityRouteWorkForTeleport' 'BeginTeleport') 'manual and recovery reset retire work before new teleport origin'
Check (Before $origin 'ClearCapabilityRouteWorkForTeleport' 'BeginTeleport') 'standalone safe relocation also clears route work'
foreach($name in @('RetireLifecycleProbe','UninstallLifecycleProbe','ClearCapabilityStrategyTargets','Cancel','Retire','Remove')){Check ((Calls $clean $name).Count -ge 1) ('clean teleport requires '+$name)}
foreach($name in @('FollowerRouteMissions','FollowerTraversalRouteCursors','FollowerMovementRecoveryStates','FollowerWorldSafetyStates')){Check ((Fields $clean $name 'ldsfld').Count -ge 1) ('teleport clears actor-owned work '+$name)}
foreach($name in @('PostTraversalChaseLeases','LearningV430PendingPostTraversalFollowRollovers','TraversalChainLocomotionIntents','LearningV444TransitionMomentumEpochs')){Check ((Fields (Method 'ClearCapabilityStrategyTargets') $name 'ldsfld').Count -ge 1) ('handoff clears independent old target source '+$name)}
foreach($name in @('RestoreProbeInput','MarkFollowerTraversalEpisodeCompleted','MarkLearnedTraversalEdgeSuccess','PublishProbeLifecycleCompletion')){Check ((Calls $clean $name).Count -eq 0) ('clean teleport cannot restore stale input or award completion '+$name)}
Check ((Calls (Method 'ClearCapabilityStrategyTargets') 'CancelDelivery').Count -eq 1) 'legacy PIT accepted path lease cancelled on strategy and teleport handoff'
$soft=$m.GetTypes()|Where-Object Name -eq 'GenericSoftCollision'
Check (@($soft.Methods|Where-Object Name -eq 'FixedUpdate').Count -eq 0) 'stationary player repulsion physically removed'
Check (@($soft.Methods|Where-Object Name -eq 'DropCloseAvoidance').Count -eq 0) 'aggregate avoidance reset no longer erases unrelated offsets'
$friendly=$m.GetTypes()|Where-Object Name -eq 'FollowerFriendlyAvoidancePatch';$skip=$friendly.Methods|Where-Object Name -eq 'SkipFriendlyPair'
Check ((Calls $skip 'Owns').Count -eq 2 -and (Calls $skip 'PassThroughPermitted').Count -eq 2) 'native avoidance filter targets registered permitted friendly pairs only'
Check ((Calls $skip 'get_Value').Count -eq 1) 'native friendly avoidance follows pass-through setting'
$plugin=$m.GetTypes()|Where-Object Name -eq 'BotTraversalPlugin';$legacy=$plugin.Methods|Where-Object Name -eq 'get_LegacyPITCompatibilityEnabled'
Check ($legacy -and $legacy.Body.Instructions.Count -eq 2 -and $legacy.Body.Instructions[0].OpCode.Name -eq 'ldc.i4.1') 'legacy compatibility forced true in background'
Check (@($plugin.Fields|Where-Object Name -eq 'LegacyPITCompatibilityEnabled').Count -eq 0) 'legacy compatibility no longer a ConfigEntry option'
Check ((Calls ($plugin.Methods|Where-Object Name -eq 'Awake') 'Bind').Count -eq 3) 'only three user-facing configuration bindings remain'
foreach($f in @($advance,$select,$native,$hold)){
 foreach($name in @('MarkLearnedTraversalEdgeSuccess','PublishProbeLifecycleCompletion','TryPlayerTeleport')){Check ((Calls $f $name).Count -eq 0) ('coordination never fabricates success or bypasses safe relocation '+$name)}
}
$h=[Security.Cryptography.SHA256]::Create();try{$sha=[BitConverter]::ToString($h.ComputeHash([IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $Dll).Path))).Replace('-','')}finally{$h.Dispose()}
@{passed=($script:errors.Count -eq 0);checks=$script:n;errors=@($script:errors);dllSha256=$sha;physicsSimulated=$false}|ConvertTo-Json -Depth 6|Set-Content -Encoding UTF8 -LiteralPath $Output
Write-Output "RC121 coordination integration: checks=$script:n failures=$($script:errors.Count)";$m.Dispose();if($script:errors.Count){$script:errors;exit 1}
