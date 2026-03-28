using System.Threading.Tasks;
using IEC101MasterTester.Models;

namespace IEC101MasterTester.Services.Settings
{
    public interface ISettingsStore
    {
        Task<ConnectionSettings> LoadAsync();
        Task SaveAsync(ConnectionSettings settings);
    }
}
