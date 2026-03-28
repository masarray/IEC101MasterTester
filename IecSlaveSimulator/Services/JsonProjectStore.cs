using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using IecSlaveSimulator.Models;

namespace IecSlaveSimulator.Services
{
    public sealed class JsonProjectStore
    {
        private const string DefaultFolderName = "IecSlaveSimulator";
        private const string DefaultFileName = "slave-project.json";
        private static readonly DataContractJsonSerializer Serializer = new DataContractJsonSerializer(typeof(SlaveProjectDefinition));

        public string GetDefaultDirectory()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), DefaultFolderName);
        }

        public string GetDefaultPath()
        {
            return Path.Combine(GetDefaultDirectory(), DefaultFileName);
        }

        public string EnsureDefaultDirectory()
        {
            string directory = GetDefaultDirectory();
            Directory.CreateDirectory(directory);
            return directory;
        }

        public Task<SlaveProjectDefinition> LoadAsync(string filePath)
        {
            return Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                {
                    return SlaveProjectDefinition.CreateDefault();
                }

                using (FileStream stream = File.OpenRead(filePath))
                {
                    SlaveProjectDefinition project = Serializer.ReadObject(stream) as SlaveProjectDefinition;
                    return project ?? SlaveProjectDefinition.CreateDefault();
                }
            });
        }

        public Task SaveAsync(string filePath, SlaveProjectDefinition project)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path is required.", nameof(filePath));
            }

            return Task.Run(() =>
            {
                string directory = Path.GetDirectoryName(filePath);
                if (string.IsNullOrWhiteSpace(directory))
                {
                    directory = EnsureDefaultDirectory();
                    filePath = Path.Combine(directory, Path.GetFileName(filePath));
                }
                else if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (FileStream stream = File.Create(filePath))
                {
                    Serializer.WriteObject(stream, project);
                }
            });
        }
    }
}
