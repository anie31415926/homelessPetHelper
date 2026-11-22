using System.Collections.Generic;
using System.Linq;

namespace HomelessPetHelper.Modules.Siege.UI
{
    /// <summary>Reversible UI intent. Only SiegeSession.TryOrder commits an action.</summary>
    public sealed class SiegeFieldPlan
    {
        private readonly List<int> _materials = new List<int>();
        private readonly IReadOnlyList<int> _materialView;
        public SiegeFieldPlan() => _materialView = _materials.AsReadOnly();
        public int? SelectedCard { get; private set; }
        public int? DeployCard { get; private set; }
        public string DeployLane { get; private set; }
        public int? FocusedUnit { get; private set; }
        public int? Anchor { get; private set; }
        public bool SelectingMaterials { get; private set; }
        public bool SelectingReroute { get; private set; }
        public string RerouteLane { get; private set; }
        public int? RerouteUnit { get; private set; }
        public IReadOnlyList<int> Materials => _materialView;
        public bool HasAction => DeployCard.HasValue || Anchor.HasValue || RerouteLane != null;
        public bool ShowsEntrances => SelectedCard.HasValue || SelectingReroute;

        public void SelectCard(int id)
        {
            SelectedCard = SelectedCard == id ? null : (int?)id;
            FocusedUnit = null;
            SelectingReroute = false;
        }

        public void ChooseLane(string id)
        {
            if (SelectingReroute) { RerouteLane = id; SelectingReroute = false; }
            else if (SelectedCard.HasValue)
            {
                if (DeployCard != SelectedCard) RemoveNewMaterial();
                DeployCard = SelectedCard;
                DeployLane = id;
                SelectedCard = null;
            }
        }

        public void Focus(int id, SiegeCatalog catalog, SiegeState state)
        {
            if (!Eligible(id, catalog, state)) return;
            if (SelectingMaterials && Anchor.HasValue)
            {
                if (id == Anchor) return;
                string anchorType = Definition(Anchor.Value, state);
                if (Definition(id, state) != anchorType) return;
                if (_materials.Contains(id)) _materials.Remove(id);
                else if (_materials.Count < 3) _materials.Add(id);
                return;
            }
            FocusedUnit = id;
            SelectedCard = null;
            SelectingReroute = false;
        }

        public bool CanMerge(SiegeCatalog catalog, SiegeState state) => FocusedUnit.HasValue &&
            Eligible(FocusedUnit.Value, catalog, state) &&
            !string.IsNullOrEmpty(catalog.Unit(Definition(FocusedUnit.Value, state)).UpgradeId);

        public bool CanReroute(SiegeState state)
        {
            var unit = state.Units.FirstOrDefault(item => item.Id == FocusedUnit);
            return unit != null && unit.Reroutes > 0 && !unit.IsCarried && unit.TransitRemainingMs == 0 &&
                unit.HeldUntilMs <= state.ElapsedMs;
        }

        public void BeginMerge(SiegeCatalog catalog, SiegeState state)
        {
            if (!CanMerge(catalog, state)) return;
            Anchor = FocusedUnit;
            _materials.Clear(); _materials.Add(Anchor.Value);
            SelectingMaterials = true;
            SelectingReroute = false;
            RerouteLane = null; RerouteUnit = null;
        }

        public void BeginReroute(SiegeState state)
        {
            if (!CanReroute(state)) return;
            Anchor = null; _materials.Clear(); SelectingMaterials = false;
            SelectingReroute = true; RerouteLane = null; RerouteUnit = FocusedUnit;
        }

        public SiegeOrder Order(SiegeState state) => new SiegeOrder
        {
            SessionId = state.SessionId,
            Turn = state.Turn,
            DeployCardId = DeployCard,
            DeployLaneId = DeployLane,
            MergeIds = Anchor.HasValue ? _materials.ToArray() : null,
            AnchorId = Anchor ?? 0,
            RerouteUnitId = RerouteLane == null ? null : RerouteUnit,
            RerouteLaneId = RerouteLane,
        };

        public void CancelSelection()
        {
            SelectedCard = null; FocusedUnit = null; SelectingReroute = false;
            if (Anchor.HasValue && _materials.Count != 3)
            { Anchor = null; _materials.Clear(); SelectingMaterials = false; }
        }
        public void Clear()
        {
            CancelSelection(); DeployCard = null; DeployLane = null;
            Anchor = null; _materials.Clear(); SelectingMaterials = false; RerouteLane = null; RerouteUnit = null;
        }

        public string Definition(int id, SiegeState state) => id == SiegeOrder.NewUnit ?
            state.Hand.FirstOrDefault(item => item.Id == DeployCard)?.DefinitionId :
            state.Units.FirstOrDefault(item => item.Id == id)?.DefinitionId;

        private bool Eligible(int id, SiegeCatalog catalog, SiegeState state)
        {
            string definition = Definition(id, state);
            if (definition == null || catalog.Unit(definition) == null) return false;
            if (id == SiegeOrder.NewUnit) return DeployCard.HasValue;
            var unit = state.Units.First(item => item.Id == id);
            return !unit.IsCarried && unit.TransitRemainingMs == 0;
        }

        private void RemoveNewMaterial()
        {
            _materials.Remove(SiegeOrder.NewUnit);
            if (Anchor == SiegeOrder.NewUnit) { Anchor = null; _materials.Clear(); SelectingMaterials = false; }
        }
    }
}
