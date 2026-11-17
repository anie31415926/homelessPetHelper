namespace HomelessPetHelper.Modules.Rescue.Options
{
    /// <summary>
    /// Immutable option snapshot. Instance IDs are unique within the owning hand's session.
    /// </summary>
    public sealed class RescueOption
    {
        public long InstanceId { get; }

        public RescueActionDefinition Definition { get; }

        public bool IsLocked { get; }

        internal RescueOption(long instanceId, RescueActionDefinition definition, bool isLocked)
        {
            InstanceId = instanceId;
            Definition = definition;
            IsLocked = isLocked;
        }
    }
}
