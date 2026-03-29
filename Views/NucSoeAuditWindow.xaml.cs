using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using IEC101MasterTester.Models;
using IEC101MasterTester.Models.Export;
using IEC101MasterTester.Services.Export;
using IEC101MasterTester.ViewModels;
using Microsoft.Win32;

namespace IEC101MasterTester.Views
{
    public partial class NucSoeAuditWindow : Window
    {
        private readonly EventLogExportService _eventLogExportService = new EventLogExportService();
        private ICollectionView _auditView;

        public NucSoeAuditWindow()
        {
            InitializeComponent();
            Loaded += NucSoeAuditWindow_Loaded;
            DataContextChanged += NucSoeAuditWindow_DataContextChanged;
        }

        private void NucSoeAuditWindow_Loaded(object sender, RoutedEventArgs e)
        {
            AttachAuditView();
        }

        private void NucSoeAuditWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            AttachAuditView();
        }

        private void AttachAuditView()
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel == null)
            {
                _auditView = null;
                AuditGrid.ItemsSource = null;
                FilterSummaryTextBlock.Text = "No SOE audit source is attached.";
                return;
            }

            _auditView = CollectionViewSource.GetDefaultView(viewModel.NucSoeAuditLog);
            if (_auditView == null)
            {
                AuditGrid.ItemsSource = null;
                FilterSummaryTextBlock.Text = "SOE audit view is unavailable.";
                return;
            }

            _auditView.Filter = FilterAuditRow;
            AuditGrid.ItemsSource = _auditView;
            _auditView.Refresh();
            RefreshFilterSummary();
        }

        private bool FilterAuditRow(object item)
        {
            EventLogRow row = item as EventLogRow;
            if (row == null)
            {
                return false;
            }

            if (!MatchesContains(row.IOA, IoaFilterTextBox.Text))
            {
                return false;
            }

            if (!MatchesContains(row.Name, SignalFilterTextBox.Text))
            {
                return false;
            }

            if (!MatchesContains(row.Source, ChannelFilterTextBox.Text))
            {
                return false;
            }

            if (!MatchesContains(row.Event, EventFilterTextBox.Text))
            {
                return false;
            }

            if (!MatchesContains(row.Value, ValueFilterTextBox.Text))
            {
                return false;
            }

            DateTime rowTimeUtc;
            bool hasRowTime = TryParseRowTime(row.Time, out rowTimeUtc);
            DateTime fromUtc;
            if (TryParseFilterDate(FromFilterTextBox.Text, out fromUtc))
            {
                if (!hasRowTime || rowTimeUtc < fromUtc)
                {
                    return false;
                }
            }

            DateTime toUtc;
            if (TryParseFilterDate(ToFilterTextBox.Text, out toUtc))
            {
                if (!hasRowTime || rowTimeUtc > toUtc)
                {
                    return false;
                }
            }

            string searchText = (SearchFilterTextBox.Text ?? string.Empty).Trim();
            if (searchText.Length == 0)
            {
                return true;
            }

            string haystack = string.Join(" | ",
                row.Time ?? string.Empty,
                row.Source ?? string.Empty,
                row.Name ?? string.Empty,
                row.IOA ?? string.Empty,
                row.Type ?? string.Empty,
                row.Event ?? string.Empty,
                row.Value ?? string.Empty,
                row.Cot ?? string.Empty,
                row.Quality ?? string.Empty);

            return haystack.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void FilterControl_Changed(object sender, RoutedEventArgs e)
        {
            if (_auditView == null)
            {
                return;
            }

            _auditView.Refresh();
            RefreshFilterSummary();
        }

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            IoaFilterTextBox.Text = string.Empty;
            SignalFilterTextBox.Text = string.Empty;
            ChannelFilterTextBox.Text = string.Empty;
            EventFilterTextBox.Text = string.Empty;
            ValueFilterTextBox.Text = string.Empty;
            FromFilterTextBox.Text = string.Empty;
            ToFilterTextBox.Text = string.Empty;
            SearchFilterTextBox.Text = string.Empty;

            if (_auditView != null)
            {
                _auditView.Refresh();
            }

            RefreshFilterSummary();
        }

        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel == null || _auditView == null)
            {
                MessageBox.Show(this, "SOE audit source is not available yet.", "IEC-101 NUC SOE Audit", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            List<EventLogRow> visibleRows = _auditView.Cast<object>().OfType<EventLogRow>().ToList();
            if (visibleRows.Count == 0)
            {
                MessageBox.Show(this, "Tidak ada row yang cocok dengan filter aktif. Export dibatalkan.", "IEC-101 NUC SOE Audit", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "Export IEC60870 Event Log Data",
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                DefaultExt = ".xlsx",
                AddExtension = true,
                FileName = "IEC60870-EventLog-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".xlsx"
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                EventLogExportRequest request = new EventLogExportRequest
                {
                    OutputPath = dialog.FileName,
                    Rows = visibleRows,
                    Metadata = new EventLogExportMetadata
                    {
                        Title = "IEC60870 Event Log Data",
                        ModuleName = "IEC101MasterTester",
                        SourceWindow = "NUC SOE Audit",
                        SessionStartedText = viewModel.AvailabilitySessionStartedText,
                        ContextSummary = BuildContextSummary(viewModel),
                        FilterSummary = BuildFilterSummary(),
                        ExportedAtText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)
                    }
                };

                string outputPath = _eventLogExportService.ExportToExcel(request);
                MessageBox.Show(this, "Export Excel berhasil dibuat:\n" + outputPath, "IEC-101 NUC SOE Audit", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Export Excel gagal:\n" + ex.Message, "IEC-101 NUC SOE Audit", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshFilterSummary()
        {
            int visibleCount = _auditView == null ? 0 : _auditView.Cast<object>().Count();
            MainViewModel viewModel = DataContext as MainViewModel;
            int totalCount = viewModel?.NucSoeAuditLog.Count ?? 0;
            string filterSummary = BuildFilterSummary();

            if (string.Equals(filterSummary, "No filter applied", StringComparison.Ordinal))
            {
                FilterSummaryTextBlock.Text = string.Format(
                    CultureInfo.InvariantCulture,
                    "Showing all rows. Visible: {0} / Total: {1}",
                    visibleCount,
                    totalCount);
                return;
            }

            FilterSummaryTextBlock.Text = string.Format(
                CultureInfo.InvariantCulture,
                "Filtered rows: {0} / {1} | {2}",
                visibleCount,
                totalCount,
                filterSummary);
        }

        private string BuildFilterSummary()
        {
            List<string> parts = new List<string>();
            AppendFilterPart(parts, "IOA", IoaFilterTextBox.Text);
            AppendFilterPart(parts, "Signal", SignalFilterTextBox.Text);
            AppendFilterPart(parts, "Channel", ChannelFilterTextBox.Text);
            AppendFilterPart(parts, "Event", EventFilterTextBox.Text);
            AppendFilterPart(parts, "Value", ValueFilterTextBox.Text);
            AppendFilterPart(parts, "From", FromFilterTextBox.Text);
            AppendFilterPart(parts, "To", ToFilterTextBox.Text);
            AppendFilterPart(parts, "Search", SearchFilterTextBox.Text);
            return parts.Count == 0 ? "No filter applied" : string.Join(" | ", parts);
        }

        private static string BuildContextSummary(MainViewModel viewModel)
        {
            return string.Join(" | ", new[]
            {
                viewModel.RedundancyConfigSummaryText ?? "-",
                viewModel.RedundancyActiveLinkText ?? "-",
                viewModel.RedundancyGiObservationText ?? "-"
            }.Where(part => !string.IsNullOrWhiteSpace(part)));
        }

        private static void AppendFilterPart(ICollection<string> parts, string label, string value)
        {
            string trimmed = (value ?? string.Empty).Trim();
            if (trimmed.Length > 0)
            {
                parts.Add(label + "=" + trimmed);
            }
        }

        private static bool MatchesContains(string source, string filter)
        {
            string trimmed = (filter ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                return true;
            }

            return (source ?? string.Empty).IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool TryParseRowTime(string text, out DateTime value)
        {
            string[] formats =
            {
                "yyyy-MM-dd HH:mm:ss.fff",
                "yyyy-MM-dd HH:mm:ss",
                "HH:mm:ss.fff",
                "HH:mm:ss"
            };

            foreach (string format in formats)
            {
                if (DateTime.TryParseExact(text, format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out value))
                {
                    return true;
                }
            }

            return DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out value)
                || DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out value);
        }

        private static bool TryParseFilterDate(string text, out DateTime value)
        {
            string trimmed = (text ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                value = default(DateTime);
                return false;
            }

            string[] formats =
            {
                "yyyy-MM-dd HH:mm:ss.fff",
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-dd HH:mm",
                "yyyy-MM-dd"
            };

            foreach (string format in formats)
            {
                if (DateTime.TryParseExact(trimmed, format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out value))
                {
                    return true;
                }
            }

            return DateTime.TryParse(trimmed, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out value)
                || DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out value);
        }
    }
}
