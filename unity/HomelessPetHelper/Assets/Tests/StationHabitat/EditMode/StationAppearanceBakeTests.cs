using System;
using System.IO;
using HomelessPetHelper.Editor.StationHabitat;
using NUnit.Framework;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace HomelessPetHelper.Tests.StationHabitat.EditMode
{
    public sealed class StationAppearanceBakeTests
    {
        private string _path;
        private string _relative;
        [SetUp]
        public void SetUp()
        {
            _relative = "appearance-validation-" + Guid.NewGuid().ToString("N") + ".json";
            _path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", _relative));
        }
        [TearDown]
        public void TearDown() { if (File.Exists(_path)) File.Delete(_path); }

        [Test]
        public void ValidBakeLoadsWithExplicitDestinationsAndInvalidWeightsAreRejected()
        {
            var data = CreateBake();
            File.WriteAllText(_path, JsonConvert.SerializeObject(data));
            data = StationAnimalBake.Load(_relative);
            Assert.That(data.appearanceId, Is.EqualTo("cat.orange.side"));
            Assert.That(data.unity.actorPath,
                Is.EqualTo("Assets/Prefabs/Widgets/Widget_StationHabitatActor.prefab"));
            // Serialize through Unity's authoring DTO, then exercise the independent import boundary.
            data.meshes[0].weights[0] = -1;
            File.WriteAllText(_path, JsonConvert.SerializeObject(data));
            Assert.Throws<InvalidDataException>(() => StationAnimalBake.Load(_relative));
        }

        private static StationAnimalBake.Data CreateBake()
        {
            var bones = new StationAnimalBake.Bone[36];
            var bindposes = new float[36][];
            for (int i = 0; i < 36; i++)
            {
                bones[i] = new StationAnimalBake.Bone { id = i == 35 ? "tailArt" : "bone" + i,
                    parent = -1, position = new float[3], rotation = new[] { 0f, 0, 0, 1 } };
                bindposes[i] = new float[16];
                for (int j = 0; j < 4; j++) bindposes[i][j * 5] = 1;
            }
            var curve = new StationAnimalBake.Curve { path = "bone0", property = "m_LocalPosition.x",
                times = new[] { 0f, 1 }, values = new[] { 0f, 0 } };
            return new StationAnimalBake.Data { schema = 2, appearanceId = "cat.orange.side",
                template = "side-quadruped-v1", bones = bones, bindposes = bindposes,
                meshes = new[] { new StationAnimalBake.Skin { id = "body", tint = "ffffff",
                    positions = new[] { 0f, 0, 0, 1, 0, 0, 0, 1, 0 }, uv = new float[6],
                    weights = new[] { 1f, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0 },
                    indices = new int[12], triangles = new[] { 0, 1, 2 } } },
                takes = new[] { new StationAnimalBake.Take { id = "SitDown", duration = 1,
                    curves = new[] { curve } }, new StationAnimalBake.Take { id = "Tread", duration = 1,
                    curves = new[] { curve } } },
                tailMotion = new StationAnimalBake.TailMotion { joinLocal = new float[3], periodSeconds = 1 },
                framing = new StationAnimalBake.Frame { orthoSize = 3, cameraPosition = new[] { 0f, 0, -10 } },
                unity = new StationAnimalBake.Target { assetFolder = "Assets/Art/PetSit", assetPrefix = "Cat",
                    actorPath = "Assets/Prefabs/Widgets/Widget_StationHabitatActor.prefab",
                    templateActorPath = "Assets/Prefabs/Widgets/Widget_StationHabitatActor.prefab",
                    atlasPath = "Assets/Art/PetSit/T_Cat_SitAtlas.png",
                    shaderPath = "Assets/Art/PetSit/Sh_StationCat.shader" } };
        }

        [Test]
        public void InvalidBakeIsRejectedBeforeAnyPrefabCanChange()
        {
            string prefab = "Assets/Prefabs/Widgets/Widget_StationHabitatActor.prefab";
            byte[] before = File.ReadAllBytes(prefab);
            var data = CreateBake();
            File.WriteAllText(_path, JsonConvert.SerializeObject(data));
            data = StationAnimalBake.Load(_relative);
            data.unity.actorPath = "Assets/../Unexpected.prefab";
            File.WriteAllText(_path, JsonConvert.SerializeObject(data));
            Assert.Throws<InvalidDataException>(() => new StationAnimalAssets(_relative));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(prefab));
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(prefab), Is.Not.Null);
        }
    }
}
