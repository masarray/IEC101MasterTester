using System;
using System.Threading.Tasks;
using IEC101MasterTester.Models;

namespace IEC101MasterTester.Services.Iec101
{
    public interface IIec101MasterService
    {
        event EventHandler<ConnectionStatusInfo> ConnectionStateChanged;
        event EventHandler<LineMonitorRow> LineMonitorRecordReceived;
        event EventHandler<ValueViewerRow> ValueReceived;

        bool IsConnected { get; }

        void ApplySettings(ConnectionSettings settings);
        Task ConnectAsync();
        Task DisconnectAsync();
        Task SendGeneralInterrogationAsync();
        Task<bool> SendLinkLayerTestFunctionAsync();
        void NotifyActiveLinkSwitchover();
        Task SendClockSyncAsync();
        Task SendSingleCommandAsync(int ioa, bool state, bool select = false, int quality = 0);
        Task SendDoubleCommandAsync(int ioa, bool on, bool select = false, int quality = 0);
        Task SendStepCommandAsync(int ioa, bool raise, bool select = false, int quality = 0);
        Task SendSetpointNormalizedCommandAsync(int ioa, float normalizedValue, bool select = false, int quality = 0);
    }
}
