namespace HomelessPetHelper.Modules.Rescue
{
    // Completion signals come from validated gameplay, not directly from UI buttons.
    public enum RescueSignal
    {
        LeaveStation = 0,
        ApproachCompleted = 1,
        NegotiationCompleted = 2,
        ReturnToStation = 3,
        CareCompleted = 4
    }
}
