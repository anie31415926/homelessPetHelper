namespace HomelessPetHelper.Modules.Outing
{
    /// <summary>
    /// Phase owned by one outing session. The map layer only covers point selection;
    /// on-site approach/negotiation/rescue remain owned by the existing Rescue module.
    /// A session is fixed at construction and never resets through ordinary operations.
    /// </summary>
    public enum OutingStage
    {
        MapSelecting = 0,
        Departed = 1,
        Settled = 2
    }
}
