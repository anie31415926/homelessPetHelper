using System;

namespace HomelessPetHelper.Modules.Animals
{
    public sealed class AnimalSnapshot
    {
        public Guid AnimalId { get; }
        public string DefinitionId { get; }
        public AnimalDisposition Disposition { get; }
        public AnimalHealthSnapshot Health { get; }
        public AnimalCarePermissions CarePermissions { get; }
        /// <summary>Null means not supplied, never implicitly clean.</summary>
        public AnimalCoatCondition Coat { get; }

        internal AnimalSnapshot(Guid animalId, string definitionId,
            AnimalDisposition disposition, AnimalHealthSnapshot health, AnimalCarePermissions carePermissions,
            AnimalCoatCondition coat)
        {
            AnimalId = animalId;
            DefinitionId = definitionId;
            Disposition = disposition;
            Health = health;
            CarePermissions = carePermissions;
            Coat = coat;
        }
    }
}
