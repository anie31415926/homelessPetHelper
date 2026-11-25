using System;
using System.Collections;
using UnityEngine;

namespace HomelessPetHelper.Game.Loading
{
    [DisallowMultipleComponent]
    public sealed class LoadingSceneRoot : MonoBehaviour
    {
        public const string ScenePath = "Assets/Scenes/SC_Loading.unity";
        public const string MainScenePath = "Assets/Scenes/Main.unity";
        private const float EntryFadeSeconds = .22f;
        [SerializeField] private LoadingView _view;
        [SerializeField] private string _destination = MainScenePath;
        [SerializeField, Min(1f)] private float _timeoutSeconds = 60f;
        [SerializeField] private bool _previewOnly;
        private ILoadingTask _preparation;
        private UnitySceneLoadingTask _scene;
        private Coroutine _routine;
        private bool _started, _cancelled;
        private bool _entryRequested;
        public bool HasFailed { get; private set; }

        private void Start()
        {
            if (_cancelled) return;
            if (_view == null) throw new InvalidOperationException("Missing Loading view binding.");
            _started = true;
            _view.SetProgress(0f);
            if (_previewOnly) return;
            _view.ShowCover();
            _view.EnterRequested += RequestEntry;
        }

        public void ConfigurePreview()
        {
            EnsureConfigurable();
            if (_preparation != null) throw new InvalidOperationException("A preparation task is already owned.");
            _previewOnly = true;
        }

        // Ownership transfers only on success. Inject real resource preparation before Start, not into the UI.
        public void ConfigurePreparation(ILoadingTask preparation)
        {
            if (preparation == null) throw new ArgumentNullException(nameof(preparation));
            EnsureConfigurable();
            if (_previewOnly || _preparation != null)
                throw new InvalidOperationException("Loading is already configured.");
            _preparation = preparation;
        }

        private void EnsureConfigurable()
        {
            if (_started || _cancelled) throw new InvalidOperationException("Loading has already started or stopped.");
        }

        private IEnumerator LoadRoutine()
        {
            yield return FadeCover();
            _view.ShowLoading();
            // Paint the lightweight scene before starting any potentially expensive preparation.
            yield return null;
            if (!BeginPreparation()) yield break;
            float sceneStart = _preparation == null ? 0f : .9f;
            if (_preparation != null) yield return RunTaskRoutine(_preparation, 0f, sceneStart);
            if (HasFailed || _cancelled || !BeginScene()) yield break;
            yield return RunTaskRoutine(_scene, sceneStart, .99f);
            if (HasFailed || _cancelled) yield break;
            _view.SetProgress(1f);
            yield return ActivateScene();
            _routine = null;
        }

        private void RequestEntry()
        {
            if (_entryRequested || _cancelled) return;
            _entryRequested = true;
            _view.HideEntry();
            _routine = StartCoroutine(LoadRoutine());
        }

        private IEnumerator FadeCover()
        {
            float elapsed = 0f;
            while (elapsed < EntryFadeSeconds)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
                _view.SetOpacity(1f - Mathf.Clamp01(elapsed / EntryFadeSeconds));
            }
        }

        private IEnumerator ActivateScene()
        {
            _scene.Activate();
            float waited = 0f;
            while (!_scene.HasActivated)
            {
                waited += Time.unscaledDeltaTime;
                if (waited >= _timeoutSeconds)
                {
                    Fail(new TimeoutException("Loading scene activation timed out."));
                    yield break;
                }
                yield return null;
            }
        }

        private bool BeginPreparation()
        {
            try
            {
                if (float.IsNaN(_timeoutSeconds) || float.IsInfinity(_timeoutSeconds) || _timeoutSeconds <= 0f)
                    throw new InvalidOperationException("Invalid Loading timeout.");
                // Injected preparation may first load a bundle containing the destination scene.
                if (_preparation == null) UnitySceneLoadingTask.ValidateDestination(_destination);
                return true;
            }
            catch (Exception exception) { Fail(exception); return false; }
        }

        private bool BeginScene()
        {
            try
            {
                ReleasePreparation();
                if (_cancelled) return false;
                _scene = new UnitySceneLoadingTask(_destination);
                return true;
            }
            catch (Exception exception) { Fail(exception); return false; }
        }

        private IEnumerator RunTaskRoutine(ILoadingTask task, float from, float to)
        {
            float waited = 0f;
            bool complete = false;
            while (!complete)
            {
                if (!AdvanceTask(task, from, to, ref waited, out complete)) yield break;
                yield return null;
            }
        }

        private bool AdvanceTask(ILoadingTask task, float from, float to, ref float waited, out bool complete)
        {
            complete = false;
            try
            {
                float delta = Time.unscaledDeltaTime;
                task.Tick(delta);
                if (_cancelled) return false;
                complete = task.IsComplete;
                float progress = task.Progress;
                if (float.IsNaN(progress) || float.IsInfinity(progress))
                    throw new InvalidOperationException("Invalid Loading task progress.");
                waited += delta;
                if (!complete && waited >= _timeoutSeconds)
                    throw new TimeoutException("Loading task timed out.");
                float value = Mathf.Lerp(from, to, complete ? 1f : Mathf.Clamp(progress, 0f, .999f));
                _view.SetProgress(Mathf.Max(_view.Progress, value));
                return true;
            }
            catch (Exception exception) { Fail(exception); return false; }
        }

        private void Fail(Exception exception)
        {
            HasFailed = true;
            _view.ShowFailure();
            Debug.LogException(exception, this);
            ReleaseTasks();
            _routine = null;
        }

        private void OnDisable()
        {
            if (_view != null && _started && !_previewOnly)
            {
                _view.EnterRequested -= RequestEntry;
                _view.HideEntry();
            }
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            _cancelled = true;
            ReleaseTasks();
        }

        private void ReleasePreparation()
        {
            ILoadingTask preparation = _preparation;
            _preparation = null;
            preparation?.Dispose();
        }

        private void OnDestroy() => ReleaseTasks();

        private void ReleaseTasks()
        {
            try { ReleasePreparation(); }
            catch (Exception exception) { Debug.LogException(exception, this); }
            _scene?.Dispose();
            _scene = null;
        }
    }
}
