using HomelessPetHelper.Modules.Outing.UI;
using UnityEditor;

namespace HomelessPetHelper.Editor.CityMap
{
    /// <summary>Expose the map's saved size and artwork binding alongside inherited scroll settings.</summary>
    [CustomEditor(typeof(CityMapScrollRect))]
    public sealed class CityMapScrollRectInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI() => DrawDefaultInspector();
    }
}
