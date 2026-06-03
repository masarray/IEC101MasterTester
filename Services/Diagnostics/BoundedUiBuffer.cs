using System.Collections.ObjectModel;
using IEC101MasterTester.Models;

namespace IEC101MasterTester.Services.Diagnostics
{
    public static class BoundedUiBuffer
    {
        public static void InsertNewest<T>(ObservableCollection<T> rows, T row, int maxRows)
        {
            if (rows == null || row == null || maxRows <= 0)
            {
                return;
            }

            rows.Insert(0, row);
            Trim(rows, maxRows);
        }

        public static void Trim<T>(ObservableCollection<T> rows, int maxRows)
        {
            if (rows == null || maxRows <= 0)
            {
                return;
            }

            while (rows.Count > maxRows)
            {
                rows.RemoveAt(rows.Count - 1);
            }
        }

        public static LineMonitorRow CreateLineSnapshot(LineMonitorRow source, string channel, int rawHexLimit, int detailLimit)
        {
            if (source == null)
            {
                return null;
            }

            return new LineMonitorRow
            {
                Time = source.Time,
                Channel = channel ?? source.Channel,
                Direction = source.Direction,
                FrameType = source.FrameType,
                Summary = source.Summary,
                ControlFc = source.ControlFc,
                ACD = source.ACD,
                DFC = source.DFC,
                AsduType = source.AsduType,
                COT = source.COT,
                CASDU = source.CASDU,
                IOA = source.IOA,
                RawHex = TrimText(source.RawHex, rawHexLimit),
                Detail = TrimText(source.Detail, detailLimit),
                DataClass = source.DataClass
            };
        }

        public static string TrimText(string text, int limit)
        {
            if (string.IsNullOrEmpty(text) || limit <= 0 || text.Length <= limit)
            {
                return text ?? string.Empty;
            }

            return text.Substring(0, limit) + "...";
        }
    }
}
