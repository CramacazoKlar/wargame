using System.IO;
using UnityEngine;
using UnityEditor;
namespace CampaignDemo.Editor
{
    public static class StaffObjectivePrefabBuilder
    {
        [MenuItem("Tools/态势地图/重建圆环据点预制体")]
        public static void Build()
        {
            const string folder="Assets/Game/StaffMapObjectives";Directory.CreateDirectory(folder);
            string image=folder+"/ObjectiveRing64.png";File.WriteAllBytes(image,StaffObjectiveMarker.Circle(64).texture.EncodeToPNG());AssetDatabase.ImportAsset(image);
            var importer=(TextureImporter)AssetImporter.GetAtPath(image);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;importer.spritePixelsPerUnit=100;importer.SaveAndReimport();var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(image);
            foreach(string side in new[]{"Friendly","Enemy"})
            {
                string path="Assets/Game/UnitLibrary/Prefabs/"+side+"/City.prefab";var root=PrefabUtility.LoadPrefabContents(path);
                try{var marker=root.GetComponent<StaffObjectiveMarker>();if(marker==null)marker=root.AddComponent<StaffObjectiveMarker>();marker.diameterPixels=24;var r=root.GetComponent<SpriteRenderer>();r.sprite=sprite;r.sharedMaterial=AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");r.color=side=="Friendly"?StaffMapStyle.Blue:StaffMapStyle.Red;r.drawMode=SpriteDrawMode.Sliced;var scale=BattleUnitView.SymbolScale(root.transform);r.size=new Vector2(.55f/scale.x,.55f/scale.y);PrefabUtility.SaveAsPrefabAsset(root,path);}
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        }
    }
}

