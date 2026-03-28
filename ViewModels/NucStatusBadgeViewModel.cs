using System.Windows.Media;

namespace IEC101MasterTester.ViewModels
{
    public sealed class NucStatusBadgeViewModel : ViewModelBase
    {
        private string _text;
        private Brush _backgroundBrush;
        private Brush _borderBrush;
        private Brush _foregroundBrush;

        public string Text
        {
            get => _text;
            set => SetProperty(ref _text, value);
        }

        public Brush BackgroundBrush
        {
            get => _backgroundBrush;
            set => SetProperty(ref _backgroundBrush, value);
        }

        public Brush BorderBrush
        {
            get => _borderBrush;
            set => SetProperty(ref _borderBrush, value);
        }

        public Brush ForegroundBrush
        {
            get => _foregroundBrush;
            set => SetProperty(ref _foregroundBrush, value);
        }
    }
}
