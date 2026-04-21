using System.Collections.Generic;

namespace StrategicAdvisorAI.AI
{
    public static class PlanCatalog
    {
        public const string DefenseHill = "DEFENSE_HILL";
        public const string DefenseCompact = "DEFENSE_COMPACT";
        public const string AggressivePush = "AGGRESSIVE_PUSH";
        public const string RangedAnchor = "RANGED_ANCHOR";
        public const string CavalryHarass = "CAVALRY_HARASS";
        public const string FlankLeft = "FLANK_LEFT";
        public const string FlankRight = "FLANK_RIGHT";
        public const string RushArchers = "RUSH_ARCHERS";
        public const string SkirmishDelay = "SKIRMISH_DELAY";
        public const string AllInCharge = "ALL_IN_CHARGE";

        public const string AntiCavalryBrace = "ANTI_CAVALRY_BRACE";
        public const string AntiArcherRush = "ANTI_ARCHER_RUSH";
        public const string EliteShockPush = "ELITE_SHOCK_PUSH";
        public const string ProtectFlanks = "PROTECT_FLANKS";
        public const string StopBreakthrough = "STOP_BREAKTHROUGH";

        public const string SiegeAttackLadders = "SIEGE_ATTACK_LADDERS";
        public const string SiegeAttackGate = "SIEGE_ATTACK_GATE";
        public const string SiegeAttackMissilePressure = "SIEGE_ATTACK_MISSILE_PRESSURE";
        public const string SiegeAttackReservePush = "SIEGE_ATTACK_RESERVE_PUSH";
        public const string SiegeAttackSplitPressure = "SIEGE_ATTACK_SPLIT_PRESSURE";

        public const string SiegeDefendWalls = "SIEGE_DEFEND_WALLS";
        public const string SiegeDefendLadders = "SIEGE_DEFEND_LADDERS";
        public const string SiegeDefendGate = "SIEGE_DEFEND_GATE";
        public const string SiegeDefendMissileAttrition = "SIEGE_DEFEND_MISSILE_ATTRITION";
        public const string SiegeDefendReserveCounter = "SIEGE_DEFEND_RESERVE_COUNTER";

        public static readonly List<string> FieldBattlePlans = new List<string>
        {
            DefenseHill,
            DefenseCompact,
            AggressivePush,
            RangedAnchor,
            CavalryHarass,
            FlankLeft,
            FlankRight,
            RushArchers,
            SkirmishDelay,
            AllInCharge,
            AntiCavalryBrace,
            AntiArcherRush,
            EliteShockPush,
            ProtectFlanks,
            StopBreakthrough
        };

        public static readonly List<string> SiegeAttackerPlans = new List<string>
        {
            SiegeAttackLadders,
            SiegeAttackGate,
            SiegeAttackMissilePressure,
            SiegeAttackReservePush,
            SiegeAttackSplitPressure
        };

        public static readonly List<string> SiegeDefenderPlans = new List<string>
        {
            SiegeDefendWalls,
            SiegeDefendLadders,
            SiegeDefendGate,
            SiegeDefendMissileAttrition,
            SiegeDefendReserveCounter
        };
    }
}