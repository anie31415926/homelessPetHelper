using System;

namespace HomelessPetHelper.Editor.RescueCase
{
    internal static class CaseActions
    {
        private static readonly string[] Ids = { "give-space", "wait", "food", "toy", "approach" };

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
