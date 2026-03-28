namespace IEC101MasterTester.ViewModels
{
    public sealed class NucInfoRowViewModel : ViewModelBase
    {
        private string _label;
        private string _value;

        public string Label
        {
            get => _label;
            set => SetProperty(ref _label, value);
        }

        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }
    }
}
