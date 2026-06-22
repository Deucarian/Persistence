using System;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Deucarian.Persistence
{
    /// <summary>Serializer abstraction for persistence payloads.</summary>
    public interface IPersistenceSerializer
    {
        /// <summary>Serializes a value.</summary>
        string Serialize<T>(T value);

        /// <summary>Deserializes a value.</summary>
        T Deserialize<T>(string text);

        /// <summary>Reads the schema version from a saved envelope.</summary>
        bool TryReadEnvelopeMetadata(string text, out DocumentId documentId, out SchemaVersion schemaVersion, out string payload, out string checksum, out string message);
    }

    /// <summary>Default Newtonsoft JSON serializer.</summary>
    public sealed class NewtonsoftPersistenceSerializer : IPersistenceSerializer
    {
        private readonly JsonSerializerSettings _settings;

        /// <summary>Creates the default serializer.</summary>
        public NewtonsoftPersistenceSerializer()
        {
            _settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Include,
                TypeNameHandling = TypeNameHandling.None,
                DateParseHandling = DateParseHandling.None,
                ObjectCreationHandling = ObjectCreationHandling.Replace
            };
        }

        /// <inheritdoc />
        public string Serialize<T>(T value) => JsonConvert.SerializeObject(value, _settings);

        /// <inheritdoc />
        public T Deserialize<T>(string text) => JsonConvert.DeserializeObject<T>(text, _settings);

        /// <inheritdoc />
        public bool TryReadEnvelopeMetadata(string text, out DocumentId documentId, out SchemaVersion schemaVersion, out string payload, out string checksum, out string message)
        {
            documentId = default;
            schemaVersion = default;
            payload = string.Empty;
            checksum = string.Empty;
            message = string.Empty;

            try
            {
                JObject root = JObject.Parse(text);
                string document = root.Value<string>("documentId");
                int? version = root.Value<int?>("schemaVersion");
                JToken payloadToken = root["payload"];
                if (string.IsNullOrWhiteSpace(document) || version == null || payloadToken == null)
                {
                    message = "Envelope is missing required metadata.";
                    return false;
                }

                documentId = new DocumentId(document);
                schemaVersion = new SchemaVersion(version.Value);
                payload = payloadToken.Type == JTokenType.String ? payloadToken.Value<string>() : payloadToken.ToString(Formatting.None);
                checksum = root.Value<string>("checksum") ?? string.Empty;
                return true;
            }
            catch (Exception ex) when (ex is JsonException || ex is ArgumentException)
            {
                message = ex.Message;
                return false;
            }
        }
    }

    /// <summary>Persistence envelope persisted around document payloads.</summary>
    public sealed class SaveEnvelope
    {
        /// <summary>Envelope format marker.</summary>
        [JsonProperty("format")]
        public string Format { get; set; } = "deucarian.persistence.v1";

        /// <summary>Document id.</summary>
        [JsonProperty("documentId")]
        public string DocumentId { get; set; }

        /// <summary>Schema version.</summary>
        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; }

        /// <summary>UTC save timestamp.</summary>
        [JsonProperty("savedAtUtc")]
        public string SavedAtUtc { get; set; }

        /// <summary>SHA-256 checksum over the payload text for accidental corruption detection.</summary>
        [JsonProperty("checksum")]
        public string Checksum { get; set; }

        /// <summary>Serialized document payload as JSON text.</summary>
        [JsonProperty("payload")]
        public string Payload { get; set; }
    }

    /// <summary>Envelope helpers.</summary>
    public static class SaveEnvelopeCodec
    {
        /// <summary>Creates serialized envelope text.</summary>
        public static string Create<T>(DocumentDefinition<T> definition, T document, IPersistenceSerializer serializer, DateTimeOffset savedAtUtc)
        {
            string payload = serializer.Serialize(document);
            var envelope = new SaveEnvelope
            {
                DocumentId = definition.DocumentId.Value,
                SchemaVersion = definition.CurrentVersion.Value,
                SavedAtUtc = savedAtUtc.UtcDateTime.ToString("O"),
                Payload = payload,
                Checksum = ComputeChecksum(payload)
            };
            return serializer.Serialize(envelope);
        }

        /// <summary>Computes a SHA-256 checksum over payload text.</summary>
        public static string ComputeChecksum(string payload)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(payload ?? string.Empty));
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    builder.Append(hash[index].ToString("x2"));
                }

                return builder.ToString();
            }
        }

        /// <summary>Checks whether the checksum matches the payload.</summary>
        public static bool ChecksumMatches(string payload, string checksum)
        {
            return string.Equals(ComputeChecksum(payload), checksum ?? string.Empty, StringComparison.Ordinal);
        }
    }
}
