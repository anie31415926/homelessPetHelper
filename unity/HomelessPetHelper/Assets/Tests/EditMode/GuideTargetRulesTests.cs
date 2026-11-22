using HomelessPetHelper.Framework.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.EditMode
{
    /// <summary>A guide hole is the only opening in a full-screen input mask, so a target that cannot
    /// receive the click must be refused instead of becoming a dead opening.</summary>
    public sealed class GuideTargetRulesTests
    {
        private GameObject _target;

        [TearDown]
        public void TearDown()
        {
            if (_target != null) Object.DestroyImmediate(_target);
        }

        [Test]
        public void HiddenOrDisabledTargetsAreRefusedSoTheirHoleCannotStrandTheLevel()
        {
            _target = new GameObject("GuideTarget", typeof(RectTransform), typeof(Button));
            var button = _target.GetComponent<Button>();
            Assert.That(GuideTargetRules.IsClickable(button), Is.True, "A visible enabled button is a target.");
            button.interactable = false;
            Assert.That(GuideTargetRules.IsClickable(button), Is.False,
                "A disabled button would swallow the click opened for it.");
            button.interactable = true;
            _target.SetActive(false);
            Assert.That(GuideTargetRules.IsClickable(button), Is.False,
                "A hidden target degenerates its hole to zero and locks the whole screen.");
        }

        [Test]
        public void MissingTargetIsNeverClickable() =>
            Assert.That(GuideTargetRules.IsClickable(null), Is.False);
    }
}
