using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json.Linq;

namespace HomelessPetHelper.Framework.Persistence.WeChat
{
    /// <summary>
    /// Explicit WeChat-only adapter; construct after SDK initialization. Ordinary WebGL is unsupported.
    /// Uses native wx synchronous storage, not PlayerPrefs or the SDK's error-to-default getter.
    /// Actual device persistence, quota and hide/kill behavior still require channel validation.
    /// </summary>
    public sealed class WeChatSaveStorage : IDeletableSaveStorage
    {
        private const int MaxValueBytes = 900 * 1024;
        private readonly string _prefix;

        public WeChatSaveStorage(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix) || prefix.Length > 128)
                throw new ArgumentException("A stable storage prefix of at most 128 characters is required.");
            _prefix = prefix;
        }

        public string Read(int slot) => ReadKey(Key(slot));

        public void Write(int slot, string content) => WriteKey(Key(slot), content);

        public void Delete(int slot)
        {
            string key = Key(slot);
#if UNITY_WEBGL && !UNITY_EDITOR
            RequireSuccess(SaveJson.Parse(HPHSaveDelete(key)));
#else
            throw new PlatformNotSupportedException("WeChat storage requires the converted WeChat runtime.");
#endif
        }

        public void Archive(int slot)
        {
            string original = Read(slot);
            if (original == null) return;
            string archiveKey = Key(slot) + ".damaged-" + Guid.NewGuid().ToString("N");
            WriteKey(archiveKey, original);
            if (!string.Equals(ReadKey(archiveKey), original, StringComparison.Ordinal))
                throw new IOException("The damaged WeChat save archive could not be verified.");
        }

        private string Key(int slot)
        {
            if (slot < 0 || slot > 1) throw new ArgumentOutOfRangeException(nameof(slot));
            return _prefix + ".slot" + slot;
        }

        private static string ReadKey(string key)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var response = SaveJson.Parse(HPHSaveRead(key));
            RequireSuccess(response);
            if (response["value"]?.Type == JTokenType.Null) return null;
            if (response["value"]?.Type != JTokenType.String)
                throw new IOException("Unexpected WeChat storage value type.");
            return response["value"].Value<string>();
#else
            throw new PlatformNotSupportedException("WeChat storage requires the converted WeChat runtime.");
#endif
        }

        private static void WriteKey(string key, string content)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (Encoding.UTF8.GetByteCount(content) > MaxValueBytes)
                throw new IOException("Checkpoint exceeds the WeChat adapter's 900 KiB per-slot budget.");
#if UNITY_WEBGL && !UNITY_EDITOR
            RequireSuccess(SaveJson.Parse(HPHSaveWrite(key, content)));
#else
            throw new PlatformNotSupportedException("WeChat storage requires the converted WeChat runtime.");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private static void RequireSuccess(JObject response)
        {
            if (response["ok"]?.Type != JTokenType.Boolean || !response["ok"].Value<bool>())
                throw new IOException("WeChat storage operation failed: " + response["code"]?.Value<string>());
        }

        [DllImport("__Internal")]
        private static extern string HPHSaveRead(string key);

        [DllImport("__Internal")]
        private static extern string HPHSaveWrite(string key, string content);

        [DllImport("__Internal")]
        private static extern string HPHSaveDelete(string key);
#endif
    }
}
