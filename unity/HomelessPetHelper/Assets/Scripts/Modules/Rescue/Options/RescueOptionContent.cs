using System;

namespace HomelessPetHelper.Modules.Rescue.Options
{
    public sealed class RescueOptionContent
    {
        public string Id { get; }
        public string GroupId { get; }
        public RescueOptionKind Kind { get; }
        public string TitleKey { get; }
        public string FeedbackKey { get; }

        public RescueOptionContent(string id, string groupId, RescueOptionKind kind,
            string titleKey, string feedbackKey)
        {
            ValidateKey(id, nameof(id));
            ValidateKey(groupId, nameof(groupId));
            ValidateKey(titleKey, nameof(titleKey));
            if (!Enum.IsDefined(typeof(RescueOptionKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (kind == RescueOptionKind.Flavor)
            {
                ValidateKey(feedbackKey, nameof(feedbackKey));
            }
            else if (feedbackKey != string.Empty)
            {
                throw new ArgumentException("Rescue feedback is supplied by its actual rule result.",
                    nameof(feedbackKey));
            }

            Id = id;
            GroupId = groupId;
            Kind = kind;
            TitleKey = titleKey;
            FeedbackKey = feedbackKey;
        }

        private static void ValidateKey(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
            {
                throw new ArgumentException("A nonempty trimmed key is required.", name);
            }
        }
    }
}
