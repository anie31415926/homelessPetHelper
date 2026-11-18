using System;

namespace HomelessPetHelper.Modules.Animals
{
    public sealed class AnimalSnapshot
    {
        public Guid AnimalId { get; }
        public string DefinitionId { get; }
        public AnimalDisposition Disposition { get; }
        public AnimalHealthSnapshot Health { get; }

        internal AnimalSnapshot(Guid animalId, string definitionId,
            AnimalDisposition disposition, AnimalHealthSnapshot health)
        {
            AnimalId = animalId;
            DefinitionId = definitionId;
            Disposition = disposition;
            Health = health;
        }
    }
}
