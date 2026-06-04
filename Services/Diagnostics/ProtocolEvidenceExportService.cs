using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using IEC101MasterTester.Models;

namespace IEC101MasterTester.Services.Diagnostics
{
    public sealed class ProtocolEvidenceExportService
    {
        public string ExportSharedSnapshotToCsv(string filePath)
        {
            return ExportToCsv(filePath, ProtocolEvidenceRecorder.Shared.Snapshot());
        }

        public string ExportToCsv(string filePath, IReadOnlyList<ProtocolEvidence> rows)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("Export path is required.", nameof(filePath));
            }

            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                writer.WriteLine("Sequence,CapturedAtUtc,Engine,Direction,FrameType,Control,ACD,DFC,TypeId,COT,CASDU,IOA,LinkAddressLength,LinkAddress,CasduLength,CasduAddress,IoaLength,DecodeStatus,DecodeDetail,RawHex");

                if (rows != null)
                {
                    for (int index = 0; index < rows.Count; index++)
                    {
                        WriteRow(writer, rows[index]);
                    }
                }
            }

            return filePath;
        }

        private static void WriteRow(StreamWriter writer, ProtocolEvidence row)
        {
            if (row == null)
            {
                return;
            }

            string[] values =
            {
                row.Sequence.ToString(CultureInfo.InvariantCulture),
                row.CapturedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                row.Engine,
                row.Direction,
                row.FrameType,
                row.Control,
                row.ACD,
                row.DFC,
                row.TypeId,
                row.COT,
                row.CASDU,
                row.IOA,
                row.LinkAddressLength.ToString(CultureInfo.InvariantCulture),
                row.LinkAddress.ToString(CultureInfo.InvariantCulture),
                row.CasduLength.ToString(CultureInfo.InvariantCulture),
                row.CasduAddress.ToString(CultureInfo.InvariantCulture),
                row.IoaLength.ToString(CultureInfo.InvariantCulture),
                row.DecodeStatus,
                row.DecodeDetail,
                ToHex(row.RawFrame)
            };

            writer.WriteLine(string.Join(",", Escape(values)));
        }

        private static IEnumerable<string> Escape(IEnumerable<string> values)
        {
            foreach (string value in values)
            {
                string text = value ?? string.Empty;
                if (text.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
                {
                    yield return "\"" + text.Replace("\"", "\"\"") + "\"";
                }
                else
                {
                    yield return text;
                }
            }
        }

        private static string ToHex(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(bytes.Length * 3);
            for (int index = 0; index < bytes.Length; index++)
            {
                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(bytes[index].ToString("X2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }
}
