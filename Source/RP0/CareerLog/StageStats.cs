using MechJebLib.FuelFlowSimulation;
using ROUtils.DataTypes;

namespace RP0
{
    public class StageStats : ConfigNodePersistenceBase, IConfigNode
    {
        [Persistent] public int KSPStage;
        [Persistent] public double DeltaV;
        [Persistent] public double Isp;
        [Persistent] public double Thrust;
        [Persistent] public double StartTWR;
        [Persistent] public double EndTWR;
        [Persistent] public double StartMass;
        [Persistent] public double EndMass;
        [Persistent] public double StagedMass;
        [Persistent] public double DeltaTime;

        public StageStats() { }

        public StageStats(FuelStats stats)
        {
            KSPStage = stats.KSPStage;
            DeltaV = stats.DeltaV;
            Isp = stats.Isp;
            Thrust = stats.Thrust;
            StartMass = stats.StartMass;
            EndMass = stats.EndMass;
            StagedMass = stats.StagedMass;
            DeltaTime = stats.DeltaTime;
            StartTWR = stats.StartTWR(1.0);
            EndTWR = stats.MaxTWR(1.0);
        }
    }
}
