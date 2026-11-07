using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using BotTraversal.Core;

namespace BotTraversal.Core
{
    /// <summary>
    /// R8.3 compatibility shell for the retired runtime walk-link bridge. Learned WalkThrough seams are
    /// Traversal-private knowledge and must never be published into Unity's global NavMesh. The
    /// reflection helpers remain only so ResetRaid can safely remove a legacy handle if a prior build
    /// created one before shutdown.
    /// </summary>
    internal static class FollowerRuntimeNavMeshLinks
    {
        private static readonly Dictionary<int, object> Handles = new Dictionary<int, object>();
        private static MethodInfo addLinkMethod;
        private static MethodInfo removeLinkMethod;
        private static Type linkDataType;
        private static bool resolved;
        private static bool unavailableLogged;

        internal static void ResetRaid()
        {
            foreach (object handle in Handles.Values.ToArray())
            {
                TryRemoveHandle(handle);
            }
            Handles.Clear();
        }

        internal static bool RegisterWalkLink(int edgeId, Vector3 start, Vector3 end)
        {
            // R8.3 safety architecture: a learned WalkThrough is special traversal knowledge, not
            // ordinary NavMesh connectivity. Publishing it with NavMesh.AddLink lets unrelated
            // EFT/SAIN BotMover paths consume the seam without PIT's explicit corridor executor.
            // That produced both elevated-platform path traps and an out-of-world follower fall in
            // the R8.1 trace. Keep this compatibility entry point as a hard no-op so no stale call
            // site can ever mutate Unity's global NavMesh topology again.
            if (!unavailableLogged)
            {
                unavailableLogged = true;
                Logger.LogInfo("[TraversalLink] R8.3 private topology active; global NavMesh.AddLink publication is disabled.");
            }
            return false;
        }

        internal static void Remove(int edgeId)
        {
            if (!Handles.TryGetValue(edgeId, out object handle))
            {
                return;
            }

            TryRemoveHandle(handle);
            Handles.Remove(edgeId);
        }

        private static bool ResolveApi()
        {
            if (resolved)
            {
                return addLinkMethod != null && linkDataType != null;
            }
            resolved = true;

            try
            {
                addLinkMethod = typeof(NavMesh)
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(method => string.Equals(method.Name, "AddLink", StringComparison.Ordinal))
                    .OrderBy(method => method.GetParameters().Length)
                    .FirstOrDefault(method =>
                    {
                        ParameterInfo[] args = method.GetParameters();
                        return (args.Length == 1 || args.Length == 3) &&
                               string.Equals(args[0].ParameterType.FullName, "UnityEngine.AI.NavMeshLinkData", StringComparison.Ordinal);
                    });

                if (addLinkMethod != null)
                {
                    linkDataType = addLinkMethod.GetParameters()[0].ParameterType;
                    Type handleType = addLinkMethod.ReturnType;
                    removeLinkMethod = typeof(NavMesh)
                        .GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .FirstOrDefault(method =>
                        {
                            if (!string.Equals(method.Name, "RemoveLink", StringComparison.Ordinal))
                            {
                                return false;
                            }
                            ParameterInfo[] args = method.GetParameters();
                            return args.Length == 1 && args[0].ParameterType == handleType;
                        });
                }
            }
            catch
            {
                addLinkMethod = null;
                linkDataType = null;
            }

            if ((addLinkMethod == null || linkDataType == null) && !unavailableLogged)
            {
                unavailableLogged = true;
                Logger.LogWarning("[TraversalLink] Legacy NavMesh.AddLink reflection API unavailable; R8.3 private WalkThrough topology is unaffected.");
            }

            return addLinkMethod != null && linkDataType != null;
        }

        private static void TryRemoveHandle(object handle)
        {
            if (handle == null)
            {
                return;
            }

            try
            {
                MethodInfo instanceRemove = handle.GetType().GetMethod("Remove", BindingFlags.Public | BindingFlags.Instance);
                if (instanceRemove != null)
                {
                    instanceRemove.MethodBaseInvoke("R1-7B86F13EDFC4",null,handle,null);
                    return;
                }

                removeLinkMethod?.MethodBaseInvoke("R1-38910642BB3D",null,null,new[] { handle });
            }
            catch
            {
                // Raid teardown must stay fail-open.
            }
        }

        private static void SetMember(object instance, string name, object value)
        {
            Type type = instance.GetType();
            PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property != null && property.CanWrite)
            {
                property.PropertyInfoSetValue("R1-B4006D2457D7",null,instance,value,null);
                return;
            }

            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            field?.FieldInfoSetValue("R1-7592E7FA504F",null,instance,value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
