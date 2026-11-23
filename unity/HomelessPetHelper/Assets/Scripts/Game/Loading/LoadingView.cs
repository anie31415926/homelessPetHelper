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
        private LoadingText _strings;
        private int _lastPercent = -1;
        public float Progress { get; private set; }

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
                string.IsNullOrWhiteSpace(_strings.failed))
                throw new InvalidOperationException("Invalid Loading localization source.");
        }

        [Serializable]
        private sealed class LoadingText
        {
            public string loading;
            public string failed;
        }
    }
}
