using System.Collections.Generic;

namespace StrategicAdvisorAI
{
    public class AdvisorSnapshot
    {
        public bool Visible = true;
        public bool HasPlan = false;
        public string Header = "Strategic Advisor";
        public string PlanName = "Analysis pending";
        public string ConfidenceText = "Confidence: --";
        public string BattleTypeText = "Battle type: --";
        public string FollowText = "Follow plan: OFF";
        public List<string> Reasons = new List<string>();
    }

    public static class AdvisorRuntimeState
    {
        private static readonly object _lock = new object();
        private static AdvisorSnapshot _snapshot = new AdvisorSnapshot();
        private static int _version = 0;

        public static int Version
        {
            get
            {
                lock (_lock) return _version;
            }
        }

        public static AdvisorSnapshot GetSnapshot()
        {
            lock (_lock)
            {
                return new AdvisorSnapshot
                {
                    Visible = _snapshot.Visible,
                    HasPlan = _snapshot.HasPlan,
                    Header = _snapshot.Header,
                    PlanName = _snapshot.PlanName,
                    ConfidenceText = _snapshot.ConfidenceText,
                    BattleTypeText = _snapshot.BattleTypeText,
                    FollowText = _snapshot.FollowText,
                    Reasons = new List<string>(_snapshot.Reasons)
                };
            }
        }

        public static void SetRecommendation(string header, string battleType, string planName, float confidence, List<string> reasons, bool followed)
        {
            lock (_lock)
            {
                _snapshot.Header = header;
                _snapshot.BattleTypeText = battleType;
                _snapshot.PlanName = planName;
                _snapshot.ConfidenceText = "Confidence: " + (int)(confidence * 100f) + "%";
                _snapshot.FollowText = "Follow plan: " + (followed ? "ON" : "OFF");
                _snapshot.Reasons = new List<string>(reasons);
                _snapshot.HasPlan = true;
                _version++;
            }
        }

        public static void SetFollowed(bool followed)
        {
            lock (_lock)
            {
                _snapshot.FollowText = "Follow plan: " + (followed ? "ON" : "OFF");
                _version++;
            }
        }

        public static void ToggleVisible()
        {
            lock (_lock)
            {
                _snapshot.Visible = !_snapshot.Visible;
                _version++;
            }
        }

        public static void Reset()
        {
            lock (_lock)
            {
                _snapshot = new AdvisorSnapshot();
                _version++;
            }
        }
    }
}