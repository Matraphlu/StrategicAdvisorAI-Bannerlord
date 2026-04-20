namespace StrategicAdvisorAI
{
    public enum AdvisorBattleKind
    {
        Field,
        Siege,
        Disabled
    }

    public enum AdvisorSiegeRole
    {
        None,
        Attacker,
        Defender
    }

    public class BattleSession
    {
        public string PlanId;
        public float[] Features;
        public float Confidence;

        public bool AutoCommandEnabled;
        public bool FollowChoiceMade;

        public int PlayerStartCount;
        public int EnemyStartCount;

        public int PlayerAliveAtEnd;
        public int EnemyAliveAtEnd;

        public bool PlayerWon;

        public AdvisorBattleKind BattleKind;
        public AdvisorSiegeRole SiegeRole;

        public bool EnemyChargeDetected;
        public bool BreakthroughDetected;
        public bool ExposedFlankDetected;

        public float LastEnemyDistance;
        public float LastPlanChangeTime;
    }
}