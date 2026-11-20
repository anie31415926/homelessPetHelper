using System;
using HomelessPetHelper.Game;
using UnityEditor;

namespace HomelessPetHelper.Editor
{
    public static class RuntimePreviewContent
    {
        public const string Path = "Assets/Settings/Game/GameContent.asset";
        public static GameContent Load() => AssetDatabase.LoadAssetAtPath<GameContent>(Path) ??
            throw new InvalidOperationException("Author the saved Main game content first.");
    }
}
