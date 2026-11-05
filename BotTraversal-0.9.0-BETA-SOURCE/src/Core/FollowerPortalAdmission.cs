using System;
using System.Collections.Generic;
namespace BotTraversal.Core
{
    // Admission only: no movement writer, native success or route-debt mutation.
    // An exact bot/mission/step ticket is released by semantic completion/cancellation.
    internal sealed class FollowerPortalAdmission
    {
        internal sealed class Ticket
        {
            internal string Bot;internal int Mission,Step;internal long ClaimSerial;
            internal string CompletedBot;internal int CompletedMission,CompletedStep=-1;internal bool LastAdmissionContinued;
            internal double X,Y,Z,DX,DZ,ReadyAt;
            internal bool SamePortal(double x,double y,double z,double dx,double dz)
                => (X-x)*(X-x)+(Z-z)*(Z-z)<=1.0&&Math.Abs(Y-y)<=.65&&DX*dx+DZ*dz>=.85;
        }
        private readonly List<Ticket> portals=new List<Ticket>();
        private long claimSequence;
        internal bool Owns(string bot,int mission,int step)=>portals.Exists(p=>p.Bot==bot&&p.Mission==mission&&p.Step==step);
        internal static bool InWindow(double distance,bool participating)=>Finite(distance)&&distance>=0&&distance<=(participating?7:5);
        private static bool Finite(double n)=>!double.IsNaN(n)&&!double.IsInfinity(n);
        internal static bool Closer(double candidate,string candidateId,double own,string ownId)
            => Finite(candidate)&&Finite(own)&&candidate>=0&&own>=0&&
                (candidate<own-.02||Math.Abs(candidate-own)<=.02&&string.CompareOrdinal(candidateId,ownId)<0);
        internal bool Admit(string bot,int mission,int step,double x,double y,double z,double dx,double dz,
            double now,bool nearest,Func<string,int,int,bool> live)
        {
            if(string.IsNullOrEmpty(bot)||mission<=0||step<0||!Finite(now)||now<0||!Finite(x)||!Finite(y)||!Finite(z)||
                !Finite(dx)||!Finite(dz)||dx*dx+dz*dz<.5||live==null||!live(bot,mission,step))return false;
            portals.RemoveAll(p=>p.Bot==null&&now>=p.ReadyAt+2);
            var p=portals.Find(t=>t.SamePortal(x,y,z,dx,dz));
            if(p!=null&&p.Bot!=null&&!live(p.Bot,p.Mission,p.Step)){p.Bot=null;p.ReadyAt=now+1;}
            if(p!=null&&p.Bot!=null)return p.Bot==bot&&p.Mission==mission&&p.Step==step;
            bool continuing=p!=null&&p.Bot==null&&now<p.ReadyAt&&p.CompletedBot==bot&&
                p.CompletedMission==mission&&step==p.CompletedStep+1;
            if((!nearest&&!continuing)||(p!=null&&now<p.ReadyAt&&!continuing))return false;
            if(p==null){p=new Ticket{X=x,Y=y,Z=z,DX=dx,DZ=dz};portals.Add(p);}
            p.Bot=bot;p.Mission=mission;p.Step=step;p.LastAdmissionContinued=continuing;p.ClaimSerial=++claimSequence;
            p.CompletedBot=null;p.CompletedMission=0;p.CompletedStep=-1;return true;
        }
        internal Ticket Inspect(double x,double y,double z,double dx,double dz)
            => portals.Find(t=>t.SamePortal(x,y,z,dx,dz));
        internal static bool HeadStalled(double now,double admitted,double progress,bool active,bool failure,bool measuredCorridor=false)
            => Finite(now)&&Finite(admitted)&&Finite(progress)&&!active&&!failure&&admitted>=0&&
                (now-progress>4||!measuredCorridor&&now-admitted>8);
        internal void Complete(string bot,int mission,int step,double now)
        {foreach(var p in portals)if(p.Bot==bot&&p.Mission==mission&&p.Step==step){p.CompletedBot=bot;p.CompletedMission=mission;p.CompletedStep=step;p.Bot=null;p.ReadyAt=now+1;}}
        internal void Cancel(string bot,double now)
        {foreach(var p in portals)if(p.Bot==bot){p.Bot=null;p.ReadyAt=now+1;}}
        internal void Clear()=>portals.Clear();
    }
}
