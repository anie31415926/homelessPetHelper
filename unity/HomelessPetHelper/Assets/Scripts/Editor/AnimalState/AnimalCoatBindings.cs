using System;
using System.Linq;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Field;
using HomelessPetHelper.Modules.Animals.UI;
using HomelessPetHelper.Modules.Siege.UI;
using HomelessPetHelper.Modules.Station.UI;
using HomelessPetHelper.Modules.Station.UI.Habitat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Editor.AnimalState
{
    /// <summary>Incremental binding only. Run in a disposable saved-project copy.</summary>
    public static class AnimalCoatBindings
    {
        private const string ShaderPath = "Assets/Scripts/Modules/Animals/UI/AnimalCoat.shader";
        public static void Apply()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch copy.");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null) throw new InvalidOperationException("Coat shader is missing.");
            Edit("Assets/Prefabs/Widgets/Widget_StationHabitatResident.prefab", shader);
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Panels" }))
                Edit(AssetDatabase.GUIDToAssetPath(guid), shader);
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
            foreach (var motion in scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<SiegePetMotion>(true)))
            {
                if (BindGraphic(motion, motion.GetComponentsInChildren<RawImage>(true)
                    .Single(image => image.name == "AnimatedAnimal"), "_coat", shader))
                    EditorSceneManager.MarkSceneDirty(scene);
            }
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static void Edit(string path, Shader shader)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;
            try
            {
                foreach (var view in root.GetComponentsInChildren<OutingFieldView>(true))
                    changed |= Bind(view, "_animal", "_coat", shader);
                foreach (var view in root.GetComponentsInChildren<SiegeBattlefield>(true))
                    changed |= Bind(view, "_cat", "_coat", shader);
                foreach (var view in root.GetComponentsInChildren<SiegePetMotion>(true))
                    changed |= BindGraphic(view, view.GetComponentsInChildren<RawImage>(true)
                        .Single(image => image.name == "AnimatedAnimal"), "_coat", shader);
                foreach (var view in root.GetComponentsInChildren<StationPanelView>(true))
                    changed |= Bind(view, "_summaryAnimal", "_summaryCoat", shader);
                foreach (var view in root.GetComponentsInChildren<StationResidentView>(true))
                {
                    changed |= Bind(view, "_stillImage", "_stillCoat", shader);
                    changed |= Bind(view, "_motionImage", "_motionCoat", shader);
                }
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static bool Bind(Component owner, string graphicField, string coatField, Shader shader)
        {
            var data = new SerializedObject(owner);
            var graphic = data.FindProperty(graphicField).objectReferenceValue as Graphic;
            if (graphic == null) throw new InvalidOperationException("Missing graphic: " + graphicField);
            return BindGraphic(owner, graphic, coatField, shader);
        }

        private static bool BindGraphic(Component owner, Graphic graphic, string coatField, Shader shader)
        {
            var data = new SerializedObject(owner);
            if (data.FindProperty(coatField).objectReferenceValue != null) return false;
            var coat = graphic.gameObject.GetComponent<AnimalCoatView>();
            if (coat == null) coat = graphic.gameObject.AddComponent<AnimalCoatView>();
            var binding = new SerializedObject(coat);
            binding.FindProperty("_target").objectReferenceValue = graphic;
            binding.FindProperty("_shader").objectReferenceValue = shader;
            binding.ApplyModifiedPropertiesWithoutUndo();
            data.FindProperty(coatField).objectReferenceValue = coat;
            data.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
    }
}
