using System;

namespace HomelessPetHelper.Framework.Persistence
{
    public sealed class SaveError : Exception
    {
        public SaveErrorCode Code { get; }

        public SaveError(SaveErrorCode code, string message, Exception cause = null) : base(message, cause)
        {
            Code = code;
        }
    }
}
