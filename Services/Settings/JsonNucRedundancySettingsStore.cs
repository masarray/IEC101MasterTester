using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using IEC101MasterTester.Models;

namespace IEC101MasterTester.Services.Settings
{
    public sealed class JsonNucRedundancySettingsStore : INucRedundancySettingsStore
    {
        private static readonly DataContractJsonSerializer Serializer = new DataContractJsonSerializer(typeof(NucRedundancySettings));
        private readonly string _settingsFilePath;

        public JsonNucRedundancySettingsStore()
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IEC101MasterTester");
            _settingsFilePath = Path.Combine(root, "nuc-redundancy-settings.json");
        }

        public Task<NucRedundancySettings> LoadAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(_settingsFilePath))
                    {
                        return null;
                    }

                    using (FileStream stream = File.OpenRead(_settingsFilePath))
                    {
                        return Serializer.ReadObject(stream) as NucRedundancySettings;
                    }
                }
                catch
                {
                    return null;
                }
            });
        }

        public Task SaveAsync(NucRedundancySettings settings)
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
