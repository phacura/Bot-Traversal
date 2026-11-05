using D=BotTraversal.Core.FollowerMovementWriterAuthority.WriterDomain;
namespace BotTraversal.Core
{
    internal enum CapabilityFollowRecoveryMode { FollowPath, GeneralRecovery, ProbeRecovery, TeleportQuiescence }
    internal enum CapabilityFollowRecoveryFailure { None, MissingActor, MissingOrigin, StaleOrigin, WrongOwner, NativeOwnsMotion, CanonicalOwnsMotion, MissionOwnsMotion, StaleProbe, Preempted, ScopeRejected }
    internal static class FollowerCapabilityFollowRecoveryPolicy
    {
        internal readonly struct Snapshot
        {
            internal readonly long Origin,Live;
            internal readonly D Owner;
            internal readonly CapabilityFollowRecoveryMode Mode;
            internal readonly bool ActorCurrent,NativeActive,ProbeCurrent,CanonicalActive,MissionActive,RecoveryEpochCurrent,Preempted;
            internal Snapshot(long origin,long live,D owner,CapabilityFollowRecoveryMode mode,bool actorCurrent,
                bool nativeActive,bool probeCurrent,bool canonicalActive,bool missionActive,bool recoveryEpochCurrent,bool preempted)
            {Origin=origin;Live=live;Owner=owner;Mode=mode;ActorCurrent=actorCurrent;NativeActive=nativeActive;
             ProbeCurrent=probeCurrent;CanonicalActive=canonicalActive;MissionActive=missionActive;
             RecoveryEpochCurrent=recoveryEpochCurrent;Preempted=preempted;}
        }
        internal static CapabilityFollowRecoveryFailure Evaluate(Snapshot s)
        {
            if(!s.ActorCurrent)return CapabilityFollowRecoveryFailure.MissingActor;
            if(s.Origin<=0||s.Live<=0)return CapabilityFollowRecoveryFailure.MissingOrigin;
            if(s.Origin!=s.Live)return CapabilityFollowRecoveryFailure.StaleOrigin;
            if(s.Mode==CapabilityFollowRecoveryMode.TeleportQuiescence)
                return s.Owner==D.Teleport?CapabilityFollowRecoveryFailure.None:CapabilityFollowRecoveryFailure.WrongOwner;
            if(s.Preempted)return CapabilityFollowRecoveryFailure.Preempted;
            if(s.Mode==CapabilityFollowRecoveryMode.ProbeRecovery)
            {
                if(s.Owner!=D.Traversal)return CapabilityFollowRecoveryFailure.WrongOwner;
                return s.NativeActive&&s.ProbeCurrent?CapabilityFollowRecoveryFailure.None:CapabilityFollowRecoveryFailure.StaleProbe;
            }
            if(s.Mode!=CapabilityFollowRecoveryMode.FollowPath&&s.Mode!=CapabilityFollowRecoveryMode.GeneralRecovery)
                return CapabilityFollowRecoveryFailure.WrongOwner;
            if(s.NativeActive)return CapabilityFollowRecoveryFailure.NativeOwnsMotion;
            if(s.CanonicalActive)return CapabilityFollowRecoveryFailure.CanonicalOwnsMotion;
            if(s.MissionActive&&!s.RecoveryEpochCurrent)return CapabilityFollowRecoveryFailure.MissionOwnsMotion;
            bool owner=s.Owner==D.Follow||(s.Mode==CapabilityFollowRecoveryMode.GeneralRecovery&&
                (s.Owner==D.Recovery||s.Owner==D.PostTraversal));
            return owner?CapabilityFollowRecoveryFailure.None:CapabilityFollowRecoveryFailure.WrongOwner;
        }
        // Intent preflight is not a ticket. Only the existing Follow issuer can mint one.
        internal static CapabilityFollowRecoveryFailure EvaluateFresh(Snapshot s)
        {
            if(s.Mode!=CapabilityFollowRecoveryMode.FollowPath)return CapabilityFollowRecoveryFailure.WrongOwner;
            if(!s.ActorCurrent)return CapabilityFollowRecoveryFailure.MissingActor;
            if(s.Preempted||s.Owner==D.Teleport)return CapabilityFollowRecoveryFailure.Preempted;
            if(s.Owner!=D.Unknown&&s.Owner!=D.Follow&&s.Owner!=D.Recovery)return CapabilityFollowRecoveryFailure.WrongOwner;
            if(s.NativeActive)return CapabilityFollowRecoveryFailure.NativeOwnsMotion;
            if(s.CanonicalActive)return CapabilityFollowRecoveryFailure.CanonicalOwnsMotion;
            if(s.MissionActive&&!s.RecoveryEpochCurrent)return CapabilityFollowRecoveryFailure.MissionOwnsMotion;
            return CapabilityFollowRecoveryFailure.None;
        }
    }
}
