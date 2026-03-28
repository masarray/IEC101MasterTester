using System;
using System.Threading.Tasks;
using IEC101MasterTester.Models;

namespace IEC101MasterTester.Services.Redundancy
{
    public interface INucRedundancyService
    {
        event EventHandler<NucRedundancySessionState> SessionStateChanged;
        event EventHandler<NucRedundancyConnectionEventArgs> ConnectionStateChanged;
        event EventHandler<NucRedundancyLineMonitorEventArgs> LineMonitorRecordReceived;
        event EventHandler<NucRedundancyValueEventArgs> ValueReceived;

        bool IsSessionActive { get; }

        void ApplySettings(NucRedundancySettings settings);

        void StartSession();

        void StopSession();

        Task StopSessionAsync();

        Task SendGeneralInterrogationAsync();

        Task SendSingleCommandAsync(int ioa, bool state, bool select = false, int quality = 0);

        Task SendDoubleCommandAsync(int ioa, bool on, bool select = false, int quality = 0);

        Task SendStepCommandAsync(int ioa, bool raise, bool select = false, int quality = 0);
    }
}
