using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static HomelessPetHelper.Modules.Siege.SiegeState;

namespace HomelessPetHelper.Modules.Siege.UI
{
    public sealed class SiegeTrialView : MonoBehaviour
    {
        [SerializeField]
        private TextAsset _data,
            _copy;

        [SerializeField]
        private Text _status,
            _intent,
            _feedback,
            _plan,
            _seed,
            _detail;

        [SerializeField]
        private Slider _playerBar,
            _animalBar;

        [SerializeField]
        private SiegeBattlefield _field;

        [SerializeField]
        private SiegeChoiceCell _cellPrefab;

        [SerializeField]
        private RectTransform _handRoot,
            _rosterRoot;

        [SerializeField]
        private GameObject _materialsPanel;

        [SerializeField]
        private Button _top,
            _mid,
            _bottom,
            _submit,
            _discard,
            _clear,
            _materials;

        [SerializeField]
        private Button _merge,
            _anchor,
            _newMaterial,
            _reroute,
            _destination;

        [SerializeField]
        private Button _pause,
            _retry,
            _newSeed,
            _profile;
        private SiegeCatalog _catalog;
        private SiegeText _text;
        private SiegeSession _session;
        private SiegeCellPool _handPool,
            _rosterPool;
        private uint _seedValue = 20261119;
        private int _profileIndex,
            _laneIndex = 1,
            _destinationIndex;
        private int? _card,
            _rerouteId;
        private readonly List<int> _materialIds = new List<int>();
        private int _anchorId;
        private bool _mergePlanned;
        private string _tower,
            _error;
        private int _renderedTime = -1;
        private float _handWidth = -1;
        private HorizontalLayoutGroup _handLayout;
        public SiegeState Snapshot => _session?.Snapshot;

        private void OnEnable()
        {
            _top.onClick.AddListener(Top);
            _mid.onClick.AddListener(Mid);
            _bottom.onClick.AddListener(Bottom);
            _submit.onClick.AddListener(Submit);
            _discard.onClick.AddListener(Discard);
            _clear.onClick.AddListener(Clear);
            _materials.onClick.AddListener(MaterialPanel);
            _merge.onClick.AddListener(Merge);
            _anchor.onClick.AddListener(Anchor);
            _newMaterial.onClick.AddListener(NewMaterial);
            _reroute.onClick.AddListener(Reroute);
            _destination.onClick.AddListener(Destination);
            _pause.onClick.AddListener(Pause);
            _retry.onClick.AddListener(Restart);
            _newSeed.onClick.AddListener(NewSeed);
            _profile.onClick.AddListener(Profile);
            _field.UnitClicked += SelectUnit;
            _field.TowerClicked += SelectTower;
            if (_catalog != null)
                Open();
        }

        private void OnDisable()
        {
            _top.onClick.RemoveListener(Top);
            _mid.onClick.RemoveListener(Mid);
            _bottom.onClick.RemoveListener(Bottom);
            _submit.onClick.RemoveListener(Submit);
            _discard.onClick.RemoveListener(Discard);
            _clear.onClick.RemoveListener(Clear);
            _materials.onClick.RemoveListener(MaterialPanel);
            _merge.onClick.RemoveListener(Merge);
            _anchor.onClick.RemoveListener(Anchor);
            _newMaterial.onClick.RemoveListener(NewMaterial);
            _reroute.onClick.RemoveListener(Reroute);
            _destination.onClick.RemoveListener(Destination);
            _pause.onClick.RemoveListener(Pause);
            _retry.onClick.RemoveListener(Restart);
            _newSeed.onClick.RemoveListener(NewSeed);
            _profile.onClick.RemoveListener(Profile);
            _field.UnitClicked -= SelectUnit;
            _field.TowerClicked -= SelectTower;
            _handPool?.Dispose();
            _rosterPool?.Dispose();
            _handPool = null;
            _rosterPool = null;
            _session = null;
        }

        private void Start()
        {
            try
            {
                if (_data == null || _copy == null || _cellPrefab == null || _field == null)
                    throw new InvalidOperationException("Missing siege bindings.");
                _catalog = new SiegeCatalog(JsonUtility.FromJson<SiegeCatalogData>(_data.text));
                _text = new SiegeText(_copy);
                _text.Validate(_catalog);
                if (_catalog.Lanes.Count != 3)
                    throw new InvalidOperationException("This trial view has three authored route controls.");
                Open();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                _feedback.text = _text?.Get("unity.failed") ?? "Configuration error";
                enabled = false;
            }
        }

