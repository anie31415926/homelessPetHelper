using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Field;
using UnityEditor;
using UnityEngine;

namespace HomelessPetHelper.Editor.Field
{
    public sealed class OutingWeatherPreview : EditorWindow
    {
        private OutingWeatherKind _kind = OutingWeatherKind.Rain;
        private OutingWeatherIntensity _intensity = OutingWeatherIntensity.Medium;
        private string _status;

        [MenuItem("Homeless Pet Helper/Preview/Outing Weather")]
        private static void Open() => GetWindow<OutingWeatherPreview>("Outing Weather");

        private void OnGUI()
        {
            _kind = (OutingWeatherKind)EditorGUILayout.EnumPopup("Weather", _kind);
            using (new EditorGUI.DisabledScope(_kind == OutingWeatherKind.Clear))
                _intensity = (OutingWeatherIntensity)EditorGUILayout.EnumPopup("Intensity", _intensity);
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            {
                if (GUILayout.Button("Set World Weather")) Apply();
                if (GUILayout.Button("Resume Schedule")) Resume();
            }
            if (!string.IsNullOrEmpty(_status)) EditorGUILayout.HelpBox(_status, MessageType.Info);
        }

        private MainLoopPresenter Presenter()
        {
            var main = FindObjectOfType<MainGameRoot>();
            if (main != null && main.Presenter != null) return main.Presenter;
            _status = "Start Main in Play Mode first.";
            return null;
        }

        private void Apply()
        {
            var presenter = Presenter();
            if (presenter == null) return;
            presenter.SetWorldWeather(new OutingWeather(_kind, _intensity));
            _status = "Debug override set. An outing in progress keeps the weather it started with.";
        }

        private void Resume()
        {
            var presenter = Presenter();
            if (presenter == null) return;
            presenter.ResampleWorldWeather();
            _status = "Weather follows the world clock again.";
        }
    }
}
