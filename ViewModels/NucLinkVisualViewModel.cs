using System.Collections.ObjectModel;
using System.Windows.Media;

namespace IEC101MasterTester.ViewModels
{
    public sealed class NucLinkVisualViewModel : ViewModelBase
    {
        private string _linkName;
        private string _stateText;
        private Brush _stateBrush;
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

        public Brush StateBrush
        {
            get => _stateBrush;
            set => SetProperty(ref _stateBrush, value);
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
