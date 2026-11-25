using System.IO;
using HomelessPetHelper.Modules.PetMotion;
using UnityEditor;
using UnityEngine;

namespace HomelessPetHelper.Editor.PetMotion
{
    public static class PetPortraitBaker
    {
        [MenuItem("Homeless Pet Helper/Art/Bake Cat Portrait Motion")]
        public static void Bake()
        {
            const int side = 128, columns = 4, rows = 3;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PetMotionAssets.RigPath);
            var rig = Object.Instantiate(prefab, new Vector3(-16000, 0, 0), Quaternion.identity);
            var camera = rig.GetComponentInChildren<Camera>(true);
            var target = new RenderTexture(side, side, 0, RenderTextureFormat.ARGB32);
            var atlas = new Texture2D(side * columns, side * rows, TextureFormat.RGBA32, false);
            var active = RenderTexture.active;
            try
            {
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.orthographicSize = 320;
                camera.aspect = 1;
                camera.targetTexture = target;
                var player = rig.GetComponent<PetMotionPlayer>();
                player.SendMessage("Awake");
                player.Select(1);
                RenderTexture.active = target;
                for (int frame = 0; frame < columns * rows; frame++)
                {
                    player.Seek((float)frame / (columns * rows));
                    camera.Render();
                    atlas.ReadPixels(new Rect(0, 0, side, side), frame % columns * side,
                        (rows - 1 - frame / columns) * side, false);
                }
                atlas.Apply();
                string path = PetMotionAssets.Folder + "/T_CatPortraitMotion.png";
                File.WriteAllBytes(path, atlas.EncodeToPNG());
                AssetDatabase.ImportAsset(path);
            }
            finally
            {
                RenderTexture.active = active;
                Object.DestroyImmediate(rig);
                Object.DestroyImmediate(atlas);
                Object.DestroyImmediate(target);
            }
        }
    }
}
