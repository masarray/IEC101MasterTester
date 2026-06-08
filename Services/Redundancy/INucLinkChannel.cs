using System;
using System.Threading.Tasks;
using IEC101MasterTester.Models;

namespace IEC101MasterTester.Services.Redundancy
{
    public interface INucLinkChannel
    {
        event EventHandler<ConnectionStatusInfo> ConnectionStateChanged;
        event EventHandler<LineMonitorRow> LineMonitorRecordReceived;
        event EventHandler<ValueViewerRow> ValueReceived;
        event EventHandler<NucChannelSnapshot> SnapshotChanged;

        string Name { get; }

        NucChannelRole Role { get; }

        NucChannelSnapshot Snapshot { get; }

        void ApplySettings(ConnectionSettings baseSettings);

        Task StartAsActiveAsync();

        Task StartAsStandbyAsync();

        Task PromoteToActiveAsync();

        Task DemoteToStandbyAsync();

        Task StopAsync();

        Task RecoverAsync(string reason = null);

        Task SendGeneralInterrogationAsync();
        void NotifyActiveLinkSwitchover();

        Task SendSingleCommandAsync(int ioa, bool state, bool select = false, int quality = 0);

        Task SendDoubleCommandAsync(int ioa, bool on, bool select = false, int quality = 0);

        Task SendStepCommandAsync(int ioa, bool raise, bool select = false, int quality = 0);

        Task SendSetpointNormalizedCommandAsync(int ioa, float normalizedValue, bool select = false, int quality = 0);
    }
}
