// Migration-generated typed adapters; authoritative checked-in source.
// There is no build-time generation. Changes require boundary contract review.
using System;
using EFT;

namespace BotTraversal.Core
{
    internal static partial class FollowerControlBackend
    {
        internal static global::UnityEngine.AI.NavMeshPathStatus BotOwnerGoToPoint(this global::EFT.BotOwner controlTarget, string site, global::EFT.BotOwner bot, global::UnityEngine.Vector3 @position, bool @slowAtTheEnd = true, float @reachDist = -1f, bool @getUpWithCheck = false, bool @mustHaveWay = true, bool @mustGetUp = true, bool @onlyShortTrie = false, bool @force = false)
        {
            if (!Authorize(bot, controlTarget, site, "BotOwnerGoToPoint", "BotOwner.GoToPoint"))
            {
                return global::UnityEngine.AI.NavMeshPathStatus.PathInvalid;
            }

            return controlTarget.@GoToPoint(@position, @slowAtTheEnd, @reachDist, @getUpWithCheck, @mustHaveWay, @mustGetUp, @onlyShortTrie, @force);
        }

        internal static bool TryPlayerMove(this global::EFT.Player controlTarget, string site, global::EFT.BotOwner bot, global::UnityEngine.Vector2 @direction)
        {
            if (!Authorize(bot, controlTarget, site, "TryPlayerMove", "Player.Move"))
            {
                return false;
            }

            controlTarget.@Move(@direction);
            return true;
        }

        internal static bool TryMovementContextLastBlendMotionDelta(this global::EFT.MovementContext controlTarget, string site, global::EFT.BotOwner bot, global::UnityEngine.Vector3 value)
        {
            if (!Authorize(bot, controlTarget, site, "TryMovementContextLastBlendMotionDelta", "MovementContext.LastBlendMotionDelta"))
            {
                return false;
            }

            controlTarget.@LastBlendMotionDelta = value;
            return true;
        }

        internal static bool TryMovementContextApplyApproachMotion(this global::EFT.MovementContext controlTarget, string site, global::EFT.BotOwner bot, global::UnityEngine.Vector3 @motion, float @deltaTime)
        {
            if (!Authorize(bot, controlTarget, site, "TryMovementContextApplyApproachMotion", "MovementContext.ApplyApproachMotion"))
            {
                return false;
            }

            controlTarget.@ApplyApproachMotion(@motion, @deltaTime);
            return true;
        }

        internal static bool TryICharacterControllerMove(this global::ICharacterController controlTarget, string site, global::EFT.BotOwner bot, global::UnityEngine.Vector3 @motion, float @deltaTime)
        {
            if (!Authorize(bot, controlTarget, site, "TryICharacterControllerMove", "ICharacterController.Move"))
            {
                return false;
            }

            controlTarget.@Move(@motion, @deltaTime);
            return true;
        }

        internal static bool SetBotMoverSprinting(this global::BotMover controlTarget, string site, global::EFT.BotOwner bot, bool value)
        {
            if (FollowerTraversalTestRuntime.IsCapabilityRetryWalking(bot)) value=false;
            if (!Authorize(bot, controlTarget, site, "SetBotMoverSprinting", "BotMover.Sprinting"))
            {
                return value;
            }

            controlTarget.@Sprinting = value;
            return value;
        }

        internal static void PlayerEnableSprint(this global::EFT.Player controlTarget, string site, global::EFT.BotOwner bot, bool @enable)
        {
            if (FollowerTraversalTestRuntime.IsCapabilityRetryWalking(bot)) @enable=false;
            if (!Authorize(bot, controlTarget, site, "PlayerEnableSprint", "Player.EnableSprint"))
            {
                return;
            }

            controlTarget.@EnableSprint(@enable);
        }

