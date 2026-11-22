using System;
using System.Collections.Generic;

namespace HomelessPetHelper.Framework.UI
{
    /// <summary>Successful actions advance exactly one step. Cancellation never means learning completed.</summary>
    public sealed class GuideSession
    {
        private readonly IReadOnlyList<GuideStep> _steps;
        private bool _cancelled;
        public string MechanicId { get; }
        public int StepIndex { get; private set; }
        public int StepCount => _steps.Count;
        public bool IsComplete => !_cancelled && StepIndex == _steps.Count;
        public bool IsActive => !_cancelled && StepIndex < _steps.Count;
        public GuideStep Current => IsActive ? _steps[StepIndex] : null;
        public int Revision { get; private set; }

        public GuideSession(string mechanicId, GuideStep[] steps)
        {
            if (string.IsNullOrWhiteSpace(mechanicId) || steps == null || steps.Length == 0)
                throw new ArgumentException("A guide requires a mechanic version and steps.");
            foreach (var step in steps)
                if (step == null) throw new ArgumentException("A guide cannot contain null steps.");
            MechanicId = mechanicId;
            _steps = Array.AsReadOnly((GuideStep[])steps.Clone());
        }

        public bool Allows(string action)
        {
            if (!IsActive) return true;
            foreach (string allowed in Current.AllowedActions)
                if (string.Equals(allowed, action, StringComparison.Ordinal)) return true;
            return false;
        }

        public bool ReportSucceeded(string action)
        {
            if (!IsActive || !string.Equals(Current.CompletionAction, action, StringComparison.Ordinal))
                return false;
            StepIndex++;
            Revision++;
            return true;
        }

        public void Cancel()
        {
            if (!IsActive) return;
            _cancelled = true;
            Revision++;
        }
    }
}