        private void Open()
        {
            _handLayout = _handRoot.GetComponent<HorizontalLayoutGroup>();
            _handLayout.childForceExpandWidth = false;
            _handPool = new SiegeCellPool(_cellPrefab, _handRoot, _catalog.Rules.HandSize);
            _rosterPool = new SiegeCellPool(
                _cellPrefab,
                _rosterRoot,
                _catalog.Rules.DurationMs / _catalog.Rules.PlayerPeriodMs + 2
            );
            _materialsPanel.SetActive(false);
            Restart();
        }

        private void Restart()
        {
            _session = new SiegeSession(
                _catalog,
                _seedValue,
                _catalog.Profiles[_profileIndex].Id,
                Guid.NewGuid().ToString("N")
            );
            _tower = null;
            _laneIndex = 1;
            Clear();
            _renderedTime = -1;
        }

        private void NewSeed()
        {
            unchecked
            {
                _seedValue++;
            }
            Restart();
        }

        private void Profile()
        {
            _profileIndex = (_profileIndex + 1) % _catalog.Profiles.Count;
            Restart();
        }

        private void Update()
        {
            if (_session == null)
                return;
            var phase = _session.Phase;
            _session.Advance(Math.Min(100, Time.unscaledDeltaTime * 1000));
            if (
                _renderedTime != _session.ElapsedMs
                || phase != _session.Phase
                || !Mathf.Approximately(_handWidth, _handRoot.rect.width)
            )
                Render();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
                Interrupt();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                Interrupt();
        }

        private void Interrupt()
        {
            if (_session == null)
                return;
            _session.SetPaused(true);
            Render();
        }

        private void Pause()
        {
            _session.SetPaused(!_session.Paused);
            Render();
        }

        private void Top() => Route(0);

        private void Mid() => Route(1);

        private void Bottom() => Route(2);

        private void Route(int index)
        {
            _laneIndex = index;
            _error = null;
            Render();
        }

        private void SelectCard(int id)
        {
            _card = _card == id ? null : (int?)id;
            if (!_card.HasValue)
                _materialIds.Remove(SiegeOrder.NewUnit);
            _error = null;
            Render();
        }

        private void SelectTower(string id)
        {
            _tower = _tower == id ? null : id;
            Render();
        }

        private void MaterialPanel()
        {
            _materialsPanel.SetActive(!_materialsPanel.activeSelf);
            Render();
        }

        private void SelectUnit(int id)
        {
            if (_session.Phase != BattlePhase.Decision || _session.Paused)
                return;
            if (_materialIds.Contains(id))
                _materialIds.Remove(id);
            else if (_materialIds.Count < 3)
                _materialIds.Add(id);
            _rerouteId = null;
            if (!_materialIds.Contains(_anchorId))
                _anchorId = _materialIds.FirstOrDefault();
            _error = null;
            Render();
        }

        private void NewMaterial() => SelectUnit(SiegeOrder.NewUnit);

        private void Merge()
        {
            _mergePlanned = !_mergePlanned;
            _rerouteId = null;
            _error = null;
            Render();
        }

        private void Anchor()
        {
            if (_materialIds.Count == 0)
                return;
            _anchorId = _materialIds[(_materialIds.IndexOf(_anchorId) + 1) % _materialIds.Count];
            Render();
        }

        private void Reroute()
        {
            int? selected = _materialIds.Count == 1 && _materialIds[0] > 0 ? _materialIds[0] : (int?)null;
            _rerouteId = _rerouteId.HasValue ? null : selected;
            _mergePlanned = false;
            var unit = _session.Snapshot.Units.FirstOrDefault(x => x.Id == _rerouteId);
            if (unit != null)
                _destinationIndex = (_catalog.Lanes.ToList().FindIndex(x => x.Id == unit.LaneId) + 1) % 3;
            _error = null;
            Render();
        }

        private void Destination()
        {
            _destinationIndex = (_destinationIndex + 1) % 3;
            Render();
        }

        private void Clear()
        {
            _card = null;
            _materialIds.Clear();
            _anchorId = 0;
            _mergePlanned = false;
            _rerouteId = null;
            _error = null;
            if (_session != null)
                Render();
        }

