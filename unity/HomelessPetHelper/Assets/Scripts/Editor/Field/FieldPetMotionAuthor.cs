using System;
using HomelessPetHelper.Framework.UI.Motion;
using HomelessPetHelper.Game.Field;
using HomelessPetHelper.Modules.Animals.UI;
using HomelessPetHelper.Modules.PetMotion;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static HomelessPetHelper.Editor.Field.FieldAuthorUi;

namespace HomelessPetHelper.Editor.Field
{
    /// <summary>Incrementally saves motion surfaces on the existing animal positions; never rebuilds panels.</summary>
    public static class FieldPetMotionAuthor
    {
        public static void Apply()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch Editor.");
            Add("Assets/Prefabs/Panels/Panel_Approach.prefab", new Vector3(-10000, 0, 0));
            Add("Assets/Prefabs/Panels/Panel_SiegeTrial.prefab", new Vector3(-12000, 0, 0));
        }

        private static void Add(string path, Vector3 renderPosition)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var field = root.GetComponent<OutingFieldView>();
                var animal = Ref<Image>(field, "_animal");
                if (animal.GetComponent<FieldPetMotion>() != null)
                    throw new InvalidOperationException("Already migrated; edit the saved prefab: " + path);
                var stillCoat = Ref<AnimalCoatView>(field, "_coat");
                var rect = Rect("AnimatedFieldAnimal", animal.transform);
                rect.gameObject.layer = animal.gameObject.layer;
                var surface = rect.gameObject.AddComponent<RawImage>();
                surface.raycastTarget = false;
                var coat = rect.gameObject.AddComponent<AnimalCoatView>();
                Bind(coat, "_target", surface);
                Bind(coat, "_shader", Ref<Shader>(stillCoat, "_shader"));
                var motion = animal.gameObject.AddComponent<FieldPetMotion>();
                Bind(motion, "_prefab", AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Art/PetMotion/Widget_PetMotion.prefab").GetComponent<PetMotionPlayer>());
                Bind(motion, "_still", animal); Bind(motion, "_surface", surface);
                Bind(motion, "_stillCoat", stillCoat); Bind(motion, "_motionCoat", coat);
                Bind(motion, "_clock", Ref<MotionScope>(field, "_motion"));
                var serialized = new SerializedObject(motion);
                serialized.FindProperty("_renderPosition").vector3Value = renderPosition;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Bind(field, "_petMotion", motion);
                rect.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