        internal static object MethodBaseInvoke(this global::System.Reflection.MethodBase controlTarget, string site, global::EFT.BotOwner bot, object @obj, object[] @parameters)
        {
            if (!Authorize(bot, @obj, site, "MethodBaseInvoke", "MethodBase.Invoke"))
            {
                throw new InvalidOperationException("Control entry rejected reflection invocation");
            }

            return controlTarget.@Invoke(@obj, @parameters);
        }

        internal static void PropertyInfoSetValue(this global::System.Reflection.PropertyInfo controlTarget, string site, global::EFT.BotOwner bot, object @obj, object @value, object[] @index)
        {
            if (!Authorize(bot, @obj, site, "PropertyInfoSetValue", "PropertyInfo.SetValue"))
            {
                return;
            }

            controlTarget.@SetValue(@obj, @value, @index);
        }

        internal static void FieldInfoSetValue(this global::System.Reflection.FieldInfo controlTarget, string site, global::EFT.BotOwner bot, object @obj, object @value)
        {
            if (!Authorize(bot, @obj, site, "FieldInfoSetValue", "FieldInfo.SetValue"))
            {
                return;
            }

            controlTarget.@SetValue(@obj, @value);
        }

        internal static void BotMoverStop(this global::BotMover controlTarget, string site, global::EFT.BotOwner bot)
        {
            if (!Authorize(bot, controlTarget, site, "BotMoverStop", "BotMover.Stop"))
            {
                return;
            }

            controlTarget.@Stop();
        }

        internal static void BotOwnerStopMove(this global::EFT.BotOwner controlTarget, string site, global::EFT.BotOwner bot)
        {
            if (!Authorize(bot, controlTarget, site, "BotOwnerStopMove", "BotOwner.StopMove"))
            {
                return;
            }

            controlTarget.@StopMove();
        }

        internal static bool SetBotMoverPause(this global::BotMover controlTarget, string site, global::EFT.BotOwner bot, bool value)
        {
            if (!Authorize(bot, controlTarget, site, "SetBotMoverPause", "BotMover.Pause=" + value.ToString()))
            {
                return value;
            }

            controlTarget.@Pause = value;
            return value;
        }

        internal static void BotGoToPointDataSetPoint(this global::BotGoToPointData controlTarget, string site, global::EFT.BotOwner bot, global::UnityEngine.Vector3 @p)
        {
            if (!Authorize(bot, controlTarget, site, "BotGoToPointDataSetPoint", "BotGoToPointData.SetPoint"))
            {
                return;
            }

            controlTarget.@SetPoint(@p);
        }

        internal static void BotGoToPointDataUpdateToGo(this global::BotGoToPointData controlTarget, string site, global::EFT.BotOwner bot, bool @sprint, float @speed = 1f, float @pose = 1f)
        {
            if (!Authorize(bot, controlTarget, site, "BotGoToPointDataUpdateToGo", "BotGoToPointData.UpdateToGo"))
            {
                return;
            }

            controlTarget.@UpdateToGo(@sprint, @speed, @pose);
        }

        internal static void PatrollingDataPause(this global::PatrollingData controlTarget, string site, global::EFT.BotOwner bot)
        {
            if (!Authorize(bot, controlTarget, site, "PatrollingDataPause", "PatrollingData.Pause"))
            {
                return;
            }

            controlTarget.@Pause();
        }

        internal static void BotMoverTeleport(this global::BotMover controlTarget, string site, global::EFT.BotOwner bot, global::UnityEngine.Vector3 @rPosition)
        {
            if (!Authorize(bot, controlTarget, site, "BotMoverTeleport", "BotMover.Teleport"))
            {
                return;
            }

            controlTarget.@Teleport(@rPosition);
        }

        internal static void PlayerTeleport(this global::EFT.Player controlTarget, string site, global::EFT.BotOwner bot, global::UnityEngine.Vector3 @position, bool @onServerToo = false)
        {
            if (!Authorize(bot, controlTarget, site, "PlayerTeleport", "Player.Teleport"))
            {
                return;
            }

            controlTarget.@Teleport(@position, @onServerToo);
        }

