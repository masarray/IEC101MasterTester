using System.Collections.ObjectModel;
using System.Windows.Media;

namespace IEC101MasterTester.ViewModels
{
    public sealed class NucEndpointPanelViewModel : ViewModelBase
    {
        private string _title;
        private string _statusText;
        private Brush _statusBrush;

        public NucEndpointPanelViewModel()
        {
            Rows = new ObservableCollection<NucInfoRowViewModel>();
        }

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public Brush StatusBrush
        {
            get => _statusBrush;
            set => SetProperty(ref _statusBrush, value);
        }

        public ObservableCollection<NucInfoRowViewModel> Rows { get; }
    }
}
