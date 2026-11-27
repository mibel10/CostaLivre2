using System;

namespace CostaLivre
{
    internal sealed class CostaAdaptiveBudget
    {
        internal enum Pressure { None, Gpu, Cpu, Memory, Mixed, Unknown }
        private int slow, fast;
        private double cooldown;
        private double frame, cpu, gpu;
        private double cpuAt = double.NegativeInfinity, gpuAt = double.NegativeInfinity;
        internal Pressure Bottleneck { get; private set; }
        internal int GpuTier { get; private set; }
        internal int CpuTier { get; private set; }
        internal double FrameMs => frame;

        internal bool Sample(double now,int target,double frameMs,double cpuMs,double gpuMs,bool memory)
        {
            static double Smooth(double old,double next) => old == 0 ? next : old*.72 + next*.28;
            frame = Smooth(frame,frameMs);
            if (now - cpuAt > 1.7) cpu = 0;
            if (now - gpuAt > 1.7) gpu = 0;
            if (cpuMs > 0) { cpu = Smooth(cpu,cpuMs); cpuAt = now; }
            if (gpuMs > 0) { gpu = Smooth(gpu,gpuMs); gpuAt = now; }
            double budget=1000d/target;
            double work=Math.Max(cpu,gpu);
            Bottleneck=memory ? Pressure.Memory : gpu>budget*.8 && gpu>cpu*1.12 ? Pressure.Gpu
                : cpu>budget*.8 && cpu>gpu*1.10 ? Pressure.Cpu
                : cpu > budget*.85 && gpu > budget*.85 ? Pressure.Mixed
                : frame>budget*1.13 ? Pressure.Unknown : Pressure.None;
            bool constrained=frame>budget*1.10 && Bottleneck!=Pressure.None;
            slow = constrained ? slow+1 : 0;
            fast = !memory && frame<budget*1.045 && work<budget*.70 ? fast+1 : 0;
            if(now<cooldown) return false;
            if(slow>=3)
            {
                slow=fast=0; cooldown=now+2.5;
                if (Bottleneck == Pressure.Mixed && CpuTier < 4 && (CpuTier <= GpuTier || GpuTier >= 8))
                {
                    CpuTier++;
                    return true;
                }
                if((Bottleneck is Pressure.Gpu or Pressure.Mixed or Pressure.Unknown) && GpuTier<8) { GpuTier++; return true; }
                if((Bottleneck is Pressure.Cpu or Pressure.Memory or Pressure.Unknown) && CpuTier<4) { CpuTier++; return true; }
            }
            if(fast>=12 && (GpuTier>0 || CpuTier>0))
            {
                fast=0; cooldown=now+6;
                if(CpuTier>0) CpuTier--; else GpuTier--;
                return true;
            }
            return false;
        }

        internal bool Reset()
        {
            bool changed=GpuTier!=0||CpuTier!=0;
            GpuTier=CpuTier=slow=fast=0;
            cooldown=frame=cpu=gpu=0;
            cpuAt = gpuAt = double.NegativeInfinity;
            Bottleneck=Pressure.None;
            return changed;
        }
    }
}
