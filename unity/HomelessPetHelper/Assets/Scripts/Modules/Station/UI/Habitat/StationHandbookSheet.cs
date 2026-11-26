using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Station.UI.Habitat
{
    /// <summary>
    /// Station handbook sheet: one paper with a tab strip and one page per tab.
    /// Static prefab owns structure; this class only fills data and reuses list cells.
    /// </summary>
    public sealed class StationHandbookSheet : MonoBehaviour
    {
        [SerializeField] private Button[] _tabs;
        [SerializeField] private Image[] _tabSurfaces;
        [SerializeField] private Text[] _tabLabels;
        [SerializeField] private Sprite _idlePaper;
        [SerializeField] private Sprite _selectedPaper;
        [SerializeField] private GameObject[] _pages;
        [SerializeField] private Text _title;
        [SerializeField] private Text[] _pageTitles;
        [SerializeField] private Text _status;
        [SerializeField] private Text _aid;
        [SerializeField] private RectTransform _offerList;
        [SerializeField] private StationHandbookOfferView _offerTemplate;
        [SerializeField] private Text _offerEmpty;
        [SerializeField] private Sprite[] _offerIcons;
        [SerializeField] private RectTransform _memoryGrid;
        [SerializeField] private GameObject _memoryScroll;
        [SerializeField] private StationHandbookMemoryView _memoryTemplate;
        [SerializeField] private Text _memoryEmpty;
        [SerializeField] private Toggle _pause;
        [SerializeField] private Text _pauseLabel;
        [SerializeField] private GameObject _pauseIcon;
        [SerializeField] private GameObject _continueIcon;
        [SerializeField] private string _pauseText = "暂停";
        [SerializeField] private string _continueText = "继续";
        [SerializeField] private Color _idleText = new Color32(42, 61, 48, 255);
        [SerializeField] private Color _selectedText = new Color32(42, 61, 48, 255);
        private readonly List<StationHandbookOfferView> _offers = new List<StationHandbookOfferView>();
        private readonly List<StationHandbookMemoryView> _memories = new List<StationHandbookMemoryView>();
        private int _current = -1;

        public int CurrentPage => _current;

        private void Awake()
        {
            if (_pause != null)
            {
                _pause.onValueChanged.AddListener(HandlePauseChanged);
                RefreshPauseState();
            }
            if (_tabs == null) return;
            for (int index = 0; index < _tabs.Length; index++)
            {
                if (_tabs[index] == null) continue;
                int page = index;
                _tabs[index].onClick.AddListener(() => Show(page));
            }
        }

        private void OnDestroy()
        {
            if (_pause != null) _pause.onValueChanged.RemoveListener(HandlePauseChanged);
            if (_tabs == null) return;
            foreach (var tab in _tabs)
                if (tab != null) tab.onClick.RemoveAllListeners();
        }

        private void HandlePauseChanged(bool paused) => RefreshPauseState();

        private void RefreshPauseState()
        {
            bool paused = _pause != null && _pause.isOn;
            if (_pauseLabel != null) _pauseLabel.text = paused ? _continueText : _pauseText;
            if (_pauseIcon != null) _pauseIcon.SetActive(!paused);
            if (_continueIcon != null) _continueIcon.SetActive(paused);
        }

        public void Open() => Show(0);

        public void Show(int index)
        {
            if (_pages == null || _pages.Length == 0) return;
            _current = Mathf.Clamp(index, 0, _pages.Length - 1);
            for (int page = 0; page < _pages.Length; page++)
                if (_pages[page] != null) _pages[page].SetActive(page == _current);
            if (_tabs == null) return;
            for (int tab = 0; tab < _tabs.Length; tab++)
            {
                bool selected = tab == _current;
                if (tab < _tabSurfaces.Length && _tabSurfaces[tab] != null)
                    _tabSurfaces[tab].sprite = selected ? _selectedPaper : _idlePaper;
                if (tab < _tabLabels.Length && _tabLabels[tab] != null)
                    _tabLabels[tab].color = selected ? _selectedText : _idleText;
            }
        }

        public void SetLabels(string title, IReadOnlyList<string> tabs, IReadOnlyList<string> pages)
        {
            if (_title != null) _title.text = title;
            if (tabs != null && _tabLabels != null)
                for (int index = 0; index < _tabLabels.Length && index < tabs.Count; index++)
                    if (_tabLabels[index] != null) _tabLabels[index].text = tabs[index];
            if (pages != null && _pageTitles != null)
                for (int index = 0; index < _pageTitles.Length && index < pages.Count; index++)
                    if (_pageTitles[index] != null) _pageTitles[index].text = pages[index];
        }

        public void SetStatus(string status)
        {
            if (_status != null) _status.text = status;
        }

        public void SetOffers(string aid, IReadOnlyList<HandbookOffer> offers, Action<int> open)
        {
            if (_aid != null) _aid.text = aid;
            int count = offers?.Count ?? 0;
            bool empty = count == 0;
            if (_offerEmpty != null) _offerEmpty.gameObject.SetActive(empty);
            if (_offerTemplate == null || _offerList == null) return;
            while (_offers.Count < count)
            {
                StationHandbookOfferView cell = Instantiate(_offerTemplate, _offerList);
                cell.gameObject.name = HandbookCellNames.Offer + _offers.Count;
                _offers.Add(cell);
            }
            for (int index = 0; index < _offers.Count; index++)
            {
                bool used = index < count;
                _offers[index].gameObject.SetActive(used);
                if (!used) continue;
                int cell = index;
                Sprite icon = _offerIcons != null && _offerIcons.Length > 0
                    ? _offerIcons[index % _offerIcons.Length] : null;
                _offers[index].Bind(offers[index], icon, open == null ? null : (Action)(() => open(cell)));
            }
        }

        public void SetMemories(IReadOnlyList<HandbookMemory> memories, Action<int> open)
        {
            int count = memories?.Count ?? 0;
            bool empty = count == 0;
            if (_memoryEmpty != null) _memoryEmpty.gameObject.SetActive(empty);
            if (_memoryScroll != null) _memoryScroll.SetActive(!empty);
            if (_memoryTemplate == null || _memoryGrid == null) return;
            while (_memories.Count < count)
            {
                StationHandbookMemoryView cell = Instantiate(_memoryTemplate, _memoryGrid);
                cell.gameObject.name = HandbookCellNames.Memory + _memories.Count;
                _memories.Add(cell);
            }
            for (int index = 0; index < _memories.Count; index++)
            {
                bool used = index < count;
                _memories[index].gameObject.SetActive(used);
                if (!used) continue;
                int cell = index;
                _memories[index].Bind(memories[index], open == null ? null : (Action)(() => open(cell)));
            }
        }
    }

    /// <summary>Stable cell names so PlayMode tests and save screenshots can address reused cells.</summary>
    public static class HandbookCellNames
    {
        public const string Offer = "EconomyOffer";
        public const string Memory = "MemorialCard";
    }
}
