using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using IEC101MasterTester.Models;
using IEC101MasterTester.ViewModels;

namespace IEC101MasterTester.Views
{
    public partial class SetpointCommandWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private readonly DispatcherTimer _feedbackTimer;

        public SetpointCommandWindow(MainViewModel viewModel, SetpointCommandWindowModel model)
        {
            InitializeComponent();
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            Model = model ?? throw new ArgumentNullException(nameof(model));
            DataContext = Model;

            _feedbackTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(350)
            };
            _feedbackTimer.Tick += FeedbackTimer_Tick;

            Loaded += SetpointCommandWindow_Loaded;
            Closed += SetpointCommandWindow_Closed;
        }

        public SetpointCommandWindowModel Model { get; }

        private void SetpointCommandWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshFeedback(seedPercentFromFeedback: true);
            _feedbackTimer.Start();
        }

        private void SetpointCommandWindow_Closed(object sender, EventArgs e)
        {
            _feedbackTimer.Stop();
            _feedbackTimer.Tick -= FeedbackTimer_Tick;
            _sendLock.Dispose();
        }

        private void FeedbackTimer_Tick(object sender, EventArgs e)
        {
            RefreshFeedback(seedPercentFromFeedback: false);
        }

        private async void SendDirect_Click(object sender, RoutedEventArgs e) => await SendAsync(select: false);
        private async void Select_Click(object sender, RoutedEventArgs e) => await SendAsync(select: true);
        private async void Execute_Click(object sender, RoutedEventArgs e) => await SendAsync(select: false);
        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private async Task SendAsync(bool select)
        {
            float normalizedValue;
            string error;
            if (!Model.TryGetNormalizedValue(out normalizedValue, out error))
            {
                MessageBox.Show(this, error, "POOP Setpoint", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await _sendLock.WaitAsync();
                Cursor = System.Windows.Input.Cursors.Wait;
                await _viewModel.SendSetpointCommandAsync(Model.CommandIoa, normalizedValue, select, Model.UseNucSession);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "POOP Setpoint", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Cursor = System.Windows.Input.Cursors.Arrow;
                _sendLock.Release();
            }
        }

        private void RefreshFeedback(bool seedPercentFromFeedback)
        {
            ValueViewerRow row = _viewModel.TryGetCurrentValueByIoa(Model.FeedbackIoa, Model.UseNucSession);
            Model.ApplyFeedbackRow(row);

            if (!seedPercentFromFeedback || !string.IsNullOrWhiteSpace(Model.SetpointPercentText))
            {
                return;
            }

            double normalizedValue;
            if (row != null
                && double.TryParse(row.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out normalizedValue))
            {
                Model.SetpointPercentText = (normalizedValue * 100d).ToString("0.###", CultureInfo.InvariantCulture);
            }
        }
    }

    public sealed class SetpointCommandWindowModel : ViewModelBase
    {
        private string _signalName;
        private string _signalInfo;
        private int _commandIoa;
        private int _feedbackIoa;
        private string _feedbackName;
        private string _setpointPercentText;
        private string _feedbackValueText = "-";
        private string _feedbackPercentText = "-";
        private string _feedbackTimestampText = "-";
        private string _feedbackMetaText = "-";

        public string SignalName { get => _signalName; set => SetProperty(ref _signalName, value); }
        public string SignalInfo { get => _signalInfo; set => SetProperty(ref _signalInfo, value); }
        public int CommandIoa { get => _commandIoa; set => SetProperty(ref _commandIoa, value); }
        public int FeedbackIoa { get => _feedbackIoa; set => SetProperty(ref _feedbackIoa, value); }
        public string FeedbackName { get => _feedbackName; set => SetProperty(ref _feedbackName, value); }
        public ObservableCollection<CommandLifeMonitorRow> CommandLifeMonitor { get; set; }
        public bool UseNucSession { get; set; }

        public string SetpointPercentText
        {
            get => _setpointPercentText;
            set
            {
                if (SetProperty(ref _setpointPercentText, value))
                {
                    OnPropertyChanged(nameof(NormalizedPreviewText));
                }
            }
        }

        public string FeedbackValueText { get => _feedbackValueText; set => SetProperty(ref _feedbackValueText, value); }
        public string FeedbackPercentText { get => _feedbackPercentText; set => SetProperty(ref _feedbackPercentText, value); }
        public string FeedbackTimestampText { get => _feedbackTimestampText; set => SetProperty(ref _feedbackTimestampText, value); }
        public string FeedbackMetaText { get => _feedbackMetaText; set => SetProperty(ref _feedbackMetaText, value); }

        public string FeedbackHeaderText
        {
            get
            {
                return string.Format(CultureInfo.InvariantCulture, "{0} | IOA {1}", string.IsNullOrWhiteSpace(FeedbackName) ? "POAQ" : FeedbackName, FeedbackIoa);
            }
        }

        public string NormalizedPreviewText
        {
            get
            {
                float normalizedValue;
                string error;
                if (!TryGetNormalizedValue(out normalizedValue, out error))
                {
                    return string.IsNullOrWhiteSpace(error) ? "-" : error;
                }

                return string.Format(CultureInfo.InvariantCulture, "C_SE_NA_1 normalized value = {0:0.###}", normalizedValue);
            }
        }

        public bool TryGetNormalizedValue(out float normalizedValue, out string error)
        {
            normalizedValue = 0f;
            error = null;

            double percentValue;
            if (!double.TryParse(SetpointPercentText, NumberStyles.Float, CultureInfo.InvariantCulture, out percentValue))
            {
                error = "Masukkan POOP dalam persen 0 sampai 100.";
                return false;
            }

            if (percentValue < 0d || percentValue > 100d)
            {
                error = "POOP persen harus berada di antara 0 dan 100.";
                return false;
            }

            normalizedValue = (float)(percentValue / 100d);
            return true;
        }

        public void ApplyFeedbackRow(ValueViewerRow row)
        {
            if (row == null)
            {
                FeedbackValueText = "-";
                FeedbackPercentText = "-";
                FeedbackTimestampText = "-";
                FeedbackMetaText = "-";
                return;
            }

            FeedbackValueText = string.IsNullOrWhiteSpace(row.Value) ? "-" : row.Value;

            double normalizedValue;
            FeedbackPercentText = double.TryParse(row.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out normalizedValue)
                ? string.Format(CultureInfo.InvariantCulture, "{0:0.###}%", normalizedValue * 100d)
                : "-";

            FeedbackTimestampText = string.IsNullOrWhiteSpace(row.Timestamp) ? "-" : row.Timestamp;

            string cot = string.IsNullOrWhiteSpace(row.Cot) ? "-" : row.Cot;
            string trafficClass = string.IsNullOrWhiteSpace(row.TrafficClass) ? "-" : row.TrafficClass;
            FeedbackMetaText = string.Format(CultureInfo.InvariantCulture, "{0} / {1}", cot, trafficClass);
        }
    }
}
