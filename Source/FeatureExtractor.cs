using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace StrategicAdvisorAI
{
    public static class FeatureExtractor
    {
        public const int FeatureDim = 32;

        public static float[] Extract(
            Mission mission,
            AdvisorBattleKind battleKind,
            AdvisorSiegeRole siegeRole,
            bool enemyChargeDetected,
            bool breakthroughDetected,
            bool exposedFlankDetected)
        {
            Team player = mission?.PlayerTeam;
            Team enemy = FindMainEnemy(mission, player);

            int pTotal = CountHumans(player);
            int eTotal = CountHumans(enemy);

            int pRanged = CountRanged(player);
            int eRanged = CountRanged(enemy);

            int pMounted = CountMounted(player);
            int eMounted = CountMounted(enemy);

            int pInf = Math.Max(0, pTotal - pRanged - pMounted);
            int eInf = Math.Max(0, eTotal - eRanged - eMounted);

            float pRangedShare = Share(pRanged, pTotal);
            float eRangedShare = Share(eRanged, eTotal);

            float pMountedShare = Share(pMounted, pTotal);
            float eMountedShare = Share(eMounted, eTotal);

            float pInfShare = Share(pInf, pTotal);
            float eInfShare = Share(eInf, eTotal);

            Vec3 pCenter = EstimateTeamCenter(player);
            Vec3 eCenter = EstimateTeamCenter(enemy);

            float distanceNorm = Clamp01(pCenter.Distance(eCenter) / 250f);

            TerrainInfo terrain = AnalyzeTerrain(player, enemy);

            float isSiege = battleKind == AdvisorBattleKind.Siege ? 1f : 0f;
            float isAttacker = siegeRole == AdvisorSiegeRole.Attacker ? 1f : 0f;
            float isDefender = siegeRole == AdvisorSiegeRole.Defender ? 1f : 0f;

            float eliteDiff = EstimateEliteShare(player) - EstimateEliteShare(enemy);
            float heroDiff = EstimateHeroShare(player) - EstimateHeroShare(enemy);

            float enemyCavThreat = Clamp01((eMountedShare - pMountedShare + 1f) * 0.5f);
            float enemyRangedThreat = Clamp01((eRangedShare - pRangedShare + 1f) * 0.5f);
            float numericRisk = Clamp01((1f - SignedRatio(pTotal, eTotal)) * 0.5f);

            float enemyCharge = enemyChargeDetected ? 1f : 0f;
            float breakthrough = breakthroughDetected ? 1f : 0f;
            float exposedFlank = exposedFlankDetected ? 1f : 0f;

            float enemyQuality = EstimateEliteShare(enemy);
            float playerQuality = EstimateEliteShare(player);

            float enemyArcherMass = eRangedShare > 0.40f ? 1f : 0f;
            float enemyCavMass = eMountedShare > 0.35f ? 1f : 0f;
            float playerEliteMass = playerQuality > 0.20f ? 1f : 0f;

            return new[]
            {
                SignedRatio(pTotal, eTotal),     // 0
                pRangedShare - eRangedShare,     // 1
                pMountedShare - eMountedShare,   // 2
                pInfShare - eInfShare,           // 3
                pRangedShare,                    // 4
                eRangedShare,                    // 5
                pMountedShare,                   // 6
                eMountedShare,                   // 7
                pInfShare,                       // 8
                eInfShare,                       // 9
                eliteDiff,                       // 10
                distanceNorm,                    // 11
                terrain.HeightAdvantage,         // 12
                terrain.Openness,                // 13
                Clamp01(pTotal / 300f),          // 14
                isSiege,                         // 15
                isAttacker,                      // 16
                isDefender,                      // 17
                heroDiff,                        // 18
                enemyRangedThreat,               // 19
                enemyCavThreat,                  // 20
                numericRisk,                     // 21
                enemyCharge,                     // 22
                breakthrough,                    // 23
                exposedFlank,                    // 24
                terrain.SlopeAdvantage,          // 25
                terrain.CoverScore,              // 26
                playerQuality,                   // 27
                enemyQuality,                    // 28
                enemyArcherMass,                 // 29
                enemyCavMass,                    // 30
                1f                               // 31 bias
            };
        }

        public static Team FindMainEnemy(Mission mission, Team player)
        {
            if (mission == null || player == null || mission.Teams == null)
                return null;

            Team best = null;
            int bestCount = -1;

            foreach (Team t in mission.Teams)
            {
                if (t == null || t == player || !t.IsEnemyOf(player))
                    continue;

                int count = CountHumans(t);
                if (count > bestCount)
                {
                    bestCount = count;
                    best = t;
                }
            }

            return best;
        }

        public static int CountHumans(Team team)
        {
            if (team == null || team.ActiveAgents == null) return 0;
            int total = 0;
            foreach (Agent a in team.ActiveAgents)
                if (a != null && a.IsHuman) total++;
            return total;
        }

        public static int CountMounted(Team team)
        {
            if (team == null || team.ActiveAgents == null) return 0;
            int total = 0;
            foreach (Agent a in team.ActiveAgents)
                if (a != null && a.IsHuman && a.HasMount) total++;
            return total;
        }

        public static int CountRanged(Team team)
        {
            if (team == null || team.ActiveAgents == null) return 0;
            int total = 0;
            foreach (Agent a in team.ActiveAgents)
                if (a != null && a.IsHuman && a.Character != null && a.Character.IsRanged) total++;
            return total;
        }

        public static Vec3 EstimateTeamCenter(Team team)
        {
            if (team == null || team.ActiveAgents == null)
                return Vec3.Zero;

            Vec3 sum = Vec3.Zero;
            int count = 0;

            foreach (Agent a in team.ActiveAgents)
            {
                if (a == null || !a.IsHuman)
                    continue;

                sum += a.Position;
                count++;
            }

            return count > 0 ? sum / count : Vec3.Zero;
        }

        private static float EstimateEliteShare(Team team)
        {
            if (team == null || team.ActiveAgents == null) return 0f;

            int total = 0;
            int elite = 0;

            foreach (Agent a in team.ActiveAgents)
            {
                if (a == null || !a.IsHuman) continue;
                total++;
                if (a.IsHero) elite++;
            }

            return Share(elite, total);
        }

        private static float EstimateHeroShare(Team team)
        {
            if (team == null || team.ActiveAgents == null) return 0f;

            int total = 0;
            int heroes = 0;

            foreach (Agent a in team.ActiveAgents)
            {
                if (a == null || !a.IsHuman) continue;
                total++;
                if (a.IsHero) heroes++;
            }

            return Share(heroes, total);
        }

        private static TerrainInfo AnalyzeTerrain(Team player, Team enemy)
        {
            Vec3 p = EstimateTeamCenter(player);
            Vec3 e = EstimateTeamCenter(enemy);

            float dz = e.z - p.z;
            float heightAdvantage = Clamp(dz / 20f, -1f, 1f);
            float openness = Clamp01(1f - Math.Abs(dz) / 20f);
            float slopeAdvantage = Clamp(heightAdvantage * 0.8f, -1f, 1f);

            // approximation légère tant qu’on n’a pas de scan terrain avancé
            float coverScore = Clamp01(1f - openness);

            return new TerrainInfo
            {
                HeightAdvantage = heightAdvantage,
                Openness = openness,
                SlopeAdvantage = slopeAdvantage,
                CoverScore = coverScore
            };
        }

        private static float Share(int part, int total)
        {
            if (total <= 0) return 0f;
            return Clamp01(part / (float)total);
        }

        private static float SignedRatio(int a, int b)
        {
            if (b <= 0) return 1f;
            if (a <= 0) return -1f;

            float ratio = (float)a / b;

            if (ratio >= 3f) return 1f;
            if (ratio >= 2f) return 0.75f;
            if (ratio >= 1.5f) return 0.5f;
            if (ratio >= 1.2f) return 0.25f;
            if (ratio >= 0.8f) return 0f;
            if (ratio >= 0.6f) return -0.25f;
            if (ratio >= 0.4f) return -0.5f;
            return -0.75f;
        }

        private static float Clamp01(float v) => Clamp(v, 0f, 1f);

        private static float Clamp(float v, float min, float max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        private struct TerrainInfo
        {
            public float HeightAdvantage;
            public float Openness;
            public float SlopeAdvantage;
            public float CoverScore;
        }
    }
}