        internal static void BotMoverSprint(this global::BotMover controlTarget, string site, global::EFT.BotOwner bot, bool @val, bool @withDebugCallback = true)
        {
            if (FollowerTraversalTestRuntime.IsCapabilityRetryWalking(bot)) @val=false;
            if (!Authorize(bot, controlTarget, site, "BotMoverSprint", "BotMover.Sprint"))
            {
                return;
            }

            controlTarget.@Sprint(@val, @withDebugCallback);
        }

        internal static bool SetBotMoverIsMoving(this global::BotMover controlTarget, string site, global::EFT.BotOwner bot, bool value)
        {
            if (!Authorize(bot, controlTarget, site, "SetBotMoverIsMoving", "BotMover.IsMoving"))
            {
                return value;
            }

            controlTarget.@IsMoving = value;
            return value;
        }

        internal static void BotSteeringLookToDirection(this global::BotSteering controlTarget, string site, global::EFT.BotOwner bot, global::UnityEngine.Vector3 @dir)
        {
            if (!Authorize(bot, controlTarget, site, "BotSteeringLookToDirection", "BotSteering.LookToDirection"))
            {
                return;
            }

            controlTarget.@LookToDirection(@dir);
        }

        internal static global::UnityEngine.Vector2 SetPlayerInputDirection(this global::EFT.Player controlTarget, string site, global::EFT.BotOwner bot, global::UnityEngine.Vector2 value)
        {
            if (!Authorize(bot, controlTarget, site, "SetPlayerInputDirection", "Player.InputDirection"))
            {
                return value;
            }

            controlTarget.@InputDirection = value;
            return value;
        }

        internal static global::UnityEngine.Vector2 SetMovementContextMovementDirection(this global::EFT.MovementContext controlTarget, string site, global::EFT.BotOwner bot, global::UnityEngine.Vector2 value)
        {
            if (!Authorize(bot, controlTarget, site, "SetMovementContextMovementDirection", "MovementContext.MovementDirection"))
            {
                return value;
            }

            controlTarget.@MovementDirection = value;
            return value;
        }

        internal static void GClass492DropOffset(this global::GClass492 controlTarget, string site, global::EFT.BotOwner bot)
        {
            if (!Authorize(bot, controlTarget, site, "GClass492DropOffset", "GClass492.DropOffset"))
            {
                return;
            }

            controlTarget.@DropOffset();
        }

        internal static void BotOwnerSetPose(this global::EFT.BotOwner controlTarget, string site, global::EFT.BotOwner bot, float @targetPose)
        {
            if (!Authorize(bot, controlTarget, site, "BotOwnerSetPose", "BotOwner.SetPose"))
            {
                return;
            }

            controlTarget.@SetPose(@targetPose);
        }

        internal static void BotOwnerSetTargetMoveSpeed(this global::EFT.BotOwner controlTarget, string site, global::EFT.BotOwner bot, float @speed)
        {
            if (!Authorize(bot, controlTarget, site, "BotOwnerSetTargetMoveSpeed", "BotOwner.SetTargetMoveSpeed"))
            {
                return;
            }

            controlTarget.@SetTargetMoveSpeed(@speed);
        }

        internal static void BotMoverSetTargetMoveSpeed(this global::BotMover controlTarget, string site, global::EFT.BotOwner bot, float @target)
        {
            if (!Authorize(bot, controlTarget, site, "BotMoverSetTargetMoveSpeed", "BotMover.SetTargetMoveSpeed"))
            {
                return;
            }

            controlTarget.@SetTargetMoveSpeed(@target);
        }

        internal static global::UnityEngine.Vector3 SetBotMoverNormDirCurPoint(this global::BotMover controlTarget, string site, global::EFT.BotOwner bot, global::UnityEngine.Vector3 value)
        {
            if (!Authorize(bot, controlTarget, site, "SetBotMoverNormDirCurPoint", "BotMover.NormDirCurPoint"))
            {
                return value;
            }

            controlTarget.@NormDirCurPoint = value;
            return value;
        }