        private SiegeOrder Plan()
        {
            var s = _session.Snapshot;
            return new SiegeOrder
            {
                SessionId = s.SessionId,
                Turn = s.Turn,
                DeployCardId = _card,
                DeployLaneId = _card.HasValue ? _catalog.Lanes[_laneIndex].Id : null,
                MergeIds = _mergePlanned ? _materialIds.ToArray() : null,
                AnchorId = _mergePlanned ? _anchorId : 0,
                RerouteUnitId = _rerouteId,
                RerouteLaneId = _rerouteId.HasValue ? _catalog.Lanes[_destinationIndex].Id : null,
            };
        }

        private void Submit()
        {
            if (_session.TryOrder(Plan(), true, out _error, out _))
                Clear();
            else
                Render();
        }

        private void Discard()
        {
            var s = _session.Snapshot;
            if (!_card.HasValue)
                return;
            var order = new SiegeOrder
            {
                SessionId = s.SessionId,
                Turn = s.Turn,
                DiscardCardId = _card,
            };
            if (_session.TryOrder(order, true, out _error, out _))
                Clear();
            else
                Render();
        }

        private void Render()
        {
            var s = _session.Snapshot;
            var a = s.AnimalState;
            _renderedTime = s.ElapsedMs;
            bool decision = s.Phase == BattlePhase.Decision && !s.Paused;
            _materialIds.RemoveAll(id => id != SiegeOrder.NewUnit && s.Units.All(x => x.Id != id));
            _status.text = _text.Format(
                "unity.status",
                s.Turn,
                s.Progress,
                _catalog.Rules.Target,
                Math.Ceiling((_catalog.Rules.DurationMs - s.ElapsedMs) / 1000.0),
                s.Energy
            );
            string kind = a.Intent.ToString().ToLowerInvariant();
            _intent.text = _text.Format(
                "unity.intent",
                _text.Get(_catalog.Conditions(a.Vigilance, a.Stamina).NameKey),
                _text.Get("unity." + kind),
                _text.Get(_catalog.Lane(a.IntentLane).NameKey),
                a.BuildsLeft,
                a.RepairsLeft
            );
            _playerBar.SetValueWithoutNotify((float)s.PlayerChargeMs / _catalog.Rules.PlayerPeriodMs);
            _animalBar.SetValueWithoutNotify((float)a.Charge);
            bool valid = _session.TryOrder(Plan(), false, out string reason, out int cost);
            _feedback.text =
                _error != null ? _text.Get(_error)
                : decision && !valid ? _text.Get(reason)
                : _text.Get("unity." + (s.Paused ? "paused" : s.Phase.ToString().ToLowerInvariant()));
            if (a.RecoveryUntilMs > s.ElapsedMs)
                _feedback.text += " · " + _text.Get("unity.recovering");
            if (a.RepairTowerId != null)
                _feedback.text += " · " + _text.Get("unity.repairing");
            _seed.text = _text.Format("unity.seed", _seedValue);
            SetLabel(_profile, _text.Format("unity.profile", _text.Get(_catalog.Profiles[_profileIndex].NameKey)));
            _submit.interactable = decision && valid;
            SetLabel(
                _submit,
                _text.Get(_card.HasValue || _mergePlanned || _rerouteId.HasValue ? "unity.submit" : "unity.skip")
            );
            _discard.interactable = decision && _card.HasValue;
            foreach (var button in new[] { _clear, _materials, _merge, _anchor, _top, _mid, _bottom })
                button.interactable = decision;
            _newMaterial.interactable = decision && _card.HasValue;
            _reroute.interactable =
                decision
                && (
                    _rerouteId.HasValue
                    || (
                        _materialIds.Count == 1
                        && s.Units.Any(x => x.Id == _materialIds[0] && x.Reroutes > 0 && x.TransitRemainingMs == 0)
                    )
                );
            _destination.interactable = decision && _rerouteId.HasValue;
            _pause.interactable = s.Phase == BattlePhase.Decision || s.Phase == BattlePhase.Running;
            SetLabel(
                _anchor,
                _anchorId == SiegeOrder.NewUnit ? _text.Get("unity.newUnit") : _text.Format("unity.anchor", _anchorId)
            );
            SetLabel(
                _destination,
                _text.Format("unity.destination", _text.Get(_catalog.Lanes[_destinationIndex].NameKey))
            );
            SetLabel(_merge, _text.Format("unity.mergeCost", _catalog.Rules.MergeCost));
            _plan.text = _text.Format(
                "unity.plan",
                CardName(s),
                _text.Get(_catalog.Lanes[_laneIndex].NameKey),
                string.Join(", ", _materialIds),
                _anchorId == -1 ? _text.Get("unity.newUnit") : _anchorId.ToString(CultureInfo.InvariantCulture),
                valid ? cost : 0
            );
            Tint(_top, _laneIndex == 0);
            Tint(_mid, _laneIndex == 1);
            Tint(_bottom, _laneIndex == 2);
            Tint(_merge, _mergePlanned);
            Tint(_reroute, _rerouteId.HasValue);
            Tint(_newMaterial, _materialIds.Contains(-1));
            RenderCells(s, decision);
            _field.Show(_catalog, s, _materialIds, _tower, decision);
            RenderDetail(s);
        }

