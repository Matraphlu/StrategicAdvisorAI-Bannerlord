using TaleWorlds.MountAndBlade;

namespace StrategicAdvisorAI
{
    public class SubModule : MBSubModuleBase
    {
        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            if (mission == null)
                return;

            mission.AddMissionBehavior(new StrategicAdvisorMissionBehavior());
        }
    }
}