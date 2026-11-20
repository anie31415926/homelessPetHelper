using System;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HomelessPetHelper.Framework.Persistence
{
    internal static class SaveEnvelope
    {
        public static JObject NewDocument() => new JObject
        {
            ["formatVersion"] = 1,
            ["revision"] = 0L,
            ["sections"] = new JObject()
        };

        public static string Encode(JObject document)
        {
            string payload = document.ToString(Formatting.None);
            return new JObject { ["payload"] = payload, ["sha256"] = Hash(payload) }.ToString(Formatting.None);
        }

        public static JObject Decode(string content)
        {
            var envelope = SaveJson.Parse(content);
            if (envelope.Count != 2 || envelope["payload"]?.Type != JTokenType.String ||
                envelope["sha256"]?.Type != JTokenType.String)
                throw new JsonException("Invalid save envelope.");
            string payload = envelope["payload"].Value<string>();
            if (!string.Equals(Hash(payload), envelope["sha256"].Value<string>(), StringComparison.Ordinal))
                throw new JsonException("Save checksum mismatch.");
            var document = SaveJson.Parse(payload);
            long format = SaveJson.Integer(document, "formatVersion");
            if (format > 1) throw new SaveError(SaveErrorCode.FutureVersion, "Save format requires a newer client.");
            if (format != 1 || SaveJson.Integer(document, "revision") < 1 || !(document["sections"] is JObject))
                throw new JsonException("Invalid save document.");
            foreach (var section in ((JObject)document["sections"]).Properties())
            {
                if (!(section.Value is JObject record) || SaveJson.Integer(record, "version") < 1 ||
                    !(record["data"] is JObject)) throw new JsonException("Invalid section record.");
            }
            return document;
        }

        private static string Hash(string content)
        {
            using (var algorithm = SHA256.Create())
                return BitConverter.ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(content)))
                    .Replace("-", "").ToLowerInvariant();
        }
    }
}
