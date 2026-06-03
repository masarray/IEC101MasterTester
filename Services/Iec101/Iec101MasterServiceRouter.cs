using System;
using System.Threading.Tasks;
using IEC101MasterTester.Models;
using IEC101MasterTester.Services.Iec101.Native.Master;

namespace IEC101MasterTester.Services.Iec101
{
    public sealed class Iec101MasterServiceRouter : IIec101MasterService
    {
        private readonly Iec101MasterService _lib60870Service;
        private readonly NativeIec101MasterService _nativeExperimentalService;
        private IIec101MasterService _activeService;

        public Iec101MasterServiceRouter()
        {
            _lib60870Service = new Iec101MasterService();
            _nativeExperimentalService = new NativeIec101MasterService();
            _activeService = _lib60870Service;

            Subscribe(_lib60870Service);
            Subscribe(_nativeExperimentalService);
        }

        public event EventHandler<ConnectionStatusInfo> ConnectionStateChanged;
        public event EventHandler<LineMonitorRow> LineMonitorRecordReceived;
        public event EventHandler<ValueViewerRow> ValueReceived;

        public bool IsConnected
        {
            get { return _activeService.IsConnected; }
        }

        public void ApplySettings(ConnectionSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            _activeService = settings.MasterEngine == Iec101MasterEngine.NativeExperimental
                ? (IIec101MasterService)_nativeExperimentalService
                : _lib60870Service;

            _activeService.ApplySettings(settings);
        }

        public Task ConnectAsync()
        {
            return _activeService.ConnectAsync();
        }

        public Task DisconnectAsync()
        {
            return _activeService.DisconnectAsync();
        }

        public Task SendGeneralInterrogationAsync()
        {
            return _activeService.SendGeneralInterrogationAsync();
        }

        public Task<bool> SendLinkLayerTestFunctionAsync()
        {
            return _activeService.SendLinkLayerTestFunctionAsync();
        }

        public void NotifyActiveLinkSwitchover()
        {
            _activeService.NotifyActiveLinkSwitchover();
        }

        public Task SendClockSyncAsync()
        {
            return _activeService.SendClockSyncAsync();
        }

        public Task SendSingleCommandAsync(int ioa, bool state, bool select = false, int quality = 0)
        {
            return _activeService.SendSingleCommandAsync(ioa, state, select, quality);
        }

        public Task SendDoubleCommandAsync(int ioa, bool on, bool select = false, int quality = 0)
        {
            return _activeService.SendDoubleCommandAsync(ioa, on, select, quality);
        }

        public Task SendStepCommandAsync(int ioa, bool raise, bool select = false, int quality = 0)
        {
            return _activeService.SendStepCommandAsync(ioa, raise, select, quality);
        }

        public Task SendSetpointNormalizedCommandAsync(int ioa, float normalizedValue, bool select = false, int quality = 0)
        {
            return _activeService.SendSetpointNormalizedCommandAsync(ioa, normalizedValue, select, quality);
        }

        private void Subscribe(IIec101MasterService service)
        {
            service.ConnectionStateChanged += (sender, args) => ConnectionStateChanged?.Invoke(sender, args);
            service.LineMonitorRecordReceived += (sender, args) => LineMonitorRecordReceived?.Invoke(sender, args);
            service.ValueReceived += (sender, args) => ValueReceived?.Invoke(sender, args);
        }
    }
}
