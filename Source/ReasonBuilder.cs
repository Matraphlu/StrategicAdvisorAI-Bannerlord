using System.Collections.Generic;
using StrategicAdvisorAI.AI;

namespace StrategicAdvisorAI
{
    public static class ReasonBuilder
    {
        public static List<string> Build(float[] f, string planId, AdvisorBattleKind battleKind, AdvisorSiegeRole siegeRole)
        {
            List<string> reasons = new List<string>();

            if (f[23] > 0.5f)
                reasons.Add("Breakthrough detected. The center needs immediate stabilization.");

            if (f[24] > 0.5f)
                reasons.Add("An exposed flank was detected. Protection is urgent.");

            if (f[22] > 0.5f)
                reasons.Add("Enemy charge detected. Fast reaction matters now.");

            if (f[30] > 0.5f)
                reasons.Add("Enemy cavalry concentration is high.");

            if (f[29] > 0.5f)
                reasons.Add("Enemy archer concentration is high.");

            if (f[27] > 0.20f)
                reasons.Add("Your army quality is strong enough for elite pressure.");

            switch (planId)
            {
                case PlanCatalog.AntiCavalryBrace:
                    reasons.Add("This plan braces the line and protects vulnerable edges.");
                    break;
                case PlanCatalog.AntiArcherRush:
                    reasons.Add("This plan closes distance fast to shut down archer value.");
                    break;
                case PlanCatalog.EliteShockPush:
                    reasons.Add("This plan uses troop quality for a decisive forward punch.");
                    break;
                case PlanCatalog.ProtectFlanks:
                    reasons.Add("This plan rotates formations to secure the sides.");
                    break;
                case PlanCatalog.StopBreakthrough:
                    reasons.Add("This plan commits reserves to stop a local collapse.");
                    break;
            }

            while (reasons.Count > 4)
                reasons.RemoveAt(reasons.Count - 1);

            return reasons;
        }
    }
}