        private string CardName(SiegeState s) =>
            _card.HasValue && s.Hand.Any(x => x.Id == _card.Value)
                ? _text.Get(_catalog.Unit(s.Hand.First(x => x.Id == _card.Value).DefinitionId).NameKey)
                : _text.Get("unity.none");

        private void RenderCells(SiegeState s, bool decision)
        {
            _handWidth = _handRoot.rect.width;
            float slotWidth = Mathf.Max(
                0,
                (_handWidth - _handLayout.spacing * (_catalog.Rules.HandSize - 1)) / _catalog.Rules.HandSize
            );
            for (int i = 0; i < s.Hand.Count; i++)
            {
                var card = s.Hand[i];
                var definition = _catalog.Unit(card.DefinitionId);
                var cell = _handPool.Get(i);
                cell.SetSlotWidth(slotWidth);
                cell.Bind(
                    card.Id,
                    _text.Format("unity.card", _text.Get(definition.NameKey), definition.Cost),
                    _card == card.Id,
                    decision,
                    SelectCard
                );
            }
            _handPool.Trim(s.Hand.Count);
            if (!_materialsPanel.activeSelf)
            {
                _rosterPool.Trim(0);
                return;
            }
            for (int i = 0; i < s.Units.Count; i++)
            {
                var unit = s.Units[i];
                var d = _catalog.Unit(unit.DefinitionId);
                _rosterPool
                    .Get(i)
                    .Bind(
                        unit.Id,
                        _text.Format(
                            "unity.unit",
                            unit.Id,
                            _text.Get(d.NameKey),
                            _text.Get(_catalog.Lane(unit.LaneId).NameKey),
                            unit.Hp,
                            d.Hp
                        ),
                        _materialIds.Contains(unit.Id),
                        decision && unit.TransitRemainingMs == 0,
                        SelectUnit
                    );
            }
            _rosterPool.Trim(s.Units.Count);
        }

        private void RenderDetail(SiegeState s)
        {
            var tower = s.Towers.FirstOrDefault(x => x.Id == _tower);
            if (tower != null)
            {
                _detail.text = _text.Format(
                    "unity.tower",
                    Math.Max(0, tower.Hp),
                    _catalog.Tower.Hp,
                    _catalog.Tower.Range
                );
                return;
            }
            var unit = s.Units.FirstOrDefault(x => _materialIds.Contains(x.Id));
            var card = s.Hand.FirstOrDefault(x => x.Id == _card);
            var d =
                unit != null ? _catalog.Unit(unit.DefinitionId)
                : card == null ? null
                : _catalog.Unit(card.DefinitionId);
            _detail.text =
                d == null
                    ? string.Empty
                    : _text.Format(
                        "unity.effects",
                        _text.Get(d.NameKey),
                        _text.Get(
                            "unity."
                                + (
                                    d.Ability == "reroute" ? "rerouteAbility"
                                    : d.Ability == "none" ? "noneAbility"
                                    : d.Ability
                                )
                        ),
                        d.Damage,
                        d.Gain
                    );
        }

        private static void SetLabel(Button button, string label) => button.GetComponentInChildren<Text>().text = label;

        private static void Tint(Button button, bool selected) =>
            button.image.color = selected ? new Color32(173, 225, 207, 255) : new Color32(242, 243, 244, 255);
    }
}
