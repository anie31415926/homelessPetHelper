using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HomelessPetHelper.Editor
{
    public static class RuntimeMigration
    {
        public static void MoveAssets()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated snapshot.");
            var moves = new List<string[]>();
            void Move(string from, string to)
            {
                if (AssetDatabase.LoadMainAssetAtPath(from) == null)
                {
                    if (AssetDatabase.LoadMainAssetAtPath(to) != null) return;
                    throw new InvalidOperationException("Missing migration source and target: " + from);
                }
                if (AssetDatabase.LoadMainAssetAtPath(to) != null)
                    throw new InvalidOperationException("Migration target already exists: " + to);
                EnsureFolder(Path.GetDirectoryName(to).Replace('\\', '/'));
                string error = AssetDatabase.MoveAsset(from, to);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
                moves.Add(new[] { from, to });
            }
            Move("Assets/EditorScenes/CityMapArt", "Assets/Art/CityMap");
            Move("Assets/EditorScenes/StationCareArt", "Assets/Art/Station");
            foreach (string name in new[] { "CityMapConfig", "CityMapText", "StationPreviewText" })
                Move("Assets/EditorScenes/" + name + ".json", "Assets/Settings/Game/" + name + ".json");
            foreach (string name in new[] { "SiegeTrialData", "SiegeTrialText" })
                Move("Assets/EditorScenes/Siege/" + name + ".json", "Assets/Settings/Siege/" + name + ".json");
            foreach (string file in Directory.GetFiles("Assets/EditorScenes/PetMotion"))
            {
                string name = Path.GetFileName(file);
                if (name.EndsWith(".meta") || name.EndsWith(".json") || name.EndsWith(".unity")) continue;
                Move(file.Replace('\\', '/'), "Assets/Art/PetMotion/" + name);
            }
            foreach (string name in new[] { "CityMapPresenter", "CityMapPreviewConfig", "CityMapPreviewConfigData" })
                Move("Assets/Scripts/Editor/CityMap/" + name + ".cs", "Assets/Scripts/Game/CityMap/" + name + ".cs");
            Move("Assets/Scripts/Editor/OptionPreview/OptionPreviewText.cs",
                "Assets/Scripts/Game/Text/OptionPreviewText.cs");
            foreach (string name in new[] { "StationPreviewSession", "StationResidentPresentation",
                "StationCareTool", "StationContactSample" })
                Move("Assets/Scripts/Editor/StationPreview/" + name + ".cs",
                    "Assets/Scripts/Game/Station/" + name + ".cs");
            AssetDatabase.SaveAssets();
            File.WriteAllText("RuntimeMigrationMoves.json", "[" + string.Join(",", moves.ConvertAll(pair =>
                "[\"" + pair[0] + "\",\"" + pair[1] + "\"]")) + "]");
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
