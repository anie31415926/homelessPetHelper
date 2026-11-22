using System;
using System.Collections.Generic;

namespace HomelessPetHelper.Framework.UI
{
    public sealed class GuideStep
    {
        public string CaptionKey { get; }
        public string CompletionAction { get; }
        public IReadOnlyList<string> AllowedActions { get; }

        public GuideStep(string captionKey, string completionAction, string[] allowedActions)
        {
            if (string.IsNullOrWhiteSpace(captionKey) || string.IsNullOrWhiteSpace(completionAction) ||
                allowedActions == null || allowedActions.Length == 0)
                throw new ArgumentException("A guide step needs a caption, successful action and input targets.");
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string action in allowedActions)
                if (string.IsNullOrWhiteSpace(action) || !unique.Add(action))
                    throw new ArgumentException("Guide actions must be nonempty and unique.");
            if (!unique.Contains(completionAction))
                throw new ArgumentException("Guide completion must be an allowed action.");
            CaptionKey = captionKey;
            CompletionAction = completionAction;
            AllowedActions = Array.AsReadOnly((string[])allowedActions.Clone());
        }
    }
}
