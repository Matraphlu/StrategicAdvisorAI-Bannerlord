using System.Collections.Generic;
using TaleWorlds.Library;

namespace StrategicAdvisorAI.UI
{
    public class StrategicAdvisorVM : ViewModel
    {
        private bool _isVisible = true;
        private string _header = "Strategic Advisor";
        private string _battleType = "Battle type: --";
        private string _planName = "Analysis pending";
        private string _confidence = "Confidence: --";
        private string _follow = "Follow plan: OFF";
        private string _hint = "O = toggle follow plan";
        private MBBindingList<AdviceItemVM> _reasons = new MBBindingList<AdviceItemVM>();

        [DataSourceProperty]
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (value != _isVisible)
                {
                    _isVisible = value;
                    OnPropertyChangedWithValue(value, nameof(IsVisible));
                }
            }
        }

        [DataSourceProperty]
        public string Header
        {
            get => _header;
            set
            {
                if (value != _header)
                {
                    _header = value;
                    OnPropertyChangedWithValue(value, nameof(Header));
                }
            }
        }

        [DataSourceProperty]
        public string BattleType
        {
            get => _battleType;
            set
            {
                if (value != _battleType)
                {
                    _battleType = value;
                    OnPropertyChangedWithValue(value, nameof(BattleType));
                }
            }
        }

        [DataSourceProperty]
        public string PlanName
        {
            get => _planName;
            set
            {
                if (value != _planName)
                {
                    _planName = value;
                    OnPropertyChangedWithValue(value, nameof(PlanName));
                }
            }
        }

        [DataSourceProperty]
        public string Confidence
        {
            get => _confidence;
            set
            {
                if (value != _confidence)
                {
                    _confidence = value;
                    OnPropertyChangedWithValue(value, nameof(Confidence));
                }
            }
        }

        [DataSourceProperty]
        public string Follow
        {
            get => _follow;
            set
            {
                if (value != _follow)
                {
                    _follow = value;
                    OnPropertyChangedWithValue(value, nameof(Follow));
                }
            }
        }

        [DataSourceProperty]
        public string Hint
        {
            get => _hint;
            set
            {
                if (value != _hint)
                {
                    _hint = value;
                    OnPropertyChangedWithValue(value, nameof(Hint));
                }
            }
        }

        [DataSourceProperty]
        public MBBindingList<AdviceItemVM> Reasons
        {
            get => _reasons;
            set
            {
                if (value != _reasons)
                {
                    _reasons = value;
                    OnPropertyChangedWithValue(value, nameof(Reasons));
                }
            }
        }

        public void Apply(AdvisorSnapshot snapshot)
        {
            IsVisible = snapshot.Visible;
            Header = snapshot.Header;
            BattleType = snapshot.BattleTypeText;
            PlanName = snapshot.PlanName;
            Confidence = snapshot.ConfidenceText;
            Follow = snapshot.FollowText;

            Reasons.Clear();
            foreach (string reason in snapshot.Reasons)
                Reasons.Add(new AdviceItemVM(reason));
        }
    }
}