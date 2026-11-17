using System;

namespace HomelessPetHelper.Modules.Outing
{
    /// <summary>
    /// One mutable help-request instance. Holds a stable identity and point reference;
    /// its state is changed only through RequestPoolState.TryTransition.
    /// Does not certify a rescue, issue rewards, or store a concrete animal instance.
    /// </summary>
    public sealed class HelpRequest
    {
        public Guid RequestId { get; }

        public string TemplateId { get; }

        public string PointId { get; }

        public int TemplateVersion { get; }

        public RequestState State { get; internal set; }

        public HelpRequest(Guid requestId, string templateId, string pointId, int templateVersion)
        {
            if (requestId == Guid.Empty)
            {
                throw new ArgumentException("A nonempty request ID is required.", nameof(requestId));
            }

            if (string.IsNullOrWhiteSpace(templateId) || templateId != templateId.Trim())
            {
                throw new ArgumentException("A trimmed nonempty template ID is required.", nameof(templateId));
            }

            if (string.IsNullOrWhiteSpace(pointId) || pointId != pointId.Trim())
            {
                throw new ArgumentException("A trimmed nonempty point ID is required.", nameof(pointId));
            }

            RequestId = requestId;
            TemplateId = templateId;
            PointId = pointId;
            TemplateVersion = templateVersion;
            State = RequestState.Available;
        }
    }
}
