using TaleWorlds.Library;

namespace StrategicAdvisorAI.UI
{
    public class AdviceItemVM : ViewModel
    {
        private string _text;

        [DataSourceProperty]
        public string Text
        {
            get => _text;
            set
            {
                if (value != _text)
                {
                    _text = value;
                    OnPropertyChangedWithValue(value, nameof(Text));
                }
            }
        }

        public AdviceItemVM(string text)
        {
            _text = text;
        }
    }
}