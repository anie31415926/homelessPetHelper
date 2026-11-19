namespace HomelessPetHelper.Modules.Station.Cleaning
{
    public interface IReadOnlyDirtField
    {
        int Width { get; }
        int Height { get; }
        long InitialAmount { get; }
        long RemainingAmount { get; }
        double RemovedFraction { get; }
        byte Sample(int index);
        bool ContainsCoat(float x, float y);
        bool HasResidueAt(float x, float y);
    }
}
