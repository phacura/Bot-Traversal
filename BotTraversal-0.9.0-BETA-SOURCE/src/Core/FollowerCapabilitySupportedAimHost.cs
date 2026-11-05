using EFT;
using UnityEngine;
namespace BotTraversal.Core
{
    internal static partial class FollowerTraversalTestRuntime
    {
        // Local terrain proof only. Existing callers own targets, progress clocks and movement tickets.
        private static bool TryGetCapabilitySupportedLocalAim(Player player,Vector3 target,out Vector3 aim)
        {
            aim=target;
            if(player?.MovementContext==null||!IsFinite(target)||!IsFinite(player.Position))return false;
            float length=Vector3.Distance(player.Position,target);
            aim=Vector3.Lerp(player.Position,target,(float)FollowerCapabilityPassagePolicy.LocalAimFraction(length));
            bool supported=true;
            foreach(var sample in new[]{Vector3.Lerp(player.Position,aim,.5f),aim})
                if(!TryGetFirstNonSelfRaycastHit(player,sample+Vector3.up*.60f,Vector3.down,1f,LayerMaskClass.HighPolyWithTerrainMask,out var support)||
                    Mathf.Abs(support.point.y-sample.y)>.35f||support.normal.y<.65f){supported=false;break;}
            var delta=GetFlatDelta(player.Position,aim);var direction=GetFlatDirection(delta);
            RaycastHit obstacle=default;
            bool hit=delta.magnitude>.001f&&TryGetFirstNonSelfCapsuleCastHit(player,player.Position+Vector3.up*.40f,
                player.Position+Vector3.up*1.35f,.18f,direction,delta.magnitude,LayerMaskClass.HighPolyWithTerrainMask,out obstacle);
            // A walkable floor hit is not a wall. Native collision remains active throughout.
            bool blocked=hit&&obstacle.normal.y<.65f;
            return FollowerCapabilityPassagePolicy.GroundAim(player.MovementContext.IsGrounded,IsFinite(aim),aim.y-player.Position.y,supported,blocked);
        }
    }
}
