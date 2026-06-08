using System;
using System.Threading.Tasks;
using IEC101MasterTester.Models;
using IEC101MasterTester.Services.Iec101.Native.Master;

namespace IEC101MasterTester.Services.Iec101
{
    public sealed class Iec101MasterServiceRouter : IIec101MasterService
    {
        private readonly NativeIec101MasterService _nativeService;

        public Iec101MasterServiceRouter()
        {
            _nativeService = new NativeIec101MasterService();
            Subscribe(_nativeService);
        }

        public event EventHandler<ConnectionStatusInfo> ConnectionStateChanged;
        public event EventHandler<LineMonitorRow> LineMonitorRecordReceived;
        public event EventHandler<ValueViewerRow> ValueReceived;

        public bool IsConnected
        {
            get { return _nativeService.IsConnected; }
        }

        public void ApplySettings(ConnectionSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            _nativeService.ApplySettings(settings);
        }

        public Task ConnectAsync()
        {
            return _nativeService.ConnectAsync();
        }

        public Task DisconnectAsync()
        {
            return _nativeService.DisconnectAsync();
        }

        public Task SendGeneralInterrogationAsync()
        {
            return _nativeService.SendGeneralInterrogationAsync();
        }

        public Task<bool> SendLinkLayerTestFunctionAsync()
        {
            return _nativeService.SendLinkLayerTestFunctionAsync();
        }

        public void NotifyActiveLinkSwitchover()
        {
            _nativeService.NotifyActiveLinkSwitchover();
        }

        public Task SendClockSyncAsync()
        {
            return _nativeService.SendClockSyncAsync();
        }

        public Task SendSingleCommandAsync(int ioa, bool state, bool select = false, int quality = 0)
        {
            return _nativeService.SendSingleCommandAsync(ioa, state, select, quality);
        }

        public Task SendDoubleCommandAsync(int ioa, bool on, bool select = false, int quality = 0)
        {
            return _nativeService.SendDoubleCommandAsync(ioa, on, select, quality);
        }

        public Task SendStepCommandAsync(int ioa, bool raise, bool select = false, int quality = 0)
        {
            return _nativeService.SendStepCommandAsync(ioa, raise, select, quality);
        }

        public Task SendSetpointNormalizedCommandAsync(int ioa, float normalizedValue, bool select = false, int quality = 0)
        {
            return _nativeService.SendSetpointNormalizedCommandAsync(ioa, normalizedValue, select, quality);
        }

        private void Subscribe(IIec101MasterService service)
        {
            service.ConnectionStateChanged += (sender, args) => ConnectionStateChanged?.Invoke(sender, args);
            service.LineMonitorRecordReceived += (sender, args) => LineMonitorRecordReceived?.Invoke(sender, args);
            service.ValueReceived += (sender, args) => ValueReceived?.Invoke(sender, args);
        }
    }
}
