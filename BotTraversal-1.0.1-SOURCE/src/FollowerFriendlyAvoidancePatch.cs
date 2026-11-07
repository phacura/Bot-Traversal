using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using EFT;
using BotTraversal.Core;

namespace BotTraversal
{
    internal static class FollowerFriendlyAvoidancePatch
    {
        private static readonly FieldInfo Owner=AccessTools.Field(typeof(BotLocalAvoidance),"_owner");
        internal static void Apply(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(BotLocalAvoidance),"ManualUpdate"),
                transpiler:new HarmonyMethod(typeof(FollowerFriendlyAvoidancePatch),nameof(FilterFriendlyPairs)));
        }
        private static bool SkipFriendlyPair(BotLocalAvoidance context,BotOwner other)
        {
            if(BotTraversalPlugin.PassThroughEnabled?.Value!=true||context==null||other==null)return false;
            var bot=Owner?.GetValue(context) as BotOwner;
            return bot!=null&&FollowerEnvironment.Owns(bot)&&FollowerEnvironment.Owns(other)&&
                FollowerEnvironment.PassThroughPermitted(bot)&&FollowerEnvironment.PassThroughPermitted(other);
        }
        // Verified SPT4.0.13 loop compares local2.Id with this._owner.Id and skips self.
        // Branch to that same loop continuation for a registered friendly pair *before*
        // method_1 adds reciprocal offsets. Enemy and world collision code stays intact.
        internal static IEnumerable<CodeInstruction> FilterFriendlyPairs(IEnumerable<CodeInstruction> input)
        {
            var code=input.ToList();int start=-1,branch=-1;
            for(int i=0;i+7<code.Count;i++)
                if(code[i].opcode==OpCodes.Ldloc_2&&code[i+1].operand is MethodInfo id&&id.Name=="get_Id"&&
                    code[i+2].opcode==OpCodes.Ldarg_0)
                {
                    for(int j=i+3;j<Math.Min(i+9,code.Count);j++)
                        if(code[j].opcode==OpCodes.Beq||code[j].opcode==OpCodes.Beq_S){start=i;branch=j;break;}
                    if(start>=0)break;
                }
            if(start<0||Owner==null)throw new InvalidOperationException("SPT4.0.13 friendly avoidance loop contract unavailable");
            var first=new CodeInstruction(OpCodes.Ldarg_0);first.labels.AddRange(code[start].labels);code[start].labels.Clear();
            code.InsertRange(start,new[]{first,new CodeInstruction(OpCodes.Ldloc_2),
                new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(FollowerFriendlyAvoidancePatch),nameof(SkipFriendlyPair))),
                new CodeInstruction(OpCodes.Brtrue,code[branch].operand)});
            return code;
        }
    }
}
