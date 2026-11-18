using System;
using System.Collections.Generic;
using System.Linq;

namespace HomelessPetHelper.Modules.Rescue.Options
{
    // Content selection only. The caller evaluates applicability and safety against the latest field state.
    public static class RescueOptionSupply
    {
        public static IReadOnlyList<RescueActionDefinition> Draw(RescueOptionCatalog catalog,
            IReadOnlyList<RescueOption> hand, IReadOnlyCollection<string> eligibleIds,
            IReadOnlyCollection<string> stabilizingIds, int offerCount, Random random)
        {
            if (catalog == null || hand == null || eligibleIds == null || stabilizingIds == null || random == null)
            {
                throw new ArgumentNullException("Supply inputs must be explicit.");
            }

            var retained = hand.Where(option => option.IsLocked).Select(option => catalog.Get(option.Definition.Id))
                .ToList();
            var groups = new HashSet<string>(retained.Select(entry => entry.GroupId), StringComparer.Ordinal);
            int flavorCount = retained.Count(entry => entry.Kind == RescueOptionKind.Flavor);
            int count = offerCount - retained.Count;
            if (count <= 0 || flavorCount > 1 || groups.Count != retained.Count)
            {
                throw new InvalidOperationException("Invalid retained option composition.");
            }

            var eligible = new HashSet<string>(eligibleIds, StringComparer.Ordinal);
            var stable = new HashSet<string>(stabilizingIds, StringComparer.Ordinal);
            foreach (string id in eligible)
            {
                catalog.Get(id);
            }

            // Catalog order, not HashSet enumeration, is the deterministic starting order.
            var available = catalog.Entries.Where(entry => eligible.Contains(entry.Id) &&
                !groups.Contains(entry.GroupId)).ToList();
            Shuffle(available, random);
            List<RescueOptionContent> drawn = SelectDistinct(available, groups, count, flavorCount);
            bool IsStable(RescueOptionContent entry) => entry.Kind == RescueOptionKind.Rescue &&
                eligible.Contains(entry.Id) && stable.Contains(entry.Id);
            if (!retained.Concat(drawn).Any(IsStable))
            {
                EnsureStableChoice(drawn, retained, available, IsStable);
            }

            return Array.AsReadOnly(drawn.Select(entry => new RescueActionDefinition(entry.Id)).ToArray());
        }

        private static void Shuffle(List<RescueOptionContent> available, Random random)
        {
            for (int index = available.Count - 1; index > 0; index--)
            {
                int swap = random.Next(index + 1);
                RescueOptionContent value = available[index];
                available[index] = available[swap];
                available[swap] = value;
            }
        }

        private static List<RescueOptionContent> SelectDistinct(IReadOnlyList<RescueOptionContent> available,
            HashSet<string> groups, int count, int flavorCount)
        {
            var drawn = new List<RescueOptionContent>();
            foreach (RescueOptionContent entry in available)
            {
                if (drawn.Count == count)
                {
                    break;
                }

                if (groups.Contains(entry.GroupId) || (entry.Kind == RescueOptionKind.Flavor && flavorCount == 1))
                {
                    continue;
                }

                drawn.Add(entry);
                groups.Add(entry.GroupId);
                flavorCount += entry.Kind == RescueOptionKind.Flavor ? 1 : 0;
            }

            if (drawn.Count != count)
            {
                throw new InvalidOperationException("Insufficient distinct eligible methods; no filler is allowed.");
            }

            return drawn;
        }

        private static void EnsureStableChoice(List<RescueOptionContent> drawn,
            IReadOnlyList<RescueOptionContent> retained, IReadOnlyList<RescueOptionContent> available,
            Func<RescueOptionContent, bool> isStable)
        {
            var retainedGroups = new HashSet<string>(retained.Select(entry => entry.GroupId),
                StringComparer.Ordinal);
            RescueOptionContent safe = available.FirstOrDefault(entry => isStable(entry) &&
                !retainedGroups.Contains(entry.GroupId));
            if (safe == null)
            {
                throw new InvalidOperationException("No real stabilizing rescue choice is available.");
            }

            int replace = drawn.FindIndex(entry => entry.GroupId == safe.GroupId);
            drawn[replace < 0 ? drawn.Count - 1 : replace] = safe;
        }
    }
}
