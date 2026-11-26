using System;
using HomelessPetHelper.Game.Progress;
using HomelessPetHelper.Game.Text;
using HomelessPetHelper.Game.UI;

namespace HomelessPetHelper.Game.Station
{
    internal sealed class StationEconomyMenu
    {
        private static readonly StationPurchase[] Items =
            { StationPurchase.Beds, StationPurchase.Cleaning, StationPurchase.Clip };
        private readonly MainProgressSession _progress;
        private readonly OptionPreviewText _text;
        private readonly Action<StationDialog> _show;
        private readonly Action _refresh, _saveFailed;

        internal StationEconomyMenu(MainProgressSession progress, OptionPreviewText text,
            Action<StationDialog> show, Action refresh, Action saveFailed)
        {
            _progress = progress;
            _text = text;
            _show = show;
            _refresh = refresh;
            _saveFailed = saveFailed;
        }

        internal void Open(int index = 0) =>
            Show(index < 0 || index >= Items.Length ? 0 : index);

        private void Show(int index)
        {
            var item = Items[index];
            string key = "economy." + item;
            int price = _progress.EconomyRules.Price(item);
            int capacity = _progress.Session.Station.TemporaryCapacity;
            bool owned = _progress.Owns(item);
            bool canBuy = !owned && _progress.AidBalance >= price;
            bool canEquip = owned && item == StationPurchase.Clip;
            string ownedStatus = canEquip ? _progress.ClipEquipped ? "economy.equipped" :
                "economy.unequipped" : "economy.owned";
            string status = _text.Get(owned ? ownedStatus : canBuy ? "economy.price" : "economy.short", price);
            int addedBeds = _progress.EconomyRules.extraBeds;
            _show(new StationDialog
            {
                Title = _text.Get(key + ".title"),
                Body = _text.Get("economy.page", index + 1, Items.Length, _progress.AidBalance,
                    _text.Get(key + (owned ? ".owned" : ".description"), capacity,
                        (long)capacity + addedBeds, addedBeds), status),
                CancelLabel = _text.Get("adoption.close"), BrowseLabel = _text.Get("economy.next"),
                Browse = () => Show((index + 1) % Items.Length),
                ConfirmLabel = _text.Get(canEquip ? _progress.ClipEquipped ? "economy.unequip" :
                    "economy.equip" : "economy.buy"),
                Deliberate = canBuy,
                Accept = canBuy || canEquip ? (Action)(() => Apply(item, index, canBuy)) : null
            });
        }

        private void Apply(StationPurchase item, int index, bool buy)
        {
            bool changed = buy ? _progress.TryPurchase(item) : _progress.TrySetClipEquipped(!_progress.ClipEquipped);
            if (!_progress.CanContinue) { _saveFailed(); return; }
            if (changed) _refresh();
            Show(index);
        }
    }
}
