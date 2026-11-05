param([string]$Dll,[string]$Output=(Join-Path $PSScriptRoot 'motion-evidence-il.json'))
$ErrorActionPreference='Stop'
[Reflection.Assembly]::LoadFrom('D:/Games/SIT_DIR/mod or addon builds/dnSpy-net-win64/bin/dnlib.dll')|Out-Null
$m=[dnlib.DotNet.ModuleDefMD]::Load((Resolve-Path -LiteralPath $Dll).Path);$r=$m.GetTypes()|Where-Object FullName -eq 'BotTraversal.Core.FollowerTraversalTestRuntime'
$script:n=0;$script:errors=[Collections.Generic.List[string]]::new()
function Check($v,$s){$script:n++;if(!$v){$script:errors.Add($s)}}
function Method($s){$r.Methods|Where-Object Name -eq $s|Select-Object -First 1}
function Calls($f,$s){if(!$f -or !$f.HasBody){return @()};@($f.Body.Instructions|Where-Object{$_.OpCode.Name -in @('call','callvirt') -and $_.Operand.Name -eq $s})}
function Field($f,$s){if(!$f -or !$f.HasBody){return @()};@($f.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldfld' -and $_.Operand.Name -eq $s})}
$read=Method 'TryObserveCapabilityJumpBodyVelocity';$observe=Method 'ObserveCapabilityPlanarBodyVelocity'
foreach($name in @('TryGetActiveFollowerRouteMission','TryValidateV2CurrentTypedStep','TryValidateV2AdjacentPhysicalContinuationBoundary','IsCapabilityRouteRetrySafe','PeekExactGeneration','BodyVelocityObservation','PreviousVelocityAnchor')){Check ((Calls $read $name).Count -eq 1) ('shared observation requires '+$name)}
foreach($name in @('Generation','MissionId','LearningV375MovementGeneration','LearningV375RouteMissionId','LearningV375TransactionActive','LearningV375LastBodySampleAt','PreviousAt','PreviousPosition','JumpAttempted')){Check ((Field $read $name).Count -ge 1) ('read requires exact passive origin and time '+$name)}
Check ((Calls (Method 'GetLearningV329PlanarSpeed') 'GetCapabilityJumpPlanarVelocity').Count -eq 1) 'all existing scalar speed consumers share reconciled observation'
foreach($name in @('TryGetLearningV460JumpAdmissionGeometry','TryGetLearningV353JumpRouteFunnelTarget','TryGetLearningV448LiveJumpOpportunity','TryBuildLearningV448MonotonicJumpOpportunityTarget','TryTriggerLearningV354RollingJumpActuatorLead','ShouldDeferCapabilityJumpProbe','IsCapabilityColdRunJumpTrialReady','TryObserveCapabilityJumpContactWindow')){Check ((Calls (Method $name) 'GetCapabilityJumpPlanarVelocity').Count -eq 1) ('physical heading and speed consumer '+$name)}
Check ((Calls (Method 'UpdateLearningV375PersistentExecutablePhysicalRouteTransaction') 'ObserveCapabilityPlanarBodyVelocity').Count -eq 1) 'existing physical observer captures bounded body interval'
Check ((Calls $observe 'RotateVelocityAnchor').Count -eq 1) 'observer retains previous interval instead of one frame alias'
Check (@($observe.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'stfld' -and $_.Operand.Name -eq 'CapabilityBodyVelocity'}).Count -eq 2) 'only existing cursor receives passive anchors'
foreach($name in @('MovementContextTryJump','PlayerMove','TryMovementContextApplyApproachMotion','SetMovementContextSprintSpeed','TryPlayerTeleport','TryRequestCapabilityFullRouteRetry','PublishCapabilityCompletion','GetLearningV329PlanarSpeed')){Check ((Calls $read $name).Count -eq 0 -and (Calls $observe $name).Count -eq 0) ('measurement cannot mutate engine, progress or recursively read '+$name)}
$lane=Method 'TryGetLearningV363ActuatorRouteEntryLaneTarget';$face=Method 'TryGetCapabilityVaultIngressFacing'
Check (@($lane.Body.Instructions|Where-Object{$_.OpCode.Name -eq 'ldc.r4' -and [Math]::Abs([double]$_.Operand-.16) -lt .000001}).Count -ge 1) 'Vault lane retains narrow native .16 fallback instead of .22 Climb fallback'
Check ((Calls $lane 'VaultPortalExcess').Count -eq 1 -and (Calls $lane 'VaultSideStep').Count -eq 1) 'Vault lane corrects nearest feasible portal excess only'
Check ((Calls $face 'VaultPortalExcess').Count -eq 1 -and (Calls $face 'GetLearningV357ActuatorEntryLateralTolerance').Count -eq 1) 'Vault facing and speed share same learned portal tolerance'
Check ((Calls (Method 'ApplyCollisionActivePhysicalRouteMotion') 'TryGetCapabilityVaultIngressFacing').Count -eq 1) 'existing motion gateway still holds native axis'
$policy=$m.GetTypes()|Where-Object Name -eq 'FollowerCapabilityApproachPolicy';$speed=$policy.Methods|Where-Object Name -eq 'ReconcileJumpVelocity';$portal=$policy.Methods|Where-Object Name -eq 'VaultPortalExcess'
Check ((Calls $speed 'UnsafeJumpSpeed').Count -eq 2) 'raw and physical overspeed use same retained protection'
Check ((Calls $portal 'Sqrt').Count -eq 1 -and (Calls $portal 'Min').Count -eq 1 -and (Calls $portal 'Max').Count -eq 1) 'portal remains within original Euclidean trigger and learned lateral'
Check ((Calls (Method 'ShouldDeferCapabilityJumpProbe') 'UnsafeJumpSpeed').Count -eq 1 -and (Calls (Method 'IsCapabilityJumpTriggerReady') 'UnsafeJumpSpeed').Count -eq 1) 'both preallocation and final native consumer retain unsafe speed veto'
$h=[Security.Cryptography.SHA256]::Create();try{$sha=[BitConverter]::ToString($h.ComputeHash([IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $Dll).Path))).Replace('-','')}finally{$h.Dispose()}
@{passed=($script:errors.Count -eq 0);checks=$script:n;errors=@($script:errors);dllSha256=$sha;physicsSimulated=$false}|ConvertTo-Json -Depth 5|Set-Content -Encoding UTF8 -LiteralPath $Output
Write-Output "RC118 motion evidence boundary: checks=$script:n failures=$($script:errors.Count)";$m.Dispose();if($script:errors.Count){$script:errors;exit 1}

