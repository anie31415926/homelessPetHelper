using System;

namespace HomelessPetHelper.Modules.Rescue.Options
{
    /// <summary>
    /// Identifies an action without defining its still-unconfirmed effects or presentation.
    /// Shared definitions are immutable; each offered option has separate instance state.
    /// </summary>
    public sealed class RescueActionDefinition
    {
        public string Id { get; }

        public RescueActionDefinition(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id != id.Trim())
            {
                throw new ArgumentException(
                    "Action ID must be nonempty and have no surrounding whitespace.", nameof(id));
            }

            Id = id;
        }
    }
}