        internal static void ICharacterControllerSetSteerDirection(this global::ICharacterController controlTarget, string site, global::EFT.BotOwner bot, global::UnityEngine.Vector3 @steerDirection)
        {
            if (!Authorize(bot, controlTarget, site, "ICharacterControllerSetSteerDirection", "ICharacterController.SetSteerDirection"))
            {
                return;
            }

            controlTarget.@SetSteerDirection(@steerDirection);
        }

        internal static void SubscribeMovementContextOnStateChanged(this global::EFT.MovementContext controlTarget, string site, global::EFT.BotOwner bot, global::EFT.MovementContext.GDelegate72 value)
        {
            if (!Authorize(bot, controlTarget, site, "SubscribeMovementContextOnStateChanged", "MovementContext.OnStateChanged"))
            {
                return;
            }

            controlTarget.@OnStateChanged += value;
        }

        internal static void BotMoverSetPose(this global::BotMover controlTarget, string site, global::EFT.BotOwner bot, float @targetPose)
        {
            if (!Authorize(bot, controlTarget, site, "BotMoverSetPose", "BotMover.SetPose"))
            {
                return;
            }

            controlTarget.@SetPose(@targetPose);
        }

        internal static void MovementContextPlayerAnimatorSetDoVault(this global::EFT.MovementContext controlTarget, string site, global::EFT.BotOwner bot, bool @doVault)
        {
            if (!Authorize(bot, controlTarget, site, "MovementContextPlayerAnimatorSetDoVault", "MovementContext.PlayerAnimatorSetDoVault"))
            {
                return;
            }

            controlTarget.@PlayerAnimatorSetDoVault(@doVault);
        }

        internal static void MovementContextPlayerAnimatorSetDoClimb(this global::EFT.MovementContext controlTarget, string site, global::EFT.BotOwner bot, bool @doClimb)
        {
            if (!Authorize(bot, controlTarget, site, "MovementContextPlayerAnimatorSetDoClimb", "MovementContext.PlayerAnimatorSetDoClimb"))
            {
                return;
            }

            controlTarget.@PlayerAnimatorSetDoClimb(@doClimb);
        }

        internal static global::UnityEngine.Vector3 SetBotMoverPositionOnWayInner(this global::BotMover controlTarget, string site, global::EFT.BotOwner bot, global::UnityEngine.Vector3 value)
        {
            if (!Authorize(bot, controlTarget, site, "SetBotMoverPositionOnWayInner", "BotMover.PositionOnWayInner"))
            {
                return value;
            }

            controlTarget.@PositionOnWayInner = value;
            return value;
        }

        internal static global::UnityEngine.Vector3 SetBotMoverPrevSuccessLinkedFrom_1(this global::BotMover controlTarget, string site, global::EFT.BotOwner bot, global::UnityEngine.Vector3 value)
        {
            if (!Authorize(bot, controlTarget, site, "SetBotMoverPrevSuccessLinkedFrom_1", "BotMover.PrevSuccessLinkedFrom_1"))
            {
                return value;
            }

            controlTarget.@PrevSuccessLinkedFrom_1 = value;
            return value;
        }

        internal static float SetBotMoverPrevPosLinkedTime_1(this global::BotMover controlTarget, string site, global::EFT.BotOwner bot, float value)
        {
            if (!Authorize(bot, controlTarget, site, "SetBotMoverPrevPosLinkedTime_1", "BotMover.PrevPosLinkedTime_1"))
            {
                return value;
            }

            controlTarget.@PrevPosLinkedTime_1 = value;
            return value;
        }

        internal static bool SetBotMoverLinkedToNavmeshInitially(this global::BotMover controlTarget, string site, global::EFT.BotOwner bot, bool value)
        {
            if (!Authorize(bot, controlTarget, site, "SetBotMoverLinkedToNavmeshInitially", "BotMover.LinkedToNavmeshInitially"))
            {
                return value;
            }

            controlTarget.@LinkedToNavmeshInitially = value;
            return value;
        }

