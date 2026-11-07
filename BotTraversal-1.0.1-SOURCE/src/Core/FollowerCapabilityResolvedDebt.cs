using System;
using System.Collections.Generic;
namespace BotTraversal.Core
{
    // Terminal receipts only. The existing lifecycle resolver is the sole producer.
    // This book issues no motion, completion, rank or Native-success permission.
    internal sealed class FollowerCapabilityResolvedDebt
    {
        private readonly HashSet<string> keys=new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> writers=new HashSet<string>(StringComparer.Ordinal);
        internal void MarkWriter(int raid,string bot,long generation)
        {if(raid>0&&!string.IsNullOrEmpty(bot)&&generation>0)writers.Add(raid+"|"+bot+"|"+generation);}
        internal bool HasWriter(int raid,string bot,long generation)=>raid>0&&!string.IsNullOrEmpty(bot)&&generation>0&&writers.Contains(raid+"|"+bot+"|"+generation);
        private static string Key(int raid,string bot,int episode,int edge)=>raid+"|"+bot+"|"+episode+"|"+edge;
        internal void Mark(int raid,string bot,int episode,int edge)
        {if(raid>0&&!string.IsNullOrEmpty(bot)&&episode>0&&edge>0)keys.Add(Key(raid,bot,episode,edge));}
        internal bool Contains(int raid,string bot,int episode,int edge)
            => raid>0&&!string.IsNullOrEmpty(bot)&&episode>0&&edge>0&&keys.Contains(Key(raid,bot,episode,edge));
        internal void Clear(){keys.Clear();writers.Clear();}
    }
}
