using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Framework.UI
{
    /// <summary>Explicitly referenced single-line notice. Text steps down through type roles to stay on one line,
    /// then pages evenly; nothing is discarded.</summary>
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
            int oneLine = OneLineFontSize(textWidth);
            if (oneLine > 0) { _label.fontSize = oneLine; _end = _elements.Length; }
            else
            {
                _label.fontSize = _metrics.bodySize;
                float remaining = Measure(_start, _elements.Length);
                int pages = Mathf.Max(2, Mathf.CeilToInt(remaining / textWidth));
                _end = FarthestEnd(_start, Mathf.Min(textWidth, remaining / pages * 1.02f));
            }
            _label.text = Slice(_start, _end);
            var rect = (RectTransform)transform;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                Mathf.Min(_width, _label.preferredWidth + _metrics.gap * 2));
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                Mathf.Max(_metrics.captionHeight,
                    Mathf.Max(_label.preferredHeight, _label.fontSize * 1.6f) + _metrics.gap * 2));
        }

        /// <summary>Largest type role fitting the whole message on one line; 0 when the floor overflows.</summary>
        private int OneLineFontSize(float textWidth)
        {
            int body = _metrics.bodySize;
            int floor = Mathf.Clamp(_metrics.auxiliarySize, 1, body);
            int label = Mathf.Clamp(_metrics.labelSize, floor, body);
            _label.fontSize = body;
            if (Measure(0, _elements.Length) <= textWidth) return body;
            if (label != body)
            {
                _label.fontSize = label;
                if (Measure(0, _elements.Length) <= textWidth) return label;
            }
            if (floor == body) return 0;
            _label.fontSize = floor;
            return Measure(0, _elements.Length) <= textWidth ? floor : 0;
        }

        private float Measure(int start, int end)
        {
            _label.text = Slice(start, end);
            return _label.preferredWidth;
        }

        private int FarthestEnd(int start, float limit)
        {
            int low = start + 1, high = _elements.Length;
            while (low < high)
            {
                int middle = (low + high + 1) / 2;
                if (Measure(start, middle) <= limit) low = middle;
                else high = middle - 1;
            }
            return low;
        }
    }
}
