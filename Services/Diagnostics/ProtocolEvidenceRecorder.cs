using System;
using System.Collections.Generic;
using IEC101MasterTester.Models;
using IEC101MasterTester.Services.Iec101.Native;
using IEC101MasterTester.Services.Iec101.Native.Asdu;
using IEC101MasterTester.Services.Iec101.Native.Frames;

namespace IEC101MasterTester.Services.Diagnostics
{
    public sealed class ProtocolEvidenceRecorder
    {
        private const int DefaultCapacity = 4096;

        private static readonly ProtocolEvidenceRecorder SharedRecorder = new ProtocolEvidenceRecorder(DefaultCapacity);

        private readonly object _syncRoot = new object();
        private readonly Queue<ProtocolEvidence> _rows;
        private readonly int _capacity;
        private long _sequence;

        public ProtocolEvidenceRecorder(int capacity)
        {
            _capacity = Math.Max(128, capacity);
            _rows = new Queue<ProtocolEvidence>(_capacity);
        }

        public static ProtocolEvidenceRecorder Shared
        {
            get { return SharedRecorder; }
        }

        public int Count
        {
            get
            {
                lock (_syncRoot)
                {
                    return _rows.Count;
                }
            }
        }

        public void Clear()
        {
            lock (_syncRoot)
            {
                _rows.Clear();
            }
        }

        public void RecordRaw(string engine, string direction, byte[] frameBytes, int frameSize, ConnectionSettings settings)
        {
            if (frameBytes == null || frameSize <= 0)
            {
                return;
            }

            int length = Math.Min(frameSize, frameBytes.Length);
            byte[] rawCopy = new byte[length];
            Buffer.BlockCopy(frameBytes, 0, rawCopy, 0, length);

            ConnectionSettings effectiveSettings = settings == null ? ConnectionSettings.CreateDefault() : settings.Clone();
            ProtocolEvidence row = CreateBaseRow(engine, direction, rawCopy, effectiveSettings);
            PopulateNativeDecode(row, rawCopy, effectiveSettings);

            lock (_syncRoot)
            {
                row.Sequence = ++_sequence;
                _rows.Enqueue(row);

                while (_rows.Count > _capacity)
                {
                    _rows.Dequeue();
                }
            }
        }

        public IReadOnlyList<ProtocolEvidence> Snapshot()
        {
            lock (_syncRoot)
            {
                return new List<ProtocolEvidence>(_rows).AsReadOnly();
            }
        }

        private static ProtocolEvidence CreateBaseRow(string engine, string direction, byte[] rawFrame, ConnectionSettings settings)
        {
            return new ProtocolEvidence
            {
                CapturedAtUtc = DateTime.UtcNow,
                Engine = string.IsNullOrWhiteSpace(engine) ? "Unknown" : engine,
                Direction = string.IsNullOrWhiteSpace(direction) ? "-" : direction,
                FrameType = "-",
                Control = "-",
                ACD = "-",
                DFC = "-",
                TypeId = "-",
                COT = "-",
                CASDU = "-",
                IOA = "-",
                LinkAddressLength = settings.LinkAddressLength,
                LinkAddress = settings.LinkAddress,
                CasduLength = settings.CasduLength,
                CasduAddress = settings.CasduAddress,
                IoaLength = settings.IoaLength,
                RawFrame = rawFrame,
                DecodeStatus = "Pending",
                DecodeDetail = string.Empty
            };
        }

        private static void PopulateNativeDecode(ProtocolEvidence row, byte[] rawFrame, ConnectionSettings settings)
        {
            Iec101ApplicationProfile profile = Iec101ApplicationProfile.FromValues(settings.LinkAddressLength, settings.CasduLength, settings.IoaLength, settings.OriginatorAddress);
            Iec101Frame frame;
            string frameError;
            if (!Iec101FrameCodec.TryParse(rawFrame, rawFrame.Length, profile, out frame, out frameError))
            {
                row.FrameType = DetectFrameType(rawFrame);
                row.DecodeStatus = "FrameError";
                row.DecodeDetail = frameError ?? string.Empty;
                return;
            }

            row.FrameType = frame.FrameType.ToString();
            row.Control = frame.Control == null ? "-" : frame.Control.Describe();
            row.ACD = frame.Control != null && !frame.Control.IsPrimary ? (frame.Control.Acd ? "1" : "0") : "-";
            row.DFC = frame.Control != null && !frame.Control.IsPrimary ? (frame.Control.Dfc ? "1" : "0") : "-";
            row.LinkAddress = frame.LinkAddress.HasValue ? frame.LinkAddress.Value : settings.LinkAddress;
            row.DecodeStatus = "FrameOk";

            byte[] asduBytes = frame.GetAsduBytesOrEmpty();
            if (asduBytes.Length == 0)
            {
                return;
            }

            Iec101Asdu asdu;
            string asduError;
            if (!Iec101AsduCodec.TryParse(asduBytes, profile, out asdu, out asduError))
            {
                row.DecodeStatus = "AsduError";
                row.DecodeDetail = asduError ?? string.Empty;
                return;
            }

            row.TypeId = asdu.TypeId.ToString();
            row.COT = asdu.Cause == Iec101CauseOfTransmission.Unknown ? "COT" + asdu.CauseRaw : asdu.Cause.ToString();
            row.CASDU = asdu.CommonAddress.ToString();
            row.IOA = asdu.Objects.Count > 0 ? asdu.Objects[0].ObjectAddress.ToString() : "-";
            row.DecodeStatus = "AsduOk";
        }

        private static string DetectFrameType(byte[] rawFrame)
        {
            if (rawFrame == null || rawFrame.Length == 0)
            {
                return "Empty";
            }

            switch (rawFrame[0])
            {
                case Iec101FrameCodec.SingleCharacterAck:
                    return "SingleCharacterAck";
                case Iec101FrameCodec.FixedStart:
                    return "Fixed";
                case Iec101FrameCodec.VariableStart:
                    return "Variable";
                default:
                    return "Unknown";
            }
        }
    }
}
