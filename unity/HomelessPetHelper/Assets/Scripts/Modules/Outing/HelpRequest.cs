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

        public OwnerReunionPlan ReunionPlan { get; }

        public RequestState State { get; internal set; }

        public HelpRequest(Guid requestId, string templateId, string pointId, int templateVersion,
            OwnerReunionPlan reunionPlan = null)
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

            if (reunionPlan != null && reunionPlan.RequestId != requestId)
            {
                throw new ArgumentException("The reunion plan must belong to this request.", nameof(reunionPlan));
            }

            RequestId = requestId;
            TemplateId = templateId;
            PointId = pointId;
            TemplateVersion = templateVersion;
            ReunionPlan = reunionPlan;
            State = RequestState.Available;
        }
    }
}
