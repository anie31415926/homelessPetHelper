using System;
using UnityEngine;

namespace HomelessPetHelper.Modules.Station.UI.Habitat
{
    /// <summary>One row of the handbook build page. Text is already localized by the caller.</summary>
    public readonly struct HandbookOffer
    {
        public readonly string Title;
        public readonly string State;
        public readonly Sprite Icon;
        public readonly bool Owned;

        public HandbookOffer(string title, string state, Sprite icon, bool owned)
        {
            Title = title;
            State = state;
            Icon = icon;
            Owned = owned;
        }
    }

    /// <summary>One card of the handbook memorial page. Index follows the caller's display order.</summary>
    public readonly struct HandbookMemory
    {
        public readonly string Name;
        public readonly Sprite Portrait;

        public HandbookMemory(string name, Sprite portrait)
        {
            Name = name;
            Portrait = portrait;
        }
    }
}
