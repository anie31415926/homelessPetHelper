using System;
using System.Linq;
using HomelessPetHelper.Modules.Rescue.Options;
using HomelessPetHelper.Modules.Rescue.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class RescueOptionContentTests
    {
        private const string OptionsPath = "Assets/Settings/Rescue/RescueOptions.json";
        private const string TextPath = "Assets/Settings/Rescue/RescueOptionText.zh-CN.json";

        [Test]
        public void SavedJsonHasSevenUniqueMethodsAndLiteralReferencedText()
        {
            var content = LoadSaved();
            Assert.That(content.Catalog.Entries.Count, Is.EqualTo(7));
            Assert.That(content.Catalog.Entries.Count(entry => entry.Kind == RescueOptionKind.Flavor), Is.EqualTo(2));
            foreach (RescueOptionContent entry in content.Catalog.Entries)
            {
                Assert.That(content.GetText(entry.TitleKey), Is.Not.Empty);
                if (entry.Kind == RescueOptionKind.Flavor)
                {
                    Assert.That(content.GetText(entry.FeedbackKey), Is.Not.Empty);
                }
            }

            Assert.Throws<ArgumentException>(() => content.GetText("missing"));
        }

        [TestCase("\"schemaVersion\": 1", "\"schemaVersion\": 2")]
        [TestCase("\"schemaVersion\": 1,", "")]
        [TestCase("\"kind\": \"Flavor\"", "\"kind\": \"Unknown\"")]
        [TestCase("\"kind\": \"Flavor\"", "\"kind\": \"1\"")]
        [TestCase("\"id\": \"phone.photo\"", "\"id\": \"phone.reply\"")]
        [TestCase("\"titleKey\": \"option.phone.photo.title\"", "\"titleKey\": \"missing\"")]
        [TestCase("\"feedbackKey\": \"option.phone.photo.feedback\"", "\"feedbackKey\": \"missing\"")]
        [TestCase("\"groupId\": \"phone-photo\"", "\"groupId\": \"wait\"")]
        [TestCase("\"groupId\": \"phone-photo\",", "")]
        public void InvalidOptionsFailInsteadOfBuildingPartialContent(string from, string to)
        {
            string options = AssetDatabase.LoadAssetAtPath<TextAsset>(OptionsPath).text;
            Assert.That(options, Does.Contain(from));
            AssertInvalid(options.Replace(from, to), AssetDatabase.LoadAssetAtPath<TextAsset>(TextPath).text);
        }

        [TestCase("\"schemaVersion\": 1", "\"schemaVersion\": 0")]
        [TestCase("\"key\": \"option.phone.photo.title\"", "\"key\": \"option.phone.reply.title\"")]
        [TestCase("\"key\": \"option.phone.photo.feedback\"", "\"key\": \"missing\"")]
        [TestCase("\"value\":", "\"unused\":")]
        public void MissingOrDuplicateTextFails(string from, string to)
        {
            string text = AssetDatabase.LoadAssetAtPath<TextAsset>(TextPath).text;
            Assert.That(text, Does.Contain(from));
            AssertInvalid(AssetDatabase.LoadAssetAtPath<TextAsset>(OptionsPath).text, text.Replace(from, to));
        }

        [TestCase("{")]
        [TestCase("{}")]
        [TestCase("{\"schemaVersion\":1,\"options\":[]}")]
        public void MalformedOrEmptyOptionsFail(string options)
        {
            AssertInvalid(options, AssetDatabase.LoadAssetAtPath<TextAsset>(TextPath).text);
        }

        [TestCase("{0}")]
        [TestCase("<b>title</b>")]
        public void FirstSchemaRejectsFormattingAndRichText(string value)
        {
            AssertInvalid(AssetDatabase.LoadAssetAtPath<TextAsset>(OptionsPath).text,
                "{\"schemaVersion\":1,\"entries\":[{\"key\":\"wait\",\"value\":\"" + value + "\"}]}");
        }

        [Test]
        public void CatalogCopiesInputAndRejectsUnknownIdentity()
        {
            var entry = new RescueOptionContent("wait", "wait", RescueOptionKind.Rescue, "wait", "");
            var input = new[] { entry };
            var catalog = new RescueOptionCatalog(input);
            input[0] = null;
            Assert.That(catalog.Entries.Single(), Is.SameAs(entry));
            Assert.Throws<ArgumentException>(() => catalog.Get("missing"));
        }

        internal static RescueOptionContentLoader LoadSaved()
        {
            return new RescueOptionContentLoader(AssetDatabase.LoadAssetAtPath<TextAsset>(OptionsPath),
                AssetDatabase.LoadAssetAtPath<TextAsset>(TextPath));
        }

        private static void AssertInvalid(string options, string text)
        {
            var optionAsset = new TextAsset(options);
            var textAsset = new TextAsset(text);
            try
            {
                Assert.Throws<ArgumentException>(() => new RescueOptionContentLoader(optionAsset, textAsset));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(optionAsset);
                UnityEngine.Object.DestroyImmediate(textAsset);
            }
        }
    }
}
