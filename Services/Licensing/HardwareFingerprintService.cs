using Microsoft.Win32;
using System;
using System.Security.Cryptography;
using System.Text;

namespace IEC101MasterTester.Services.Licensing
{
    public sealed class HardwareFingerprintService
    {
        public string GenerateHardwareId()
        {
            string machineGuid = ReadMachineGuid();
            string machineName = Environment.MachineName ?? string.Empty;
            string domainName = Environment.UserDomainName ?? string.Empty;
            string osVersion = Environment.OSVersion.VersionString ?? string.Empty;
            string processorCount = Environment.ProcessorCount.ToString();

            string raw = string.Join("|",
                Normalize(machineGuid),
                Normalize(machineName),
                Normalize(domainName),
                Normalize(osVersion),
                Normalize(processorCount));

            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
                return Convert.ToBase64String(hash)
                    .Replace('+', '-')
                    .Replace('/', '_')
                    .TrimEnd('=');
            }
        }

        private static string ReadMachineGuid()
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
            {
                object value = key?.GetValue("MachineGuid");
                return value as string ?? string.Empty;
            }
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToUpperInvariant();
        }
    }
}
