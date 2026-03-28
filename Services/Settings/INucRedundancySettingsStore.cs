using System.Threading.Tasks;
using IEC101MasterTester.Models;

namespace IEC101MasterTester.Services.Settings
{
    public interface INucRedundancySettingsStore
    {
        Task<NucRedundancySettings> LoadAsync();
        Task SaveAsync(NucRedundancySettings settings);
    }
}
