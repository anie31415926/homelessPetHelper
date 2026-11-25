using System;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Game.Loading
{
    [DisallowMultipleComponent]
    public sealed class LoadingView : MonoBehaviour
    {
        [SerializeField] private Image _fill;
        [SerializeField] private UnityEngine.UI.Text _label;
        [SerializeField] private UnityEngine.UI.Text _percent;
        [SerializeField] private TextAsset _text;
        [SerializeField] private GameObject _cover;
        [SerializeField] private GameObject _previewArtwork;
        [SerializeField] private GameObject _progress;
        [SerializeField] private Button _enter;
        [SerializeField] private UnityEngine.UI.Text _enterLabel;
        [SerializeField] private CanvasGroup _canvasGroup;
        private LoadingText _strings;
        private int _lastPercent = -1;
        public float Progress { get; private set; }
        public event Action EnterRequested;

        private void OnEnable() => _enter.onClick.AddListener(OnEnterClicked);
        private void OnDisable() => _enter.onClick.RemoveListener(OnEnterClicked);

        private void OnEnterClicked() => EnterRequested?.Invoke();

        public void ShowCover()
        {
            EnsureText();
            _previewArtwork.SetActive(false);
            _progress.SetActive(false);
            _cover.SetActive(true);
            _enterLabel.text = _strings.enter;
            _enter.gameObject.SetActive(true);
        }

        public void ShowLoading()
        {
            HideEntry();
            _cover.SetActive(false);
            _previewArtwork.SetActive(true);
            _progress.SetActive(true);
            SetOpacity(1f);
        }

        public void HideEntry() => _enter.gameObject.SetActive(false);

        public void SetOpacity(float opacity) => _canvasGroup.alpha = opacity;

        public void SetProgress(float progress)
        {
            EnsureText();
            Progress = Mathf.Clamp01(progress);
            _fill.fillAmount = Progress;
            int percent = Mathf.FloorToInt(Progress * 100f);
            if (percent != _lastPercent)
            {
                _lastPercent = percent;
                _percent.text = percent.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";
            }
            _label.text = _strings.loading;
        }

        public void ShowFailure()
        {
            EnsureText();
            HideEntry();
            SetOpacity(1f);
            _progress.SetActive(true);
            _label.text = _strings.failed;
            _percent.text = string.Empty;
            _lastPercent = -1;
        }

        private void EnsureText()
        {
            if (_strings != null) return;
            if (_fill == null || _label == null || _percent == null || _text == null)
                throw new InvalidOperationException("Incomplete Loading view bindings.");
            _strings = JsonUtility.FromJson<LoadingText>(_text.text);
            if (_strings == null || string.IsNullOrWhiteSpace(_strings.loading) ||
                string.IsNullOrWhiteSpace(_strings.failed) || string.IsNullOrWhiteSpace(_strings.enter))
                throw new InvalidOperationException("Invalid Loading localization source.");
        }

        [Serializable]
        private sealed class LoadingText
        {
            public string loading;
            public string failed;
            public string enter;
        }
    }
}
