using System;
using System.Linq;
using HomelessPetHelper.Modules.Rescue.Options;

namespace HomelessPetHelper.Editor.RescueCase
{
    internal static class CaseActions
    {
        private static readonly string[] Ids = { "give-space", "wait", "food", "toy", "approach" };

        // The five-rule fixture remains usable without Unity assets in existing rule tests.
        internal static RescueOptionCatalog CreateSampleCatalog()
        {
            return new RescueOptionCatalog(Ids.Select(id => new RescueOptionContent(id, id,
                RescueOptionKind.Rescue, id, string.Empty)).ToArray());
        }

        internal static void ValidateCatalog(RescueOptionCatalog catalog)
        {
            foreach (string id in Ids)
            {
                if (catalog.Get(id).Kind != RescueOptionKind.Rescue || catalog.Get(id).GroupId != id)
                {
                    throw new ArgumentException("Trial rescue methods must keep their identity: " + id);
                }
            }

            foreach (RescueOptionContent entry in catalog.Entries.Where(item => item.Kind == RescueOptionKind.Rescue))
            {
                Parse(entry.Id);
            }
        }

        internal static string Id(CaseAction action)
        {
            return Ids[(int)action];
        }

        internal static CaseAction Parse(string id)
        {
            int index = Array.IndexOf(Ids, id);
            if (index < 0)
            {
                throw new ArgumentException("Unknown trial action.", nameof(id));
            }

            return (CaseAction)index;
        }
    }
}
