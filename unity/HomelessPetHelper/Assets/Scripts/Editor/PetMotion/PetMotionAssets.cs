using System;
using System.Collections.Generic;
using System.IO;
using HomelessPetHelper.Modules.PetMotion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HomelessPetHelper.Editor.PetMotion
{
    public static class PetMotionAssets
    {
        public const string Folder = "Assets/EditorScenes/PetMotion";
        public const string ScenePath = Folder + "/SC_PetMotion.unity";
        public const string PanelPath = "Assets/Prefabs/Panels/Panel_PetMotion.prefab";
        public const string RigPath = Folder + "/Widget_PetMotion.prefab";
        private static readonly List<Transform> Bones = new List<Transform>();

        [MenuItem("Homeless Pet Helper/Preview/Open Animal Motion")]
        public static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        public static void CreateAssets()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Use isolated PetMotionAssets task.");
            foreach (string path in new[] { ScenePath, RigPath, PanelPath })
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                    throw new InvalidOperationException(
                        "Creation refuses overwrites; edit saved Prefabs in Prefab Mode.");
            string source = Path.Combine(Application.dataPath, "../PetMotionSource/unity-export.json");
            var data = JsonUtility.FromJson<PetMotionBakeData>(File.ReadAllText(source));
            if (data.schema != 1 || data.rigs.Length != 7 || data.takes.Length != 6)
                throw new InvalidDataException("Invalid pet motion export.");
            ImportTextures();
            CreateRig(data);
            PetMotionPanelAuthor.CreatePanel();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath));
            var camera = new GameObject("MainCamera", typeof(Camera)).GetComponent<Camera>();
            camera.cullingMask = 0;
            camera.backgroundColor = Color.white;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.transform.position = new Vector3(0, 0, -10);
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            PetMotionAudit.WriteReport();
        }

        public static void UpdateClips()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Use isolated PetMotionBake task.");
            var data = JsonUtility.FromJson<PetMotionBakeData>(File.ReadAllText(
                Path.Combine(Application.dataPath, "../PetMotionSource/unity-export.json")));
            foreach (var take in data.takes)
                CreateClip(take);
            PetMotionPanelAuthor.RefineSavedPanel();
            AssetDatabase.SaveAssets();
        }

        private static void ImportTextures()
        {
            foreach (string file in Directory.GetFiles(Folder, "T_*.png"))
            {
                string path = file.Replace('\\', '/');
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 512;
                importer.mipmapEnabled = false;
                importer.isReadable = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        private static void CreateRig(PetMotionBakeData data)
        {
            Bones.Clear();
            var root = new GameObject("Widget_PetMotion");
            root.SetActive(false);
            var player = root.AddComponent<PetMotionPlayer>();
            var rig = Child("Rig", root.transform);
            var stand = Child("Stand", rig.transform);
            GameObject sit = null, side = null;
            for (int i = 0; i < data.rigs.Length; i++)
            {
                var definition = data.rigs[i];
                var node = Child(definition.id, i < 5 ? stand.transform : rig.transform);
                if (i == 5) sit = node;
                if (i == 6) side = node;
                CreateSkin(node, definition, i);
            }
            var clips = new AnimationClip[data.takes.Length];
            for (int i = 0; i < clips.Length; i++)
                clips[i] = CreateClip(data.takes[i]);
            var sources = new GameObject[3];
            string[] textureNames = { "original", "sit-rest", "side-rest" };
            for (int i = 0; i < sources.Length; i++)
            {
                var node = Child("Source" + i, root.transform);
                var filter = node.AddComponent<MeshFilter>();
                filter.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/Mesh_body.asset");
                var renderer = node.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = Material(textureNames[i]);
                node.SetActive(false);
                sources[i] = node;
            }
            var camera = Child("MotionCamera", root.transform).AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 320;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 100;
            camera.cullingMask = 1 << 30;
            camera.backgroundColor = new Color32(240, 246, 241, 255);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.transform.localPosition = new Vector3(0, 0, -10);
            camera.allowHDR = false;
            camera.allowMSAA = false;
            Bind(player, "_rig", rig.transform);
            Bind(player, "_standing", stand);
            Bind(player, "_sit", sit);
            Bind(player, "_side", side);
            BindArray(player, "_clips", clips);
            BindArray(player, "_sources", sources);
            BindArray(player, "_bones", Bones.ToArray());
            sit.SetActive(false);
            side.SetActive(false);
            root.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(root, RigPath);
            UnityEngine.Object.DestroyImmediate(root);
            Bones.Clear();
        }

        private static void CreateSkin(GameObject node, PetMotionBakeData.Rig data, int order)
        {
            var bones = new Transform[data.bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                var b = data.bones[i];
                bones[i] = Child(b.id, b.parent < 0 ? node.transform : bones[b.parent]).transform;
                bones[i].localPosition = new Vector3(b.position[0], b.position[1], b.position[2]);
                Bones.Add(bones[i]);
            }
            int count = data.positions.Length / 3;
            var vertices = new Vector3[count];
            var uv = new Vector2[count];
            var weights = new BoneWeight[count];
            for (int i = 0; i < count; i++)
            {
                vertices[i] = new Vector3(data.positions[i * 3], data.positions[i * 3 + 1], 0);
                uv[i] = new Vector2(data.uv[i * 2], data.uv[i * 2 + 1]);
                int w = i * 4;
                weights[i] = new BoneWeight
                {
                    boneIndex0 = data.indices[w], boneIndex1 = data.indices[w + 1],
                    boneIndex2 = data.indices[w + 2], boneIndex3 = data.indices[w + 3],
                    weight0 = data.weights[w], weight1 = data.weights[w + 1],
                    weight2 = data.weights[w + 2], weight3 = data.weights[w + 3],
                };
            }
            var bind = new Matrix4x4[bones.Length];
            for (int i = 0; i < bind.Length; i++)
                bind[i] = bones[i].worldToLocalMatrix * node.transform.localToWorldMatrix;
            var mesh = new Mesh
            {
                name = "Mesh_" + data.id,
                vertices = vertices, uv = uv, triangles = data.triangles,
                boneWeights = weights, bindposes = bind,
            };
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, Folder + "/Mesh_" + data.id + ".asset");
            var renderer = node.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.sharedMaterial = Material(data.texture);
            renderer.bones = bones;
            renderer.rootBone = bones[0];
            renderer.quality = SkinQuality.Bone4;
            renderer.sortingOrder = order;
            renderer.updateWhenOffscreen = true;
            renderer.localBounds = new Bounds(Vector3.zero, new Vector3(650, 650, 10));
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static AnimationClip CreateClip(PetMotionBakeData.Take take)
        {
            var clip = new AnimationClip { name = "Anim_Pet_" + take.id, legacy = true, frameRate = 60 };
            foreach (var c in take.curves)
            {
                var keys = new Keyframe[c.times.Length];
                for (int i = 0; i < keys.Length; i++)
                    keys[i] = new Keyframe(c.times[i], c.values[i]);
                var curve = new AnimationCurve(keys);
                var mode = c.step ? AnimationUtility.TangentMode.Constant : AnimationUtility.TangentMode.Linear;
                for (int i = 0; i < keys.Length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, mode);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, mode);
                }
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(c.path, typeof(Transform),
                    c.property), curve);
            }
            clip.wrapMode = WrapMode.ClampForever;
            string path = Folder + "/" + clip.name + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(clip, existing);
                UnityEngine.Object.DestroyImmediate(clip);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        private static Material Material(string texture)
        {
            string path = Folder + "/M_Pet_" + texture + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;
            var material = new Material(AssetDatabase.LoadAssetAtPath<Shader>(Folder + "/Sh_PetMotion.shader"));
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/T_Pet_" + texture + ".png");
            if (material.mainTexture == null)
                throw new InvalidDataException("Missing pet texture: " + texture);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject Child(string name, Transform parent)
        {
            var node = new GameObject(name) { layer = 30 };
            node.transform.SetParent(parent, false);
            return node;
        }

        internal static void Bind(UnityEngine.Object owner, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(owner);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BindArray(UnityEngine.Object owner, string field, UnityEngine.Object[] values)
        {
            var serialized = new SerializedObject(owner);
            var property = serialized.FindProperty(field);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