        internal static void BotMoverSetPointOnWay(this global::BotMover controlTarget, string site, global::EFT.BotOwner bot, global::UnityEngine.Vector3 @point)
        {
            if (!Authorize(bot, controlTarget, site, "BotMoverSetPointOnWay", "BotMover.SetPointOnWay"))
            {
                return;
            }

            controlTarget.@SetPointOnWay(@point);
        }

        internal static float SetMovementContextSprintSpeed(this global::EFT.MovementContext controlTarget, string site, global::EFT.BotOwner bot, float value)
        {
            if (!Authorize(bot, controlTarget, site, "SetMovementContextSprintSpeed", "MovementContext.SprintSpeed"))
            {
                return value;
            }

            controlTarget.@SprintSpeed = value;
            return value;
        }

        internal static void MovementContextProcessStateEnter(this global::EFT.MovementContext controlTarget, string site, global::EFT.BotOwner bot, global::EFT.BaseMovementState @enteredState)
        {
            if (!Authorize(bot, controlTarget, site, "MovementContextProcessStateEnter", "MovementContext.ProcessStateEnter"))
            {
                return;
            }

            controlTarget.@ProcessStateEnter(@enteredState);
        }

        internal static void BotMoverGoToPointNoWay(this global::BotMover controlTarget, string site, global::EFT.BotOwner bot, global::UnityEngine.Vector3? @v)
        {
            if (!Authorize(bot, controlTarget, site, "BotMoverGoToPointNoWay", "BotMover.GoToPointNoWay"))
            {
                return;
            }

            controlTarget.@GoToPointNoWay(@v);
        }

        internal static void AnimatorCrossFadeInFixedTime(this global::UnityEngine.Animator controlTarget, string site, global::EFT.BotOwner bot, int @stateHashName, float @fixedTransitionDuration, int @layer, float @fixedTimeOffset)
        {
            if (!Authorize(bot, controlTarget, site, "AnimatorCrossFadeInFixedTime", "Animator.CrossFadeInFixedTime"))
            {
                return;
            }

            controlTarget.@CrossFadeInFixedTime(@stateHashName, @fixedTransitionDuration, @layer, @fixedTimeOffset);
        }

        internal static global::UnityEngine.Quaternion SetTransformrotation(this global::UnityEngine.Transform controlTarget, string site, global::EFT.BotOwner bot, global::UnityEngine.Quaternion value)
        {
            if (!Authorize(bot, controlTarget, site, "SetTransformrotation", "Transform.rotation"))
            {
                return value;
            }

            controlTarget.@rotation = value;
            return value;
        }

        internal static void BotMoverGoToByWay(this global::BotMover controlTarget, string site, global::EFT.BotOwner bot, global::UnityEngine.Vector3[] @way, float @reachDist)
        {
            if (!Authorize(bot, controlTarget, site, "BotMoverGoToByWay", "BotMover.GoToByWay"))
            {
                return;
            }

            controlTarget.@GoToByWay(@way, @reachDist);
        }

        internal static void MovementContextTryJump(this global::EFT.MovementContext controlTarget, string site, global::EFT.BotOwner bot)
        {
            if (!Authorize(bot, controlTarget, site, "MovementContextTryJump", "MovementContext.TryJump"))
            {
                return;
            }

            controlTarget.@TryJump();
        }

        internal static void MovementContextPlayerAnimatorSetObstacleHeight(this global::EFT.MovementContext controlTarget, string site, global::EFT.BotOwner bot, float @height)
        {
            if (!Authorize(bot, controlTarget, site, "MovementContextPlayerAnimatorSetObstacleHeight", "MovementContext.PlayerAnimatorSetObstacleHeight"))
            {
                return;
            }

            controlTarget.@PlayerAnimatorSetObstacleHeight(@height);
        }

