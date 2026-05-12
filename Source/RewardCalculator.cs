namespace StrategicAdvisorAI
{
    public static class RewardCalculator
    {
        public static float Calculate(BattleSession session)
        {
            float playerCas = session.PlayerStartCount > 0
                ? (session.PlayerStartCount - session.PlayerAliveAtEnd) / (float)session.PlayerStartCount
                : 0f;

            float enemyCas = session.EnemyStartCount > 0
                ? (session.EnemyStartCount - session.EnemyAliveAtEnd) / (float)session.EnemyStartCount
                : 0f;

            float result = session.PlayerWon ? 1f : -1f;
            float survivalBonus = 0.45f * (1f - playerCas);
            float attritionBonus = 0.25f * enemyCas;

            return result + survivalBonus + attritionBonus;
        }
    }
}