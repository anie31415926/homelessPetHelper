using System;
using System.Linq;
using HomelessPetHelper.Game.Field;
using HomelessPetHelper.Modules.Approach;
using HomelessPetHelper.Tests.Support;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class FieldDataTests
    {
        private string Json => AssetDatabase.LoadAssetAtPath<TextAsset>(
            "Assets/Settings/Game/YardFieldConfig.json").text;
        [Test]
        public void RectangularFieldMapsHighIndicesAndRetainsIndependentCatalogs()
        {
            var data = FieldTestData.Resize(Json, 9, 10);
            var source = new TextAsset(JsonUtility.ToJson(data));
            try
            {
                var field = new OutingFieldLayout(source);
                Assert.That(field.Columns, Is.EqualTo(9)); Assert.That(field.Rows, Is.EqualTo(10));
                Assert.That(field.CellCount, Is.EqualTo(90));
                var point = field.CellPoint(64);
                Assert.That(point.x, Is.EqualTo(field.Grid.x + 1.5f * field.Grid.width / 9).Within(.001));
                Assert.That(point.y, Is.EqualTo(field.Grid.y + 7.5f * field.Grid.height / 10).Within(.001));
                Assert.Throws<ArgumentOutOfRangeException>(() => field.CellPoint(90));
                Assert.That(field.Opening.Get(ApproachMode.Puzzle).Entrance, Is.EqualTo(64));
                data.approach.templates[0].rows[0] = "#########";
                Assert.That(field.Opening.Get(ApproachMode.Puzzle).Terrain(5), Is.EqualTo('.'));
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        [TestCase("empty")] [TestCase("ragged")] [TestCase("oversize")]
        [TestCase("dimensions")] [TestCase("endpoint")] [TestCase("id")]
        public void MalformedDimensionsAndIdentityAreRejected(string defect)
        {
            var data = FieldTestData.Resize(Json, 9, 10);
            var template = data.approach.templates[0];
            if (defect == "empty") template.rows = Array.Empty<string>();
            if (defect == "ragged") template.rows[1] = "#";
            if (defect == "oversize") template.rows = Enumerable.Repeat(new string('.', 33), 33).ToArray();
            if (defect == "dimensions") template.rows[0] += "#";
            if (defect == "endpoint") template.entrance = 90;
            if (defect == "id") data.layoutId = " bad ";
            var source = new TextAsset(JsonUtility.ToJson(data));
            try { Assert.Throws<ArgumentException>(() => new OutingFieldLayout(source)); }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        [Test]
        public void VariantsAreStableAcrossOrderingAndLegacyMissingIdKeepsPointIdentity()
        {
            var data = FieldTestData.Resize(Json, 9, 10); data.layoutId = "yard.wide";
            var wide = new TextAsset(JsonUtility.ToJson(data));
            data.layoutId = null;
            var legacy = new TextAsset(JsonUtility.ToJson(data));
            var original = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Settings/Game/YardFieldConfig.json");
            try
            {
                var a = new OutingFieldCatalog(new[] { original, wide }, new[] { "yard" });
                var b = new OutingFieldCatalog(new[] { wide, original }, new[] { "yard" });
                var selected = Enumerable.Range(0, 50).Select(seed => a.Get("yard", seed).LayoutId).ToArray();
                Assert.That(selected.Distinct().Count(), Is.EqualTo(2));
                Assert.That(selected, Is.EqualTo(Enumerable.Range(0, 50).Select(seed => b.Get("yard", seed).LayoutId)));
                Assert.That(new OutingFieldLayout(legacy).LayoutId, Is.EqualTo("yard"));
                Assert.Throws<ArgumentException>(() => new OutingFieldCatalog(new[] { wide, wide }, new[] { "yard" }));
                Assert.Throws<ArgumentOutOfRangeException>(() => a.Get("yard", -1));
            }
            finally { UnityEngine.Object.DestroyImmediate(wide); UnityEngine.Object.DestroyImmediate(legacy); }
        }
    }
}
