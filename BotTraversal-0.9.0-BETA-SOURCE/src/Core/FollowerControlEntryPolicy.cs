using System;

namespace BotTraversal.Core
{
    internal enum ControlSiteMode
    {
        ExactScope,
        ReviewedLegacy,
        Infrastructure
    }

    internal enum ControlAdmission
    {
        Reject,
        ExactOrigin,
        ReviewedCompatibility,
        Infrastructure
    }

    internal enum ControlUpdatePhase
    {
        ExternalCallback,
        MainUpdate,
        BotUpdate,
        NativeFixedUpdate
    }

    // Pure gate: a catalog exception cannot turn an explicit invalid ticket
    // into an unscoped compatibility call. No ownership or clock is allocated.
    internal static class FollowerControlEntryPolicy
    {
        internal static ControlAdmission Decide(bool knownSite, bool operationMatches, ControlSiteMode mode, bool targetBound, bool hasScope, bool exactScopeValid, bool existingCompatibilityAllowed)
        {
            if (!knownSite || !operationMatches ||
                (mode != ControlSiteMode.ExactScope && mode != ControlSiteMode.ReviewedLegacy && mode != ControlSiteMode.Infrastructure))
                return ControlAdmission.Reject;
            if (mode == ControlSiteMode.Infrastructure)
                return ControlAdmission.Infrastructure;
            if (!targetBound)
                return ControlAdmission.Reject;
            if (hasScope)
                return exactScopeValid ? ControlAdmission.ExactOrigin : ControlAdmission.Reject;
            if (mode == ControlSiteMode.ExactScope)
                return ControlAdmission.Reject;
            return existingCompatibilityAllowed ? ControlAdmission.ReviewedCompatibility : ControlAdmission.Reject;
        }
    }

    internal readonly struct ControlSite
    {
        internal readonly string Operation;
        internal readonly ControlSiteMode Mode;
        internal ControlSite(string operation, ControlSiteMode mode)
        {
            Operation = operation;
            Mode = mode;
        }
    }
}
