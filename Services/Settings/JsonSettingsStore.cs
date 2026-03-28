using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using IEC101MasterTester.Models;

namespace IEC101MasterTester.Services.Settings
{
    public sealed class JsonSettingsStore : ISettingsStore
    {
        private static readonly DataContractJsonSerializer Serializer = new DataContractJsonSerializer(typeof(ConnectionSettings));
        private readonly string _settingsFilePath;

        public JsonSettingsStore()
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IEC101MasterTester");
            _settingsFilePath = Path.Combine(root, "connection-settings.json");
        }

        public Task<ConnectionSettings> LoadAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(_settingsFilePath))
                    {
                        return ConnectionSettings.CreateDefault();
                    }

                    using (FileStream stream = File.OpenRead(_settingsFilePath))
                    {
                        ConnectionSettings loaded = Serializer.ReadObject(stream) as ConnectionSettings;
                        return loaded ?? ConnectionSettings.CreateDefault();
                    }
                }
                catch
                {
                    return ConnectionSettings.CreateDefault();
                }
            });
        }

        public Task SaveAsync(ConnectionSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            return Task.Run(() =>
            {
                string directory = Path.GetDirectoryName(_settingsFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (FileStream stream = File.Create(_settingsFilePath))
                {
                    Serializer.WriteObject(stream, settings);
                }
            });
        }
    }
}
