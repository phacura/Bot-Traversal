using System;

namespace BotTraversal.Core
{
    // Raid-local semantic state. No engine references, owner registry, clock, movement
    // API, or learning writes. Runtime mission/steps embed these objects as their sole
    // cursor/debt state; the test harness executes these same transition methods.
    internal static class FollowerTraversalLifecycle
    {
        internal enum Resolution { Outstanding, Executed, Subsumed, TeleportBypass, Suppressed, Cancelled }
        internal enum Phase { Ready, Executing, Recovering, Blocked, Resolved, Retired }
        internal enum ActionOutcome { Unproven, NativePhysical, NonNative, Teleport, Aborted }
        internal enum CommitResult { Rejected, Applied, Repeated }

        internal readonly struct Identity : IEquatable<Identity>
        {
            internal readonly string Bot;
            internal readonly int Raid, Mission, Step, Episode, Edge, Chain;
            internal readonly long Geometry, Writer;
            internal Identity(string bot, int raid, int mission, int step, int episode,
                int edge, int chain, long geometry, long writer)
            { Bot=bot; Raid=raid; Mission=mission; Step=step; Episode=episode;
              Edge=edge; Chain=chain; Geometry=geometry; Writer=writer; }
            internal bool Valid => !string.IsNullOrEmpty(Bot) && Raid>0 && Mission>0 &&
                Step>=0 && Episode>0 && Edge>0 && Chain>=0 && Geometry>=0 && Writer>=0;
            public bool Equals(Identity other) => Bot==other.Bot && Raid==other.Raid &&
                Mission==other.Mission && Step==other.Step && Episode==other.Episode &&
                Edge==other.Edge && Chain==other.Chain && Geometry==other.Geometry && Writer==other.Writer;
            public override bool Equals(object obj) => obj is Identity other && Equals(other);
            public override int GetHashCode() => Mission ^ Step ^ Episode ^ Edge ^ Raid;
        }

        internal sealed class StepState
        {
            internal long GeometryVersion { get; private set; }
            internal Resolution Result { get; private set; }
            internal Phase Status { get; private set; }
            internal ProbeState Probe { get; private set; }
            internal bool DebtOutstanding => Result==Resolution.Outstanding;
            internal bool PhysicallyResolved => Result==Resolution.Executed || Result==Resolution.Subsumed;
            internal void GeometryChanged() { checked { GeometryVersion++; } }
            internal bool Attach(ProbeState probe)
            {
                if (probe==null || !probe.Origin.Valid || probe.Origin.Writer<=0 || !ReferenceEquals(probe.Step,this) ||
                    probe.Origin.Geometry!=GeometryVersion || !DebtOutstanding || (Probe!=null && !Probe.Retired)) return false;
                Probe=probe; Status=Phase.Executing; return true;
            }
            internal void Detach(ProbeState probe)
            {
                if (!ReferenceEquals(Probe,probe)) return;
                Probe=null;
                if (DebtOutstanding && Status!=Phase.Blocked) Status=Phase.Recovering;
            }
            internal void Block() { if (DebtOutstanding) Status=Phase.Blocked; }
            internal void Recover() { if (DebtOutstanding) Status=Phase.Recovering; }
            internal bool Resolve(Resolution resolution)
            {
                if (resolution==Resolution.Outstanding || !DebtOutstanding) return false;
                Result=resolution; Status=Phase.Resolved; return true;
            }
        }

