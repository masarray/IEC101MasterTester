using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using IEC101MasterTester.Models;
using IEC101MasterTester.Models.Export;
using IEC101MasterTester.Models.Soe;
using IEC101MasterTester.Services.Export;
using IEC101MasterTester.ViewModels;
using Microsoft.Win32;

namespace IEC101MasterTester.Views
{
    public partial class NucSoeAuditWindow : Window
    {
        private const string AllFilterOption = "All";

        private readonly EventLogExportService _eventLogExportService = new EventLogExportService();
        private readonly ObservableCollection<AuditDisplayRow> _auditRows = new ObservableCollection<AuditDisplayRow>();
        private readonly List<AuditPreset> _presets = BuildPresets();
        private readonly List<string> _dataTypeOptions = new List<string> { AllFilterOption, "Binary", "Measured", "Command" };
        private readonly List<string> _classOptions = new List<string> { AllFilterOption, "Class 1", "Class 2" };
        private readonly DispatcherTimer _incomingRefreshTimer;
        private readonly DispatcherTimer _searchDebounceTimer;
        private ICollectionView _auditView;
        private MainViewModel _currentViewModel;
        private AppliedFilterState _appliedFilter = new AppliedFilterState();
        private bool _pendingIncomingRefresh;

        public NucSoeAuditWindow()
        {
            InitializeComponent();
            _incomingRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
            _incomingRefreshTimer.Tick += IncomingRefreshTimer_Tick;
            _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;
            Loaded += NucSoeAuditWindow_Loaded;
            DataContextChanged += NucSoeAuditWindow_DataContextChanged;
        }

        private void NucSoeAuditWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _appliedFilter.DataTypeFilter = "All";
            SyncDataTypeToggleVisuals();
            AttachAuditView();
        }

