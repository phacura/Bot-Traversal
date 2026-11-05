using EFT;
using EFT.Vaulting;
using System;
using UnityEngine;

namespace BotTraversal.Core
{
    /// <summary>
    /// Optional integration point for multiplayer transports. Bot Traversal owns traversal intent
    /// but does not reference Fika. A transport addon can subscribe and replay the same native EFT
    /// vault parameters for remote observers.
    /// </summary>
    public static class FollowerNativeVaultNetworkBridge
    {
        public sealed class VaultEvent
        {
            public Player Player;
            public string ProfileId;
            public EVaultingStrategy Strategy;
            public Vector3 VaultingPoint;
            public float VaultingHeight;
            public float VaultingLength;
            public float DemonstratedVaultingSpeed;
            public float BehindObstacleRatio;
            public float AbsoluteForwardVelocity;
            public int EdgeId;
            public float TriggeredAt;
        }

        public static event Action<VaultEvent> VaultInjected;

        internal static void Publish(VaultEvent value)
        {
            if (value == null)
            {
                return;
            }

            Delegate[] subscribers = VaultInjected?.GetInvocationList();
            if (subscribers == null)
            {
                return;
            }

            for (int i = 0; i < subscribers.Length; i++)
            {
                try
                {
                    ((Action<VaultEvent>)subscribers[i]).Invoke(value);
                }
                catch (Exception ex)
                {
                    Logger.LogError("[TraversalTest][FikaBridge] Native vault subscriber failed.");
                    Logger.LogError(ex);
                }
            }
        }
    }
}
