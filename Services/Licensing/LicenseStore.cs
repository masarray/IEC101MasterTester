using IEC101MasterTester.Models.Licensing;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IEC101MasterTester.Services.Licensing
{
    public sealed class LicenseStore
    {
        private const string RegistryRoot = @"Software\Arisulistiono\IEC101MasterTester\Licensing";
        private readonly LicenseCryptoService _cryptoService;

        public LicenseStore(LicenseCryptoService cryptoService)
        {
            _cryptoService = cryptoService;
        }

        public string RegistryLocation => @"HKCU\" + RegistryRoot;
        public string ProgramDataLocation => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Arisulistiono", "IEC101MasterTester", ".license.dat");
        public string LocalAppDataLocation => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Arisulistiono", "IEC101MasterTester", ".license.dat");

        public IReadOnlyList<LicenseStoreRecord> LoadAll()
        {
            return new List<LicenseStoreRecord>
            {
                LoadRegistry(),
                LoadFile(ProgramDataLocation, "ProgramData"),
                LoadFile(LocalAppDataLocation, "LocalAppData")
            };
        }

        public void SaveAll(PersistedLicensePayload payload)
        {
            PersistedLicensePayload signedPayload = _cryptoService.CreateSignedPayload(payload);
            TrySaveRegistry(signedPayload);
            TrySaveFile(ProgramDataLocation, signedPayload);
            TrySaveFile(LocalAppDataLocation, signedPayload);
        }

        public PersistedLicensePayload Reconcile(IReadOnlyList<LicenseStoreRecord> records, string hardwareId, out string tamperReason)
        {
            tamperReason = DetectMissingOrMismatch(records, hardwareId);
            LicenseStoreRecord best = records
                .Where(r => r.Payload != null && r.IsSignatureValid)
                .OrderByDescending(r => r.Payload.LastRunUtc)
                .FirstOrDefault();

            return best?.Payload;
        }

        public string DetectMissingOrMismatch(IReadOnlyList<LicenseStoreRecord> records, string hardwareId)
        {
            List<LicenseStoreRecord> accessible = records.Where(r => r.LoadError == null).ToList();
            List<LicenseStoreRecord> valid = accessible.Where(r => r.Payload != null).ToList();
            if (valid.Count == 0)
            {
                return null;
            }

            if (valid.Any(r => !r.IsSignatureValid))
            {
                return "License payload signature mismatch detected.";
            }

            bool hasMissingStore = accessible.Count > 0 && valid.Count != accessible.Count;
            if (hasMissingStore)
            {
                return "One or more license stores are missing.";
            }

            string installId = valid[0].Payload.InstallId;
            if (valid.Any(r => !string.Equals(r.Payload.InstallId, installId, StringComparison.Ordinal)))
            {
                return "License install identity mismatch detected.";
            }

            if (valid.Any(r => !string.Equals(r.Payload.HardwareId, hardwareId, StringComparison.Ordinal)))
            {
                return "License hardware identity mismatch detected.";
            }

            string canonicalSignature = valid[0].Payload.Signature ?? string.Empty;
            if (valid.Any(r => !string.Equals(r.Payload.Signature ?? string.Empty, canonicalSignature, StringComparison.Ordinal)))
            {
                return "License store copies are inconsistent.";
            }

            return null;
        }

        private LicenseStoreRecord LoadRegistry()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryRoot))
                {
                    string json = key?.GetValue("Payload") as string;
                    return CreateRecord("Registry", json);
                }
            }
            catch (Exception ex)
            {
                return new LicenseStoreRecord("Registry", null, false, ex.Message);
            }
        }

        private LicenseStoreRecord LoadFile(string path, string name)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return new LicenseStoreRecord(name, null, false, null);
                }

                string json = File.ReadAllText(path);
                return CreateRecord(name, json);
            }
            catch (Exception ex)
            {
                return new LicenseStoreRecord(name, null, false, ex.Message);
            }
        }

        private LicenseStoreRecord CreateRecord(string locationName, string json)
        {
            PersistedLicensePayload payload = _cryptoService.Deserialize(json);
            bool isValid = payload != null && _cryptoService.ValidateSignature(payload);
            return new LicenseStoreRecord(locationName, payload, isValid, null);
        }

        private void TrySaveRegistry(PersistedLicensePayload payload)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryRoot))
                {
                    key.SetValue("Payload", _cryptoService.Serialize(payload), RegistryValueKind.String);
                }
            }
            catch
            {
            }
        }

        private void TrySaveFile(string path, PersistedLicensePayload payload)
        {
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(path, _cryptoService.Serialize(payload));
                try
                {
                    File.SetAttributes(path, FileAttributes.Hidden);
                }
                catch
                {
                }
            }
            catch
            {
            }
        }
    }

    public sealed class LicenseStoreRecord
    {
        public LicenseStoreRecord(string locationName, PersistedLicensePayload payload, bool isSignatureValid, string loadError)
        {
            LocationName = locationName;
            Payload = payload;
            IsSignatureValid = isSignatureValid;
            LoadError = loadError;
        }

        public string LocationName { get; }
        public PersistedLicensePayload Payload { get; }
        public bool IsSignatureValid { get; }
        public string LoadError { get; }
    }
}
