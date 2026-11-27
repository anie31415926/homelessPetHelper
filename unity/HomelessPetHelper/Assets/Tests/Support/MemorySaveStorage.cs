using System.Collections.Generic;
using System.IO;
using HomelessPetHelper.Framework.Persistence;

namespace HomelessPetHelper.Tests.Support
{
    public sealed class MemorySaveStorage : IDeletableSaveStorage
    {
        public readonly string[] Slots = new string[2];
        public readonly List<string> Archives = new List<string>();
        public bool FailWrites;
        public bool FailReads;
        public int Writes;
        public int? FailingDeleteSlot;
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
        public void Delete(int slot)
        {
            if (slot == FailingDeleteSlot) throw new IOException("Injected deletion failure.");
            Slots[slot] = null;
        }
        public void Archive(int slot) => Archives.Add(Read(slot));
    }
}
