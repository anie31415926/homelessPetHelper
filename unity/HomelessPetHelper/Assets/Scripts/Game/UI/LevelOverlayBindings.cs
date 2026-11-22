using System;
using System.Collections.Generic;
using HomelessPetHelper.Framework.UI;
using UnityEngine;

namespace HomelessPetHelper.Game.UI
{
    public sealed class LevelOverlayBindings
    {
        public Action<bool> Suspend { get; }
        public Action Return { get; }
        public Func<string> DescribeObjectives { get; }
        public Func<GuideSession> CreateGuide { get; }
        public Action<ILevelOverlay, GuideSession> BindInput { get; }
        public Func<string, List<RectTransform>, bool> ResolveTarget { get; }
        public RectTransform PauseTarget { get; }
        public RectTransform ObjectivesTarget { get; }

        public LevelOverlayBindings(Action<bool> suspend, Action returnRequested, Func<string> describeObjectives,
            Func<GuideSession> createGuide, Action<ILevelOverlay, GuideSession> bindInput,
            Func<string, List<RectTransform>, bool> resolveTarget, RectTransform pause, RectTransform objectives)
        {
            Suspend = suspend ?? throw new ArgumentNullException(nameof(suspend));
            Return = returnRequested ?? throw new ArgumentNullException(nameof(returnRequested));
            DescribeObjectives = describeObjectives ?? throw new ArgumentNullException(nameof(describeObjectives));
            CreateGuide = createGuide ?? throw new ArgumentNullException(nameof(createGuide));
            BindInput = bindInput ?? throw new ArgumentNullException(nameof(bindInput));
            ResolveTarget = resolveTarget ?? throw new ArgumentNullException(nameof(resolveTarget));
            if (pause == null || objectives == null) throw new ArgumentException("Missing corner input targets.");
            PauseTarget = pause;
            ObjectivesTarget = objectives;
        }
    }
}
