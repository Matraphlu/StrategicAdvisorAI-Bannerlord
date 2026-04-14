using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace StrategicAdvisorAI.AI
{
    [DataContract]
    public class BrainState
    {
        [DataMember] public int FeatureDim { get; set; }
        [DataMember] public float LearningRate { get; set; } = 0.05f;
        [DataMember] public float Epsilon { get; set; } = 0.15f;
        [DataMember] public int BattlesLearned { get; set; } = 0;
        [DataMember] public Dictionary<string, float[]> Weights { get; set; } = new Dictionary<string, float[]>();
        [DataMember] public Dictionary<string, int> TimesChosen { get; set; } = new Dictionary<string, int>();
    }

    public class StrategyBrain
    {
        public BrainState State { get; private set; }
        private readonly Random _rng = new Random();

        public StrategyBrain(int dim)
        {
            State = new BrainState { FeatureDim = dim };
        }

        public static StrategyBrain LoadOrCreate(string path, int dim)
        {
            try
            {
                if (File.Exists(path))
                {
                    using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                    {
                        var serializer = new DataContractJsonSerializer(typeof(BrainState));
                        BrainState state = serializer.ReadObject(fs) as BrainState;
                        if (state != null && state.FeatureDim == dim)
                        {
                            var brain = new StrategyBrain(dim);
                            brain.State = state;
                            return brain;
                        }
                    }
                }
            }
            catch
            {
            }

            return new StrategyBrain(dim);
        }

        public void Save(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                var serializer = new DataContractJsonSerializer(typeof(BrainState));
                serializer.WriteObject(fs, State);
            }
        }

        public BrainDecision ChoosePlan(float[] x, List<string> plans)
        {
            EnsureAll(plans);
            DecayExploration();

            // domination totale
            if (x != null && x.Length > 0 && x[0] > 0.7f && plans.Contains(PlanCatalog.AllInCharge))
            {
                return new BrainDecision
                {
                    PlanId = PlanCatalog.AllInCharge,
                    Confidence = 0.95f,
                    Exploration = false,
                    BestScore = 999f,
                    SecondScore = 0f
                };
            }

            // anti-cavalerie
            if (x != null && x.Length > 30 && x[30] > 0.5f && plans.Contains(PlanCatalog.AntiCavalryBrace))
            {
                return new BrainDecision
                {
                    PlanId = PlanCatalog.AntiCavalryBrace,
                    Confidence = 0.88f,
                    Exploration = false,
                    BestScore = 500f,
                    SecondScore = 0f
                };
            }

            // anti-archers
            if (x != null && x.Length > 29 && x[29] > 0.5f && plans.Contains(PlanCatalog.AntiArcherRush))
            {
                return new BrainDecision
                {
                    PlanId = PlanCatalog.AntiArcherRush,
                    Confidence = 0.86f,
                    Exploration = false,
                    BestScore = 480f,
                    SecondScore = 0f
                };
            }

            // percée
            if (x != null && x.Length > 23 && x[23] > 0.5f && plans.Contains(PlanCatalog.StopBreakthrough))
            {
                return new BrainDecision
                {
                    PlanId = PlanCatalog.StopBreakthrough,
                    Confidence = 0.90f,
                    Exploration = false,
                    BestScore = 520f,
                    SecondScore = 0f
                };
            }

            // flanc exposé
            if (x != null && x.Length > 24 && x[24] > 0.5f && plans.Contains(PlanCatalog.ProtectFlanks))
            {
                return new BrainDecision
                {
                    PlanId = PlanCatalog.ProtectFlanks,
                    Confidence = 0.87f,
                    Exploration = false,
                    BestScore = 510f,
                    SecondScore = 0f
                };
            }

            // armée élite
            if (x != null && x.Length > 27 && x[27] > 0.20f && plans.Contains(PlanCatalog.EliteShockPush))
            {
                return new BrainDecision
                {
                    PlanId = PlanCatalog.EliteShockPush,
                    Confidence = 0.84f,
                    Exploration = false,
                    BestScore = 450f,
                    SecondScore = 0f
                };
            }

            if (_rng.NextDouble() < State.Epsilon)
            {
                string pick = plans[_rng.Next(plans.Count)];
                BumpChosen(pick);
                return new BrainDecision
                {
                    PlanId = pick,
                    Confidence = 0.35f,
                    Exploration = true,
                    BestScore = 0f,
                    SecondScore = 0f
                };
            }

            string bestPlan = plans[0];
            float best = Score(plans[0], x);
            float second = float.NegativeInfinity;

            for (int i = 1; i < plans.Count; i++)
            {
                float s = Score(plans[i], x);
                if (s > best)
                {
                    second = best;
                    best = s;
                    bestPlan = plans[i];
                }
                else if (s > second)
                {
                    second = s;
                }
            }

            if (float.IsNegativeInfinity(second))
                second = best - 0.05f;

            BumpChosen(bestPlan);

            return new BrainDecision
            {
                PlanId = bestPlan,
                Confidence = Sigmoid(best - second),
                Exploration = false,
                BestScore = best,
                SecondScore = second
            };
        }

        public void Update(string plan, float[] x, float reward)
        {
            Ensure(plan);

            float[] w = State.Weights[plan];
            float pred = Dot(w, x);
            float err = reward - pred;

            for (int i = 0; i < w.Length; i++)
                w[i] += State.LearningRate * err * x[i];

            State.BattlesLearned++;
        }

        private void DecayExploration()
        {
            float floor = 0.04f;
            float dynamic = 0.18f * (float)Math.Exp(-State.BattlesLearned / 220.0);
            State.Epsilon = Math.Max(floor, dynamic);
        }

        private void EnsureAll(List<string> plans)
        {
            foreach (var p in plans)
                Ensure(p);
        }

        private void Ensure(string plan)
        {
            if (!State.Weights.ContainsKey(plan) || State.Weights[plan] == null || State.Weights[plan].Length != State.FeatureDim)
                State.Weights[plan] = new float[State.FeatureDim];

            if (!State.TimesChosen.ContainsKey(plan))
                State.TimesChosen[plan] = 0;
        }

        private void BumpChosen(string plan)
        {
            if (!State.TimesChosen.ContainsKey(plan))
                State.TimesChosen[plan] = 0;

            State.TimesChosen[plan]++;
        }

        private float Score(string plan, float[] x)
        {
            Ensure(plan);
            return Dot(State.Weights[plan], x);
        }

        private static float Dot(float[] w, float[] x)
        {
            float s = 0f;
            int n = Math.Min(w.Length, x.Length);
            for (int i = 0; i < n; i++)
                s += w[i] * x[i];
            return s;
        }

        private static float Sigmoid(float z)
        {
            if (z > 10f) return 0.9999f;
            if (z < -10f) return 0.0001f;
            return 1f / (1f + (float)Math.Exp(-z));
        }
        public string GetStatsSummary()
        {
            string bestPlan = "None";
            int bestCount = -1;

            foreach (var kv in State.TimesChosen)
            {
                if (kv.Value > bestCount)
                {
                    bestCount = kv.Value;
                    bestPlan = kv.Key;
                }
            }

            return "Battles learned: " + State.BattlesLearned + " | Most chosen plan: " + bestPlan;
        }
    }

    public class BrainDecision
    {
        public string PlanId { get; set; }
        public float Confidence { get; set; }
        public float BestScore { get; set; }
        public float SecondScore { get; set; }
        public bool Exploration { get; set; }
    }
}