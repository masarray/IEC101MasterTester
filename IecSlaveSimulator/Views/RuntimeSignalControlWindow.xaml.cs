using System.Windows;
using IecSlaveSimulator.Models;

namespace IecSlaveSimulator.Views
{
    public partial class RuntimeSignalControlWindow : Window
    {
        public RuntimeSignalControlWindow(SignalDefinition workingCopy)
        {
            InitializeComponent();
            WorkingCopy = workingCopy;
            DataContext = WorkingCopy;
        }

        public SignalDefinition WorkingCopy { get; }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
