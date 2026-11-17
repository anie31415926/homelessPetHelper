namespace HomelessPetHelper.Modules.Rescue
{
    // Completion signals come from validated gameplay, not directly from UI buttons.
    public enum RescueSignal
    {
        LeaveStation = 0,
        Depart = 1,
        ApproachCompleted = 2,
        NegotiationCompleted = 3,
        ReturnToStation = 4,
        CareCompleted = 5
    }
}
