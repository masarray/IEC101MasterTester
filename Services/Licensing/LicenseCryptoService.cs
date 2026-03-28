using IEC101MasterTester.Models.Licensing;
using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace IEC101MasterTester.Services.Licensing
{
    public interface IActivationKeyValidator
    {
        bool ValidateActivationKey(string activationKey, string hardwareId, out string reason);
    }

    public sealed class PlaceholderActivationKeyValidator : IActivationKeyValidator
    {
        public bool ValidateActivationKey(string activationKey, string hardwareId, out string reason)
        {
            reason = "Activation backend not configured.";
            return false;
        }
    }

    public sealed class LicenseCryptoService
    {
        private const string HmacSecret = "IEC101MasterTester-License-Core-v1";

        public string ComputeSignature(PersistedLicensePayload payload)
        {
            string canonicalJson = SerializeForSignature(payload);
            using (HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(HmacSecret)))
            {
                return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonicalJson)));
            }
        }

        public bool ValidateSignature(PersistedLicensePayload payload)
        {
            if (payload == null || string.IsNullOrWhiteSpace(payload.Signature))
            {
                return false;
            }

            string expected = ComputeSignature(CloneWithoutSignature(payload));
            return string.Equals(expected, payload.Signature, StringComparison.Ordinal);
        }

        public string Serialize(PersistedLicensePayload payload)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(PersistedLicensePayload));
            using (MemoryStream stream = new MemoryStream())
            {
                serializer.WriteObject(stream, payload);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        public PersistedLicensePayload Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(PersistedLicensePayload));
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return serializer.ReadObject(stream) as PersistedLicensePayload;
            }
        }

        public PersistedLicensePayload CreateSignedPayload(PersistedLicensePayload payload)
        {
            PersistedLicensePayload unsigned = CloneWithoutSignature(payload);
            unsigned.Signature = ComputeSignature(unsigned);
            return unsigned;
        }

        private static PersistedLicensePayload CloneWithoutSignature(PersistedLicensePayload payload)
        {
            return new PersistedLicensePayload
            {
                SchemaVersion = payload.SchemaVersion,
                InstallId = payload.InstallId,
                HardwareId = payload.HardwareId,
                FirstRunUtc = payload.FirstRunUtc,
                LastRunUtc = payload.LastRunUtc,
                LastReminderUtc = payload.LastReminderUtc,
                LicenseState = payload.LicenseState,
                ActivatedLicenseKey = payload.ActivatedLicenseKey,
                TamperFlag = payload.TamperFlag,
                TamperReason = payload.TamperReason,
                Signature = null
            };
        }

        private string SerializeForSignature(PersistedLicensePayload payload)
        {
            return Serialize(CloneWithoutSignature(payload));
        }
    }
}
