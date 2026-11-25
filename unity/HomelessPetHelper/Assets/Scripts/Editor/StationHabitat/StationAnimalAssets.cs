using System;
using System.Collections.Generic;
using System.IO;
using HomelessPetHelper.Modules.PetMotion;
using HomelessPetHelper.Modules.Station.UI.Habitat;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static HomelessPetHelper.Editor.StationHabitat.StationAnimalBake;

namespace HomelessPetHelper.Editor.StationHabitat
{
    /// <summary>Imports one validated appearance into its own assets and saved prefab.</summary>
    public sealed class StationAnimalAssets
    {
        private readonly Data _data;
        public StationAnimalAssets(string bakePath) { _data = StationAnimalBake.Load(bakePath); }

        public static void ImportFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-animalBake");
            if (index < 0 || index + 1 >= args.Length)
                throw new ArgumentException("Supply -animalBake <project-relative JSON>.");
            new StationAnimalAssets(args[index + 1]).Apply();
        }

        private void PrepareTarget()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch Editor.");
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(_data.unity.atlasPath) == null ||
                AssetDatabase.LoadAssetAtPath<Shader>(_data.unity.shaderPath) == null ||
                AssetDatabase.LoadAssetAtPath<GameObject>(_data.unity.templateActorPath) == null)
                throw new InvalidDataException("Missing explicitly referenced appearance assets.");
            string path = _data.unity.actorPath;
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                var player = existing.GetComponent<StationCatPlayer>();
                if (player == null || (!string.IsNullOrEmpty(player.AppearanceId) &&
                    player.AppearanceId != _data.appearanceId))
                    throw new InvalidDataException("Refusing to replace a different animal appearance.");
            }
            if (!AssetDatabase.IsValidFolder(_data.unity.assetFolder))
            {
                Directory.CreateDirectory(_data.unity.assetFolder);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            if (existing == null && !AssetDatabase.CopyAsset(_data.unity.templateActorPath, path))
                throw new InvalidDataException("Cannot create appearance prefab from the explicit template.");
        }

        public void Apply()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch Editor.");
            var data = _data;
            PrepareTarget();
            ImportAtlas();
            var root = PrefabUtility.LoadPrefabContents(_data.unity.actorPath);
            try
            {
                if (PrefabUtility.IsPartOfPrefabInstance(root))
                    PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely,
                        InteractionMode.AutomatedAction);
                var renderer = root.GetComponent<StationHabitatRenderer>();
                var camera = root.GetComponentInChildren<Camera>(true);
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                {
                    var child = root.transform.GetChild(i);
                    if (child != camera.transform) UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
                var old = root.GetComponent<PetMotionPlayer>();
                if (old != null) UnityEngine.Object.DestroyImmediate(old);
                var player = root.GetComponent<StationCatPlayer>() ?? root.AddComponent<StationCatPlayer>();
                var rig = Child(_data.unity.assetPrefix + "Rig", root.transform);
                var bones = CreateBones(rig, data.bones);
                var contact = new List<SkinnedMeshRenderer>();
                foreach (var skin in data.meshes)
                {
                    var mesh = CreateSkin(rig, bones, skin, data.bindposes);
                    if (skin.seatContact != 0) contact.Add(mesh);
                }
                var playerFields = new SerializedObject(player);
                var contactFields = playerFields.FindProperty("_seatContactRenderers");
                contactFields.arraySize = contact.Count;
                for (int i = 0; i < contact.Count; i++)
                    contactFields.GetArrayElementAtIndex(i).objectReferenceValue = contact[i];
                playerFields.ApplyModifiedPropertiesWithoutUndo();
                Bind(player, "_rig", rig);
                BindProfile(root, player);
                Bind(player, "_sitDown", CreateClip(data.takes[0]));
                Bind(player, "_tread", CreateClip(data.takes[1]));
                BindTail(root, player, bones[Array.FindIndex(data.bones, bone => bone.id == "tailArt")],
                    data.tailMotion);
                Bind(renderer, "_player", player);
                Bind(renderer, "_camera", camera);
                PrefabUtility.SaveAsPrefabAsset(root, _data.unity.actorPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            NormalizeSavedPrefab();
        }

        public void UpdateExpressionBindings()
        {
            PrepareTarget();
            UpdateSitClip();
            var data = _data;
            var root = PrefabUtility.LoadPrefabContents(_data.unity.actorPath);
            try
            {
                var player = root.GetComponent<StationCatPlayer>();
                // Editor-only lookup; the saved runtime prefab holds explicit references.
                var bones = root.GetComponentInChildren<SkinnedMeshRenderer>().bones;
                var tail = Array.Find(bones, bone => bone.name == "tailArt");
                if (tail == null) throw new InvalidDataException("Missing authored tail bone.");
                BindTail(root, player, tail, data.tailMotion);
                BindProfile(root, player);
                PrefabUtility.SaveAsPrefabAsset(root, _data.unity.actorPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            NormalizeSavedPrefab();
        }

        private void BindProfile(GameObject root, StationCatPlayer player)
        {
            var identity = new SerializedObject(player);
            identity.FindProperty("_appearanceId").stringValue = _data.appearanceId;
            identity.ApplyModifiedPropertiesWithoutUndo();
            var camera = root.GetComponentInChildren<Camera>(true);
            camera.orthographicSize = _data.framing.orthoSize;
            var position = _data.framing.cameraPosition;
            camera.transform.localPosition = new Vector3(position[0], position[1], position[2]);
        }

        private void BindTail(GameObject root, StationCatPlayer player, Transform bone, TailMotion data)
        {
            if (data == null || data.joinLocal.Length != 3 || data.periodSeconds <= 0)
                throw new InvalidDataException("Missing authored tail expression settings.");
            var tail = root.GetComponent<StationCatTail>() ?? root.AddComponent<StationCatTail>();
            Bind(tail, "_tail", bone);
            var fields = new SerializedObject(tail);
            fields.FindProperty("_joinLocal").vector3Value = new Vector3(data.joinLocal[0], data.joinLocal[1],
                data.joinLocal[2]);
            fields.FindProperty("_raisedDegrees").floatValue = data.raisedDegrees;
            fields.FindProperty("_swishLiftDegrees").floatValue = data.swishLiftDegrees;
            fields.FindProperty("_swishDegrees").floatValue = data.swishDegrees;
            fields.FindProperty("_periodSeconds").floatValue = data.periodSeconds;
            fields.ApplyModifiedPropertiesWithoutUndo();
            Bind(player, "_tail", tail);
        }

        public void NormalizeSavedPrefab()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch Editor.");
            string[] lines = File.ReadAllLines(_data.unity.actorPath);
            for (int i = 0; i < lines.Length; i++) lines[i] = lines[i].TrimEnd();
            File.WriteAllText(_data.unity.actorPath, string.Join("\n", lines) + "\n");
            AssetDatabase.ImportAsset(_data.unity.actorPath, ImportAssetOptions.ForceSynchronousImport);
        }

        public void UpdateSitClip()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch Editor.");
            var data = _data;

            // A narrow update must not silently change the bind pose, atlas or existing mesh topology.
            RequireSameSkeleton();
            CreateClip(data.takes[0]);
            AssetDatabase.SaveAssets();
        }

        private void RequireSameSkeleton()
        {
            var actor = AssetDatabase.LoadAssetAtPath<GameObject>(_data.unity.actorPath);
            if (actor == null) throw new InvalidDataException("Import the appearance before updating animation.");
            var player = actor.GetComponent<StationCatPlayer>();
            if (player == null || (!string.IsNullOrEmpty(player.AppearanceId) &&
                player.AppearanceId != _data.appearanceId))
                throw new InvalidDataException("Animation update targets a different appearance.");
            var renderer = actor.GetComponentInChildren<SkinnedMeshRenderer>();
            var bones = renderer.bones;
            if (bones.Length != _data.bones.Length) throw new InvalidDataException("Skeleton topology changed.");
            for (int i = 0; i < bones.Length; i++)
                if (bones[i].name != _data.bones[i].id)
                    throw new InvalidDataException("Skeleton topology changed; use a full appearance import.");
            var bindposes = renderer.sharedMesh.bindposes;
            for (int i = 0; i < bindposes.Length; i++)
                for (int j = 0; j < 16; j++)
                    if (Math.Abs(bindposes[i][j] - _data.bindposes[i][j]) > .000001f)
                        throw new InvalidDataException("Bind pose changed; use a full appearance import.");
            if (AssetDatabase.GetAssetPath(renderer.sharedMaterial.mainTexture) != _data.unity.atlasPath)
                throw new InvalidDataException("Atlas changed; use a full appearance import.");
        }

        private Transform[] CreateBones(Transform root, Bone[] data)
        {
            var bones = new Transform[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                var b = data[i];
                bones[i] = Child(b.id, b.parent < 0 ? root : bones[b.parent]);
                bones[i].localPosition = new Vector3(b.position[0], b.position[1], b.position[2]);
                bones[i].localRotation = new Quaternion(b.rotation[0], b.rotation[1], b.rotation[2], b.rotation[3]);
            }
            return bones;
        }

        private SkinnedMeshRenderer CreateSkin(Transform rig, Transform[] bones, Skin skin, float[][] inverses)
        {
            string path = _data.unity.assetFolder + "/Mesh_" + _data.unity.assetPrefix + "_" + skin.id + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var mesh = CreateMesh(skin, inverses, existing);
            if (existing == null) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            var node = Child(skin.id, rig);
            var renderer = node.gameObject.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.bones = bones;
            renderer.rootBone = rig;
            renderer.sharedMaterial = Material(skin.tint);
            renderer.sortingOrder = skin.order;
            renderer.quality = SkinQuality.Bone4;
            renderer.updateWhenOffscreen = true;
            renderer.localBounds = new Bounds(new Vector3(0, _data.framing.cameraPosition[1], 0),
                new Vector3(_data.framing.orthoSize * 2.5f, _data.framing.orthoSize * 2.5f, 2));
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private static BoneWeight PackWeights(Skin data, int offset, int[] slots)
        {
            for (int slot = 0; slot < slots.Length; slot++)
            {
                int source = offset + slot;
                int insert = slot;
                while (insert > 0 && data.weights[slots[insert - 1]] < data.weights[source])
                {
                    slots[insert] = slots[insert - 1];
                    insert--;
                }
                slots[insert] = source;
            }
            return new BoneWeight
            {
                boneIndex0 = data.indices[slots[0]], boneIndex1 = data.indices[slots[1]],
                boneIndex2 = data.indices[slots[2]], boneIndex3 = data.indices[slots[3]],
                weight0 = data.weights[slots[0]], weight1 = data.weights[slots[1]],
                weight2 = data.weights[slots[2]], weight3 = data.weights[slots[3]]
            };
        }

        private Mesh CreateMesh(Skin data, float[][] inverses, Mesh existing)
        {
            int count = data.positions.Length / 3;
            var vertices = new Vector3[count];
            var uv = new Vector2[count];
            var weights = new BoneWeight[count];
            var weightSlots = new int[4];
            for (int i = 0; i < count; i++)
            {
                vertices[i] = new Vector3(data.positions[i * 3], data.positions[i * 3 + 1], 0);
                uv[i] = new Vector2(data.uv[i * 2], data.uv[i * 2 + 1]);
                weights[i] = PackWeights(data, i * 4, weightSlots);
            }
            var bind = new Matrix4x4[inverses.Length];
            for (int i = 0; i < bind.Length; i++)
                for (int j = 0; j < 16; j++) bind[i][j] = inverses[i][j];
            // Updating the native buffers directly also replaces the vertex layout on an existing asset.
            var mesh = existing ?? new Mesh { name = "Mesh_" + _data.unity.assetPrefix + "_" + data.id };
            mesh.Clear(false);
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = data.triangles;
            mesh.boneWeights = weights;
            mesh.bindposes = bind;
            if (data.seatContact != 0)
            {
                var contact = new Vector2[count];
                int paw = Array.FindIndex(_data.bones, bone => bone.id == "nearPaw");
                for (int i = 0; i < count; i++)
                {
                    float protectedWeight = 0;
                    if (data.seatContact == 2)
                        for (int j = i * 4; j < i * 4 + 4; j++)
                            if (data.indices[j] == paw) protectedWeight += data.weights[j];
                    contact[i].x = 1 - protectedWeight;
                }
                mesh.uv2 = contact;
            }
            mesh.RecalculateBounds();
            return mesh;
        }

        private AnimationClip CreateClip(Take take)
        {
            var clip = new AnimationClip { name = "Anim_" + _data.unity.assetPrefix + "_" + take.id,
                legacy = true, frameRate = 60,
                wrapMode = WrapMode.ClampForever };
            foreach (var c in take.curves)
            {
                var keys = new Keyframe[c.times.Length];
                for (int i = 0; i < keys.Length; i++) keys[i] = new Keyframe(c.times[i], c.values[i]);
                var curve = new AnimationCurve(keys);
                for (int i = 0; i < keys.Length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                }
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve(c.path, typeof(Transform), c.property), curve);
            }
            return Save(clip, _data.unity.assetFolder + "/" + clip.name + ".anim");
        }

        private Material Material(string tint)
        {
            string path = _data.unity.assetFolder + "/M_" + _data.unity.assetPrefix + "_" + tint + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(AssetDatabase.LoadAssetAtPath<Shader>(_data.unity.shaderPath));
                AssetDatabase.CreateAsset(material, path);
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(_data.unity.atlasPath);
            if (texture == null) throw new InvalidDataException("Missing imported cat atlas.");
            material.SetTexture("_MainTex", texture);
            ColorUtility.TryParseHtmlString("#" + tint, out var color);
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private T Save<T>(T value, string path) where T : UnityEngine.Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) { AssetDatabase.CreateAsset(value, path); return value; }
            EditorUtility.CopySerialized(value, existing);
            UnityEngine.Object.DestroyImmediate(value);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private void ImportAtlas()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(_data.unity.atlasPath);
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 1024;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            AssetDatabase.ImportAsset(_data.unity.atlasPath, ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
        }

        private Transform Child(string name, Transform parent)
        {
            var node = new GameObject(name) { layer = 29 };
            node.transform.SetParent(parent, false);
            return node.transform;
        }

        private void Bind(UnityEngine.Object owner, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(owner);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
