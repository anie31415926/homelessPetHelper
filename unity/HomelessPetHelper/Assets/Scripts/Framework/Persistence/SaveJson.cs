using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HomelessPetHelper.Framework.Persistence
{
    internal static class SaveJson
    {
        public static JsonSerializer Serializer() => JsonSerializer.Create(new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Error,
            DateParseHandling = DateParseHandling.None,
            Culture = CultureInfo.InvariantCulture,
            MaxDepth = 64
        });

        public static JObject Parse(string text)
        {
            using (var input = new StringReader(text))
            using (var reader = new JsonTextReader(input) { DateParseHandling = DateParseHandling.None, MaxDepth = 64 })
            {
                var value = JObject.Load(reader, new JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                    CommentHandling = CommentHandling.Ignore
                });
                if (reader.Read()) throw new JsonException("Unexpected trailing save content.");
                return value;
            }
        }

        public static long Integer(JObject value, string key)
        {
            if (value[key]?.Type != JTokenType.Integer) throw new JsonException("Missing integer: " + key);
            return value[key].Value<long>();
        }
    }
}
