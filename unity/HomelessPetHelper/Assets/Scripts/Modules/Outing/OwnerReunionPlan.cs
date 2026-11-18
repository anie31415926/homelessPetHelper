using System;

namespace HomelessPetHelper.Modules.Outing
{
    /// <summary>
    /// Caller-confirmed safe direct handoff arrangement for one request. This is a departure
    /// prerequisite, not evidence that rescue, identity verification or handoff has completed.
    /// A pet-search label alone must never create a capacity exemption.
    /// </summary>
    public sealed class OwnerReunionPlan
    {
        public Guid RequestId { get; }
        public string OwnerId { get; }
        public string ReceivingArrangementId { get; }

        public OwnerReunionPlan(Guid requestId, string ownerId, string receivingArrangementId)
        {
            if (requestId == Guid.Empty)
            {
                throw new ArgumentException("A request ID is required.", nameof(requestId));
            }

            if (string.IsNullOrWhiteSpace(ownerId) || ownerId != ownerId.Trim())
            {
                throw new ArgumentException("A trimmed owner ID is required.", nameof(ownerId));
            }

            if (string.IsNullOrWhiteSpace(receivingArrangementId) ||
                receivingArrangementId != receivingArrangementId.Trim())
            {
                throw new ArgumentException("A confirmed receiving arrangement is required.",
                    nameof(receivingArrangementId));
            }

            RequestId = requestId;
            OwnerId = ownerId;
            ReceivingArrangementId = receivingArrangementId;
        }
    }
}
