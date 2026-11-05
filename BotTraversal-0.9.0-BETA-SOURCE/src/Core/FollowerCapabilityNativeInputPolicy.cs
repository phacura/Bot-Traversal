// R3U_NATIVE_INPUT_EXTENSION_REVIEWED
namespace BotTraversal.Core
{
    internal static class FollowerCapabilityNativeInputPolicy
    {
        internal enum Action { Reject, Unavailable, AwaitInput, Publish }
        internal readonly struct Snapshot
        {
            internal readonly bool Current,IdleConsumer,CanWalk,LocomotionConsumer;
            internal readonly float X,Y;
            internal Snapshot(bool current,bool idle,bool canWalk,float x,float y)
                :this(current,idle,canWalk,x,y,false){}
            internal Snapshot(bool current,bool idle,bool canWalk,float x,float y,bool locomotion)
            {Current=current;IdleConsumer=idle;CanWalk=canWalk;X=x;Y=y;LocomotionConsumer=locomotion;}
        }
        internal static Action Evaluate(Snapshot s)
        {
            if(!s.Current)return Action.Reject;
            if(s.IdleConsumer&&s.LocomotionConsumer)return Action.Reject;
            if(!s.IdleConsumer&&!s.LocomotionConsumer)return Action.Unavailable;
            if(!s.CanWalk||float.IsNaN(s.X)||float.IsInfinity(s.X)||float.IsNaN(s.Y)||float.IsInfinity(s.Y))return Action.Reject;
            if(s.X>1.0001f||s.X< -1.0001f||s.Y>1.0001f||s.Y< -1.0001f)return Action.Reject;
            // Same per-axis epsilon as native IdleStateClass.smethod_0.
            return s.X>0.00001f||s.X< -0.00001f||s.Y>0.00001f||s.Y< -0.00001f?Action.Publish:Action.AwaitInput;
        }
    }
    internal interface ICapabilityNativeInputPort
    {
        FollowerCapabilityNativeInputPolicy.Snapshot CaptureInput();
        bool RequestInput(FollowerCapabilityNativeInputPolicy.Snapshot observed);
    }
    internal static class FollowerCapabilityNativeInputDriver
    {
        internal enum Receipt { Rejected, Unavailable, AwaitingInput, InputRequested }
        internal static Receipt Commit(ICapabilityNativeInputPort port)
        {
            if(port==null)return Receipt.Unavailable;
            var s=port.CaptureInput();var action=FollowerCapabilityNativeInputPolicy.Evaluate(s);
            if(action==FollowerCapabilityNativeInputPolicy.Action.Unavailable)return Receipt.Unavailable;
            if(action==FollowerCapabilityNativeInputPolicy.Action.Reject)return Receipt.Rejected;
            if(action==FollowerCapabilityNativeInputPolicy.Action.AwaitInput)return Receipt.AwaitingInput;
            return port.RequestInput(s)?Receipt.InputRequested:Receipt.Rejected;
        }
    }
}
