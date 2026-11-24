namespace HomelessPetHelper.Editor.StationHabitat
{
    /// <summary>Compatibility entry points for the original orange cat authoring workflow.</summary>
    public static class StationCatAssets
    {
        public const string Folder = "Assets/Art/PetSit";
        private const string BakePath = "PetSitSource/unity-export.json";
        public static void Apply() => new StationAnimalAssets(BakePath).Apply();
        public static void UpdateExpressionBindings() => new StationAnimalAssets(BakePath).UpdateExpressionBindings();
        public static void UpdateSitClip() => new StationAnimalAssets(BakePath).UpdateSitClip();
        public static void NormalizeSavedPrefab() => new StationAnimalAssets(BakePath).NormalizeSavedPrefab();
    }
}