        internal sealed class ProbeState
        {
            internal readonly Identity Origin;
            internal readonly StepState Step;
            internal bool Retired { get; private set; }
            internal bool CompletionPublished { get; private set; }
            internal ActionOutcome Outcome { get; private set; }
            internal ProbeState(Identity origin, StepState step) { Origin=origin; Step=step; }
            internal bool Matches(Identity live, StepState step) => !Retired && Origin.Valid &&
                ReferenceEquals(Step,step) && ReferenceEquals(step?.Probe,this) && Origin.Equals(live);
            internal CommitResult PublishCompletion(Identity live, StepState step, ActionOutcome outcome)
            {
                if (!Matches(live,step) || outcome==ActionOutcome.Unproven || outcome==ActionOutcome.Aborted)
                    return CommitResult.Rejected;
                if (CompletionPublished) return Outcome==outcome ? CommitResult.Repeated : CommitResult.Rejected;
                CompletionPublished=true; Outcome=outcome; return CommitResult.Applied;
            }
            internal void Retire()
            {
                if (Retired) return;
                Retired=true; if (!CompletionPublished) Outcome=ActionOutcome.Aborted;
                Step?.Detach(this);
            }
        }

        internal sealed class MissionState
        {
            internal int Cursor { get; private set; }
            internal Phase Status { get; private set; }
            internal void RequestFullRetry() { if(!Retired)Status=Phase.Recovering; }
            internal void CancelFullRetry() { if(!Retired)Status=Phase.Ready; }
            internal bool Retired { get; private set; }
            internal bool HasNonPhysicalRetirement { get; private set; }
            private StepState lastStep;
            private Identity lastOrigin;
            private Resolution lastResolution;
            internal bool FinishedAtRetirement { get; private set; }

            // A callback's origin and step instance are mandatory. A duplicate cannot
            // consume the successor, clear its retries, or refresh its progress clock.
            internal CommitResult Resolve(StepState current, Identity live, StepState offeredStep,
                Identity origin, Resolution resolution, bool qualifiedEvidence, bool requiresActuator)
            {
                if (Retired || Status==Phase.Recovering || Status==Phase.Blocked || !origin.Valid || live.Bot!=origin.Bot || live.Raid!=origin.Raid || live.Mission!=origin.Mission ||
                    !qualifiedEvidence || resolution==Resolution.Outstanding)
                    return CommitResult.Rejected;
                if (ReferenceEquals(lastStep,offeredStep) && lastOrigin.Equals(origin) && lastResolution==resolution)
                    return CommitResult.Repeated;
                if (!ReferenceEquals(current,offeredStep) || current==null || !origin.Equals(live) ||
                    live.Step!=Cursor || !current.DebtOutstanding) return CommitResult.Rejected;
                if (resolution==Resolution.Suppressed && requiresActuator)
                { current.Block(); return CommitResult.Rejected; }
                if (current.Probe!=null && !current.Probe.Retired &&
                    ((resolution!=Resolution.Executed && resolution!=Resolution.TeleportBypass) || !current.Probe.CompletionPublished ||
                     !current.Probe.Matches(live,current))) return CommitResult.Rejected;
                if (resolution==Resolution.Executed &&
                    (current.Probe==null || !current.Probe.CompletionPublished ||
                     current.Probe.Outcome==ActionOutcome.Teleport || !current.Probe.Matches(live,current))) return CommitResult.Rejected;
                if (!current.Resolve(resolution)) return CommitResult.Rejected;
                lastStep=current; lastOrigin=origin; lastResolution=resolution;
                HasNonPhysicalRetirement |= !current.PhysicallyResolved;
                Cursor++; return CommitResult.Applied;
            }
            internal bool SkipEmpty(int expectedCursor)
            {
                if (Retired || Cursor!=expectedCursor) return false;
                HasNonPhysicalRetirement=true; Cursor++; return true;
            }
            // Structural edits preserve the current step's identity, rather than
            // pretending a list index adjustment completed any physical action.
            internal bool RemovedAt(int index, bool removedOutstanding)
            {
                if (Retired || index<0) return false;
                if (index<Cursor) Cursor--;
                if (removedOutstanding) HasNonPhysicalRetirement=true;
                return true;
            }
            internal bool CanHandoffToFollow(int stepCount, bool activeProbe, bool unresolvedDebt) =>
                (!Retired || FinishedAtRetirement) && Cursor==stepCount && !activeProbe && !unresolvedDebt;
            internal void Retire(bool finished=false) { if (Retired) return; FinishedAtRetirement=finished; Retired=true; }
        }
    }
}
