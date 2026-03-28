using System.ComponentModel;
using System;
using System.Runtime.CompilerServices;

namespace IEC101MasterTester.Models
{
    public sealed class ValueViewerRow : INotifyPropertyChanged
    {
        private int _no;
        private int _ioa;
        private string _name;
        private string _type;
        private string _value;
        private string _quality;
        private string _timestamp;
        private DateTime? _eventTimestampUtc;
        private DateTime? _snapshotTimestampUtc;
        private bool _hasProtocolTimestamp;
        private string _sourceType;
        private string _acd;
        private string _cot;
        private string _trafficClass;
        private string _updateSource;
        private string _pointKey;

        public event PropertyChangedEventHandler PropertyChanged;

        public int No { get => _no; set => SetField(ref _no, value); }
        public int IOA { get => _ioa; set => SetField(ref _ioa, value); }
        public string Name { get => _name; set => SetField(ref _name, value); }
        public string Type { get => _type; set => SetField(ref _type, value); }
        public string Value { get => _value; set => SetField(ref _value, value); }
        public string Quality { get => _quality; set => SetField(ref _quality, value); }
        public string Timestamp { get => _timestamp; set => SetField(ref _timestamp, value); }
        public DateTime? EventTimestampUtc { get => _eventTimestampUtc; set => SetField(ref _eventTimestampUtc, value); }
        public DateTime? SnapshotTimestampUtc { get => _snapshotTimestampUtc; set => SetField(ref _snapshotTimestampUtc, value); }
        public bool HasProtocolTimestamp { get => _hasProtocolTimestamp; set => SetField(ref _hasProtocolTimestamp, value); }
        public string SourceType { get => _sourceType; set => SetField(ref _sourceType, value); }
        public string Acd { get => _acd; set => SetField(ref _acd, value); }
        public string Cot { get => _cot; set => SetField(ref _cot, value); }
        public string TrafficClass { get => _trafficClass; set => SetField(ref _trafficClass, value); }
        public string UpdateSource { get => _updateSource; set => SetField(ref _updateSource, value); }
        public string PointKey { get => _pointKey; set => SetField(ref _pointKey, value); }

        private void SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

