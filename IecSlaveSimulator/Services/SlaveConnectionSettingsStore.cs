using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using IecSlaveSimulator.Models;

namespace IecSlaveSimulator.Services
{
    public sealed class SlaveConnectionSettingsStore
    {
        private readonly DataContractJsonSerializer _serializer = new DataContractJsonSerializer(typeof(SlaveConnectionSettings));

        public string GetDefaultPath()
        {
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IecSlaveSimulator");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "connection-settings.json");
        }

        public async Task<SlaveConnectionSettings> LoadAsync()
        {
            string path = GetDefaultPath();
            if (!File.Exists(path))
                return SlaveConnectionSettings.CreateDefault();

            return await Task.Run(() =>
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    return (SlaveConnectionSettings)_serializer.ReadObject(stream);
                }
            }).ConfigureAwait(false);
        }

        public async Task SaveAsync(SlaveConnectionSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            string path = GetDefaultPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            await Task.Run(() =>
            {
                using (FileStream stream = File.Create(path))
                {
                    _serializer.WriteObject(stream, settings);
                }
            }).ConfigureAwait(false);
        }
    }
}
