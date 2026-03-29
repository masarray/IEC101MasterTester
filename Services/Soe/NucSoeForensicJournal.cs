using System;
using System.Collections.Generic;
using IEC101MasterTester.Models.Soe;

namespace IEC101MasterTester.Services.Soe
{
    public sealed class NucSoeForensicJournal
    {
        private readonly object _syncRoot = new object();
        private readonly LinkedList<SoeForensicRow> _rows = new LinkedList<SoeForensicRow>();

        public NucSoeForensicJournal(int capacity)
        {
            Capacity = capacity < 100 ? 100 : capacity;
        }

        public int Capacity { get; }

        public event EventHandler Changed;

        public void Append(SoeForensicRow row)
        {
            if (row == null)
            {
                return;
            }

            lock (_syncRoot)
            {
                _rows.AddFirst(row);
                while (_rows.Count > Capacity)
                {
                    _rows.RemoveLast();
                }
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }

        public IReadOnlyList<SoeForensicRow> Snapshot()
        {
            lock (_syncRoot)
            {
                return new List<SoeForensicRow>(_rows);
            }
        }

        public void Clear()
        {
            lock (_syncRoot)
            {
                _rows.Clear();
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