        private void NucSoeAuditWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            DetachCurrentViewModel();
            AttachAuditView();
        }

        private void DetachCurrentViewModel()
        {
            if (_currentViewModel == null)
            {
                return;
            }

            _currentViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            _currentViewModel.NucSoeForensicJournal.Changed -= NucSoeForensicJournal_Changed;
            _currentViewModel.BufferReplaySessions.CollectionChanged -= BufferReplaySessions_CollectionChanged;
        }

        private void AttachAuditView()
        {
            _currentViewModel = DataContext as MainViewModel;
            if (_currentViewModel == null)
            {
                _auditRows.Clear();
                _auditView = null;
                AuditGrid.ItemsSource = null;
                FilterSummaryTextBlock.Text = "No SOE audit source is attached.";
                RefreshRuntimeTruthSummary();
                RefreshAnalysisSummary();
                return;
            }

            _currentViewModel.PropertyChanged += ViewModel_PropertyChanged;
            _currentViewModel.NucSoeForensicJournal.Changed += NucSoeForensicJournal_Changed;
            _currentViewModel.BufferReplaySessions.CollectionChanged += BufferReplaySessions_CollectionChanged;

            RebuildAuditRows();
            _auditView = CollectionViewSource.GetDefaultView(_auditRows);
            _auditView.Filter = FilterAuditRow;
            AuditGrid.ItemsSource = _auditView;
            PopulateChannelFilter();
            PopulateEventFilter();
            ApplyFilterAndRefresh();
            RefreshRuntimeTruthSummary();
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.RedundancyActiveLinkText)
                || e.PropertyName == nameof(MainViewModel.RedundancyGiObservationText)
                || e.PropertyName == nameof(MainViewModel.RedundancyContinuityText)
                || e.PropertyName == nameof(MainViewModel.RedundancySwitchSummaryText)
                || e.PropertyName == nameof(MainViewModel.LastRedundancySwitchText)
                || e.PropertyName == nameof(MainViewModel.RedundancySwitchoverCountValue)
                || e.PropertyName == nameof(MainViewModel.IsGiObservedAfterRedundancySwitch)
                || e.PropertyName == nameof(MainViewModel.NucAvailabilityAcdAssertCountValue)
                || e.PropertyName == nameof(MainViewModel.AvailabilitySessionStartedText))
            {
                RefreshRuntimeTruthSummary();
            }
        }

        private void NucSoeForensicJournal_Changed(object sender, EventArgs e)
        {
            _pendingIncomingRefresh = true;
            _incomingRefreshTimer.Stop();
            _incomingRefreshTimer.Start();
        }

        private void BufferReplaySessions_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshRuntimeTruthSummary();
            RefreshAnalysisSummary();
        }

        private void RebuildAuditRows()
        {
            _auditRows.Clear();
            if (_currentViewModel == null)
            {
                return;
            }

            DateTime? switchoverUtc = ParsePreciseTimestamp(ExtractTimestampTail(_currentViewModel.LastRedundancySwitchText));
            int rowNo = 1;
            foreach (SoeForensicRow row in _currentViewModel.NucSoeForensicJournal.Snapshot().OrderByDescending(r => r.RecvTimeUtc))
            {
                if (row == null)
                {
                    continue;
                }

                _auditRows.Add(new AuditDisplayRow
                {
                    No = rowNo++,
                    SourceRow = row,
                    Time = FormatUtc(row.RecvTimeUtc),
                    RecvTime = FormatUtc(row.RecvTimeUtc),
                    SourceTime = row.SourceTimeUtc.HasValue ? FormatUtc(row.SourceTimeUtc.Value) : "-",
                    DeltaMs = row.DeltaMs.HasValue ? row.DeltaMs.Value.ToString(CultureInfo.InvariantCulture) : "-",
                    Source = NormalizeChannel(row.Channel),
                    Name = string.IsNullOrWhiteSpace(row.SignalName) ? "-" : row.SignalName,
                    IOA = row.IOA > 0 ? row.IOA.ToString(CultureInfo.InvariantCulture) : "-",
                    Type = string.IsNullOrWhiteSpace(row.TypeIdText) ? row.TypeId.ToString(CultureInfo.InvariantCulture) : row.TypeIdText,
                    TypeId = string.IsNullOrWhiteSpace(row.TypeIdText) ? row.TypeId.ToString(CultureInfo.InvariantCulture) : row.TypeIdText,
                    Casdu = row.CA > 0 ? row.CA.ToString(CultureInfo.InvariantCulture) : "-",
                    Event = string.IsNullOrWhiteSpace(row.Origin) ? "-" : row.Origin,
                    Value = string.IsNullOrWhiteSpace(row.ValueText) ? "-" : row.ValueText,
                    Cot = string.IsNullOrWhiteSpace(row.CotText) ? "-" : row.CotText,
                    Quality = string.IsNullOrWhiteSpace(row.QualityText) ? "-" : row.QualityText,
                    SourceKind = string.IsNullOrWhiteSpace(row.Origin) ? "-" : row.Origin,
                    ClassKind = "-",
                    ReplayFlagText = "-",
                    SwitchoverContext = ClassifySwitchoverContext(row, switchoverUtc)
                });
            }
        }

        private void PopulateChannelFilter()
        {
        }

        private void PopulateEventFilter()
        {
        }

        private void SetComboSelection(ComboBox comboBox, string requestedValue)
        {
            string fallback = AllFilterOption;
            string target = string.IsNullOrWhiteSpace(requestedValue) ? fallback : requestedValue;
            IEnumerable<string> values = comboBox.ItemsSource as IEnumerable<string>;
            if (values != null && values.Any(v => string.Equals(v, target, StringComparison.OrdinalIgnoreCase)))
            {
                comboBox.SelectedItem = values.First(v => string.Equals(v, target, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                comboBox.SelectedItem = fallback;
            }
        }

        private bool FilterAuditRow(object item)
        {
            AuditDisplayRow row = item as AuditDisplayRow;
            if (row == null)
            {
                return false;
            }

            if (!MatchesIoaFilter(row.IOA, _appliedFilter.IoaFilter))
            {
                return false;
            }

            if (!MatchesContains(row.Name, _appliedFilter.SignalFilter))
            {
                return false;
            }

            string channelFilter = _appliedFilter.ChannelFilter;
            if (!IsAllFilter(channelFilter) && !string.Equals(row.Source, channelFilter, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string eventFilter = _appliedFilter.EventFilter;
            if (!IsAllFilter(eventFilter) && !MatchesContains(row.Cot, eventFilter))
            {
                return false;
            }

            if (!MatchesContains(row.Value, _appliedFilter.ValueFilter))
            {
                return false;
            }

            if (!MatchesContains(row.TypeId, _appliedFilter.TypeIdFilter))
            {
                return false;
            }

            if (!MatchesDataTypeFilter(row, _appliedFilter.DataTypeFilter))
            {
                return false;
            }

            if (!MatchesClassFilter(row, _appliedFilter.ClassFilter))
            {
                return false;
            }

            DateTime rowTimeUtc;
            bool hasRowTime = TryParseRowTime(row.SourceTime, out rowTimeUtc);
            if (_appliedFilter.FromDate.HasValue)
            {
                DateTime fromUtc = _appliedFilter.FromDate.Value.Date;
                if (!hasRowTime || rowTimeUtc < fromUtc)
                {
                    return false;
                }
            }

            if (_appliedFilter.ToDate.HasValue)
            {
                DateTime toUtc = _appliedFilter.ToDate.Value.Date.AddDays(1).AddTicks(-1);
                if (!hasRowTime || rowTimeUtc > toUtc)
                {
                    return false;
                }
            }

            string searchText = (_appliedFilter.SearchFilter ?? string.Empty).Trim();
            if (searchText.Length == 0)
            {
                return true;
            }

            string haystack = string.Join(" | ",
                row.Time,
                row.Source,
                row.Name,
                row.IOA,
                row.Type,
                row.SourceKind,
                row.ClassKind,
                row.ReplayFlagText,
                row.SwitchoverContext,
                row.Event,
                row.Value,
                row.Cot,
                row.Quality);

            return haystack.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsAllFilter(string value)
        {
            return string.IsNullOrWhiteSpace(value) || string.Equals(value, AllFilterOption, StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesContains(string haystack, string needle)
        {
            string text = haystack ?? string.Empty;
            string filter = (needle ?? string.Empty).Trim();
            return filter.Length == 0 || text.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool MatchesIoaFilter(string rowIoa, string requestedIoa)
        {
            string filter = (requestedIoa ?? string.Empty).Trim();
            if (filter.Length == 0)
            {
                return true;
            }

            HashSet<string> requested = new HashSet<string>(
                filter.Split(new[] { ',', ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries),
                StringComparer.OrdinalIgnoreCase);

            return requested.Contains(rowIoa ?? string.Empty);
        }

        private static bool MatchesDataTypeFilter(AuditDisplayRow row, string filter)
        {
            if (IsAllFilter(filter))
            {
                return true;
            }

            string typeId = row.TypeId ?? string.Empty;
            if (string.Equals(filter, "Binary", StringComparison.OrdinalIgnoreCase))
            {
                return typeId.StartsWith("M_SP", StringComparison.OrdinalIgnoreCase)
                    || typeId.StartsWith("M_DP", StringComparison.OrdinalIgnoreCase);
            }

            if (string.Equals(filter, "Measured", StringComparison.OrdinalIgnoreCase)
                || string.Equals(filter, "Analog", StringComparison.OrdinalIgnoreCase))
            {
                return typeId.StartsWith("M_ME", StringComparison.OrdinalIgnoreCase)
                    || typeId.StartsWith("M_ST", StringComparison.OrdinalIgnoreCase)
                    || typeId.StartsWith("M_IT", StringComparison.OrdinalIgnoreCase);
            }

            if (string.Equals(filter, "Command", StringComparison.OrdinalIgnoreCase))
            {
                return typeId.StartsWith("C_SC", StringComparison.OrdinalIgnoreCase)
                    || typeId.StartsWith("C_DC", StringComparison.OrdinalIgnoreCase)
                    || typeId.StartsWith("C_RC", StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }

        private static bool MatchesClassFilter(AuditDisplayRow row, string filter)
        {
            if (IsAllFilter(filter))
            {
                return true;
            }

            string cot = row.Cot ?? string.Empty;
            string typeId = row.TypeId ?? string.Empty;
            bool isCommand = typeId.StartsWith("C_SC", StringComparison.OrdinalIgnoreCase)
                || typeId.StartsWith("C_DC", StringComparison.OrdinalIgnoreCase)
                || typeId.StartsWith("C_RC", StringComparison.OrdinalIgnoreCase);
            bool isClass1 = string.Equals(cot, "Spont", StringComparison.OrdinalIgnoreCase)
                || string.Equals(cot, "Req", StringComparison.OrdinalIgnoreCase)
                || string.Equals(cot, "Act", StringComparison.OrdinalIgnoreCase)
                || string.Equals(cot, "ActCon", StringComparison.OrdinalIgnoreCase)
                || string.Equals(cot, "ActTerm", StringComparison.OrdinalIgnoreCase)
                || isCommand;
            bool isClass2 = string.Equals(cot, "GI", StringComparison.OrdinalIgnoreCase)
                || string.Equals(cot, "Periodic", StringComparison.OrdinalIgnoreCase)
                || string.Equals(cot, "BgScan", StringComparison.OrdinalIgnoreCase);

            if (string.Equals(filter, "Class 1", StringComparison.OrdinalIgnoreCase))
            {
                return isClass1;
            }

            if (string.Equals(filter, "Class 2", StringComparison.OrdinalIgnoreCase))
            {
                return isClass2;
            }

            return true;
        }

        private void FilterInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            CaptureAppliedFilterFromDraft();
            ApplyFilterAndRefresh();
            e.Handled = true;
        }

        private void ApplyFilterAndRefresh()
        {
            if (_auditView == null)
            {
                return;
            }

            _auditView.Refresh();
            RefreshFilterSummary();
            RefreshAnalysisSummary();
            RefreshRuntimeTruthSummary();
        }

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            ClearFiltersCore();
            CaptureAppliedFilterFromDraft();
            ApplyFilterAndRefresh();
        }

        private void ClearFiltersCore()
        {
            SearchFilterTextBox.Text = string.Empty;
            _appliedFilter = new AppliedFilterState
            {
                DataTypeFilter = "All"
            };
            SyncDataTypeToggleVisuals();
        }

        private void CaptureAppliedFilterFromDraft()
        {
            _appliedFilter.SearchFilter = (SearchFilterTextBox.Text ?? string.Empty).Trim();
            _appliedFilter.PresetName = null;
        }

        private void IncomingRefreshTimer_Tick(object sender, EventArgs e)
        {
            _incomingRefreshTimer.Stop();
            if (!_pendingIncomingRefresh)
            {
                return;
            }

            _pendingIncomingRefresh = false;
            RebuildAuditRows();
            PopulateChannelFilter();
            PopulateEventFilter();
            ApplyFilterAndRefresh();
            RefreshRuntimeTruthSummary();
        }

        private void SearchFilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
        }

        private void SearchDebounceTimer_Tick(object sender, EventArgs e)
        {
            _searchDebounceTimer.Stop();
            CaptureAppliedFilterFromDraft();
            ApplyFilterAndRefresh();
        }

        private void AuditGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AnalysisSelectedRowsText.Text = AuditGrid.SelectedItems.Count.ToString(CultureInfo.InvariantCulture);
        }

        private void AllData_Click(object sender, RoutedEventArgs e)
        {
            _appliedFilter.DataTypeFilter = "All";
            SyncDataTypeToggleVisuals();
            ApplyFilterAndRefresh();
        }

        private void BinaryData_Click(object sender, RoutedEventArgs e)
        {
            _appliedFilter.DataTypeFilter = "Binary";
            SyncDataTypeToggleVisuals();
            ApplyFilterAndRefresh();
        }

        private void AnalogData_Click(object sender, RoutedEventArgs e)
        {
            _appliedFilter.DataTypeFilter = "Analog";
            SyncDataTypeToggleVisuals();
            ApplyFilterAndRefresh();
        }

        private void SyncDataTypeToggleVisuals()
        {
            SetToggleButtonState(AllDataButton, string.Equals(_appliedFilter.DataTypeFilter, "All", StringComparison.OrdinalIgnoreCase));
            SetToggleButtonState(BinaryDataButton, string.Equals(_appliedFilter.DataTypeFilter, "Binary", StringComparison.OrdinalIgnoreCase));
            SetToggleButtonState(AnalogDataButton, string.Equals(_appliedFilter.DataTypeFilter, "Analog", StringComparison.OrdinalIgnoreCase));
        }

        private static void SetToggleButtonState(Button button, bool isActive)
        {
            if (button == null)
            {
                return;
            }

            button.Opacity = isActive ? 1.0 : 0.78;
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            ExportRows(GetVisibleRows(), false);
        }

        private void ExportSelectedCsv_Click(object sender, RoutedEventArgs e)
        {
            IList<AuditDisplayRow> selectedRows = AuditGrid.SelectedItems.Cast<AuditDisplayRow>().ToList();
            ExportRows(selectedRows, true);
        }

        private void ExportRows(IList<AuditDisplayRow> rowsToExport, bool sampleOnly)
        {
            if (_currentViewModel == null || _auditView == null)
            {
                MessageBox.Show(this, "SOE audit source is not available yet.", "IEC-101 NUC SOE Audit", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (rowsToExport == null || rowsToExport.Count == 0)
            {
                MessageBox.Show(this,
                    sampleOnly ? "Belum ada row yang dipilih. Export selection dibatalkan." : "Tidak ada row yang cocok dengan filter aktif. Export dibatalkan.",
                    "IEC-101 NUC SOE Audit",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            SaveFileDialog dialog = new SaveFileDialog
            {
                Title = sampleOnly ? "Export IEC60870 Event Log Selection (CSV)" : "Export IEC60870 Event Log Data (CSV)",
                Filter = "CSV File (*.csv)|*.csv",
                DefaultExt = ".csv",
                AddExtension = true,
                FileName = (sampleOnly ? "IEC60870-EventLog-Selected-" : "IEC60870-EventLog-") + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".csv"
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            EventLogExportRequest request = new EventLogExportRequest
            {
                OutputPath = dialog.FileName,
                Rows = rowsToExport.Select(MapExportRow).ToList(),
                Metadata = new EventLogExportMetadata
                {
                    Title = sampleOnly ? "IEC60870 Event Log Data - Samples" : "IEC60870 Event Log Data",
                    ModuleName = "IEC101MasterTester",
                    SourceWindow = "NUC SOE Audit",
                    SessionStartedText = _currentViewModel.AvailabilitySessionStartedText,
                    ContextSummary = BuildContextSummary(_currentViewModel),
                    FilterSummary = BuildFilterSummary(),
                    ExportedAtText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
                    SummaryRows = BuildExportSummaryRows(rowsToExport, sampleOnly)
                }
            };

            try
            {
                string outputPath = _eventLogExportService.ExportToCsv(request);
                MessageBox.Show(this, "Export CSV berhasil dibuat:\n" + outputPath, "IEC-101 NUC SOE Audit", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Export CSV gagal:\n" + ex.Message, "IEC-101 NUC SOE Audit", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private IList<KeyValuePair<string, string>> BuildExportSummaryRows(IList<AuditDisplayRow> rowsToExport, bool sampleOnly)
        {
            List<KeyValuePair<string, string>> rows = new List<KeyValuePair<string, string>>();
            BufferReplaySession replaySession = GetLatestReplaySession();
            rows.Add(new KeyValuePair<string, string>("Active Link", _currentViewModel?.RedundancyActiveLinkText ?? "Active link: Unknown"));
            rows.Add(new KeyValuePair<string, string>("Switchover Count", (_currentViewModel?.RedundancySwitchoverCountValue ?? 0).ToString(CultureInfo.InvariantCulture)));
            rows.Add(new KeyValuePair<string, string>("Last Switchover", _currentViewModel?.LastRedundancySwitchText ?? "Last switchover: -"));
            rows.Add(new KeyValuePair<string, string>("Continuity Gap", _currentViewModel?.RedundancyContinuityText ?? "Continuity gap: -"));
            rows.Add(new KeyValuePair<string, string>("GI After Switchover", _currentViewModel != null && _currentViewModel.IsGiObservedAfterRedundancySwitch ? "Observed" : "Not observed"));
            rows.Add(new KeyValuePair<string, string>("ACD Observed", _currentViewModel != null && _currentViewModel.NucAvailabilityAcdAssertCountValue > 0 ? "Yes" : "No"));
            rows.Add(new KeyValuePair<string, string>("Replay Count", replaySession != null ? replaySession.ReplayEventCount.ToString(CultureInfo.InvariantCulture) : "0"));
            rows.Add(new KeyValuePair<string, string>("Duplicate Count", replaySession != null ? replaySession.DuplicateEventCount.ToString(CultureInfo.InvariantCulture) : "0"));
            rows.Add(new KeyValuePair<string, string>("FIFO Violation Count", replaySession != null ? replaySession.FifoViolationCount.ToString(CultureInfo.InvariantCulture) : "0"));
            rows.Add(new KeyValuePair<string, string>(sampleOnly ? "Selected Rows" : "Visible Rows", rowsToExport.Count.ToString(CultureInfo.InvariantCulture)));
            rows.Add(new KeyValuePair<string, string>("Filtered Rows", rowsToExport.Count.ToString(CultureInfo.InvariantCulture)));
            return rows;
        }

        private string BuildContextSummary(MainViewModel vm)
        {
            if (vm == null)
            {
                return "-";
            }

            return string.Join(" | ",
                vm.RedundancyActiveLinkText ?? "Active link: Unknown",
                vm.RedundancySwitchSummaryText ?? "Switchover count: 0",
                vm.RedundancyContinuityText ?? "Continuity gap: -",
                vm.RedundancyGiObservationText ?? "GI after switchover: Not observed");
        }

        private string BuildFilterSummary()
        {
            List<string> parts = new List<string>();
            AppendFilterPart(parts, "DataType", NormalizeFilterText(_appliedFilter.DataTypeFilter, true));
            AppendFilterPart(parts, "Search", NormalizeFilterText(_appliedFilter.SearchFilter));
            return parts.Count == 0 ? "No filter applied" : string.Join(" | ", parts);
        }

        private static void AppendFilterPart(ICollection<string> target, string label, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                target.Add(label + "=" + value);
            }
        }

        private static string NormalizeFilterText(string value, bool treatAllAsEmpty = false)
        {
            string normalized = (value ?? string.Empty).Trim();
            if (normalized.Length == 0)
            {
                return null;
            }

            if (treatAllAsEmpty && string.Equals(normalized, AllFilterOption, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return normalized;
        }

        private void RefreshFilterSummary()
        {
            IList<AuditDisplayRow> visibleRows = GetVisibleRows();
            FilterSummaryTextBlock.Text = string.Format(
                CultureInfo.InvariantCulture,
                "Filtered rows: {0} / {1} | {2}",
                visibleRows.Count,
                _auditRows.Count,
                BuildFilterSummary());
            FilteredRowsSummaryText.Text = string.Format(CultureInfo.InvariantCulture, "Filtered: {0} / {1}", visibleRows.Count, _auditRows.Count);
        }

        private void RefreshRuntimeTruthSummary()
        {
            if (_currentViewModel == null)
            {
                ActiveLinkSummaryText.Text = "Active link: Unknown";
                GiObservationSummaryText.Text = "GI after switchover: Not observed";
                SwitchoverSummaryValueText.Text = "0";
                LastSwitchoverValueText.Text = "Last switchover: -";
                ContinuitySummaryValueText.Text = "Continuity gap: -";
                GiSummaryValueText.Text = "GI: Not observed";
                ReplaySummaryValueText.Text = "Replay: 0";
                ReplaySampleStatusText.Text = "No replay session yet.";
                DuplicateSummaryValueText.Text = "Duplicate: 0";
                FifoSummaryValueText.Text = "FIFO violation: 0";
                AcdSummaryValueText.Text = "ACD: No";
                FilteredRowsSummaryText.Text = "Filtered: 0 / 0";
                return;
            }

            BufferReplaySession replaySession = GetLatestReplaySession();
            ActiveLinkSummaryText.Text = _currentViewModel.RedundancyActiveLinkText ?? "Active link: Unknown";
            GiObservationSummaryText.Text = _currentViewModel.RedundancyGiObservationText ?? "GI after switchover: Not observed";
            SwitchoverSummaryValueText.Text = _currentViewModel.RedundancySwitchoverCountValue.ToString(CultureInfo.InvariantCulture);
            LastSwitchoverValueText.Text = _currentViewModel.LastRedundancySwitchText ?? "Last switchover: -";
            ContinuitySummaryValueText.Text = _currentViewModel.RedundancyContinuityText ?? "Continuity gap: -";
            GiSummaryValueText.Text = _currentViewModel.IsGiObservedAfterRedundancySwitch ? "GI observed after switchover" : "GI after switchover not observed";
            ReplaySummaryValueText.Text = "Replay: " + (replaySession?.ReplayEventCount ?? 0).ToString(CultureInfo.InvariantCulture);
            ReplaySampleStatusText.Text = replaySession == null
                ? "No replay session yet."
                : string.Format(
                    CultureInfo.InvariantCulture,
                "Replay: {0} | Min600: {1} | Verdict: {2}",
                    replaySession.SampleCheckCount,
                    replaySession.MeetsMinimum600Events ? "PASS" : "WAIT",
                    replaySession.FinalVerdict ?? "-");
            DuplicateSummaryValueText.Text = "Duplicate: " + (replaySession?.DuplicateEventCount ?? 0).ToString(CultureInfo.InvariantCulture);
            FifoSummaryValueText.Text = "FIFO violation: " + (replaySession?.FifoViolationCount ?? 0).ToString(CultureInfo.InvariantCulture);
            AcdSummaryValueText.Text = _currentViewModel.NucAvailabilityAcdAssertCountValue > 0
                ? "ACD: Yes (" + _currentViewModel.NucAvailabilityAcdAssertCountValue.ToString(CultureInfo.InvariantCulture) + ")"
                : "ACD: No";
        }

        private void RefreshAnalysisSummary()
        {
            IList<AuditDisplayRow> visibleRows = GetVisibleRows();
            AnalysisRowCountText.Text = visibleRows.Count.ToString(CultureInfo.InvariantCulture);
            AnalysisUniqueIoaText.Text = visibleRows
                .Select(r => r.IOA)
                .Where(ioa => !string.IsNullOrWhiteSpace(ioa) && ioa != "-")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count()
                .ToString(CultureInfo.InvariantCulture);
            AnalysisSelectedRowsText.Text = AuditGrid.SelectedItems.Count.ToString(CultureInfo.InvariantCulture);

            List<DateTime> sourceTimes = visibleRows
                .Select(r =>
                {
                    DateTime parsed;
                    return TryParseRowTime(r.SourceTime, out parsed) ? (DateTime?)parsed : null;
                })
                .Where(t => t.HasValue)
                .Select(t => t.Value)
                .OrderBy(t => t)
                .ToList();

            if (sourceTimes.Count == 0)
            {
                AnalysisTimeRangeText.Text = "-";
                AnalysisDeltaText.Text = "Source time missing: " + visibleRows.Count.ToString(CultureInfo.InvariantCulture)
                        + " | Delta: -";
            }
            else
            {
                AnalysisTimeRangeText.Text = sourceTimes.First().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)
                    + " -> "
                    + sourceTimes.Last().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);

                List<double> deltas = new List<double>();
                foreach (AuditDisplayRow row in visibleRows)
                {
                    double delta;
                    if (double.TryParse(row.DeltaMs, NumberStyles.Float, CultureInfo.InvariantCulture, out delta))
                    {
                        deltas.Add(delta);
                    }
                }

                int missingSourceTimeCount = visibleRows.Count(r => string.IsNullOrWhiteSpace(r.SourceTime) || r.SourceTime == "-");
                AnalysisDeltaText.Text = deltas.Count == 0
                    ? "Source time missing: " + missingSourceTimeCount.ToString(CultureInfo.InvariantCulture)
                        + " | Delta: - | Samples: " + visibleRows.Count(r => r.IsSample).ToString(CultureInfo.InvariantCulture)
                    : string.Format(
                        CultureInfo.InvariantCulture,
                        "Source time missing: {0} | Delta min/max/avg: {1:F0}/{2:F0}/{3:F0} ms",
                        missingSourceTimeCount,
                        deltas.Min(),
                        deltas.Max(),
                        deltas.Average());
            }

            int replayCount = visibleRows.Count(r => string.Equals(r.Cot, "Spont", StringComparison.OrdinalIgnoreCase) || string.Equals(r.Event, "Command", StringComparison.OrdinalIgnoreCase));
            AnalysisReplayText.Text = "Replay/Events: " + replayCount.ToString(CultureInfo.InvariantCulture);

            int duplicateCount = CountDuplicateVisibleRows(visibleRows);
            int fifoViolationCount = CountVisibleFifoViolations(visibleRows);
            AnalysisDuplicateFifoText.Text = string.Format(
                CultureInfo.InvariantCulture,
                "Duplicate: {0} | FIFO: {1}",
                duplicateCount,
                fifoViolationCount);
        }

        private static int CountDuplicateVisibleRows(IEnumerable<AuditDisplayRow> rows)
        {
            HashSet<string> signatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int duplicateCount = 0;
            foreach (AuditDisplayRow row in rows)
            {
                string signature = string.Join("|", row.Time, row.IOA, row.Event, row.Value, row.Cot);
                if (!signatures.Add(signature))
                {
                    duplicateCount++;
                }
            }

            return duplicateCount;
        }

        private static int CountVisibleFifoViolations(IEnumerable<AuditDisplayRow> rows)
        {
            DateTime? previous = null;
            int violations = 0;
            foreach (AuditDisplayRow row in rows.Reverse())
            {
                DateTime current;
                if (!TryParseRowTime(row.SourceTime, out current))
                {
                    continue;
                }

                if (previous.HasValue && current < previous.Value)
                {
                    violations++;
                }

                previous = current;
            }

            return violations;
        }

        private static string BuildTopDistribution(IEnumerable<string> values)
        {
            List<string> top = values
                .Where(v => !string.IsNullOrWhiteSpace(v) && v != "-")
                .GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .Select(g => g.Key + "=" + g.Count().ToString(CultureInfo.InvariantCulture))
                .ToList();

            return top.Count == 0 ? "-" : string.Join(", ", top);
        }

        private IList<AuditDisplayRow> GetVisibleRows()
        {
            if (_auditView == null)
            {
                return new List<AuditDisplayRow>();
            }

            return _auditView.Cast<AuditDisplayRow>().ToList();
        }

        private BufferReplaySession GetLatestReplaySession()
        {
            return _currentViewModel?.BufferReplaySessions.FirstOrDefault();
        }

        private static string NormalizeChannel(string source)
        {
            string text = (source ?? string.Empty).Trim();
            return text.Length == 0 ? "Main" : text;
        }

        private static string FormatUtc(DateTime timestampUtc)
        {
            return timestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        }

        private static EventLogRow MapExportRow(AuditDisplayRow row)
        {
            SoeForensicRow source = row?.SourceRow;
            return new EventLogRow
            {
                Time = row?.RecvTime ?? "-",
                RecvTime = row?.RecvTime ?? "-",
                SourceTime = row?.SourceTime ?? "-",
                DeltaMs = row?.DeltaMs ?? "-",
                Source = row?.Source ?? "-",
                Name = row?.Name ?? "-",
                IOA = row?.IOA ?? "-",
                Type = row?.TypeId ?? "-",
                TypeId = row?.TypeId ?? "-",
                Casdu = row?.Casdu ?? "-",
                Event = source?.Origin ?? row?.Event ?? "-",
                Value = row?.Value ?? "-",
                Quality = row?.Quality ?? "-",
                Cot = row?.Cot ?? "-"
            };
        }

        private static string ClassifySourceKind(EventLogRow row)
        {
            string eventText = (row.Event ?? string.Empty).Trim();
            string cotText = (row.Cot ?? string.Empty).Trim();
            string nameText = (row.Name ?? string.Empty).Trim();
            string typeText = (row.Type ?? string.Empty).Trim();

            if (ContainsAny(eventText, "select", "execute", "command", "reject", "confirm"))
            {
                return "Command";
            }

            if (ContainsAny(nameText, "L1FT", "L2FT", "IEDF"))
            {
                return "LinkFault";
            }

            if (ContainsAny(cotText, "INTERROGATED", "GI", "BACKGROUND"))
            {
                return "GI";
            }

            if (ContainsAny(cotText, "SPONTANEOUS", "SPONT"))
            {
                return "Spont";
            }

            if (ContainsAny(typeText, "Measured", "Step Position") || IsMeasurementIoa(row.IOA))
            {
                return "Measurement";
            }

            if (ContainsAny(eventText, "replay", "buffer"))
            {
                return "Replay";
            }

            return "Unknown";
        }

        private static string ClassifyClassKind(EventLogRow row)
        {
            string dataClass = (row.DataClass ?? string.Empty).Trim();
            string eventText = (row.Event ?? string.Empty).Trim();
            if (ContainsAny(eventText, "select", "execute", "command", "reject", "confirm"))
            {
                return "Command";
            }

            if (ContainsAny(dataClass, "Class1", "Class 1"))
            {
                return "Class1";
            }

            if (ContainsAny(dataClass, "Class2", "Class 2"))
            {
                return "Class2";
            }

            return "Unknown";
        }

        private static string ClassifyReplayFlag(EventLogRow row)
        {
            string eventText = (row.Event ?? string.Empty).Trim();
            string cotText = (row.Cot ?? string.Empty).Trim();
            if (ContainsAny(eventText, "replay", "buffer"))
            {
                return "Yes";
            }

            if (ContainsAny(eventText, "command", "reject", "confirm"))
            {
                return "No";
            }

            return ContainsAny(cotText, "SPONTANEOUS") ? "Yes" : "No";
        }

        private static string ClassifySwitchoverContext(SoeForensicRow row, DateTime? switchoverUtc)
        {
            if (!switchoverUtc.HasValue)
            {
                return "-";
            }

            if (row == null)
            {
                return "-";
            }

            DateTime rowUtc = row.RecvTimeUtc;
            double deltaMs = (rowUtc - switchoverUtc.Value).TotalMilliseconds;
            if (Math.Abs(deltaMs) <= 1500d)
            {
                return "During";
            }

            return deltaMs < 0 ? "Before" : "After";
        }

        private static bool TryParseRowTime(string text, out DateTime timestampUtc)
        {
            string value = (text ?? string.Empty).Trim();
            return DateTime.TryParseExact(
                value,
                new[] { "yyyy-MM-dd HH:mm:ss.fff", "yyyy-MM-dd HH:mm:ss", "HH:mm:ss.fff", "HH:mm:ss" },
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out timestampUtc);
        }

        private static DateTime? ParsePreciseTimestamp(string text)
        {
            DateTime parsed;
            return TryParseRowTime(text, out parsed) ? (DateTime?)parsed : null;
        }

        private static string ExtractTimestampTail(string text)
        {
            string source = text ?? string.Empty;
            int atIndex = source.LastIndexOf('@');
            return atIndex >= 0 ? source.Substring(atIndex + 1).Trim() : string.Empty;
        }

        private static bool ContainsAny(string source, params string[] needles)
        {
            string haystack = source ?? string.Empty;
            return needles.Any(needle => haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool IsMeasurementIoa(string ioa)
        {
            int parsed;
            if (!int.TryParse(ioa, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                return false;
            }

            return parsed == 790446
                || parsed == 790447
                || parsed == 790438
                || parsed == 790439
                || parsed == 790442
                || parsed == 790443
                || parsed == 790449
                || parsed == 790448;
        }

        private static List<AuditPreset> BuildPresets()
        {
            return new List<AuditPreset>
            {
                new AuditPreset { Name = "None" },
                new AuditPreset { Name = "SOE Replay", SearchFilter = "replay" },
                new AuditPreset { Name = "Monitoring Link", IoaFilter = "8388725,8388714,8388715" },
                new AuditPreset { Name = "Telesignal Single", IoaFilter = "8388754,8388716,8388717,8388725" },
                new AuditPreset { Name = "Telesignal Double", IoaFilter = "16712689,16712686,16712704,16712694,16712701,16712708,16712709,16712710" },
                new AuditPreset { Name = "Tap Changer", IoaFilter = "790448,74537,16712709,16712710" },
                new AuditPreset { Name = "Remote Control Digital", IoaFilter = "68542,68539,68550,16712689,16712686,16712704" },
                new AuditPreset { Name = "Telemetering", IoaFilter = "790446,790447,790438,790439,790442,790443,790449" }
            };
        }

        protected override void OnClosed(EventArgs e)
        {
            DetachCurrentViewModel();
            base.OnClosed(e);
        }

        private sealed class AuditPreset
        {
            public string Name { get; set; }
            public string IoaFilter { get; set; }
            public string SignalFilter { get; set; }
            public string ChannelFilter { get; set; }
            public string EventFilter { get; set; }
            public string ValueFilter { get; set; }
            public string SearchFilter { get; set; }
            public DateTime? FromDate { get; set; }
            public DateTime? ToDate { get; set; }
        }

        private sealed class AuditDisplayRow : INotifyPropertyChanged
        {
            private bool _isSample;

            public int No { get; set; }
            public SoeForensicRow SourceRow { get; set; }
            public string Time { get; set; }
            public string RecvTime { get; set; }
            public string SourceTime { get; set; }
            public string DeltaMs { get; set; }
            public string Source { get; set; }
            public string Name { get; set; }
            public string IOA { get; set; }
            public string Type { get; set; }
            public string TypeId { get; set; }
            public string Casdu { get; set; }
            public string Event { get; set; }
            public string Value { get; set; }
            public string Cot { get; set; }
            public string Quality { get; set; }
            public string SourceKind { get; set; }
            public string ClassKind { get; set; }
            public string ReplayFlagText { get; set; }
            public string SwitchoverContext { get; set; }

            public bool IsSample
            {
                get { return _isSample; }
                set
                {
                    if (_isSample == value)
                    {
                        return;
                    }

                    _isSample = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSample)));
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        private sealed class AppliedFilterState
        {
            public string PresetName { get; set; }
            public string IoaFilter { get; set; }
            public string SignalFilter { get; set; }
            public string ChannelFilter { get; set; }
            public string EventFilter { get; set; }
            public string ValueFilter { get; set; }
            public string TypeIdFilter { get; set; }
            public string DataTypeFilter { get; set; }
            public string ClassFilter { get; set; }
            public string SearchFilter { get; set; }
            public DateTime? FromDate { get; set; }
            public DateTime? ToDate { get; set; }
        }
    }
}
