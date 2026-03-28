using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using IEC101MasterTester.Models;
using IEC101MasterTester.ViewModels;

namespace IEC101MasterTester.Views
{
    public partial class SignalCommandWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private string _lastSendSignature;
        private DateTime _lastSendAtUtc;

        public SignalCommandWindow(MainViewModel viewModel, SignalCommandWindowModel model)
        {
            InitializeComponent();
            _viewModel = viewModel;
            Model = model;
            DataContext = Model;
        }

        public SignalCommandWindowModel Model { get; }

        private async void DirectPrimary_Click(object sender, RoutedEventArgs e) => await SendAsync(Model.PrimaryOperation, false);
        private async void DirectSecondary_Click(object sender, RoutedEventArgs e) => await SendAsync(Model.SecondaryOperation, false);
        private async void SelectPrimary_Click(object sender, RoutedEventArgs e) => await SendAsync(Model.PrimaryOperation, true);
        private async void SelectSecondary_Click(object sender, RoutedEventArgs e) => await SendAsync(Model.SecondaryOperation, true);
        private async void ExecPrimary_Click(object sender, RoutedEventArgs e) => await SendAsync(Model.PrimaryOperation, false);
        private async void ExecSecondary_Click(object sender, RoutedEventArgs e) => await SendAsync(Model.SecondaryOperation, false);

        private async Task SendAsync(string operation, bool select)
        {
            if (string.IsNullOrWhiteSpace(operation))
            {
                return;
            }

            string signature = string.Format(
                "{0}|{1}|{2}|{3}",
                Model.Family ?? string.Empty,
                Model.CommandIoa,
                operation,
                select ? 1 : 0);

            try
            {
                await _sendLock.WaitAsync();

                DateTime nowUtc = DateTime.UtcNow;
                if (string.Equals(_lastSendSignature, signature, StringComparison.Ordinal)
                    && (nowUtc - _lastSendAtUtc).TotalMilliseconds < 300)
                {
                    return;
                }

                _lastSendSignature = signature;
                _lastSendAtUtc = nowUtc;
                Cursor = System.Windows.Input.Cursors.Wait;

                if (Model.UseNucSession)
                {
                    await _viewModel.SendNucSignalCommandAsync(Model.Family, Model.CommandIoa, operation, select);
                }
                else
                {
                    await _viewModel.SendSignalCommandAsync(Model.Family, Model.CommandIoa, operation, select);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Signal Command", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Cursor = System.Windows.Input.Cursors.Arrow;
                _sendLock.Release();
            }
        }
    }

    public sealed class SignalCommandWindowModel : ViewModelBase
    {
        private int _commandIoa;

        public string Family { get; set; }
        public string SignalName { get; set; }
        public string SignalInfo { get; set; }
        public string PrimaryOperation { get; set; }
        public string SecondaryOperation { get; set; }
        public string DirectPrimaryLabel { get; set; }
        public string DirectSecondaryLabel { get; set; }
        public string SelectPrimaryLabel { get; set; }
        public string SelectSecondaryLabel { get; set; }
        public string ExecPrimaryLabel { get; set; }
        public string ExecSecondaryLabel { get; set; }
        public ObservableCollection<CommandLifeMonitorRow> CommandLifeMonitor { get; set; }
        public bool UseNucSession { get; set; }
        public int CommandIoa { get => _commandIoa; set => SetProperty(ref _commandIoa, value); }
    }
}
