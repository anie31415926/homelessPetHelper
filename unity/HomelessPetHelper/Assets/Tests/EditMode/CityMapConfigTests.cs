using HomelessPetHelper.Game.CityMap;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Text;
using HomelessPetHelper.Game.Station;
using System;
using System.Linq;
using HomelessPetHelper.Editor.CityMap;
using HomelessPetHelper.Editor.OptionPreview;
using HomelessPetHelper.Modules.Outing;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class CityMapConfigTests
    {
        [Test]
        public void SavedConfigMatchesTextAndBuildsFreshRequestStateForEverySession()
        {
            var config = new CityMapPreviewConfig(Source());
            config.ValidateText(new OptionPreviewText(
                AssetDatabase.LoadAssetAtPath<TextAsset>(CityMapAssets.TextPath)));
            RequestPoolState first = config.CreateRequestPool();
            RequestPoolState second = config.CreateRequestPool();
            HelpRequest original = first.Requests.First();
            HelpRequest other = second.Requests.First();
            Assert.That(original.RequestId, Is.Not.EqualTo(other.RequestId));
            Assert.That(first.TryTransition(original.RequestId, RequestSignal.Accept, out _), Is.True);
            Assert.That(other.State, Is.EqualTo(RequestState.Available));
            Assert.That(config.CreateRules(new PointUnlockState(), second)
                .TryPlanDeparture("porch", 0, out MapDeparturePlan plan, out _), Is.True);
            Assert.That(plan.EntryKind, Is.EqualTo(MapEntryKind.OwnerReunion));
        }

        [TestCase(1)]
        [TestCase(4)]
        [TestCase(12)]
        public void DefinitionsSupportVariableCountsIdsAndTextKeys(int count)
        {
            string points = string.Join(",", Enumerable.Range(0, count).Select(index =>
                "{\"id\":\"site-" + index + "\",\"nameKey\":\"label-" + index +
                "\",\"detailKey\":\"description-" + index + "\",\"regionId\":\"area\"," +
                "\"openByDefault\":true,\"sceneTemplateIds\":[\"scene\"]}"));
            var source = new TextAsset("{\"schemaVersion\":1,\"backgroundId\":\"terrain\"," +
                "\"regionIds\":[\"area\"],\"initialPointId\":\"site-0\",\"initialCapacity\":8," +
                "\"minutesPerDay\":600,\"seed\":19,\"timePolicy\":{\"explorationMinutes\":7," +
                "\"openingMinutes\":2,\"negotiationMinutes\":3,\"handoverMinutes\":4}," +
                "\"points\":[" + points + "],\"requestTemplates\":[],\"initialRequests\":[]}");
            try
            {
                var config = new CityMapPreviewConfig(source);
                var rules = config.CreateRules(new PointUnlockState(), config.CreateRequestPool());
                Assert.That(rules.BuildSnapshot().Points.Count, Is.EqualTo(count));
                Assert.That(config.GetDetailKey("site-" + (count - 1)), Is.EqualTo("description-" + (count - 1)));
                Assert.That(config.InitialCapacity, Is.EqualTo(8));
                Assert.That(config.Seed, Is.EqualTo(19));
                Assert.That(config.MinutesPerDay, Is.EqualTo(600));
                Assert.That(config.TimePolicy.TryGetDuration(OutingReturnProgress.Exploring, out long duration, out _),
                    Is.True);
                Assert.That(duration, Is.EqualTo(7));
                Assert.That(rules.TryPlanDeparture("site-" + (count - 1), 1, out _, out _), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        [TestCase("\"schemaVersion\": 1", "\"schemaVersion\": 2")]
        [TestCase("\"initialCapacity\": 2", "\"initialCapacity\": -1")]
        [TestCase("\"minutesPerDay\": 1440", "\"minutesPerDay\": 0")]
        [TestCase("\"seed\": 20261118", "\"seed\": -1")]
        [TestCase("\"initialPointId\": \"parking\"", "\"initialPointId\": \"missing\"")]
        [TestCase("\"backgroundId\": \"neighborhood\"", "\"backgroundId\": \" \"")]
        [TestCase("\"openByDefault\": true", "\"openByDefault\": false")]
        [TestCase("\"id\": \"yard\"", "\"id\": \"parking\"")]
        [TestCase("\"regionId\": \"nearby\"", "\"regionId\": \"unknown\"")]
        [TestCase("\"regionIds\": [\"nearby\"]", "\"regionIds\": [\"nearby\",\"nearby\"]")]
        [TestCase("\"detailKey\": \"yard.detail\"", "\"detailKey\": \"\"")]
        [TestCase("\"sceneTemplateIds\": [\"shed\"]", "\"sceneTemplateIds\": []")]
        [TestCase("\"allowedPointIds\": [\"parking\"]", "\"allowedPointIds\": [\"missing\"]")]
        [TestCase("\"allowedPointIds\": [\"parking\"]", "\"allowedPointIds\": [\"yard\"]")]
        [TestCase("\"id\": \"reunion\"", "\"id\": \"ordinary\"")]
        [TestCase("\"templateId\": \"ordinary\"", "\"templateId\": \"missing\"")]
        [TestCase("\"templateVersion\": 1", "\"templateVersion\": 0")]
        [TestCase("\"receivingArrangementId\": \"safe-receiving\"", "\"receivingArrangementId\": \"\"")]
        [TestCase("\"explorationMinutes\": 5", "\"explorationMinutes\": 0")]
        [TestCase("\"hasDirectReception\": true", "\"hasDirectReception\": false")]
        [TestCase("\"points\": [", "\"points\": null,\"ignoredPoints\": [")]
        [TestCase("\"requestTemplates\": [", "\"requestTemplates\": null,\"ignoredTemplates\": [")]
        [TestCase("\"initialCapacity\": 2", "\"ignoredCapacity\": 2")]
        public void InvalidConfigIsRejectedBeforeAnySessionIsCreated(string before, string after)
        {
            string json = Source().text;
            Assert.That(json.Contains(before), Is.True, "Fixture changed; update this corruption probe.");
            var source = new TextAsset(json.Replace(before, after));
            try { Assert.Catch<ArgumentException>(() => new CityMapPreviewConfig(source)); }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        [Test]
        public void DurationOverflowAndMissingTextAreRejected()
        {
            var overflowing = new TextAsset(Source().text.Replace("\"explorationMinutes\": 5",
                "\"explorationMinutes\": 9223372036854775807"));
            var missingText = new TextAsset("{\"entries\":[{\"key\":\"unused\",\"value\":\"unused\"}]}");
            try
            {
                Assert.Throws<OverflowException>(() => new CityMapPreviewConfig(overflowing));
                Assert.Throws<ArgumentException>(() => new CityMapPreviewConfig(Source())
                    .ValidateText(new OptionPreviewText(missingText)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(overflowing);
                UnityEngine.Object.DestroyImmediate(missingText);
            }
        }

        [Test]
        public void OptionalInitialRequestsMayBeEmptyOrNullForAnExplorationMap()
        {
            var source = new TextAsset(Source().text.Replace("\"initialRequests\": [",
                "\"initialRequests\": null,\"ignoredRequests\": ["));
            try
            {
                var config = new CityMapPreviewConfig(source);
                RequestPoolState pool = config.CreateRequestPool();
                Assert.That(pool.Requests.Count, Is.Zero);
                Assert.That(config.CreateRules(new PointUnlockState(), pool)
                    .TryPlanDeparture("parking", 1, out MapDeparturePlan plan, out _), Is.True);
                Assert.That(plan.EntryKind, Is.EqualTo(MapEntryKind.Exploration));
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        [TestCase("title")]
        [TestCase("pause")]
        [TestCase("status")]
        [TestCase("point.Locked")]
        [TestCase("space.required")]
        [TestCase("departure.rescue")]
        [TestCase("unavailable.reunion")]
        [TestCase("unavailable.exploration")]
        [TestCase("request.unavailable")]
        [TestCase("returned.exploring")]
        [TestCase("full")]
        [TestCase("shelter")]
        public void MissingMapCopyIsRejectedDuringValidationWithItsKey(string key)
        {
            TextAsset source = ChangedText(key, null);
            try
            {
                var error = Assert.Throws<ArgumentException>(() => new CityMapPreviewConfig(Source())
                    .ValidateText(new OptionPreviewText(source)));
                Assert.That(error.Message, Does.Contain(key));
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        [TestCase("status", "{2}")]
        [TestCase("status", "{0")]
        [TestCase("detail.request", "{2}")]
        [TestCase("detail.noRequest", "{1}")]
        [TestCase("departure.rescue", "{2}")]
        [TestCase("departure.preview", "{0:Q}")]
        [TestCase("returned", "{1}")]
        [TestCase("arrived", "{1}")]
        [TestCase("parking", "{0}")]
        [TestCase("parking.detail", "broken }")]
        [TestCase("parking.rescue", "{0}")]
        [TestCase("full", "{0}")]
        public void InvalidMapFormatIsRejectedWithTheKeyAndOriginalCause(string key, string value)
        {
            TextAsset source = ChangedText(key, value);
            try
            {
                var error = Assert.Throws<ArgumentException>(() => new CityMapPreviewConfig(Source())
                    .ValidateText(new OptionPreviewText(source)));
                Assert.That(error.Message, Does.Contain(key));
                Assert.That(error.InnerException, Is.TypeOf<FormatException>());
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        [TestCase("status", "{1:N0} / {0:D2}")]
        [TestCase("detail.request", "{1} {{help}} {0}")]
        [TestCase("departure.rescue", "{1} {0:D2}")]
        [TestCase("ready", "{{ready}}")]
        public void LegalReorderedArgumentsFormatsAndEscapedBracesRemainValid(string key, string value)
        {
            TextAsset source = ChangedText(key, value);
            try
            {
                Assert.DoesNotThrow(() => new CityMapPreviewConfig(Source())
                    .ValidateText(new OptionPreviewText(source)));
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        [Test]
        public void MainContentRejectsBadMapTextBeforeCreatingGameplay()
        {
            var content = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameContent>(
                "Assets/Settings/Game/GameContent.asset"));
            TextAsset source = ChangedText("returned", "{1}");
            try
            {
                var serialized = new SerializedObject(content);
                serialized.FindProperty("_mapText").objectReferenceValue = source;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var error = Assert.Throws<ArgumentException>(content.Validate);
                Assert.That(error.Message, Does.Contain("returned"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(content);
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        private static TextAsset ChangedText(string key, string replacement)
        {
            var table = JObject.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(CityMapAssets.TextPath).text);
            JToken entry = table["entries"].Single(item => (string)item["key"] == key);
            if (replacement == null) entry.Remove();
            else entry["value"] = replacement;
            return new TextAsset(table.ToString());
        }

        private static TextAsset Source() => AssetDatabase.LoadAssetAtPath<TextAsset>(CityMapAssets.ConfigPath);
    }
}