        internal static void MovementContextPlayerAnimatorSetObstacleLength(this global::EFT.MovementContext controlTarget, string site, global::EFT.BotOwner bot, float @length)
        {
            if (!Authorize(bot, controlTarget, site, "MovementContextPlayerAnimatorSetObstacleLength", "MovementContext.PlayerAnimatorSetObstacleLength"))
            {
                return;
            }

            controlTarget.@PlayerAnimatorSetObstacleLength(@length);
        }

        internal static void MovementContextPlayerAnimatorSetBehindObstacleRatio(this global::EFT.MovementContext controlTarget, string site, global::EFT.BotOwner bot, float @height)
        {
            if (!Authorize(bot, controlTarget, site, "MovementContextPlayerAnimatorSetBehindObstacleRatio", "MovementContext.PlayerAnimatorSetBehindObstacleRatio"))
            {
                return;
            }

            controlTarget.@PlayerAnimatorSetBehindObstacleRatio(@height);
        }

        internal static void MovementContextPlayerAnimatorSetAbsoluteForwardVelocity(this global::EFT.MovementContext controlTarget, string site, global::EFT.BotOwner bot, float @velocity)
        {
            if (!Authorize(bot, controlTarget, site, "MovementContextPlayerAnimatorSetAbsoluteForwardVelocity", "MovementContext.PlayerAnimatorSetAbsoluteForwardVelocity"))
            {
                return;
            }

            controlTarget.@PlayerAnimatorSetAbsoluteForwardVelocity(@velocity);
        }

        internal static void PlayerRemoveLeftHandItem(this global::EFT.Player controlTarget, string site, global::EFT.BotOwner bot, float @speed = 1f)
        {
            if (!Authorize(bot, controlTarget, site, "PlayerRemoveLeftHandItem", "Player.RemoveLeftHandItem"))
            {
                return;
            }

            controlTarget.@RemoveLeftHandItem(@speed);
        }

        internal static void PhysicsIgnoreCollision(string site, global::EFT.BotOwner bot, global::UnityEngine.Collider @collider1, global::UnityEngine.Collider @collider2, bool @ignore)
        {
            if (!Authorize(bot, null, site, "PhysicsIgnoreCollision", "Physics.IgnoreCollision"))
            {
                return;
            }

            global::UnityEngine.Physics.@IgnoreCollision(@collider1, @collider2, @ignore);
        }

        internal static bool MovementContextTryVaulting(this global::EFT.MovementContext controlTarget, string site, global::EFT.BotOwner bot)
        {
            if (!Authorize(bot, controlTarget, site, "MovementContextTryVaulting", "MovementContext.TryVaulting"))
            {
                return default(bool);
            }

            return controlTarget.@TryVaulting();
        }

        internal static bool IVaultingComponentTryVaulting(this global::EFT.Vaulting.IVaultingComponent controlTarget, string site, global::EFT.BotOwner bot)
        {
            if (!Authorize(bot, controlTarget, site, "IVaultingComponentTryVaulting", "IVaultingComponent.TryVaulting"))
            {
                return default(bool);
            }

            return controlTarget.@TryVaulting();
        }

        internal static void UnsubscribeMovementContextOnStateChanged(this global::EFT.MovementContext controlTarget, string site, global::EFT.BotOwner bot, global::EFT.MovementContext.GDelegate72 value)
        {
            if (!Authorize(bot, controlTarget, site, "UnsubscribeMovementContextOnStateChanged", "MovementContext.OnStateChanged"))
            {
                return;
            }

            controlTarget.@OnStateChanged -= value;
        }

        internal static bool SetAnimatorapplyRootMotion(this global::UnityEngine.Animator controlTarget, string site, global::EFT.BotOwner bot, bool value)
        {
            if (!Authorize(bot, controlTarget, site, "SetAnimatorapplyRootMotion", "Animator.applyRootMotion"))
            {
                return value;
            }

            controlTarget.@applyRootMotion = value;
            return value;
        }

