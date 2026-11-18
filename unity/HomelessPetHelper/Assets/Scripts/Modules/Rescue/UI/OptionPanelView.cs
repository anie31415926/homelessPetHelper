using System;
using System.Collections.Generic;
using HomelessPetHelper.Modules.Rescue.Options;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Rescue.UI
{
    /// <summary>
    /// Presentation/input only. The owner supplies localized content and subscribes before Open.
    /// Only cell instances are pooled; the panel hierarchy and all references are saved in a Prefab.
    /// </summary>
    public sealed class OptionPanelView : MonoBehaviour
    {
        [SerializeField] private Text _turn;
        [SerializeField] private Text _retained;
        [SerializeField] private Text _feedback;
        [SerializeField] private Button _use;
        [SerializeField] private Button _restart;
        [SerializeField] private Toggle _pause;
        [SerializeField] private ScrollRect _scroll;
        [SerializeField] private RectTransform _content;
        [SerializeField] private OptionCardView _cardPrefab;
        [SerializeField] private bool _holdToConfirm;

        private readonly List<OptionCardView> _cards = new List<OptionCardView>();
        private readonly List<long> _selectedIds = new List<long>();
        private readonly HashSet<long> _unavailableIds = new HashSet<long>();
        private ObjectPool<OptionCardView> _pool;
        private IReadOnlyList<RescueOption> _options = Array.Empty<RescueOption>();
        private int _selectionLimit;
        private int _lockLimit;
        private bool _interactable;
        private bool _isOpen;

        public event Action UseRequested;
        public event Action RestartRequested;
        public event Action<bool> PauseRequested;
        public event Action<long, bool> RetentionRequested;

        public IReadOnlyList<long> SelectedIds => _selectedIds.AsReadOnly();
        public bool HoldToConfirm => _holdToConfirm;

        public void Open(int selectionLimit, int lockLimit)
        {
            if (selectionLimit <= 0 || lockLimit < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(selectionLimit));
            }

            Close();
            _selectionLimit = selectionLimit;
            _lockLimit = lockLimit;
            _pool = _pool ?? new ObjectPool<OptionCardView>(CreateCard, null, ReleaseCard, DestroyCard,
                true, 3, 12);
            _isOpen = true;
            if (!_holdToConfirm) _use.onClick.AddListener(OnUseRequested);
            _use.gameObject.SetActive(!_holdToConfirm);
            _restart.onClick.AddListener(OnRestartRequested);
            _pause.onValueChanged.AddListener(OnPauseRequested);
            gameObject.SetActive(true);
        }

        public void Refresh(IReadOnlyList<RescueOption> options, IReadOnlyList<string> titles, Font font,
            IReadOnlyList<string> unavailableReasons = null)
        {
            if (!_isOpen || options == null || titles == null || options.Count != titles.Count || font == null ||
                (unavailableReasons != null && unavailableReasons.Count != options.Count))
            {
                throw new InvalidOperationException("Open the view and supply matching options, labels and a font.");
            }

            bool sameInstances = HaveSameInstances(options);
            float scrollPosition = _scroll.verticalNormalizedPosition;
            ReleaseCards();
            _options = options;
            _unavailableIds.Clear();
            var visibleIds = new HashSet<long>();
            for (int index = 0; index < options.Count; index++)
            {
                visibleIds.Add(options[index].InstanceId);
                if (unavailableReasons != null && !string.IsNullOrEmpty(unavailableReasons[index]))
                {
                    _unavailableIds.Add(options[index].InstanceId);
                }
            }

            _selectedIds.RemoveAll(id => !visibleIds.Contains(id) || _unavailableIds.Contains(id));
            for (int index = 0; index < options.Count; index++)
            {
                OptionCardView card = _pool.Get();
                card.transform.SetAsLastSibling();
                card.gameObject.SetActive(true);
                string identity = _unavailableIds.Contains(options[index].InstanceId) ? unavailableReasons[index] :
                    "#" + options[index].InstanceId;
                card.Bind(options[index].InstanceId, titles[index], identity, font);
                card.SelectionChanged += OnSelectionChanged;
                card.RetentionChanged += OnRetentionRequested;
                if (_holdToConfirm) card.HoldConfirmed += OnHoldConfirmed;
                _cards.Add(card);
            }

            _scroll.verticalNormalizedPosition = sameInstances ? scrollPosition : 1f;
            RefreshControls();
        }

        public void SetStatus(string turn, string retained, string feedback, bool suspended, bool choicesEnabled = true)
        {
            _turn.text = turn;
            _retained.text = retained;
            _feedback.text = feedback;
            _pause.SetIsOnWithoutNotify(suspended);
            _interactable = !suspended && choicesEnabled;
            RefreshControls();
        }

        public void Close()
        {
            _isOpen = false;
            _use.onClick.RemoveListener(OnUseRequested);
            _restart.onClick.RemoveListener(OnRestartRequested);
            _pause.onValueChanged.RemoveListener(OnPauseRequested);
            ReleaseCards();
            _options = Array.Empty<RescueOption>();
            _selectedIds.Clear();
            _unavailableIds.Clear();
            _feedback.text = string.Empty;
            _pause.SetIsOnWithoutNotify(false);
            _use.interactable = false;
            _scroll.verticalNormalizedPosition = 1f;
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            Close();
        }

        private void OnDestroy()
        {
            _pool?.Clear();
        }

        private OptionCardView CreateCard()
        {
            return Instantiate(_cardPrefab, _content);
        }

        private static void ReleaseCard(OptionCardView card)
        {
            card.Unbind();
            card.gameObject.SetActive(false);
        }

        private static void DestroyCard(OptionCardView card)
        {
            Destroy(card.gameObject);
        }

        private void ReleaseCards()
        {
            foreach (OptionCardView card in _cards)
            {
                _pool.Release(card);
            }

            _cards.Clear();
        }

        private void OnSelectionChanged(long id, bool selected)
        {
            if (_holdToConfirm || !_interactable || _unavailableIds.Contains(id))
            {
                RefreshControls();
                return;
            }

            _selectedIds.Remove(id);
            if (selected)
            {
                if (_selectedIds.Count == _selectionLimit)
                {
                    _selectedIds.RemoveAt(0);
                }

                _selectedIds.Add(id);
            }

            RefreshControls();
        }

        private void RefreshControls()
        {
            int lockedCount = 0;
            foreach (RescueOption option in _options)
            {
                lockedCount += option.IsLocked ? 1 : 0;
            }

            for (int index = 0; index < _cards.Count; index++)
            {
                RescueOption option = _options[index];
                _cards[index].Refresh(_selectedIds.Contains(option.InstanceId), option.IsLocked,
                    lockedCount < _lockLimit, _interactable, !_unavailableIds.Contains(option.InstanceId));
            }

            _use.interactable = !_holdToConfirm && _isOpen && _interactable && _selectedIds.Count > 0;
        }

        private void OnHoldConfirmed(long id)
        {
            if (!_holdToConfirm || !_isOpen || !_interactable || _unavailableIds.Contains(id)) return;
            bool offered = false;
            foreach (RescueOption option in _options) offered |= option.InstanceId == id;
            if (!offered) return;
            _selectedIds.Clear();
            _selectedIds.Add(id);
            _interactable = false;
            RefreshControls();
            UseRequested?.Invoke();
        }

        private bool HaveSameInstances(IReadOnlyList<RescueOption> options)
        {
            if (options.Count != _options.Count)
            {
                return false;
            }

            for (int index = 0; index < options.Count; index++)
            {
                if (options[index].InstanceId != _options[index].InstanceId)
                {
                    return false;
                }
            }

            return true;
        }

        private void OnUseRequested() => UseRequested?.Invoke();
        private void OnRestartRequested() => RestartRequested?.Invoke();
        private void OnPauseRequested(bool value) => PauseRequested?.Invoke(value);
        private void OnRetentionRequested(long id, bool value) => RetentionRequested?.Invoke(id, value);
    }
}
