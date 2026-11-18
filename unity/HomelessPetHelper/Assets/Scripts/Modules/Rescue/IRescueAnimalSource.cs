using System;
using HomelessPetHelper.Modules.Animals;

namespace HomelessPetHelper.Modules.Rescue
{
    /// <summary>
    /// Trusted rule-owned source, bound before completion. Completion must come from actual rescue rules,
    /// not a UI button, animation, map transition or caller-created replacement animal.
    /// </summary>
    public interface IRescueAnimalSource
    {
        Guid SessionId { get; }
        AnimalIndividual Individual { get; }
        bool IsRescueComplete { get; }
    }
}
