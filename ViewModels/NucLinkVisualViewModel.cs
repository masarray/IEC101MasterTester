using System.Collections.ObjectModel;
using System.Windows.Media;

namespace IEC101MasterTester.ViewModels
{
    public sealed class NucLinkVisualViewModel : ViewModelBase
    {
        private string _linkName;
        private string _stateText;
        private string _roleText;
        private string _healthText;
        private Brush _stateBrush;
        private Brush _healthBrush;
        private Brush _lineBrush;
        private Brush _cardBrush;
        private Brush _pulseBrush;
        private bool _isDataFlowActive;

        public NucLinkVisualViewModel()
        {
            Badges = new ObservableCollection<NucStatusBadgeViewModel>();
            Rows = new ObservableCollection<NucInfoRowViewModel>();
        }

        public string LinkName
        {
            get => _linkName;
            set => SetProperty(ref _linkName, value);
        }

        public string StateText
        {
            get => _stateText;
            set => SetProperty(ref _stateText, value);
        }

        public string RoleText
        {
            get => _roleText;
            set => SetProperty(ref _roleText, value);
        }

        public string HealthText
        {
            get => _healthText;
            set => SetProperty(ref _healthText, value);
        }

        public Brush StateBrush
        {
            get => _stateBrush;
            set => SetProperty(ref _stateBrush, value);
        }

        public Brush HealthBrush
        {
            get => _healthBrush;
            set => SetProperty(ref _healthBrush, value);
        }

        public Brush LineBrush
        {
            get => _lineBrush;
            set => SetProperty(ref _lineBrush, value);
        }

        public Brush CardBrush
        {
            get => _cardBrush;
            set => SetProperty(ref _cardBrush, value);
        }

        public Brush PulseBrush
        {
            get => _pulseBrush;
            set => SetProperty(ref _pulseBrush, value);
        }

        public bool IsDataFlowActive
        {
            get => _isDataFlowActive;
            set => SetProperty(ref _isDataFlowActive, value);
        }

        public ObservableCollection<NucStatusBadgeViewModel> Badges { get; }

        public ObservableCollection<NucInfoRowViewModel> Rows { get; }
    }
}
