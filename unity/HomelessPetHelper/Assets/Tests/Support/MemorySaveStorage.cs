using System.Collections.Generic;
using System.IO;
using HomelessPetHelper.Framework.Persistence;

namespace HomelessPetHelper.Tests.Support
{
    public sealed class MemorySaveStorage : ISaveStorage
    {
        public readonly string[] Slots = new string[2];
        public readonly List<string> Archives = new List<string>();
        public bool FailWrites;
        public bool FailReads;
        public int Writes;
        public string Read(int slot)
        {
            if (FailReads) throw new IOException("Injected read failure.");
            return Slots[slot];
        }
        public void Write(int slot, string content)
        {
            if (FailWrites) throw new IOException("Injected write failure.");
            Slots[slot] = content;
            Writes++;
        }
        public void Archive(int slot) => Archives.Add(Read(slot));
    }
}
