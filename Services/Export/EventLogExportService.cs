using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using IEC101MasterTester.Models;
using IEC101MasterTester.Models.Export;

namespace IEC101MasterTester.Services.Export
{
    public sealed class EventLogExportService
    {
        public string ExportToCsv(EventLogExportRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.OutputPath))
            {
                throw new ArgumentException("Output path is required.", nameof(request));
            }

            if (request.Rows == null || request.Rows.Count == 0)
            {
                throw new InvalidOperationException("No event-log rows available for export.");
            }

            string outputPath = request.OutputPath;
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            EventLogExportMetadata metadata = request.Metadata ?? new EventLogExportMetadata();
            IList<EventLogRow> rows = request.Rows;

            using (StreamWriter writer = new StreamWriter(outputPath, false, new UTF8Encoding(true)))
            {
                WriteMetadataLine(writer, "IEC60870 Event Log Data");
                WriteMetadataLine(writer, "Module", metadata.ModuleName ?? "IEC101MasterTester");
                WriteMetadataLine(writer, "Source Window", metadata.SourceWindow ?? "NUC SOE Audit");
                WriteMetadataLine(writer, "Exported At", metadata.ExportedAtText ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
                WriteMetadataLine(writer, "Session Started", metadata.SessionStartedText ?? "-");
                WriteMetadataLine(writer, "Context", metadata.ContextSummary ?? "-");
                WriteMetadataLine(writer, "Filters", metadata.FilterSummary ?? "No filter applied");
                if (metadata.SummaryRows != null)
                {
                    foreach (KeyValuePair<string, string> summaryRow in metadata.SummaryRows)
                    {
                        WriteMetadataLine(writer, summaryRow.Key ?? "-", summaryRow.Value ?? "-");
                    }
                }
                writer.WriteLine();

                writer.WriteLine(string.Join(",",
                    EscapeCsv("No"),
                    EscapeCsv("Time"),
                    EscapeCsv("Channel"),
                    EscapeCsv("Signal Name"),
                    EscapeCsv("IOA"),
                    EscapeCsv("Type"),
                    EscapeCsv("Event"),
                    EscapeCsv("Value"),
                    EscapeCsv("COT"),
                    EscapeCsv("Quality")));

                for (int index = 0; index < rows.Count; index++)
                {
                    EventLogRow row = rows[index] ?? new EventLogRow();
                    writer.WriteLine(string.Join(",",
                        EscapeCsv((index + 1).ToString(CultureInfo.InvariantCulture)),
                        EscapeCsv(row.Time ?? "-"),
                        EscapeCsv(row.Source ?? "-"),
                        EscapeCsv(row.Name ?? "-"),
                        EscapeCsv(row.IOA ?? "-"),
                        EscapeCsv(row.Type ?? "-"),
                        EscapeCsv(row.Event ?? "-"),
                        EscapeCsv(row.Value ?? "-"),
                        EscapeCsv(row.Cot ?? "-"),
                        EscapeCsv(row.Quality ?? "-")));
                }
            }

            return outputPath;
        }

        private static void WriteMetadataLine(TextWriter writer, string label, string value = null)
        {
            if (value == null)
            {
                writer.WriteLine(EscapeCsv(label));
                return;
            }

            writer.WriteLine(string.Join(",", EscapeCsv(label), EscapeCsv(value)));
        }

        private static string EscapeCsv(string value)
        {
            string text = value ?? string.Empty;
            bool mustQuote = text.Contains(",") || text.Contains("\"") || text.Contains("\r") || text.Contains("\n");
            if (text.Contains("\""))
            {
                text = text.Replace("\"", "\"\"");
            }

            return mustQuote ? "\"" + text + "\"" : text;
        }
    }
}
