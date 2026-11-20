using System.Linq;
using HomelessPetHelper.Framework.Audio;
using HomelessPetHelper.Framework.Audio.UI;
using UnityEditor;
using UnityEngine;

namespace HomelessPetHelper.Editor.Audio
{
    [CustomEditor(typeof(ButtonAudio)), CanEditMultipleObjects]
    public sealed class ButtonAudioInspector : UnityEditor.Editor
    {
        private string[] _ids;
        private string[] _labels;

        private void OnEnable()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AudioCatalog>(BasicAudioAssets.CatalogPath);
            _ids = catalog == null ? new string[0] : catalog.CreateCues().Select(cue => cue.Id).ToArray();
            _labels = _ids.Concat(new[] { "Custom cue..." }).ToArray();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_silent"));
            var cue = serializedObject.FindProperty("_cueId");
            int index = System.Array.IndexOf(_ids, cue.stringValue);
            if (index < 0) index = _ids.Length;
            EditorGUI.showMixedValue = cue.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            int selected = EditorGUILayout.Popup("Cue", index, _labels);
            if (EditorGUI.EndChangeCheck())
                cue.stringValue = selected < _ids.Length ? _ids[selected] : string.Empty;
            EditorGUI.showMixedValue = false;
            if (selected == _ids.Length) EditorGUILayout.PropertyField(cue);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_scope"),
                new GUIContent("Scope Override", "Optional. Otherwise use the nearest bound parent AudioScope."));
            serializedObject.ApplyModifiedProperties();
        }
    }
}
