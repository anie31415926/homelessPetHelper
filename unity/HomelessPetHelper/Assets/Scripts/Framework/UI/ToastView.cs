using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Framework.UI
{
    /// <summary>Explicitly referenced single-line notice. Long text is paged, never shrunk or discarded.</summary>
    public sealed class ToastView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Text _label;
        [SerializeField] private RectTransform _bounds;
        [SerializeField] private UiLayoutMetrics _metrics;
        [SerializeField, Min(.01f)] private float _fadeInSeconds = .18f;
        [SerializeField, Min(.01f)] private float _fadeOutSeconds = .22f;
        private string _message;
        private int[] _elements;
        private int _start, _end;
        private float _elapsed, _holdSeconds, _width = -1, _pixelScale = -1;
        private bool _suspended;
        public bool IsShowing => !string.IsNullOrEmpty(_message);
        public string CurrentLine => _label.text;
        public float Opacity => _group.alpha;
        public float Height => ((RectTransform)transform).rect.height;

        public void BindBounds(RectTransform bounds)
        {
            if (bounds == null) throw new ArgumentNullException(nameof(bounds));
            _bounds = bounds; _width = -1;
        }

        /// <summary>Replaces the notice. Repeated text restarts its lifetime; empty text dismisses it.</summary>
        public void Show(string message, float holdSeconds = 2.4f)
        {
            if (float.IsNaN(holdSeconds) || float.IsInfinity(holdSeconds) || holdSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(holdSeconds));
            Clear();
            if (string.IsNullOrWhiteSpace(message) || !isActiveAndEnabled) return;
            if (_bounds == null) throw new InvalidOperationException("Toast needs explicit safe-area bounds.");
            _message = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
            _elements = StringInfo.ParseCombiningCharacters(_message);
            _holdSeconds = holdSeconds;
            FitLine();
        }

        public void Clear()
        {
            _message = null; _elements = null; _start = _end = 0; _elapsed = 0; _width = -1;
            _group.alpha = 0; _group.blocksRaycasts = _group.interactable = false;
            _label.text = string.Empty;
        }

        public void SetSuspended(bool suspended) => _suspended = suspended;

        public void SetBottomInset(float inset)
        {
            var rect = (RectTransform)transform;
            float bottom = Mathf.Clamp(inset, _metrics.margin,
                Mathf.Max(_metrics.margin, _bounds.rect.height - Height - _metrics.margin));
            rect.anchoredPosition = new Vector2(0, bottom);
        }

        private void OnEnable()
        { _suspended = false; Clear(); Canvas.willRenderCanvases += RefreshLayout; }
        private void OnDisable()
        { Canvas.willRenderCanvases -= RefreshLayout; Clear(); }
        private void OnApplicationFocus(bool focused) { if (!focused) Clear(); }
        private void OnApplicationPause(bool paused) { if (paused) Clear(); }

        private void Update()
        {
            if (!IsShowing || _suspended) return;
            RefreshLayout();
            _elapsed += Time.unscaledDeltaTime;
            float holdEnd = _fadeInSeconds + Mathf.Max(_holdSeconds, (_end - _start) * .14f);
            _group.alpha = _elapsed < _fadeInSeconds ? _elapsed / _fadeInSeconds :
                _elapsed <= holdEnd ? 1 : Mathf.Clamp01(1 - (_elapsed - holdEnd) / _fadeOutSeconds);
            if (_elapsed < holdEnd + _fadeOutSeconds) return;
            if (_end >= _elements.Length) { Clear(); return; }
            _start = _end; _elapsed = 0; FitLine();
        }

        private void RefreshLayout()
        {
            if (!IsShowing) return;
            float available = _bounds.rect.width - _metrics.margin * 2;
            if (!Mathf.Approximately(_width, available) ||
                !Mathf.Approximately(_pixelScale, _label.pixelsPerUnit)) FitLine();
        }

        private string Slice(int start, int end)
        {
            int from = _elements[start], to = end == _elements.Length ? _message.Length : _elements[end];
            return _message.Substring(from, to - from);
        }

        private void FitLine()
        {
            _width = _bounds.rect.width - _metrics.margin * 2;
            _pixelScale = _label.pixelsPerUnit;
            float textWidth = Mathf.Max(_metrics.bodySize, _width - _metrics.gap * 2);
            int low = _start + 1, high = _elements.Length;
            while (low < high)
            {
                int middle = (low + high + 1) / 2;
                _label.text = Slice(_start, middle);
                if (_label.preferredWidth <= textWidth) low = middle;
                else high = middle - 1;
            }
            _end = low; _label.text = Slice(_start, _end);
            var rect = (RectTransform)transform;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                Mathf.Min(_width, _label.preferredWidth + _metrics.gap * 2));
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                Mathf.Max(_metrics.captionHeight,
                    Mathf.Max(_label.preferredHeight, _metrics.bodySize * 1.6f) + _metrics.gap * 2));
        }
    }
}
