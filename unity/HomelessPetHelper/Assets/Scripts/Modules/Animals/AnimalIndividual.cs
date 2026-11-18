using System;

namespace HomelessPetHelper.Modules.Animals
{
    /// <summary>
    /// Sole mutable individual record on the calling gameplay thread. Rule owners supply validated
    /// disposition effects; UI must never call this to bypass action, stage, session or pause checks.
    /// Initial health is explicit and retained; medical state updates are deliberately not implemented yet.
    /// </summary>
    public sealed class AnimalIndividual
    {
        public AnimalSnapshot Current { get; private set; }

        public AnimalIndividual(Guid animalId, string definitionId,
            AnimalDisposition disposition, AnimalHealthSnapshot health)
        {
            if (animalId == Guid.Empty)
            {
                throw new ArgumentException("A nonempty animal ID is required.", nameof(animalId));
            }

            if (string.IsNullOrWhiteSpace(definitionId) || definitionId != definitionId.Trim())
            {
                throw new ArgumentException("A trimmed animal definition ID is required.", nameof(definitionId));
            }

            if (disposition == null)
            {
                throw new ArgumentNullException(nameof(disposition));
            }

            if (health == null)
            {
                throw new ArgumentNullException(nameof(health));
            }
            Current = new AnimalSnapshot(animalId, definitionId, disposition, health);
        }

        /// <summary>Applies a rule result only to the expected latest snapshot; refusal changes nothing.</summary>
        public bool TryApplyDisposition(AnimalSnapshot expected, AnimalDisposition disposition)
        {
            if (!ReferenceEquals(expected, Current) || disposition == null)
            {
                return false;
            }

            if (!ReferenceEquals(disposition, Current.Disposition))
            {
                Current = new AnimalSnapshot(Current.AnimalId, Current.DefinitionId, disposition, Current.Health);
            }

            return true;
        }
    }
}
