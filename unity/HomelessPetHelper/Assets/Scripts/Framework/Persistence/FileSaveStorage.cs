using System;
using System.IO;
using System.Text;

namespace HomelessPetHelper.Framework.Persistence
{
    /// <summary>Desktop/native storage. Not a claim of durable browser or WeChat filesystem support.</summary>
    public sealed class FileSaveStorage : IDeletableSaveStorage
    {
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        private readonly string _directory;

        public FileSaveStorage(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Save directory is required.");
            _directory = Path.GetFullPath(directory);
#if UNITY_WEBGL && !UNITY_EDITOR
            throw new PlatformNotSupportedException("Use a verified platform storage adapter, not browser file IO.");
#endif
        }

        public string Read(int slot)
        {
            try
            {
                return File.ReadAllText(GetPath(slot), Utf8);
            }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
        }

        public void Write(int slot, string content)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            Directory.CreateDirectory(_directory);
            byte[] bytes = Utf8.GetBytes(content);
            // Only the older/inactive slot is touched. The latest verified slot remains intact.
            using (var stream = new FileStream(GetPath(slot), FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        public void Delete(int slot)
        {
            try { File.Delete(GetPath(slot)); }
            catch (DirectoryNotFoundException) { } // The checkpoint is already absent.
        }

        public void Archive(int slot)
        {
            string original = Read(slot);
            if (original == null) return;
            string destination = GetPath(slot) + ".damaged-" + Guid.NewGuid().ToString("N");
            File.Copy(GetPath(slot), destination, false);
            if (!string.Equals(original, File.ReadAllText(destination, Utf8), StringComparison.Ordinal))
                throw new IOException("The damaged save archive could not be verified.");
        }

        private string GetPath(int slot)
        {
            if (slot < 0 || slot > 1) throw new ArgumentOutOfRangeException(nameof(slot));
            return Path.Combine(_directory, "save-" + slot + ".json");
        }
    }
}
