using HomelessPetHelper.Modules.Rescue.UI;
using UnityEditor;

namespace HomelessPetHelper.Editor.RescueCase
{
    [CustomEditor(typeof(HoldActionButton)), CanEditMultipleObjects]
    public sealed class HoldActionButtonEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            // The inherited click event is intentionally unused; confirmation is owned by the option panel.
            DrawPropertiesExcluding(serializedObject, "m_Script", "m_OnClick");
            serializedObject.ApplyModifiedProperties();
        }
    }
}