        internal static void PlayerInitVaultingComponent(this global::EFT.Player controlTarget, string site, global::EFT.BotOwner bot, bool @aiControlled)
        {
            if (!Authorize(bot, controlTarget, site, "PlayerInitVaultingComponent", "Player.InitVaultingComponent"))
            {
                return;
            }

            controlTarget.@InitVaultingComponent(@aiControlled);
        }

        internal static global::System.Func<bool> SetGClass2679Func_0(this global::GClass2679 controlTarget, string site, global::EFT.BotOwner bot, global::System.Func<bool> value)
        {
            if (!Authorize(bot, controlTarget, site, "SetGClass2679Func_0", "GClass2679.Func_0"))
            {
                return value;
            }

            controlTarget.@Func_0 = value;
            return value;
        }

        internal static void BotMoverSetReachDist(this global::BotMover controlTarget, string site, global::EFT.BotOwner bot, float @reachDist)
        {
            if (!Authorize(bot, controlTarget, site, "BotMoverSetReachDist", "BotMover.SetReachDist"))
            {
                return;
            }

            controlTarget.@SetReachDist(@reachDist);
        }

        internal static void EFTPhysicsClassIgnoreCollision(string site, global::EFT.BotOwner bot, global::UnityEngine.Collider @collider1, global::UnityEngine.Collider @collider2, bool @ignore = true)
        {
            if (!Authorize(bot, null, site, "EFTPhysicsClassIgnoreCollision", "global::EFTPhysicsClass.IgnoreCollision"))
            {
                return;
            }

            global::EFTPhysicsClass.@IgnoreCollision(@collider1, @collider2, @ignore);
        }

        internal static void ColliderExtrusionAddIgnoredCollider(this global::ColliderExtrusion controlTarget, string site, global::EFT.BotOwner bot, global::UnityEngine.Collider @collider)
        {
            if (!Authorize(bot, controlTarget, site, "ColliderExtrusionAddIgnoredCollider", "ColliderExtrusion.AddIgnoredCollider"))
            {
                return;
            }

            controlTarget.@AddIgnoredCollider(@collider);
        }

        internal static void EFTPhysicsClasssmethod_0(string site, global::EFT.BotOwner bot, global::UnityEngine.Collider @collider1, global::UnityEngine.Collider @collider2, bool @ignore)
        {
            if (!Authorize(bot, null, site, "EFTPhysicsClasssmethod_0", "global::EFTPhysicsClass.smethod_0"))
            {
                return;
            }

            global::EFTPhysicsClass.@smethod_0(@collider1, @collider2, @ignore);
        }

        internal static void ColliderExtrusionRemoveIgnoredCollider(this global::ColliderExtrusion controlTarget, string site, global::EFT.BotOwner bot, global::UnityEngine.Collider @collider)
        {
            if (!Authorize(bot, controlTarget, site, "ColliderExtrusionRemoveIgnoredCollider", "ColliderExtrusion.RemoveIgnoredCollider"))
            {
                return;
            }

            controlTarget.@RemoveIgnoredCollider(@collider);
        }

        internal static bool SetBehaviourenabled(this global::UnityEngine.Behaviour controlTarget, string site, global::EFT.BotOwner bot, bool value)
        {
            if (!Authorize(bot, controlTarget, site, "SetBehaviourenabled", "Behaviour.enabled"))
            {
                return value;
            }

            controlTarget.@enabled = value;
            return value;
        }

        internal static global::EBotLinkResult RelinkBotMoverBody(this global::BotMover controlTarget, string site, global::EFT.BotOwner bot, global::UnityEngine.Vector3 position, bool force)
        {
            if (!Authorize(bot, controlTarget, site, "RelinkBotMoverBody", "BotMover.method_10"))
                return global::EBotLinkResult.fail;
            return controlTarget.method_10(position, force);
        }
    }
}
