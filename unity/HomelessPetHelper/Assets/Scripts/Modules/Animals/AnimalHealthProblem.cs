using System;

namespace HomelessPetHelper.Modules.Animals
{
    /// <summary>Identity of one existing problem, not a diagnosis, severity or treatment instruction.</summary>
    public sealed class AnimalHealthProblem
    {
        public string InstanceId { get; }
        public string TypeId { get; }

        public AnimalHealthProblem(string instanceId, string typeId)
        {
            if (string.IsNullOrWhiteSpace(instanceId) || instanceId != instanceId.Trim())
            {
                throw new ArgumentException("A trimmed problem instance ID is required.", nameof(instanceId));
            }

            if (string.IsNullOrWhiteSpace(typeId) || typeId != typeId.Trim())
            {
                throw new ArgumentException("A trimmed problem type ID is required.", nameof(typeId));
            }

            InstanceId = instanceId;
            TypeId = typeId;
        }
    }
}
